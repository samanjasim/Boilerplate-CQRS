using Starter.Abstractions.Ai;
using Starter.Abstractions.Capabilities;
using Xunit;

namespace Starter.Api.Tests.Ai.Templates;

public class AiAgentTemplateRegistrationTests
{
    [Fact]
    public void Registration_exposes_module_source_and_delegates_template_properties()
    {
        var inner = new TestTemplate(slug: "x", display: "X");
        var reg = new AiAgentTemplateRegistration(inner, "Products");

        Assert.Equal("Products", ((IAiAgentTemplateModuleSource)reg).ModuleSource);
        Assert.Equal("x", ((IAiAgentTemplate)reg).Slug);
        Assert.Equal("X", ((IAiAgentTemplate)reg).DisplayName);
        Assert.Equal(inner.SystemPrompt, ((IAiAgentTemplate)reg).SystemPrompt);
        Assert.Same(inner.EnabledToolNames, ((IAiAgentTemplate)reg).EnabledToolNames);
    }

    private sealed class TestTemplate(string slug, string display) : IAiAgentTemplate
    {
        public string Slug { get; } = slug;
        public string DisplayName { get; } = display;
        public string Description => "test";
        public string Category => "Test";
        public string SystemPrompt => "You are a test.";
        public AiProviderType Provider => AiProviderType.Anthropic;
        public string Model => "test-model";
        public double Temperature => 0.5;
        public int MaxTokens => 512;
        public AssistantExecutionMode ExecutionMode => AssistantExecutionMode.Chat;
        public IReadOnlyList<string> EnabledToolNames { get; } = new[] { "fixture_tool" };
        public IReadOnlyList<string> PersonaTargetSlugs { get; } = new[] { "default" };
        public SafetyPreset? SafetyPresetHint => SafetyPreset.Standard;
    }
}
