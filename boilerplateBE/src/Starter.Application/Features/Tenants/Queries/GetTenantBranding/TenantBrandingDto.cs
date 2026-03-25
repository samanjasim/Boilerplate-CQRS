namespace Starter.Application.Features.Tenants.Queries.GetTenantBranding;

public sealed record TenantBrandingDto(
    Guid TenantId,
    string? Slug,
    string Status,
    string? LogoUrl,
    string? FaviconUrl,
    string? PrimaryColor,
    string? SecondaryColor,
    string? LoginPageTitle,
    string? LoginPageSubtitle,
    string TenantName);
