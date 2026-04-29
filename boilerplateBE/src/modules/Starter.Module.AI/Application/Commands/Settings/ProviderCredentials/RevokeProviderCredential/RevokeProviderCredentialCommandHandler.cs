using MediatR;
using Microsoft.EntityFrameworkCore;
using Starter.Application.Common.Interfaces;
using Starter.Domain.Common.Enums;
using Starter.Module.AI.Application.Commands.Settings.ProviderCredentials;
using Starter.Module.AI.Domain.Errors;
using Starter.Module.AI.Infrastructure.Persistence;
using Starter.Shared.Results;

namespace Starter.Module.AI.Application.Commands.Settings.ProviderCredentials.RevokeProviderCredential;

internal sealed class RevokeProviderCredentialCommandHandler(
    AiDbContext db,
    IApplicationDbContext coreDb,
    ICurrentUserService currentUser) : IRequestHandler<RevokeProviderCredentialCommand, Result>
{
    public async Task<Result> Handle(RevokeProviderCredentialCommand request, CancellationToken ct)
    {
        if (currentUser.TenantId is null || currentUser.TenantId == Guid.Empty)
            return Result.Failure(
                Error.Validation("AiSettings.TenantIdRequired", "A tenant id is required to revoke AI provider credentials."));

        var credential = await db.AiProviderCredentials
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(c =>
                c.Id == request.Id &&
                c.TenantId == currentUser.TenantId.Value, ct);

        if (credential is null)
            return Result.Failure(AiSettingsErrors.ProviderCredentialNotFound);

        credential.Revoke();
        await db.SaveChangesAsync(ct);

        AiProviderCredentialAudit.Add(
            coreDb,
            currentUser,
            credential,
            "AiProviderCredential.Revoked",
            AuditAction.Deleted);
        await coreDb.SaveChangesAsync(ct);

        return Result.Success();
    }
}
