using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Starter.Module.CommentsActivity.Domain.Entities;
using Starter.Module.CommentsActivity.Domain.Enums;
using Starter.Module.CommentsActivity.Domain.Events;
using Starter.Module.CommentsActivity.Infrastructure.Persistence;

namespace Starter.Module.CommentsActivity.Application.EventHandlers;

internal sealed class AutoWatchOnMentionHandler(
    CommentsActivityDbContext context,
    ILogger<AutoWatchOnMentionHandler> logger) : INotificationHandler<CommentCreatedEvent>
{
    public async Task Handle(CommentCreatedEvent notification, CancellationToken cancellationToken)
    {
        try
        {
            if (string.IsNullOrEmpty(notification.MentionsJson)) return;

            var mentionedUserIds = JsonSerializer.Deserialize<List<Guid>>(notification.MentionsJson);
            if (mentionedUserIds is null or { Count: 0 }) return;

            var existingWatcherUserIds = await context.EntityWatchers
                .Where(w => w.EntityType == notification.EntityType &&
                            w.EntityId == notification.EntityId &&
                            mentionedUserIds.Contains(w.UserId))
                .Select(w => w.UserId)
                .ToListAsync(cancellationToken);

            var newWatcherIds = mentionedUserIds.Except(existingWatcherUserIds);

            foreach (var userId in newWatcherIds)
            {
                var watcher = EntityWatcher.Create(
                    notification.TenantId,
                    notification.EntityType,
                    notification.EntityId,
                    userId,
                    WatchReason.Mentioned);

                context.EntityWatchers.Add(watcher);
            }

            await context.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to auto-watch mentioned users for {EntityType}/{EntityId}",
                notification.EntityType, notification.EntityId);
        }
    }
}
