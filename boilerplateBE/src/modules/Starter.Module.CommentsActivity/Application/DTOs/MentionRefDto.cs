namespace Starter.Module.CommentsActivity.Application.DTOs;

public sealed record MentionRefDto(Guid UserId, string Username, string DisplayName);
