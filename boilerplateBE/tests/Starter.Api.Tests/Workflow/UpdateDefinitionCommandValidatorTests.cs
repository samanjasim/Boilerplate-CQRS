using System.Text.Json;
using FluentAssertions;
using Starter.Abstractions.Capabilities;
using Starter.Module.Workflow.Application.Commands.UpdateDefinition;
using Xunit;

namespace Starter.Api.Tests.Workflow;

public sealed class UpdateDefinitionCommandValidatorTests
{
    private readonly UpdateDefinitionCommandValidator _sut = new();

    private static string StatesJson(params WorkflowStateConfig[] states) =>
        JsonSerializer.Serialize(states.ToList());

    private static string TransitionsJson(params WorkflowTransitionConfig[] t) =>
        JsonSerializer.Serialize(t.ToList());

    [Fact]
    public void Passes_when_payload_has_one_initial_one_terminal_and_no_transitions()
    {
        var cmd = new UpdateDefinitionCommand(
            Guid.NewGuid(),
            DisplayName: "ok",
            Description: null,
            StatesJson: StatesJson(
                new("Start", "Start", "Initial"),
                new("Done", "Done", "Terminal")),
            TransitionsJson: TransitionsJson());

        var result = _sut.Validate(cmd);

        result.IsValid.Should().BeTrue(because: string.Join(", ", result.Errors.Select(e => e.ErrorMessage)));
    }

