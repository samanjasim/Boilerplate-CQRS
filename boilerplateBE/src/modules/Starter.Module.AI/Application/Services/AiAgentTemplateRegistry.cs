using Starter.Abstractions.Capabilities;

namespace Starter.Module.AI.Application.Services;

internal sealed class AiAgentTemplateRegistry : IAiAgentTemplateRegistry
{
    private readonly IReadOnlyList<IAiAgentTemplate> _ordered;
    private readonly IReadOnlyDictionary<string, IAiAgentTemplate> _bySlug;

    public AiAgentTemplateRegistry(IEnumerable<IAiAgentTemplate> templates)
    {
        _bySlug = BuildDictionary(templates);
        _ordered = _bySlug.Values
            .OrderBy(t => t.Category, StringComparer.Ordinal)
            .ThenBy(t => t.Slug, StringComparer.Ordinal)
            .ToList();
    }

    public IReadOnlyCollection<IAiAgentTemplate> GetAll() => _ordered;

    public IAiAgentTemplate? Find(string slug) =>
        _bySlug.TryGetValue(slug, out var t) ? t : null;

    private static Dictionary<string, IAiAgentTemplate> BuildDictionary(
        IEnumerable<IAiAgentTemplate> templates)
    {
        var dict = new Dictionary<string, IAiAgentTemplate>(StringComparer.Ordinal);
        foreach (var t in templates)
        {
            if (!dict.TryAdd(t.Slug, t))
            {
                throw new InvalidOperationException(
                    $"Duplicate AI agent template slug '{t.Slug}': "
                    + $"both {dict[t.Slug].GetType().Name} and {t.GetType().Name} "
                    + "claim the same slug.");
            }
        }
        return dict;
    }
}
