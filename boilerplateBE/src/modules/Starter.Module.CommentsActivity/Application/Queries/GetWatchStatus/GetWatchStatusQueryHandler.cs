using MediatR;
using Microsoft.EntityFrameworkCore;
using Starter.Application.Common.Interfaces;
using Starter.Module.CommentsActivity.Application.DTOs;
using Starter.Module.CommentsActivity.Infrastructure.Persistence;
using Starter.Shared.Results;

namespace Starter.Module.CommentsActivity.Application.Queries.GetWatchStatus;

internal sealed class GetWatchStatusQueryHandler(
    CommentsActivityDbContext context,
    ICurrentUserService currentUser) : IRequestHandler<GetWatchStatusQuery, Result<WatchStatusDto>>
{
    public async Task<Result<WatchStatusDto>> Handle(
        GetWatchStatusQuery request, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId!.Value;

        var isWatching = await context.EntityWatchers
            .AnyAsync(
                w => w.EntityType == request.EntityType &&
                     w.EntityId == request.EntityId &&
                     w.UserId == userId,
                cancellationToken);

        var watcherCount = await context.EntityWatchers
            .CountAsync(
                w => w.EntityType == request.EntityType &&
                     w.EntityId == request.EntityId,
                cancellationToken);

        return Result.Success(new WatchStatusDto(isWatching, watcherCount));
    }
}
