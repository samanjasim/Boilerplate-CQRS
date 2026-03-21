namespace Starter.Application.Features.Auth.Queries.GetLoginHistory;

public sealed record LoginHistoryDto(
    Guid Id,
    string Email,
    string? IpAddress,
    string? DeviceInfo,
    bool Success,
    string? FailureReason,
    DateTime CreatedAt);
