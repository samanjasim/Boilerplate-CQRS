using FluentAssertions;
using Starter.Abstractions.Ai;
using Starter.Module.AI.Domain.Entities;
using Starter.Module.AI.Domain.Events;
using Xunit;

namespace Starter.Api.Tests.Ai.Moderation;

public sealed class AiAssistantSafetyOverrideTests
{
    private static AiAssistant Make() =>
        AiAssistant.Create(
            tenantId: Guid.NewGuid(),
            name: "Tutor",
            description: null,
            systemPrompt: "you are a tutor",
            createdByUserId: Guid.NewGuid());

    [Fact]
    public void SetSafetyPreset_Persists_Value_And_Raises_Updated()
    {
        var a = Make();
        a.ClearDomainEvents();

        a.SetSafetyPreset(SafetyPreset.ChildSafe);

        a.SafetyPresetOverride.Should().Be(SafetyPreset.ChildSafe);
        a.DomainEvents.Should().ContainSingle(e => e is AssistantUpdatedEvent);
    }

    [Fact]
    public void SetSafetyPreset_Null_Clears_Override()
    {
        var a = Make();
        a.SetSafetyPreset(SafetyPreset.ChildSafe);
        a.SetSafetyPreset(null);

        a.SafetyPresetOverride.Should().BeNull();
    }
}
