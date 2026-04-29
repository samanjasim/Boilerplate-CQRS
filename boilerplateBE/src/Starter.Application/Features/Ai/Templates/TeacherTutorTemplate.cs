using Starter.Abstractions.Ai;
using Starter.Abstractions.Capabilities;

namespace Starter.Application.Features.Ai.Templates;

public sealed class TeacherTutorTemplate : IAiAgentTemplate
{
    public string Slug => "teacher_tutor";
    public string DisplayName => "Teacher Tutor";
    public string Description => TeacherTutorPrompts.Description;
    public string Category => "Education";
    public string SystemPrompt => TeacherTutorPrompts.SystemPrompt;
    public AiProviderType Provider => AiProviderType.Anthropic;
    public string Model => "claude-sonnet-4-20250514";
    public double Temperature => 0.5;
    public int MaxTokens => 2048;
    public AssistantExecutionMode ExecutionMode => AssistantExecutionMode.Chat;
    public IReadOnlyList<string> EnabledToolNames { get; } = Array.Empty<string>();
    public IReadOnlyList<string> PersonaTargetSlugs { get; } = new[] { "student" };
    public SafetyPreset? SafetyPresetOverride => SafetyPreset.ChildSafe;
}
