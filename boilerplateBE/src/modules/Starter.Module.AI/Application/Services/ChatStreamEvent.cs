namespace Starter.Module.AI.Application.Services;

/// <summary>
/// One SSE frame written to the client. Type discriminates the payload:
///   "start"  — { ConversationId, UserMessageId, AssistantMessageId }
///   "delta"  — { Content } (text chunk)
///   "done"   — { MessageId, InputTokens, OutputTokens, FinishReason }
///   "error"  — { Code, Message }
///   "tool_call"   — { CallId, Name, ArgumentsJson } emitted once per tool invocation.
///   "tool_result" — { CallId, IsError, Content } emitted after the tool returns.
///
/// Stability of identifiers:
///   • start.ConversationId and start.UserMessageId are persisted before the frame is
///     emitted, so clients can rely on them immediately.
///   • start.AssistantMessageId is an advisory placeholder — the server reserves a Guid
///     so the UI can paint a shell message, but the authoritative id lands in done.MessageId.
///     If the stream fails mid-flight the advisory id is discarded (no row exists).
///     Clients should reconcile the advisory id to done.MessageId before persisting
///     any local state keyed off the message id.
/// </summary>
public sealed record ChatStreamEvent(string Type, object Data);
