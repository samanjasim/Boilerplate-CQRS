using System.Text.Json;
using Starter.Abstractions.Readers;
using Starter.Module.CommentsActivity.Domain.Entities;

namespace Starter.Module.CommentsActivity.Application.DTOs;

internal static class CommentDtoMapper
{
    public static CommentDto MapComment(
        Comment comment,
        List<Comment> allReplies,
        List<CommentAttachment> allAttachments,
        List<CommentReaction> allReactions,
        Dictionary<Guid, UserSummary> userMap,
        Dictionary<Guid, FileSummary> fileMap,
        Dictionary<Guid, string> fileUrlMap,
        Guid? currentUserId)
    {
        var author = userMap.GetValueOrDefault(comment.AuthorId);
        var commentReplies = allReplies
            .Where(r => r.ParentCommentId == comment.Id)
            .Select(r => MapReply(r, allAttachments, allReactions, userMap, fileMap, fileUrlMap, currentUserId))
            .ToList();

        var commentAttachments = allAttachments
            .Where(a => a.CommentId == comment.Id)
            .Select(a => MapAttachment(a, fileMap, fileUrlMap))
            .ToList();

        var commentReactions = MapReactions(allReactions, comment.Id, currentUserId);
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

    public static CommentDto MapReply(
        Comment reply,
        List<CommentAttachment> allAttachments,
        List<CommentReaction> allReactions,
        Dictionary<Guid, UserSummary> userMap,
        Dictionary<Guid, FileSummary> fileMap,
        Dictionary<Guid, string> fileUrlMap,
        Guid? currentUserId)
    {
        var author = userMap.GetValueOrDefault(reply.AuthorId);

        var replyAttachments = allAttachments
            .Where(a => a.CommentId == reply.Id)
            .Select(a => MapAttachment(a, fileMap, fileUrlMap))
            .ToList();

        var replyReactions = MapReactions(allReactions, reply.Id, currentUserId);
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

    public static CommentAttachmentDto MapAttachment(
        CommentAttachment attachment,
        Dictionary<Guid, FileSummary> fileMap,
        Dictionary<Guid, string> fileUrlMap)
    {
        var file = fileMap.GetValueOrDefault(attachment.FileMetadataId);
        var url = fileUrlMap.GetValueOrDefault(attachment.FileMetadataId);
        return new CommentAttachmentDto(
            attachment.Id,
            attachment.FileMetadataId,
            file?.FileName ?? string.Empty,
            file?.ContentType ?? string.Empty,
            file?.Size ?? 0,
            url);
    }

    public static List<ReactionSummaryDto> MapReactions(
        List<CommentReaction> allReactions,
        Guid commentId,
        Guid? currentUserId)
    {
        return allReactions
            .Where(r => r.CommentId == commentId)
            .GroupBy(r => r.ReactionType)
            .Select(g => new ReactionSummaryDto(
                g.Key,
                g.Count(),
                currentUserId.HasValue && g.Any(r => r.UserId == currentUserId.Value)))
            .ToList();
    }

    public static List<MentionRefDto>? ParseMentions(
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
                .Select(id => new MentionRefDto(userMap[id].Id, userMap[id].Username, userMap[id].DisplayName))
                .ToList();
        }
        catch { return null; }
    }
}
