namespace Starter.Module.AI.Application.Services;

/// <summary>
/// One SSE frame written to the client. Type discriminates the payload:
///   "start"       — { ConversationId, UserMessageId }
///   "delta"       — { Content } (text chunk)
///   "tool_call"   — { CallId, Name, ArgumentsJson } emitted once per tool invocation.
///   "tool_result" — { CallId, IsError, Content } emitted after the tool returns.
///   "done"        — { MessageId, InputTokens, OutputTokens, FinishReason }
///   "error"       — { Code, Message }
///
/// Stability of identifiers:
///   • start.ConversationId and start.UserMessageId are persisted before the frame is
///     emitted, so clients can rely on them immediately.
///   • The authoritative final-message id arrives in done.MessageId. With tool-calling turns
///     the server may persist multiple assistant rows (one per round); only the final text
///     row is surfaced through done.MessageId.
/// </summary>
public sealed record ChatStreamEvent(string Type, object Data);
