namespace Starter.Module.AI.Application.Services.Retrieval;

/// <summary>
/// Conversation turn slice passed from <c>ChatExecutionService</c> into
/// <c>IRagRetrievalService</c>. Role is <c>"user"</c> or <c>"assistant"</c>.
/// Tool-call / tool-result / system rows are filtered out by the caller so the
/// resolver never has to decide how to render them.
/// </summary>
public sealed record RagHistoryMessage(string Role, string Content);
