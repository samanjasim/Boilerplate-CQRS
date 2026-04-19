using System.Text.Json;
using MediatR;
using Microsoft.Extensions.Logging;
using Starter.Abstractions.Capabilities;
using Starter.Abstractions.Readers;
using Starter.Module.CommentsActivity.Domain.Events;

namespace Starter.Module.CommentsActivity.Application.EventHandlers;

/// <summary>
/// Sends an in-app notification to every user mentioned in a comment.
/// The comment author is excluded — self-mentions don't generate a bell entry.
/// Watcher notifications skip these recipients (see
/// <see cref="NotifyWatchersOnCommentCreatedHandler"/>) so no one gets notified
/// twice for the same comment.
/// </summary>
internal sealed class NotifyMentionedUsersOnCommentCreatedHandler(
    INotificationServiceCapability notificationService,
    IUserReader userReader,
    ILogger<NotifyMentionedUsersOnCommentCreatedHandler> logger)
    : INotificationHandler<CommentCreatedEvent>
{
    public async Task Handle(CommentCreatedEvent notification, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(notification.MentionsJson)) return;

        List<Guid>? mentionedUserIds;
        try
        {
            mentionedUserIds = JsonSerializer.Deserialize<List<Guid>>(notification.MentionsJson);
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "Could not parse MentionsJson for comment {CommentId}",
                notification.CommentId);
            return;
        }

        if (mentionedUserIds is null || mentionedUserIds.Count == 0) return;

        var recipientIds = mentionedUserIds
            .Where(id => id != notification.AuthorId)
            .Distinct()
            .ToList();

        if (recipientIds.Count == 0) return;

        // Scope each notification to the RECIPIENT's tenant so the multi-tenant
        // query filter admits it when they read their bell. The event's TenantId
        // may be null (e.g. SuperAdmin acting cross-tenant) — using it would
        // orphan the notification behind the recipient's tenant filter.
        var recipients = await userReader.GetManyAsync(recipientIds, cancellationToken);

        if (recipients.Count < recipientIds.Count)
        {
            var foundIds = recipients.Select(r => r.Id).ToHashSet();
            var missing = recipientIds.Where(id => !foundIds.Contains(id)).ToArray();
            logger.LogDebug(
                "Skipping mention notification for {Count} unresolved user id(s) on comment {CommentId}: {MissingIds}",
                missing.Length, notification.CommentId, missing);
        }

        if (recipients.Count == 0) return;

        var data = JsonSerializer.Serialize(new
        {
            commentId = notification.CommentId,
            entityType = notification.EntityType,
            entityId = notification.EntityId,
            authorId = notification.AuthorId,
        });

        foreach (var recipient in recipients)
        {
            try
            {
                await notificationService.CreateAsync(
                    recipient.Id,
                    recipient.TenantId,
                    WellKnownNotificationTypes.CommentMentioned,
                    "You were mentioned in a comment",
                    $"You were mentioned in a comment on {notification.EntityType}.",
                    data,
                    cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex,
                    "Failed to send mention notification to {UserId} for comment {CommentId}",
                    recipient.Id, notification.CommentId);
            }
        }
    }
}
