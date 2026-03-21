using System.Text.Json;
using Starter.Application.Common.Interfaces;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;

namespace Starter.Infrastructure.Services;

public sealed class CacheService : ICacheService
{
    private readonly IDistributedCache _cache;
    private readonly ILogger<CacheService> _logger;

    private static readonly TimeSpan DefaultExpiration = TimeSpan.FromMinutes(30);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public CacheService(IDistributedCache cache, ILogger<CacheService> logger)
    {
        _cache = cache;
        _logger = logger;
    }

    public async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            var cachedValue = await _cache.GetStringAsync(key, cancellationToken);

            if (cachedValue is null)
                return default;

            return JsonSerializer.Deserialize<T>(cachedValue, JsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error retrieving cache key: {Key}", key);
            return default;
        }
    }

    public async Task SetAsync<T>(
        string key,
        T value,
        TimeSpan? expiration = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var serializedValue = JsonSerializer.Serialize(value, JsonOptions);

            var options = new DistributedCacheEntryOptions();

            if (expiration.HasValue)
            {
                options.AbsoluteExpirationRelativeToNow = expiration.Value;
            }
            else
            {
                options.AbsoluteExpirationRelativeToNow = DefaultExpiration;
            }

            await _cache.SetStringAsync(key, serializedValue, options, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error setting cache key: {Key}", key);
        }
    }

    public async Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            await _cache.RemoveAsync(key, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error removing cache key: {Key}", key);
        }
    }

    public async Task RemoveByPrefixAsync(string prefix, CancellationToken cancellationToken = default)
    {
        // Note: IDistributedCache does not natively support prefix-based removal.
        // For Redis-specific prefix removal, consider using IConnectionMultiplexer directly.
        // This implementation logs a warning since the operation requires Redis-specific commands.
        _logger.LogDebug(
            "RemoveByPrefixAsync is a no-op with IDistributedCache. " +
            "Inject IConnectionMultiplexer for Redis prefix removal. Prefix: {Prefix}",
            prefix);

        await Task.CompletedTask;
    }

    public async Task<T> GetOrSetAsync<T>(
        string key,
        Func<Task<T>> factory,
        TimeSpan? expiration = null,
        CancellationToken cancellationToken = default)
    {
        var cached = await GetAsync<T>(key, cancellationToken);

        if (cached is not null)
            return cached;

        var value = await factory();

        await SetAsync(key, value, expiration, cancellationToken);

        return value;
    }

    public async Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            var value = await _cache.GetAsync(key, cancellationToken);
            return value is not null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error checking cache key existence: {Key}", key);
            return false;
        }
    }
}
