using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Starter.Abstractions.Capabilities;
using Starter.Module.CommentsActivity.Domain.Entities;
using Starter.Module.CommentsActivity.Domain.Events;
using Starter.Module.CommentsActivity.Infrastructure.Persistence;
using DomainWatchReason = Starter.Module.CommentsActivity.Domain.Enums.WatchReason;

namespace Starter.Module.CommentsActivity.Application.EventHandlers;

/// <summary>
/// Consolidated handler: auto-watches the comment author and any mentioned users.
/// Single DB round-trip instead of two separate handlers.
/// </summary>
internal sealed class AutoWatchOnCommentHandler(
    CommentsActivityDbContext context,
    ICommentableEntityRegistry registry,
    ILogger<AutoWatchOnCommentHandler> logger) : INotificationHandler<CommentCreatedEvent>
{
    public async Task Handle(CommentCreatedEvent notification, CancellationToken cancellationToken)
    {
        try
        {
            var definition = registry.GetDefinition(notification.EntityType);

            // Collect all user IDs that should be auto-watched
            var candidateWatchers = new List<(Guid UserId, DomainWatchReason Reason)>();

            if (definition?.AutoWatchOnComment == true)
            {
                candidateWatchers.Add((notification.AuthorId, DomainWatchReason.Participated));
            }

            // Parse mentioned users
            if (!string.IsNullOrEmpty(notification.MentionsJson))
            {
                var mentionedUserIds = JsonSerializer.Deserialize<List<Guid>>(notification.MentionsJson);
                if (mentionedUserIds is { Count: > 0 })
                {
                    foreach (var userId in mentionedUserIds)
                    {
                        candidateWatchers.Add((userId, DomainWatchReason.Mentioned));
                    }
                }
            }

            if (candidateWatchers.Count == 0) return;

            // Deduplicate by userId (author takes priority if they're also mentioned)
            var uniqueUserIds = candidateWatchers.Select(c => c.UserId).Distinct().ToList();

            // Check which are already watching
            var existingWatcherUserIds = await context.EntityWatchers
                .Where(w => w.EntityType == notification.EntityType &&
                            w.EntityId == notification.EntityId &&
                            uniqueUserIds.Contains(w.UserId))
                .Select(w => w.UserId)
                .ToListAsync(cancellationToken);

            var existingSet = existingWatcherUserIds.ToHashSet();
            var added = false;

            foreach (var candidate in candidateWatchers)
            {
                if (existingSet.Contains(candidate.UserId)) continue;
                existingSet.Add(candidate.UserId); // prevent duplicate adds within this batch

                var watcher = EntityWatcher.Create(
                    notification.TenantId,
                    notification.EntityType,
                    notification.EntityId,
                    candidate.UserId,
                    candidate.Reason);

                context.EntityWatchers.Add(watcher);
                added = true;
            }

            if (added)
            {
                await context.SaveChangesAsync(cancellationToken);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to auto-watch on comment for {EntityType}/{EntityId}",
                notification.EntityType, notification.EntityId);
        }
    }
}
