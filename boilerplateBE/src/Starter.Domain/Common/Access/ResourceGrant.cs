using Starter.Domain.Common.Access.Enums;

namespace Starter.Domain.Common.Access;

public sealed class ResourceGrant : BaseEntity, ITenantEntity
{
    public Guid? TenantId { get; private set; }
    public string ResourceType { get; private set; } = default!;
    public Guid ResourceId { get; private set; }
    public GrantSubjectType SubjectType { get; private set; }
    public Guid SubjectId { get; private set; }
    public AccessLevel Level { get; private set; }
    public Guid GrantedByUserId { get; private set; }
    public DateTime GrantedAt { get; private set; }

    private ResourceGrant() { }

    private ResourceGrant(Guid id) : base(id) { }

    public static ResourceGrant Create(
        Guid? tenantId,
        string resourceType,
        Guid resourceId,
        GrantSubjectType subjectType,
        Guid subjectId,
        AccessLevel level,
        Guid grantedByUserId)
    {
        return new ResourceGrant(Guid.NewGuid())
        {
            TenantId = tenantId,
            ResourceType = resourceType,
            ResourceId = resourceId,
            SubjectType = subjectType,
            SubjectId = subjectId,
            Level = level,
            GrantedByUserId = grantedByUserId,
            GrantedAt = DateTime.UtcNow,
        };
    }

    public void UpdateLevel(AccessLevel level) => Level = level;
}
