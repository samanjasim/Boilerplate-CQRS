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
    List<HookConfig>? OnExit = null,
    List<FormFieldDefinition>? FormFields = null,
    ParallelConfig? Parallel = null,
    SlaConfig? Sla = null);

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
    string? Field = null,
    string? Operator = null,
    object? Value = null,
    string? Logic = null,
    List<ConditionConfig>? Conditions = null);

public sealed record FormFieldDefinition(
    string Name,
    string Label,
    string Type,
    bool Required = false,
    List<SelectOption>? Options = null,
    double? Min = null,
    double? Max = null,
    int? MaxLength = null,
    string? Placeholder = null,
    string? Description = null);

public sealed record SelectOption(string Value, string Label);

public sealed record ParallelConfig(
    string Mode,
    List<AssigneeConfig> Assignees);

public sealed record SlaConfig(
    int? ReminderAfterHours = null,
    int? EscalateAfterHours = null);
