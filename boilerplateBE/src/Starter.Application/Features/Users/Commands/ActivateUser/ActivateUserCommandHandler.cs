using Starter.Application.Common.Interfaces;
using Starter.Domain.Identity.Enums;
using Starter.Domain.Identity.Errors;
using Starter.Shared.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Starter.Application.Features.Users.Commands.ActivateUser;

internal sealed class ActivateUserCommandHandler(
    IApplicationDbContext context) : IRequestHandler<ActivateUserCommand, Result>
{
    public async Task<Result> Handle(ActivateUserCommand request, CancellationToken cancellationToken)
    {
        var user = await context.Users
            .FirstOrDefaultAsync(u => u.Id == request.Id, cancellationToken);

        if (user is null)
            return Result.Failure(UserErrors.NotFound(request.Id));

        if (user.Status == UserStatus.Active)
            return Result.Success();

        user.Activate();
        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
