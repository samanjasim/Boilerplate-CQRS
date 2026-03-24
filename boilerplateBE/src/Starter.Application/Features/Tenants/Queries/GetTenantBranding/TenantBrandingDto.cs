namespace Starter.Application.Features.Tenants.Queries.GetTenantBranding;

public sealed record TenantBrandingDto(
    string? LogoUrl,
    string? FaviconUrl,
    string? PrimaryColor,
    string? SecondaryColor,
    string? LoginPageTitle,
    string? LoginPageSubtitle,
    string TenantName);
