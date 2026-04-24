namespace Starter.Module.AI.Application.DTOs;

public sealed record UserPersonaDto(
    Guid UserId,
    string? UserDisplayName,
    Guid PersonaId,
    string PersonaSlug,
    string PersonaDisplayName,
    bool IsDefault,
    DateTime AssignedAt);
