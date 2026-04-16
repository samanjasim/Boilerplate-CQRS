namespace Starter.Abstractions.Capabilities;

/// <summary>
/// Records and queries activity log entries on any entity that has activity
/// tracking enabled. Implemented by the Comments &amp; Activity module; when the
/// module is not installed, a silent no-op Null Object is registered in core.
/// </summary>
public interface IActivityService : ICapability
{
    Task RecordAsync(
        string entityType,
        Guid entityId,
        Guid? tenantId,
        string action,
        Guid? actorId,
        string? metadataJson = null,
        string? description = null,
        CancellationToken ct = default);

    Task<IReadOnlyList<ActivitySummary>> GetActivityAsync(
        string entityType,
        Guid entityId,
        int pageNumber = 1,
        int pageSize = 50,
        CancellationToken ct = default);
}
