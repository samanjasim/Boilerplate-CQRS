namespace Starter.Module.AI.Application.Services.Retrieval;

/// <summary>
/// Rewrites the latest user message into a self-contained query using recent
/// conversation history. Never throws — falls back to <paramref name="latestUserMessage"/>
/// on any failure. Implementations are responsible for respecting per-stage
/// timeouts and recording their own cache metrics; stage duration/outcome is
/// recorded by the RagRetrievalService wrapper around the call.
/// </summary>
public interface IContextualQueryResolver
{
    /// <summary>
    /// Returns the resolved query string. When <paramref name="history"/> is empty,
    /// when the feature flag is off, or when the heuristic decides the message is
    /// already self-contained, returns <paramref name="latestUserMessage"/> unchanged
    /// without calling the LLM.
    /// </summary>
    Task<string> ResolveAsync(
        Guid tenantId,
        string latestUserMessage,
        IReadOnlyList<RagHistoryMessage> history,
        string? language,
        CancellationToken ct);
}
