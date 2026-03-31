using MediatR;
using Starter.Domain.Billing.Enums;
using Starter.Shared.Results;

namespace Starter.Application.Features.Billing.Commands.ChangePlan;

public sealed record ChangePlanCommand(
    Guid PlanId,
    BillingInterval? Interval,
    Guid? TenantId) : IRequest<Result>;
