using Starter.Module.AI.Application.DTOs;
using Starter.Shared.Results;

namespace Starter.Module.AI.Application.Services;

public interface IChatExecutionService
{
    /// <summary>
    /// Run a full (non-streaming) chat turn. Persists user + assistant messages,
    /// writes usage log, increments usage tracker, publishes webhook.
    /// </summary>
    Task<Result<AiChatReplyDto>> ExecuteAsync(
        Guid? conversationId,
        Guid? assistantId,
        string userMessage,
        Guid? personaId = null,
        CancellationToken ct = default);

    /// <summary>
    /// Run a streaming chat turn. Yields stream events (start, delta*, done|error).
    /// Persists both messages and writes usage after the stream finishes.
    /// </summary>
    IAsyncEnumerable<ChatStreamEvent> ExecuteStreamAsync(
        Guid? conversationId,
        Guid? assistantId,
        string userMessage,
        Guid? personaId = null,
        CancellationToken ct = default);
}
