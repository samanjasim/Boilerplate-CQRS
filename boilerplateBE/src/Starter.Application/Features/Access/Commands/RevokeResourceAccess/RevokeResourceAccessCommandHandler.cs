using MediatR;
using Microsoft.EntityFrameworkCore;
using Starter.Application.Common.Access;
using Starter.Application.Common.Interfaces;
using Starter.Domain.Common.Access.Errors;
using Starter.Shared.Results;

namespace Starter.Application.Features.Access.Commands.RevokeResourceAccess;

internal sealed class RevokeResourceAccessCommandHandler(
    IApplicationDbContext db,
    IResourceAccessService access,
    IResourceOwnershipProbe probe)
    : IRequestHandler<RevokeResourceAccessCommand, Result>
{
    public async Task<Result> Handle(RevokeResourceAccessCommand request, CancellationToken ct)
    {
        var grant = await db.ResourceGrants.FirstOrDefaultAsync(g => g.Id == request.GrantId, ct);
        if (grant is null) return Result.Failure(AccessErrors.GrantNotFound);

        var ownerCheck = await probe.EnsureCallerCanShareAsync(grant.ResourceType, grant.ResourceId, ct);
        if (ownerCheck.IsFailure) return Result.Failure(ownerCheck.Error);

        await access.RevokeAsync(request.GrantId, ct);
        return Result.Success();
    }
}
