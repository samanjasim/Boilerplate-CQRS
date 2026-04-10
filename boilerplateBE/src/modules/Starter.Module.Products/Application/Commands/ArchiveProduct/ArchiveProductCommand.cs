using MediatR;
using Starter.Shared.Results;

namespace Starter.Module.Products.Application.Commands.ArchiveProduct;

public sealed record ArchiveProductCommand(Guid Id) : IRequest<Result>;
