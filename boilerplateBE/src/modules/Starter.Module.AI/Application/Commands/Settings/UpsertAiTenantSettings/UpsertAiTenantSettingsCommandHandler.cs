using MediatR;
using Microsoft.EntityFrameworkCore;
using Starter.Application.Common.Interfaces;
using Starter.Module.AI.Application.DTOs;
using Starter.Module.AI.Application.Services.Costs;
using Starter.Module.AI.Application.Services.Settings;
using Starter.Module.AI.Domain.Entities;
using Starter.Module.AI.Domain.Enums;
using Starter.Module.AI.Domain.Errors;
using Starter.Module.AI.Infrastructure.Persistence;
using Starter.Shared.Results;

namespace Starter.Module.AI.Application.Commands.Settings.UpsertAiTenantSettings;

internal sealed class UpsertAiTenantSettingsCommandHandler(
    AiDbContext db,
    ICurrentUserService currentUser,
    IAiEntitlementResolver entitlements,
    ICostCapResolver costCaps) : IRequestHandler<UpsertAiTenantSettingsCommand, Result<AiTenantSettingsDto>>
{
    public async Task<Result<AiTenantSettingsDto>> Handle(UpsertAiTenantSettingsCommand request, CancellationToken ct)
    {
        var tenantId = currentUser.TenantId ?? request.TenantId;
        if (tenantId is null || tenantId == Guid.Empty)
            return Result.Failure<AiTenantSettingsDto>(
                Error.Validation("AiSettings.TenantIdRequired", "A tenant id is required to update AI tenant settings."));

        var resolvedEntitlements = await entitlements.ResolveAsync(tenantId.Value, ct);
        var validationResult = ValidateLimits(request, resolvedEntitlements);
        if (validationResult is not null)
            return Result.Failure<AiTenantSettingsDto>(validationResult);

        var settings = await db.AiTenantSettings
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(s => s.TenantId == tenantId.Value, ct);

        if (settings is null)
        {
            settings = AiTenantSettings.CreateDefault(tenantId.Value);
            db.AiTenantSettings.Add(settings);
        }

        try
        {
            settings.UpdatePolicy(request.RequestedProviderCredentialPolicy, request.DefaultSafetyPreset);
            settings.UpdateCostSelfLimits(
                request.MonthlyCostCapUsd,
                request.DailyCostCapUsd,
                request.PlatformMonthlyCostCapUsd,
                request.PlatformDailyCostCapUsd,
                request.RequestsPerMinute);
            settings.UpdatePublicWidgetDefaults(
                request.PublicMonthlyTokenCap,
                request.PublicDailyTokenCap,
                request.PublicRequestsPerMinute);
            settings.UpdateBrandProfile(
                request.AssistantDisplayName,
                request.Tone,
                request.AvatarFileId,
                request.BrandInstructions);
        }
        catch (ArgumentOutOfRangeException ex)
        {
            return Result.Failure<AiTenantSettingsDto>(
                Error.Validation("AiSettings.InvalidSelfLimit", $"AI setting '{ex.ParamName}' must be non-negative."));
        }

        await db.SaveChangesAsync(ct);
        await costCaps.InvalidateTenantAsync(tenantId.Value, ct);

        var effectivePolicy = resolvedEntitlements.ByokEnabled
            ? settings.RequestedProviderCredentialPolicy
            : ProviderCredentialPolicy.PlatformOnly;

        return Result.Success(AiTenantSettingsMappings.ToDto(settings, tenantId.Value, effectivePolicy, resolvedEntitlements));
    }

    private static Error? ValidateLimits(UpsertAiTenantSettingsCommand request, AiEntitlementsDto entitlements)
    {
        if (request.MonthlyCostCapUsd is { } monthly && monthly > entitlements.TotalMonthlyUsd)
            return AiSettingsErrors.SelfLimitExceedsEntitlement(nameof(request.MonthlyCostCapUsd));
        if (request.DailyCostCapUsd is { } daily && daily > entitlements.TotalDailyUsd)
            return AiSettingsErrors.SelfLimitExceedsEntitlement(nameof(request.DailyCostCapUsd));
        if (request.PlatformMonthlyCostCapUsd is { } platformMonthly && platformMonthly > entitlements.PlatformMonthlyUsd)
            return AiSettingsErrors.SelfLimitExceedsEntitlement(nameof(request.PlatformMonthlyCostCapUsd));
        if (request.PlatformDailyCostCapUsd is { } platformDaily && platformDaily > entitlements.PlatformDailyUsd)
            return AiSettingsErrors.SelfLimitExceedsEntitlement(nameof(request.PlatformDailyCostCapUsd));
        if (request.RequestsPerMinute is { } rpm && rpm > entitlements.RequestsPerMinute)
            return AiSettingsErrors.SelfLimitExceedsEntitlement(nameof(request.RequestsPerMinute));

        var hasPublicDefaults = request.PublicMonthlyTokenCap.HasValue ||
                                request.PublicDailyTokenCap.HasValue ||
                                request.PublicRequestsPerMinute.HasValue;
        if (hasPublicDefaults && !entitlements.WidgetsEnabled)
            return AiSettingsErrors.WidgetDisabledByPlan;

        if (request.PublicMonthlyTokenCap is { } publicMonthly && publicMonthly > entitlements.WidgetMonthlyTokens)
            return AiSettingsErrors.WidgetQuotaExceedsEntitlement(nameof(request.PublicMonthlyTokenCap));
        if (request.PublicDailyTokenCap is { } publicDaily && publicDaily > entitlements.WidgetDailyTokens)
            return AiSettingsErrors.WidgetQuotaExceedsEntitlement(nameof(request.PublicDailyTokenCap));
        if (request.PublicRequestsPerMinute is { } publicRpm && publicRpm > entitlements.WidgetRequestsPerMinute)
            return AiSettingsErrors.WidgetQuotaExceedsEntitlement(nameof(request.PublicRequestsPerMinute));

        return null;
    }
}
