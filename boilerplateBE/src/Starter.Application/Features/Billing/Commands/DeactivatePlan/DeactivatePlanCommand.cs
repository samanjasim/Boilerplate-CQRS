using MediatR;
using Starter.Shared.Results;

namespace Starter.Application.Features.Billing.Commands.DeactivatePlan;

public sealed record DeactivatePlanCommand(Guid Id) : IRequest<Result>;
