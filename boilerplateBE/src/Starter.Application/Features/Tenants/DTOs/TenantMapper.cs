using Starter.Domain.Tenants.Entities;

namespace Starter.Application.Features.Tenants.DTOs;

public static class TenantMapper
{
    public static TenantDto ToDto(this Tenant tenant)
    {
        return new TenantDto(
            tenant.Id,
            tenant.Name,
            tenant.Slug,
            tenant.Status.Name,
            tenant.CreatedAt);
    }

    public static IReadOnlyList<TenantDto> ToDtoList(this IEnumerable<Tenant> tenants)
    {
        return tenants.Select(t => t.ToDto()).ToList();
    }
}
