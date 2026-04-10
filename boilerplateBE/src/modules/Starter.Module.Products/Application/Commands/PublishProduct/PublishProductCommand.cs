using MediatR;
using Starter.Shared.Results;

namespace Starter.Module.Products.Application.Commands.PublishProduct;

public sealed record PublishProductCommand(Guid Id) : IRequest<Result>;
