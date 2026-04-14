namespace Starter.Module.AI.Application.Services;

/// <summary>
/// One SSE frame written to the client. Type discriminates the payload:
///   "start"  — { ConversationId, UserMessageId, AssistantMessageId }
///   "delta"  — { Content } (text chunk)
///   "done"   — { InputTokens, OutputTokens, FinishReason }
///   "error"  — { Code, Message }
/// </summary>
public sealed record ChatStreamEvent(string Type, object Data);
