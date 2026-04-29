using MediatR;
using Starter.Module.Products.Application.DTOs;
using Starter.Shared.Results;

namespace Starter.Module.Products.Application.Queries.GetProductStatusCounts;

public sealed record GetProductStatusCountsQuery(Guid? TenantId = null)
    : IRequest<Result<ProductStatusCountsDto>>;
