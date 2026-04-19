using Starter.Domain.Common;

namespace Starter.Module.Workflow.Domain.Events;

public sealed record WorkflowStartedEvent(
    Guid InstanceId,
    string EntityType,
    Guid EntityId,
    string DefinitionName,
    Guid InitiatorUserId,
    Guid? TenantId) : DomainEventBase;

public sealed record WorkflowTransitionEvent(
    Guid InstanceId,
    string FromState,
    string ToState,
    string Action,
    Guid? ActorUserId,
    string EntityType,
    Guid EntityId,
    Guid? TenantId) : DomainEventBase;

public sealed record WorkflowCompletedEvent(
    Guid InstanceId,
    string EntityType,
    Guid EntityId,
    string FinalState,
    Guid? TenantId) : DomainEventBase;

public sealed record WorkflowCancelledEvent(
    Guid InstanceId,
    string? Reason,
    Guid CancelledByUserId,
    Guid? TenantId) : DomainEventBase;

public sealed record ApprovalTaskAssignedEvent(
    Guid TaskId,
    Guid InstanceId,
    Guid? AssigneeUserId,
    string? AssigneeRole,
    string StepName,
    string EntityType,
    Guid EntityId,
    Guid? TenantId) : DomainEventBase;

public sealed record ApprovalTaskCompletedEvent(
    Guid TaskId,
    string Action,
    Guid ActorUserId,
    string? Comment,
    Guid? TenantId) : DomainEventBase;
