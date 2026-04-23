namespace Starter.Module.CommentsActivity.Application.DTOs;

public sealed record CommentAttachmentDto(
    Guid Id,
    Guid FileMetadataId,
    string FileName,
    string ContentType,
    long Size,
    string? Url);
