namespace Starter.Module.CommentsActivity.Application.DTOs;

public sealed record ReactionSummaryDto(string ReactionType, int Count, bool UserReacted);
