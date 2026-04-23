using Starter.Domain.Common;
using Starter.Module.CommentsActivity.Domain.Enums;

namespace Starter.Module.CommentsActivity.Domain.Entities;

public sealed class EntityWatcher : BaseEntity, ITenantEntity
{
    public Guid? TenantId { get; private set; }
    public string EntityType { get; private set; } = default!;
    public Guid EntityId { get; private set; }
    public Guid UserId { get; private set; }
    public WatchReason Reason { get; private set; }

    private EntityWatcher() { }

    public static EntityWatcher Create(
        Guid? tenantId,
        string entityType,
        Guid entityId,
        Guid userId,
        WatchReason reason)
    {
        return new EntityWatcher
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            EntityType = entityType.Trim(),
            EntityId = entityId,
            UserId = userId,
            Reason = reason,
            CreatedAt = DateTime.UtcNow
        };
    }
}
