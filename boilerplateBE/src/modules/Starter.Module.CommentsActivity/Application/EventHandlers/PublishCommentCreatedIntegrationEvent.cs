using MediatR;
using Microsoft.Extensions.Logging;
using Starter.Abstractions.Events.CommentsActivity;
using Starter.Application.Common.Interfaces;
using Starter.Module.CommentsActivity.Domain.Events;
using Starter.Module.CommentsActivity.Infrastructure.Persistence;

namespace Starter.Module.CommentsActivity.Application.EventHandlers;

/// <summary>
/// Republishes the internal <see cref="CommentCreatedEvent"/> as the public
/// <see cref="CommentCreatedIntegrationEvent"/> on the message bus so modules
/// outside this one (analytics, AI enrichment, audit, etc.) can subscribe with
/// an <c>IConsumer&lt;CommentCreatedIntegrationEvent&gt;</c>.
///
/// Runs inside the EF save pipeline (dispatched by
/// <c>DomainEventDispatcherInterceptor</c>). A crash between the broker
/// publish and the Postgres commit can leave the bus with a message for a
/// comment that never lands in the database — consumers must tolerate that.
/// See <c>ROADMAP.md</c> \"Transactional outbox on CommentsActivityDbContext\"
/// for the planned at-least-once upgrade.
/// </summary>
internal sealed class PublishCommentCreatedIntegrationEvent(
    CommentsActivityDbContext context,
    IMessagePublisher messagePublisher,
    TimeProvider clock,
    ILogger<PublishCommentCreatedIntegrationEvent> logger)
    : INotificationHandler<CommentCreatedEvent>
{
    public async Task Handle(CommentCreatedEvent notification, CancellationToken cancellationToken)
    {
        // The comment is tracked (added) but not yet committed when this
        // handler fires. FindAsync consults the change-tracker first, so the
        // new instance is returned without a database round-trip.
        var comment = await context.Comments.FindAsync([notification.CommentId], cancellationToken);
        if (comment is null)
        {
            logger.LogWarning(
                "Skipped publishing CommentCreatedIntegrationEvent — comment {CommentId} not found in change tracker.",
                notification.CommentId);
            return;
        }

        await messagePublisher.PublishAsync(
            new CommentCreatedIntegrationEvent(
                notification.CommentId,
                notification.EntityType,
                notification.EntityId,
                notification.TenantId,
                notification.AuthorId,
                comment.Body,
                notification.MentionsJson,
                notification.ParentCommentId,
                clock.GetUtcNow().UtcDateTime),
            cancellationToken);
    }
}
