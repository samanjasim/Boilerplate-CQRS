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
}
