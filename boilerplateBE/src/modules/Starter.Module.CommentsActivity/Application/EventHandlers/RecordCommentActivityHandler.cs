using System.Text.Json;
using MediatR;
using Starter.Module.CommentsActivity.Constants;
using Starter.Module.CommentsActivity.Domain.Entities;
using Starter.Module.CommentsActivity.Domain.Events;
using Starter.Module.CommentsActivity.Infrastructure.Persistence;

namespace Starter.Module.CommentsActivity.Application.EventHandlers;

// No try/catch: activity-feed rows are a functional part of the comment write,
// not observability. If the insert fails we want the surrounding transaction to
// abort so the feed never silently diverges from the comment thread.
internal sealed class RecordCommentActivityHandler(
    CommentsActivityDbContext context) : INotificationHandler<CommentCreatedEvent>
{
    public async Task Handle(CommentCreatedEvent notification, CancellationToken cancellationToken)
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
            CommentsActivityActions.CommentAdded,
            notification.AuthorId,
            metadata,
            notification.ParentCommentId.HasValue ? "Replied to a comment" : "Added a comment");

        context.ActivityEntries.Add(activity);
        await context.SaveChangesAsync(cancellationToken);
    }
}
