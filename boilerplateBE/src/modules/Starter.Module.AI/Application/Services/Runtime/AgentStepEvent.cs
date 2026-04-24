namespace Starter.Module.AI.Application.Services.Runtime;

internal sealed record AgentStepEvent(
    int StepIndex,
    AgentStepKind Kind,
    string? AssistantContent,
    IReadOnlyList<AgentToolInvocation> ToolInvocations,
    int InputTokens,
    int OutputTokens,
    string FinishReason,
    DateTimeOffset StartedAt,
    DateTimeOffset CompletedAt);

internal enum AgentStepKind
{
    Final,
    ToolCall,
    /// <summary>
    /// Reserved for future chain-of-thought steps emitted by reasoning-specialised
    /// models (e.g., OpenAI o-series, Claude Opus thinking). Not emitted in 5a.
    /// </summary>
    ThinkOnly
}

internal sealed record AgentToolInvocation(
    string CallId,
    string Name,
    string ArgumentsJson,
    string ResultJson,
    bool IsError,
    DateTimeOffset StartedAt,
    DateTimeOffset CompletedAt);
