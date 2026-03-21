using Starter.Application.Common.Interfaces;
using Starter.Domain.Identity.Errors;
using Starter.Shared.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Starter.Application.Features.Users.Commands.SuspendUser;

internal sealed class SuspendUserCommandHandler(
    IApplicationDbContext context,
    ICurrentUserService currentUserService) : IRequestHandler<SuspendUserCommand, Result>
{
    public async Task<Result> Handle(SuspendUserCommand request, CancellationToken cancellationToken)
    {
        var user = await context.Users
            .FirstOrDefaultAsync(u => u.Id == request.Id, cancellationToken);

        if (user is null)
            return Result.Failure(UserErrors.NotFound(request.Id));

        if (currentUserService.UserId == request.Id)
            return Result.Failure(Error.Validation("SuspendUser.CannotSuspendSelf", "You cannot suspend your own account."));

        user.Suspend();
        user.RevokeRefreshToken();
        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
