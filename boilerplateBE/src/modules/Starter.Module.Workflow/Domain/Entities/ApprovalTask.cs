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
        DateTime? dueDate) : base(id)
    {
        TenantId = tenantId;
        InstanceId = instanceId;
        StepName = stepName;
        AssigneeUserId = assigneeUserId;
        AssigneeRole = assigneeRole;
        AssigneeStrategyJson = assigneeStrategyJson;
        Status = TaskStatus.Pending;
        DueDate = dueDate;
    }

    public static ApprovalTask Create(
        Guid? tenantId,
        Guid instanceId,
        string stepName,
        Guid? assigneeUserId,
        string? assigneeRole,
        string? assigneeStrategyJson,
        DateTime? dueDate)
    {
        var task = new ApprovalTask(
            Guid.NewGuid(),
            tenantId,
            instanceId,
            stepName,
            assigneeUserId,
            assigneeRole,
            assigneeStrategyJson,
            dueDate);

        // Event needs EntityType/EntityId from the instance — handler will enrich
        task.RaiseDomainEvent(new ApprovalTaskAssignedEvent(
            task.Id,
            instanceId,
            assigneeUserId,
            assigneeRole,
            stepName,
            string.Empty,
            Guid.Empty,
            tenantId));

        return task;
    }

    public void Complete(string action, string? comment, Guid userId)
    {
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
        Status = TaskStatus.Cancelled;
        ModifiedAt = DateTime.UtcNow;
    }
}
