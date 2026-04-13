namespace Starter.Abstractions.Capabilities;

/// <summary>
/// Describes an entity type that supports comments and/or activity tracking.
/// Registered at startup via <see cref="ICommentableEntityRegistry"/>.
/// </summary>
public sealed record CommentableEntityDefinition(
    string EntityType,
    string DisplayNameKey,
    bool EnableComments,
    bool EnableActivity,
    string[] CustomActivityTypes,
    bool AutoWatchOnCreate,
    bool AutoWatchOnComment);
