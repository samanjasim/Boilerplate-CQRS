using MediatR;
using Microsoft.EntityFrameworkCore;
using Starter.Abstractions.Readers;
using Starter.Module.Products.Application.DTOs;
using Starter.Module.Products.Domain.Errors;
using Starter.Module.Products.Infrastructure.Persistence;
using Starter.Shared.Results;

namespace Starter.Module.Products.Application.Queries.GetProductById;

internal sealed class GetProductByIdQueryHandler(
    ProductsDbContext context,
    ITenantReader tenantReader) : IRequestHandler<GetProductByIdQuery, Result<ProductDto>>
{
    public async Task<Result<ProductDto>> Handle(
        GetProductByIdQuery request, CancellationToken cancellationToken)
    {
        var product = await context.Products
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == request.Id, cancellationToken);

        if (product is null)
            return Result.Failure<ProductDto>(ProductErrors.NotFound);

        string? tenantName = null;
        if (product.TenantId.HasValue)
        {
            var tenant = await tenantReader.GetAsync(product.TenantId.Value, cancellationToken);
            tenantName = tenant?.Name;
        }

        return Result.Success(product.ToDto(tenantName));
    }
}
