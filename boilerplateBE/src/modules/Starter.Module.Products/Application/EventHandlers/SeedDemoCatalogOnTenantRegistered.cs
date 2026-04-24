using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Starter.Application.Common.Events;
using Starter.Module.Products.Domain.Entities;
using Starter.Module.Products.Domain.Enums;
using Starter.Module.Products.Infrastructure.Persistence;

namespace Starter.Module.Products.Application.EventHandlers;

public sealed class SeedDemoCatalogOnTenantRegistered(
    ProductsDbContext context,
    ILogger<SeedDemoCatalogOnTenantRegistered> logger)
    : IConsumer<TenantRegisteredEvent>
{
    public async Task Consume(ConsumeContext<TenantRegisteredEvent> context_)
    {
        var ct = context_.CancellationToken;
        var evt = context_.Message;

        if (await context.Products.IgnoreQueryFilters().AnyAsync(p => p.TenantId == evt.TenantId, ct))
        {
            logger.LogInformation(
                "Tenant {TenantId} already has products, skipping demo catalog seed",
                evt.TenantId);
            return;
        }

        var products = new[]
        {
            Product.Create(evt.TenantId, "Sample Product 1", "sample-product-1", "A sample product for demonstration.", 9.99m, "USD", ProductStatus.Active),
            Product.Create(evt.TenantId, "Sample Product 2", "sample-product-2", "Another sample product for demonstration.", 19.99m, "USD", ProductStatus.Active),
            Product.Create(evt.TenantId, "Sample Product 3", "sample-product-3", "A premium sample product for demonstration.", 29.99m, "USD", ProductStatus.Active),
        };

        context.Products.AddRange(products);
        await context.SaveChangesAsync(ct);

        logger.LogInformation(
            "Seeded {Count} demo products for tenant {TenantId}",
            products.Length, evt.TenantId);
    }
}
