namespace Starter.Abstractions.Capabilities;

/// <summary>
/// Manages per-entity watch subscriptions so users receive notifications
/// when comments or activity occur on entities they care about. Implemented
/// by the Comments &amp; Activity module; when the module is not installed,
/// a silent no-op Null Object is registered in core.
/// </summary>
public interface IEntityWatcherService : ICapability
{
    Task WatchAsync(
        string entityType,
        Guid entityId,
        Guid? tenantId,
        Guid userId,
        WatchReason reason = WatchReason.Explicit,
        CancellationToken ct = default);

    Task UnwatchAsync(
        string entityType,
        Guid entityId,
        Guid userId,
        CancellationToken ct = default);

    Task<bool> IsWatchingAsync(
        string entityType,
        Guid entityId,
        Guid userId,
        CancellationToken ct = default);

    Task<IReadOnlyList<Guid>> GetWatcherUserIdsAsync(
        string entityType,
        Guid entityId,
        CancellationToken ct = default);
}
