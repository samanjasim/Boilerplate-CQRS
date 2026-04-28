using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Starter.Module.AI.Infrastructure.Observability;

/// <summary>
/// Central OpenTelemetry meter, instruments, and activity source for the agent runtime.
/// One <see cref="Meter"/> and one <see cref="ActivitySource"/> are shared across all
/// agent runtime invocations so they share a single registration and export path.
/// </summary>
internal static class AiAgentMetrics
{
    public const string MeterName = "Starter.Ai.Agent";
    public const string ActivitySourceName = "Starter.Ai.Agent";

    private static readonly Meter _meter = new(MeterName, "1.0.0");

    public static readonly Histogram<int> StepCount = _meter.CreateHistogram<int>(
        name: "ai_agent_steps",
        unit: "steps",
        description: "Number of steps in a completed agent run.");

    public static readonly Counter<int> LoopBreaks = _meter.CreateCounter<int>(
        name: "ai_agent_loop_breaks",
        unit: "runs",
        description: "Runs terminated because LoopBreakDetector detected a repeated tool call.");

    public static readonly Counter<int> MaxStepsExceeded = _meter.CreateCounter<int>(
        name: "ai_agent_max_steps_exceeded",
        unit: "runs",
        description: "Runs terminated because MaxSteps was reached.");

    public static readonly Counter<long> RunsByPersona = _meter.CreateCounter<long>(
        name: "ai_agent_runs_by_persona_total",
        unit: "{run}",
        description: "Agent runs partitioned by persona (slug, audience, safety).");

    public static readonly Counter<long> ModerationOutcomes = _meter.CreateCounter<long>(
        name: "ai_moderation_outcomes_total",
        description: "Count of moderation scan outcomes by stage / preset / outcome.");

    public static readonly Histogram<double> ModerationLatency = _meter.CreateHistogram<double>(
        name: "ai_moderation_latency_ms",
        unit: "ms",
        description: "Moderation scan latency by stage / provider.");

    public static readonly Counter<long> ModerationProviderUnavailable = _meter.CreateCounter<long>(
        name: "ai_moderation_provider_unavailable_total",
        description: "Times the moderator returned Unavailable, by failure mode.");

    public static readonly Counter<long> PendingApprovals = _meter.CreateCounter<long>(
        name: "ai_pending_approvals_total",
        description: "Approval lifecycle events: created/approved/denied/expired.");

    public static readonly Counter<long> DangerousActionBlocks = _meter.CreateCounter<long>(
        name: "ai_dangerous_action_blocks_total",
        description: "Times AgentToolDispatcher created a pending approval row, by tool_name.");

    public static readonly ActivitySource Source = new(ActivitySourceName, "1.0.0");
}
