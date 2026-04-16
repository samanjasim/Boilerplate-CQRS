namespace Starter.Module.CommentsActivity.Application.DTOs;

public sealed record CommentDto(
    Guid Id,
    string EntityType,
    Guid EntityId,
    Guid? ParentCommentId,
    Guid AuthorId,
    string AuthorName,
    string AuthorEmail,
    string Body,
    List<MentionRefDto>? Mentions,
    List<CommentAttachmentDto> Attachments,
    List<ReactionSummaryDto> Reactions,
    bool IsDeleted,
    List<CommentDto>? Replies,
    DateTime CreatedAt,
    DateTime? ModifiedAt);
