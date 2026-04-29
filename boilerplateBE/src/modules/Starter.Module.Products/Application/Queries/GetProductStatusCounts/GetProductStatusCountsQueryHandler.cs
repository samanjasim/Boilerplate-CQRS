using MediatR;
using Microsoft.EntityFrameworkCore;
using Starter.Module.Products.Application.DTOs;
using Starter.Module.Products.Domain.Enums;
using Starter.Module.Products.Infrastructure.Persistence;
using Starter.Shared.Results;

namespace Starter.Module.Products.Application.Queries.GetProductStatusCounts;

internal sealed class GetProductStatusCountsQueryHandler(ProductsDbContext context)
    : IRequestHandler<GetProductStatusCountsQuery, Result<ProductStatusCountsDto>>
{
    public async Task<Result<ProductStatusCountsDto>> Handle(
        GetProductStatusCountsQuery request,
        CancellationToken cancellationToken)
    {
        var query = context.Products.AsNoTracking().AsQueryable();

        if (request.TenantId.HasValue)
            query = query.Where(p => p.TenantId == request.TenantId.Value);

        var counts = await query
            .GroupBy(p => p.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var dict = counts.ToDictionary(x => x.Status, x => x.Count);

        return Result.Success(new ProductStatusCountsDto(
            Draft: dict.GetValueOrDefault(ProductStatus.Draft),
            Active: dict.GetValueOrDefault(ProductStatus.Active),
            Archived: dict.GetValueOrDefault(ProductStatus.Archived)
        ));
    }
}
