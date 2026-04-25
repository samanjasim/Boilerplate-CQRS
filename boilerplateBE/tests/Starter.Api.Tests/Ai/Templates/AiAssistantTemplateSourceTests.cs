using Starter.Abstractions.Ai;
using Starter.Module.AI.Domain.Entities;
using Xunit;

namespace Starter.Api.Tests.Ai.Templates;

public class AiAssistantTemplateSourceTests
{
    [Fact]
    public void New_assistant_has_null_template_source_fields_by_default()
    {
        var a = AiAssistant.Create(
            tenantId: Guid.NewGuid(),
            name: "X",
            description: null,
            systemPrompt: "You are X.",
            createdByUserId: Guid.NewGuid());

        Assert.Null(a.TemplateSourceSlug);
        Assert.Null(a.TemplateSourceVersion);
    }

    [Fact]
    public void StampTemplateSource_sets_slug_and_version()
    {
        var a = AiAssistant.Create(
            tenantId: Guid.NewGuid(),
            name: "X",
            description: null,
            systemPrompt: "You are X.",
            createdByUserId: Guid.NewGuid());

        a.StampTemplateSource("support_assistant_anthropic", version: null);

        Assert.Equal("support_assistant_anthropic", a.TemplateSourceSlug);
        Assert.Null(a.TemplateSourceVersion);
    }

    [Fact]
    public void StampTemplateSource_accepts_version_when_provided()
    {
        var a = AiAssistant.Create(
            tenantId: Guid.NewGuid(),
            name: "X",
            description: null,
            systemPrompt: "You are X.",
            createdByUserId: Guid.NewGuid());

        a.StampTemplateSource("support_assistant_anthropic", version: "v1");

        Assert.Equal("v1", a.TemplateSourceVersion);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void StampTemplateSource_rejects_empty_or_whitespace_slug(string slug)
    {
        var a = AiAssistant.Create(
            tenantId: Guid.NewGuid(),
            name: "X",
            description: null,
            systemPrompt: "You are X.",
            createdByUserId: Guid.NewGuid());

        var ex = Assert.Throws<ArgumentException>(() =>
            a.StampTemplateSource(slug, version: null));
        Assert.Equal("templateSlug", ex.ParamName);
    }
}
