using FluentAssertions;
using Starter.Module.AI.Domain.Entities;
using Starter.Module.AI.Domain.Events;
using Xunit;

namespace Starter.Api.Tests.Ai.Identity;

public sealed class AssistantUpdatedEventTests
{
    [Fact]
    public void SetBudget_Raises_AssistantUpdatedEvent()
    {
        var tenantId = Guid.NewGuid();
        var assistant = AiAssistant.Create(
            tenantId: tenantId,
            name: "Tutor",
            description: null,
            systemPrompt: "Be helpful.",
            createdByUserId: Guid.NewGuid());

        // creation may emit other events; clear them so we observe only the SetBudget event
        assistant.GetDomainEventsAndClear();

        assistant.SetBudget(monthlyUsd: 50m, dailyUsd: 5m, requestsPerMinute: 30);

        assistant.DomainEvents.Should()
            .ContainSingle(e => e is AssistantUpdatedEvent)
            .Which.Should().BeOfType<AssistantUpdatedEvent>()
            .Which.Should().Match<AssistantUpdatedEvent>(e =>
                e.TenantId == tenantId && e.AssistantId == assistant.Id);
    }
}
