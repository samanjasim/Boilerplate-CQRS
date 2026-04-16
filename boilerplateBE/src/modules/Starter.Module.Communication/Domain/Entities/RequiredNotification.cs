using Starter.Domain.Common;
using Starter.Module.Communication.Domain.Enums;

namespace Starter.Module.Communication.Domain.Entities;

public sealed class RequiredNotification : BaseEntity
{
    public Guid TenantId { get; private set; }
    public string Category { get; private set; } = default!;
    public NotificationChannel Channel { get; private set; }

    private RequiredNotification() { }

    public static RequiredNotification Create(Guid tenantId, string category, NotificationChannel channel)
    {
        return new RequiredNotification
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Category = category,
            Channel = channel,
            CreatedAt = DateTime.UtcNow
        };
    }
}
