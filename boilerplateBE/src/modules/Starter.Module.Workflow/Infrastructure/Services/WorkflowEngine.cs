using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Starter.Abstractions.Capabilities;
using Starter.Abstractions.Readers;
using Starter.Module.Workflow.Domain.Entities;
using Starter.Module.Workflow.Domain.Enums;
using Starter.Module.Workflow.Infrastructure.Persistence;

namespace Starter.Module.Workflow.Infrastructure.Services;

/// <summary>
/// Core state-machine engine implementing <see cref="IWorkflowService"/>.
/// Coordinates persistence, condition evaluation, assignee resolution,
/// hook execution, and cross-module capability calls (comments, activity, user reader).
/// </summary>
public sealed class WorkflowEngine(
    WorkflowDbContext context,
    IConditionEvaluator conditionEvaluator,
    AssigneeResolverService assigneeResolver,
    HookExecutor hookExecutor,
    ICommentService commentService,
    IUserReader userReader,
    ILogger<WorkflowEngine> logger) : IWorkflowService
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    // ── Lifecycle ────────────────────────────────────────────────────────────

    public async Task<Guid> StartAsync(
        string entityType, Guid entityId, string definitionName,
        Guid initiatorUserId, Guid? tenantId, string? entityDisplayName = null,
        CancellationToken ct = default)
    {
        var definition = await context.WorkflowDefinitions
            .FirstOrDefaultAsync(d =>
                d.Name == definitionName
                && d.IsActive
                && (d.TenantId == null || d.TenantId == tenantId), ct);

        if (definition is null)
        {
            logger.LogWarning(
                "No active workflow definition '{Name}' found for tenant {TenantId}.",
                definitionName, tenantId);
            return Guid.Empty;
        }

        var states = DeserializeStates(definition.StatesJson);
        var initialState = states.FirstOrDefault(s =>
            s.Type.Equals("Initial", StringComparison.OrdinalIgnoreCase))
            ?? states.FirstOrDefault();

        if (initialState is null)
        {
            logger.LogError(
                "Definition '{Name}' has no states defined.", definitionName);
            return Guid.Empty;
        }

        var instance = WorkflowInstance.Create(
            tenantId,
            definition.Id,
            entityType,
            entityId,
            initialState.Name,
            initiatorUserId,
            contextJson: null,
            definitionName: definition.DisplayName,
            entityDisplayName: entityDisplayName);

        context.WorkflowInstances.Add(instance);

        // Execute onEnter hooks for the initial state
        var hookCtx = BuildHookContext(instance, definition.Name, initialState.Name,
            previousState: null, action: null, actorUserId: initiatorUserId,
            assigneeUserId: null, assigneeRole: null);
        await hookExecutor.ExecuteAsync(initialState.OnEnter, hookCtx, ct);

        // Delegate to HandleNewStateAsync with isStarting=true so that
        // Initial-type states auto-transition on first start (but not on
        // return-for-revision via ExecuteTask).
        await HandleNewStateAsync(instance, definition, states, initialState,
            initiatorUserId, ct, isStarting: true);

        await context.SaveChangesAsync(ct);

        logger.LogInformation(
            "Started workflow instance {InstanceId} for {EntityType}/{EntityId} using definition '{Definition}'.",
            instance.Id, entityType, entityId, definitionName);

        return instance.Id;
    }

    public async Task CancelAsync(
        Guid instanceId, string? reason, Guid actorUserId, CancellationToken ct = default)
    {
        var instance = await context.WorkflowInstances
            .FirstOrDefaultAsync(i => i.Id == instanceId, ct);

        if (instance is null || instance.Status != InstanceStatus.Active)
        {
            logger.LogWarning("Cannot cancel instance {InstanceId}: not found or not active.", instanceId);
            return;
        }

        instance.Cancel(reason, actorUserId);

        // Cancel all pending approval tasks
        var pendingTasks = await context.ApprovalTasks
            .Where(t => t.InstanceId == instanceId
                && t.Status == Domain.Enums.TaskStatus.Pending)
            .ToListAsync(ct);

        foreach (var task in pendingTasks)
            task.Cancel();

        // Record a cancellation step
        var step = WorkflowStep.Create(
            instanceId,
            instance.CurrentState,
            instance.CurrentState,
            StepType.SystemAction,
            "cancel",
            actorUserId,
            reason,
            metadataJson: null);

        context.WorkflowSteps.Add(step);
        await context.SaveChangesAsync(ct);

        logger.LogInformation(
            "Cancelled workflow instance {InstanceId}. Reason: {Reason}",
            instanceId, reason);
    }

    // ── Task Actions ─────────────────────────────────────────────────────────

    public async Task<bool> ExecuteTaskAsync(
        Guid taskId, string action, string? comment,
        Guid actorUserId, CancellationToken ct = default)
    {
        var task = await context.ApprovalTasks
            .Include(t => t.Instance)
                .ThenInclude(i => i.Definition)
            .FirstOrDefaultAsync(t => t.Id == taskId, ct);

        if (task is null)
        {
            logger.LogWarning("Approval task {TaskId} not found.", taskId);
            return false;
        }

        if (task.Status != Domain.Enums.TaskStatus.Pending)
        {
            logger.LogWarning("Task {TaskId} is not pending (status: {Status}).", taskId, task.Status);
            return false;
        }

        // Verify the actor is the assigned user (or task has no specific assignee)
        if (task.AssigneeUserId.HasValue && task.AssigneeUserId.Value != actorUserId)
        {
            logger.LogWarning(
                "Actor {ActorId} is not assigned to task {TaskId} (assigned to {AssigneeId}).",
                actorUserId, taskId, task.AssigneeUserId);
            return false;
        }

        var instance = task.Instance;
        var definition = instance.Definition;

        var states = DeserializeStates(definition.StatesJson);
        var transitions = DeserializeTransitions(definition.TransitionsJson);

        // Find valid transitions from the current state matching the action
        var matchingTransitions = transitions
            .Where(t => t.From == instance.CurrentState && t.Trigger == action)
            .ToList();

        if (matchingTransitions.Count == 0)
        {
            logger.LogWarning(
                "No transition from '{FromState}' with trigger '{Action}' in definition '{DefName}'.",
                instance.CurrentState, action, definition.Name);
            return false;
        }

        // If multiple transitions match, evaluate conditional ones first
        WorkflowTransitionConfig? selectedTransition = null;

        var conditionalTransitions = matchingTransitions
            .Where(t => t.Condition is not null)
            .ToList();

        if (conditionalTransitions.Count > 0)
        {
            // Parse instance context for condition evaluation
            var instanceContext = instance.ContextJson is not null
                ? JsonSerializer.Deserialize<Dictionary<string, object>>(instance.ContextJson, JsonOpts)
                : null;

            foreach (var ct2 in conditionalTransitions)
            {
                if (conditionEvaluator.Evaluate(ct2.Condition!, instanceContext))
                {
                    selectedTransition = ct2;
                    break;
                }
            }
        }

        // If no conditional transition matched, use the default (manual) one
        selectedTransition ??= matchingTransitions.FirstOrDefault(t => t.Condition is null)
            ?? matchingTransitions[0];

        var fromState = instance.CurrentState;
        var toState = selectedTransition.To;

        // Look up state configs
        var fromStateConfig = states.FirstOrDefault(s => s.Name == fromState);
        var toStateConfig = states.FirstOrDefault(s => s.Name == toState);

        // Execute onExit hooks for the old state
        if (fromStateConfig is not null)
        {
            var exitHookCtx = BuildHookContext(instance, definition.Name, fromState,
                previousState: null, action: action, actorUserId: actorUserId,
                assigneeUserId: task.AssigneeUserId, assigneeRole: task.AssigneeRole);
            await hookExecutor.ExecuteAsync(fromStateConfig.OnExit, exitHookCtx, ct);
        }

        // Transition the instance
        instance.TransitionTo(toState, action, actorUserId);

        // Complete the task
        task.Complete(action, comment, actorUserId);

        // Create a WorkflowStep record
        var step = WorkflowStep.Create(
            instance.Id,
            fromState,
            toState,
            StepType.HumanTask,
            action,
            actorUserId,
            comment,
            metadataJson: null);

        context.WorkflowSteps.Add(step);

        // Save the comment via ICommentService if provided
        if (!string.IsNullOrWhiteSpace(comment))
        {
            try
            {
                await commentService.AddCommentAsync(
                    instance.EntityType,
                    instance.EntityId,
                    instance.TenantId,
                    actorUserId,
                    comment,
                    mentionsJson: null,
                    attachmentFileIds: null,
                    ct: ct);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to save comment for task {TaskId}.", taskId);
            }
        }

        // Execute onEnter hooks for the new state
        if (toStateConfig is not null)
        {
            var enterHookCtx = BuildHookContext(instance, definition.Name, toState,
                previousState: fromState, action: action, actorUserId: actorUserId,
                assigneeUserId: task.AssigneeUserId, assigneeRole: task.AssigneeRole);
            await hookExecutor.ExecuteAsync(toStateConfig.OnEnter, enterHookCtx, ct);
        }

        // Handle state type of the new state
        if (toStateConfig is not null)
        {
            await HandleNewStateAsync(instance, definition, states, toStateConfig, actorUserId, ct);
        }

        await context.SaveChangesAsync(ct);

        logger.LogInformation(
            "Task {TaskId}: transitioned instance {InstanceId} from '{From}' to '{To}' via '{Action}'.",
            taskId, instance.Id, fromState, toState, action);

        return true;
    }

    // ── Query: Status ────────────────────────────────────────────────────────

    public async Task<WorkflowStatusSummary?> GetStatusAsync(
        string entityType, Guid entityId, CancellationToken ct = default)
    {
        var instance = await context.WorkflowInstances
            .Include(i => i.Definition)
            .Where(i => i.EntityType == entityType
                && i.EntityId == entityId
                && i.Status == InstanceStatus.Active)
            .FirstOrDefaultAsync(ct);

        if (instance is null) return null;

        return new WorkflowStatusSummary(
            instance.Id,
            instance.DefinitionId,
            instance.Definition.Name,
            instance.CurrentState,
            instance.Status.ToString(),
            instance.StartedAt,
            instance.StartedByUserId,
            instance.EntityDisplayName);
    }

    public async Task<bool> IsInStateAsync(
        string entityType, Guid entityId, string stateName, CancellationToken ct = default)
    {
        return await context.WorkflowInstances
            .AnyAsync(i => i.EntityType == entityType
                && i.EntityId == entityId
                && i.Status == InstanceStatus.Active
                && i.CurrentState == stateName, ct);
    }

    // ── Query: Inbox ─────────────────────────────────────────────────────────

    public async Task<IReadOnlyList<PendingTaskSummary>> GetPendingTasksAsync(
        Guid userId, CancellationToken ct = default)
    {
        var tasks = await context.ApprovalTasks
            .Include(t => t.Instance)
                .ThenInclude(i => i.Definition)
            .Where(t => t.Status == Domain.Enums.TaskStatus.Pending
                && t.AssigneeUserId == userId)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync(ct);

        return tasks.Select(t =>
        {
            // Derive available actions from the definition's transitions for
            // the instance's current state (manual transitions only).
            List<string>? availableActions = null;
            try
            {
                var transitions = DeserializeTransitions(t.Instance.Definition.TransitionsJson);
                availableActions = transitions
                    .Where(tr => tr.From == t.Instance.CurrentState
                        && tr.Type.Equals("Manual", StringComparison.OrdinalIgnoreCase))
                    .Select(tr => tr.Trigger)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }
            catch
            {
                // Swallow deserialization errors — fall back to null
            }

            return new PendingTaskSummary(
                t.Id,
                t.InstanceId,
                t.Instance.Definition.Name,
                t.Instance.EntityType,
                t.Instance.EntityId,
                t.StepName,
                t.AssigneeRole,
                t.CreatedAt,
                t.DueDate,
                availableActions,
                t.Instance.EntityDisplayName);
        }).ToList();
    }

    public async Task<int> GetPendingTaskCountAsync(
        Guid userId, CancellationToken ct = default)
    {
        return await context.ApprovalTasks
            .CountAsync(t => t.Status == Domain.Enums.TaskStatus.Pending
                && t.AssigneeUserId == userId, ct);
    }

    // ── Query: History ───────────────────────────────────────────────────────

    public async Task<IReadOnlyList<WorkflowStepRecord>> GetHistoryAsync(
        Guid instanceId, CancellationToken ct = default)
    {
        var steps = await context.WorkflowSteps
            .Where(s => s.InstanceId == instanceId)
            .OrderBy(s => s.Timestamp)
            .ToListAsync(ct);

        // Resolve actor display names
        var actorIds = steps
            .Where(s => s.ActorUserId.HasValue)
            .Select(s => s.ActorUserId!.Value)
            .Distinct()
            .ToList();

        var userLookup = new Dictionary<Guid, string>();
        if (actorIds.Count > 0)
        {
            var users = await userReader.GetManyAsync(actorIds, ct);
            foreach (var u in users)
                userLookup[u.Id] = u.DisplayName;
        }

        return steps.Select(s => new WorkflowStepRecord(
            s.FromState,
            s.ToState,
            s.StepType.ToString(),
            s.Action,
            s.ActorUserId,
            s.ActorUserId.HasValue && userLookup.TryGetValue(s.ActorUserId.Value, out var name)
                ? name : null,
            s.Comment,
            s.Timestamp,
            s.MetadataJson is not null
                ? JsonSerializer.Deserialize<Dictionary<string, object>>(s.MetadataJson, JsonOpts)
                : null))
            .ToList();
    }

    public async Task<IReadOnlyList<WorkflowInstanceSummary>> GetInstancesAsync(
        string? entityType = null, string? state = null, Guid? startedByUserId = null,
        string? status = null, int page = 1, int pageSize = 20,
        CancellationToken ct = default)
    {
        var query = context.WorkflowInstances
            .Include(i => i.Definition)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(entityType))
            query = query.Where(i => i.EntityType == entityType);

        if (!string.IsNullOrWhiteSpace(state))
            query = query.Where(i => i.CurrentState == state);

        if (startedByUserId.HasValue)
            query = query.Where(i => i.StartedByUserId == startedByUserId.Value);

        if (!string.IsNullOrWhiteSpace(status)
            && Enum.TryParse<InstanceStatus>(status, ignoreCase: true, out var parsedStatus))
            query = query.Where(i => i.Status == parsedStatus);

        var instances = await query
            .OrderByDescending(i => i.StartedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        // Resolve display names for all unique initiators in one batch
        var initiatorIds = instances
            .Select(i => i.StartedByUserId)
            .Distinct()
            .ToList();

        var userLookup = new Dictionary<Guid, string>();
        if (initiatorIds.Count > 0)
        {
            var users = await userReader.GetManyAsync(initiatorIds, ct);
            foreach (var u in users)
                userLookup[u.Id] = u.DisplayName;
        }

        return instances.Select(i => new WorkflowInstanceSummary(
            i.Id,
            i.DefinitionId,
            i.Definition.Name,
            i.EntityType,
            i.EntityId,
            i.CurrentState,
            i.Status.ToString(),
            i.StartedAt,
            i.CompletedAt,
            i.StartedByUserId,
            userLookup.TryGetValue(i.StartedByUserId, out var name) ? name : null,
            i.EntityDisplayName))
            .ToList();
    }

    // ── Query: Definitions ───────────────────────────────────────────────────

    public async Task<IReadOnlyList<WorkflowDefinitionSummary>> GetDefinitionsAsync(
        string? entityType = null, Guid? tenantId = null, CancellationToken ct = default)
    {
        var query = context.WorkflowDefinitions.AsQueryable();

        if (!string.IsNullOrWhiteSpace(entityType))
            query = query.Where(d => d.EntityType == entityType);

        if (tenantId.HasValue)
            query = query.Where(d => d.TenantId == null || d.TenantId == tenantId.Value);

        var definitions = await query
            .OrderBy(d => d.Name)
            .ToListAsync(ct);

        return definitions.Select(d =>
        {
            var stateCount = 0;
            try
            {
                var stList = DeserializeStates(d.StatesJson);
                stateCount = stList.Count;
            }
            catch { /* swallow parse errors for listing */ }

            return new WorkflowDefinitionSummary(
                d.Id,
                d.Name,
                d.EntityType,
                stateCount,
                d.IsTemplate,
                d.IsActive,
                d.SourceModule);
        }).ToList();
    }

    public async Task<WorkflowDefinitionDetail?> GetDefinitionAsync(
        Guid definitionId, CancellationToken ct = default)
    {
        var definition = await context.WorkflowDefinitions
            .FirstOrDefaultAsync(d => d.Id == definitionId, ct);

        if (definition is null) return null;

        var states = DeserializeStates(definition.StatesJson);
        var transitions = DeserializeTransitions(definition.TransitionsJson);

        return new WorkflowDefinitionDetail(
            definition.Id,
            definition.Name,
            definition.EntityType,
            definition.IsTemplate,
            definition.IsActive,
            definition.SourceModule,
            states,
            transitions);
    }

    // ── Template Seeding ─────────────────────────────────────────────────────

    public async Task SeedTemplateAsync(
        string name, string entityType, WorkflowTemplateConfig config,
        CancellationToken ct = default)
    {
        var exists = await context.WorkflowDefinitions
            .IgnoreQueryFilters()
            .AnyAsync(d => d.Name == name, ct);

        if (exists)
        {
            logger.LogDebug("Template '{Name}' already exists; skipping seed.", name);
            return;
        }

        var statesJson = JsonSerializer.Serialize(config.States, JsonOpts);
        var transitionsJson = JsonSerializer.Serialize(config.Transitions, JsonOpts);

        var definition = WorkflowDefinition.Create(
            tenantId: null,
            name: name,
            displayName: config.DisplayName,
            entityType: entityType,
            statesJson: statesJson,
            transitionsJson: transitionsJson,
            isTemplate: true,
            sourceModule: null);

        context.WorkflowDefinitions.Add(definition);
        await context.SaveChangesAsync(ct);

        logger.LogInformation("Seeded workflow template '{Name}' for entity type '{EntityType}'.",
            name, entityType);
    }

    // ── Private Helpers ──────────────────────────────────────────────────────

    private async Task CreateApprovalTaskAsync(
        WorkflowInstance instance,
        WorkflowStateConfig stateConfig,
        WorkflowDefinition definition,
        Guid initiatorUserId,
        CancellationToken ct)
    {
        Guid? assigneeUserId = null;
        string? assigneeRole = null;
        string? assigneeStrategyJson = null;

        if (stateConfig.Assignee is not null)
        {
            assigneeStrategyJson = JsonSerializer.Serialize(stateConfig.Assignee, JsonOpts);

            var assigneeContext = new WorkflowAssigneeContext(
                instance.EntityType,
                instance.EntityId,
                instance.TenantId,
                initiatorUserId,
                instance.CurrentState);

            var assignees = await assigneeResolver.ResolveAsync(
                stateConfig.Assignee, assigneeContext, ct);

            if (assignees.Count > 0)
            {
                assigneeUserId = assignees[0];
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
            instance.TenantId,
            instance.Id,
            stateConfig.Name,
            assigneeUserId,
            assigneeRole,
            assigneeStrategyJson,
            dueDate: null,
            entityType: instance.EntityType,
            entityId: instance.EntityId);

        context.ApprovalTasks.Add(approvalTask);
    }

    private async Task AutoTransitionAsync(
        WorkflowInstance instance,
        WorkflowDefinition definition,
        List<WorkflowStateConfig> states,
        WorkflowStateConfig currentStateConfig,
        Guid actorUserId,
        CancellationToken ct)
    {
        var transitions = DeserializeTransitions(definition.TransitionsJson);

        // Find automatic transitions from this state
        var autoTransitions = transitions
            .Where(t => t.From == currentStateConfig.Name)
            .ToList();

        if (autoTransitions.Count == 0) return;

        // Evaluate conditional transitions first
        WorkflowTransitionConfig? selected = null;
        var instanceContext = instance.ContextJson is not null
            ? JsonSerializer.Deserialize<Dictionary<string, object>>(instance.ContextJson, JsonOpts)
            : null;

        foreach (var t in autoTransitions.Where(t => t.Condition is not null))
        {
            if (conditionEvaluator.Evaluate(t.Condition!, instanceContext))
            {
                selected = t;
                break;
            }
        }

        selected ??= autoTransitions.FirstOrDefault(t => t.Condition is null);

        if (selected is null) return;

        var fromState = instance.CurrentState;

        // Execute onExit hooks
        var exitHookCtx = BuildHookContext(instance, definition.Name, fromState,
            previousState: null, action: selected.Trigger, actorUserId: actorUserId,
            assigneeUserId: null, assigneeRole: null);
        await hookExecutor.ExecuteAsync(currentStateConfig.OnExit, exitHookCtx, ct);

        // Transition
        instance.TransitionTo(selected.To, selected.Trigger, actorUserId);

        // Create step record
        var step = WorkflowStep.Create(
            instance.Id,
            fromState,
            selected.To,
            StepType.SystemAction,
            selected.Trigger,
            actorUserId,
            comment: null,
            metadataJson: null);

        context.WorkflowSteps.Add(step);

        // Execute onEnter hooks for the new state
        var toStateConfig = states.FirstOrDefault(s => s.Name == selected.To);
        if (toStateConfig is not null)
        {
            var enterHookCtx = BuildHookContext(instance, definition.Name, selected.To,
                previousState: fromState, action: selected.Trigger, actorUserId: actorUserId,
                assigneeUserId: null, assigneeRole: null);
            await hookExecutor.ExecuteAsync(toStateConfig.OnEnter, enterHookCtx, ct);

            await HandleNewStateAsync(instance, definition, states, toStateConfig, actorUserId, ct);
        }
    }

    /// <summary>
    /// Handles a ConditionalGate state: evaluates outgoing transitions against
    /// the instance context and auto-transitions to the matching target state.
    /// Supports chaining (e.g. ConditionalGate -> SystemAction -> HumanTask).
    /// </summary>
    private async Task HandleConditionalGateAsync(
        WorkflowInstance instance,
        WorkflowDefinition definition,
        List<WorkflowStateConfig> states,
        WorkflowStateConfig stateConfig,
        Guid actorUserId,
        CancellationToken ct)
    {
        var instanceContext = string.IsNullOrWhiteSpace(instance.ContextJson)
            ? null
            : JsonSerializer.Deserialize<Dictionary<string, object>>(instance.ContextJson, JsonOpts);

        var transitions = DeserializeTransitions(definition.TransitionsJson);
        var fromState = instance.CurrentState;
        var matchingTransitions = transitions.Where(t => t.From == fromState).ToList();

        // Evaluate conditional transitions first
        string? targetState = null;
        foreach (var t in matchingTransitions.Where(t => t.Condition is not null))
        {
            if (conditionEvaluator.Evaluate(t.Condition!, instanceContext))
            {
                targetState = t.To;
                break;
            }
        }

        // Fall back to default (non-conditional) transition
        targetState ??= matchingTransitions.FirstOrDefault(t => t.Condition is null)?.To;

        if (targetState is null)
        {
            logger.LogWarning(
                "ConditionalGate '{State}' in instance {InstanceId} has no valid transition — stalling.",
                fromState, instance.Id);
            return;
        }

        // Execute onExit hooks for the gate state
        var exitHookCtx = BuildHookContext(instance, definition.Name, fromState,
            previousState: null, action: "auto-branch", actorUserId: actorUserId,
            assigneeUserId: null, assigneeRole: null);
        await hookExecutor.ExecuteAsync(stateConfig.OnExit, exitHookCtx, ct);

        // Create step record
        var step = WorkflowStep.Create(
            instance.Id,
            fromState,
            targetState,
            StepType.ConditionalGate,
            "auto-branch",
            actorUserId: null,
            comment: null,
            metadataJson: null);

        context.WorkflowSteps.Add(step);

        // Transition
        instance.TransitionTo(targetState, "auto-branch", actorUserId);

        // Execute onEnter hooks for the new state
        var toStateConfig = states.FirstOrDefault(s => s.Name == targetState);
        if (toStateConfig is not null)
        {
            var enterHookCtx = BuildHookContext(instance, definition.Name, targetState,
                previousState: fromState, action: "auto-branch", actorUserId: actorUserId,
                assigneeUserId: null, assigneeRole: null);
            await hookExecutor.ExecuteAsync(toStateConfig.OnEnter, enterHookCtx, ct);

            await HandleNewStateAsync(instance, definition, states, toStateConfig, actorUserId, ct);
        }
    }

    /// <summary>
    /// After transitioning to a new state, handles the state type:
    /// Terminal -> complete instance, HumanTask -> create approval task,
    /// SystemAction -> auto-transition, ConditionalGate -> evaluate and branch.
    /// Initial states only auto-transition when <paramref name="isStarting"/> is true
    /// (i.e. during StartAsync). After ExecuteTask transitions (e.g. ReturnForRevision),
    /// the requester holds at the Initial state to make changes before resubmitting.
    /// </summary>
    private async Task HandleNewStateAsync(
        WorkflowInstance instance,
        WorkflowDefinition definition,
        List<WorkflowStateConfig> states,
        WorkflowStateConfig toStateConfig,
        Guid actorUserId,
        CancellationToken ct,
        bool isStarting = false)
    {
        if (toStateConfig.Type.Equals("Terminal", StringComparison.OrdinalIgnoreCase))
        {
            instance.Complete();
        }
        else if (toStateConfig.Type.Equals("HumanTask", StringComparison.OrdinalIgnoreCase))
        {
            await CreateApprovalTaskAsync(instance, toStateConfig, definition,
                instance.StartedByUserId, ct);
        }
        else if (toStateConfig.Type.Equals("SystemAction", StringComparison.OrdinalIgnoreCase))
        {
            await AutoTransitionAsync(instance, definition, states, toStateConfig,
                actorUserId, ct);
        }
        else if (toStateConfig.Type.Equals("ConditionalGate", StringComparison.OrdinalIgnoreCase))
        {
            await HandleConditionalGateAsync(instance, definition, states, toStateConfig,
                actorUserId, ct);
        }
        else if (toStateConfig.Type.Equals("Initial", StringComparison.OrdinalIgnoreCase) && isStarting)
        {
            // Auto-transition from Initial states only during StartAsync.
            // When returned via ExecuteTask (e.g. ReturnForRevision), the requester
            // holds at Draft and must explicitly resubmit.
            var transitions = DeserializeTransitions(definition.TransitionsJson);
            var firstTransition = transitions.FirstOrDefault(t =>
                t.From.Equals(toStateConfig.Name, StringComparison.OrdinalIgnoreCase));

            if (firstTransition is not null)
            {
                var step = WorkflowStep.Create(
                    instance.Id, toStateConfig.Name, firstTransition.To,
                    StepType.SystemAction, firstTransition.Trigger, actorUserId, null, null);
                context.WorkflowSteps.Add(step);

                instance.TransitionTo(firstTransition.To, firstTransition.Trigger, actorUserId);

                var nextState = states.FirstOrDefault(s =>
                    s.Name.Equals(firstTransition.To, StringComparison.OrdinalIgnoreCase));

                if (nextState is not null)
                {
                    await HandleNewStateAsync(instance, definition, states, nextState,
                        actorUserId, ct, isStarting: false);
                }
            }
        }
    }

    private static HookContext BuildHookContext(
        WorkflowInstance instance,
        string definitionName,
        string currentState,
        string? previousState,
        string? action,
        Guid? actorUserId,
        Guid? assigneeUserId,
        string? assigneeRole)
        => new(
            InstanceId: instance.Id,
            EntityType: instance.EntityType,
            EntityId: instance.EntityId,
            TenantId: instance.TenantId,
            InitiatorUserId: instance.StartedByUserId,
            CurrentState: currentState,
            PreviousState: previousState,
            Action: action,
            ActorUserId: actorUserId,
            AssigneeUserId: assigneeUserId,
            AssigneeRole: assigneeRole,
            DefinitionName: definitionName);

    private static List<WorkflowStateConfig> DeserializeStates(string json)
        => JsonSerializer.Deserialize<List<WorkflowStateConfig>>(json, JsonOpts) ?? [];

    private static List<WorkflowTransitionConfig> DeserializeTransitions(string json)
        => JsonSerializer.Deserialize<List<WorkflowTransitionConfig>>(json, JsonOpts) ?? [];
}
