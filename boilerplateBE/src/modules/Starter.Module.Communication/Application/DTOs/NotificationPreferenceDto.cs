namespace Starter.Module.Communication.Application.DTOs;

public sealed record NotificationPreferenceDto(
    Guid UserId,
    string Category,
    bool EmailEnabled,
    bool SmsEnabled,
    bool PushEnabled,
    bool WhatsAppEnabled,
    bool InAppEnabled);
