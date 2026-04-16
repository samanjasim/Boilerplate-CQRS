using MediatR;
using Microsoft.Extensions.Logging;
using Starter.Abstractions.Events.CommentsActivity;
using Starter.Application.Common.Interfaces;
using Starter.Module.CommentsActivity.Domain.Events;
using Starter.Module.CommentsActivity.Infrastructure.Persistence;

namespace Starter.Module.CommentsActivity.Application.EventHandlers;

/// <summary>
/// Republishes <see cref="CommentEditedEvent"/> as the public
/// <see cref="CommentEditedIntegrationEvent"/> so external consumers can
/// react to edits. See sibling
/// <see cref="PublishCommentCreatedIntegrationEvent"/> for the at-most-once
/// caveat this handler shares.
/// </summary>
internal sealed class PublishCommentEditedIntegrationEvent(
    CommentsActivityDbContext context,
    IMessagePublisher messagePublisher,
    TimeProvider clock,
    ILogger<PublishCommentEditedIntegrationEvent> logger)
    : INotificationHandler<CommentEditedEvent>
{
    public async Task Handle(CommentEditedEvent notification, CancellationToken cancellationToken)
    {
        var comment = await context.Comments.FindAsync([notification.CommentId], cancellationToken);
        if (comment is null)
        {
            logger.LogWarning(
                "Skipped publishing CommentEditedIntegrationEvent — comment {CommentId} not found in change tracker.",
                notification.CommentId);
            return;
        }

        await messagePublisher.PublishAsync(
            new CommentEditedIntegrationEvent(
                notification.CommentId,
                notification.EntityType,
                notification.EntityId,
                notification.TenantId,
                notification.EditorId,
                comment.Body,
                comment.MentionsJson,
                clock.GetUtcNow().UtcDateTime),
            cancellationToken);
    }
}
