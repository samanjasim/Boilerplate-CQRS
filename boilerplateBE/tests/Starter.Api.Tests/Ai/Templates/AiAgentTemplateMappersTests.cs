using Starter.Abstractions.Ai;
using Starter.Abstractions.Capabilities;
using Starter.Module.AI.Application.DTOs;
using Xunit;

namespace Starter.Api.Tests.Ai.Templates;

public class AiAgentTemplateMappersTests
{
    [Fact]
    public void ToDto_maps_all_fields()
    {
        var template = new TestTemplate(
            slug: "x_slug",
            displayName: "X Display",
            systemPrompt: "X prompt",
            provider: AiProviderType.OpenAI,
            model: "gpt-4o-mini",
            tools: new[] { "tool_a" },
            personas: new[] { "default", "anonymous" },
            safetyOverride: SafetyPreset.ChildSafe);

        var reg = new AiAgentTemplateRegistration(template, "Products");
        var dto = ((IAiAgentTemplate)reg).ToDto();

        Assert.Equal("x_slug", dto.Slug);
        Assert.Equal("X Display", dto.DisplayName);
        Assert.Equal("Products", dto.Module);
        Assert.Equal("OpenAI", dto.Provider);
        Assert.Equal("gpt-4o-mini", dto.Model);
        Assert.Equal(new[] { "tool_a" }, dto.EnabledToolNames);
        Assert.Equal(new[] { "default", "anonymous" }, dto.PersonaTargetSlugs);
        Assert.Equal("ChildSafe", dto.SafetyPresetOverride);
    }

    [Fact]
    public void ToDto_uses_unknown_module_when_template_lacks_capability()
    {
        var template = new TestTemplate(slug: "no_module");
        var dto = template.ToDto();

        Assert.Equal("Unknown", dto.Module);
    }

    [Fact]
    public void ToDto_serialises_null_safety_hint_as_null()
    {
        var template = new TestTemplate(slug: "no_safety", safetyOverride: null);
        var dto = template.ToDto();

        Assert.Null(dto.SafetyPresetOverride);
    }
}
