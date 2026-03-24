namespace Starter.Application.Features.Tenants.DTOs;

public sealed record TenantDto(
    Guid Id,
    string Name,
    string? Slug,
    string Status,
    DateTime CreatedAt,
    // Branding
    Guid? LogoFileId = null,
    Guid? FaviconFileId = null,
    string? LogoUrl = null,
    string? FaviconUrl = null,
    string? PrimaryColor = null,
    string? SecondaryColor = null,
    string? Description = null,
    // Business Info
    string? Address = null,
    string? Phone = null,
    string? Website = null,
    string? TaxId = null,
    // Custom Text
    string? LoginPageTitle = null,
    string? LoginPageSubtitle = null,
    string? EmailFooterText = null);
