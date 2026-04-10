using MediatR;
using Starter.Shared.Results;

namespace Starter.Module.Products.Application.Commands.CreateProduct;

public sealed record CreateProductCommand(
    string Name,
    string Slug,
    string? Description,
    decimal Price,
    string Currency,
    Guid? TenantId = null) : IRequest<Result<Guid>>;
