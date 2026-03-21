namespace Starter.Application.Features.Notifications;

public sealed record NotificationDto(
    Guid Id,
    string Type,
    string Title,
    string Message,
    string? Data,
    bool IsRead,
    DateTime CreatedAt);
