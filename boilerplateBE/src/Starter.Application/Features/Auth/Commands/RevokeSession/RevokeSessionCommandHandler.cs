using Starter.Application.Common.Interfaces;
using Starter.Domain.Identity.Errors;
using Starter.Shared.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Starter.Application.Features.Auth.Commands.RevokeSession;

internal sealed class RevokeSessionCommandHandler(
    IApplicationDbContext context,
    ICurrentUserService currentUserService) : IRequestHandler<RevokeSessionCommand, Result>
{
    public async Task<Result> Handle(RevokeSessionCommand request, CancellationToken cancellationToken)
    {
        var userId = currentUserService.UserId;
        if (userId is null)
            return Result.Failure(UserErrors.Unauthorized());

        var session = await context.Sessions
            .FirstOrDefaultAsync(s => s.Id == request.SessionId && s.UserId == userId.Value && !s.IsRevoked, cancellationToken);

        if (session is null)
            return Result.Failure(UserErrors.SessionNotFound());

        session.Revoke();
        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
