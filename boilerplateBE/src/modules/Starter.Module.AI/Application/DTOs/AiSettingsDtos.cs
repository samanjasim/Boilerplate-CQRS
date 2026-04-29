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

public sealed record AiProviderCredentialDto(
    Guid Id,
    AiProviderType Provider,
    string DisplayName,
    string MaskedKey,
    ProviderCredentialStatus Status,
    DateTimeOffset? LastValidatedAt,
    DateTimeOffset? LastUsedAt,
    DateTime CreatedAt);

public sealed record AiModelDefaultDto(
    Guid Id,
    Guid? TenantId,
    AiAgentClass AgentClass,
    AiProviderType Provider,
    string Model,
    int? MaxTokens,
    double? Temperature);

public sealed record AiPublicWidgetDto(
    Guid Id,
    string Name,
    AiPublicWidgetStatus Status,
    IReadOnlyList<string> AllowedOrigins,
    Guid? DefaultAssistantId,
    string DefaultPersonaSlug,
    int? MonthlyTokenCap,
    int? DailyTokenCap,
    int? RequestsPerMinute,
    string? MetadataJson,
    DateTime CreatedAt);

public sealed record AiWidgetCredentialDto(
    Guid Id,
    Guid WidgetId,
    string KeyPrefix,
    string MaskedKey,
    AiWidgetCredentialStatus Status,
    DateTimeOffset? ExpiresAt,
    DateTimeOffset? LastUsedAt,
    DateTime CreatedAt);

public sealed record CreateAiWidgetCredentialResponse(
    AiWidgetCredentialDto Credential,
    string FullKey);
