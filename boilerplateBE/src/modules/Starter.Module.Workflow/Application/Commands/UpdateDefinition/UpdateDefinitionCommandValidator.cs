using System.Text.Json;
using System.Text.RegularExpressions;
using FluentValidation;
using Starter.Abstractions.Capabilities;

namespace Starter.Module.Workflow.Application.Commands.UpdateDefinition;

public sealed partial class UpdateDefinitionCommandValidator : AbstractValidator<UpdateDefinitionCommand>
{
    private static readonly string[] KnownTypes = ["Initial", "HumanTask", "SystemAction", "Terminal"];

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
    }

    private static void ValidateStates(string statesJson, ValidationContext<UpdateDefinitionCommand> ctx)
    {
        List<WorkflowStateConfig>? states;
        try
        {
            states = JsonSerializer.Deserialize<List<WorkflowStateConfig>>(statesJson,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
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

    [GeneratedRegex(@"^[A-Za-z][A-Za-z0-9_]*$", RegexOptions.CultureInvariant)]
    private static partial Regex SlugRegex();
}
