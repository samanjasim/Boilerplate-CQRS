namespace Starter.Module.AI.Application.DTOs;

public sealed record MePersonasDto(
    IReadOnlyList<UserPersonaDto> Personas,
    Guid? DefaultPersonaId);
