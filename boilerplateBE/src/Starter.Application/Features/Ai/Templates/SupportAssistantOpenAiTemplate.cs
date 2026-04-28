using Starter.Abstractions.Ai;
using Starter.Abstractions.Capabilities;

namespace Starter.Application.Features.Ai.Templates;

public sealed class SupportAssistantOpenAiTemplate : IAiAgentTemplate
{
    public string Slug => "support_assistant_openai";
    public string DisplayName => "Support Assistant (OpenAI)";
    public string Description => SupportAssistantPrompts.Description;
    public string Category => "General";
    public string SystemPrompt => SupportAssistantPrompts.SystemPrompt;
    public AiProviderType Provider => AiProviderType.OpenAI;
    public string Model => "gpt-4o-mini";
    public double Temperature => 0.3;
    public int MaxTokens => 2048;
    public AssistantExecutionMode ExecutionMode => AssistantExecutionMode.Chat;
    public IReadOnlyList<string> EnabledToolNames { get; } = new[] { "list_users" };
    public IReadOnlyList<string> PersonaTargetSlugs { get; } = new[] { "default" };
    public SafetyPreset? SafetyPresetOverride => null;
}
