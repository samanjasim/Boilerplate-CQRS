using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using Starter.Module.AI.Application.Services.Costs;
using Starter.Module.AI.Domain.Enums;
using Starter.Module.AI.Infrastructure.Persistence;

namespace Starter.Module.AI.Infrastructure.Background;

/// <summary>
/// Nightly reconciliation: rebuilds the Redis cost-cap counters from the authoritative
/// `AiUsageLog` ground truth so that any drift caused by crashed runs or rollback failures
/// is corrected. Runs every 24 h after a 5-minute startup delay so the job doesn't
/// race with module init.
///
/// Plan 5d-1: optional in scope, but the spec calls for ground-truth reconciliation
/// to back the Redis hot path. This implementation is the minimum needed to keep
/// counters honest in production. Manual trigger is also supported via DI lookup.
/// </summary>
internal sealed class AiCostReconciliationJob(
    IServiceScopeFactory scopeFactory,
    IConnectionMultiplexer multiplexer,
    ILogger<AiCostReconciliationJob> logger) : BackgroundService
{
    private static readonly TimeSpan StartupDelay = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan Interval = TimeSpan.FromHours(24);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(StartupDelay, stoppingToken);
        }
        catch (TaskCanceledException)
        {
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ReconcileAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "AiCostReconciliationJob iteration failed; retrying after interval.");
            }

            try
            {
                await Task.Delay(Interval, stoppingToken);
            }
            catch (TaskCanceledException)
            {
                return;
            }
        }
    }

    internal async Task ReconcileAsync(CancellationToken ct)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AiDbContext>();
        var redis = multiplexer.GetDatabase();

        var nowUtc = DateTime.UtcNow;
        var monthStart = new DateTime(nowUtc.Year, nowUtc.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var dayStart = DateTime.SpecifyKind(nowUtc.Date, DateTimeKind.Utc);

        // Monthly truth per (TenantId, AssistantId)
        var monthlyTotals = await db.AiUsageLogs
            .AsNoTracking()
            .Where(l => l.AiAssistantId != null && l.TenantId != null && l.CreatedAt >= monthStart)
            .GroupBy(l => new { l.TenantId, l.AiAssistantId })
            .Select(g => new
            {
                g.Key.TenantId,
                AssistantId = g.Key.AiAssistantId,
                Cost = g.Sum(x => x.EstimatedCost)
            })
            .ToListAsync(ct);

        foreach (var t in monthlyTotals)
        {
            await SetCounterAsync(redis, t.TenantId!.Value, t.AssistantId!.Value, CapWindow.Monthly, CostCapBucket.Total, t.Cost, TimeSpan.FromDays(35));
        }

        var monthlyPlatformTotals = await db.AiUsageLogs
            .AsNoTracking()
            .Where(l => l.AiAssistantId != null
                && l.TenantId != null
                && l.CreatedAt >= monthStart
                && l.ProviderCredentialSource == ProviderCredentialSource.Platform)
            .GroupBy(l => new { l.TenantId, l.AiAssistantId })
            .Select(g => new
            {
                g.Key.TenantId,
                AssistantId = g.Key.AiAssistantId,
                Cost = g.Sum(x => x.EstimatedCost)
            })
            .ToListAsync(ct);

        foreach (var t in monthlyPlatformTotals)
        {
            await SetCounterAsync(redis, t.TenantId!.Value, t.AssistantId!.Value, CapWindow.Monthly, CostCapBucket.PlatformCredit, t.Cost, TimeSpan.FromDays(35));
        }

        // Daily truth per (TenantId, AssistantId)
        var dailyTotals = await db.AiUsageLogs
            .AsNoTracking()
            .Where(l => l.AiAssistantId != null && l.TenantId != null && l.CreatedAt >= dayStart)
            .GroupBy(l => new { l.TenantId, l.AiAssistantId })
            .Select(g => new
            {
                g.Key.TenantId,
                AssistantId = g.Key.AiAssistantId,
                Cost = g.Sum(x => x.EstimatedCost)
            })
            .ToListAsync(ct);

        foreach (var t in dailyTotals)
        {
            await SetCounterAsync(redis, t.TenantId!.Value, t.AssistantId!.Value, CapWindow.Daily, CostCapBucket.Total, t.Cost, TimeSpan.FromHours(36));
        }

        var dailyPlatformTotals = await db.AiUsageLogs
            .AsNoTracking()
            .Where(l => l.AiAssistantId != null
                && l.TenantId != null
                && l.CreatedAt >= dayStart
                && l.ProviderCredentialSource == ProviderCredentialSource.Platform)
            .GroupBy(l => new { l.TenantId, l.AiAssistantId })
            .Select(g => new
            {
                g.Key.TenantId,
                AssistantId = g.Key.AiAssistantId,
                Cost = g.Sum(x => x.EstimatedCost)
            })
            .ToListAsync(ct);

        foreach (var t in dailyPlatformTotals)
        {
            await SetCounterAsync(redis, t.TenantId!.Value, t.AssistantId!.Value, CapWindow.Daily, CostCapBucket.PlatformCredit, t.Cost, TimeSpan.FromHours(36));
        }

        logger.LogInformation(
            "AiCostReconciliationJob updated {MonthlyCount} monthly + {DailyCount} daily + {MonthlyPlatformCount} platform monthly + {DailyPlatformCount} platform daily counters from AiUsageLog truth.",
            monthlyTotals.Count, dailyTotals.Count, monthlyPlatformTotals.Count, dailyPlatformTotals.Count);
    }

    private static async Task SetCounterAsync(
        IDatabase redis,
        Guid tenantId,
        Guid assistantId,
        CapWindow window,
        CostCapBucket bucket,
        decimal cost,
        TimeSpan ttl)
    {
        var key = WindowKey(tenantId, assistantId, window, bucket);
        await redis.StringSetAsync(key, cost.ToString(System.Globalization.CultureInfo.InvariantCulture));
        await redis.KeyExpireAsync(key, ttl);
    }

    private static string WindowKey(Guid tenantId, Guid assistantId, CapWindow window, CostCapBucket bucket)
    {
        var bucketName = bucket == CostCapBucket.PlatformCredit ? "platform-cost" : "cost";
        var windowValue = window switch
        {
            CapWindow.Monthly => $"{DateTimeOffset.UtcNow:yyyy-MM}",
            CapWindow.Daily => $"{DateTimeOffset.UtcNow:yyyy-MM-dd}",
            _ => throw new ArgumentOutOfRangeException(nameof(window))
        };

        return $"ai:{bucketName}:{tenantId}:{assistantId}:{window.ToString().ToLowerInvariant()}:{windowValue}";
    }
}
