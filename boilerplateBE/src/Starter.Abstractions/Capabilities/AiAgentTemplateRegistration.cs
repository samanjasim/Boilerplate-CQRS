using Starter.Abstractions.Ai;

namespace Starter.Abstractions.Capabilities;

internal sealed class AiAgentTemplateRegistration(
    IAiAgentTemplate inner,
    string moduleSource) : IAiAgentTemplate, IAiAgentTemplateModuleSource
{
    public string Slug => inner.Slug;
    public string DisplayName => inner.DisplayName;
    public string Description => inner.Description;
    public string Category => inner.Category;
    public string SystemPrompt => inner.SystemPrompt;
    public AiProviderType Provider => inner.Provider;
    public string Model => inner.Model;
    public double Temperature => inner.Temperature;
    public int MaxTokens => inner.MaxTokens;
    public AssistantExecutionMode ExecutionMode => inner.ExecutionMode;
    public IReadOnlyList<string> EnabledToolNames => inner.EnabledToolNames;
    public IReadOnlyList<string> PersonaTargetSlugs => inner.PersonaTargetSlugs;
    public SafetyPreset? SafetyPresetOverride => inner.SafetyPresetOverride;

    public string ModuleSource => moduleSource;
}
