using MediatR;
using Microsoft.EntityFrameworkCore;
using Starter.Application.Common.Interfaces;
using Starter.Domain.Common.Enums;
using Starter.Module.AI.Application.Commands.Settings.ProviderCredentials;
using Starter.Module.AI.Application.DTOs;
using Starter.Module.AI.Application.Services.Settings;
using Starter.Module.AI.Domain.Entities;
using Starter.Module.AI.Domain.Enums;
using Starter.Module.AI.Domain.Errors;
using Starter.Module.AI.Infrastructure.Persistence;
using Starter.Shared.Results;

namespace Starter.Module.AI.Application.Commands.Settings.ProviderCredentials.RotateProviderCredential;

internal sealed class RotateProviderCredentialCommandHandler(
    AiDbContext db,
    IApplicationDbContext coreDb,
    ICurrentUserService currentUser,
    IAiEntitlementResolver entitlements,
    IAiSecretProtector secrets) : IRequestHandler<RotateProviderCredentialCommand, Result<AiProviderCredentialDto>>
{
    public async Task<Result<AiProviderCredentialDto>> Handle(RotateProviderCredentialCommand request, CancellationToken ct)
    {
        if (currentUser.TenantId is null || currentUser.TenantId == Guid.Empty)
            return Result.Failure<AiProviderCredentialDto>(
                Error.Validation("AiSettings.TenantIdRequired", "A tenant id is required to rotate AI provider credentials."));

        var resolvedEntitlements = await entitlements.ResolveAsync(ct);
        if (!resolvedEntitlements.ByokEnabled)
            return Result.Failure<AiProviderCredentialDto>(AiSettingsErrors.ByokDisabledByPlan);

        var credential = await db.AiProviderCredentials
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(c =>
                c.Id == request.Id &&
                c.TenantId == currentUser.TenantId.Value &&
                c.Status == ProviderCredentialStatus.Active, ct);

        if (credential is null)
            return Result.Failure<AiProviderCredentialDto>(AiSettingsErrors.ProviderCredentialNotFound);

        credential.Revoke();
        var replacement = AiProviderCredential.Create(
            currentUser.TenantId.Value,
            credential.Provider,
            credential.DisplayName,
            secrets.Protect(request.Secret),
            secrets.Prefix(request.Secret),
            currentUser.UserId);

        db.AiProviderCredentials.Add(replacement);
        await db.SaveChangesAsync(ct);

        AiProviderCredentialAudit.Add(
            coreDb,
            currentUser,
            replacement,
            "AiProviderCredential.Rotated",
            AuditAction.Updated);
        await coreDb.SaveChangesAsync(ct);

        return Result.Success(AiProviderCredentialDtos.ToDto(replacement, secrets));
    }
}
