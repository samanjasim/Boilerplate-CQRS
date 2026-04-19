using Starter.Domain.Common;
using Starter.Module.Workflow.Domain.Enums;
using Starter.Module.Workflow.Domain.Events;

namespace Starter.Module.Workflow.Domain.Entities;

public sealed class WorkflowInstance : AggregateRoot, ITenantEntity
{
    public Guid? TenantId { get; private set; }
    public Guid DefinitionId { get; private set; }
    public string EntityType { get; private set; } = default!;
    public Guid EntityId { get; private set; }
    public string CurrentState { get; private set; } = default!;
    public InstanceStatus Status { get; private set; }
    public Guid StartedByUserId { get; private set; }
    public DateTime StartedAt { get; private set; }
    public DateTime? CompletedAt { get; private set; }
    public DateTime? CancelledAt { get; private set; }
    public Guid? CancelledByUserId { get; private set; }
    public string? CancellationReason { get; private set; }
    public string? ContextJson { get; private set; }

    public WorkflowDefinition Definition { get; private set; } = default!;
    public ICollection<WorkflowStep> Steps { get; private set; } = [];
    public ICollection<ApprovalTask> Tasks { get; private set; } = [];

    private WorkflowInstance() { }

    private WorkflowInstance(
        Guid id,
        Guid? tenantId,
        Guid definitionId,
        string entityType,
        Guid entityId,
        string initialState,
        Guid startedByUserId,
        string? contextJson) : base(id)
    {
        TenantId = tenantId;
        DefinitionId = definitionId;
        EntityType = entityType;
        EntityId = entityId;
        CurrentState = initialState;
        Status = InstanceStatus.Active;
        StartedByUserId = startedByUserId;
        StartedAt = DateTime.UtcNow;
        ContextJson = contextJson;
    }

    public static WorkflowInstance Create(
        Guid? tenantId,
        Guid definitionId,
        string entityType,
        Guid entityId,
        string initialState,
        Guid startedByUserId,
        string? contextJson,
        string definitionName)
    {
        var instance = new WorkflowInstance(
            Guid.NewGuid(),
            tenantId,
            definitionId,
            entityType.Trim(),
            entityId,
            initialState.Trim(),
            startedByUserId,
            contextJson);

        instance.RaiseDomainEvent(new WorkflowStartedEvent(
            instance.Id,
            instance.EntityType,
            instance.EntityId,
            definitionName,
            startedByUserId,
            tenantId));

        return instance;
    }

    public void TransitionTo(string newState, string action, Guid? actorUserId)
    {
        var fromState = CurrentState;
        CurrentState = newState;
        ModifiedAt = DateTime.UtcNow;

        RaiseDomainEvent(new WorkflowTransitionEvent(
            Id,
            fromState,
            newState,
            action,
            actorUserId,
            EntityType,
            EntityId,
            TenantId));
    }

    public void Complete()
    {
        Status = InstanceStatus.Completed;
        CompletedAt = DateTime.UtcNow;
        ModifiedAt = DateTime.UtcNow;

        RaiseDomainEvent(new WorkflowCompletedEvent(
            Id,
            EntityType,
            EntityId,
            CurrentState,
            TenantId));
    }

    public void Cancel(string? reason, Guid userId)
    {
        Status = InstanceStatus.Cancelled;
        CancelledAt = DateTime.UtcNow;
        CancelledByUserId = userId;
        CancellationReason = reason;
        ModifiedAt = DateTime.UtcNow;

        RaiseDomainEvent(new WorkflowCancelledEvent(
            Id,
            reason,
            userId,
            TenantId));
    }
}
