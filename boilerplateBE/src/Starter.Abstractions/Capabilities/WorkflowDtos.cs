namespace Starter.Abstractions.Capabilities;

public sealed record WorkflowStatusSummary(
    Guid InstanceId, Guid DefinitionId, string DefinitionName,
    string CurrentState, string Status, DateTime StartedAt, Guid StartedByUserId,
    string? EntityDisplayName = null,
    bool CanResubmit = false);

public sealed record PendingTaskSummary(
    Guid TaskId, Guid InstanceId, string DefinitionName,
    string EntityType, Guid EntityId, string StepName,
    string? AssigneeRole, DateTime CreatedAt, DateTime? DueDate,
    List<string>? AvailableActions = null,
    string? EntityDisplayName = null);

public sealed record WorkflowStepRecord(
    string FromState, string ToState, string StepType, string Action,
    Guid? ActorUserId, string? ActorDisplayName, string? Comment,
    DateTime Timestamp, Dictionary<string, object>? Metadata);

public sealed record WorkflowInstanceSummary(
    Guid InstanceId, Guid DefinitionId, string DefinitionName,
    string EntityType, Guid EntityId, string CurrentState,
    string Status, DateTime StartedAt, DateTime? CompletedAt,
    Guid? StartedByUserId = null, string? StartedByDisplayName = null,
    string? EntityDisplayName = null,
    bool CanResubmit = false);

public sealed record WorkflowDefinitionSummary(
    Guid Id, string Name, string EntityType, int StepCount,
    bool IsTemplate, bool IsActive, string? SourceModule);

public sealed record WorkflowDefinitionDetail(
    Guid Id, string Name, string EntityType, bool IsTemplate,
    bool IsActive, string? SourceModule,
    List<WorkflowStateConfig> States,
    List<WorkflowTransitionConfig> Transitions);
