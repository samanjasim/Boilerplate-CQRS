using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Starter.Application.Common.Interfaces;
using Starter.Module.CommentsActivity.Domain.Events;
using Starter.Module.CommentsActivity.Infrastructure.Persistence;

namespace Starter.Module.CommentsActivity.Application.EventHandlers;

internal sealed class NotifyWatchersOnCommentCreatedHandler(
    CommentsActivityDbContext context,
    INotificationService notificationService,
    ILogger<NotifyWatchersOnCommentCreatedHandler> logger) : INotificationHandler<CommentCreatedEvent>
{
    public async Task Handle(CommentCreatedEvent notification, CancellationToken cancellationToken)
    {
        try
        {
            var watcherUserIds = await context.EntityWatchers
                .Where(w => w.EntityType == notification.EntityType &&
                            w.EntityId == notification.EntityId &&
                            w.UserId != notification.AuthorId)
                .Select(w => w.UserId)
                .ToListAsync(cancellationToken);

            if (watcherUserIds.Count == 0) return;

            var data = JsonSerializer.Serialize(new
            {
                commentId = notification.CommentId,
                entityType = notification.EntityType,
                entityId = notification.EntityId
            });

            foreach (var userId in watcherUserIds)
            {
                await notificationService.CreateAsync(
                    userId,
                    notification.TenantId,
                    "CommentCreated",
                    $"New comment on {notification.EntityType}",
                    "A new comment was added to an entity you are watching.",
                    data,
                    cancellationToken);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to notify watchers for CommentCreatedEvent {CommentId}",
                notification.CommentId);
        }
    }
}
