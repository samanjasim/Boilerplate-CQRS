using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using Starter.Module.AI.Application.Services.Costs;
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

        var monthStart = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);
        var dayStart = DateTime.UtcNow.Date;

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
            var key = $"ai:cost:{t.TenantId!.Value}:{t.AssistantId!.Value}:monthly:{DateTimeOffset.UtcNow:yyyy-MM}";
            await redis.StringSetAsync(key, t.Cost.ToString(System.Globalization.CultureInfo.InvariantCulture));
            await redis.KeyExpireAsync(key, TimeSpan.FromDays(35));
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
            var key = $"ai:cost:{t.TenantId!.Value}:{t.AssistantId!.Value}:daily:{DateTimeOffset.UtcNow:yyyy-MM-dd}";
            await redis.StringSetAsync(key, t.Cost.ToString(System.Globalization.CultureInfo.InvariantCulture));
            await redis.KeyExpireAsync(key, TimeSpan.FromHours(36));
        }

        logger.LogInformation(
            "AiCostReconciliationJob updated {MonthlyCount} monthly + {DailyCount} daily counters from AiUsageLog truth.",
            monthlyTotals.Count, dailyTotals.Count);
    }
}
