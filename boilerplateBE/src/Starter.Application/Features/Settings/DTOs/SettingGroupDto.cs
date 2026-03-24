namespace Starter.Application.Features.Settings.DTOs;

public sealed record SettingGroupDto(
    string Category,
    List<SystemSettingDto> Settings);
