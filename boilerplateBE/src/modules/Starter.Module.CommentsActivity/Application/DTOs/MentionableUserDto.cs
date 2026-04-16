namespace Starter.Module.CommentsActivity.Application.DTOs;

public sealed record MentionableUserDto(Guid Id, string Username, string DisplayName, string Email);
