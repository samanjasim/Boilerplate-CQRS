using MediatR;
using Starter.Application.Common.Models;
using Starter.Module.Products.Application.DTOs;
using Starter.Shared.Results;

namespace Starter.Module.Products.Application.Queries.GetProducts;

public sealed record GetProductsQuery(
    int PageNumber = 1,
    int PageSize = 20,
    string? SearchTerm = null,
    string? Status = null,
    Guid? TenantId = null) : IRequest<Result<PaginatedList<ProductDto>>>;
