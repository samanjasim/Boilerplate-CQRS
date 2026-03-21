namespace Starter.Application.Features.Tenants.DTOs;

public sealed record TenantDto(
    Guid Id,
    string Name,
    string? Slug,
    string Status,
    DateTime CreatedAt);
