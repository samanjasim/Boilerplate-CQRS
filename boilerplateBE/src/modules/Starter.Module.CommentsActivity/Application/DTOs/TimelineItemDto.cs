namespace Starter.Module.CommentsActivity.Application.DTOs;

public sealed record TimelineItemDto(
    string Type,
    CommentDto? Comment,
    ActivityEntryDto? Activity,
    DateTime Timestamp);
