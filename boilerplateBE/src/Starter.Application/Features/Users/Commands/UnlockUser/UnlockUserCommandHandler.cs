using Starter.Application.Common.Interfaces;
using Starter.Domain.Identity.Errors;
using Starter.Shared.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Starter.Application.Features.Users.Commands.UnlockUser;

internal sealed class UnlockUserCommandHandler(
    IApplicationDbContext context) : IRequestHandler<UnlockUserCommand, Result>
{
    public async Task<Result> Handle(UnlockUserCommand request, CancellationToken cancellationToken)
    {
        var user = await context.Users
            .FirstOrDefaultAsync(u => u.Id == request.Id, cancellationToken);

        if (user is null)
            return Result.Failure(UserErrors.NotFound(request.Id));

        user.Unlock();
        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
