namespace Starter.Abstractions.Capabilities;

/// <summary>
/// Resolves the owning tenant of an entity instance. Used by the
/// Comments &amp; Activity module (and any other consumer of
/// <see cref="CommentableEntityDefinition"/>) to determine the effective
/// tenant for platform-admin flows, background jobs, and webhooks where
/// <c>ICurrentUserService.TenantId</c> is not trustworthy.
///
/// Register as scoped in your module's <c>ConfigureServices</c>, then wire it
/// via <c>CommentableEntityBuilder.UseTenantResolver&lt;TResolver&gt;()</c>.
/// The delegate form on <see cref="CommentableEntityDefinition.ResolveTenantIdAsync"/>
/// remains available for simple cases.
/// </summary>
public interface ITenantResolver
{
    /// <summary>
    /// Returns the owning tenant id for the given entity id, or
    /// <c>null</c> when the entity is not tenant-scoped or does not exist.
    /// </summary>
    Task<Guid?> ResolveTenantIdAsync(Guid entityId, CancellationToken ct);
}
