using System.Text.Json;
using Starter.Abstractions.Capabilities;
using Starter.Module.Workflow.Domain.Entities;
using Starter.Module.Workflow.Infrastructure.Persistence;

namespace Starter.Module.Workflow.Infrastructure.Services;

/// <summary>
/// Creates human approval tasks for a workflow instance entering a HumanTask state.
/// Handles both single-assignee and parallel (AllOf) assignee configurations, including
/// delegation swaps, assignee role extraction, and denormalization of columns used by
/// task inbox queries (step name, definition name, entity name, available actions, SLA).
/// </summary>
public sealed class HumanTaskFactory(
    WorkflowDbContext context,
    AssigneeResolverService assigneeResolver)
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    /// <summary>
    /// Creates one or more <see cref="ApprovalTask"/>s for the given state and adds them
    /// to the <see cref="WorkflowDbContext"/>. The caller is responsible for calling
    /// <c>SaveChangesAsync</c>.
    /// </summary>
    public async Task CreateAsync(
        WorkflowInstance instance,
        WorkflowStateConfig stateConfig,
        WorkflowDefinition definition,
        Guid initiatorUserId,
        CancellationToken ct)
    {
        // Pre-compute denormalized fields shared by both parallel + single-assignee branches.
        var formFieldsJson = stateConfig.FormFields is { Count: > 0 }
            ? JsonSerializer.Serialize(stateConfig.FormFields, JsonOpts)
            : null;

        var transitions = DeserializeTransitions(definition.TransitionsJson);
        var availableActions = transitions
            .Where(tr => tr.From == stateConfig.Name
                && tr.Type.Equals("Manual", StringComparison.OrdinalIgnoreCase))
            .Select(tr => tr.Trigger)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var availableActionsJson = JsonSerializer.Serialize(availableActions, JsonOpts);

        var slaReminderAfterHours = stateConfig.Sla?.ReminderAfterHours;

        // Parallel mode: create one task per assignee with a shared GroupId
        if (stateConfig.Parallel is { Assignees.Count: > 0 })
        {
            var groupId = Guid.NewGuid();
            var assigneeContext = new WorkflowAssigneeContext(
                instance.EntityType,
                instance.EntityId,
                instance.TenantId,
                initiatorUserId,
                instance.CurrentState);

            foreach (var assigneeConfig in stateConfig.Parallel.Assignees)
            {
                var strategyJson = JsonSerializer.Serialize(assigneeConfig, JsonOpts);
                var resolveResult = await assigneeResolver.ResolveWithDelegationAsync(
                    assigneeConfig, assigneeContext, ct);

                Guid? userId = resolveResult.AssigneeIds.Count > 0 ? resolveResult.AssigneeIds[0] : null;
                string? role = null;

                if (assigneeConfig.Strategy.Equals("Role", StringComparison.OrdinalIgnoreCase)
                    && assigneeConfig.Parameters is not null
                    && assigneeConfig.Parameters.TryGetValue("roleName", out var roleObj))
                {
                    role = roleObj?.ToString();
                }

                // If delegated, record the original assignee
                Guid? originalAssigneeUserId = userId.HasValue
                    && resolveResult.DelegationMap.TryGetValue(userId.Value, out var origId)
                        ? origId : null;

                var task = ApprovalTask.Create(
                    tenantId: instance.TenantId,
                    instanceId: instance.Id,
                    stepName: stateConfig.Name,
                    assigneeUserId: userId,
                    assigneeRole: role,
                    assigneeStrategyJson: strategyJson,
                    dueDate: null,
                    entityType: instance.EntityType,
                    entityId: instance.EntityId,
                    definitionName: definition.Name,
                    definitionDisplayName: definition.DisplayName,
                    entityDisplayName: instance.EntityDisplayName,
                    formFieldsJson: formFieldsJson,
                    availableActionsJson: availableActionsJson,
                    slaReminderAfterHours: slaReminderAfterHours,
                    groupId: groupId,
                    originalAssigneeUserId: originalAssigneeUserId);

                context.ApprovalTasks.Add(task);
            }

            return;
        }

        // Single-assignee mode
        Guid? assigneeUserId = null;
        string? assigneeRole = null;
        string? assigneeStrategyJson = null;
        Guid? originalAssignee = null;

        if (stateConfig.Assignee is not null)
        {
            assigneeStrategyJson = JsonSerializer.Serialize(stateConfig.Assignee, JsonOpts);

            var assigneeContext = new WorkflowAssigneeContext(
                instance.EntityType,
                instance.EntityId,
                instance.TenantId,
                initiatorUserId,
                instance.CurrentState);

            var resolveResult = await assigneeResolver.ResolveWithDelegationAsync(
                stateConfig.Assignee, assigneeContext, ct);

            if (resolveResult.AssigneeIds.Count > 0)
            {
                assigneeUserId = resolveResult.AssigneeIds[0];

                // If delegated, record the original assignee
                if (resolveResult.DelegationMap.TryGetValue(assigneeUserId.Value, out var origId))
                    originalAssignee = origId;
            }

            // Extract role from assignee config parameters if strategy is "Role"
            if (stateConfig.Assignee.Strategy.Equals("Role", StringComparison.OrdinalIgnoreCase)
                && stateConfig.Assignee.Parameters is not null
                && stateConfig.Assignee.Parameters.TryGetValue("roleName", out var roleObj))
            {
                assigneeRole = roleObj?.ToString();
            }
        }

        var approvalTask = ApprovalTask.Create(
            tenantId: instance.TenantId,
            instanceId: instance.Id,
            stepName: stateConfig.Name,
            assigneeUserId: assigneeUserId,
            assigneeRole: assigneeRole,
            assigneeStrategyJson: assigneeStrategyJson,
            dueDate: null,
            entityType: instance.EntityType,
            entityId: instance.EntityId,
            definitionName: definition.Name,
            definitionDisplayName: definition.DisplayName,
            entityDisplayName: instance.EntityDisplayName,
            formFieldsJson: formFieldsJson,
            availableActionsJson: availableActionsJson,
            slaReminderAfterHours: slaReminderAfterHours,
            originalAssigneeUserId: originalAssignee);

        context.ApprovalTasks.Add(approvalTask);
    }

    private static List<WorkflowTransitionConfig> DeserializeTransitions(string json)
        => JsonSerializer.Deserialize<List<WorkflowTransitionConfig>>(json, JsonOpts) ?? [];
}
