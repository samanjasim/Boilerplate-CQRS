using FluentAssertions;
using Starter.Abstractions.Ai;
using Starter.Module.AI.Application.Services.Moderation;
using Starter.Module.AI.Application.Services.Runtime;
using Starter.Module.AI.Domain.Enums;
using Xunit;

namespace Starter.Api.Tests.Ai.AcidTests;

/// <summary>
/// Plan 5d-2 acid M5 — streaming buffering (preset-conditional sink wrapper).
///
/// Contract: the moderation decorator wraps the user-facing sink with a
/// <c>BufferingSink</c> on safe presets (ChildSafe / ProfessionalModerated)
/// and a <c>PassthroughSink</c> on Standard. Both tests use the same chunking
/// inner runtime — the only difference is the preset, which exercises both
/// branches of the wrap site in <c>ContentModerationEnforcingAgentRuntime</c>.
///
///   * <see cref="ChildSafe_Streaming_Suppresses_Deltas_Until_Moderation_Passes"/>:
///     the user-facing sink must not see ANY delta until after the output
///     moderation scan finishes. This is the load-bearing safety property —
///     a child must never glimpse unsafe text before the moderator clears it.
///   * <see cref="Standard_Streaming_Passes_Deltas_Live"/>: the user-facing
///     sink must see deltas while the inner runtime is still streaming, well
///     before the post-run output scan completes. This is the load-bearing
///     UX property — Standard chat preserves token-by-token streaming latency.
///
/// Timing is asserted via timestamps captured by the sink + a
/// <c>TaskCompletionSource&lt;DateTime&gt;</c> the moderator signals when its
/// Output-stage scan returns. We avoid race-prone fixed-delay sleeps in the
/// assertion path by comparing recorded timestamps.
/// </summary>
public sealed class Plan5d2StreamingBufferingAcidTests
{
    [Fact]
    public async Task ChildSafe_Streaming_Suppresses_Deltas_Until_Moderation_Passes()
    {
        // Arrange — ChildSafe + FailClosed; chunking runtime emits two deltas with
        // a small inter-chunk delay so any live forwarding would be observable.
        var tenant = Guid.NewGuid();
        var (db, assistant) = Plan5d2TestRuntimeBuilder.SeedAssistant(tenant, SafetyPreset.ChildSafe);

        var sink = new TimeRecordingSink();
        var inner = new ChunkingRuntime("first chunk ", "second chunk");
        var moderator = new TimingModerator();
        var rt = Plan5d2TestRuntimeBuilder.Wire(
            db, inner, moderator,
            preset: SafetyPreset.ChildSafe,
            failureMode: ModerationFailureMode.FailClosed);

        // Act
        var result = await rt.RunAsync(
            Plan5d2TestRuntimeBuilder.Ctx(assistant, SafetyPreset.ChildSafe, streaming: true),
            sink, default);

        // Assert — moderation completed (output stage signalled) and deltas only
        // landed on the user-facing sink AFTER moderation cleared the buffered text.
        result.Status.Should().Be(AgentRunStatus.Completed);
        result.FinalContent.Should().Be("first chunk second chunk");

        moderator.OutputScanCompletedAt.Task.IsCompleted.Should().BeTrue(
            "the output scan must have run for ChildSafe");
        var modCompleted = await moderator.OutputScanCompletedAt.Task;

        sink.DeltaTimestamps.Should().NotBeEmpty(
            "BufferingSink.ReleaseAsync must flush the buffered content as a single forwarded delta");
        var firstDeltaTime = sink.DeltaTimestamps[0];
        firstDeltaTime.Should().BeOnOrAfter(modCompleted,
            "ChildSafe must not stream a single delta until moderation passes");
    }

