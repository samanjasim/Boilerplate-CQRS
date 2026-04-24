using Starter.Module.AI.Domain.Entities;

namespace Starter.Module.AI.Application.DTOs;

internal static class AiPersonaMappers
{
    public static AiPersonaDto ToDto(this AiPersona p) =>
        new(
            p.Id,
            p.Slug,
            p.DisplayName,
            p.Description,
            p.AudienceType,
            p.SafetyPreset,
            p.PermittedAgentSlugs,
            p.IsSystemReserved,
            p.IsActive,
            p.CreatedAt,
            p.ModifiedAt);
}
