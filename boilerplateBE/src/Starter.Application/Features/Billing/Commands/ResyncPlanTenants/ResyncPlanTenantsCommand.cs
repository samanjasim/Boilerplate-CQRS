using MediatR;
using Starter.Shared.Results;

namespace Starter.Application.Features.Billing.Commands.ResyncPlanTenants;

public sealed record ResyncPlanTenantsCommand(Guid PlanId) : IRequest<Result<int>>;
