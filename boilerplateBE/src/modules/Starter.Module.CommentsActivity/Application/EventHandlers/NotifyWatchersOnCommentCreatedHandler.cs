using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Starter.Abstractions.Capabilities;
using Starter.Abstractions.Readers;
using Starter.Module.CommentsActivity.Domain.Events;
using Starter.Module.CommentsActivity.Infrastructure.Persistence;

namespace Starter.Module.CommentsActivity.Application.EventHandlers;

internal sealed class NotifyWatchersOnCommentCreatedHandler(
    CommentsActivityDbContext context,
    INotificationServiceCapability notificationService,
    IUserReader userReader,
    ILogger<NotifyWatchersOnCommentCreatedHandler> logger) : INotificationHandler<CommentCreatedEvent>
{
    public async Task Handle(CommentCreatedEvent notification, CancellationToken cancellationToken)
    {
        try
        {
            // Mentioned users get a dedicated, higher-priority notification from
            // NotifyMentionedUsersOnCommentCreatedHandler — exclude them here so
            // they don't receive both.
            var mentionedSet = ParseMentionedUserIds(notification.MentionsJson);

            var watcherUserIds = await context.EntityWatchers
                .Where(w => w.EntityType == notification.EntityType &&
                            w.EntityId == notification.EntityId &&
                            w.UserId != notification.AuthorId)
                .Select(w => w.UserId)
                .ToListAsync(cancellationToken);

            var recipientIds = watcherUserIds.Where(id => !mentionedSet.Contains(id)).ToList();
            if (recipientIds.Count == 0) return;

            // Use each recipient's own TenantId so their multi-tenant filter
            // admits the row. See note in NotifyMentionedUsersOnCommentCreatedHandler.
            var recipients = await userReader.GetManyAsync(recipientIds, cancellationToken);

            if (recipients.Count < recipientIds.Count)
            {
                var foundIds = recipients.Select(r => r.Id).ToHashSet();
                var missing = recipientIds.Where(id => !foundIds.Contains(id)).ToArray();
                logger.LogDebug(
                    "Skipping watcher notification for {Count} unresolved user id(s) on comment {CommentId}: {MissingIds}",
                    missing.Length, notification.CommentId, missing);
            }

            if (recipients.Count == 0) return;

            var data = JsonSerializer.Serialize(new
            {
                commentId = notification.CommentId,
                entityType = notification.EntityType,
                entityId = notification.EntityId,
            });

            foreach (var recipient in recipients)
            {
                await notificationService.CreateAsync(
                    recipient.Id,
                    recipient.TenantId,
                    "CommentOnWatchedEntity",
                    $"New comment on {notification.EntityType}",
                    "A new comment was added to an item you are watching.",
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

    private static HashSet<Guid> ParseMentionedUserIds(string? mentionsJson)
    {
        if (string.IsNullOrWhiteSpace(mentionsJson)) return [];
        try
        {
            var ids = JsonSerializer.Deserialize<List<Guid>>(mentionsJson);
            return ids is null ? [] : new HashSet<Guid>(ids);
        }
        catch (JsonException)
        {
            return [];
        }
    }
}
