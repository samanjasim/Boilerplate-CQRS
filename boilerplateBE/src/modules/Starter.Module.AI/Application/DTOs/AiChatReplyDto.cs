namespace Starter.Module.AI.Application.DTOs;

public sealed record AiChatReplyDto(
    Guid ConversationId,
    AiMessageDto UserMessage,
    AiMessageDto AssistantMessage,
    string? PersonaSlug = null);
