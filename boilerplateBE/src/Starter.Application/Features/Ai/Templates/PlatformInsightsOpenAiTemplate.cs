using Starter.Abstractions.Ai;
using Starter.Abstractions.Capabilities;

namespace Starter.Application.Features.Ai.Templates;

public sealed class PlatformInsightsOpenAiTemplate : IAiAgentTemplate
{
    public string Slug => "platform_insights_openai";
    public string DisplayName => "Platform Insights (OpenAI)";
    public string Description => PlatformInsightsPrompts.Description;
    public string Category => "Platform";
    public string SystemPrompt => PlatformInsightsPrompts.SystemPrompt;
    public AiProviderType Provider => AiProviderType.OpenAI;
    public string Model => "gpt-4o-mini";
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
