using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Starter.Abstractions.Capabilities;
using Starter.Module.CommentsActivity.Domain.Entities;
using Starter.Module.CommentsActivity.Domain.Events;
using DomainWatchReason = Starter.Module.CommentsActivity.Domain.Enums.WatchReason;
using Starter.Module.CommentsActivity.Infrastructure.Persistence;

namespace Starter.Module.CommentsActivity.Application.EventHandlers;

internal sealed class AutoWatchOnCommentHandler(
    CommentsActivityDbContext context,
    ICommentableEntityRegistry registry,
    ILogger<AutoWatchOnCommentHandler> logger) : INotificationHandler<CommentCreatedEvent>
{
    public async Task Handle(CommentCreatedEvent notification, CancellationToken cancellationToken)
    {
        try
        {
            var definition = registry.GetDefinition(notification.EntityType);
            if (definition is null || !definition.AutoWatchOnComment)
                return;

            var alreadyWatching = await context.EntityWatchers
                .AnyAsync(
                    w => w.EntityType == notification.EntityType &&
                         w.EntityId == notification.EntityId &&
                         w.UserId == notification.AuthorId,
                    cancellationToken);

            if (alreadyWatching) return;

            var watcher = EntityWatcher.Create(
                notification.TenantId,
                notification.EntityType,
                notification.EntityId,
                notification.AuthorId,
                DomainWatchReason.Participated);

            context.EntityWatchers.Add(watcher);
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to auto-watch on comment for {EntityType}/{EntityId}",
                notification.EntityType, notification.EntityId);
        }
    }
}
