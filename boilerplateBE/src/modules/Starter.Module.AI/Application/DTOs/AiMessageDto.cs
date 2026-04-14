namespace Starter.Module.AI.Application.DTOs;

public sealed record AiMessageDto(
    Guid Id,
    Guid ConversationId,
    string Role,
    string? Content,
    int Order,
    int InputTokens,
    int OutputTokens,
    DateTime CreatedAt);
