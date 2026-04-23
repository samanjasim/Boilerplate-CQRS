using System.Text.Json;
using System.Text.RegularExpressions;
using FluentValidation;
using Starter.Abstractions.Capabilities;

namespace Starter.Module.Workflow.Application.Commands.UpdateDefinition;

public sealed partial class UpdateDefinitionCommandValidator : AbstractValidator<UpdateDefinitionCommand>
{
    private static readonly string[] KnownTypes = ["Initial", "HumanTask", "SystemAction", "Terminal"];
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public UpdateDefinitionCommandValidator()
    {
        RuleFor(x => x.DisplayName)
            .NotEmpty().WithMessage("DisplayName is required.")
            .MaximumLength(120);

        When(x => x.StatesJson is not null, () =>
        {
            RuleFor(x => x.StatesJson!)
                .Custom((statesJson, ctx) => ValidateStates(statesJson, ctx));
        });

        When(x => x.StatesJson is not null && x.TransitionsJson is not null, () =>
        {
            RuleFor(x => x).Custom((cmd, ctx) => ValidateTransitions(cmd.StatesJson!, cmd.TransitionsJson!, ctx));
        });
    }

    private static void ValidateStates(string statesJson, ValidationContext<UpdateDefinitionCommand> ctx)
    {
        List<WorkflowStateConfig>? states;
        try
        {
            states = JsonSerializer.Deserialize<List<WorkflowStateConfig>>(statesJson, JsonOpts);
        }
        catch (JsonException ex)
        {
            ctx.AddFailure(nameof(UpdateDefinitionCommand.StatesJson),
                $"StatesJson is not valid JSON: {ex.Message}");
            return;
        }

        if (states is null || states.Count == 0)
        {
            ctx.AddFailure(nameof(UpdateDefinitionCommand.StatesJson), "At least one state is required.");
            return;
        }

        // Per-state rules
        foreach (var (state, index) in states.Select((s, i) => (s, i)))
        {
            var prefix = $"states[{index}]";

            if (string.IsNullOrWhiteSpace(state.Name))
                ctx.AddFailure($"{prefix}.name", "State name is required.");
            else if (!SlugRegex().IsMatch(state.Name))
                ctx.AddFailure($"{prefix}.name",
                    $"State name '{state.Name}' must be a slug (letters, digits, underscore; starts with a letter).");
            else if (state.Name.Length > 80)
                ctx.AddFailure($"{prefix}.name", "State name must be 80 characters or fewer.");

            if (string.IsNullOrWhiteSpace(state.DisplayName))
                ctx.AddFailure($"{prefix}.displayName", "State displayName is required.");
            else if (state.DisplayName.Length > 120)
                ctx.AddFailure($"{prefix}.displayName", "State displayName must be 120 characters or fewer.");

            if (!KnownTypes.Contains(state.Type, StringComparer.OrdinalIgnoreCase))
                ctx.AddFailure($"{prefix}.type",
                    $"State type '{state.Type}' is not one of Initial, HumanTask, SystemAction, Terminal.");

            if (state.Type.Equals("HumanTask", StringComparison.OrdinalIgnoreCase)
                && (state.Assignee is null || string.IsNullOrWhiteSpace(state.Assignee.Strategy)))
            {
                ctx.AddFailure($"{prefix}.assignee",
                    $"State '{state.Name}' is HumanTask but has no assignee strategy.");
            }

            if (state.Sla is { ReminderAfterHours: { } reminder, EscalateAfterHours: { } escalate }
                && reminder >= escalate)
            {
                ctx.AddFailure($"{prefix}.sla",
                    $"State '{state.Name}' SLA: reminder hours ({reminder}) must be less than escalate hours ({escalate}).");
            }
        }

        // Uniqueness
        var duplicateNames = states
            .GroupBy(s => s.Name, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();
        foreach (var dup in duplicateNames)
            ctx.AddFailure(nameof(UpdateDefinitionCommand.StatesJson),
                $"State name '{dup}' must be unique within the definition.");

        // Exactly one Initial
        var initialCount = states.Count(s => s.Type.Equals("Initial", StringComparison.OrdinalIgnoreCase));
        if (initialCount != 1)
            ctx.AddFailure(nameof(UpdateDefinitionCommand.StatesJson),
                $"A definition must have exactly one Initial state (found {initialCount}).");

        // At least one Terminal
        var terminalCount = states.Count(s => s.Type.Equals("Terminal", StringComparison.OrdinalIgnoreCase));
        if (terminalCount == 0)
            ctx.AddFailure(nameof(UpdateDefinitionCommand.StatesJson),
                "A definition must have at least one Terminal state.");
    }

    private static void ValidateTransitions(
        string statesJson,
        string transitionsJson,
        ValidationContext<UpdateDefinitionCommand> ctx)
    {
        List<WorkflowStateConfig>? states;
        List<WorkflowTransitionConfig>? transitions;

        try
        {
            states = JsonSerializer.Deserialize<List<WorkflowStateConfig>>(statesJson, JsonOpts);
            transitions = JsonSerializer.Deserialize<List<WorkflowTransitionConfig>>(transitionsJson, JsonOpts);
        }
        catch (JsonException ex)
        {
            ctx.AddFailure(nameof(UpdateDefinitionCommand.TransitionsJson),
                $"TransitionsJson is not valid JSON: {ex.Message}");
            return;
        }

        if (states is null || transitions is null) return;

        // Build lookup defensively — duplicate state names are caught by ValidateStates; skip them here.
        var stateByName = states
            .GroupBy(s => s.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        foreach (var (t, index) in transitions.Select((t, i) => (t, i)))
        {
            var prefix = $"transitions[{index}]";

            if (string.IsNullOrWhiteSpace(t.Trigger))
                ctx.AddFailure($"{prefix}.trigger", "Transition trigger is required.");

            if (!stateByName.TryGetValue(t.From, out var fromState))
                ctx.AddFailure($"{prefix}.from",
                    $"Transition from '{t.From}' references an unknown state.");
            else if (fromState.Type.Equals("Terminal", StringComparison.OrdinalIgnoreCase))
                ctx.AddFailure($"{prefix}.from",
                    $"Transition cannot originate from Terminal state '{t.From}'.");

            if (!stateByName.ContainsKey(t.To))
                ctx.AddFailure($"{prefix}.to",
                    $"Transition to '{t.To}' references an unknown state.");
        }

        // Duplicate (from, trigger) pairs
        var duplicates = transitions
            .Where(t => !string.IsNullOrWhiteSpace(t.Trigger))
            .GroupBy(t => (t.From, t.Trigger), TupleEqualityComparer)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        foreach (var (from, trigger) in duplicates)
            ctx.AddFailure(nameof(UpdateDefinitionCommand.TransitionsJson),
                $"Transitions have duplicate (from='{from}', trigger='{trigger}') pair.");
    }

    private static readonly IEqualityComparer<(string From, string Trigger)> TupleEqualityComparer =
        EqualityComparer<(string From, string Trigger)>.Create(
            (a, b) => StringComparer.OrdinalIgnoreCase.Equals(a.From, b.From)
                      && StringComparer.OrdinalIgnoreCase.Equals(a.Trigger, b.Trigger),
            t => HashCode.Combine(
                StringComparer.OrdinalIgnoreCase.GetHashCode(t.From),
                StringComparer.OrdinalIgnoreCase.GetHashCode(t.Trigger)));

    [GeneratedRegex(@"^[A-Za-z][A-Za-z0-9_]*$", RegexOptions.CultureInvariant)]
    private static partial Regex SlugRegex();
}
