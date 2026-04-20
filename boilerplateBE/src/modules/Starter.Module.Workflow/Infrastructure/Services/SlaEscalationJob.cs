using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Starter.Abstractions.Capabilities;
using Starter.Abstractions.Readers;
using Starter.Module.Workflow.Domain.Entities;
using Starter.Module.Workflow.Infrastructure.Persistence;
using TaskStatus = Starter.Module.Workflow.Domain.Enums.TaskStatus;

namespace Starter.Module.Workflow.Infrastructure.Services;

/// <summary>
/// Background job that runs every 15 minutes to check for overdue approval tasks.
/// Sends reminders and escalates tasks based on SLA configuration in workflow definitions.
/// </summary>
public sealed class SlaEscalationJob(
    IServiceScopeFactory scopeFactory,
    ILogger<SlaEscalationJob> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(15);

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await ProcessOverdueTasksAsync(ct);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "SLA processing failed");
            }

            await Task.Delay(Interval, ct);
        }
    }

    public async Task ProcessOverdueTasksAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<WorkflowDbContext>();
        var messageDispatcher = scope.ServiceProvider.GetRequiredService<IMessageDispatcher>();
        var assigneeResolver = scope.ServiceProvider.GetRequiredService<AssigneeResolverService>();
        var userReader = scope.ServiceProvider.GetRequiredService<IUserReader>();
        var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();

        var now = DateTime.UtcNow;

        // Only load tasks old enough to potentially need reminders/escalation.
        // Any SLA shorter than 1 hour would be unusual, so use 1h as a safe cutoff.
        var cutoff = now.AddHours(-1);

        var pendingTasks = await dbContext.ApprovalTasks
            .IgnoreQueryFilters()
            .Include(t => t.Instance)
                .ThenInclude(i => i.Definition)
            .Where(t => t.Status == TaskStatus.Pending && t.CreatedAt <= cutoff)
            .ToListAsync(ct);

        foreach (var task in pendingTasks)
        {
            try
            {
                var states = JsonSerializer.Deserialize<List<WorkflowStateConfig>>(
                    task.Instance.Definition.StatesJson, JsonOpts);
                var stateConfig = states?.FirstOrDefault(s => s.Name == task.StepName);
                if (stateConfig?.Sla is null) continue;

                var hoursElapsed = (now - task.CreatedAt).TotalHours;

                // Reminder
                if (stateConfig.Sla.ReminderAfterHours.HasValue
                    && hoursElapsed >= stateConfig.Sla.ReminderAfterHours.Value
                    && task.ReminderSentAt is null)
                {
                    await SendReminderAsync(task, messageDispatcher, userReader, config, ct);
                    task.MarkReminderSent();
                }

                // Escalation
                if (stateConfig.Sla.EscalateAfterHours.HasValue
                    && hoursElapsed >= stateConfig.Sla.EscalateAfterHours.Value
                    && task.EscalatedAt is null)
                {
                    await EscalateTaskAsync(task, stateConfig, dbContext, assigneeResolver,
                        messageDispatcher, userReader, config, ct);
                    task.MarkEscalated();
                }

                // Save after each task to avoid losing progress if a later task fails
                await dbContext.SaveChangesAsync(ct);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "SLA processing failed for task {TaskId}", task.Id);
            }
        }
    }

    private async Task SendReminderAsync(
        ApprovalTask task,
        IMessageDispatcher messageDispatcher,
        IUserReader userReader,
        IConfiguration config,
        CancellationToken ct)
    {
        if (!task.AssigneeUserId.HasValue) return;

        var appUrl = config.GetValue<string>("AppSettings:BaseUrl") ?? "";

        var variables = new Dictionary<string, object>
        {
            ["stepName"] = task.StepName,
            ["entityType"] = task.Instance.EntityType,
            ["entityId"] = task.Instance.EntityId.ToString(),
            ["workflowName"] = task.Instance.Definition.DisplayName,
            ["appUrl"] = appUrl,
        };

        try
        {
            await messageDispatcher.SendAsync(
                "workflow.sla-reminder",
                task.AssigneeUserId.Value,
                variables,
                task.TenantId,
                ct);

            logger.LogInformation(
                "Sent SLA reminder for task {TaskId} to user {UserId}",
                task.Id, task.AssigneeUserId.Value);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "Failed to send SLA reminder for task {TaskId}", task.Id);
        }
    }

    private async Task EscalateTaskAsync(
        ApprovalTask task,
        WorkflowStateConfig stateConfig,
        WorkflowDbContext dbContext,
        AssigneeResolverService assigneeResolver,
        IMessageDispatcher messageDispatcher,
        IUserReader userReader,
        IConfiguration config,
        CancellationToken ct)
    {
        var originalAssigneeId = task.AssigneeUserId;

        // Cancel the original task
        task.Cancel();

        // Resolve fallback assignee from the state's assignee config
        Guid? newAssigneeId = null;
        if (stateConfig.Assignee is not null)
        {
            var assigneeContext = new WorkflowAssigneeContext(
                task.Instance.EntityType,
                task.Instance.EntityId,
                task.TenantId,
                task.Instance.StartedByUserId,
                task.Instance.CurrentState);

            // Try fallback strategy first, then primary
            var assigneeConfig = stateConfig.Assignee.Fallback ?? stateConfig.Assignee;
            var assignees = await assigneeResolver.ResolveAsync(assigneeConfig, assigneeContext, ct);

            // Pick the first assignee that is different from the original
            newAssigneeId = assignees.FirstOrDefault(id => id != originalAssigneeId);
            if (newAssigneeId == Guid.Empty) newAssigneeId = null;

            // If no different assignee, use the first one anyway
            if (newAssigneeId is null && assignees.Count > 0)
                newAssigneeId = assignees[0];
        }

        // Create a new escalated task
        var escalatedTask = ApprovalTask.Create(
            task.TenantId,
            task.InstanceId,
            task.StepName,
            newAssigneeId,
            task.AssigneeRole,
            task.AssigneeStrategyJson,
            dueDate: null,
            entityType: task.Instance.EntityType,
            entityId: task.Instance.EntityId,
            groupId: task.GroupId,
            originalAssigneeUserId: originalAssigneeId);

        dbContext.ApprovalTasks.Add(escalatedTask);

        logger.LogInformation(
            "Escalated task {OldTaskId} -> {NewTaskId}. Original assignee: {OriginalAssignee}, new assignee: {NewAssignee}",
            task.Id, escalatedTask.Id, originalAssigneeId, newAssigneeId);

        // Send escalation notification to the new assignee
        if (newAssigneeId.HasValue)
        {
            var appUrl = config.GetValue<string>("AppSettings:BaseUrl") ?? "";

            var variables = new Dictionary<string, object>
            {
                ["stepName"] = task.StepName,
                ["entityType"] = task.Instance.EntityType,
                ["entityId"] = task.Instance.EntityId.ToString(),
                ["workflowName"] = task.Instance.Definition.DisplayName,
                ["appUrl"] = appUrl,
            };

            try
            {
                await messageDispatcher.SendAsync(
                    "workflow.sla-escalated",
                    newAssigneeId.Value,
                    variables,
                    task.TenantId,
                    ct);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex,
                    "Failed to send escalation notification for task {TaskId}", escalatedTask.Id);
            }
        }
    }
}
