using System.Globalization;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using Starter.Module.AI.Application.Services.Costs;

using NS = System.Globalization.NumberStyles;

namespace Starter.Module.AI.Infrastructure.Services.Costs;

/// <summary>
/// Atomic cost-cap accountant using Redis Lua scripts. Bypasses the codebase's
/// `ICacheService` wrapper because that surface lacks `EvaluateAsync` for Lua execution.
/// This is the only place 5d-1 reaches `IConnectionMultiplexer` directly; the dependency
/// is sealed inside this single class. Behavior is covered end-to-end by the M2
/// concurrent-cost-cap acid test (real Redis from docker-compose).
/// </summary>
internal sealed class RedisCostCapAccountant(
    IConnectionMultiplexer multiplexer,
    ILogger<RedisCostCapAccountant> logger) : ICostCapAccountant
{
    // Atomic check-and-increment. KEYS[1]=counter key, ARGV[1]=cap, ARGV[2]=claim, ARGV[3]=ttl_seconds.
    // Returns {granted (0|1), current, cap} as bulk strings (Lua doesn't return decimal cleanly).
    private const string ClaimScript = @"
        local current = tonumber(redis.call('GET', KEYS[1]) or '0')
        local cap     = tonumber(ARGV[1])
        local claim   = tonumber(ARGV[2])
        if current + claim > cap then
            return {0, tostring(current), tostring(cap)}
        end
        redis.call('INCRBYFLOAT', KEYS[1], claim)
        redis.call('EXPIRE', KEYS[1], ARGV[3])
        return {1, tostring(current + claim), tostring(cap)}
    ";

    public async Task<ClaimResult> TryClaimAsync(
        Guid tenantId, Guid assistantId, decimal estimatedUsd,
        CapWindow window, decimal capUsd, CancellationToken ct = default)
    {
        if (estimatedUsd < 0)
            throw new ArgumentOutOfRangeException(nameof(estimatedUsd));
        if (capUsd < 0)
            throw new ArgumentOutOfRangeException(nameof(capUsd));

        var db = multiplexer.GetDatabase();
        var key = WindowKey(tenantId, assistantId, window);
        var ttl = WindowTtl(window);

        try
        {
            var raw = (RedisResult[]?)await db.ScriptEvaluateAsync(
                ClaimScript,
                keys: [(RedisKey)key],
                values:
                [
                    (RedisValue)capUsd.ToString(CultureInfo.InvariantCulture),
                    (RedisValue)estimatedUsd.ToString(CultureInfo.InvariantCulture),
                    (RedisValue)ttl.TotalSeconds.ToString("F0", CultureInfo.InvariantCulture)
                ]);
            if (raw is null || raw.Length != 3)
                throw new InvalidOperationException("Unexpected Redis Lua response shape.");

            var granted = ((int)raw[0]) == 1;
            var current = ParseDecimal((string)raw[1]!, CultureInfo.InvariantCulture);
            var cap = ParseDecimal((string)raw[2]!, CultureInfo.InvariantCulture);
            return new ClaimResult(granted, current, cap);
        }
        catch (RedisException ex)
        {
            logger.LogError(ex,
                "Redis unavailable during cost-cap claim for tenant {Tenant} assistant {Assistant}; failing closed.",
                tenantId, assistantId);
            // Fail closed: the agent run is blocked rather than risk over-spend.
            return new ClaimResult(Granted: false, CurrentUsd: 0m, CapUsd: capUsd);
        }
    }

    public async Task RollbackClaimAsync(
        Guid tenantId, Guid assistantId, decimal estimatedUsd,
        CapWindow window, CancellationToken ct = default)
    {
        if (estimatedUsd <= 0) return;
        var db = multiplexer.GetDatabase();
        var key = WindowKey(tenantId, assistantId, window);
        try
        {
            await db.StringIncrementAsync(key, (double)(-estimatedUsd));
        }
        catch (RedisException ex)
        {
            logger.LogWarning(ex,
                "Rollback failed for tenant {Tenant} assistant {Assistant} window {Window}; reconciliation job will correct drift.",
                tenantId, assistantId, window);
        }
    }

    public async Task RecordActualAsync(
        Guid tenantId, Guid assistantId, decimal deltaUsd,
        CapWindow window, CancellationToken ct = default)
    {
        if (deltaUsd == 0m) return;
        var db = multiplexer.GetDatabase();
        var key = WindowKey(tenantId, assistantId, window);
        try
        {
            await db.StringIncrementAsync(key, (double)deltaUsd);
        }
        catch (RedisException ex)
        {
            logger.LogWarning(ex,
                "RecordActual failed for tenant {Tenant} assistant {Assistant} window {Window}; reconciliation job will correct drift.",
                tenantId, assistantId, window);
        }
    }

    public async Task<decimal> GetCurrentAsync(
        Guid tenantId, Guid assistantId, CapWindow window, CancellationToken ct = default)
    {
        var db = multiplexer.GetDatabase();
        var key = WindowKey(tenantId, assistantId, window);
        try
        {
            var value = await db.StringGetAsync(key);
            return value.IsNullOrEmpty
                ? 0m
                : ParseDecimal((string)value!, CultureInfo.InvariantCulture);
        }
        catch (RedisException ex)
        {
            logger.LogWarning(ex,
                "GetCurrent failed for tenant {Tenant} assistant {Assistant}; returning 0.",
                tenantId, assistantId);
            return 0m;
        }
    }

    /// <summary>
    /// Lua's `tostring()` formats very small decimals in scientific notation (e.g. `6.96e-05`).
    /// Default `decimal.Parse` only accepts that with `NumberStyles.Float`. We accept any
    /// numeric format coming back from Redis; precision loss is bounded by Lua's double.
    /// </summary>
    private static decimal ParseDecimal(string value, CultureInfo culture) =>
        decimal.Parse(value, NS.Float | NS.AllowThousands, culture);

    private static string WindowKey(Guid tenantId, Guid assistantId, CapWindow window)
    {
        var now = DateTimeOffset.UtcNow;
        var bucket = window switch
        {
            CapWindow.Daily => now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            CapWindow.Monthly => now.ToString("yyyy-MM", CultureInfo.InvariantCulture),
            _ => throw new ArgumentOutOfRangeException(nameof(window))
        };
        return $"ai:cost:{tenantId}:{assistantId}:{window.ToString().ToLowerInvariant()}:{bucket}";
    }

    private static TimeSpan WindowTtl(CapWindow window) => window switch
    {
        // Conservative TTLs that span the full window plus margin to handle clock skew + late writes.
        CapWindow.Daily => TimeSpan.FromHours(36),
        CapWindow.Monthly => TimeSpan.FromDays(35),
        _ => throw new ArgumentOutOfRangeException(nameof(window))
    };
}
