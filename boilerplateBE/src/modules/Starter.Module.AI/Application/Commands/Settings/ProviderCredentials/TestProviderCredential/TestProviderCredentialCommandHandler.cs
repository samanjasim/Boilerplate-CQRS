using MediatR;
using Microsoft.EntityFrameworkCore;
using Starter.Application.Common.Interfaces;
using Starter.Domain.Common.Enums;
using Starter.Module.AI.Application.Commands.Settings.ProviderCredentials;
using Starter.Module.AI.Application.DTOs;
using Starter.Module.AI.Application.Services.Settings;
using Starter.Module.AI.Domain.Enums;
using Starter.Module.AI.Domain.Errors;
using Starter.Module.AI.Infrastructure.Persistence;
using Starter.Shared.Results;

namespace Starter.Module.AI.Application.Commands.Settings.ProviderCredentials.TestProviderCredential;

internal sealed class TestProviderCredentialCommandHandler(
    AiDbContext db,
    IApplicationDbContext coreDb,
    ICurrentUserService currentUser,
    IAiSecretProtector secrets) : IRequestHandler<TestProviderCredentialCommand, Result<AiProviderCredentialDto>>
{
    public async Task<Result<AiProviderCredentialDto>> Handle(TestProviderCredentialCommand request, CancellationToken ct)
    {
        if (currentUser.TenantId is null || currentUser.TenantId == Guid.Empty)
            return Result.Failure<AiProviderCredentialDto>(
                Error.Validation("AiSettings.TenantIdRequired", "A tenant id is required to test AI provider credentials."));

        var credential = await db.AiProviderCredentials
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(c =>
                c.Id == request.Id &&
                c.TenantId == currentUser.TenantId.Value &&
                c.Status == ProviderCredentialStatus.Active, ct);

        if (credential is null)
            return Result.Failure<AiProviderCredentialDto>(AiSettingsErrors.ProviderCredentialNotFound);

        _ = secrets.Unprotect(credential.EncryptedSecret);
        credential.MarkValidated();
        await db.SaveChangesAsync(ct);

        AiProviderCredentialAudit.Add(
            coreDb,
            currentUser,
            credential,
            "AiProviderCredential.Tested",
            AuditAction.Updated);
        await coreDb.SaveChangesAsync(ct);

        return Result.Success(AiProviderCredentialDtos.ToDto(credential, secrets));
    }
}
