using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Starter.Abstractions.Ai;
using Starter.Module.AI.Application.Services.Moderation;
using Starter.Module.AI.Application.Services.Runtime;
using Starter.Module.AI.Domain.Enums;
using Starter.Module.AI.Infrastructure.Services.Moderation;
using Xunit;

namespace Starter.Api.Tests.Ai.AcidTests;

/// <summary>
/// Plan 5d-2 acid M3 — ProfessionalModerated redacts PII (social flagship).
///
/// Contract: when an assistant runs under the ProfessionalModerated preset, the
/// safety profile resolves with <c>RedactPii=true</c>. The moderator allows the
/// content (no policy violation), but the real <see cref="RegexPiiRedactor"/>
/// detects PII (email + E.164 phone) and replaces each match with <c>[REDACTED]</c>.
/// The decorator emits exactly one <see cref="Domain.Entities.AiModerationEvent"/>
/// with <c>Outcome=Redacted</c> for the chat layer to persist.
///
/// This test deliberately uses the REAL <see cref="RegexPiiRedactor"/> (not a fake)
/// to prove the C3 redactor + D2 decorator wire together correctly end-to-end.
/// </summary>
public sealed class Plan5d2ProfessionalRedactionAcidTests
{
    [Fact]
    public async Task ProfessionalModerated_Redacts_PII_In_Output()
    {
        // Arrange — ProfessionalModerated assistant; moderator allows everything;
        // inner runtime returns text containing both an email and a phone number.
        var tenant = Guid.NewGuid();
        var (db, assistant) = Plan5d2TestRuntimeBuilder.SeedAssistant(
            tenant, SafetyPreset.ProfessionalModerated);

        const string contentWithPii = "Email me at john@example.com or call +14155552671";
        var inner = new StubRuntime(new AgentRunResult(
            Status: AgentRunStatus.Completed,
            FinalContent: contentWithPii,
            Steps: Array.Empty<AgentStepEvent>(),
            TotalInputTokens: 5,
            TotalOutputTokens: 5,
            TerminationReason: null));
        var moderator = new AlwaysAllowModerator();
        var redactor = new RegexPiiRedactor(NullLogger<RegexPiiRedactor>.Instance);

        var rt = Plan5d2TestRuntimeBuilder.Wire(
            db, inner, moderator,
            preset: SafetyPreset.ProfessionalModerated,
            failureMode: ModerationFailureMode.FailClosed,
            redactPii: true,
            redactor: redactor);

        // Act
        var result = await rt.RunAsync(
            Plan5d2TestRuntimeBuilder.Ctx(assistant, SafetyPreset.ProfessionalModerated),
            Mock.Of<IAgentRunSink>(),
            default);

        // Assert — content is fully redacted; the run still completes successfully.
        result.Status.Should().Be(AgentRunStatus.Completed);
        result.FinalContent.Should().NotBeNull();
        result.FinalContent.Should().NotContain("john@example.com");
        result.FinalContent.Should().NotContain("+14155552671");
        result.FinalContent.Should().Contain("[REDACTED]");

        // Single moderation event with Outcome=Redacted, Stage=Output, Preset=ProfessionalModerated.
        result.ModerationEvents.Should().NotBeNull().And.HaveCount(1);
        var ev = result.ModerationEvents![0];
        ev.Stage.Should().Be(ModerationStage.Output);
        ev.Outcome.Should().Be(ModerationOutcome.Redacted);
        ev.Preset.Should().Be(SafetyPreset.ProfessionalModerated);
    }

    /// <summary>
    /// Inner runtime that streams the result content into the supplied sink (so the
    /// decorator's <c>BufferingSink</c> captures it) before returning the canned
    /// <see cref="AgentRunResult"/>. The decorator reads from the buffer rather than
    /// the result's <c>FinalContent</c> when a buffering sink is in play.
    /// </summary>
    private sealed class StubRuntime(AgentRunResult result) : IAiAgentRuntime
    {
        public async Task<AgentRunResult> RunAsync(AgentRunContext ctx, IAgentRunSink sink, CancellationToken ct = default)
        {
            if (!string.IsNullOrEmpty(result.FinalContent))
                await sink.OnDeltaAsync(result.FinalContent, ct);
            return result;
        }
    }

    /// <summary>Moderator that allows every text at every stage.</summary>
    private sealed class AlwaysAllowModerator : IContentModerator
    {
        public Task<ModerationVerdict> ScanAsync(
            string text, ModerationStage stage, ResolvedSafetyProfile profile, string? language, CancellationToken ct) =>
            Task.FromResult(ModerationVerdict.Allowed(2));
    }
}
