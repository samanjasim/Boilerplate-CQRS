using Starter.Domain.Common;
using Starter.Domain.Tenants.Enums;
using Starter.Domain.Tenants.Events;

namespace Starter.Domain.Tenants.Entities;

public sealed class Tenant : AggregateRoot
{
    public string Name { get; private set; } = null!;
    public string? Slug { get; private set; }
    public TenantStatus Status { get; private set; } = null!;
    public string? ConnectionString { get; private set; }

    private Tenant() { }

    private Tenant(
        Guid id,
        string name,
        string? slug,
        string? connectionString) : base(id)
    {
        Name = name;
        Slug = slug;
        Status = TenantStatus.Active;
        ConnectionString = connectionString;
    }

    public static Tenant Create(
        string name,
        string? slug = null,
        string? connectionString = null)
    {
        var tenant = new Tenant(
            Guid.NewGuid(),
            name,
            slug,
            connectionString);

        tenant.RaiseDomainEvent(new TenantCreatedEvent(tenant.Id));

        return tenant;
    }

    public void Update(string name, string? slug)
    {
        Name = name;
        Slug = slug;
    }

    public void Activate()
    {
        Status = TenantStatus.Active;
    }

    public void Deactivate()
    {
        Status = TenantStatus.Inactive;
    }

    public void Suspend()
    {
        Status = TenantStatus.Suspended;
    }
}
