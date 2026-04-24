using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Starter.Abstractions.Capabilities;
using Starter.Abstractions.Paging;
using Starter.Abstractions.Readers;
using Starter.Application.Common.Interfaces;
using Starter.Module.Workflow.Domain.Constants;
using Starter.Module.Workflow.Domain.Entities;
using Starter.Module.Workflow.Domain.Enums;
using Starter.Module.Workflow.Domain.Errors;
using Starter.Module.Workflow.Infrastructure.Persistence;
using Starter.Shared.Results;

namespace Starter.Module.Workflow.Infrastructure.Services;

/// <summary>
/// Core state-machine engine implementing <see cref="IWorkflowService"/>.
/// Coordinates persistence, condition evaluation, assignee resolution,
/// hook execution, and cross-module capability calls (comments, activity, user reader).
/// </summary>
internal sealed class WorkflowEngine(
    WorkflowDbContext context,
    HookExecutor hookExecutor,
    ICommentService commentService,
    IUserReader userReader,
    IFormDataValidator formDataValidator,
    HumanTaskFactory humanTaskFactory,
    AutoTransitionEvaluator autoTransitionEvaluator,
    ParallelApprovalCoordinator parallelCoordinator,
    ICurrentUserService currentUserService,
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
        // Resolve with a total ordering so the same call always picks the
        // same row: tenant-scoped definitions beat global templates, and
        // within the same scope the highest Version wins (tie-broken by Id
        // to keep the result deterministic even in a same-version race).
        // A tenant's clone of a template must always take precedence — the
        // clone is where the tenant's customizations live.
        var definition = await context.WorkflowDefinitions
            .Where(d =>
                d.Name == definitionName
                && d.IsActive
                && (d.TenantId == null || d.TenantId == tenantId))
            .OrderBy(d => d.TenantId == null ? 1 : 0)
            .ThenByDescending(d => d.Version)
            .ThenBy(d => d.Id)
            .FirstOrDefaultAsync(ct);

        if (definition is null)
        {
            logger.LogWarning(
                "No active workflow definition '{Name}' found for tenant {TenantId}.",
                definitionName, tenantId);
            return Guid.Empty;
        }

        var states = DeserializeStates(definition.StatesJson);
        var initialState = states.FirstOrDefault(s =>
            s.Type.Equals(WorkflowStateTypes.Initial, StringComparison.OrdinalIgnoreCase))
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

    public async Task<bool> CancelAsync(
        Guid instanceId, string? reason, Guid actorUserId, CancellationToken ct = default)
    {
        var instance = await context.WorkflowInstances
            .FirstOrDefaultAsync(i => i.Id == instanceId, ct);

        if (instance is null || instance.Status != InstanceStatus.Active)
        {
            logger.LogWarning("Cannot cancel instance {InstanceId}: not found or not active.", instanceId);
            return false;
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

        try
        {
            await context.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            logger.LogWarning("Concurrency conflict on cancel for instance {InstanceId}. Another user may have already acted.", instanceId);
            return false;
        }

        logger.LogInformation(
            "Cancelled workflow instance {InstanceId}. Reason: {Reason}",
            instanceId, reason);
        return true;
    }

    // ── Transition (resubmit from Initial state) ──────────────────────────

    public async Task<bool> TransitionAsync(
        Guid instanceId, string trigger, Guid actorUserId, CancellationToken ct = default)
    {
        var instance = await context.WorkflowInstances
            .Include(i => i.Definition)
            .FirstOrDefaultAsync(i => i.Id == instanceId, ct);

        if (instance is null || instance.Status != InstanceStatus.Active) return false;

        // Only the initiator can transition from Initial states
        if (instance.StartedByUserId != actorUserId) return false;

        var states = DeserializeStates(instance.Definition.StatesJson);
        var currentState = states.FirstOrDefault(s => s.Name == instance.CurrentState);
        if (currentState is null || !currentState.Type.Equals(WorkflowStateTypes.Initial, StringComparison.OrdinalIgnoreCase))
        {
            // Idempotent: if already past the Initial state, check whether the
            // instance is at the state the trigger would have transitioned to.
            var allTransitions = DeserializeTransitions(instance.Definition.TransitionsJson);
            var wouldHaveGoneTo = allTransitions
                .Where(t => t.Trigger == trigger)
                .Select(t => t.To)
                .FirstOrDefault();

            if (wouldHaveGoneTo is not null && instance.CurrentState == wouldHaveGoneTo)
            {
                logger.LogDebug("TransitionAsync: instance {InstanceId} already at state '{State}' — idempotent success.", instanceId, wouldHaveGoneTo);
                return true;
            }

            return false;
        }

        var transitions = DeserializeTransitions(instance.Definition.TransitionsJson);
        var transition = transitions.FirstOrDefault(t =>
            t.From == instance.CurrentState && t.Trigger == trigger);
        if (transition is null) return false;

        var fromState = instance.CurrentState;
        var toState = transition.To;
        var toStateConfig = states.FirstOrDefault(s => s.Name == toState);

        // Execute onExit hooks for the current state
        var exitHookCtx = BuildHookContext(instance, instance.Definition.Name, fromState,
            previousState: null, action: trigger, actorUserId: actorUserId,
            assigneeUserId: null, assigneeRole: null);
        await hookExecutor.ExecuteAsync(currentState.OnExit, exitHookCtx, ct);

        // Create step record
        var step = WorkflowStep.Create(
            instance.Id,
            fromState,
            toState,
            StepType.HumanTask,
            trigger,
            actorUserId,
            comment: null,
            metadataJson: null);
        context.WorkflowSteps.Add(step);

        // Transition the instance
        instance.TransitionTo(toState, trigger, actorUserId);

        // Execute onEnter hooks for the new state
        if (toStateConfig is not null)
        {
            var enterHookCtx = BuildHookContext(instance, instance.Definition.Name, toState,
                previousState: fromState, action: trigger, actorUserId: actorUserId,
                assigneeUserId: null, assigneeRole: null);
            await hookExecutor.ExecuteAsync(toStateConfig.OnEnter, enterHookCtx, ct);

            // Handle the new state (HumanTask creates task, SystemAction auto-transitions, etc.)
            await HandleNewStateAsync(instance, instance.Definition, states, toStateConfig,
                actorUserId, ct);
        }

        try
        {
            await context.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            logger.LogWarning("Concurrency conflict on transition for instance {InstanceId}. Another user may have already acted.", instanceId);
            return false;
        }

        logger.LogInformation(
            "TransitionAsync: instance {InstanceId} transitioned from '{From}' to '{To}' via '{Trigger}' by user {Actor}.",
            instance.Id, fromState, toState, trigger, actorUserId);

        return true;
    }

    // ── Task Actions ─────────────────────────────────────────────────────────

    public async Task<WorkflowTaskResult> ExecuteTaskAsync(
        Guid taskId, string action, string? comment,
        Guid actorUserId, Dictionary<string, object>? formData = null,
        CancellationToken ct = default)
    {
        var task = await context.ApprovalTasks
            .Include(t => t.Instance)
                .ThenInclude(i => i.Definition)
            .FirstOrDefaultAsync(t => t.Id == taskId, ct);

        if (task is null)
        {
            logger.LogWarning("Approval task {TaskId} not found.", taskId);
            return ToWorkflowTaskResult(WorkflowErrors.TaskNotFound(taskId));
        }

        // Idempotent: if already completed with the same action, return success
        if (task.Status == Domain.Enums.TaskStatus.Completed && task.Action == action)
        {
            logger.LogDebug("Task {TaskId} already completed with action {Action} — idempotent success.", taskId, action);
            return WorkflowTaskResult.Success();
        }

        if (task.Status != Domain.Enums.TaskStatus.Pending)
        {
            logger.LogWarning("Task {TaskId} is not pending (status: {Status}).", taskId, task.Status);
            return ToWorkflowTaskResult(WorkflowErrors.TaskNotPending(taskId));
        }

        // Authorize the actor. When the task has a specific assignee, require
        // an exact user match. When the task is role-assigned (AssigneeUserId
        // null, AssigneeRole set — produced by role-only strategies or when
        // the resolver found no candidate user), require the actor to hold
        // that role. Without the role check, any tenant member with the
        // generic ActOnTask permission could execute tasks intended for
        // privileged roles.
        if (task.AssigneeUserId.HasValue)
        {
            if (task.AssigneeUserId.Value != actorUserId)
            {
                logger.LogWarning(
                    "Actor {ActorId} is not assigned to task {TaskId} (assigned to {AssigneeId}).",
                    actorUserId, taskId, task.AssigneeUserId);
                return ToWorkflowTaskResult(WorkflowErrors.TaskNotAssignedToUser(taskId, actorUserId));
            }
        }
        else if (!string.IsNullOrEmpty(task.AssigneeRole))
        {
            if (!currentUserService.IsInRole(task.AssigneeRole))
            {
                logger.LogWarning(
                    "Actor {ActorId} does not hold role {Role} required by task {TaskId}.",
                    actorUserId, task.AssigneeRole, taskId);
                return ToWorkflowTaskResult(WorkflowErrors.TaskNotAssignedToUser(taskId, actorUserId));
            }
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
            return ToWorkflowTaskResult(WorkflowErrors.InvalidTransition(instance.CurrentState, action));
        }

        var fromState = instance.CurrentState;
        var fromStateConfig = states.FirstOrDefault(s => s.Name == fromState);

        // Validate form data first so a conditional transition never sees
        // data that was rejected. Failure here short-circuits with no state
        // change and no context merge.
        if (fromStateConfig?.FormFields is { Count: > 0 })
        {
            var fieldErrors = formDataValidator.Validate(fromStateConfig.FormFields, formData);
            if (fieldErrors.Count > 0)
            {
                logger.LogWarning(
                    "Form data validation failed for task {TaskId}: {Errors}",
                    taskId, string.Join(", ", fieldErrors.Select(e => $"{e.FieldName}: {e.Message}")));

                var fieldDict = fieldErrors
                    .GroupBy(e => e.FieldName)
                    .ToDictionary(g => g.Key, g => g.Select(e => e.Message).ToArray());
                return WorkflowTaskResult.ValidationFailure(fieldDict);
            }
        }

        // Build the eval context = persisted context overlaid with the form
        // data just submitted, so a condition like `amount > 10000` can branch
        // on the same action that carried the value. Only keys declared in
        // the current state's FormFields are merged — otherwise a submitter
        // could inject condition inputs (e.g. isManagerApproved=true) that
        // were never part of the form.
        var declaredFieldNames = fromStateConfig?.FormFields?.Select(f => f.Name).ToList();
        var instanceContext = MergeFormDataIntoContext(instance.ContextJson, formData, declaredFieldNames);

        // If multiple transitions match, evaluate conditional ones first;
        // fall back to the first matching transition if nothing selected.
        var selectedTransition = autoTransitionEvaluator.Select(
                matchingTransitions, instance.CurrentState, instanceContext)
            ?? matchingTransitions[0];

        var toState = selectedTransition.To;
        var toStateConfig = states.FirstOrDefault(s => s.Name == toState);

        // ── Step 1: Complete the task (always) ──────────────────────────────
        task.Complete(action, comment, actorUserId);

        // Serialize form data into step metadata for history
        var metadataJson = formData is not null ? JsonSerializer.Serialize(formData, JsonOpts) : null;

        // Persist the merged context so downstream conditions can reference
        // form fields. The merged dict was already built above for transition
        // eval — reuse it instead of re-deserializing.
        if (formData is not null)
        {
            instance.UpdateContext(JsonSerializer.Serialize(instanceContext, JsonOpts));
        }

        // Save the comment via ICommentService if provided
        if (!string.IsNullOrWhiteSpace(comment))
        {
            try
            {
                await commentService.AddCommentAsync(
                    "WorkflowInstance",
                    instance.Id,
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

        // ── Step 2: Check parallel group status ─────────────────────────────
        // If this task belongs to a parallel group, determine whether the group
        // is complete before allowing the workflow to transition.
        if (task.GroupId.HasValue)
        {
            var parallelMode = fromStateConfig?.Parallel?.Mode ?? "AllOf";
            var decision = await parallelCoordinator.EvaluateAsync(task, parallelMode, action, ct);

            if (!decision.ShouldProceed)
            {
                // AllOf: not all siblings done yet — stay at current state.
                var waitStep = WorkflowStep.Create(
                    instance.Id,
                    fromState,
                    fromState, // stays at same state
                    StepType.HumanTask,
                    action,
                    actorUserId,
                    comment,
                    metadataJson);

                context.WorkflowSteps.Add(waitStep);

                try
                {
                    await context.SaveChangesAsync(ct);
                }
                catch (DbUpdateConcurrencyException)
                {
                    logger.LogWarning("Concurrency conflict on task {TaskId}. Another user may have already acted.", taskId);
                    return ToWorkflowTaskResult(WorkflowErrors.Concurrency());
                }

                logger.LogInformation(
                    "Task {TaskId}: completed (parallel AllOf, waiting for siblings). Instance {InstanceId} stays at '{State}'.",
                    taskId, instance.Id, fromState);

                return WorkflowTaskResult.Success();
            }
        }

        // ── Step 3: Transition the workflow ──────────────────────────────────

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

        // Create a WorkflowStep record
        var step = WorkflowStep.Create(
            instance.Id,
            fromState,
            toState,
            StepType.HumanTask,
            action,
            actorUserId,
            comment,
            metadataJson);

        context.WorkflowSteps.Add(step);

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

        try
        {
            await context.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            logger.LogWarning("Concurrency conflict on task {TaskId}. Another user may have already acted.", taskId);
            return ToWorkflowTaskResult(WorkflowErrors.Concurrency());
        }

        logger.LogInformation(
            "Task {TaskId}: transitioned instance {InstanceId} from '{From}' to '{To}' via '{Action}'.",
            taskId, instance.Id, fromState, toState, action);

        return WorkflowTaskResult.Success();
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

        var canResubmit = false;
        if (instance.Status == InstanceStatus.Active)
        {
            try
            {
                var states = DeserializeStates(instance.Definition.StatesJson);
                var currentSt = states.FirstOrDefault(s => s.Name == instance.CurrentState);
                canResubmit = currentSt is not null
                    && currentSt.Type.Equals(WorkflowStateTypes.Initial, StringComparison.OrdinalIgnoreCase);
            }
            catch { /* swallow deserialization errors */ }
        }

        return new WorkflowStatusSummary(
            instance.Id,
            instance.DefinitionId,
            instance.Definition.Name,
            instance.CurrentState,
            instance.Status.ToString(),
            instance.StartedAt,
            instance.StartedByUserId,
            instance.EntityDisplayName,
            canResubmit);
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

    public async Task<PaginatedList<PendingTaskSummary>> GetPendingTasksAsync(
        Guid userId, int pageNumber = 1, int pageSize = 20, CancellationToken ct = default)
    {
        // Single-table query — denormalized columns on ApprovalTask remove the need
        // to JOIN WorkflowInstances and WorkflowDefinitions. See Phase 2b spec.
        var baseQuery = context.ApprovalTasks
            .Where(t => t.Status == Domain.Enums.TaskStatus.Pending
                && (t.AssigneeUserId == userId || t.OriginalAssigneeUserId == userId))
            .OrderByDescending(t => t.CreatedAt);

        var totalCount = await baseQuery.CountAsync(ct);

        var tasks = await baseQuery
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        // Resolve display names for original assignees (delegation source) — batched.
        var originalAssigneeIds = tasks
            .Where(t => t.OriginalAssigneeUserId.HasValue)
            .Select(t => t.OriginalAssigneeUserId!.Value)
            .Distinct()
            .ToList();

        var delegationNameLookup = new Dictionary<Guid, string>();
        if (originalAssigneeIds.Count > 0)
        {
            var users = await userReader.GetManyAsync(originalAssigneeIds, ct);
            foreach (var u in users)
                delegationNameLookup[u.Id] = u.DisplayName;
        }

        // Pre-load parallel group sibling counts to avoid N+1 queries.
        var groupIds = tasks
            .Where(t => t.GroupId.HasValue)
            .Select(t => t.GroupId!.Value)
            .Distinct()
            .ToList();

        var groupCounts = new Dictionary<Guid, (int Total, int Completed)>();
        if (groupIds.Count > 0)
        {
            var siblingTasks = await context.ApprovalTasks
                .Where(t => t.GroupId.HasValue && groupIds.Contains(t.GroupId.Value))
                .Select(t => new { t.GroupId, t.Status })
                .ToListAsync(ct);

            foreach (var group in siblingTasks.GroupBy(t => t.GroupId!.Value))
            {
                groupCounts[group.Key] = (
                    group.Count(),
                    group.Count(t => t.Status == Domain.Enums.TaskStatus.Completed));
            }
        }

        // Identify legacy rows whose denormalized columns were never populated
        // (created before the Phase 2b migration). Fall back to a JOIN for these only.
        // TODO: retire this branch once no pre-Phase-2b pending tasks remain.
        // Check via: SELECT COUNT(*) FROM workflow_approval_tasks
        //            WHERE status='Pending' AND (definition_name IS NULL OR definition_name='');
        var legacyTaskIds = tasks
            .Where(t => string.IsNullOrEmpty(t.DefinitionName))
            .Select(t => t.Id)
            .ToList();

        Dictionary<Guid, LegacyTaskFallback>? legacyLookup = null;
        if (legacyTaskIds.Count > 0)
        {
            var legacyRows = await context.ApprovalTasks
                .Where(t => legacyTaskIds.Contains(t.Id))
                .Include(t => t.Instance)
                    .ThenInclude(i => i.Definition)
                .Select(t => new
                {
                    t.Id,
                    DefinitionName = t.Instance.Definition.Name,
                    t.Instance.EntityType,
                    t.Instance.EntityId,
                    t.Instance.EntityDisplayName,
                    t.Instance.Definition.StatesJson,
                    t.Instance.Definition.TransitionsJson,
                    CurrentState = t.Instance.CurrentState,
                })
                .ToListAsync(ct);

            legacyLookup = legacyRows.ToDictionary(
                r => r.Id,
                r => new LegacyTaskFallback(
                    r.DefinitionName,
                    r.EntityType,
                    r.EntityId,
                    r.EntityDisplayName,
                    r.StatesJson,
                    r.TransitionsJson,
                    r.CurrentState));
        }

        var items = tasks.Select(t =>
        {
            // Resolve denormalized vs legacy values.
            string definitionName;
            string entityType;
            Guid entityId;
            string? entityDisplayName;
            List<string>? availableActions;
            List<FormFieldDefinition>? formFields;
            int? slaReminderAfterHours;

            if (string.IsNullOrEmpty(t.DefinitionName) && legacyLookup is not null
                && legacyLookup.TryGetValue(t.Id, out var legacy))
            {
                definitionName = legacy.DefinitionName;
                entityType = legacy.EntityType;
                entityId = legacy.EntityId;
                entityDisplayName = legacy.EntityDisplayName;
                (availableActions, formFields, slaReminderAfterHours) =
                    DeriveLegacyStateFields(legacy.StatesJson, legacy.TransitionsJson, legacy.CurrentState);
            }
            else
            {
                definitionName = t.DefinitionName;
                entityType = t.EntityType;
                entityId = t.EntityId;
                entityDisplayName = t.EntityDisplayName;
                availableActions = DeserializeAvailableActions(t.AvailableActionsJson);
                formFields = DeserializeFormFields(t.FormFieldsJson);
                slaReminderAfterHours = t.SlaReminderAfterHours;
            }

            // Compute overdue from SLA config.
            bool isOverdue = false;
            int? hoursOverdue = null;
            if (slaReminderAfterHours.HasValue)
            {
                var hours = (int)(DateTime.UtcNow - t.CreatedAt).TotalHours;
                if (hours >= slaReminderAfterHours.Value)
                {
                    isOverdue = true;
                    hoursOverdue = hours - slaReminderAfterHours.Value;
                }
            }

            // Only treat as delegated when the caller is NOT themselves the original assignee —
            // otherwise a pending reassignment returning to the original would render as "me delegated to me".
            var isDelegated = t.OriginalAssigneeUserId.HasValue && t.OriginalAssigneeUserId.Value != userId;
            string? delegatedFromDisplayName = null;
            if (isDelegated && delegationNameLookup.TryGetValue(t.OriginalAssigneeUserId!.Value, out var name))
                delegatedFromDisplayName = name;

            int? parallelTotal = null;
            int? parallelCompleted = null;
            if (t.GroupId.HasValue && groupCounts.TryGetValue(t.GroupId.Value, out var counts))
            {
                parallelTotal = counts.Total;
                parallelCompleted = counts.Completed;
            }

            return new PendingTaskSummary(
                t.Id,
                t.InstanceId,
                definitionName,
                entityType,
                entityId,
                t.StepName,
                t.AssigneeRole,
                t.CreatedAt,
                t.DueDate,
                availableActions,
                entityDisplayName,
                FormFields: formFields,
                GroupId: t.GroupId,
                ParallelTotal: parallelTotal,
                ParallelCompleted: parallelCompleted,
                IsOverdue: isOverdue,
                HoursOverdue: hoursOverdue,
                IsDelegated: isDelegated,
                DelegatedFromDisplayName: delegatedFromDisplayName);
        }).ToList();

        return PaginatedList<PendingTaskSummary>.Create(items, totalCount, pageNumber, pageSize);
    }

    private sealed record LegacyTaskFallback(
        string DefinitionName,
        string EntityType,
        Guid EntityId,
        string? EntityDisplayName,
        string StatesJson,
        string TransitionsJson,
        string CurrentState);

    private List<string>? DeserializeAvailableActions(string? json)
    {
        if (string.IsNullOrEmpty(json)) return null;
        try { return JsonSerializer.Deserialize<List<string>>(json, JsonOpts); }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "Failed to deserialize AvailableActionsJson");
            return null;
        }
    }

    private List<FormFieldDefinition>? DeserializeFormFields(string? json)
    {
        if (string.IsNullOrEmpty(json)) return null;
        try { return JsonSerializer.Deserialize<List<FormFieldDefinition>>(json, JsonOpts); }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "Failed to deserialize FormFieldsJson");
            return null;
        }
    }

    private (List<string>? Actions, List<FormFieldDefinition>? FormFields, int? SlaReminderAfterHours)
        DeriveLegacyStateFields(string statesJson, string transitionsJson, string currentState)
    {
        try
        {
            var transitions = DeserializeTransitions(transitionsJson);
            var actions = transitions
                .Where(tr => tr.From == currentState
                    && tr.Type.Equals("Manual", StringComparison.OrdinalIgnoreCase))
                .Select(tr => tr.Trigger)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var states = DeserializeStates(statesJson);
            var stateConfig = states.FirstOrDefault(s => s.Name == currentState);
            var formFields = stateConfig?.FormFields is { Count: > 0 } ? stateConfig.FormFields : null;
            var sla = stateConfig?.Sla?.ReminderAfterHours;

            return (actions, formFields, sla);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to derive legacy state fields for state {CurrentState}", currentState);
            return (null, null, null);
        }
    }

    public async Task<int> GetPendingTaskCountAsync(
        Guid userId, CancellationToken ct = default)
    {
        return await context.ApprovalTasks
            .CountAsync(t => t.Status == Domain.Enums.TaskStatus.Pending
                && (t.AssigneeUserId == userId || t.OriginalAssigneeUserId == userId), ct);
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

        return steps.Select(s =>
        {
            var metadata = s.MetadataJson is not null
                ? JsonSerializer.Deserialize<Dictionary<string, object>>(s.MetadataJson, JsonOpts)
                : null;

            return new WorkflowStepRecord(
                s.FromState,
                s.ToState,
                s.StepType.ToString(),
                s.Action,
                s.ActorUserId,
                s.ActorUserId.HasValue && userLookup.TryGetValue(s.ActorUserId.Value, out var name)
                    ? name : null,
                s.Comment,
                s.Timestamp,
                metadata,
                FormData: metadata);
        }).ToList();
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

        return instances.Select(i =>
        {
            var canResubmit = false;
            if (i.Status == InstanceStatus.Active)
            {
                try
                {
                    var sts = DeserializeStates(i.Definition.StatesJson);
                    var curSt = sts.FirstOrDefault(s => s.Name == i.CurrentState);
                    canResubmit = curSt is not null
                        && curSt.Type.Equals(WorkflowStateTypes.Initial, StringComparison.OrdinalIgnoreCase);
                }
                catch { /* swallow */ }
            }

            return new WorkflowInstanceSummary(
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
                i.EntityDisplayName,
                canResubmit);
        }).ToList();
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
                d.DisplayName,
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

    private Task CreateApprovalTaskAsync(
        WorkflowInstance instance,
        WorkflowStateConfig stateConfig,
        WorkflowDefinition definition,
        Guid initiatorUserId,
        CancellationToken ct)
        => humanTaskFactory.CreateAsync(instance, stateConfig, definition, initiatorUserId, ct);

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

        // Evaluate conditional transitions first, falling back to the default unconditional one.
        var instanceContext = instance.ContextJson is not null
            ? JsonSerializer.Deserialize<Dictionary<string, object>>(instance.ContextJson, JsonOpts)
            : null;
        var selected = autoTransitionEvaluator.Select(
            autoTransitions, currentStateConfig.Name, instanceContext);

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

        // Evaluate conditional transitions first, falling back to the default unconditional one.
        var selected = autoTransitionEvaluator.Select(transitions, fromState, instanceContext);
        var targetState = selected?.To;

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
        if (toStateConfig.Type.Equals(WorkflowStateTypes.Terminal, StringComparison.OrdinalIgnoreCase))
        {
            instance.Complete();
        }
        else if (toStateConfig.Type.Equals(WorkflowStateTypes.HumanTask, StringComparison.OrdinalIgnoreCase))
        {
            await CreateApprovalTaskAsync(instance, toStateConfig, definition,
                instance.StartedByUserId, ct);
        }
        else if (toStateConfig.Type.Equals(WorkflowStateTypes.SystemAction, StringComparison.OrdinalIgnoreCase))
        {
            await AutoTransitionAsync(instance, definition, states, toStateConfig,
                actorUserId, ct);
        }
        else if (toStateConfig.Type.Equals(WorkflowStateTypes.ConditionalGate, StringComparison.OrdinalIgnoreCase))
        {
            await HandleConditionalGateAsync(instance, definition, states, toStateConfig,
                actorUserId, ct);
        }
        else if (toStateConfig.Type.Equals(WorkflowStateTypes.Initial, StringComparison.OrdinalIgnoreCase) && isStarting)
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
            EntityType: "WorkflowInstance",
            EntityId: instance.Id,
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

    /// <summary>
    /// Deserializes <paramref name="contextJson"/> and overlays
    /// <paramref name="formData"/> on top so conditional transitions can branch
    /// on just-submitted values. Only keys that appear in
    /// <paramref name="declaredFields"/> are merged — undeclared keys are
    /// silently dropped to prevent attacker-supplied form fields from
    /// influencing downstream conditional transitions (e.g. injecting
    /// `isManagerApproved=true` through a submitter-facing form).
    /// </summary>
    private Dictionary<string, object> MergeFormDataIntoContext(
        string? contextJson,
        Dictionary<string, object>? formData,
        IReadOnlyCollection<string>? declaredFields)
    {
        var merged = contextJson is not null
            ? JsonSerializer.Deserialize<Dictionary<string, object>>(contextJson, JsonOpts) ?? new()
            : new Dictionary<string, object>();

        if (formData is null) return merged;

        var allowed = declaredFields is null
            ? new HashSet<string>(StringComparer.Ordinal)
            : new HashSet<string>(declaredFields, StringComparer.Ordinal);

        foreach (var (key, value) in formData)
        {
            if (!allowed.Contains(key))
            {
                logger.LogWarning(
                    "Dropping undeclared form field '{Key}' during task execution — not present in state form definition.",
                    key);
                continue;
            }
            if (merged.ContainsKey(key))
            {
                logger.LogDebug(
                    "Form field '{Key}' overwrites existing context value during task execution.",
                    key);
            }
            merged[key] = value;
        }

        return merged;
    }

    private static WorkflowTaskResult ToWorkflowTaskResult(Error error)
        => WorkflowTaskResult.Failure(error.Code, error.Description, MapKind(error.Type));

    private static WorkflowErrorKind MapKind(ErrorType type) => type switch
    {
        ErrorType.Validation => WorkflowErrorKind.Validation,
        ErrorType.NotFound => WorkflowErrorKind.NotFound,
        ErrorType.Conflict => WorkflowErrorKind.Conflict,
        ErrorType.Unauthorized => WorkflowErrorKind.Unauthorized,
        ErrorType.Forbidden => WorkflowErrorKind.Forbidden,
        _ => WorkflowErrorKind.Failure,
    };
}
