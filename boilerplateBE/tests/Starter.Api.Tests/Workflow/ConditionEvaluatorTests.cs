using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Starter.Abstractions.Capabilities;
using Starter.Module.Workflow.Infrastructure.Services;
using Xunit;

namespace Starter.Api.Tests.Workflow;

public sealed class ConditionEvaluatorTests
{
    private readonly IConditionEvaluator _sut = new ConditionEvaluator(NullLogger<ConditionEvaluator>.Instance);

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

    // --- Compound condition tests ---

    [Fact]
    public void Evaluate_AndGroup_AllTrue_ReturnsTrue()
    {
        var condition = new ConditionConfig(
            Logic: "and",
            Conditions:
            [
                new ConditionConfig(Field: "status", Operator: "equals", Value: "Active"),
                new ConditionConfig(Field: "role", Operator: "equals", Value: "Admin"),
            ]);
        var context = MakeContext(new() { ["status"] = "Active", ["role"] = "Admin" });

        _sut.Evaluate(condition, context).Should().BeTrue();
    }

    [Fact]
    public void Evaluate_AndGroup_OneFalse_ReturnsFalse()
    {
        var condition = new ConditionConfig(
            Logic: "and",
            Conditions:
            [
                new ConditionConfig(Field: "status", Operator: "equals", Value: "Active"),
                new ConditionConfig(Field: "role", Operator: "equals", Value: "Admin"),
            ]);
        var context = MakeContext(new() { ["status"] = "Active", ["role"] = "User" });

        _sut.Evaluate(condition, context).Should().BeFalse();
    }

    [Fact]
    public void Evaluate_OrGroup_OneTrueRestFalse_ReturnsTrue()
    {
        var condition = new ConditionConfig(
            Logic: "or",
            Conditions:
            [
                new ConditionConfig(Field: "status", Operator: "equals", Value: "Closed"),
                new ConditionConfig(Field: "status", Operator: "equals", Value: "Active"),
            ]);
        var context = MakeContext(new() { ["status"] = "Active" });

        _sut.Evaluate(condition, context).Should().BeTrue();
    }

    [Fact]
    public void Evaluate_OrGroup_AllFalse_ReturnsFalse()
    {
        var condition = new ConditionConfig(
            Logic: "or",
            Conditions:
            [
                new ConditionConfig(Field: "status", Operator: "equals", Value: "Closed"),
                new ConditionConfig(Field: "status", Operator: "equals", Value: "Pending"),
            ]);
        var context = MakeContext(new() { ["status"] = "Active" });

        _sut.Evaluate(condition, context).Should().BeFalse();
    }

    [Fact]
    public void Evaluate_NestedAndOr_EvaluatesCorrectly()
    {
        // { and: [leaf-true, { or: [leaf-false, leaf-true] }] } → true
        var condition = new ConditionConfig(
            Logic: "and",
            Conditions:
            [
                new ConditionConfig(Field: "status", Operator: "equals", Value: "Active"),
                new ConditionConfig(
                    Logic: "or",
                    Conditions:
                    [
                        new ConditionConfig(Field: "role", Operator: "equals", Value: "Manager"),
                        new ConditionConfig(Field: "role", Operator: "equals", Value: "Admin"),
                    ]),
            ]);
        var context = MakeContext(new() { ["status"] = "Active", ["role"] = "Admin" });

        _sut.Evaluate(condition, context).Should().BeTrue();
    }

    [Fact]
    public void Evaluate_EmptyConditionsList_AndReturnsTrue()
    {
        // AND over empty set is vacuously true (All() on empty = true)
        var condition = new ConditionConfig(Logic: "and", Conditions: []);
        var context = MakeContext(new() { ["status"] = "Active" });

        _sut.Evaluate(condition, context).Should().BeFalse();
    }

    [Fact]
    public void Evaluate_LeafCondition_BackwardCompatible()
    {
        // Plain leaf ConditionConfig (no Logic/Conditions) still works
        var condition = new ConditionConfig(Field: "x", Operator: "equals", Value: "y");
        var context = MakeContext(new() { ["x"] = "y" });

        _sut.Evaluate(condition, context).Should().BeTrue();
    }

    // --- NOT operator ---

