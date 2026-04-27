using System.Globalization;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using Starter.Module.AI.Application.Services.Costs;

namespace Starter.Module.AI.Infrastructure.Services.Costs;

/// <summary>
/// Redis sorted-set sliding-window rate limiter, scoped per agent. Like the cap accountant,
/// bypasses `ICacheService` because sorted-set ops aren't on that wrapper. Keys auto-expire
/// after the window so abandoned agents don't leak memory.
/// </summary>
internal sealed class RedisAgentRateLimiter(
    IConnectionMultiplexer multiplexer,
    ILogger<RedisAgentRateLimiter> logger) : IAgentRateLimiter
{
    // KEYS[1] = sorted-set key
    // ARGV[1] = current epoch ms
    // ARGV[2] = window-start epoch ms (now - 60000)
    // ARGV[3] = rpm cap
    // ARGV[4] = ttl seconds (window + margin)
    // ARGV[5] = unique member id (epoch_ms-uuid)
    private const string AcquireScript = @"
        redis.call('ZREMRANGEBYSCORE', KEYS[1], 0, tonumber(ARGV[2]))
        local count = redis.call('ZCARD', KEYS[1])
        if count >= tonumber(ARGV[3]) then
            return 0
        end
        redis.call('ZADD', KEYS[1], ARGV[1], ARGV[5])
        redis.call('EXPIRE', KEYS[1], ARGV[4])
        return 1
    ";

    public async Task<bool> TryAcquireAsync(Guid assistantId, int rpm, CancellationToken ct = default)
    {
        if (rpm <= 0)
            return false;     // 0 RPM means "blocked" — consistent with Free plan default

        var db = multiplexer.GetDatabase();
        var key = $"ai:rate:{assistantId}";
        var nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var windowStartMs = nowMs - 60_000L;
        var memberId = $"{nowMs}-{Guid.NewGuid():N}";

        try
        {
            var raw = (long)(await db.ScriptEvaluateAsync(
                AcquireScript,
                keys: [(RedisKey)key],
                values:
                [
                    (RedisValue)nowMs.ToString(CultureInfo.InvariantCulture),
                    (RedisValue)windowStartMs.ToString(CultureInfo.InvariantCulture),
                    (RedisValue)rpm.ToString(CultureInfo.InvariantCulture),
                    (RedisValue)"90",
                    (RedisValue)memberId
                ]));
            return raw == 1;
        }
        catch (RedisException ex)
        {
            logger.LogError(ex,
                "Redis unavailable during rate-limit check for assistant {Assistant}; failing closed.",
                assistantId);
            return false;
        }
    }
}
