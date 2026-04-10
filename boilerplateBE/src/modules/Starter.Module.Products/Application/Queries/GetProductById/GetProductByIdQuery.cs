using MediatR;
using Starter.Module.Products.Application.DTOs;
using Starter.Shared.Results;

namespace Starter.Module.Products.Application.Queries.GetProductById;

public sealed record GetProductByIdQuery(Guid Id) : IRequest<Result<ProductDto>>;
