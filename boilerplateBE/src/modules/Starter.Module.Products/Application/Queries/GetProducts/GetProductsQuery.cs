using System.ComponentModel;
using MediatR;
using Starter.Abstractions.Capabilities;
using Starter.Abstractions.Paging;
using Starter.Module.Products.Application.DTOs;
using Starter.Module.Products.Constants;
using Starter.Shared.Results;

namespace Starter.Module.Products.Application.Queries.GetProducts;

[AiTool(
    Name = "list_products",
    Description = "List products in the current tenant, paged and optionally filtered by status or search term.",
    Category = "Products",
    RequiredPermission = ProductPermissions.View,
    IsReadOnly = true)]
public sealed record GetProductsQuery(
    [property: Description("Page number, 1-based. Default 1.")]
    int PageNumber = 1,
    [property: Description("Page size, 1–100. Default 20.")]
    int PageSize = 20,
    [property: Description("Free-text search across product name and SKU.")]
    string? SearchTerm = null,
    [property: Description("Status filter: 'active', 'draft', or 'archived'.")]
    string? Status = null,
    [property: AiParameterIgnore] Guid? TenantId = null)
    : IRequest<Result<PaginatedList<ProductDto>>>;
