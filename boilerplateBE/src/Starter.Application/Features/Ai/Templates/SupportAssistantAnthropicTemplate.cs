using Starter.Abstractions.Ai;
using Starter.Abstractions.Capabilities;

namespace Starter.Application.Features.Ai.Templates;

public sealed class SupportAssistantAnthropicTemplate : IAiAgentTemplate
{
    public string Slug => "support_assistant_anthropic";
    public string DisplayName => "Support Assistant (Anthropic)";
    public string Description => SupportAssistantPrompts.Description;
    public string Category => "General";
    public string SystemPrompt => SupportAssistantPrompts.SystemPrompt;
    public AiProviderType Provider => AiProviderType.Anthropic;
    public string Model => "claude-sonnet-4-20250514";
    public double Temperature => 0.3;
    public int MaxTokens => 2048;
    public AssistantExecutionMode ExecutionMode => AssistantExecutionMode.Chat;
    public IReadOnlyList<string> EnabledToolNames { get; } = new[] { "list_users" };
    public IReadOnlyList<string> PersonaTargetSlugs { get; } = new[] { "default" };
    public SafetyPreset? SafetyPresetOverride => null;
}
