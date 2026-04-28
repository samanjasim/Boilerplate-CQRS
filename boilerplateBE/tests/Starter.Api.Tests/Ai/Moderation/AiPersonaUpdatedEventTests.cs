using FluentAssertions;
using Starter.Abstractions.Ai;
using Starter.Module.AI.Domain.Entities;
using Starter.Module.AI.Domain.Enums;
using Starter.Module.AI.Domain.Events;
using Xunit;

namespace Starter.Api.Tests.Ai.Moderation;

public sealed class AiPersonaUpdatedEventTests
{
    [Fact]
    public void Update_Raises_PersonaUpdatedEvent()
    {
        var p = AiPersona.Create(
            tenantId: Guid.NewGuid(),
            slug: "teacher",
            displayName: "Teacher",
            description: null,
            audienceType: PersonaAudienceType.Internal,
            safetyPreset: SafetyPreset.Standard,
            createdByUserId: Guid.NewGuid());
        p.ClearDomainEvents();

        p.Update("Teacher", null, SafetyPreset.ChildSafe, null, isActive: true);

        p.DomainEvents.Should().ContainSingle(e => e is PersonaUpdatedEvent);
    }
}
