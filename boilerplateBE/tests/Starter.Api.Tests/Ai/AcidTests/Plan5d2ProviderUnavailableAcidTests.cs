using FluentAssertions;
using Moq;
using Starter.Abstractions.Ai;
using Starter.Module.AI.Application.Services.Moderation;
using Starter.Module.AI.Application.Services.Runtime;
using Starter.Module.AI.Domain.Enums;
using Xunit;

namespace Starter.Api.Tests.Ai.AcidTests;

/// <summary>
/// Plan 5d-2 acid M6 — moderation provider unavailable: FailOpen vs FailClosed.
///
/// Contract: when the moderator returns
/// <see cref="ModerationVerdict.Unavailable"/> at the input scan, the decorator
/// branches on the resolved profile's failure mode:
///
///   * <c>FailClosed</c> (ChildSafe default) → terminate the run with
///     <see cref="AgentRunStatus.ModerationProviderUnavailable"/>, surface a
///     refusal text, and crucially DO NOT call the inner runtime. This is the
///     load-bearing safety property — when our safety pipeline can't certify
///     output as safe, a child-facing assistant must not produce any output.
///   * <c>FailOpen</c> (Standard default) → log a warning and ALLOW the run to
///     proceed; the inner runtime IS called and the run completes normally. We
///     accept the moderation gap rather than break Standard chat on a transient
///     OpenAI Moderation API outage.
///
/// Both branches are exercised here against the same <c>UnavailableModerator</c>
/// fake and the same <c>RecordingRuntime</c>; the only differences are the
/// preset/failure-mode pair and the expected <c>Called</c> flag.
/// </summary>
public sealed class Plan5d2ProviderUnavailableAcidTests
{
    [Fact]
    public async Task ChildSafe_FailClosed_Returns_ProviderUnavailable()
    {
        // Arrange — ChildSafe + FailClosed; moderator reports unavailability.
        var tenant = Guid.NewGuid();
        var (db, assistant) = Plan5d2TestRuntimeBuilder.SeedAssistant(tenant, SafetyPreset.ChildSafe);

        var inner = new RecordingRuntime();
        var moderator = new UnavailableModerator();
        var rt = Plan5d2TestRuntimeBuilder.Wire(
            db, inner, moderator,
            preset: SafetyPreset.ChildSafe,
            failureMode: ModerationFailureMode.FailClosed);

        // Act
        var result = await rt.RunAsync(
            Plan5d2TestRuntimeBuilder.Ctx(assistant, SafetyPreset.ChildSafe),
            Mock.Of<IAgentRunSink>(), default);

        // Assert — FailClosed terminates the run with ModerationProviderUnavailable
        // BEFORE delegating to the inner runtime. The user-visible content carries
        // the localized "moderation unavailable" refusal so the chat layer can
        // surface a 503-equivalent without polluting the conversation history.
        result.Status.Should().Be(AgentRunStatus.ModerationProviderUnavailable);
        result.FinalContent.Should().Contain("unavailable");
        inner.Called.Should().BeFalse(
            "FailClosed must short-circuit before the inner runtime — no LLM tokens spent " +
            "on a turn the safety pipeline cannot certify");

        // No tokens consumed when the moderator can't reach its provider.
        result.TotalInputTokens.Should().Be(0);
        result.TotalOutputTokens.Should().Be(0);
    }

    [Fact]
    public async Task Standard_FailOpen_Allows_With_Warning_Log()
    {
        // Arrange — Standard + FailOpen; same unavailable moderator.
        var tenant = Guid.NewGuid();
        var (db, assistant) = Plan5d2TestRuntimeBuilder.SeedAssistant(tenant, SafetyPreset.Standard);

        var inner = new RecordingRuntime();
        var moderator = new UnavailableModerator();
        var rt = Plan5d2TestRuntimeBuilder.Wire(
            db, inner, moderator,
            preset: SafetyPreset.Standard,
            failureMode: ModerationFailureMode.FailOpen);

        // Act
        var result = await rt.RunAsync(
            Plan5d2TestRuntimeBuilder.Ctx(assistant, SafetyPreset.Standard),
            Mock.Of<IAgentRunSink>(), default);

        // Assert — FailOpen on Standard logs a warning and allows the run. The
        // inner runtime IS called; the post-run output scan also reports
        // unavailable (FailOpen again), so the run completes with the inner
        // runtime's content untouched.
        result.Status.Should().Be(AgentRunStatus.Completed);
        inner.Called.Should().BeTrue(
            "FailOpen must let the run proceed past an unavailable moderator " +
            "rather than break Standard chat on a transient outage");
        result.FinalContent.Should().Be("inner-runtime-output");

        // No moderation events surfaced — Unavailable is neither Blocked nor Redacted.
        (result.ModerationEvents is null || result.ModerationEvents.Count == 0)
            .Should().BeTrue("Unavailable verdicts under FailOpen produce no surfaced events");
    }

    /// <summary>
    /// Inner runtime that records whether it was invoked. The flagship
    /// FailClosed assertion requires <see cref="Called"/> to remain
    /// <c>false</c>; the FailOpen assertion requires it to flip to <c>true</c>.
    /// </summary>
    private sealed class RecordingRuntime : IAiAgentRuntime
    {
        public bool Called { get; private set; }

        public Task<AgentRunResult> RunAsync(AgentRunContext ctx, IAgentRunSink sink, CancellationToken ct = default)
        {
            Called = true;
            return Task.FromResult(new AgentRunResult(
                Status: AgentRunStatus.Completed,
                FinalContent: "inner-runtime-output",
                Steps: Array.Empty<AgentStepEvent>(),
                TotalInputTokens: 3,
                TotalOutputTokens: 4,
                TerminationReason: null));
        }
    }

    /// <summary>
    /// Moderator that always reports its provider is unreachable. Lets the
    /// decorator's <c>HandleUnavailable</c> branch run on every stage call.
    /// </summary>
    private sealed class UnavailableModerator : IContentModerator
    {
        public Task<ModerationVerdict> ScanAsync(
            string text, ModerationStage stage, ResolvedSafetyProfile profile, string? language, CancellationToken ct) =>
            Task.FromResult(ModerationVerdict.Unavailable(0));
    }
}