    [Fact]
    public void Fails_when_state_name_is_not_a_slug()
    {
        var cmd = new UpdateDefinitionCommand(Guid.NewGuid(), "ok", null,
            StatesJson(
                new("bad name", "Bad", "Initial"),
                new("Done", "Done", "Terminal")),
            TransitionsJson());

        var result = _sut.Validate(cmd);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("slug"));
    }

    [Fact]
    public void Fails_when_state_names_are_duplicated()
    {
        var cmd = new UpdateDefinitionCommand(Guid.NewGuid(), "ok", null,
            StatesJson(
                new("Start", "Start", "Initial"),
                new("Start", "Other", "Terminal")),
            TransitionsJson());

        var result = _sut.Validate(cmd);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("unique"));
    }

    [Fact]
    public void Fails_when_type_is_unknown()
    {
        var cmd = new UpdateDefinitionCommand(Guid.NewGuid(), "ok", null,
            StatesJson(
                new("Start", "Start", "Initial"),
                new("Weird", "Weird", "Bogus"),
                new("Done", "Done", "Terminal")),
            TransitionsJson());

        var result = _sut.Validate(cmd);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("Initial") && e.ErrorMessage.Contains("Terminal"));
    }

    [Fact]
    public void Fails_when_there_is_no_initial_state()
    {
        var cmd = new UpdateDefinitionCommand(Guid.NewGuid(), "ok", null,
            StatesJson(
                new("Mid", "Mid", "HumanTask"),
                new("Done", "Done", "Terminal")),
            TransitionsJson());

        var result = _sut.Validate(cmd);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("exactly one") && e.ErrorMessage.Contains("Initial"));
    }

    [Fact]
    public void Fails_when_there_are_two_initial_states()
    {
        var cmd = new UpdateDefinitionCommand(Guid.NewGuid(), "ok", null,
            StatesJson(
                new("A", "A", "Initial"),
                new("B", "B", "Initial"),
                new("Done", "Done", "Terminal")),
            TransitionsJson());

        var result = _sut.Validate(cmd);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("exactly one"));
    }

    [Fact]
    public void Fails_when_there_is_no_terminal_state()
    {
        var cmd = new UpdateDefinitionCommand(Guid.NewGuid(), "ok", null,
            StatesJson(
                new("Start", "Start", "Initial"),
                new("Middle", "Middle", "HumanTask")),
            TransitionsJson());

        var result = _sut.Validate(cmd);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("Terminal"));
    }

    [Fact]
    public void Fails_when_humantask_has_no_assignee()
    {
        var cmd = new UpdateDefinitionCommand(Guid.NewGuid(), "ok", null,
            StatesJson(
                new("Start", "Start", "Initial"),
                new("Review", "Review", "HumanTask"),
                new("Done", "Done", "Terminal")),
            TransitionsJson());

        var result = _sut.Validate(cmd);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("assignee"));
    }

    [Fact]
    public void Fails_when_sla_reminder_ge_escalate()
    {
        var cmd = new UpdateDefinitionCommand(Guid.NewGuid(), "ok", null,
            StatesJson(
                new("Start", "Start", "Initial"),
                new("Review", "Review", "HumanTask",
                    Assignee: new("Role", new() { ["roleName"] = "Admin" }),
                    Sla: new(ReminderAfterHours: 8, EscalateAfterHours: 4)),
                new("Done", "Done", "Terminal")),
            TransitionsJson());

        var result = _sut.Validate(cmd);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("reminder") && e.ErrorMessage.Contains("escalate"));
    }

    [Fact]
    public void Fails_when_displayName_is_empty()
    {
        var cmd = new UpdateDefinitionCommand(Guid.NewGuid(), "", null, null, null);

        var result = _sut.Validate(cmd);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "DisplayName");
    }

    [Fact]
    public void Fails_when_state_name_exceeds_80_chars()
    {
        var longName = "A" + new string('a', 80); // 81 chars, all valid slug chars
        var cmd = new UpdateDefinitionCommand(Guid.NewGuid(), "ok", null,
            StatesJson(
                new(longName, "Long", "Initial"),
                new("Done", "Done", "Terminal")),
            TransitionsJson());

        var result = _sut.Validate(cmd);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("80 characters"));
    }

    [Fact]
    public void Fails_when_state_displayName_exceeds_120_chars()
    {
        var longDisplay = new string('a', 121);
        var cmd = new UpdateDefinitionCommand(Guid.NewGuid(), "ok", null,
            StatesJson(
                new("Start", longDisplay, "Initial"),
                new("Done", "Done", "Terminal")),
            TransitionsJson());

        var result = _sut.Validate(cmd);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("120 characters"));
    }

    [Fact]
    public void Fails_when_transition_from_references_unknown_state()
    {
        var cmd = new UpdateDefinitionCommand(Guid.NewGuid(), "ok", null,
            StatesJson(
                new("Start", "Start", "Initial"),
                new("Done", "Done", "Terminal")),
            TransitionsJson(new WorkflowTransitionConfig("Ghost", "Done", "Go")));

        var result = _sut.Validate(cmd);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("from") && e.ErrorMessage.Contains("Ghost"));
    }

    [Fact]
    public void Fails_when_transition_to_references_unknown_state()
    {
        var cmd = new UpdateDefinitionCommand(Guid.NewGuid(), "ok", null,
            StatesJson(
                new("Start", "Start", "Initial"),
                new("Done", "Done", "Terminal")),
            TransitionsJson(new WorkflowTransitionConfig("Start", "Ghost", "Go")));

        var result = _sut.Validate(cmd);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("to") && e.ErrorMessage.Contains("Ghost"));
    }

    [Fact]
    public void Fails_when_trigger_is_empty()
    {
        var cmd = new UpdateDefinitionCommand(Guid.NewGuid(), "ok", null,
            StatesJson(
                new("Start", "Start", "Initial"),
                new("Done", "Done", "Terminal")),
            TransitionsJson(new WorkflowTransitionConfig("Start", "Done", "")));

        var result = _sut.Validate(cmd);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("trigger"));
    }

    [Fact]
    public void Fails_when_transition_originates_from_a_terminal_state()
    {
        var cmd = new UpdateDefinitionCommand(Guid.NewGuid(), "ok", null,
            StatesJson(
                new("Start", "Start", "Initial"),
                new("Done", "Done", "Terminal"),
                new("AlsoDone", "AlsoDone", "Terminal")),
            TransitionsJson(new WorkflowTransitionConfig("Done", "AlsoDone", "X")));

        var result = _sut.Validate(cmd);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("Terminal"));
    }

    [Fact]
    public void Fails_when_same_from_and_trigger_pair_appears_twice()
    {
        var cmd = new UpdateDefinitionCommand(Guid.NewGuid(), "ok", null,
            StatesJson(
                new("Start", "Start", "Initial"),
                new("A", "A", "Terminal"),
                new("B", "B", "Terminal")),
            TransitionsJson(
                new WorkflowTransitionConfig("Start", "A", "Go"),
                new WorkflowTransitionConfig("Start", "B", "Go")));

        var result = _sut.Validate(cmd);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("duplicate"));
    }
}
