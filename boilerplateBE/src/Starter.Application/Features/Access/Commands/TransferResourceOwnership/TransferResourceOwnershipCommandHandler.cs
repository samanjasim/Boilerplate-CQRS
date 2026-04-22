using MediatR;
using Starter.Application.Common.Access;
using Starter.Application.Common.Access.Contracts;
using Starter.Domain.Common.Access.Errors;
using Starter.Shared.Results;

namespace Starter.Application.Features.Access.Commands.TransferResourceOwnership;

internal sealed class TransferResourceOwnershipCommandHandler(
    IResourceOwnershipProbe probe)
    : IRequestHandler<TransferResourceOwnershipCommand, Result>
{
    public async Task<Result> Handle(TransferResourceOwnershipCommand request, CancellationToken ct)
    {
        if (!ResourceTypes.IsKnown(request.ResourceType))
            return Result.Failure(AccessErrors.ResourceNotFound);

        var ownerCheck = await probe.EnsureCallerCanShareAsync(request.ResourceType, request.ResourceId, ct);
        if (ownerCheck.IsFailure) return Result.Failure(ownerCheck.Error);

        return await probe.TransferOwnershipAsync(request.ResourceType, request.ResourceId, request.NewOwnerId, ct);
    }
}
