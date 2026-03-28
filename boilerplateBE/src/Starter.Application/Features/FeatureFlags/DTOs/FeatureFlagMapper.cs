using Starter.Domain.FeatureFlags.Entities;

namespace Starter.Application.Features.FeatureFlags.DTOs;

public static class FeatureFlagMapper
{
    public static FeatureFlagDto ToDto(this FeatureFlag entity, string? tenantOverrideValue = null)
    {
        return new FeatureFlagDto(
            Id: entity.Id,
            Key: entity.Key,
            Name: entity.Name,
            Description: entity.Description,
            DefaultValue: entity.DefaultValue,
            ValueType: entity.ValueType,
            Category: entity.Category,
            IsSystem: entity.IsSystem,
            TenantOverrideValue: tenantOverrideValue,
            ResolvedValue: tenantOverrideValue ?? entity.DefaultValue,
            CreatedAt: entity.CreatedAt,
            ModifiedAt: entity.ModifiedAt);
    }
}
