using MediatR;
using Microsoft.EntityFrameworkCore;
using Starter.Application.Common.Interfaces;
using Starter.Module.AI.Application.DTOs;
using Starter.Module.AI.Application.Events;
using Starter.Module.AI.Application.Services.Settings;
using Starter.Module.AI.Domain.Entities;
using Starter.Module.AI.Domain.Enums;
using Starter.Module.AI.Domain.Errors;
using Starter.Module.AI.Infrastructure.Persistence;
using Starter.Shared.Results;

namespace Starter.Module.AI.Application.Commands.Settings.ProviderCredentials.CreateProviderCredential;

internal sealed class CreateProviderCredentialCommandHandler(
    AiDbContext db,
    IApplicationDbContext appDb,
    ICurrentUserService currentUser,
    IAiEntitlementResolver entitlements,
    IAiSecretProtector secrets,
    IIntegrationEventCollector eventCollector) : IRequestHandler<CreateProviderCredentialCommand, Result<AiProviderCredentialDto>>
{
    public async Task<Result<AiProviderCredentialDto>> Handle(CreateProviderCredentialCommand request, CancellationToken ct)
    {
        var tenantId = currentUser.TenantId ?? request.TenantId;
        if (tenantId is null || tenantId == Guid.Empty)
            return Result.Failure<AiProviderCredentialDto>(
                Error.Validation("AiSettings.TenantIdRequired", "A tenant id is required to create AI provider credentials."));

        var resolvedEntitlements = await entitlements.ResolveAsync(ct);
        if (!resolvedEntitlements.ByokEnabled)
            return Result.Failure<AiProviderCredentialDto>(AiSettingsErrors.ByokDisabledByPlan);

        var activeCredentials = await db.AiProviderCredentials
            .IgnoreQueryFilters()
            .Where(c =>
                c.TenantId == tenantId.Value &&
                c.Provider == request.Provider &&
                c.Status == ProviderCredentialStatus.Active)
            .ToListAsync(ct);

        foreach (var activeCredential in activeCredentials)
            activeCredential.Revoke();

        var protectedSecret = secrets.Protect(request.Secret);
        var keyPrefix = secrets.Prefix(request.Secret);
        var credential = AiProviderCredential.Create(
            tenantId.Value,
            request.Provider,
            request.DisplayName,
            protectedSecret,
            keyPrefix,
            currentUser.UserId);

        db.AiProviderCredentials.Add(credential);
        await db.SaveChangesAsync(ct);

        // Audit is written asynchronously by AiProviderCredentialAuditConsumer so a
        // transient AppDb hiccup retries via MT outbox instead of leaving a credential
        // without an audit row.
        eventCollector.Schedule(new AiProviderCredentialCreatedEvent(
            TenantId: tenantId.Value,
            CredentialId: credential.Id,
            Provider: credential.Provider,
            KeyPrefix: credential.KeyPrefix,
            PerformedBy: currentUser.UserId,
            PerformedByEmail: currentUser.Email,
            OccurredAt: DateTime.UtcNow));
        await appDb.SaveChangesAsync(ct);

        return Result.Success(AiProviderCredentialDtos.ToDto(credential, secrets));
    }
}
