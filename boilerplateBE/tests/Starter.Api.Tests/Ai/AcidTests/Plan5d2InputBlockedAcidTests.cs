using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Starter.Abstractions.Ai;
using Starter.Module.AI.Application.Services.Moderation;
using Starter.Module.AI.Application.Services.Runtime;
using Starter.Module.AI.Domain.Enums;
using Xunit;

namespace Starter.Api.Tests.Ai.AcidTests;

/// <summary>
/// Plan 5d-2 acid M2 — input blocked, no LLM call.
///
/// Contract: when the moderator flags the user message at the Input stage, the
/// decorator returns <see cref="AgentRunStatus.InputBlocked"/> WITHOUT delegating
/// to the inner runtime. Concretely this means:
///   * No model call → no input/output tokens consumed → no <c>AiUsageLog</c> row
///     would be persisted by the chat layer (the decorator emits zero token counts
///     on a blocked-on-input result).
///   * No cost claim against the per-tenant cap.
///   * No rate-limit slot consumed for the upstream provider.
/// The decorator surfaces a single Input-stage <c>AiModerationEvent</c> on the
/// result for the chat layer to persist. This test asserts the decorator-side
/// invariants (inner runtime not called + no usage log row exists in the AI db).
/// </summary>
public sealed class Plan5d2InputBlockedAcidTests
{
    [Fact]
    public async Task Input_Blocked_Skips_Inner_Runtime_No_Cost_Claim_No_Usage_Log()
    {
        // Arrange — ChildSafe assistant; moderator blocks input.
        var tenant = Guid.NewGuid();
        var (db, assistant) = Plan5d2TestRuntimeBuilder.SeedAssistant(tenant, SafetyPreset.ChildSafe);

        var inner = new RecordingRuntime();
        var moderator = new BlockingModerator(blockOnStage: ModerationStage.Input);
        var rt = Plan5d2TestRuntimeBuilder.Wire(
            db, inner, moderator,
            preset: SafetyPreset.ChildSafe,
            failureMode: ModerationFailureMode.FailClosed);

        // Act
        var result = await rt.RunAsync(
            Plan5d2TestRuntimeBuilder.Ctx(assistant, SafetyPreset.ChildSafe),
            Mock.Of<IAgentRunSink>(),
            default);

        // Assert — flagship invariants.
        result.Status.Should().Be(AgentRunStatus.InputBlocked);
        inner.WasCalled.Should().BeFalse(
            "blocked input must short-circuit before the inner cost-cap / runtime layer");

        // Token accounting on blocked-input turns is zero (nothing was consumed).
        result.TotalInputTokens.Should().Be(0);
        result.TotalOutputTokens.Should().Be(0);

        // No AiUsageLog row should exist — the decorator never reaches the runtime
        // factory's cost-cap layer that writes usage. The chat layer (D5) will likewise
        // skip persisting usage on a blocked-input result.
        (await db.AiUsageLogs.CountAsync()).Should().Be(0);

        // Decorator surfaces the Input-stage moderation event for the chat layer.
        result.ModerationEvents.Should().NotBeNull().And.HaveCount(1);
        var ev = result.ModerationEvents![0];
        ev.Stage.Should().Be(ModerationStage.Input);
        ev.Outcome.Should().Be(ModerationOutcome.Blocked);
        ev.Preset.Should().Be(SafetyPreset.ChildSafe);

        // Refusal text is the user-visible content the chat layer will return.
        result.FinalContent.Should().Contain("refused");
    }

    /// <summary>
    /// Inner runtime that records whether it was invoked. M2 requires this to remain
    /// <c>false</c> for the duration of the test — the moderation decorator must
    /// short-circuit before delegating.
    /// </summary>
    private sealed class RecordingRuntime : IAiAgentRuntime
    {
        public bool WasCalled { get; private set; }
        public Task<AgentRunResult> RunAsync(AgentRunContext ctx, IAgentRunSink sink, CancellationToken ct = default)
        {
            WasCalled = true;
            return Task.FromResult(new AgentRunResult(
                Status: AgentRunStatus.Completed,
                FinalContent: "should-not-appear",
                Steps: Array.Empty<AgentStepEvent>(),
                TotalInputTokens: 1,
                TotalOutputTokens: 1,
                TerminationReason: null));
        }
    }

    private sealed class BlockingModerator(ModerationStage blockOnStage) : IContentModerator
    {
        public Task<ModerationVerdict> ScanAsync(
            string text, ModerationStage stage, ResolvedSafetyProfile profile, string? language, CancellationToken ct) =>
            Task.FromResult(stage == blockOnStage
                ? ModerationVerdict.Blocked(
                    new Dictionary<string, double> { ["sexual"] = 0.92 },
                    "category-threshold:sexual",
                    5)
                : ModerationVerdict.Allowed(5));
    }
}
