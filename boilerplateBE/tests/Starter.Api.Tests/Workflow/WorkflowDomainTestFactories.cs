using Starter.Module.Workflow.Domain.Entities;
using Starter.Module.Workflow.Domain.Enums;

namespace Starter.Api.Tests.Workflow;

internal static class ApprovalTaskTestFactory
{
    public static ApprovalTask Pending(
        Guid assigneeUserId,
        Guid? instanceId = null,
        DateTime? dueDate = null,
        string entityType = "Invoice")
    {
        return ApprovalTask.Create(
            tenantId: null,
            instanceId: instanceId ?? Guid.NewGuid(),
            stepName: "Review",
            assigneeUserId: assigneeUserId,
            assigneeRole: null,
            assigneeStrategyJson: null,
            entityType: entityType,
            entityId: Guid.NewGuid(),
            definitionName: $"{entityType}Approval",
            availableActionsJson: "[\"Approve\",\"Reject\"]",
            dueDate: dueDate,
            definitionDisplayName: $"{entityType} Approval",
            entityDisplayName: $"{entityType} 1",
            originalAssigneeUserId: assigneeUserId);
    }
}

internal static class WorkflowInstanceTestFactory
{
    public static WorkflowInstance Create(
        Guid startedByUserId,
        string entityType = "Invoice",
        string state = "Review",
        InstanceStatus status = InstanceStatus.Active)
    {
        var instance = WorkflowInstance.Create(
            tenantId: null,
            definitionId: Guid.NewGuid(),
            entityType: entityType,
            entityId: Guid.NewGuid(),
            initialState: state,
            startedByUserId: startedByUserId,
            contextJson: null,
            definitionName: $"{entityType} Approval",
            entityDisplayName: $"{entityType} 1");

        if (status == InstanceStatus.Completed)
            instance.Complete();
        else if (status == InstanceStatus.Cancelled)
            instance.Cancel("Cancelled in test", startedByUserId);

        return instance;
    }
}
