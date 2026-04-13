using System.Text.Json;
using MediatR;
using Microsoft.Extensions.Logging;
using Starter.Module.CommentsActivity.Domain.Entities;
using Starter.Module.CommentsActivity.Domain.Events;
using Starter.Module.CommentsActivity.Infrastructure.Persistence;

namespace Starter.Module.CommentsActivity.Application.EventHandlers;

internal sealed class RecordCommentActivityHandler(
    CommentsActivityDbContext context,
    ILogger<RecordCommentActivityHandler> logger) : INotificationHandler<CommentCreatedEvent>
{
    public async Task Handle(CommentCreatedEvent notification, CancellationToken cancellationToken)
    {
        try
        {
            var metadata = JsonSerializer.Serialize(new
            {
                commentId = notification.CommentId,
                parentCommentId = notification.ParentCommentId
            });

            var activity = ActivityEntry.Create(
                notification.TenantId,
                notification.EntityType,
                notification.EntityId,
                "comment_added",
                notification.AuthorId,
                metadata,
                notification.ParentCommentId.HasValue ? "Replied to a comment" : "Added a comment");

            context.ActivityEntries.Add(activity);
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to record activity for CommentCreatedEvent {CommentId}",
                notification.CommentId);
        }
    }
}
