using System.Collections.Concurrent;
using System.Text.Json;
using Starter.Application.Common.Interfaces;

namespace EvalCacheWarmup;

/// <summary>
/// Decorator around <see cref="ICacheService"/> that records every
/// <c>SetAsync</c> with a key matching <see cref="RecordedKeyPrefix"/>
/// so the eval harness can replay the exact cache contents that the
/// production pipeline produced during a warmup run.
/// </summary>
/// <remarks>
/// Values are serialised with the same <see cref="JsonSerializerOptions"/>
/// used by <c>CacheService</c> (camelCase) so that the recorded JSON is
/// byte-identical to what ends up in Redis.
/// </remarks>
public sealed class CacheRecordingService(ICacheService inner) : ICacheService
{
    public const string RecordedKeyPrefix = "ai:rerank:";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    private readonly ConcurrentDictionary<string, string> _captured = new();

    public IReadOnlyDictionary<string, string> Captured => _captured;

    public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
        => inner.GetAsync<T>(key, cancellationToken);

    public async Task SetAsync<T>(
        string key, T value, TimeSpan? expiration = null, CancellationToken cancellationToken = default)
    {
        if (value is not null && key.StartsWith(RecordedKeyPrefix, StringComparison.Ordinal))
        {
            _captured[key] = JsonSerializer.Serialize(value, JsonOptions);
        }
        await inner.SetAsync(key, value, expiration, cancellationToken);
    }

    public Task RemoveAsync(string key, CancellationToken cancellationToken = default)
        => inner.RemoveAsync(key, cancellationToken);

    public Task RemoveByPrefixAsync(string prefix, CancellationToken cancellationToken = default)
        => inner.RemoveByPrefixAsync(prefix, cancellationToken);

    public Task<T> GetOrSetAsync<T>(
        string key, Func<Task<T>> factory, TimeSpan? expiration = null, CancellationToken cancellationToken = default)
        => inner.GetOrSetAsync(key, factory, expiration, cancellationToken);

    public Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
        => inner.ExistsAsync(key, cancellationToken);
}
