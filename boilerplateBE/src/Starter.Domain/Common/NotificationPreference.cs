namespace Starter.Domain.Common;

public sealed class NotificationPreference : BaseEntity
{
    public Guid UserId { get; private set; }
    public string NotificationType { get; private set; } = null!;
    public bool EmailEnabled { get; private set; } = true;
    public bool InAppEnabled { get; private set; } = true;

    private NotificationPreference() { }

    private NotificationPreference(Guid id) : base(id) { }

    public static NotificationPreference Create(
        Guid userId,
        string notificationType,
        bool emailEnabled = true,
        bool inAppEnabled = true)
    {
        return new NotificationPreference(Guid.NewGuid())
        {
            UserId = userId,
            NotificationType = notificationType,
            EmailEnabled = emailEnabled,
            InAppEnabled = inAppEnabled
        };
    }

    public void Update(bool emailEnabled, bool inAppEnabled)
    {
        EmailEnabled = emailEnabled;
        InAppEnabled = inAppEnabled;
    }
}
