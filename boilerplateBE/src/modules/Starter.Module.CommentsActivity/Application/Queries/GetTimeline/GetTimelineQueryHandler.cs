using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Starter.Abstractions.Readers;
using Starter.Application.Common.Interfaces;
using Starter.Application.Common.Models;
using Starter.Module.CommentsActivity.Application.DTOs;
using Starter.Module.CommentsActivity.Infrastructure.Persistence;
using Starter.Shared.Results;

namespace Starter.Module.CommentsActivity.Application.Queries.GetTimeline;

internal sealed class GetTimelineQueryHandler(
    CommentsActivityDbContext context,
    IUserReader userReader,
    ICurrentUserService currentUser) : IRequestHandler<GetTimelineQuery, Result<PaginatedList<TimelineItemDto>>>
{
    public async Task<Result<PaginatedList<TimelineItemDto>>> Handle(
        GetTimelineQuery request, CancellationToken cancellationToken)
    {
        var items = new List<TimelineItemDto>();
        var filter = request.Filter.ToLowerInvariant();

        // Load comments if needed
        if (filter is "all" or "comments")
        {
            var comments = await context.Comments
                .AsNoTracking()
                .Where(c => c.EntityType == request.EntityType &&
                            c.EntityId == request.EntityId &&
                            c.ParentCommentId == null)
                .OrderBy(c => c.CreatedAt)
                .ToListAsync(cancellationToken);

            var commentIds = comments.Select(c => c.Id).ToList();

            var replies = await context.Comments
                .AsNoTracking()
                .Where(c => c.ParentCommentId != null && commentIds.Contains(c.ParentCommentId.Value))
                .OrderBy(c => c.CreatedAt)
                .ToListAsync(cancellationToken);

            var allCommentIds = commentIds.Concat(replies.Select(r => r.Id)).ToList();

            var attachments = await context.CommentAttachments
                .AsNoTracking()
                .Where(a => allCommentIds.Contains(a.CommentId))
                .ToListAsync(cancellationToken);

            var reactions = await context.CommentReactions
                .AsNoTracking()
                .Where(r => allCommentIds.Contains(r.CommentId))
                .ToListAsync(cancellationToken);

            var authorIds = comments.Select(c => c.AuthorId)
                .Concat(replies.Select(r => r.AuthorId))
                .Distinct();
            var commentUsers = await userReader.GetManyAsync(authorIds, cancellationToken);
            var commentUserMap = commentUsers.ToDictionary(u => u.Id);
            var currentUserId = currentUser.UserId;

            foreach (var comment in comments)
            {
                var author = commentUserMap.GetValueOrDefault(comment.AuthorId);
                var commentReplies = replies
                    .Where(r => r.ParentCommentId == comment.Id)
                    .Select(r =>
                    {
                        var replyAuthor = commentUserMap.GetValueOrDefault(r.AuthorId);
                        var replyAttachments = attachments.Where(a => a.CommentId == r.Id)
                            .Select(a => new CommentAttachmentDto(a.Id, a.FileMetadataId, string.Empty, string.Empty, 0, null))
                            .ToList();
                        var replyReactions = reactions.Where(rx => rx.CommentId == r.Id)
                            .GroupBy(rx => rx.ReactionType)
                            .Select(g => new ReactionSummaryDto(g.Key, g.Count(),
                                currentUserId.HasValue && g.Any(rx => rx.UserId == currentUserId.Value)))
                            .ToList();
                        var replyMentions = ParseMentions(r.MentionsJson, commentUserMap);
                        return new CommentDto(r.Id, r.EntityType, r.EntityId, r.ParentCommentId,
                            r.AuthorId, replyAuthor?.DisplayName ?? "Unknown", replyAuthor?.Email ?? string.Empty,
                            r.Body, replyMentions, replyAttachments, replyReactions, r.IsDeleted, null,
                            r.CreatedAt, r.ModifiedAt);
                    })
                    .ToList();

                var commentAttachments = attachments.Where(a => a.CommentId == comment.Id)
                    .Select(a => new CommentAttachmentDto(a.Id, a.FileMetadataId, string.Empty, string.Empty, 0, null))
                    .ToList();
                var commentReactions = reactions.Where(rx => rx.CommentId == comment.Id)
                    .GroupBy(rx => rx.ReactionType)
                    .Select(g => new ReactionSummaryDto(g.Key, g.Count(),
                        currentUserId.HasValue && g.Any(rx => rx.UserId == currentUserId.Value)))
                    .ToList();
                var mentions = ParseMentions(comment.MentionsJson, commentUserMap);

                var dto = new CommentDto(comment.Id, comment.EntityType, comment.EntityId,
                    comment.ParentCommentId, comment.AuthorId,
                    author?.DisplayName ?? "Unknown", author?.Email ?? string.Empty,
                    comment.Body, mentions, commentAttachments, commentReactions, comment.IsDeleted,
                    commentReplies.Count > 0 ? commentReplies : null,
                    comment.CreatedAt, comment.ModifiedAt);

                items.Add(new TimelineItemDto("comment", dto, null, comment.CreatedAt));
            }
        }

        // Load activity if needed
        if (filter is "all" or "activity")
        {
            var activities = await context.ActivityEntries
                .AsNoTracking()
                .Where(a => a.EntityType == request.EntityType && a.EntityId == request.EntityId)
                .OrderBy(a => a.CreatedAt)
                .ToListAsync(cancellationToken);

            var actorIds = activities.Where(a => a.ActorId.HasValue).Select(a => a.ActorId!.Value).Distinct();
            var actorUsers = await userReader.GetManyAsync(actorIds, cancellationToken);
            var actorMap = actorUsers.ToDictionary(u => u.Id);

            foreach (var activity in activities)
            {
                var actorName = activity.ActorId.HasValue && actorMap.TryGetValue(activity.ActorId.Value, out var actor)
                    ? actor.DisplayName
                    : null;

                var dto = new ActivityEntryDto(activity.Id, activity.EntityType, activity.EntityId,
                    activity.Action, activity.ActorId, actorName, activity.MetadataJson,
                    activity.Description, activity.CreatedAt);

                items.Add(new TimelineItemDto("activity", null, dto, activity.CreatedAt));
            }
        }

        // Sort merged list by timestamp
        var sorted = items.OrderBy(i => i.Timestamp).ToList();

        // Manual pagination
        var totalCount = sorted.Count;
        var paged = sorted
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToList();

        var result = PaginatedList<TimelineItemDto>.Create(
            paged.AsReadOnly(), totalCount, request.PageNumber, request.PageSize);

        return Result.Success(result);
    }

    private static List<MentionRefDto>? ParseMentions(
        string? mentionsJson,
        Dictionary<Guid, Starter.Abstractions.Readers.UserSummary> userMap)
    {
        if (string.IsNullOrEmpty(mentionsJson)) return null;
        try
        {
            var userIds = JsonSerializer.Deserialize<List<Guid>>(mentionsJson);
            if (userIds is null or { Count: 0 }) return null;
            return userIds
                .Where(id => userMap.ContainsKey(id))
                .Select(id => new MentionRefDto(userMap[id].Id, userMap[id].Username, userMap[id].DisplayName))
                .ToList();
        }
        catch { return null; }
    }
}
