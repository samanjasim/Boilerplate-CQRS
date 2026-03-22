using Starter.Application.Common.Interfaces;
using Starter.Domain.Identity.Errors;
using RoleNames = Starter.Shared.Constants.Roles;
using Starter.Shared.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Starter.Application.Features.Auth.Commands.RevokeInvite;

internal sealed class RevokeInviteCommandHandler(
    IApplicationDbContext context,
    ICurrentUserService currentUserService) : IRequestHandler<RevokeInviteCommand, Result>
{
    public async Task<Result> Handle(RevokeInviteCommand request, CancellationToken cancellationToken)
    {
        if (currentUserService.TenantId is null && !currentUserService.IsInRole(RoleNames.SuperAdmin))
            return Result.Failure(Error.Unauthorized());

        var invitation = await context.Invitations
            .FirstOrDefaultAsync(i => i.Id == request.Id
                && (currentUserService.IsInRole(RoleNames.SuperAdmin) || i.TenantId == currentUserService.TenantId),
                cancellationToken);

        if (invitation is null)
            return Result.Failure(InvitationErrors.NotFound(request.Id));

        context.Invitations.Remove(invitation);
        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
