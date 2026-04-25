using Starter.Module.AI.Domain.Entities;

namespace Starter.Module.AI.Application.DTOs;

internal static class AiAssistantMappers
{
    public static AiAssistantDto ToDto(this AiAssistant a) =>
        new(
            a.Id,
            a.Name,
            a.Description,
            a.SystemPrompt,
            a.Provider,
            a.Model,
            a.Temperature,
            a.MaxTokens,
            a.EnabledToolNames,
            a.KnowledgeBaseDocIds,
            a.ExecutionMode,
            a.MaxAgentSteps,
            a.IsActive,
            a.CreatedAt,
            a.ModifiedAt,
            a.RagScope,
            a.Visibility,
            a.AccessMode,
            a.CreatedByUserId,
            a.Slug,
            a.PersonaTargetSlugs,
            a.TemplateSourceSlug,
            a.TemplateSourceVersion);
}
