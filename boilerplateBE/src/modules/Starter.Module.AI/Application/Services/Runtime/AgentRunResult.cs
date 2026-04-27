namespace Starter.Module.AI.Application.Services.Runtime;

internal sealed record AgentRunResult(
    AgentRunStatus Status,
    string? FinalContent,
    IReadOnlyList<AgentStepEvent> Steps,
    long TotalInputTokens,
    long TotalOutputTokens,
    string? TerminationReason,
    // Placeholder until Task B2 introduces Domain.Entities.AiModerationEvent.
    // Tightened to IReadOnlyList<Domain.Entities.AiModerationEvent>? once the entity lands.
    IReadOnlyList<object>? ModerationEvents = null);

internal enum AgentRunStatus
{
    Completed,
    MaxStepsExceeded,
    LoopBreak,
    ProviderError,
    Cancelled,
    CostCapExceeded,
    RateLimitExceeded,
    InputBlocked = 7,
    OutputBlocked = 8,
    AwaitingApproval = 9,
    ModerationProviderUnavailable = 10,
}
