using MediatR;
using Starter.Application.Common.Access;
using Starter.Application.Common.Access.Contracts;
using Starter.Application.Common.Access.DTOs;
using Starter.Domain.Common.Access.Errors;
using Starter.Shared.Results;

namespace Starter.Application.Features.Access.Queries.ListResourceGrants;

internal sealed class ListResourceGrantsQueryHandler(
    IResourceAccessService access,
    IResourceOwnershipProbe probe)
    : IRequestHandler<ListResourceGrantsQuery, Result<IReadOnlyList<ResourceGrantDto>>>
{
    public async Task<Result<IReadOnlyList<ResourceGrantDto>>> Handle(
        ListResourceGrantsQuery request,
        CancellationToken ct)
    {
        if (!ResourceTypes.IsKnown(request.ResourceType))
            return Result.Failure<IReadOnlyList<ResourceGrantDto>>(AccessErrors.ResourceNotFound);

        var ownerCheck = await probe.EnsureCallerCanShareAsync(request.ResourceType, request.ResourceId, ct);
        if (ownerCheck.IsFailure) return Result.Failure<IReadOnlyList<ResourceGrantDto>>(ownerCheck.Error);

        var grants = await access.ListGrantsAsync(request.ResourceType, request.ResourceId, ct);
        return Result.Success(grants);
    }
}
