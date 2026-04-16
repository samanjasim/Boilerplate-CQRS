using Starter.Domain.Common;

namespace Starter.Module.Communication.Domain.Entities;

public sealed class CommunicationNotificationPreference : BaseEntity
{
    public Guid UserId { get; private set; }
    public Guid TenantId { get; private set; }
    public string Category { get; private set; } = default!;
    public bool EmailEnabled { get; private set; }
    public bool SmsEnabled { get; private set; }
    public bool PushEnabled { get; private set; }
    public bool WhatsAppEnabled { get; private set; }
    public bool InAppEnabled { get; private set; }

    private CommunicationNotificationPreference() { }

    public static CommunicationNotificationPreference Create(Guid userId, Guid tenantId, string category)
    {
        return new CommunicationNotificationPreference
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TenantId = tenantId,
            Category = category,
            EmailEnabled = true,
            SmsEnabled = false,
            PushEnabled = true,
            WhatsAppEnabled = false,
            InAppEnabled = true,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void Update(bool emailEnabled, bool smsEnabled, bool pushEnabled,
        bool whatsAppEnabled, bool inAppEnabled)
    {
        EmailEnabled = emailEnabled;
        SmsEnabled = smsEnabled;
        PushEnabled = pushEnabled;
        WhatsAppEnabled = whatsAppEnabled;
        InAppEnabled = inAppEnabled;
        ModifiedAt = DateTime.UtcNow;
    }
}
