namespace Starter.Abstractions.Capabilities;

/// <summary>Config for seeding workflow templates via IWorkflowService.SeedTemplateAsync.</summary>
public sealed record WorkflowTemplateConfig(
    string DisplayName,
    string? Description,
    List<WorkflowStateConfig> States,
    List<WorkflowTransitionConfig> Transitions);

public sealed record WorkflowStateConfig(
    string Name,
    string DisplayName,
    string Type,
    AssigneeConfig? Assignee = null,
    List<string>? Actions = null,
    List<HookConfig>? OnEnter = null,
    List<HookConfig>? OnExit = null);

public sealed record WorkflowTransitionConfig(
    string From,
    string To,
    string Trigger,
    string Type = "Manual",
    ConditionConfig? Condition = null);

public sealed record AssigneeConfig(
    string Strategy,
    Dictionary<string, object>? Parameters = null,
    AssigneeConfig? Fallback = null);

public sealed record HookConfig(
    string Type,
    string? Template = null,
    string? To = null,
    string? Event = null,
    string? Action = null);

public sealed record ConditionConfig(
    string Field,
    string Operator,
    object Value);
