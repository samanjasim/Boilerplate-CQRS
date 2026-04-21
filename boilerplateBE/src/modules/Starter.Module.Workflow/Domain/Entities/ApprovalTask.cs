using Starter.Domain.Common;
using Starter.Module.Workflow.Domain.Events;
using TaskStatus = Starter.Module.Workflow.Domain.Enums.TaskStatus;

namespace Starter.Module.Workflow.Domain.Entities;

public sealed class ApprovalTask : AggregateRoot, ITenantEntity
{
    public Guid? TenantId { get; private set; }
    public Guid InstanceId { get; private set; }
    public string StepName { get; private set; } = default!;
    public Guid? AssigneeUserId { get; private set; }
    public string? AssigneeRole { get; private set; }
    public string? AssigneeStrategyJson { get; private set; }
    public TaskStatus Status { get; private set; }
    public string? Action { get; private set; }
    public string? Comment { get; private set; }
    public DateTime? DueDate { get; private set; }
    public DateTime? CompletedAt { get; private set; }
    public Guid? CompletedByUserId { get; private set; }
    public Guid? GroupId { get; private set; }
    public DateTime? ReminderSentAt { get; private set; }
    public DateTime? EscalatedAt { get; private set; }
    public Guid? OriginalAssigneeUserId { get; private set; }

    public string DefinitionName { get; private set; } = default!;
    public string? DefinitionDisplayName { get; private set; }
    public string EntityType { get; private set; } = default!;
    public Guid EntityId { get; private set; }
    public string? EntityDisplayName { get; private set; }
    public string? FormFieldsJson { get; private set; }
    public string AvailableActionsJson { get; private set; } = "[]";
    public int? SlaReminderAfterHours { get; private set; }

    public uint RowVersion { get; private set; }

    public WorkflowInstance Instance { get; private set; } = default!;

    private ApprovalTask() { }

    private ApprovalTask(
        Guid id,
        Guid? tenantId,
        Guid instanceId,
        string stepName,
        Guid? assigneeUserId,
        string? assigneeRole,
        string? assigneeStrategyJson,
        DateTime? dueDate,
        Guid? groupId,
        Guid? originalAssigneeUserId,
        string definitionName,
        string? definitionDisplayName,
        string entityType,
        Guid entityId,
        string? entityDisplayName,
        string? formFieldsJson,
        string availableActionsJson,
        int? slaReminderAfterHours) : base(id)
    {
        TenantId = tenantId;
        InstanceId = instanceId;
        StepName = stepName;
        AssigneeUserId = assigneeUserId;
        AssigneeRole = assigneeRole;
        AssigneeStrategyJson = assigneeStrategyJson;
        Status = TaskStatus.Pending;
        DueDate = dueDate;
        GroupId = groupId;
        OriginalAssigneeUserId = originalAssigneeUserId;
        DefinitionName = definitionName;
        DefinitionDisplayName = definitionDisplayName;
        EntityType = entityType;
        EntityId = entityId;
        EntityDisplayName = entityDisplayName;
        FormFieldsJson = formFieldsJson;
        AvailableActionsJson = availableActionsJson;
        SlaReminderAfterHours = slaReminderAfterHours;
    }

    public static ApprovalTask Create(
        Guid? tenantId,
        Guid instanceId,
        string stepName,
        Guid? assigneeUserId,
        string? assigneeRole,
        string? assigneeStrategyJson,
        string entityType,
        Guid entityId,
        string definitionName,
        string availableActionsJson,
        DateTime? dueDate = null,
        string? definitionDisplayName = null,
        string? entityDisplayName = null,
        string? formFieldsJson = null,
        int? slaReminderAfterHours = null,
        Guid? groupId = null,
        Guid? originalAssigneeUserId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(stepName);
        ArgumentException.ThrowIfNullOrWhiteSpace(entityType);
        ArgumentException.ThrowIfNullOrWhiteSpace(definitionName);
        ArgumentException.ThrowIfNullOrWhiteSpace(availableActionsJson);

        var task = new ApprovalTask(
            Guid.NewGuid(),
            tenantId,
            instanceId,
            stepName,
            assigneeUserId,
            assigneeRole,
            assigneeStrategyJson,
            dueDate,
            groupId,
            originalAssigneeUserId,
            definitionName,
            definitionDisplayName,
            entityType,
            entityId,
            entityDisplayName,
            formFieldsJson,
            availableActionsJson,
            slaReminderAfterHours);

        task.RaiseDomainEvent(new ApprovalTaskAssignedEvent(
            task.Id,
            instanceId,
            assigneeUserId,
            assigneeRole,
            stepName,
            entityType,
            entityId,
            tenantId));

        return task;
    }

    public void Complete(string action, string? comment, Guid userId)
    {
        if (Status != TaskStatus.Pending)
            throw new InvalidOperationException($"Cannot complete task '{Id}' — status is '{Status}', expected 'Pending'.");

        Action = action;
        Comment = comment;
        Status = TaskStatus.Completed;
        CompletedAt = DateTime.UtcNow;
        CompletedByUserId = userId;
        ModifiedAt = DateTime.UtcNow;

        RaiseDomainEvent(new ApprovalTaskCompletedEvent(
            Id,
            action,
            userId,
            comment,
            TenantId));
    }

    public void Cancel()
    {
        if (Status != TaskStatus.Pending)
            throw new InvalidOperationException($"Cannot cancel task '{Id}' — status is '{Status}', expected 'Pending'.");

        Status = TaskStatus.Cancelled;
        ModifiedAt = DateTime.UtcNow;
    }

    public void MarkReminderSent()
    {
        ReminderSentAt = DateTime.UtcNow;
        ModifiedAt = DateTime.UtcNow;
    }

    public void MarkEscalated()
    {
        EscalatedAt = DateTime.UtcNow;
        ModifiedAt = DateTime.UtcNow;
    }
}
