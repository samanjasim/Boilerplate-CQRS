namespace Starter.Abstractions.Capabilities;

/// <summary>
/// Composable state-machine workflow engine. Modules use this to start, query,
/// and manage workflows on their entities. When the Workflow module is not
/// installed, <c>NullWorkflowService</c> silently returns empty results.
/// </summary>
public interface IWorkflowService : ICapability
{
    // ── Lifecycle ──
    Task<Guid> StartAsync(string entityType, Guid entityId, string definitionName,
        Guid initiatorUserId, Guid? tenantId, CancellationToken ct = default);
    Task CancelAsync(Guid instanceId, string? reason, Guid actorUserId,
        CancellationToken ct = default);

    // ── Task Actions ──
    Task<bool> ExecuteTaskAsync(Guid taskId, string action, string? comment,
        Guid actorUserId, CancellationToken ct = default);

    // ── Query: Status ──
    Task<WorkflowStatusSummary?> GetStatusAsync(string entityType, Guid entityId,
        CancellationToken ct = default);
    Task<bool> IsInStateAsync(string entityType, Guid entityId, string stateName,
        CancellationToken ct = default);

    // ── Query: Inbox ──
    Task<IReadOnlyList<PendingTaskSummary>> GetPendingTasksAsync(Guid userId,
        CancellationToken ct = default);
    Task<int> GetPendingTaskCountAsync(Guid userId, CancellationToken ct = default);

    // ── Query: History (AI, Reporting, Audit) ──
    Task<IReadOnlyList<WorkflowStepRecord>> GetHistoryAsync(Guid instanceId,
        CancellationToken ct = default);
    Task<IReadOnlyList<WorkflowInstanceSummary>> GetInstancesAsync(string entityType,
        string? state = null, Guid? startedByUserId = null, string? status = null,
        int page = 1, int pageSize = 20,
        CancellationToken ct = default);

    // ── Query: Definitions (AI tool discovery, admin UI) ──
    Task<IReadOnlyList<WorkflowDefinitionSummary>> GetDefinitionsAsync(
        string? entityType = null, Guid? tenantId = null,
        CancellationToken ct = default);
    Task<WorkflowDefinitionDetail?> GetDefinitionAsync(Guid definitionId,
        CancellationToken ct = default);

    // ── Template Seeding ──
    Task SeedTemplateAsync(string name, string entityType,
        WorkflowTemplateConfig config, CancellationToken ct = default);
}
