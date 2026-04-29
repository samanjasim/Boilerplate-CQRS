using Starter.Abstractions.Ai;
using Starter.Abstractions.Capabilities;

namespace Starter.Application.Features.Ai.Templates;

public sealed class SupportCopilotTemplate : IAiAgentTemplate
{
    public string Slug => "support_copilot";
    public string DisplayName => "Support Copilot";
    public string Description => SupportCopilotPrompts.Description;
    public string Category => "Platform";
    public string SystemPrompt => SupportCopilotPrompts.SystemPrompt;
    public AiProviderType Provider => AiProviderType.Anthropic;
    public string Model => "claude-sonnet-4-20250514";
    public double Temperature => 0.3;
    public int MaxTokens => 2048;
    public AssistantExecutionMode ExecutionMode => AssistantExecutionMode.Chat;
    public IReadOnlyList<string> EnabledToolNames { get; } = Array.Empty<string>();
    public IReadOnlyList<string> PersonaTargetSlugs { get; } = new[] { "default" };
    public SafetyPreset? SafetyPresetOverride => null;
}
