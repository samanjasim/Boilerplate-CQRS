using MediatR;
using Microsoft.Extensions.Logging;
using Starter.Application.Common.Interfaces;
using Starter.Module.Workflow.Domain.Events;

namespace Starter.Module.Workflow.Application.EventHandlers;

/// <summary>
/// Republishes <see cref="WorkflowTransitionEvent"/> and
/// <see cref="ApprovalTaskAssignedEvent"/> as integration events on the
/// message bus so external consumers can react to workflow state changes.
/// </summary>
internal sealed class PublishWorkflowTransitionIntegrationHandler(
    IMessagePublisher messagePublisher,
    TimeProvider clock,
    ILogger<PublishWorkflowTransitionIntegrationHandler> logger)
    : INotificationHandler<WorkflowTransitionEvent>
{
    public async Task Handle(WorkflowTransitionEvent notification, CancellationToken cancellationToken)
    {
        try
        {
            await messagePublisher.PublishAsync(
                new WorkflowTransitionIntegrationEvent(
                    notification.InstanceId,
                    notification.FromState,
                    notification.ToState,
                    notification.Action,
                    notification.ActorUserId,
                    notification.EntityType,
                    notification.EntityId,
                    notification.TenantId,
                    clock.GetUtcNow().UtcDateTime),
                cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "Failed to publish WorkflowTransitionIntegrationEvent for instance {InstanceId}",
                notification.InstanceId);
        }
    }
}

internal sealed class PublishTaskAssignedIntegrationHandler(
    IMessagePublisher messagePublisher,
    TimeProvider clock,
    ILogger<PublishTaskAssignedIntegrationHandler> logger)
    : INotificationHandler<ApprovalTaskAssignedEvent>
{
    public async Task Handle(ApprovalTaskAssignedEvent notification, CancellationToken cancellationToken)
    {
        try
        {
            await messagePublisher.PublishAsync(
                new WorkflowTaskAssignedIntegrationEvent(
                    notification.TaskId,
                    notification.InstanceId,
                    notification.AssigneeUserId,
                    notification.AssigneeRole,
                    notification.StepName,
                    notification.EntityType,
                    notification.EntityId,
                    notification.TenantId,
                    clock.GetUtcNow().UtcDateTime),
                cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "Failed to publish WorkflowTaskAssignedIntegrationEvent for task {TaskId}",
                notification.TaskId);
        }
    }
}

// ── Integration event contracts ──

public sealed record WorkflowTransitionIntegrationEvent(
    Guid InstanceId,
    string FromState,
    string ToState,
    string Action,
    Guid? ActorUserId,
    string EntityType,
    Guid EntityId,
    Guid? TenantId,
    DateTime OccurredAt);

public sealed record WorkflowTaskAssignedIntegrationEvent(
    Guid TaskId,
    Guid InstanceId,
    Guid? AssigneeUserId,
    string? AssigneeRole,
    string StepName,
    string EntityType,
    Guid EntityId,
    Guid? TenantId,
    DateTime OccurredAt);
