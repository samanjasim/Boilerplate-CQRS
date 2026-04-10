namespace Starter.Application.Features.Notifications.DTOs;

public sealed record NotificationPreferenceDto(
    string NotificationType,
    bool EmailEnabled,
    bool InAppEnabled);
