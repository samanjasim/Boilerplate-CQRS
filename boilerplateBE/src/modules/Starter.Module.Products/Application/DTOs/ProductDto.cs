namespace Starter.Module.Products.Application.DTOs;

public sealed record ProductDto(
    Guid Id,
    Guid? TenantId,
    string? TenantName,
    string Name,
    string Slug,
    string? Description,
    decimal Price,
    string Currency,
    string Status,
    Guid? ImageFileId,
    DateTime CreatedAt,
    DateTime? ModifiedAt);
