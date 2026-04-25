namespace Starter.Module.AI.Application.DTOs;

public sealed record AiAgentTemplateDto(
    string Slug,
    string DisplayName,
    string Description,
    string Category,
    string Module,
    string Provider,
    string Model,
    double Temperature,
    int MaxTokens,
    string ExecutionMode,
    IReadOnlyList<string> EnabledToolNames,
    IReadOnlyList<string> PersonaTargetSlugs,
    string? SafetyPresetHint);
