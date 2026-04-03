using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Starter.Application.Common.Interfaces;
using StackExchange.Redis;

namespace Starter.Infrastructure.Services;

internal sealed class UsageTrackerService(
    IApplicationDbContext context,
    ILogger<UsageTrackerService> logger,
    IConnectionMultiplexer? multiplexer = null) : IUsageTracker
{
    private static readonly TimeSpan Ttl = TimeSpan.FromHours(24);

    private static readonly string[] SupportedMetrics =
    [
        "users",
        "storage_bytes",
        "api_keys",
        "reports_active",
        "webhooks"
    ];

    private static string BuildKey(Guid tenantId, string metric) =>
        $"usage:{tenantId}:{metric}";

    public async Task<long> GetAsync(Guid tenantId, string metric, CancellationToken ct = default)
    {
        var key = BuildKey(tenantId, metric);

        if (multiplexer is not null)
        {
            var db = multiplexer.GetDatabase();
            var value = await db.StringGetAsync(key);
            if (value.HasValue && long.TryParse((string?)value, out var cached))
                return cached;
        }

        return await RebuildAndReturnAsync(tenantId, metric, ct);
    }

    public async Task IncrementAsync(Guid tenantId, string metric, long amount = 1, CancellationToken ct = default)
    {
        var key = BuildKey(tenantId, metric);

        if (multiplexer is not null)
        {
            var db = multiplexer.GetDatabase();

            var exists = await db.KeyExistsAsync(key);
            if (!exists)
                await RebuildAndReturnAsync(tenantId, metric, ct);

            await db.StringIncrementAsync(key, amount);
            await db.KeyExpireAsync(key, Ttl);
        }
        else
        {
            logger.LogDebug("UsageTrackerService: No Redis multiplexer available, skipping increment for '{Metric}'", metric);
        }
    }

    public async Task DecrementAsync(Guid tenantId, string metric, long amount = 1, CancellationToken ct = default)
    {
        var key = BuildKey(tenantId, metric);

        if (multiplexer is not null)
        {
            var db = multiplexer.GetDatabase();

            var exists = await db.KeyExistsAsync(key);
            if (!exists)
                await RebuildAndReturnAsync(tenantId, metric, ct);

            var newValue = await db.StringDecrementAsync(key, amount);
            await db.KeyExpireAsync(key, Ttl);

            if (newValue < 0)
            {
                await db.StringSetAsync(key, 0, Ttl);
            }
        }
        else
        {
            logger.LogDebug("UsageTrackerService: No Redis multiplexer available, skipping decrement for '{Metric}'", metric);
        }
    }

    public async Task SetAsync(Guid tenantId, string metric, long value, CancellationToken ct = default)
    {
        var key = BuildKey(tenantId, metric);

        if (multiplexer is not null)
        {
            var db = multiplexer.GetDatabase();
            await db.StringSetAsync(key, value, Ttl);
        }
        else
        {
            logger.LogDebug("UsageTrackerService: No Redis multiplexer available, skipping set for '{Metric}'", metric);
        }
    }

    public async Task<Dictionary<string, long>> GetAllAsync(Guid tenantId, CancellationToken ct = default)
    {
        var result = new Dictionary<string, long>(SupportedMetrics.Length);

        foreach (var metric in SupportedMetrics)
        {
            result[metric] = await GetAsync(tenantId, metric, ct);
        }

        return result;
    }

    private async Task<long> RebuildAndReturnAsync(Guid tenantId, string metric, CancellationToken ct)
    {
        var value = await ComputeFromDbAsync(tenantId, metric, ct);
        var key = BuildKey(tenantId, metric);

        if (multiplexer is not null)
        {
            var db = multiplexer.GetDatabase();
            await db.StringSetAsync(key, value, Ttl);
        }

        return value;
    }

    private async Task<long> ComputeFromDbAsync(Guid tenantId, string metric, CancellationToken ct)
    {
        return metric switch
        {
            "users" => await context.Users
                .IgnoreQueryFilters()
                .CountAsync(u => u.TenantId == tenantId, ct),

            "storage_bytes" => await context.FileMetadata
                .IgnoreQueryFilters()
                .Where(f => f.TenantId == tenantId)
                .SumAsync(f => f.Size, ct),

            "api_keys" => await context.ApiKeys
                .IgnoreQueryFilters()
                .CountAsync(k => k.TenantId == tenantId && !k.IsRevoked, ct),

            "reports_active" => await context.ReportRequests
                .IgnoreQueryFilters()
                .CountAsync(r => r.TenantId == tenantId, ct),

            "webhooks" => await context.WebhookEndpoints
                .IgnoreQueryFilters()
                .CountAsync(w => w.TenantId == tenantId, ct),

            _ => throw new ArgumentOutOfRangeException(nameof(metric), $"Unknown usage metric: '{metric}'")
        };
    }
}
