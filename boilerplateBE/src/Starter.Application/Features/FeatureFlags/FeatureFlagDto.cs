using Starter.Domain.FeatureFlags.Enums;

namespace Starter.Application.Features.FeatureFlags;

public sealed record FeatureFlagDto(
    Guid Id,
    string Key,
    string Name,
    string? Description,
    string DefaultValue,
    FlagValueType ValueType,
    FlagCategory Category,
    bool IsSystem,
    string? TenantOverrideValue,
    string ResolvedValue,
    DateTime CreatedAt,
    DateTime? ModifiedAt);
