using MediatR;
using Starter.Application.Common.Access;
using Starter.Application.Common.Access.Contracts;
using Starter.Application.Common.Interfaces;
using Starter.Domain.Common.Access.Enums;
using Starter.Domain.Common.Access.Errors;
using Starter.Shared.Results;

namespace Starter.Application.Features.Access.Commands.SetResourceVisibility;

internal sealed class SetResourceVisibilityCommandHandler(
    IResourceOwnershipProbe probe,
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

        return await probe.SetVisibilityAsync(request.ResourceType, request.ResourceId, request.Visibility, ct);
    }
}
