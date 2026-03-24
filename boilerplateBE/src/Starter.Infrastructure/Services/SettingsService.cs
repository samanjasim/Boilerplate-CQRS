using Microsoft.EntityFrameworkCore;
using Starter.Application.Common.Interfaces;
using Starter.Application.Features.Settings.DTOs;
using Starter.Domain.Common;
using Starter.Infrastructure.Persistence;

namespace Starter.Infrastructure.Services;

internal sealed class SettingsService(
    ApplicationDbContext context,
    ICacheService cacheService) : ISettingsService
{
    private static string CacheKey(Guid? tenantId, string key)
        => $"settings:{tenantId?.ToString() ?? "platform"}:{key}";

    private static string CachePrefix(Guid? tenantId)
        => $"settings:{tenantId?.ToString() ?? "platform"}:";

    public async Task<string?> GetValueAsync(string key, Guid? tenantId = null, CancellationToken ct = default)
    {
        var cacheKey = CacheKey(tenantId, key);

        var cached = await cacheService.GetAsync<string>(cacheKey, ct);
        if (cached is not null)
            return cached;

        // Check tenant-specific first, then fall back to platform default
        string? value = null;

        if (tenantId.HasValue)
        {
            value = await context.SystemSettings
                .AsNoTracking()
                .IgnoreQueryFilters()
                .Where(s => s.Key == key && s.TenantId == tenantId)
                .Select(s => s.Value)
                .FirstOrDefaultAsync(ct);
        }

        value ??= await context.SystemSettings
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(s => s.Key == key && s.TenantId == null)
            .Select(s => s.Value)
            .FirstOrDefaultAsync(ct);

        if (value is not null)
        {
            await cacheService.SetAsync(cacheKey, value, TimeSpan.FromMinutes(30), ct);
        }

        return value;
    }

    public async Task<List<SystemSettingDto>> GetAllAsync(Guid? tenantId = null, CancellationToken ct = default)
    {
        // Get all platform defaults
        var platformDefaults = await context.SystemSettings
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(s => s.TenantId == null)
            .ToListAsync(ct);

        // Get tenant overrides if tenantId is provided
        var tenantOverrides = tenantId.HasValue
            ? await context.SystemSettings
                .AsNoTracking()
                .IgnoreQueryFilters()
                .Where(s => s.TenantId == tenantId)
                .ToListAsync(ct)
            : [];

        var tenantOverridesByKey = tenantOverrides.ToDictionary(s => s.Key);

        var result = new List<SystemSettingDto>();

        foreach (var platform in platformDefaults)
        {
            if (tenantOverridesByKey.TryGetValue(platform.Key, out var tenantOverride))
            {
                // Tenant has overridden this setting
                result.Add(new SystemSettingDto(
                    tenantOverride.Id,
                    tenantOverride.Key,
                    tenantOverride.IsSecret ? "••••••" : tenantOverride.Value,
                    platform.Description,
                    platform.Category,
                    tenantOverride.IsSecret,
                    platform.DataType,
                    IsOverridden: true));
            }
            else
            {
                // Use platform default
                result.Add(new SystemSettingDto(
                    platform.Id,
                    platform.Key,
                    platform.IsSecret ? "••••••" : platform.Value,
                    platform.Description,
                    platform.Category,
                    platform.IsSecret,
                    platform.DataType,
                    IsOverridden: false));
            }
        }

        return result;
    }

    public async Task SetValueAsync(string key, string value, Guid? tenantId = null, CancellationToken ct = default)
    {
        var existing = await context.SystemSettings
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(s => s.Key == key && s.TenantId == tenantId, ct);

        if (existing is not null)
        {
            existing.UpdateValue(value);
        }
        else
        {
            // Look up the platform default for description/category/isSecret metadata
            var platformDefault = tenantId.HasValue
                ? await context.SystemSettings
                    .AsNoTracking()
                    .IgnoreQueryFilters()
                    .FirstOrDefaultAsync(s => s.Key == key && s.TenantId == null, ct)
                : null;

            var setting = SystemSetting.Create(
                key,
                value,
                tenantId,
                platformDefault?.Description,
                platformDefault?.Category,
                platformDefault?.IsSecret ?? false);

            context.SystemSettings.Add(setting);
        }

        await context.SaveChangesAsync(ct);

        // Invalidate cache
        await cacheService.RemoveAsync(CacheKey(tenantId, key), ct);
    }
}
