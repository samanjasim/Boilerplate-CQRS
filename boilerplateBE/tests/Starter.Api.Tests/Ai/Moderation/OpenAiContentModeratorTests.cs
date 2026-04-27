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
        var scores = new Dictionary<string, double> { ["sexual/minors"] = 0.4, ["sexual"] = 0.1 };
        var v = OpenAiContentModerator.EvaluateScores(scores, ChildSafe(), 10);
        v.Outcome.Should().Be(ModerationOutcome.Blocked);
        v.BlockedReason.Should().Contain("sexual/minors");
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
