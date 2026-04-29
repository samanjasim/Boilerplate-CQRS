using MediatR;
using Starter.Application.Common.Interfaces;
using Starter.Module.AI.Application.Commands.Settings.UpsertAiTenantSettings;
using Starter.Module.AI.Application.DTOs;
using Starter.Module.AI.Application.Services.Settings;
using Starter.Module.AI.Domain.Enums;
using Starter.Shared.Results;

namespace Starter.Module.AI.Application.Queries.Settings.GetAiTenantSettings;

internal sealed class GetAiTenantSettingsQueryHandler(
    IAiTenantSettingsResolver settingsResolver,
    IAiEntitlementResolver entitlements,
    ICurrentUserService currentUser) : IRequestHandler<GetAiTenantSettingsQuery, Result<AiTenantSettingsDto>>
{
    public async Task<Result<AiTenantSettingsDto>> Handle(GetAiTenantSettingsQuery request, CancellationToken ct)
    {
        var tenantId = currentUser.TenantId ?? request.TenantId;
        if (tenantId is null || tenantId == Guid.Empty)
            return Result.Failure<AiTenantSettingsDto>(
                Error.Validation("AiSettings.TenantIdRequired", "A tenant id is required to read AI tenant settings."));

        var resolvedEntitlements = await entitlements.ResolveAsync(tenantId.Value, ct);
        var settings = await settingsResolver.GetOrDefaultAsync(tenantId.Value, ct);
        var effectivePolicy = resolvedEntitlements.ByokEnabled
            ? settings.RequestedProviderCredentialPolicy
            : ProviderCredentialPolicy.PlatformOnly;

        return Result.Success(AiTenantSettingsMappings.ToDto(settings, tenantId.Value, effectivePolicy, resolvedEntitlements));
    }
}
