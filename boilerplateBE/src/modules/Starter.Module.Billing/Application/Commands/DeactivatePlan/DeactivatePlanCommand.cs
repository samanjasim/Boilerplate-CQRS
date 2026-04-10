using MediatR;
using Starter.Shared.Results;

namespace Starter.Module.Billing.Application.Commands.DeactivatePlan;

public sealed record DeactivatePlanCommand(Guid Id) : IRequest<Result>;
