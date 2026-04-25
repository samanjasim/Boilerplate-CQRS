using MediatR;
using Starter.Module.AI.Application.DTOs;
using Starter.Abstractions.Ai;
using Starter.Module.AI.Domain.Enums;
using Starter.Shared.Results;

namespace Starter.Module.AI.Application.Commands.Personas.CreatePersona;

public sealed record CreatePersonaCommand(
    string DisplayName,
    string? Description,
    string? Slug,
    PersonaAudienceType AudienceType,
    SafetyPreset SafetyPreset,
    IReadOnlyList<string>? PermittedAgentSlugs)
    : IRequest<Result<AiPersonaDto>>;
