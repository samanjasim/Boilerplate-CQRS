using Starter.Abstractions.Ai;
using Starter.Module.AI.Domain.Enums;

namespace Starter.Module.AI.Application.DTOs;

public sealed record AiPersonaDto(
    Guid Id,
    string Slug,
    string DisplayName,
    string? Description,
    PersonaAudienceType AudienceType,
    SafetyPreset SafetyPreset,
    IReadOnlyList<string> PermittedAgentSlugs,
    bool IsSystemReserved,
    bool IsActive,
    DateTime CreatedAt,
    DateTime? ModifiedAt);
