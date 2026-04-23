using Starter.Application.Common.Interfaces;

namespace Starter.Api.Tests.Ai.Retrieval;

/// <summary>
/// <see cref="ICacheService"/> fake that throws on every call. Used to verify
/// graceful-degradation behavior when Redis is unavailable.
/// </summary>
internal sealed class ThrowingCacheService : ICacheService
{
    public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
        => throw new InvalidOperationException("cache unavailable");

    public Task SetAsync<T>(string key, T value, TimeSpan? expiration = null, CancellationToken cancellationToken = default)
        => throw new InvalidOperationException("cache unavailable");

    public Task RemoveAsync(string key, CancellationToken cancellationToken = default)
        => throw new InvalidOperationException("cache unavailable");

    public Task RemoveByPrefixAsync(string prefix, CancellationToken cancellationToken = default)
        => throw new InvalidOperationException("cache unavailable");

    public Task<T> GetOrSetAsync<T>(string key, Func<Task<T>> factory, TimeSpan? expiration = null, CancellationToken cancellationToken = default)
        => throw new InvalidOperationException("cache unavailable");

    public Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
        => throw new InvalidOperationException("cache unavailable");
}
