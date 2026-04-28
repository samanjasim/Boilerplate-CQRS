using Starter.Abstractions.Ai;
using Starter.Abstractions.Capabilities;

namespace Starter.Application.Features.Ai.Templates;

public sealed class BrandContentTemplate : IAiAgentTemplate
{
    public string Slug => "brand_content";
    public string DisplayName => "Brand Content Agent";
    public string Description => BrandContentPrompts.Description;
    public string Category => "Content";
    public string SystemPrompt => BrandContentPrompts.SystemPrompt;
    public AiProviderType Provider => AiProviderType.Anthropic;
    public string Model => "claude-sonnet-4-20250514";
    public double Temperature => 0.8;
    public int MaxTokens => 2048;
    public AssistantExecutionMode ExecutionMode => AssistantExecutionMode.Chat;
    public IReadOnlyList<string> EnabledToolNames { get; } = Array.Empty<string>();
    public IReadOnlyList<string> PersonaTargetSlugs { get; } = new[] { "editor" };
    public SafetyPreset? SafetyPresetOverride => SafetyPreset.Standard;
}
