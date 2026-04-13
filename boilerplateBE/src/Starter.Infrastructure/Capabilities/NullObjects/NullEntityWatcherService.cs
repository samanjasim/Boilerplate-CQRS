using Microsoft.Extensions.Logging;
using Starter.Abstractions.Capabilities;

namespace Starter.Infrastructure.Capabilities.NullObjects;

/// <summary>
/// Null implementation of <see cref="IEntityWatcherService"/> registered when
/// the Comments &amp; Activity module is not installed. All operations are
/// silent no-ops so callers need no module-awareness.
/// </summary>
public sealed class NullEntityWatcherService(ILogger<NullEntityWatcherService> logger) : IEntityWatcherService
{
    public Task WatchAsync(
        string entityType,
        Guid entityId,
        Guid? tenantId,
        Guid userId,
        WatchReason reason = WatchReason.Explicit,
        CancellationToken ct = default)
    {
        logger.LogDebug(
            "Watch skipped — Comments module not installed (entity: {EntityType}, id: {EntityId}, user: {UserId})",
            entityType, entityId, userId);
        return Task.CompletedTask;
    }

    public Task UnwatchAsync(
        string entityType,
        Guid entityId,
        Guid userId,
        CancellationToken ct = default)
    {
        logger.LogDebug(
            "Unwatch skipped — Comments module not installed (entity: {EntityType}, id: {EntityId}, user: {UserId})",
            entityType, entityId, userId);
        return Task.CompletedTask;
    }

    public Task<bool> IsWatchingAsync(
        string entityType,
        Guid entityId,
        Guid userId,
        CancellationToken ct = default)
    {
        logger.LogDebug(
            "Watch check skipped — Comments module not installed (entity: {EntityType}, id: {EntityId}, user: {UserId})",
            entityType, entityId, userId);
        return Task.FromResult(false);
    }

    public Task<IReadOnlyList<Guid>> GetWatcherUserIdsAsync(
        string entityType,
        Guid entityId,
        CancellationToken ct = default)
    {
        logger.LogDebug(
            "Watcher list skipped — Comments module not installed (entity: {EntityType}, id: {EntityId})",
            entityType, entityId);
        return Task.FromResult<IReadOnlyList<Guid>>([]);
    }
}
