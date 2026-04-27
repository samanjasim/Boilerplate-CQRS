using MediatR;
using Starter.Shared.Results;

namespace Starter.Module.AI.Application.Commands.DeactivateModelPricing;

public sealed record DeactivateModelPricingCommand(Guid Id) : IRequest<Result>;
