namespace Starter.Abstractions.Capabilities;

/// <summary>
/// Describes an entity type that supports comments and/or activity tracking.
/// Registered at startup via <see cref="ICommentableEntityRegistry"/>.
/// </summary>
/// <param name="ResolveTenantIdAsync">
/// Optional resolver that returns the owning tenant id for a given entity instance.
/// Used by mention scoping so platform admins see tenant-scoped users of the entity
/// they're commenting on. Return <c>null</c> when the entity is not tenant-owned.
/// </param>
public sealed record CommentableEntityDefinition(
    string EntityType,
    string DisplayNameKey,
    bool EnableComments,
    bool EnableActivity,
    string[] CustomActivityTypes,
    bool AutoWatchOnCreate,
    bool AutoWatchOnComment,
    Func<Guid, IServiceProvider, CancellationToken, Task<Guid?>>? ResolveTenantIdAsync = null);
