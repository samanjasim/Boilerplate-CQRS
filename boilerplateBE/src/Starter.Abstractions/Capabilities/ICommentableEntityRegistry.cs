namespace Starter.Abstractions.Capabilities;

/// <summary>
/// Registry of entity types that support comments and/or activity tracking.
/// The Comments &amp; Activity module populates this at startup; when the module
/// is not installed, a Null Object registered in core returns empty
/// collections so the UI degrades gracefully.
/// </summary>
public interface ICommentableEntityRegistry : ICapability
{
    CommentableEntityDefinition? GetDefinition(string entityType);
    IReadOnlyList<CommentableEntityDefinition> GetAll();
    IReadOnlyList<string> GetCommentableTypes();
    IReadOnlyList<string> GetActivityTypes();
    bool IsCommentable(string entityType);
    bool HasActivity(string entityType);
}
