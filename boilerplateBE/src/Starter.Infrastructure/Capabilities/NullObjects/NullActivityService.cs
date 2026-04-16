using Microsoft.Extensions.Logging;
using Starter.Abstractions.Capabilities;

namespace Starter.Infrastructure.Capabilities.NullObjects;

/// <summary>
/// Null implementation of <see cref="IActivityService"/> registered when the
/// Comments &amp; Activity module is not installed. Recording and querying are
/// silent no-ops so callers need no module-awareness.
/// </summary>
public sealed class NullActivityService(ILogger<NullActivityService> logger) : IActivityService
{
    public Task RecordAsync(
        string entityType,
        Guid entityId,
        Guid? tenantId,
        string action,
        Guid? actorId,
        string? metadataJson = null,
        string? description = null,
        CancellationToken ct = default)
    {
        logger.LogDebug(
            "Activity record skipped — Comments module not installed (entity: {EntityType}, id: {EntityId}, action: {Action})",
            entityType, entityId, action);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<ActivitySummary>> GetActivityAsync(
        string entityType,
        Guid entityId,
        int pageNumber = 1,
        int pageSize = 50,
        CancellationToken ct = default)
    {
        logger.LogDebug(
            "Activity list skipped — Comments module not installed (entity: {EntityType}, id: {EntityId})",
            entityType, entityId);
        return Task.FromResult<IReadOnlyList<ActivitySummary>>([]);
    }
}
