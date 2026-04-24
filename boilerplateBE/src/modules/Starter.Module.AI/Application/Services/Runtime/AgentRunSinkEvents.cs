using Starter.Module.AI.Infrastructure.Providers;

namespace Starter.Module.AI.Application.Services.Runtime;

/// <summary>
/// Sink event payloads emitted by the agent runtime. StepIndex is forwarded on every
/// event for sink implementations that persist step-indexed records (e.g., the
/// TaskAgentRunSink in Plan 8c will serialise steps into AiAgentTask.Steps).
/// ChatAgentRunSink currently ignores StepIndex because messages are ordered by the
/// sink's own _order counter, not by step number.
/// </summary>

internal sealed record AgentAssistantMessage(
    int StepIndex,
    string? Content,
    IReadOnlyList<AiToolCall> ToolCalls,
    int InputTokens,
    int OutputTokens);

internal sealed record AgentToolCallEvent(
    int StepIndex,
    AiToolCall Call);

internal sealed record AgentToolResultEvent(
    int StepIndex,
    string CallId,
    string ResultJson,
    bool IsError);
