namespace Starter.Module.Communication.Application.DTOs;

public sealed record MessageTemplateOverrideDto(
    Guid Id,
    string? SubjectTemplate,
    string BodyTemplate,
    bool IsActive,
    DateTime CreatedAt,
    DateTime? ModifiedAt);
