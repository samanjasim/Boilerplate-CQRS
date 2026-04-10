using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Starter.Application.Common.Interfaces;
using Starter.Domain.FeatureFlags.Entities;

namespace Starter.Infrastructure.Services;

internal sealed class FeatureFlagService(
    IApplicationDbContext context,
    ICacheService cache,
    ICurrentUserService currentUser) : IFeatureFlagService
{
    private const string CachePrefix = "ff";
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);

    public async Task<bool> IsEnabledAsync(string key, CancellationToken cancellationToken = default)
    {
        var value = await GetResolvedValueAsync(key, cancellationToken);
        return bool.TryParse(value, out var result) && result;
    }

    public async Task<T> GetValueAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        var value = await GetResolvedValueAsync(key, cancellationToken);
        return JsonSerializer.Deserialize<T>(value)!;
    }

    public async Task<Dictionary<string, string>> GetAllResolvedAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = currentUser.TenantId;
        var cacheKey = tenantId.HasValue ? $"{CachePrefix}:{tenantId}" : $"{CachePrefix}:platform";

        return await cache.GetOrSetAsync(
            cacheKey,
            async () => await BuildResolvedMapAsync(tenantId, cancellationToken),
            CacheTtl,
            cancellationToken);
    }

    public async Task InvalidateCacheAsync(Guid? tenantId = null, CancellationToken cancellationToken = default)
    {
        // Remove specific known cache keys via IDistributedCache (works without Redis)
        if (tenantId.HasValue)
            await cache.RemoveAsync($"{CachePrefix}:{tenantId}", cancellationToken);

        await cache.RemoveAsync($"{CachePrefix}:platform", cancellationToken);

        // Also attempt Redis prefix scan for any other keys
        await cache.RemoveByPrefixAsync(CachePrefix, cancellationToken);
    }

    private async Task<string> GetResolvedValueAsync(string key, CancellationToken cancellationToken)
    {
        var allFlags = await GetAllResolvedAsync(cancellationToken);
        return allFlags.TryGetValue(key, out var value)
            ? value
            : throw new KeyNotFoundException($"Feature flag '{key}' not found.");
    }

    private async Task<Dictionary<string, string>> BuildResolvedMapAsync(
        Guid? tenantId, CancellationToken cancellationToken)
    {
        var flags = await context.Set<FeatureFlag>()
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var overrides = tenantId.HasValue
            ? await context.Set<TenantFeatureFlag>()
                .AsNoTracking()
                .Where(t => t.TenantId == tenantId.Value)
                .ToDictionaryAsync(t => t.FeatureFlagId, t => t.Value, cancellationToken)
            : new Dictionary<Guid, string>();

        return flags.ToDictionary(
            f => f.Key,
            f => overrides.TryGetValue(f.Id, out var ov) ? ov : f.DefaultValue);
    }
}
