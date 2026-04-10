using MediatR;
using Starter.Abstractions.Capabilities;
using Starter.Shared.Results;

namespace Starter.Module.Billing.Application.Commands.ChangePlan;

public sealed record ChangePlanCommand(
    Guid PlanId,
    BillingInterval? Interval,
    Guid? TenantId) : IRequest<Result>;
