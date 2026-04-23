using Microsoft.Extensions.Logging;
using Starter.Abstractions.Capabilities;

namespace Starter.Module.CommentsActivity.Infrastructure.Services;

/// <summary>
/// Reconciles a caller-supplied <c>tenantId</c> against the tenant registered
/// for an entity via <see cref="CommentableEntityDefinition.ResolveTenantIdAsync"/>.
/// Centralises the server-enforcement rule used by capability-layer writes —
/// HTTP commands already derive tenantId from <c>ICurrentUserService.TenantId</c>
/// and do not need this hook.
/// </summary>
internal static class TenantResolution
{
    /// <summary>
    /// Returns the tenant that should be persisted against the entity.
    /// When the entity type has no <c>ResolveTenantIdAsync</c> registered,
    /// the caller-supplied value is preserved (back-compat for untenanted
    /// entities). When both are set and disagree, the resolved value wins
    /// and a warning is logged so cross-tenant write attempts stay visible.
    /// </summary>
    public static async Task<Guid?> ResolveEffectiveTenantIdAsync(
        ICommentableEntityRegistry registry,
        IServiceProvider services,
        ILogger logger,
        string entityType,
        Guid entityId,
        Guid? callerTenantId,
        CancellationToken ct)
    {
        var definition = registry.GetDefinition(entityType);
        if (definition?.ResolveTenantIdAsync is null)
            return callerTenantId;

        var resolved = await definition.ResolveTenantIdAsync(entityId, services, ct);

        if (callerTenantId.HasValue && resolved.HasValue && callerTenantId != resolved)
        {
            logger.LogWarning(
                "Tenant override on {EntityType}/{EntityId}: caller supplied {CallerTenantId} but entity is owned by {ResolvedTenantId}. Persisting as {ResolvedTenantId}.",
                entityType, entityId, callerTenantId, resolved, resolved);
        }

        return resolved ?? callerTenantId;
    }
}
