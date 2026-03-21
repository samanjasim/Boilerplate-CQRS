using Starter.Application.Common.Interfaces;
using Starter.Domain.Identity.Errors;
using Starter.Shared.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Starter.Application.Features.Auth.Commands.RevokeAllSessions;

internal sealed class RevokeAllSessionsCommandHandler(
    IApplicationDbContext context,
    ICurrentUserService currentUserService) : IRequestHandler<RevokeAllSessionsCommand, Result>
{
    public async Task<Result> Handle(RevokeAllSessionsCommand request, CancellationToken cancellationToken)
    {
        var userId = currentUserService.UserId;
        if (userId is null)
            return Result.Failure(UserErrors.Unauthorized());

        var sessions = await context.Sessions
            .Where(s => s.UserId == userId.Value && !s.IsRevoked)
            .ToListAsync(cancellationToken);

        foreach (var session in sessions)
        {
            // Keep the current session active
            if (request.CurrentRefreshToken is not null && session.RefreshToken == request.CurrentRefreshToken)
                continue;

            session.Revoke();
        }

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
