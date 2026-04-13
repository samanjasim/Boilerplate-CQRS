using MediatR;
using Microsoft.EntityFrameworkCore;
using Starter.Application.Common.Interfaces;
using Starter.Module.CommentsActivity.Domain.Entities;
using Starter.Module.CommentsActivity.Domain.Enums;
using Starter.Module.CommentsActivity.Infrastructure.Persistence;
using Starter.Shared.Results;

namespace Starter.Module.CommentsActivity.Application.Commands.WatchEntity;

internal sealed class WatchEntityCommandHandler(
    CommentsActivityDbContext context,
    ICurrentUserService currentUser) : IRequestHandler<WatchEntityCommand, Result>
{
    public async Task<Result> Handle(WatchEntityCommand request, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId!.Value;

        var alreadyWatching = await context.EntityWatchers
            .AnyAsync(
                w => w.EntityType == request.EntityType &&
                     w.EntityId == request.EntityId &&
                     w.UserId == userId,
                cancellationToken);

        if (alreadyWatching)
            return Result.Success();

        var watcher = EntityWatcher.Create(
            currentUser.TenantId,
            request.EntityType,
            request.EntityId,
            userId,
            WatchReason.Explicit);

        context.EntityWatchers.Add(watcher);
        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
