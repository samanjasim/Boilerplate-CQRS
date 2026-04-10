using MediatR;
using Starter.Shared.Results;

namespace Starter.Module.Products.Application.Commands.UpdateProduct;

public sealed record UpdateProductCommand(
    Guid Id,
    string Name,
    string? Description,
    decimal Price,
    string Currency,
    Guid? TenantId = null) : IRequest<Result>;
