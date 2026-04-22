using System.Text.Json;
using FluentAssertions;
using Starter.Abstractions.Capabilities;
using Starter.Module.Workflow.Infrastructure.Services;
using Xunit;

namespace Starter.Api.Tests.Workflow;

public sealed class AutoTransitionEvaluatorTests
{
    private readonly AutoTransitionEvaluator _sut =
        new(new ConditionEvaluator());

    [Fact]
    public void Select_MatchingConditionWins_ReturnsConditionalTransition()
    {
        var transitions = new List<WorkflowTransitionConfig>
        {
            new("A", "HighValue", "auto", Condition: new ConditionConfig("amount", "greaterThan", 1000)),
            new("A", "LowValue",  "auto"),
        };
        var context = MakeContext(new() { ["amount"] = 2000 });

        var result = _sut.Select(transitions, fromState: "A", context);

        result.Should().NotBeNull();
        result!.To.Should().Be("HighValue");
    }

    [Fact]
    public void Select_NoConditionMatches_FallsBackToUnconditional()
    {
        var transitions = new List<WorkflowTransitionConfig>
        {
            new("A", "HighValue", "auto", Condition: new ConditionConfig("amount", "greaterThan", 10_000)),
            new("A", "LowValue",  "auto"),
        };
        var context = MakeContext(new() { ["amount"] = 500 });

        var result = _sut.Select(transitions, fromState: "A", context);

        result!.To.Should().Be("LowValue");
    }

    [Fact]
    public void Select_NoTransitionFromState_ReturnsNull()
    {
        var transitions = new List<WorkflowTransitionConfig>
        {
            new("OtherState", "X", "auto"),
        };
        _sut.Select(transitions, fromState: "A", context: null).Should().BeNull();
    }

    [Fact]
    public void Select_AllConditionalAndNoneMatch_ReturnsNull()
    {
        var transitions = new List<WorkflowTransitionConfig>
        {
            new("A", "X", "auto", Condition: new ConditionConfig("status", "equals", "active")),
        };
        var context = MakeContext(new() { ["status"] = "closed" });

        _sut.Select(transitions, fromState: "A", context).Should().BeNull();
    }

    private static Dictionary<string, object> MakeContext(Dictionary<string, object> raw)
    {
        var json = JsonSerializer.Serialize(raw);
        return JsonSerializer.Deserialize<Dictionary<string, object>>(json)!;
    }
}
