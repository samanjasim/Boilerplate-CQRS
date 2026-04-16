namespace Starter.Abstractions.Capabilities;

/// <summary>
/// Notification channels for person-to-person messaging.
/// Co-located with <see cref="IMessageDispatcher"/> so the capability
/// contract is fully self-contained.
/// </summary>
public enum NotificationChannelType
{
    Email = 0,
    Sms = 1,
    Push = 2,
    WhatsApp = 3,
    InApp = 4
}
