using MediatR;
using Starter.Module.AI.Application.DTOs;
using Starter.Abstractions.Ai;
using Starter.Module.AI.Domain.Enums;
using Starter.Shared.Results;

namespace Starter.Module.AI.Application.Commands.Personas.UpdatePersona;

public sealed record UpdatePersonaCommand(
    Guid Id,
    string DisplayName,
    string? Description,
    SafetyPreset SafetyPreset,
    IReadOnlyList<string>? PermittedAgentSlugs,
    bool IsActive) : IRequest<Result<AiPersonaDto>>;
