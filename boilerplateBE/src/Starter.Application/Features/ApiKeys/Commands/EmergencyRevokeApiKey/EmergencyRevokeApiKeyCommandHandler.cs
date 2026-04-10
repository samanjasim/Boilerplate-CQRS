using MediatR;
using Microsoft.EntityFrameworkCore;
using Starter.Application.Common.Interfaces;
using Starter.Domain.ApiKeys.Entities;
using Starter.Domain.ApiKeys.Errors;
using Starter.Domain.Common;
using Starter.Domain.Common.Enums;
using Starter.Shared.Results;

namespace Starter.Application.Features.ApiKeys.Commands.EmergencyRevokeApiKey;

public sealed class EmergencyRevokeApiKeyCommandHandler(
    IApplicationDbContext dbContext,
    ICurrentUserService currentUserService)
    : IRequestHandler<EmergencyRevokeApiKeyCommand, Result>
{
    public async Task<Result> Handle(EmergencyRevokeApiKeyCommand request, CancellationToken cancellationToken)
    {
        var apiKey = await dbContext.Set<ApiKey>()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(k => k.Id == request.Id, cancellationToken);

        if (apiKey is null)
            return Result.Failure(ApiKeyErrors.NotFound);

        if (apiKey.IsRevoked)
            return Result.Failure(ApiKeyErrors.AlreadyRevoked);

        apiKey.Revoke();

        // Get tenant name for audit
        string? tenantName = null;
        if (apiKey.TenantId.HasValue)
        {
            tenantName = await dbContext.Tenants
                .IgnoreQueryFilters()
                .Where(t => t.Id == apiKey.TenantId.Value)
                .Select(t => t.Name)
                .FirstOrDefaultAsync(cancellationToken);
        }

        // Create explicit audit log for emergency action
        var auditLog = new AuditLog
        {
            Id = Guid.NewGuid(),
            EntityType = AuditEntityType.ApiKey,
            EntityId = apiKey.Id,
            Action = AuditAction.EmergencyRevoked,
            Changes = System.Text.Json.JsonSerializer.Serialize(new
            {
                keyName = apiKey.Name,
                tenantName,
                tenantId = apiKey.TenantId,
                reason = request.Reason,
                isPlatformKey = apiKey.IsPlatformKey
            }),
            PerformedBy = currentUserService.UserId,
            PerformedByName = currentUserService.Email,
            PerformedAt = DateTime.UtcNow,
            TenantId = apiKey.TenantId
        };

        dbContext.AuditLogs.Add(auditLog);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
