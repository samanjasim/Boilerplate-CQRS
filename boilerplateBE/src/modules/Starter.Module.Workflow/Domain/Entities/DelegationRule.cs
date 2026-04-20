using Starter.Domain.Common;

namespace Starter.Module.Workflow.Domain.Entities;

public sealed class DelegationRule : AggregateRoot, ITenantEntity
{
    public Guid? TenantId { get; private set; }
    public Guid FromUserId { get; private set; }
    public Guid ToUserId { get; private set; }
    public DateTime StartDate { get; private set; }
    public DateTime EndDate { get; private set; }
    public bool IsActive { get; private set; } = true;

    private DelegationRule() { }

    public static DelegationRule Create(
        Guid? tenantId,
        Guid fromUserId,
        Guid toUserId,
        DateTime startDate,
        DateTime endDate)
    {
        return new DelegationRule
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            FromUserId = fromUserId,
            ToUserId = toUserId,
            StartDate = startDate,
            EndDate = endDate,
            IsActive = true,
        };
    }

    public void Deactivate()
    {
        IsActive = false;
        ModifiedAt = DateTime.UtcNow;
    }
}
