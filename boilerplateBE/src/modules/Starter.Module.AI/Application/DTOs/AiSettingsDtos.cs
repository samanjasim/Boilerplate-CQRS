using Starter.Abstractions.Ai;
using Starter.Module.AI.Domain.Enums;

namespace Starter.Module.AI.Application.DTOs;

public sealed record AiEntitlementsDto(
    decimal TotalMonthlyUsd,
    decimal TotalDailyUsd,
    decimal PlatformMonthlyUsd,
    decimal PlatformDailyUsd,
    int RequestsPerMinute,
    bool ByokEnabled,
    bool WidgetsEnabled,
    int WidgetMaxCount,
    int WidgetMonthlyTokens,
    int WidgetDailyTokens,
    int WidgetRequestsPerMinute,
    IReadOnlyList<string> AllowedProviders,
    IReadOnlyList<string> AllowedModels);

public sealed record AiTenantSettingsDto(
    Guid TenantId,
    ProviderCredentialPolicy RequestedProviderCredentialPolicy,
    ProviderCredentialPolicy EffectiveProviderCredentialPolicy,
    SafetyPreset DefaultSafetyPreset,
    decimal? MonthlyCostCapUsd,
    decimal? DailyCostCapUsd,
    decimal? PlatformMonthlyCostCapUsd,
    decimal? PlatformDailyCostCapUsd,
    int? RequestsPerMinute,
    int? PublicMonthlyTokenCap,
    int? PublicDailyTokenCap,
    int? PublicRequestsPerMinute,
    string? AssistantDisplayName,
    string? Tone,
    Guid? AvatarFileId,
    string? BrandInstructions,
    AiEntitlementsDto Entitlements);
