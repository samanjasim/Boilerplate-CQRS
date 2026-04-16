namespace Starter.Module.Communication.Application.DTOs;

public sealed record EventRegistrationDto(
    Guid Id,
    string EventName,
    string ModuleSource,
    string DisplayName,
    string? Description);
