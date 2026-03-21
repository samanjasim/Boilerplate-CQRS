namespace Starter.Application.Features.Notifications;

public sealed record NotificationPreferenceDto(
    string NotificationType,
    bool EmailEnabled,
    bool InAppEnabled);
