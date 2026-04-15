using MediatR;
using Starter.Abstractions.Events.CommentsActivity;
using Starter.Application.Common.Interfaces;
using Starter.Module.CommentsActivity.Domain.Events;

namespace Starter.Module.CommentsActivity.Application.EventHandlers;

/// <summary>
/// Republishes <see cref="ReactionToggledEvent"/> as the public
/// <see cref="ReactionToggledIntegrationEvent"/> so external consumers can
/// react to reaction changes. <c>UserId</c> is read from
/// <see cref="ICurrentUserService"/> because the internal domain event does
/// not carry it — toggling reactions is only reachable through the HTTP
/// command path today, where the current user is always set. See sibling
/// <see cref="PublishCommentCreatedIntegrationEvent"/> for the at-most-once
/// caveat this handler shares.
/// </summary>
internal sealed class PublishReactionToggledIntegrationEvent(
    ICurrentUserService currentUser,
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
                currentUser.UserId ?? Guid.Empty,
                notification.ReactionType,
                notification.Added,
                clock.GetUtcNow().UtcDateTime),
            cancellationToken);
}
