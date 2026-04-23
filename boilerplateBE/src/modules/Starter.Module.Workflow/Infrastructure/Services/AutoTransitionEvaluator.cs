using Starter.Abstractions.Capabilities;

namespace Starter.Module.Workflow.Infrastructure.Services;

/// <summary>
/// Selects the transition to follow from a given state based on conditional logic.
/// Conditional transitions (with a <see cref="ConditionConfig"/>) are evaluated first
/// against the supplied instance context; the first one whose condition matches wins.
/// If no conditional transition matches, the first unconditional transition from the
/// state is returned. Returns <c>null</c> when no transition applies.
/// </summary>
internal sealed class AutoTransitionEvaluator(IConditionEvaluator conditionEvaluator)
{
    public WorkflowTransitionConfig? Select(
        IReadOnlyList<WorkflowTransitionConfig> transitions,
        string fromState,
        IReadOnlyDictionary<string, object>? context)
    {
        var candidates = transitions.Where(t => t.From == fromState).ToList();
        if (candidates.Count == 0) return null;

        var asDictionary = context as Dictionary<string, object>
            ?? context?.ToDictionary(kv => kv.Key, kv => kv.Value);

        foreach (var t in candidates.Where(t => t.Condition is not null))
        {
            if (conditionEvaluator.Evaluate(t.Condition!, asDictionary))
                return t;
        }

        return candidates.FirstOrDefault(t => t.Condition is null);
    }
}
