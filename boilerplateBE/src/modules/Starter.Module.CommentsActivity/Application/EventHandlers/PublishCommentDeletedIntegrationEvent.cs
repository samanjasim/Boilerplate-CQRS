using MediatR;
using Starter.Abstractions.Events.CommentsActivity;
using Starter.Application.Common.Interfaces;
using Starter.Module.CommentsActivity.Domain.Events;

namespace Starter.Module.CommentsActivity.Application.EventHandlers;

/// <summary>
/// Republishes <see cref="CommentDeletedEvent"/> as the public
/// <see cref="CommentDeletedIntegrationEvent"/> so external consumers can
/// react to deletions. See sibling
/// <see cref="PublishCommentCreatedIntegrationEvent"/> for the at-most-once
/// caveat this handler shares.
/// </summary>
internal sealed class PublishCommentDeletedIntegrationEvent(
    IMessagePublisher messagePublisher,
    TimeProvider clock) : INotificationHandler<CommentDeletedEvent>
{
    public Task Handle(CommentDeletedEvent notification, CancellationToken cancellationToken) =>
        messagePublisher.PublishAsync(
            new CommentDeletedIntegrationEvent(
                notification.CommentId,
                notification.EntityType,
                notification.EntityId,
                notification.TenantId,
                notification.DeletedBy,
                clock.GetUtcNow().UtcDateTime),
            cancellationToken);
}
