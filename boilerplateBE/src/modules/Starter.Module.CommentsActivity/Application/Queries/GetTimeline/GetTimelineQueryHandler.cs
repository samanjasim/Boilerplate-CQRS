using Starter.Abstractions.Paging;
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
    IFileReader fileReader,
    IFileService fileService,
    ICurrentUserService currentUser) : IRequestHandler<GetTimelineQuery, Result<PaginatedList<TimelineItemDto>>>
{
    public async Task<Result<PaginatedList<TimelineItemDto>>> Handle(
        GetTimelineQuery request, CancellationToken cancellationToken)
    {
        var filter = request.Filter.ToLowerInvariant();
        var includeComments = filter is "all" or "comments";
        var includeActivity = filter is "all" or "activity";

        // Step 1: Get lightweight (type, id, timestamp) entries at DB level for pagination
        var entries = new List<(string Type, Guid Id, DateTime Timestamp)>();

        if (includeComments)
        {
            var commentEntries = await context.Comments
                .AsNoTracking()
                .Where(c => c.EntityType == request.EntityType &&
                            c.EntityId == request.EntityId &&
                            c.ParentCommentId == null)
                .Select(c => new { c.Id, c.CreatedAt })
                .ToListAsync(cancellationToken);
            entries.AddRange(commentEntries.Select(c => ("comment", c.Id, c.CreatedAt)));
        }

        if (includeActivity)
        {
            var activityEntries = await context.ActivityEntries
                .AsNoTracking()
                .Where(a => a.EntityType == request.EntityType && a.EntityId == request.EntityId)
                .Select(a => new { a.Id, a.CreatedAt })
                .ToListAsync(cancellationToken);
            entries.AddRange(activityEntries.Select(a => ("activity", a.Id, a.CreatedAt)));
        }

        // Step 2: Sort and paginate the lightweight entries
        var totalCount = entries.Count;
        var pagedEntries = entries
            .OrderByDescending(e => e.Timestamp)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToList();

        // Step 3: Hydrate only the items on this page
        var pagedCommentIds = pagedEntries.Where(e => e.Type == "comment").Select(e => e.Id).ToList();
        var pagedActivityIds = pagedEntries.Where(e => e.Type == "activity").Select(e => e.Id).ToList();

        var items = new List<TimelineItemDto>();

        if (pagedCommentIds.Count > 0)
        {
            var comments = await context.Comments
                .AsNoTracking()
                .Where(c => pagedCommentIds.Contains(c.Id))
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

            // Resolve file metadata for attachments
            var fileIds = attachments.Select(a => a.FileMetadataId).Distinct();
            var fileSummaries = await fileReader.GetManyAsync(fileIds, cancellationToken);
            var fileMap = fileSummaries.ToDictionary(f => f.Id);
            var fileUrlMap = new Dictionary<Guid, string>();
            foreach (var fid in fileMap.Keys)
            {
                fileUrlMap[fid] = await fileService.GetUrlAsync(fid, cancellationToken);
            }

            var authorIds = comments.Select(c => c.AuthorId)
                .Concat(replies.Select(r => r.AuthorId))
                .Distinct();
            var commentUsers = await userReader.GetManyAsync(authorIds, cancellationToken);
            var commentUserMap = commentUsers.ToDictionary(u => u.Id);
            var currentUserId = currentUser.UserId;

            foreach (var comment in comments)
            {
                var dto = CommentDtoMapper.MapComment(
                    comment, replies, attachments, reactions,
                    commentUserMap, fileMap, fileUrlMap, currentUserId);

                items.Add(new TimelineItemDto("comment", dto, null, comment.CreatedAt));
            }
        }

        if (pagedActivityIds.Count > 0)
        {
            var activities = await context.ActivityEntries
                .AsNoTracking()
                .Where(a => pagedActivityIds.Contains(a.Id))
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

        // Re-sort the hydrated page items (newest first)
        var sorted = items.OrderByDescending(i => i.Timestamp).ToList();

        var result = PaginatedList<TimelineItemDto>.Create(
            sorted.AsReadOnly(), totalCount, request.PageNumber, request.PageSize);

        return Result.Success(result);
    }
}
