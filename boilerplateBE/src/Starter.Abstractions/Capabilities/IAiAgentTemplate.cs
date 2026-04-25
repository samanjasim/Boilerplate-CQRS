using Starter.Abstractions.Ai;

namespace Starter.Abstractions.Capabilities;

/// <summary>
/// A module-authored agent preset. Implementations describe an assistant's
/// system prompt, model parameters, capability bindings, and audience targeting.
/// Discovered by <see cref="AiAgentTemplateDiscoveryExtensions.AddAiAgentTemplatesFromAssembly"/>
/// and installed via <c>InstallTemplateCommand</c> to materialise a tenant-scoped
/// <c>AiAssistant</c>.
///
/// Implementations MUST have a public parameterless constructor — templates are
/// pure data, not DI consumers. Use <c>const string</c> fields on a sibling helper
/// type for shared content (system prompts) when multiple variants share the same
/// prose.
/// </summary>
public interface IAiAgentTemplate
{
    /// <summary>Stable identity. Unique across all registered templates.</summary>
    string Slug { get; }

    string DisplayName { get; }
    string Description { get; }
    string Category { get; }

    string SystemPrompt { get; }
    AiProviderType Provider { get; }
    string Model { get; }
    double Temperature { get; }
    int MaxTokens { get; }
    AssistantExecutionMode ExecutionMode { get; }

    /// <summary>Tool slugs from the 5c-1 catalog. Validated at install time.</summary>
    IReadOnlyList<string> EnabledToolNames { get; }

    /// <summary>Persona slugs from <c>AiPersona</c>. Validated at install time.</summary>
    IReadOnlyList<string> PersonaTargetSlugs { get; }

    /// <summary>
    /// Recommended safety preset. Today persona-level safety still applies at runtime;
    /// this field becomes load-bearing when Plan 5d hoists safety onto the assistant.
    /// </summary>
    SafetyPreset? SafetyPresetHint { get; }
}
