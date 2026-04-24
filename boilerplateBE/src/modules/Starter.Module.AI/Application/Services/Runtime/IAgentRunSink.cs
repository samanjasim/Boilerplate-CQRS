namespace Starter.Module.AI.Application.Services.Runtime;

/// <summary>
/// Caller-owned side-effect hook. The runtime never touches the database, webhooks,
/// or stream writers directly — it emits events through this sink. Chat callers
/// implement ChatAgentRunSink; future task callers will implement TaskAgentRunSink.
/// </summary>
internal interface IAgentRunSink
{
    Task OnStepStartedAsync(int stepIndex, CancellationToken ct);
    Task OnAssistantMessageAsync(AgentAssistantMessage message, CancellationToken ct);
    Task OnToolCallAsync(AgentToolCallEvent call, CancellationToken ct);
    Task OnToolResultAsync(AgentToolResultEvent result, CancellationToken ct);
    Task OnDeltaAsync(string contentDelta, CancellationToken ct);
    Task OnStepCompletedAsync(AgentStepEvent step, CancellationToken ct);
    Task OnRunCompletedAsync(AgentRunResult result, CancellationToken ct);
}
