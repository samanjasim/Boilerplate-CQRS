using Starter.Application.Common.Interfaces;
using Starter.Domain.Common;
using Starter.Domain.Identity.Errors;
using Starter.Shared.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Starter.Application.Features.Notifications.Queries.GetUnreadCount;

internal sealed class GetUnreadCountQueryHandler(
    IApplicationDbContext context,
    ICurrentUserService currentUserService) : IRequestHandler<GetUnreadCountQuery, Result<int>>
{
    public async Task<Result<int>> Handle(GetUnreadCountQuery request, CancellationToken cancellationToken)
    {
        var userId = currentUserService.UserId;
        if (userId is null)
            return Result.Failure<int>(UserErrors.Unauthorized());

        var count = await context.Set<Notification>()
            .AsNoTracking()
            .CountAsync(n => n.UserId == userId.Value && !n.IsRead, cancellationToken);

        return Result.Success(count);
    }
}
