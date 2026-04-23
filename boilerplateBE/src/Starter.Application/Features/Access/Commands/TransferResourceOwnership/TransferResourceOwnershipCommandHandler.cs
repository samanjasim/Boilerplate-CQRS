using System.Text.Json;
using MediatR;
using Starter.Application.Common.Access;
using Starter.Application.Common.Access.Contracts;
using Starter.Application.Common.Interfaces;
using Starter.Domain.Common;
using Starter.Domain.Common.Access.Errors;
using Starter.Domain.Common.Enums;
using Starter.Shared.Results;

namespace Starter.Application.Features.Access.Commands.TransferResourceOwnership;

internal sealed class TransferResourceOwnershipCommandHandler(
    IResourceOwnershipProbe probe,
    IApplicationDbContext db,
    ICurrentUserService currentUser)
    : IRequestHandler<TransferResourceOwnershipCommand, Result>
{
    public async Task<Result> Handle(TransferResourceOwnershipCommand request, CancellationToken ct)
    {
        if (!ResourceTypes.IsKnown(request.ResourceType))
            return Result.Failure(AccessErrors.ResourceNotFound);

        var ownerCheck = await probe.EnsureCallerCanShareAsync(request.ResourceType, request.ResourceId, ct);
        if (ownerCheck.IsFailure) return Result.Failure(ownerCheck.Error);

        var result = await probe.TransferOwnershipAsync(request.ResourceType, request.ResourceId, request.NewOwnerId, ct);
        if (result.IsFailure) return result;

        db.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            EntityType = AuditEntityType.File,
            EntityId = request.ResourceId,
            Action = AuditAction.Updated,
            Changes = JsonSerializer.Serialize(new
            {
                Event = "ResourceOwnershipTransferred",
                request.ResourceType,
                request.ResourceId,
                request.NewOwnerId
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
