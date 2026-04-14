namespace Starter.Module.AI.Application.DTOs;

public sealed record AiConversationDto(
    Guid Id,
    Guid AssistantId,
    string? AssistantName,
    Guid UserId,
    string? Title,
    string Status,
    int MessageCount,
    int TotalTokensUsed,
    DateTime LastMessageAt,
    DateTime CreatedAt);
