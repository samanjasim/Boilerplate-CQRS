using Starter.Abstractions.Capabilities;

namespace Starter.Infrastructure.Capabilities.NullObjects;

/// <summary>
/// Null implementation of <see cref="ICommentableEntityRegistry"/> registered
/// when the Comments &amp; Activity module is not installed. Returns empty
/// collections so the UI degrades gracefully.
/// </summary>
public sealed class NullCommentableEntityRegistry : ICommentableEntityRegistry
{
    public CommentableEntityDefinition? GetDefinition(string entityType) => null;
    public IReadOnlyList<CommentableEntityDefinition> GetAll() => [];
    public IReadOnlyList<string> GetCommentableTypes() => [];
    public IReadOnlyList<string> GetActivityTypes() => [];
    public bool IsCommentable(string entityType) => false;
    public bool HasActivity(string entityType) => false;
}
