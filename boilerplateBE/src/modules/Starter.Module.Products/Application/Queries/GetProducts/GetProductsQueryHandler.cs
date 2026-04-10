using MediatR;
using Microsoft.EntityFrameworkCore;
using Starter.Abstractions.Readers;
using Starter.Application.Common.Models;
using Starter.Module.Products.Application.DTOs;
using Starter.Module.Products.Domain.Entities;
using Starter.Module.Products.Domain.Enums;
using Starter.Module.Products.Infrastructure.Persistence;
using Starter.Shared.Results;

namespace Starter.Module.Products.Application.Queries.GetProducts;

internal sealed class GetProductsQueryHandler(
    ProductsDbContext context,
    ITenantReader tenantReader) : IRequestHandler<GetProductsQuery, Result<PaginatedList<ProductDto>>>
{
    public async Task<Result<PaginatedList<ProductDto>>> Handle(
        GetProductsQuery request, CancellationToken cancellationToken)
    {
        var query = context.Products.AsNoTracking().AsQueryable();

        if (request.TenantId.HasValue)
            query = query.Where(p => p.TenantId == request.TenantId.Value);

        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var term = request.SearchTerm.Trim().ToLowerInvariant();
            query = query.Where(p =>
                p.Name.ToLower().Contains(term) ||
                p.Slug.Contains(term));
        }

        if (!string.IsNullOrWhiteSpace(request.Status) &&
            Enum.TryParse<ProductStatus>(request.Status, true, out var status))
        {
            query = query.Where(p => p.Status == status);
        }

        query = query.OrderByDescending(p => p.CreatedAt);

        var page = await PaginatedList<Product>.CreateAsync(
            query,
            request.PageNumber,
            request.PageSize,
            cancellationToken);

        var tenantIds = page.Items
            .Where(p => p.TenantId.HasValue)
            .Select(p => p.TenantId!.Value)
            .Distinct();

        var tenants = await tenantReader.GetManyAsync(tenantIds, cancellationToken);
        var tenantNames = tenants.ToDictionary(t => t.Id, t => t.Name);

        var result = page.Map(p => p.ToDto(
            p.TenantId.HasValue && tenantNames.TryGetValue(p.TenantId.Value, out var name) ? name : null));

        return Result.Success(result);
    }
}
