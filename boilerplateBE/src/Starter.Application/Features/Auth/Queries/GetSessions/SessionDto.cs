namespace Starter.Application.Features.Auth.Queries.GetSessions;

public sealed record SessionDto(
    Guid Id,
    string? IpAddress,
    string? DeviceInfo,
    DateTime CreatedAt,
    DateTime LastActiveAt,
    bool IsCurrent);
