using Starter.Module.Communication.Domain.Entities;

namespace Starter.Module.Communication.Application.DTOs;

public static class PreferenceMapper
{
    public static NotificationPreferenceDto ToDto(this CommunicationNotificationPreference entity)
    {
        return new NotificationPreferenceDto(
            UserId: entity.UserId,
            Category: entity.Category,
            EmailEnabled: entity.EmailEnabled,
            SmsEnabled: entity.SmsEnabled,
            PushEnabled: entity.PushEnabled,
            WhatsAppEnabled: entity.WhatsAppEnabled,
            InAppEnabled: entity.InAppEnabled);
    }

    public static RequiredNotificationDto ToDto(this RequiredNotification entity)
    {
        return new RequiredNotificationDto(
            Id: entity.Id,
            Category: entity.Category,
            Channel: entity.Channel,
            CreatedAt: entity.CreatedAt);
    }
}
