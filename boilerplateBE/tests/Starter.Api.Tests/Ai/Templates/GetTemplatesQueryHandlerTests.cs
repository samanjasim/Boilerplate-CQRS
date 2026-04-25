using Starter.Abstractions.Capabilities;
using Starter.Module.AI.Application.Queries.GetTemplates;
using Starter.Module.AI.Application.Services;
using Xunit;

namespace Starter.Api.Tests.Ai.Templates;

public class GetTemplatesQueryHandlerTests
{
    [Fact]
    public async Task Handler_returns_all_templates_ordered_with_module_field()
    {
        var t1 = new AiAgentTemplateRegistration(new TestTemplate(slug: "z"), "Products");
        var t2 = new AiAgentTemplateRegistration(new TestTemplate(slug: "a"), "Core");
        var registry = new AiAgentTemplateRegistry(new IAiAgentTemplate[] { t1, t2 });

        var handler = new GetTemplatesQueryHandler(registry);
        var result = await handler.Handle(new GetTemplatesQuery(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var list = result.Value!;
        // TestTemplate.Category is "TestCat" for both — ordered by slug
        Assert.Equal(new[] { "a", "z" }, list.Select(d => d.Slug));
        Assert.Equal("Core", list.First(d => d.Slug == "a").Module);
        Assert.Equal("Products", list.First(d => d.Slug == "z").Module);
    }

    [Fact]
    public async Task Handler_returns_empty_when_no_templates_registered()
    {
        var registry = new AiAgentTemplateRegistry(Array.Empty<IAiAgentTemplate>());
        var handler = new GetTemplatesQueryHandler(registry);

        var result = await handler.Handle(new GetTemplatesQuery(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value!);
    }
}
