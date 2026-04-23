namespace Starter.Module.AI.Application.Services.Runtime;

internal sealed record AgentRunResult(
    AgentRunStatus Status,
    string? FinalContent,
    IReadOnlyList<AgentStepEvent> Steps,
    int TotalInputTokens,
    int TotalOutputTokens,
    string? TerminationReason);

internal enum AgentRunStatus
{
    Completed,
    MaxStepsExceeded,
    LoopBreak,
    ProviderError,
    Cancelled
}
