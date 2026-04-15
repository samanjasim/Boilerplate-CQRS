using Microsoft.Extensions.DependencyInjection;

namespace Starter.Abstractions.Capabilities;

/// <summary>
/// Fluent DI registration for commentable entities. Preferred over constructing
/// <see cref="CommentableEntityDefinition"/> + <see cref="CommentableEntityRegistration"/>
/// manually, though the record-constructor form remains supported.
/// </summary>
public static class CommentableEntityServiceCollectionExtensions
{
    /// <summary>
    /// Register an entity type that supports comments and/or activity tracking.
    /// See <c>DEVELOPER_GUIDE.md</c> in <c>Starter.Module.CommentsActivity</c>
    /// for the full integration walkthrough.
    /// </summary>
    public static IServiceCollection AddCommentableEntity(
        this IServiceCollection services,
        string entityType,
        Action<CommentableEntityBuilder> configure)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(entityType);
        ArgumentNullException.ThrowIfNull(configure);

        var builder = new CommentableEntityBuilder(entityType);
        configure(builder);
        services.AddSingleton<ICommentableEntityRegistration>(
            new CommentableEntityRegistration(builder.Build()));
        return services;
    }
}

/// <summary>
/// Builder surface for <see cref="CommentableEntityServiceCollectionExtensions.AddCommentableEntity"/>.
/// Property defaults mirror the conventions used by <c>Starter.Module.Products</c>.
/// </summary>
public sealed class CommentableEntityBuilder
{
    private readonly string _entityType;

    public CommentableEntityBuilder(string entityType)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(entityType);
        _entityType = entityType;
        DisplayNameKey = $"commentsActivity.entityTypes.{entityType.ToLowerInvariant()}";
    }

    public string DisplayNameKey { get; set; }
    public bool EnableComments { get; set; } = true;
    public bool EnableActivity { get; set; } = true;
    public string[] CustomActivityTypes { get; set; } = [];
    public bool AutoWatchOnCreate { get; set; } = true;
    public bool AutoWatchOnComment { get; set; } = true;
    public Func<Guid, IServiceProvider, CancellationToken, Task<Guid?>>? ResolveTenantIdAsync { get; set; }

    /// <summary>
    /// Wire an <see cref="ITenantResolver"/> implementation as the tenant
    /// resolver for this entity. The resolver is resolved from a fresh DI
    /// scope on each call (matches the behavior expected by scoped DbContexts
    /// and repositories). Register <typeparamref name="TResolver"/> separately
    /// as scoped in your module's <c>ConfigureServices</c>.
    /// </summary>
    public CommentableEntityBuilder UseTenantResolver<TResolver>()
        where TResolver : class, ITenantResolver
    {
        ResolveTenantIdAsync = async (entityId, sp, ct) =>
        {
            await using var scope = sp.CreateAsyncScope();
            var resolver = scope.ServiceProvider.GetRequiredService<TResolver>();
            return await resolver.ResolveTenantIdAsync(entityId, ct);
        };
        return this;
    }

    internal CommentableEntityDefinition Build() =>
        new(
            _entityType,
            DisplayNameKey,
            EnableComments,
            EnableActivity,
            CustomActivityTypes,
            AutoWatchOnCreate,
            AutoWatchOnComment,
            ResolveTenantIdAsync);
}
