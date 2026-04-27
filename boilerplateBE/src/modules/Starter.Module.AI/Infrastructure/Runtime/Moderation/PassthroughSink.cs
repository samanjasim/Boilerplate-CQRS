using Starter.Module.AI.Application.Services.Runtime;

namespace Starter.Module.AI.Infrastructure.Runtime.Moderation;

/// <summary>
/// Forwards every event to the inner sink immediately. Used for Standard preset where
/// the moderator's final-pass scan happens after the run completes, but deltas stream live.
/// </summary>
internal sealed class PassthroughSink(IAgentRunSink inner) : IAgentRunSink
{
    public Task OnStepStartedAsync(int stepIndex, CancellationToken ct) => inner.OnStepStartedAsync(stepIndex, ct);
    public Task OnAssistantMessageAsync(AgentAssistantMessage message, CancellationToken ct) => inner.OnAssistantMessageAsync(message, ct);
    public Task OnToolCallAsync(AgentToolCallEvent call, CancellationToken ct) => inner.OnToolCallAsync(call, ct);
    public Task OnToolResultAsync(AgentToolResultEvent result, CancellationToken ct) => inner.OnToolResultAsync(result, ct);
    public Task OnDeltaAsync(string contentDelta, CancellationToken ct) => inner.OnDeltaAsync(contentDelta, ct);
    public Task OnStepCompletedAsync(AgentStepEvent step, CancellationToken ct) => inner.OnStepCompletedAsync(step, ct);
    public Task OnRunCompletedAsync(AgentRunResult result, CancellationToken ct) => inner.OnRunCompletedAsync(result, ct);
}
