using MediatR;
using Starter.Shared.Results;

namespace Starter.Module.Billing.Application.Commands.ResyncPlanTenants;

public sealed record ResyncPlanTenantsCommand(Guid PlanId) : IRequest<Result<int>>;
