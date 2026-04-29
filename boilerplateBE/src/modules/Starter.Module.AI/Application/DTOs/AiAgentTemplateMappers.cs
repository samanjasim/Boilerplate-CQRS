using Starter.Abstractions.Capabilities;

namespace Starter.Module.AI.Application.DTOs;

internal static class AiAgentTemplateMappers
{
    public static AiAgentTemplateDto ToDto(this IAiAgentTemplate template) => new(
        Slug: template.Slug,
        DisplayName: template.DisplayName,
        Description: template.Description,
        Category: template.Category,
        Module: ModuleOf(template),
        Provider: template.Provider.ToString(),
        Model: template.Model,
        Temperature: template.Temperature,
        MaxTokens: template.MaxTokens,
        ExecutionMode: template.ExecutionMode.ToString(),
        EnabledToolNames: template.EnabledToolNames,
        PersonaTargetSlugs: template.PersonaTargetSlugs,
        SafetyPresetOverride: template.SafetyPresetOverride?.ToString());

    private static string ModuleOf(IAiAgentTemplate template) =>
        template is IAiAgentTemplateModuleSource src ? src.ModuleSource : "Unknown";
}
