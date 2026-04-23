using Microsoft.EntityFrameworkCore;
using Starter.Abstractions.Capabilities;
using Starter.Module.Products.Infrastructure.Persistence;

namespace Starter.Module.Products.Infrastructure.Tenancy;

internal sealed class ProductTenantResolver(ProductsDbContext context) : ITenantResolver
{
    public Task<Guid?> ResolveTenantIdAsync(Guid entityId, CancellationToken ct) =>
        context.Products
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(p => p.Id == entityId)
            .Select(p => p.TenantId)
            .FirstOrDefaultAsync(ct);
}
