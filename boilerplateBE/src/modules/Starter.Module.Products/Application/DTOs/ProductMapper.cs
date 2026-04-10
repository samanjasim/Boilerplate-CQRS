using Starter.Module.Products.Domain.Entities;

namespace Starter.Module.Products.Application.DTOs;

public static class ProductMapper
{
    public static ProductDto ToDto(this Product entity, string? tenantName = null)
    {
        return new ProductDto(
            Id: entity.Id,
            TenantId: entity.TenantId,
            TenantName: tenantName,
            Name: entity.Name,
            Slug: entity.Slug,
            Description: entity.Description,
            Price: entity.Price,
            Currency: entity.Currency,
            Status: entity.Status.ToString(),
            ImageFileId: entity.ImageFileId,
            CreatedAt: entity.CreatedAt,
            ModifiedAt: entity.ModifiedAt);
    }
}
