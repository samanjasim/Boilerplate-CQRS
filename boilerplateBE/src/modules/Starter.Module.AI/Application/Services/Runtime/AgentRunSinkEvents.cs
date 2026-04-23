using Starter.Module.AI.Infrastructure.Providers;

namespace Starter.Module.AI.Application.Services.Runtime;

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
