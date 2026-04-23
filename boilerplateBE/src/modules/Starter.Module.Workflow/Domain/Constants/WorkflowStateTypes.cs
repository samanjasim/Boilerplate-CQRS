namespace Starter.Module.Workflow.Domain.Constants;

/// <summary>
/// Canonical state type identifiers used in workflow definitions.
/// Matches the "type" field in WorkflowStateConfig JSON.
/// </summary>
public static class WorkflowStateTypes
{
    public const string Initial = "Initial";
    public const string HumanTask = "HumanTask";
    public const string SystemAction = "SystemAction";
    public const string Final = "Final";

    /// <summary>
    /// Absorbing state — completes the instance immediately on entry. Aliased
    /// from legacy workflow definitions; treated as a final outcome state.
    /// </summary>
    public const string Terminal = "Terminal";

    /// <summary>
    /// A transient state whose only purpose is to evaluate outgoing conditional
    /// transitions and forward automatically. No human task is created.
    /// </summary>
    public const string ConditionalGate = "ConditionalGate";
}
