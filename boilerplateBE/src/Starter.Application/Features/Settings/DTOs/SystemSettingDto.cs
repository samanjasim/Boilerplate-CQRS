namespace Starter.Application.Features.Settings.DTOs;

public sealed record SystemSettingDto(
    Guid Id,
    string Key,
    string Value,
    string? Description,
    string? Category,
    bool IsSecret,
    string DataType,
    bool IsOverridden);
