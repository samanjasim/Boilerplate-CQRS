using Starter.Domain.Primitives;

namespace Starter.Domain.Tenants.Enums;

public sealed class TenantStatus : Enumeration<TenantStatus>
{
    public static readonly TenantStatus Active = new(1, nameof(Active));
    public static readonly TenantStatus Inactive = new(2, nameof(Inactive));
    public static readonly TenantStatus Suspended = new(3, nameof(Suspended));

    private TenantStatus(int value, string name) : base(value, name) { }
}
