using FluentAssertions;
using Starter.Module.AI.Domain.Entities;
using Starter.Abstractions.Ai;
using Xunit;

namespace Starter.Api.Tests.Ai.Assistants;

public sealed class AiAssistantBudgetFieldsTests
{
    private static AiAssistant BuildAssistant() =>
        AiAssistant.Create(
            tenantId: Guid.NewGuid(),
            name: "Test Assistant",
            description: null,
            systemPrompt: "You are a helpful assistant.",
            createdByUserId: Guid.NewGuid());

    [Fact]
    public void SetBudget_Updates_Cap_Fields()
    {
        var a = BuildAssistant();
        a.SetBudget(monthlyUsd: 50m, dailyUsd: 5m, requestsPerMinute: 30);
        a.MonthlyCostCapUsd.Should().Be(50m);
        a.DailyCostCapUsd.Should().Be(5m);
        a.RequestsPerMinute.Should().Be(30);
    }

    [Fact]
    public void SetBudget_Null_Clears_Override()
    {
        var a = BuildAssistant();
        a.SetBudget(50m, 5m, 30);
        a.SetBudget(null, null, null);
        a.MonthlyCostCapUsd.Should().BeNull();
        a.DailyCostCapUsd.Should().BeNull();
        a.RequestsPerMinute.Should().BeNull();
    }

    [Fact]
    public void SetBudget_NegativeMonthly_Throws()
    {
        var a = BuildAssistant();
        var act = () => a.SetBudget(monthlyUsd: -1m, dailyUsd: null, requestsPerMinute: null);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void SetBudget_NegativeDaily_Throws()
    {
        var a = BuildAssistant();
        var act = () => a.SetBudget(monthlyUsd: null, dailyUsd: -0.01m, requestsPerMinute: null);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void SetBudget_NegativeRpm_Throws()
    {
        var a = BuildAssistant();
        var act = () => a.SetBudget(monthlyUsd: null, dailyUsd: null, requestsPerMinute: -1);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void CapFields_DefaultToNull()
    {
        var a = BuildAssistant();
        a.MonthlyCostCapUsd.Should().BeNull();
        a.DailyCostCapUsd.Should().BeNull();
        a.RequestsPerMinute.Should().BeNull();
    }
}
