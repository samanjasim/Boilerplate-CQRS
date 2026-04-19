using System.Collections.Concurrent;
using Starter.Application.Common.Interfaces;

namespace Starter.Api.Tests.Ai.Fakes;

internal sealed class FakeCacheService : ICacheService
{
    // ConcurrentDictionary so parallel reranker stages can safely read/write.
    private readonly ConcurrentDictionary<string, object?> _store = new();

    public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
        => Task.FromResult((T?)(_store.TryGetValue(key, out var v) ? v : default));

    public Task SetAsync<T>(string key, T value, TimeSpan? expiration = null, CancellationToken cancellationToken = default)
    {
        _store[key] = value;
        return Task.CompletedTask;
    }

    public Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        _store.TryRemove(key, out _);
        return Task.CompletedTask;
    }

    public Task RemoveByPrefixAsync(string prefix, CancellationToken cancellationToken = default)
    {
        foreach (var k in _store.Keys.Where(k => k.StartsWith(prefix)).ToList())
            _store.TryRemove(k, out _);
        return Task.CompletedTask;
    }

    public async Task<T> GetOrSetAsync<T>(
        string key,
        Func<Task<T>> factory,
        TimeSpan? expiration = null,
        CancellationToken cancellationToken = default)
    {
        if (_store.TryGetValue(key, out var v) && v is T existing) return existing;
        var created = await factory();
        _store[key] = created;
        return created;
    }

    public Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
        => Task.FromResult(_store.ContainsKey(key));
}
