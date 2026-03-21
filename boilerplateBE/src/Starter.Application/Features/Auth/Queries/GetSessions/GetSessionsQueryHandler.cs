using Starter.Application.Common.Interfaces;
using Starter.Shared.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Starter.Application.Features.Auth.Queries.GetSessions;

internal sealed class GetSessionsQueryHandler(
    IApplicationDbContext context,
    ICurrentUserService currentUserService) : IRequestHandler<GetSessionsQuery, Result<List<SessionDto>>>
{
    public async Task<Result<List<SessionDto>>> Handle(GetSessionsQuery request, CancellationToken cancellationToken)
    {
        var userId = currentUserService.UserId;
        if (userId is null)
            return Result.Failure<List<SessionDto>>(Starter.Domain.Identity.Errors.UserErrors.Unauthorized());

        var sessions = await context.Sessions
            .AsNoTracking()
            .Where(s => s.UserId == userId.Value && !s.IsRevoked)
            .OrderByDescending(s => s.LastActiveAt)
            .Select(s => new SessionDto(
                s.Id,
                s.IpAddress,
                s.DeviceInfo,
                s.CreatedAt,
                s.LastActiveAt,
                s.RefreshToken == request.CurrentRefreshToken))
            .ToListAsync(cancellationToken);

        return Result.Success(sessions);
    }
}
