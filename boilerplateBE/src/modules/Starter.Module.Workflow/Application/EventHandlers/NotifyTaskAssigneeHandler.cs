using System.Text.Json;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Starter.Abstractions.Capabilities;
using Starter.Module.Workflow.Domain.Events;

namespace Starter.Module.Workflow.Application.EventHandlers;

/// <summary>
/// Sends an in-app notification and email to the user assigned to a new
/// approval task. Email dispatch respects the user's notification preference
/// for <see cref="WellKnownNotificationTypes.WorkflowTaskAssigned"/>.
/// </summary>
internal sealed class NotifyTaskAssigneeHandler(
    INotificationServiceCapability notificationService,
    IMessageDispatcher messageDispatcher,
    INotificationPreferenceReader preferenceReader,
    IConfiguration configuration,
    ILogger<NotifyTaskAssigneeHandler> logger)
    : INotificationHandler<ApprovalTaskAssignedEvent>
{
    private const string TemplateName = "workflow.task-assigned";
    private const string PreferenceType = WellKnownNotificationTypes.WorkflowTaskAssigned;

    public async Task Handle(ApprovalTaskAssignedEvent notification, CancellationToken cancellationToken)
    {
        if (!notification.AssigneeUserId.HasValue) return;

        var assigneeUserId = notification.AssigneeUserId.Value;

        var data = JsonSerializer.Serialize(new
        {
            taskId = notification.TaskId,
            instanceId = notification.InstanceId,
            stepName = notification.StepName,
            entityType = notification.EntityType,
            entityId = notification.EntityId,
        });

        // In-app notification
        try
        {
            await notificationService.CreateAsync(
                assigneeUserId,
                notification.TenantId,
                WellKnownNotificationTypes.WorkflowTaskAssigned,
                "Workflow task assigned",
                $"You have a new approval task: {notification.StepName}.",
                data,
                cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Failed to send in-app notification for task {TaskId} to user {UserId}",
                notification.TaskId, assigneeUserId);
        }

        // Email notification (check preference first)
        try
        {
            var emailEnabled = await preferenceReader.IsEmailEnabledAsync(
                assigneeUserId, PreferenceType, cancellationToken);

            if (!emailEnabled)
            {
                logger.LogDebug(
                    "Skipping task-assigned email for {UserId} — preference disabled", assigneeUserId);
                return;
            }

            var appUrl = configuration.GetValue<string>("AppSettings:BaseUrl") ?? "";

            var variables = new Dictionary<string, object>
            {
                ["stepName"] = notification.StepName,
                ["entityType"] = notification.EntityType,
                ["entityId"] = notification.EntityId.ToString(),
                ["assigneeRole"] = notification.AssigneeRole ?? "",
                ["appUrl"] = appUrl,
            };

            await messageDispatcher.SendAsync(
                TemplateName, assigneeUserId, variables, notification.TenantId, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "Failed to dispatch task-assigned email to {UserId} for task {TaskId}",
                assigneeUserId, notification.TaskId);
        }
    }
}
