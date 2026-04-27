using FluentAssertions;
using Starter.Abstractions.Ai;
using Starter.Module.AI.Application.Services.Moderation;
using Starter.Module.AI.Application.Services.Runtime;
using Starter.Module.AI.Domain.Enums;
using Xunit;

namespace Starter.Api.Tests.Ai.AcidTests;

/// <summary>
/// Plan 5d-2 acid M1 — ChildSafe output blocked (school flagship).
///
/// Contract: when a ChildSafe assistant produces output that the moderator flags
/// (always-block category like <c>sexual-minors</c>), the run terminates with
/// <see cref="AgentRunStatus.OutputBlocked"/>, the inner runtime's pre-block deltas
/// are suppressed by <c>BufferingSink</c> (so the user never sees the unsafe text),
/// and exactly one <see cref="Domain.Entities.AiModerationEvent"/> is attached to the
/// result with <c>Stage=Output</c>, <c>Outcome=Blocked</c>, <c>Preset=ChildSafe</c>.
/// The chat layer persists the event in its own atomic write — the decorator's job
/// is just to surface it.
/// </summary>
public sealed class Plan5d2ChildSafeOutputBlockedAcidTests
{
    [Fact]
    public async Task ChildSafe_Output_Blocked_Returns_Refusal_And_Persists_Event()
    {
        // Arrange — ChildSafe assistant; inner runtime emits "innocuous-text" which the
        // moderator will (post-buffering) flag at the Output stage.
        var tenant = Guid.NewGuid();
        var (db, assistant) = Plan5d2TestRuntimeBuilder.SeedAssistant(tenant, SafetyPreset.ChildSafe);

        var inner = new StubRuntime(new AgentRunResult(
            Status: AgentRunStatus.Completed,
            FinalContent: "innocuous-text",
            Steps: Array.Empty<AgentStepEvent>(),
            TotalInputTokens: 5,
            TotalOutputTokens: 5,
            TerminationReason: null));
        var moderator = new BlockingModerator(blockOnStage: ModerationStage.Output);
        var rt = Plan5d2TestRuntimeBuilder.Wire(
            db, inner, moderator,
            preset: SafetyPreset.ChildSafe,
            failureMode: ModerationFailureMode.FailClosed);

        // Act
        var sink = new RecordingSink();
        var result = await rt.RunAsync(
            Plan5d2TestRuntimeBuilder.Ctx(assistant, SafetyPreset.ChildSafe),
            sink, default);

        // Assert
        result.Status.Should().Be(AgentRunStatus.OutputBlocked);
        result.FinalContent.Should().NotBeNull();
        result.FinalContent.Should().NotContain("innocuous-text");
        result.FinalContent.Should().Contain("refused");

        result.ModerationEvents.Should().NotBeNull().And.HaveCount(1);
        var ev = result.ModerationEvents![0];
        ev.Stage.Should().Be(ModerationStage.Output);
        ev.Outcome.Should().Be(ModerationOutcome.Blocked);
        ev.Preset.Should().Be(SafetyPreset.ChildSafe);

        // BufferingSink swallowed the inner runtime's pre-block delta — the user-facing
        // sink must NOT have seen the unsafe text. This is the load-bearing assertion
        // for M1: a ChildSafe student must never have an unsafe token leak through.
        sink.DeltaCount.Should().Be(0);
        sink.AssistantMessageCount.Should().Be(0);
    }

    /// <summary>Inner runtime that streams a delta then returns the canned result.</summary>
    private sealed class StubRuntime(AgentRunResult result) : IAiAgentRuntime
    {
        public async Task<AgentRunResult> RunAsync(AgentRunContext ctx, IAgentRunSink sink, CancellationToken ct = default)
        {
            // Simulate the LLM streaming a chunk that *would* be visible to the user
            // if the BufferingSink wasn't holding it back.
            await sink.OnDeltaAsync("innocuous-text", ct);
            return result;
        }
    }

    /// <summary>Moderator that blocks at one specific stage and allows the other.</summary>
    private sealed class BlockingModerator(ModerationStage blockOnStage) : IContentModerator
    {
        public Task<ModerationVerdict> ScanAsync(
            string text, ModerationStage stage, ResolvedSafetyProfile profile, string? language, CancellationToken ct) =>
            Task.FromResult(stage == blockOnStage
                ? ModerationVerdict.Blocked(
                    new Dictionary<string, double> { ["sexual-minors"] = 0.93 },
                    "always-block:sexual-minors",
                    5)
                : ModerationVerdict.Allowed(5));
    }

    /// <summary>Sink that counts deltas/messages so the test can prove BufferingSink suppression.</summary>
    private sealed class RecordingSink : IAgentRunSink
    {
        public int DeltaCount { get; private set; }
        public int AssistantMessageCount { get; private set; }
        public Task OnStepStartedAsync(int stepIndex, CancellationToken ct) => Task.CompletedTask;
        public Task OnAssistantMessageAsync(AgentAssistantMessage message, CancellationToken ct)
        {
            AssistantMessageCount++;
            return Task.CompletedTask;
        }
        public Task OnToolCallAsync(AgentToolCallEvent call, CancellationToken ct) => Task.CompletedTask;
        public Task OnToolResultAsync(AgentToolResultEvent result, CancellationToken ct) => Task.CompletedTask;
        public Task OnDeltaAsync(string contentDelta, CancellationToken ct)
        {
            DeltaCount++;
            return Task.CompletedTask;
        }
        public Task OnStepCompletedAsync(AgentStepEvent step, CancellationToken ct) => Task.CompletedTask;
        public Task OnRunCompletedAsync(AgentRunResult result, CancellationToken ct) => Task.CompletedTask;
    }
}
