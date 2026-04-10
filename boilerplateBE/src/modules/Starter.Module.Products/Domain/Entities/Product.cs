using Starter.Domain.Common;
using Starter.Module.Products.Domain.Enums;
using Starter.Module.Products.Domain.Events;

namespace Starter.Module.Products.Domain.Entities;

public sealed class Product : AggregateRoot, ITenantEntity
{
    public Guid? TenantId { get; private set; }
    public string Name { get; private set; } = default!;
    public string Slug { get; private set; } = default!;
    public string? Description { get; private set; }
    public decimal Price { get; private set; }
    public string Currency { get; private set; } = default!;
    public ProductStatus Status { get; private set; }
    public Guid? ImageFileId { get; private set; }

    private Product() { }

    private Product(
        Guid id,
        Guid? tenantId,
        string name,
        string slug,
        string? description,
        decimal price,
        string currency,
        ProductStatus status) : base(id)
    {
        TenantId = tenantId;
        Name = name;
        Slug = slug;
        Description = description;
        Price = price;
        Currency = currency;
        Status = status;
    }

    public static Product Create(
        Guid? tenantId,
        string name,
        string slug,
        string? description,
        decimal price,
        string currency,
        ProductStatus status = ProductStatus.Draft)
    {
        var product = new Product(
            Guid.NewGuid(),
            tenantId,
            name.Trim(),
            slug.Trim().ToLowerInvariant(),
            description?.Trim(),
            price,
            currency.Trim().ToUpperInvariant(),
            status);

        product.RaiseDomainEvent(new ProductCreatedEvent(
            product.Id, tenantId, product.Name, product.Slug));

        return product;
    }

    public void Update(
        string name,
        string? description,
        decimal price,
        string currency)
    {
        Name = name.Trim();
        Description = description?.Trim();
        Price = price;
        Currency = currency.Trim().ToUpperInvariant();
        ModifiedAt = DateTime.UtcNow;
    }

    public void Publish()
    {
        Status = ProductStatus.Active;
        ModifiedAt = DateTime.UtcNow;
    }

    public void Archive()
    {
        Status = ProductStatus.Archived;
        ModifiedAt = DateTime.UtcNow;
    }

    public void SetImage(Guid? fileId)
    {
        ImageFileId = fileId;
        ModifiedAt = DateTime.UtcNow;
    }

    public void SetTenant(Guid? tenantId)
    {
        TenantId = tenantId;
        ModifiedAt = DateTime.UtcNow;
    }
}
