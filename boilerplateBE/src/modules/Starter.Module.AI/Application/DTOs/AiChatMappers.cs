using Starter.Module.AI.Domain.Entities;

namespace Starter.Module.AI.Application.DTOs;

public static class AiChatMappers
{
    public static AiMessageDto ToDto(this AiMessage message) =>
        new(
            Id: message.Id,
            ConversationId: message.ConversationId,
            Role: message.Role.ToString(),
            Content: message.Content,
            Order: message.Order,
            InputTokens: message.InputTokens,
            OutputTokens: message.OutputTokens,
            CreatedAt: message.CreatedAt);

    public static AiConversationDto ToDto(this AiConversation conversation, string? assistantName = null) =>
        new(
            Id: conversation.Id,
            AssistantId: conversation.AssistantId,
            AssistantName: assistantName,
            UserId: conversation.UserId,
            Title: conversation.Title,
            Status: conversation.Status.ToString(),
            MessageCount: conversation.MessageCount,
            TotalTokensUsed: conversation.TotalTokensUsed,
            LastMessageAt: conversation.LastMessageAt,
            CreatedAt: conversation.CreatedAt);

    public static AiConversationDetailDto ToDetailDto(
        this AiConversation conversation,
        IReadOnlyList<AiMessage> messages,
        string? assistantName = null) =>
        new(
            Id: conversation.Id,
            AssistantId: conversation.AssistantId,
            AssistantName: assistantName,
            UserId: conversation.UserId,
            Title: conversation.Title,
            Status: conversation.Status.ToString(),
            MessageCount: conversation.MessageCount,
            TotalTokensUsed: conversation.TotalTokensUsed,
            LastMessageAt: conversation.LastMessageAt,
            CreatedAt: conversation.CreatedAt,
            Messages: messages.Select(m => m.ToDto()).ToList());
}
