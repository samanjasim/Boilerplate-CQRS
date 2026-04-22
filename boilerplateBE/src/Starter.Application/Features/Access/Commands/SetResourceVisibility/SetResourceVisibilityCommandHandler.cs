using System.Text.Json;
using MediatR;
using Starter.Application.Common.Access;
using Starter.Application.Common.Access.Contracts;
using Starter.Application.Common.Interfaces;
using Starter.Domain.Common;
using Starter.Domain.Common.Access.Enums;
using Starter.Domain.Common.Access.Errors;
using Starter.Domain.Common.Enums;
using Starter.Shared.Results;

namespace Starter.Application.Features.Access.Commands.SetResourceVisibility;

internal sealed class SetResourceVisibilityCommandHandler(
    IResourceOwnershipProbe probe,
    IApplicationDbContext db,
    ICurrentUserService currentUser)
    : IRequestHandler<SetResourceVisibilityCommand, Result>
{
    public async Task<Result> Handle(SetResourceVisibilityCommand request, CancellationToken ct)
    {
        if (!ResourceTypes.IsKnown(request.ResourceType))
            return Result.Failure(AccessErrors.ResourceNotFound);

        if ((int)request.Visibility > (int)ResourceTypes.MaxVisibility(request.ResourceType))
            return Result.Failure(AccessErrors.VisibilityNotAllowedForResourceType);

        if (request.Visibility == ResourceVisibility.Public
            && request.ResourceType == ResourceTypes.File
            && !currentUser.HasPermission(Starter.Shared.Constants.Permissions.Files.Manage))
            return Result.Failure(AccessErrors.VisibilityNotAllowedForResourceType);

        var ownerCheck = await probe.EnsureCallerCanShareAsync(request.ResourceType, request.ResourceId, ct);
        if (ownerCheck.IsFailure) return Result.Failure(ownerCheck.Error);

        var result = await probe.SetVisibilityAsync(request.ResourceType, request.ResourceId, request.Visibility, ct);
        if (result.IsFailure) return result;

        var changedEntry = new AuditLog
        {
            Id = Guid.NewGuid(),
            EntityType = AuditEntityType.File,
            EntityId = request.ResourceId,
            Action = AuditAction.Updated,
            Changes = JsonSerializer.Serialize(new
            {
                Event = "ResourceVisibilityChanged",
                request.ResourceType,
                request.ResourceId,
                Visibility = request.Visibility.ToString()
            }),
            PerformedBy = currentUser.UserId,
            PerformedByName = currentUser.Email,
            PerformedAt = DateTime.UtcNow,
            TenantId = currentUser.TenantId
        };
        db.AuditLogs.Add(changedEntry);

        if (request.Visibility == ResourceVisibility.Public)
        {
            db.AuditLogs.Add(new AuditLog
            {
                Id = Guid.NewGuid(),
                EntityType = AuditEntityType.File,
                EntityId = request.ResourceId,
                Action = AuditAction.Updated,
                Changes = JsonSerializer.Serialize(new
                {
                    Event = "ResourceVisibilityMadePublic",
                    request.ResourceType,
                    request.ResourceId
                }),
                PerformedBy = currentUser.UserId,
                PerformedByName = currentUser.Email,
                PerformedAt = DateTime.UtcNow,
                TenantId = currentUser.TenantId
            });
        }

        await db.SaveChangesAsync(ct);
        return Result.Success();
    }
}
