namespace Starter.Module.AI.Application.Services.Runtime;

internal sealed record AgentRunResult(
    AgentRunStatus Status,
    string? FinalContent,
    IReadOnlyList<AgentStepEvent> Steps,
    long TotalInputTokens,
    long TotalOutputTokens,
    string? TerminationReason);

internal enum AgentRunStatus
{
    Completed,
    MaxStepsExceeded,
    LoopBreak,
    ProviderError,
    Cancelled,
    CostCapExceeded,
    RateLimitExceeded,
}
