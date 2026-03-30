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
            tenant.CreatedAt,
            LogoFileId: tenant.LogoFileId,
            FaviconFileId: tenant.FaviconFileId,
            PrimaryColor: tenant.PrimaryColor,
            SecondaryColor: tenant.SecondaryColor,
            Description: tenant.Description,
            Address: tenant.Address,
            Phone: tenant.Phone,
            Website: tenant.Website,
            TaxId: tenant.TaxId,
            LoginPageTitle: tenant.LoginPageTitle,
            LoginPageSubtitle: tenant.LoginPageSubtitle,
            EmailFooterText: tenant.EmailFooterText,
            DefaultRegistrationRoleId: tenant.DefaultRegistrationRoleId);
    }

    public static TenantDto ToDto(this Tenant tenant, string? logoUrl, string? faviconUrl, string? defaultRoleName = null)
    {
        return new TenantDto(
            tenant.Id,
            tenant.Name,
            tenant.Slug,
            tenant.Status.Name,
            tenant.CreatedAt,
            LogoFileId: tenant.LogoFileId,
            FaviconFileId: tenant.FaviconFileId,
            LogoUrl: logoUrl,
            FaviconUrl: faviconUrl,
            PrimaryColor: tenant.PrimaryColor,
            SecondaryColor: tenant.SecondaryColor,
            Description: tenant.Description,
            Address: tenant.Address,
            Phone: tenant.Phone,
            Website: tenant.Website,
            TaxId: tenant.TaxId,
            LoginPageTitle: tenant.LoginPageTitle,
            LoginPageSubtitle: tenant.LoginPageSubtitle,
            EmailFooterText: tenant.EmailFooterText,
            DefaultRegistrationRoleId: tenant.DefaultRegistrationRoleId,
            DefaultRoleName: defaultRoleName);
    }

    public static IReadOnlyList<TenantDto> ToDtoList(this IEnumerable<Tenant> tenants)
    {
        return tenants.Select(t => t.ToDto()).ToList();
    }
}
