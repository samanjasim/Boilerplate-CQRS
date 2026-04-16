using MediatR;
using Microsoft.Extensions.Logging;
using Starter.Application.Common.Interfaces;
using Starter.Module.CommentsActivity.Domain.Events;

namespace Starter.Module.CommentsActivity.Application.EventHandlers;

internal sealed class PublishCommentCreatedRealtimeHandler(
    IRealtimeService realtimeService,
    ILogger<PublishCommentCreatedRealtimeHandler> logger) : INotificationHandler<CommentCreatedEvent>
{
    public async Task Handle(CommentCreatedEvent notification, CancellationToken cancellationToken)
    {
        try
        {
            var channel = $"entity-{notification.TenantId}-{notification.EntityType}-{notification.EntityId}";
            await realtimeService.PublishToChannelAsync(channel, "comment:created", new
            {
                commentId = notification.CommentId,
                entityType = notification.EntityType,
                entityId = notification.EntityId,
                authorId = notification.AuthorId
            }, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to publish realtime event for CommentCreatedEvent {CommentId}",
                notification.CommentId);
        }
    }
}

internal sealed class PublishCommentEditedRealtimeHandler(
    IRealtimeService realtimeService,
    ILogger<PublishCommentEditedRealtimeHandler> logger) : INotificationHandler<CommentEditedEvent>
{
    public async Task Handle(CommentEditedEvent notification, CancellationToken cancellationToken)
    {
        try
        {
            var channel = $"entity-{notification.TenantId}-{notification.EntityType}-{notification.EntityId}";
            await realtimeService.PublishToChannelAsync(channel, "comment:updated", new
            {
                commentId = notification.CommentId,
                entityType = notification.EntityType,
                entityId = notification.EntityId
            }, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to publish realtime event for CommentEditedEvent {CommentId}",
                notification.CommentId);
        }
    }
}

internal sealed class PublishCommentDeletedRealtimeHandler(
    IRealtimeService realtimeService,
    ILogger<PublishCommentDeletedRealtimeHandler> logger) : INotificationHandler<CommentDeletedEvent>
{
    public async Task Handle(CommentDeletedEvent notification, CancellationToken cancellationToken)
    {
        try
        {
            var channel = $"entity-{notification.TenantId}-{notification.EntityType}-{notification.EntityId}";
            await realtimeService.PublishToChannelAsync(channel, "comment:deleted", new
            {
                commentId = notification.CommentId,
                entityType = notification.EntityType,
                entityId = notification.EntityId,
                deletedBy = notification.DeletedBy
            }, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to publish realtime event for CommentDeletedEvent {CommentId}",
                notification.CommentId);
        }
    }
}

internal sealed class PublishReactionToggledRealtimeHandler(
    IRealtimeService realtimeService,
    ILogger<PublishReactionToggledRealtimeHandler> logger) : INotificationHandler<ReactionToggledEvent>
{
    public async Task Handle(ReactionToggledEvent notification, CancellationToken cancellationToken)
    {
        try
        {
            var channel = $"entity-{notification.TenantId}-{notification.EntityType}-{notification.EntityId}";
            await realtimeService.PublishToChannelAsync(channel, "reaction:changed", new
            {
                commentId = notification.CommentId,
                entityType = notification.EntityType,
                entityId = notification.EntityId,
                reactionType = notification.ReactionType,
                added = notification.Added
            }, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to publish realtime event for ReactionToggledEvent {CommentId}",
                notification.CommentId);
        }
    }
}
