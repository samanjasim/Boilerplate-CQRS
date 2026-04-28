using FluentAssertions;
using Starter.Abstractions.Ai;
using Starter.Module.AI.Domain.Entities;
using Starter.Module.AI.Domain.Enums;
using Xunit;

namespace Starter.Api.Tests.Ai.Moderation;

public sealed class AiModerationEventTests
{
    [Fact]
    public void Create_Defaults_And_Stamps_Values()
    {
        var ev = AiModerationEvent.Create(
            tenantId: Guid.NewGuid(),
            assistantId: Guid.NewGuid(),
            agentPrincipalId: null,
            conversationId: Guid.NewGuid(),
            agentTaskId: null,
            messageId: null,
            stage: ModerationStage.Output,
            preset: SafetyPreset.ChildSafe,
            outcome: ModerationOutcome.Blocked,
            categoriesJson: """{"sexual-minors":0.93}""",
            provider: ModerationProvider.OpenAi,
            latencyMs: 42,
            blockedReason: "moderation: sexual-minors");

        ev.Id.Should().NotBe(Guid.Empty);
        ev.Stage.Should().Be(ModerationStage.Output);
        ev.Outcome.Should().Be(ModerationOutcome.Blocked);
        ev.RedactionFailed.Should().BeFalse();
        ev.LatencyMs.Should().Be(42);
    }
}
