using FluentAssertions;
using Starter.Abstractions.Ai;
using Starter.Module.AI.Application.Services.Moderation;
using Starter.Module.AI.Domain.Enums;
using Starter.Module.AI.Infrastructure.Services.Moderation;
using Xunit;

namespace Starter.Api.Tests.Ai.Moderation;

public sealed class OpenAiContentModeratorTests
{
    private static ResolvedSafetyProfile ChildSafe() => new(
        SafetyPreset.ChildSafe, ModerationProvider.OpenAi,
        new Dictionary<string, double> { ["sexual"] = 0.5, ["violence"] = 0.5 },
        new[] { "sexual/minors" },
        ModerationFailureMode.FailClosed,
        RedactPii: false);

    [Fact]
    public void EvaluateScores_Blocks_On_Always_Block_Category()
    {
        // 0.5 is the always-block threshold (matches OpenAI's own flagging convention).
        // Below 0.5, the category is treated as not flagged even when in the always-block list.
        var scores = new Dictionary<string, double> { ["sexual/minors"] = 0.6, ["sexual"] = 0.1 };
        var v = OpenAiContentModerator.EvaluateScores(scores, ChildSafe(), 10);
        v.Outcome.Should().Be(ModerationOutcome.Blocked);
        v.BlockedReason.Should().Contain("sexual/minors");
    }

    [Fact]
    public void EvaluateScores_Allows_When_AlwaysBlock_Category_Below_Threshold()
    {
        // OpenAI Moderation API returns small non-zero scores (~1e-4) for all categories on every call.
        // Always-block must use a sane threshold (0.5) to avoid blocking every message. Live-tested 2026-04-27.
        var scores = new Dictionary<string, double> { ["sexual/minors"] = 0.0001, ["sexual"] = 0.0001 };
        var v = OpenAiContentModerator.EvaluateScores(scores, ChildSafe(), 10);
        v.Outcome.Should().Be(ModerationOutcome.Allowed);
    }

    [Fact]
    public void EvaluateScores_Blocks_When_Threshold_Met()
    {
        var scores = new Dictionary<string, double> { ["sexual"] = 0.55, ["violence"] = 0.0 };
        var v = OpenAiContentModerator.EvaluateScores(scores, ChildSafe(), 10);
        v.Outcome.Should().Be(ModerationOutcome.Blocked);
        v.BlockedReason.Should().Contain("sexual");
    }

    [Fact]
    public void EvaluateScores_Allows_When_All_Below_Threshold()
    {
        var scores = new Dictionary<string, double> { ["sexual"] = 0.1, ["violence"] = 0.0 };
        var v = OpenAiContentModerator.EvaluateScores(scores, ChildSafe(), 10);
        v.Outcome.Should().Be(ModerationOutcome.Allowed);
    }
}
