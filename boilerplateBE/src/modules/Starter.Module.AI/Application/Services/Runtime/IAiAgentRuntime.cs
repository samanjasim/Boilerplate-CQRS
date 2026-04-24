namespace Starter.Module.AI.Application.Services.Runtime;

/// <summary>
/// Multi-step agentic runtime. Runs the provider loop, dispatches tool calls, emits
/// normalised step events through the sink, and enforces MaxSteps + loop-break safety.
/// Per-provider implementations share AgentRuntimeBase today; the seam exists so later
/// work can diverge per-provider without breaking callers.
/// </summary>
internal interface IAiAgentRuntime
{
    Task<AgentRunResult> RunAsync(
        AgentRunContext context,
        IAgentRunSink sink,
        CancellationToken ct = default);
}
