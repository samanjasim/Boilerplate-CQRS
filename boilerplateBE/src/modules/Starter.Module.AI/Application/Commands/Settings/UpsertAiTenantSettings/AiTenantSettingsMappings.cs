using Starter.Module.AI.Application.DTOs;
using Starter.Module.AI.Domain.Entities;
using Starter.Module.AI.Domain.Enums;

namespace Starter.Module.AI.Application.Commands.Settings.UpsertAiTenantSettings;

internal static class AiTenantSettingsMappings
{
    public static AiTenantSettingsDto ToDto(
        AiTenantSettings settings,
        Guid tenantId,
        ProviderCredentialPolicy effectivePolicy,
        AiEntitlementsDto entitlements) =>
        new(
            tenantId,
            settings.RequestedProviderCredentialPolicy,
            effectivePolicy,
            settings.DefaultSafetyPreset,
            settings.MonthlyCostCapUsd,
            settings.DailyCostCapUsd,
            settings.PlatformMonthlyCostCapUsd,
            settings.PlatformDailyCostCapUsd,
            settings.RequestsPerMinute,
            settings.PublicMonthlyTokenCap,
            settings.PublicDailyTokenCap,
            settings.PublicRequestsPerMinute,
            settings.AssistantDisplayName,
            settings.Tone,
            settings.AvatarFileId,
            settings.BrandInstructions,
            entitlements);
}
