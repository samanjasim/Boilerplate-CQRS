using Starter.Domain.Common;

namespace Starter.Module.CommentsActivity.Domain.Entities;

public sealed class ActivityEntry : BaseEntity, ITenantEntity
{
    public Guid? TenantId { get; private set; }
    public string EntityType { get; private set; } = default!;
    public Guid EntityId { get; private set; }
    public string Action { get; private set; } = default!;
    public Guid? ActorId { get; private set; }
    public string? MetadataJson { get; private set; }
    public string? Description { get; private set; }

    private ActivityEntry() { }

    public static ActivityEntry Create(
        Guid? tenantId,
        string entityType,
        Guid entityId,
        string action,
        Guid? actorId,
        string? metadataJson = null,
        string? description = null)
    {
        return new ActivityEntry
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            EntityType = entityType.Trim(),
            EntityId = entityId,
            Action = action.Trim(),
            ActorId = actorId,
            MetadataJson = metadataJson,
            Description = description?.Trim(),
            CreatedAt = DateTime.UtcNow
        };
    }
}
