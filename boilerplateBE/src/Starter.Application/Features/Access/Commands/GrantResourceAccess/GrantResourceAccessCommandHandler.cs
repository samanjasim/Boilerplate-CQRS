using MediatR;
using Starter.Application.Common.Access;
using Starter.Application.Common.Access.Contracts;
using Starter.Application.Common.Interfaces;
using Starter.Domain.Common.Access.Enums;
using Starter.Domain.Common.Access.Errors;
using Starter.Shared.Results;

namespace Starter.Application.Features.Access.Commands.GrantResourceAccess;

internal sealed class GrantResourceAccessCommandHandler(
    IResourceAccessService access,
    IResourceOwnershipProbe probe,
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

        return Result.Success(id);
    }
}
