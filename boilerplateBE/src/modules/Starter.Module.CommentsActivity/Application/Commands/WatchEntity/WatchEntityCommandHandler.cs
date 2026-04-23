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

        // Cross-tenant dedup: unique index is (entity_type, entity_id, user_id)
        // with no tenant column, so a stale cross-tenant row (tenant_id = NULL
        // from an earlier SuperAdmin action) would collide on insert.
        var alreadyWatching = await context.EntityWatchers
            .IgnoreQueryFilters()
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
