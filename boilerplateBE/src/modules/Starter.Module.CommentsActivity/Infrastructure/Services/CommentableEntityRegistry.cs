using Starter.Abstractions.Capabilities;

namespace Starter.Module.CommentsActivity.Infrastructure.Services;

public sealed class CommentableEntityRegistry : ICommentableEntityRegistry
{
    private readonly Dictionary<string, CommentableEntityDefinition> _definitions;

    public CommentableEntityRegistry(IEnumerable<ICommentableEntityRegistration> registrations)
    {
        _definitions = registrations
            .ToDictionary(r => r.Definition.EntityType, r => r.Definition, StringComparer.OrdinalIgnoreCase);
    }

    public CommentableEntityDefinition? GetDefinition(string entityType) =>
        _definitions.GetValueOrDefault(entityType);

    public IReadOnlyList<CommentableEntityDefinition> GetAll() =>
        _definitions.Values.ToList().AsReadOnly();

    public IReadOnlyList<string> GetCommentableTypes() =>
        _definitions.Values
            .Where(d => d.EnableComments)
            .Select(d => d.EntityType)
            .ToList()
            .AsReadOnly();

    public IReadOnlyList<string> GetActivityTypes() =>
        _definitions.Values
            .Where(d => d.EnableActivity)
            .Select(d => d.EntityType)
            .ToList()
            .AsReadOnly();

    public bool IsCommentable(string entityType) =>
        _definitions.TryGetValue(entityType, out var def) && def.EnableComments;

    public bool HasActivity(string entityType) =>
        _definitions.TryGetValue(entityType, out var def) && def.EnableActivity;
}
