using Microsoft.EntityFrameworkCore;
using Starter.Abstractions.Capabilities;
using Starter.Module.Products.Domain.Enums;
using Starter.Module.Products.Infrastructure.Persistence;

namespace Starter.Module.Products.Infrastructure.Services;

internal sealed class ProductsUsageMetricCalculator(ProductsDbContext db) : IUsageMetricCalculator
{
    public string Metric => "products";

    public async Task<long> CalculateAsync(Guid tenantId, CancellationToken cancellationToken = default) =>
        await db.Products
            .IgnoreQueryFilters()
            .CountAsync(p => p.TenantId == tenantId && p.Status != ProductStatus.Archived, cancellationToken);
}
