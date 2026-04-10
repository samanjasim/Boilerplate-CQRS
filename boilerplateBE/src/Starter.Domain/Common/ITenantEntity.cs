namespace Starter.Domain.Common;

public interface ITenantEntity
{
    Guid? TenantId { get; }
}
