using System.Collections.Concurrent;
using Starter.Application.Common.Interfaces;

namespace Starter.Api.Tests.Access._Helpers;

public sealed class InMemoryCache : ICacheService
{
    private readonly ConcurrentDictionary<string, object?> _store = new();

    public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        if (_store.TryGetValue(key, out var v) && v is T t)
            return Task.FromResult<T?>(t);
        return Task.FromResult<T?>(default);
    }

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
        foreach (var k in _store.Keys.Where(k => k.StartsWith(prefix, StringComparison.Ordinal)).ToList())
            _store.TryRemove(k, out _);
        return Task.CompletedTask;
    }

    public async Task<T> GetOrSetAsync<T>(string key, Func<Task<T>> factory, TimeSpan? expiration = null, CancellationToken cancellationToken = default)
    {
        var existing = await GetAsync<T>(key, cancellationToken);
        if (existing is not null) return existing;
        var value = await factory();
        await SetAsync(key, value, expiration, cancellationToken);
        return value;
    }

    public Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
        => Task.FromResult(_store.ContainsKey(key));
}
