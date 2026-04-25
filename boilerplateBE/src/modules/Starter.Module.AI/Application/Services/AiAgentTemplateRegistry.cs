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
        // Template slugs are case-sensitive (Ordinal). Authors register slugs in
        // lower_snake_case as the canonical identity; downstream consumers
        // (`AiAssistant.Slug`, install commands, persona PermittedAgentSlugs)
        // store and compare the slug exactly as authored. This differs from
        // 5c-1's tool registry, which uses OrdinalIgnoreCase to tolerate
        // model-generated tool calls that may vary in casing.
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
