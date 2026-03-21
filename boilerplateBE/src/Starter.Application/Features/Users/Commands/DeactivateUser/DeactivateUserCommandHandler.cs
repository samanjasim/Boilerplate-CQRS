using Starter.Application.Common.Interfaces;
using Starter.Domain.Identity.Errors;
using Starter.Shared.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Starter.Application.Features.Users.Commands.DeactivateUser;

internal sealed class DeactivateUserCommandHandler(
    IApplicationDbContext context,
    ICurrentUserService currentUserService) : IRequestHandler<DeactivateUserCommand, Result>
{
    public async Task<Result> Handle(DeactivateUserCommand request, CancellationToken cancellationToken)
    {
        var user = await context.Users
            .FirstOrDefaultAsync(u => u.Id == request.Id, cancellationToken);

        if (user is null)
            return Result.Failure(UserErrors.NotFound(request.Id));

        if (currentUserService.UserId == request.Id)
            return Result.Failure(Error.Validation("DeactivateUser.CannotDeactivateSelf", "You cannot deactivate your own account."));

        user.Deactivate();
        user.RevokeRefreshToken();
        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
