using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Starter.Abstractions.Readers;
using Starter.Application.Common.Interfaces;
using Starter.Application.Common.Models;
using Starter.Module.CommentsActivity.Application.DTOs;
using Starter.Module.CommentsActivity.Domain.Entities;
using Starter.Module.CommentsActivity.Infrastructure.Persistence;
using Starter.Shared.Results;

namespace Starter.Module.CommentsActivity.Application.Queries.GetComments;

internal sealed class GetCommentsQueryHandler(
    CommentsActivityDbContext context,
    IUserReader userReader,
    ICurrentUserService currentUser) : IRequestHandler<GetCommentsQuery, Result<PaginatedList<CommentDto>>>
{
    public async Task<Result<PaginatedList<CommentDto>>> Handle(
        GetCommentsQuery request, CancellationToken cancellationToken)
    {
        var query = context.Comments
            .AsNoTracking()
            .Where(c => c.EntityType == request.EntityType && c.EntityId == request.EntityId)
            .Where(c => c.ParentCommentId == null)
            .OrderBy(c => c.CreatedAt);

        var page = await PaginatedList<Comment>.CreateAsync(
            query, request.PageNumber, request.PageSize, cancellationToken);

        var commentIds = page.Items.Select(c => c.Id).ToList();

        // Load replies for all top-level comments on this page
        var replies = await context.Comments
            .AsNoTracking()
            .Where(c => c.ParentCommentId != null && commentIds.Contains(c.ParentCommentId.Value))
            .OrderBy(c => c.CreatedAt)
            .ToListAsync(cancellationToken);

        // Load attachments for all comments (top-level + replies)
        var allCommentIds = commentIds.Concat(replies.Select(r => r.Id)).ToList();
        var attachments = await context.CommentAttachments
            .AsNoTracking()
            .Where(a => allCommentIds.Contains(a.CommentId))
            .ToListAsync(cancellationToken);

        // Load reactions for all comments
        var reactions = await context.CommentReactions
            .AsNoTracking()
            .Where(r => allCommentIds.Contains(r.CommentId))
            .ToListAsync(cancellationToken);

        // Resolve all author names
        var authorIds = page.Items.Select(c => c.AuthorId)
            .Concat(replies.Select(r => r.AuthorId))
            .Distinct();
        var users = await userReader.GetManyAsync(authorIds, cancellationToken);
        var userMap = users.ToDictionary(u => u.Id);

        var currentUserId = currentUser.UserId;

        var dtos = page.Items.Select(c => MapToDto(
            c, replies, attachments, reactions, userMap, currentUserId)).ToList();

        var result = PaginatedList<CommentDto>.Create(
            dtos.AsReadOnly(), page.TotalCount, page.PageNumber, page.PageSize);

        return Result.Success(result);
    }

    private static CommentDto MapToDto(
        Comment comment,
        List<Comment> allReplies,
        List<CommentAttachment> allAttachments,
        List<CommentReaction> allReactions,
        Dictionary<Guid, UserSummary> userMap,
        Guid? currentUserId)
    {
        var author = userMap.GetValueOrDefault(comment.AuthorId);
        var commentReplies = allReplies
            .Where(r => r.ParentCommentId == comment.Id)
            .Select(r => MapReplyToDto(r, allAttachments, allReactions, userMap, currentUserId))
            .ToList();

        var commentAttachments = allAttachments
            .Where(a => a.CommentId == comment.Id)
            .Select(a => new CommentAttachmentDto(a.Id, a.FileMetadataId, string.Empty, string.Empty, 0, null))
            .ToList();

        var commentReactions = allReactions
            .Where(r => r.CommentId == comment.Id)
            .GroupBy(r => r.ReactionType)
            .Select(g => new ReactionSummaryDto(
                g.Key,
                g.Count(),
                currentUserId.HasValue && g.Any(r => r.UserId == currentUserId.Value)))
            .ToList();

        var mentions = ParseMentions(comment.MentionsJson, userMap);

        return new CommentDto(
            comment.Id,
            comment.EntityType,
            comment.EntityId,
            comment.ParentCommentId,
            comment.AuthorId,
            author?.DisplayName ?? "Unknown",
            author?.Email ?? string.Empty,
            comment.Body,
            mentions,
            commentAttachments,
            commentReactions,
            comment.IsDeleted,
            commentReplies.Count > 0 ? commentReplies : null,
            comment.CreatedAt,
            comment.ModifiedAt);
    }

    private static CommentDto MapReplyToDto(
        Comment reply,
        List<CommentAttachment> allAttachments,
        List<CommentReaction> allReactions,
        Dictionary<Guid, UserSummary> userMap,
        Guid? currentUserId)
    {
        var author = userMap.GetValueOrDefault(reply.AuthorId);

        var replyAttachments = allAttachments
            .Where(a => a.CommentId == reply.Id)
            .Select(a => new CommentAttachmentDto(a.Id, a.FileMetadataId, string.Empty, string.Empty, 0, null))
            .ToList();

        var replyReactions = allReactions
            .Where(r => r.CommentId == reply.Id)
            .GroupBy(r => r.ReactionType)
            .Select(g => new ReactionSummaryDto(
                g.Key,
                g.Count(),
                currentUserId.HasValue && g.Any(r => r.UserId == currentUserId.Value)))
            .ToList();

        var mentions = ParseMentions(reply.MentionsJson, userMap);

        return new CommentDto(
            reply.Id,
            reply.EntityType,
            reply.EntityId,
            reply.ParentCommentId,
            reply.AuthorId,
            author?.DisplayName ?? "Unknown",
            author?.Email ?? string.Empty,
            reply.Body,
            mentions,
            replyAttachments,
            replyReactions,
            reply.IsDeleted,
            null,
            reply.CreatedAt,
            reply.ModifiedAt);
    }

    private static List<MentionRefDto>? ParseMentions(
        string? mentionsJson,
        Dictionary<Guid, UserSummary> userMap)
    {
        if (string.IsNullOrEmpty(mentionsJson)) return null;

        try
        {
            var userIds = JsonSerializer.Deserialize<List<Guid>>(mentionsJson);
            if (userIds is null or { Count: 0 }) return null;

            return userIds
                .Where(id => userMap.ContainsKey(id))
                .Select(id =>
                {
                    var user = userMap[id];
                    return new MentionRefDto(user.Id, user.Username, user.DisplayName);
                })
                .ToList();
        }
        catch
        {
            return null;
        }
    }
}
