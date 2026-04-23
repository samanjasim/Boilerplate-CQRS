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

namespace Starter.Application.Features.Access.Commands.GrantResourceAccess;

internal sealed class GrantResourceAccessCommandHandler(
    IResourceAccessService access,
    IResourceOwnershipProbe probe,
    IApplicationDbContext db,
    INotificationService notifications,
    ICurrentUserService currentUser)
    : IRequestHandler<GrantResourceAccessCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(GrantResourceAccessCommand request, CancellationToken ct)
    {
        if (!ResourceTypes.IsKnown(request.ResourceType))
            return Result.Failure<Guid>(AccessErrors.ResourceNotFound);

        var ownerCheck = await probe.EnsureCallerCanShareAsync(request.ResourceType, request.ResourceId, ct);
        if (ownerCheck.IsFailure) return Result.Failure<Guid>(ownerCheck.Error);

        if (request.SubjectType == GrantSubjectType.User && request.SubjectId == currentUser.UserId)
            return Result.Failure<Guid>(AccessErrors.SelfGrantBlocked);

        var subjectCheck = await probe.EnsureSubjectValidAsync(request.SubjectType, request.SubjectId, ct);
        if (subjectCheck.IsFailure) return Result.Failure<Guid>(subjectCheck.Error);

        var id = await access.GrantAsync(
            request.ResourceType, request.ResourceId,
            request.SubjectType, request.SubjectId, request.Level, ct);

        db.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            EntityType = AuditEntityType.ResourceGrant,
            EntityId = id,
            Action = AuditAction.Created,
            Changes = JsonSerializer.Serialize(new
            {
                Event = "ResourceGrantCreated",
                request.ResourceType,
                request.ResourceId,
                SubjectType = request.SubjectType.ToString(),
                request.SubjectId,
                Level = request.Level.ToString()
            }),
            PerformedBy = currentUser.UserId,
            PerformedByName = currentUser.Email,
            PerformedAt = DateTime.UtcNow,
            TenantId = currentUser.TenantId
        });
        await db.SaveChangesAsync(ct);

        if (request.SubjectType == GrantSubjectType.User)
        {
            var nameResult = await probe.GetResourceDisplayNameAsync(request.ResourceType, request.ResourceId, ct);
            var resourceName = nameResult.IsSuccess ? nameResult.Value : request.ResourceType;
            var data = JsonSerializer.Serialize(new
            {
                request.ResourceType,
                request.ResourceId,
                Level = request.Level.ToString()
            });
            await notifications.CreateAsync(
                userId: request.SubjectId,
                tenantId: currentUser.TenantId,
                type: "ResourceShared",
                title: $"Shared with you: {resourceName}",
                message: $"{currentUser.Email} gave you {request.Level} access to {resourceName}.",
                data: data,
                ct: ct);
        }

        return Result.Success(id);
    }
}
