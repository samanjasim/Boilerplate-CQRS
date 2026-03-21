namespace Starter.Domain.Common;

public sealed class NotificationPreference
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string NotificationType { get; set; } = null!;
    public bool EmailEnabled { get; set; } = true;
    public bool InAppEnabled { get; set; } = true;
}
