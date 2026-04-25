using Starter.Abstractions.Capabilities;
using Starter.Module.AI.Application.Services;
using Xunit;

namespace Starter.Api.Tests.Ai.Templates;

public class AiAgentTemplateRegistryTests
{
    [Fact]
    public void GetAll_returns_all_templates_sorted_by_category_then_slug()
    {
        var registry = new AiAgentTemplateRegistry(new IAiAgentTemplate[]
        {
            new TestTemplate(slug: "z_template"),
            new TestTemplate(slug: "a_template"),
            new TestTemplate(slug: "m_template"),
        });

        var all = registry.GetAll().Select(t => t.Slug).ToList();

        Assert.Equal(new[] { "a_template", "m_template", "z_template" }, all);
    }

    [Fact]
    public void Find_returns_matching_template()
    {
        var t = new TestTemplate(slug: "found");
        var registry = new AiAgentTemplateRegistry(new IAiAgentTemplate[] { t });

        Assert.Same(t, registry.Find("found"));
    }

    [Fact]
    public void Find_returns_null_for_unknown_slug()
    {
        var registry = new AiAgentTemplateRegistry(new IAiAgentTemplate[]
        {
            new TestTemplate(slug: "x"),
        });

        Assert.Null(registry.Find("missing"));
    }

    [Fact]
    public void Constructor_throws_on_duplicate_slug_naming_both_types()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => new AiAgentTemplateRegistry(
            new IAiAgentTemplate[]
            {
                new FixtureTemplateA(),
                new TestTemplate(slug: "fixture_a"),  // collides with FixtureTemplateA
            }));

        Assert.Contains("fixture_a", ex.Message);
        Assert.Contains(nameof(FixtureTemplateA), ex.Message);
        Assert.Contains(nameof(TestTemplate), ex.Message);
    }
}
