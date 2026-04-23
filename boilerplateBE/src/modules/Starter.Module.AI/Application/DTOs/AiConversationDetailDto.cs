namespace Starter.Module.AI.Application.DTOs;

public sealed record AiConversationDetailDto(
    Guid Id,
    Guid AssistantId,
    string? AssistantName,
    Guid UserId,
    string? Title,
    string Status,
    int MessageCount,
    int TotalTokensUsed,
    DateTime LastMessageAt,
    DateTime CreatedAt,
    IReadOnlyList<AiMessageDto> Messages);
