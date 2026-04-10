using Microsoft.Extensions.Logging;
using Starter.Abstractions.Capabilities;
using Starter.Application.Common.Interfaces;
using StackExchange.Redis;

namespace Starter.Infrastructure.Services;

internal sealed class UsageTrackerService(
    IEnumerable<IUsageMetricCalculator> calculators,
    ILogger<UsageTrackerService> logger,
    IConnectionMultiplexer? multiplexer = null) : IUsageTracker
{
    private static readonly TimeSpan Ttl = TimeSpan.FromHours(24);

    // Snapshot the registered calculators once per scope. The source-of-truth
    // list of metrics is whatever calculators were registered in DI — core
    // contributes a handful; each installed module adds its own.
    private readonly Dictionary<string, IUsageMetricCalculator> _calculatorsByMetric =
        calculators.ToDictionary(c => c.Metric, StringComparer.Ordinal);

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
        // Iterate every metric that has a calculator registered. When a module
        // is absent, its calculator isn't registered, so its metric is omitted
        // from the result (rather than returning an unhelpful 0 for a metric
        // that doesn't exist in this build).
        var result = new Dictionary<string, long>(_calculatorsByMetric.Count, StringComparer.Ordinal);

        foreach (var metric in _calculatorsByMetric.Keys)
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
        // Dispatch to the registered calculator for this metric. If none is
        // registered, the metric's owning module is absent from the build —
        // return 0 silently. This replaces the hardcoded switch + try/catch
        // pattern the old implementation used.
        if (_calculatorsByMetric.TryGetValue(metric, out var calculator))
            return await calculator.CalculateAsync(tenantId, ct);

        logger.LogDebug(
            "No IUsageMetricCalculator registered for metric '{Metric}' — returning 0 (module likely not installed)",
            metric);
        return 0;
    }
}
