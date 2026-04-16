using MediatR;
using Starter.Abstractions.Events.CommentsActivity;
using Starter.Application.Common.Interfaces;
using Starter.Module.CommentsActivity.Domain.Events;

namespace Starter.Module.CommentsActivity.Application.EventHandlers;

/// <summary>
/// Republishes <see cref="ReactionToggledEvent"/> as the public
/// <see cref="ReactionToggledIntegrationEvent"/> so external consumers can
/// react to reaction changes. <c>UserId</c> flows through the domain event
/// so the handler stays independent of request-scope services, making the
/// event payload trustworthy for out-of-band producers (background jobs,
/// webhook replays). See sibling
/// <see cref="PublishCommentCreatedIntegrationEvent"/> for the at-most-once
/// caveat this handler shares.
/// </summary>
internal sealed class PublishReactionToggledIntegrationEvent(
    IMessagePublisher messagePublisher,
    TimeProvider clock) : INotificationHandler<ReactionToggledEvent>
{
    public Task Handle(ReactionToggledEvent notification, CancellationToken cancellationToken) =>
        messagePublisher.PublishAsync(
            new ReactionToggledIntegrationEvent(
                notification.CommentId,
                notification.EntityType,
                notification.EntityId,
                notification.TenantId,
                notification.UserId,
                notification.ReactionType,
                notification.Added,
                clock.GetUtcNow().UtcDateTime),
            cancellationToken);
}
