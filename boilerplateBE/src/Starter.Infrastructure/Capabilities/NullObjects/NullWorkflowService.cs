using Microsoft.Extensions.Logging;
using Starter.Abstractions.Capabilities;
using Starter.Abstractions.Paging;

namespace Starter.Infrastructure.Capabilities.NullObjects;

/// <summary>
/// Null implementation of <see cref="IWorkflowService"/> registered when the
/// Workflow module is not installed. All operations are silent no-ops so
/// callers (command handlers, domain services) need no module-awareness.
/// </summary>
public sealed class NullWorkflowService(ILogger<NullWorkflowService> logger) : IWorkflowService
{
    public Task<Guid> StartAsync(
        string entityType,
        Guid entityId,
        string definitionName,
        Guid initiatorUserId,
        Guid? tenantId,
        string? entityDisplayName = null,
        CancellationToken ct = default)
    {
        logger.LogDebug(
            "Workflow start skipped — Workflow module not installed (entityType: {EntityType}, entityId: {EntityId}, definition: {DefinitionName})",
            entityType, entityId, definitionName);
        return Task.FromResult(Guid.Empty);
    }

    public Task CancelAsync(
        Guid instanceId,
        string? reason,
        Guid actorUserId,
        CancellationToken ct = default)
    {
        logger.LogDebug(
            "Workflow cancel skipped — Workflow module not installed (instanceId: {InstanceId})",
            instanceId);
        return Task.CompletedTask;
    }

    public Task<bool> ExecuteTaskAsync(
        Guid taskId,
        string action,
        string? comment,
        Guid actorUserId,
        Dictionary<string, object>? formData = null,
        CancellationToken ct = default)
    {
        logger.LogDebug(
            "Workflow task execution skipped — Workflow module not installed (taskId: {TaskId}, action: {Action})",
            taskId, action);
        return Task.FromResult(false);
    }

    public Task<bool> TransitionAsync(
        Guid instanceId,
        string trigger,
        Guid actorUserId,
        CancellationToken ct = default)
    {
        logger.LogDebug(
            "Workflow transition skipped — Workflow module not installed (instanceId: {InstanceId}, trigger: {Trigger})",
            instanceId, trigger);
        return Task.FromResult(false);
    }

    public Task<WorkflowStatusSummary?> GetStatusAsync(
        string entityType,
        Guid entityId,
        CancellationToken ct = default)
    {
        logger.LogDebug(
            "Workflow status query skipped — Workflow module not installed (entityType: {EntityType}, entityId: {EntityId})",
            entityType, entityId);
        return Task.FromResult<WorkflowStatusSummary?>(null);
    }

    public Task<bool> IsInStateAsync(
        string entityType,
        Guid entityId,
        string stateName,
        CancellationToken ct = default)
    {
        logger.LogDebug(
            "Workflow state check skipped — Workflow module not installed (entityType: {EntityType}, entityId: {EntityId}, state: {StateName})",
            entityType, entityId, stateName);
        return Task.FromResult(false);
    }

    public Task<PagedResult<PendingTaskSummary>> GetPendingTasksAsync(
        Guid userId,
        int pageNumber = 1,
        int pageSize = 20,
        CancellationToken ct = default)
    {
        logger.LogDebug(
            "Workflow pending tasks query skipped — Workflow module not installed (userId: {UserId})",
            userId);
        return Task.FromResult(new PagedResult<PendingTaskSummary>(
            Array.Empty<PendingTaskSummary>(), totalCount: 0, pageNumber, pageSize));
    }

    public Task<int> GetPendingTaskCountAsync(
        Guid userId,
        CancellationToken ct = default)
    {
        logger.LogDebug(
            "Workflow pending task count query skipped — Workflow module not installed (userId: {UserId})",
            userId);
        return Task.FromResult(0);
    }

    public Task<IReadOnlyList<WorkflowStepRecord>> GetHistoryAsync(
        Guid instanceId,
        CancellationToken ct = default)
    {
        logger.LogDebug(
            "Workflow history query skipped — Workflow module not installed (instanceId: {InstanceId})",
            instanceId);
        return Task.FromResult<IReadOnlyList<WorkflowStepRecord>>(new List<WorkflowStepRecord>());
    }

    public Task<IReadOnlyList<WorkflowInstanceSummary>> GetInstancesAsync(
        string? entityType = null,
        string? state = null,
        Guid? startedByUserId = null,
        string? status = null,
        int page = 1,
        int pageSize = 20,
        CancellationToken ct = default)
    {
        logger.LogDebug(
            "Workflow instances query skipped — Workflow module not installed (entityType: {EntityType})",
            entityType);
        return Task.FromResult<IReadOnlyList<WorkflowInstanceSummary>>(new List<WorkflowInstanceSummary>());
    }

    public Task<IReadOnlyList<WorkflowDefinitionSummary>> GetDefinitionsAsync(
        string? entityType = null,
        Guid? tenantId = null,
        CancellationToken ct = default)
    {
        logger.LogDebug(
            "Workflow definitions query skipped — Workflow module not installed (entityType: {EntityType})",
            entityType);
        return Task.FromResult<IReadOnlyList<WorkflowDefinitionSummary>>(new List<WorkflowDefinitionSummary>());
    }

    public Task<WorkflowDefinitionDetail?> GetDefinitionAsync(
        Guid definitionId,
        CancellationToken ct = default)
    {
        logger.LogDebug(
            "Workflow definition query skipped — Workflow module not installed (definitionId: {DefinitionId})",
            definitionId);
        return Task.FromResult<WorkflowDefinitionDetail?>(null);
    }

    public Task SeedTemplateAsync(
        string name,
        string entityType,
        WorkflowTemplateConfig config,
        CancellationToken ct = default)
    {
        logger.LogDebug(
            "Template seeding skipped — Workflow module not installed (name: {TemplateName}, entityType: {EntityType})",
            name, entityType);
        return Task.CompletedTask;
    }
}
