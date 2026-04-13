using Starter.Domain.Common;
using Starter.Module.AI.Domain.Enums;

namespace Starter.Module.AI.Domain.Entities;

public sealed class AiMessage : BaseEntity
{
    public Guid ConversationId { get; private set; }
    public MessageRole Role { get; private set; }
    public string? Content { get; private set; }
    public string? ToolCalls { get; private set; }
    public string? ToolCallId { get; private set; }
    public int InputTokens { get; private set; }
    public int OutputTokens { get; private set; }
    public int Order { get; private set; }

    private AiMessage() { }

    private AiMessage(
        Guid id,
        Guid conversationId,
        MessageRole role,
        string? content,
        string? toolCalls,
        string? toolCallId,
        int inputTokens,
        int outputTokens,
        int order) : base(id)
    {
        ConversationId = conversationId;
        Role = role;
        Content = content;
        ToolCalls = toolCalls;
        ToolCallId = toolCallId;
        InputTokens = inputTokens;
        OutputTokens = outputTokens;
        Order = order;
    }

    public static AiMessage CreateUserMessage(
        Guid conversationId,
        string content,
        int order,
        int inputTokens = 0)
    {
        return new AiMessage(
            Guid.NewGuid(),
            conversationId,
            MessageRole.User,
            content,
            null,
            null,
            inputTokens,
            0,
            order);
    }

    public static AiMessage CreateAssistantMessage(
        Guid conversationId,
        string? content,
        int order,
        int inputTokens = 0,
        int outputTokens = 0,
        string? toolCalls = null)
    {
        return new AiMessage(
            Guid.NewGuid(),
            conversationId,
            MessageRole.Assistant,
            content,
            toolCalls,
            null,
            inputTokens,
            outputTokens,
            order);
    }

    public static AiMessage CreateToolResultMessage(
        Guid conversationId,
        string toolCallId,
        string content,
        int order)
    {
        return new AiMessage(
            Guid.NewGuid(),
            conversationId,
            MessageRole.ToolResult,
            content,
            null,
            toolCallId,
            0,
            0,
            order);
    }

    public static AiMessage CreateSystemMessage(
        Guid conversationId,
        string content,
        int order)
    {
        return new AiMessage(
            Guid.NewGuid(),
            conversationId,
            MessageRole.System,
            content,
            null,
            null,
            0,
            0,
            order);
    }
}
