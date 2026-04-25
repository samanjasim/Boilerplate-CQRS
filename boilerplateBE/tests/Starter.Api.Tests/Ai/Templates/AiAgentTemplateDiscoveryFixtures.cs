using Starter.Abstractions.Ai;
using Starter.Abstractions.Capabilities;

namespace Starter.Api.Tests.Ai.Templates;

internal sealed class FixtureTemplateA : IAiAgentTemplate
{
    public string Slug => "fixture_a";
    public string DisplayName => "Fixture A";
    public string Description => "First fixture template.";
    public string Category => "FixtureCat";
    public string SystemPrompt => "You are fixture A.";
    public AiProviderType Provider => AiProviderType.Anthropic;
    public string Model => "claude-sonnet-4-20250514";
    public double Temperature => 0.3;
    public int MaxTokens => 1024;
    public AssistantExecutionMode ExecutionMode => AssistantExecutionMode.Chat;
    public IReadOnlyList<string> EnabledToolNames { get; } = new[] { "fixture_tool" };
    public IReadOnlyList<string> PersonaTargetSlugs { get; } = new[] { "default" };
    public SafetyPreset? SafetyPresetHint => SafetyPreset.Standard;
}

internal sealed class FixtureTemplateB : IAiAgentTemplate
{
    public string Slug => "fixture_b";
    public string DisplayName => "Fixture B";
    public string Description => "Second fixture template.";
    public string Category => "FixtureCat";
    public string SystemPrompt => "You are fixture B.";
    public AiProviderType Provider => AiProviderType.OpenAI;
    public string Model => "gpt-4o-mini";
    public double Temperature => 0.4;
    public int MaxTokens => 2048;
    public AssistantExecutionMode ExecutionMode => AssistantExecutionMode.Chat;
    public IReadOnlyList<string> EnabledToolNames { get; } = Array.Empty<string>();
    public IReadOnlyList<string> PersonaTargetSlugs { get; } = new[] { "anonymous" };
    public SafetyPreset? SafetyPresetHint => null;
}

// Skipped by scanner: abstract.
internal abstract class AbstractFixtureTemplate : IAiAgentTemplate
{
    public abstract string Slug { get; }
    public string DisplayName => "abstract";
    public string Description => "abstract";
    public string Category => "abstract";
    public string SystemPrompt => "abstract";
    public AiProviderType Provider => AiProviderType.Anthropic;
    public string Model => "x";
    public double Temperature => 0.5;
    public int MaxTokens => 1;
    public AssistantExecutionMode ExecutionMode => AssistantExecutionMode.Chat;
    public IReadOnlyList<string> EnabledToolNames { get; } = Array.Empty<string>();
    public IReadOnlyList<string> PersonaTargetSlugs { get; } = Array.Empty<string>();
    public SafetyPreset? SafetyPresetHint => null;
}

// Skipped by scanner: no parameterless ctor.
internal sealed class NoParameterlessCtorFixtureTemplate(string slugValue) : IAiAgentTemplate
{
    public string Slug { get; } = slugValue;
    public string DisplayName => "no-ctor";
    public string Description => "no-ctor";
    public string Category => "no-ctor";
    public string SystemPrompt => "no-ctor";
    public AiProviderType Provider => AiProviderType.Anthropic;
    public string Model => "x";
    public double Temperature => 0.5;
    public int MaxTokens => 1;
    public AssistantExecutionMode ExecutionMode => AssistantExecutionMode.Chat;
    public IReadOnlyList<string> EnabledToolNames { get; } = Array.Empty<string>();
    public IReadOnlyList<string> PersonaTargetSlugs { get; } = Array.Empty<string>();
    public SafetyPreset? SafetyPresetHint => null;
}

// Test-only mutable helper — used in handler/registry tests in later tasks.
// Skipped by the scanner because it has no parameterless ctor (the primary ctor
// has parameters — even with all-defaulted, GetConstructor(Type.EmptyTypes) returns null).
internal sealed class TestTemplate(
    string slug = "test",
    string? displayName = null,
    string? systemPrompt = null,
    string? model = null,
    AiProviderType provider = AiProviderType.Anthropic,
    IReadOnlyList<string>? tools = null,
    IReadOnlyList<string>? personas = null,
    SafetyPreset? safetyHint = null) : IAiAgentTemplate
{
    public string Slug { get; } = slug;
    public string DisplayName { get; } = displayName ?? slug;
    public string Description => "test";
    public string Category => "TestCat";
    public string SystemPrompt { get; } = systemPrompt ?? "You are a test.";
    public AiProviderType Provider { get; } = provider;
    public string Model { get; } = model ?? "test-model";
    public double Temperature => 0.5;
    public int MaxTokens => 512;
    public AssistantExecutionMode ExecutionMode => AssistantExecutionMode.Chat;
    public IReadOnlyList<string> EnabledToolNames { get; } = tools ?? Array.Empty<string>();
    public IReadOnlyList<string> PersonaTargetSlugs { get; } = personas ?? Array.Empty<string>();
    public SafetyPreset? SafetyPresetHint { get; } = safetyHint;
}