    [Fact]
    public void Evaluate_NotGroup_SingleFalseChild_ReturnsTrue()
    {
        var condition = new ConditionConfig(
            Logic: "not",
            Conditions:
            [
                new ConditionConfig(Field: "status", Operator: "equals", Value: "Active"),
            ]);
        var context = MakeContext(new() { ["status"] = "Inactive" });

        _sut.Evaluate(condition, context).Should().BeTrue();
    }

    [Fact]
    public void Evaluate_NotGroup_SingleTrueChild_ReturnsFalse()
    {
        var condition = new ConditionConfig(
            Logic: "not",
            Conditions:
            [
                new ConditionConfig(Field: "status", Operator: "equals", Value: "Active"),
            ]);
        var context = MakeContext(new() { ["status"] = "Active" });

        _sut.Evaluate(condition, context).Should().BeFalse();
    }

    [Fact]
    public void Evaluate_NotGroup_EmptyConditions_ReturnsFalse()
    {
        // NOT over empty is ambiguous; we choose false (same as empty AND) to
        // prevent accidental permit-all semantics in misconfigured workflows.
        var condition = new ConditionConfig(Logic: "not", Conditions: []);
        _sut.Evaluate(condition, MakeContext(new() { ["x"] = "y" })).Should().BeFalse();
    }

    [Fact]
    public void Evaluate_NestedAndOrNot_EvaluatesCorrectly()
    {
        // AND [ equals("status", "Active"), NOT [ equals("role", "Guest") ] ]
        // status=Active, role=Admin => true && !false => true
        var condition = new ConditionConfig(
            Logic: "and",
            Conditions:
            [
                new ConditionConfig(Field: "status", Operator: "equals", Value: "Active"),
                new ConditionConfig(
                    Logic: "not",
                    Conditions: [ new ConditionConfig(Field: "role", Operator: "equals", Value: "Guest") ]),
            ]);
        var context = MakeContext(new() { ["status"] = "Active", ["role"] = "Admin" });

        _sut.Evaluate(condition, context).Should().BeTrue();
    }

    // --- Short-circuit semantics (documents intended behavior) ---

    [Fact]
    public void Evaluate_ShortCircuit_AndStopsOnFirstFalse()
    {
        var condition = new ConditionConfig(
            Logic: "and",
            Conditions:
            [
                new ConditionConfig(Field: "alwaysFalse", Operator: "equals", Value: "never"),
                new ConditionConfig(Field: "amount", Operator: "greaterThan", Value: 10),
            ]);
        var context = MakeContext(new() { ["alwaysFalse"] = "other", ["amount"] = 100 });

        _sut.Evaluate(condition, context).Should().BeFalse();
    }

    [Fact]
    public void Evaluate_ShortCircuit_OrStopsOnFirstTrue()
    {
        var condition = new ConditionConfig(
            Logic: "or",
            Conditions:
            [
                new ConditionConfig(Field: "status", Operator: "equals", Value: "Active"),
                new ConditionConfig(Field: "status", Operator: "equals", Value: "Closed"),
            ]);
        var context = MakeContext(new() { ["status"] = "Active" });

        _sut.Evaluate(condition, context).Should().BeTrue();
    }

    // --- JSON round-trip (engine reads StatesJson/TransitionsJson and must preserve structure) ---

    [Fact]
    public void Evaluate_JsonRoundTrip_PreservesStructure_AndEvaluatesCorrectly()
    {
        var original = new ConditionConfig(
            Logic: "or",
            Conditions:
            [
                new ConditionConfig(Field: "department", Operator: "equals", Value: "Finance"),
                new ConditionConfig(
                    Logic: "and",
                    Conditions:
                    [
                        new ConditionConfig(Field: "department", Operator: "equals", Value: "HR"),
                        new ConditionConfig(
                            Logic: "not",
                            Conditions: [ new ConditionConfig(Field: "role", Operator: "equals", Value: "Guest") ]),
                    ]),
            ]);

        var json = JsonSerializer.Serialize(original);
        var rehydrated = JsonSerializer.Deserialize<ConditionConfig>(json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        rehydrated.Should().NotBeNull();

        var context = MakeContext(new() { ["department"] = "HR", ["role"] = "Admin" });
        _sut.Evaluate(rehydrated!, context).Should().BeTrue();
    }
}
