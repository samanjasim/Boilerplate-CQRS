using System.Text.Json;
using FluentAssertions;
using Starter.Abstractions.Capabilities;
using Starter.Module.Workflow.Infrastructure.Services;
using Xunit;

namespace Starter.Api.Tests.Workflow;

public sealed class ConditionEvaluatorTests
{
    private readonly IConditionEvaluator _sut = new ConditionEvaluator();

    // Helper: simulate what JsonSerializer.Deserialize<Dictionary<string, object>> produces —
    // values are JsonElement, not C# primitives.
    private static Dictionary<string, object> MakeContext(Dictionary<string, object> raw)
    {
        var json = JsonSerializer.Serialize(raw);
        return JsonSerializer.Deserialize<Dictionary<string, object>>(json)!;
    }

    [Fact]
    public void Evaluate_Equals_StringMatch_ReturnsTrue()
    {
        var condition = new ConditionConfig("status", "equals", "Active");
        var context = MakeContext(new() { ["status"] = "Active" });

        _sut.Evaluate(condition, context).Should().BeTrue();
    }

    [Fact]
    public void Evaluate_Equals_NoMatch_ReturnsFalse()
    {
        var condition = new ConditionConfig("status", "equals", "Active");
        var context = MakeContext(new() { ["status"] = "Inactive" });

        _sut.Evaluate(condition, context).Should().BeFalse();
    }

    [Fact]
    public void Evaluate_GreaterThan_NumericTrue()
    {
        // Both condition value and context value come from JSON deserialization as JsonElement
        var conditionJson = JsonSerializer.Serialize(new { field = "amount", op = "greaterThan", value = 1000 });
        var conditionDoc = JsonSerializer.Deserialize<JsonElement>(conditionJson);
        var conditionValue = conditionDoc.GetProperty("value"); // JsonElement

        var condition = new ConditionConfig("amount", "greaterThan", conditionValue);
        var context = MakeContext(new() { ["amount"] = 1500 });

        _sut.Evaluate(condition, context).Should().BeTrue();
    }

    [Fact]
    public void Evaluate_GreaterThan_NumericFalse()
    {
        var conditionJson = JsonSerializer.Serialize(new { value = 1000 });
        var conditionValue = JsonSerializer.Deserialize<JsonElement>(conditionJson).GetProperty("value");

        var condition = new ConditionConfig("amount", "greaterThan", conditionValue);
        var context = MakeContext(new() { ["amount"] = 500 });

        _sut.Evaluate(condition, context).Should().BeFalse();
    }

    [Fact]
    public void Evaluate_LessThan_Works()
    {
        var conditionJson = JsonSerializer.Serialize(new { value = 100 });
        var conditionValue = JsonSerializer.Deserialize<JsonElement>(conditionJson).GetProperty("value");

        var condition = new ConditionConfig("score", "lessThan", conditionValue);
        var context = MakeContext(new() { ["score"] = 42 });

        _sut.Evaluate(condition, context).Should().BeTrue();
    }

    [Fact]
    public void Evaluate_Contains_SubstringMatch()
    {
        var condition = new ConditionConfig("name", "contains", "test");
        var context = MakeContext(new() { ["name"] = "TestProject" });

        _sut.Evaluate(condition, context).Should().BeTrue();
    }

    [Fact]
    public void Evaluate_In_ValueInList()
    {
        // Simulate condition value as JsonElement array (as it would come from JSON)
        var conditionJson = JsonSerializer.Serialize(new { value = new[] { "HR", "Finance" } });
        var conditionValue = JsonSerializer.Deserialize<JsonElement>(conditionJson).GetProperty("value");

        var condition = new ConditionConfig("dept", "in", conditionValue);
        var context = MakeContext(new() { ["dept"] = "HR" });

        _sut.Evaluate(condition, context).Should().BeTrue();
    }

    [Fact]
    public void Evaluate_MissingField_ReturnsFalse()
    {
        var condition = new ConditionConfig("nonexistent", "equals", "value");
        var context = MakeContext(new() { ["someOtherField"] = "value" });

        _sut.Evaluate(condition, context).Should().BeFalse();
    }

    [Fact]
    public void Evaluate_NullContext_ReturnsFalse()
    {
        var condition = new ConditionConfig("status", "equals", "Active");

        _sut.Evaluate(condition, null).Should().BeFalse();
    }

    [Fact]
    public void Evaluate_NotEquals_Works()
    {
        var condition = new ConditionConfig("status", "notEquals", "Active");
        var context = MakeContext(new() { ["status"] = "Inactive" });

        _sut.Evaluate(condition, context).Should().BeTrue();
    }
}