    [Fact]
    public async Task Standard_Streaming_Passes_Deltas_Live()
    {
        // Arrange — Standard preset; same inner runtime + moderator. PassthroughSink
        // forwards every delta to the user-facing sink as the inner streams.
        var tenant = Guid.NewGuid();
        var (db, assistant) = Plan5d2TestRuntimeBuilder.SeedAssistant(tenant, SafetyPreset.Standard);

        var sink = new TimeRecordingSink();
        var inner = new ChunkingRuntime("first chunk ", "second chunk");
        var moderator = new TimingModerator();
        var rt = Plan5d2TestRuntimeBuilder.Wire(
            db, inner, moderator,
            preset: SafetyPreset.Standard,
            failureMode: ModerationFailureMode.FailOpen);

        // Act
        var result = await rt.RunAsync(
            Plan5d2TestRuntimeBuilder.Ctx(assistant, SafetyPreset.Standard, streaming: true),
            sink, default);

        // Assert — at least one delta arrived BEFORE the post-run output scan
        // completed. PassthroughSink does not buffer, so the user sees tokens live.
        result.Status.Should().Be(AgentRunStatus.Completed);

        sink.DeltaTimestamps.Should().HaveCountGreaterOrEqualTo(2,
            "PassthroughSink forwards each chunk straight to the user-facing sink");

        moderator.OutputScanCompletedAt.Task.IsCompleted.Should().BeTrue(
            "the output scan must still have run on Standard (post-run, no buffering)");
        var modCompleted = await moderator.OutputScanCompletedAt.Task;

        var firstDeltaTime = sink.DeltaTimestamps[0];
        firstDeltaTime.Should().BeBefore(modCompleted,
            "Standard must stream the first delta live, before the post-run output scan finishes");
    }

    /// <summary>
    /// User-facing sink that timestamps every delta arrival. Used to prove
    /// BufferingSink suppression vs PassthroughSink live-forwarding.
    /// </summary>
    private sealed class TimeRecordingSink : IAgentRunSink
    {
        public List<DateTime> DeltaTimestamps { get; } = new();

        public Task OnStepStartedAsync(int stepIndex, CancellationToken ct) => Task.CompletedTask;
        public Task OnAssistantMessageAsync(AgentAssistantMessage message, CancellationToken ct) => Task.CompletedTask;
        public Task OnToolCallAsync(AgentToolCallEvent call, CancellationToken ct) => Task.CompletedTask;
        public Task OnToolResultAsync(AgentToolResultEvent result, CancellationToken ct) => Task.CompletedTask;
        public Task OnDeltaAsync(string contentDelta, CancellationToken ct)
        {
            DeltaTimestamps.Add(DateTime.UtcNow);
            return Task.CompletedTask;
        }
        public Task OnStepCompletedAsync(AgentStepEvent step, CancellationToken ct) => Task.CompletedTask;
        public Task OnRunCompletedAsync(AgentRunResult result, CancellationToken ct) => Task.CompletedTask;
    }

    /// <summary>
    /// Inner runtime that emits a sequence of chunks with a small inter-chunk delay
    /// so that any live forwarding (Standard) is observable as deltas arriving
    /// before the post-run moderation scan signals completion.
    /// </summary>
    private sealed class ChunkingRuntime(params string[] chunks) : IAiAgentRuntime
    {
        public async Task<AgentRunResult> RunAsync(AgentRunContext ctx, IAgentRunSink sink, CancellationToken ct = default)
        {
            foreach (var chunk in chunks)
            {
                await sink.OnDeltaAsync(chunk, ct);
                // Real provider streams have inter-chunk gaps; mimic one so the
                // first-delta timestamp on Standard is comfortably earlier than
                // the post-run output scan timestamp.
                await Task.Delay(15, ct);
            }
            return new AgentRunResult(
                Status: AgentRunStatus.Completed,
                FinalContent: string.Concat(chunks),
                Steps: Array.Empty<AgentStepEvent>(),
                TotalInputTokens: 5,
                TotalOutputTokens: 10,
                TerminationReason: null);
        }
    }

    /// <summary>
    /// Allow-all moderator that signals (via a <see cref="TaskCompletionSource{TResult}"/>)
    /// the wall-clock instant the Output-stage scan completes. Tests compare this
    /// against the user-facing sink's first-delta timestamp to prove the
    /// BufferingSink / PassthroughSink contract.
    /// </summary>
    private sealed class TimingModerator : IContentModerator
    {
        public TaskCompletionSource<DateTime> OutputScanCompletedAt { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task<ModerationVerdict> ScanAsync(
            string text, ModerationStage stage, ResolvedSafetyProfile profile, string? language, CancellationToken ct)
        {
            if (stage == ModerationStage.Output)
                OutputScanCompletedAt.TrySetResult(DateTime.UtcNow);
            return Task.FromResult(ModerationVerdict.Allowed(2));
        }
    }
}
