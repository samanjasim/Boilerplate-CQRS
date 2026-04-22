using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Starter.Application.Common.Access;
using Starter.Application.Common.Interfaces;
using Starter.Domain.Common;
using Starter.Domain.Common.Access.Errors;
using Starter.Domain.Common.Enums;
using Starter.Shared.Results;

namespace Starter.Application.Features.Access.Commands.RevokeResourceAccess;

internal sealed class RevokeResourceAccessCommandHandler(
    IApplicationDbContext db,
    IResourceAccessService access,
    IResourceOwnershipProbe probe,
    ICurrentUserService currentUser)
    : IRequestHandler<RevokeResourceAccessCommand, Result>
{
    public async Task<Result> Handle(RevokeResourceAccessCommand request, CancellationToken ct)
    {
        var grant = await db.ResourceGrants.FirstOrDefaultAsync(g => g.Id == request.GrantId, ct);
        if (grant is null) return Result.Failure(AccessErrors.GrantNotFound);

        var ownerCheck = await probe.EnsureCallerCanShareAsync(grant.ResourceType, grant.ResourceId, ct);
        if (ownerCheck.IsFailure) return Result.Failure(ownerCheck.Error);

        var resourceType = grant.ResourceType;
        var resourceId = grant.ResourceId;
        var subjectType = grant.SubjectType;
        var subjectId = grant.SubjectId;

        await access.RevokeAsync(request.GrantId, ct);

        db.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            EntityType = AuditEntityType.ResourceGrant,
            EntityId = request.GrantId,
            Action = AuditAction.Deleted,
            Changes = JsonSerializer.Serialize(new
            {
                Event = "ResourceGrantRevoked",
                ResourceType = resourceType,
                ResourceId = resourceId,
                SubjectType = subjectType.ToString(),
                SubjectId = subjectId
            }),
            PerformedBy = currentUser.UserId,
            PerformedByName = currentUser.Email,
            PerformedAt = DateTime.UtcNow,
            TenantId = currentUser.TenantId
        });
        await db.SaveChangesAsync(ct);

        return Result.Success();
    }
}
