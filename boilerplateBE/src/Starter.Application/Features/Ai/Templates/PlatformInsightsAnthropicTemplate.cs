using Starter.Abstractions.Ai;
using Starter.Abstractions.Capabilities;

namespace Starter.Application.Features.Ai.Templates;

public sealed class PlatformInsightsAnthropicTemplate : IAiAgentTemplate
{
    public string Slug => "platform_insights_anthropic";
    public string DisplayName => "Platform Insights (Anthropic)";
    public string Description => PlatformInsightsPrompts.Description;
    public string Category => "Platform";
    public string SystemPrompt => PlatformInsightsPrompts.SystemPrompt;
    public AiProviderType Provider => AiProviderType.Anthropic;
    public string Model => "claude-sonnet-4-20250514";
    public double Temperature => 0.3;
    public int MaxTokens => 2048;
    public AssistantExecutionMode ExecutionMode => AssistantExecutionMode.Chat;
    public IReadOnlyList<string> EnabledToolNames { get; } = new[]
    {
        "list_users", "list_audit_logs", "list_subscriptions", "list_usage", "list_conversations",
    };
    public IReadOnlyList<string> PersonaTargetSlugs { get; } = new[] { "default" };
    public SafetyPreset? SafetyPresetOverride => null;
}
