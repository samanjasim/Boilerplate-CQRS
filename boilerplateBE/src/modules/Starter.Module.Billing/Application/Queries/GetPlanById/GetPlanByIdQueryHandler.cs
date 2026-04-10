using MediatR;
using Microsoft.EntityFrameworkCore;
using Starter.Module.Billing.Domain.Enums;
using Starter.Module.Billing.Domain.Errors;
using Starter.Module.Billing.Application.DTOs;
using Starter.Module.Billing.Infrastructure.Persistence;
using Starter.Shared.Results;

namespace Starter.Module.Billing.Application.Queries.GetPlanById;

internal sealed class GetPlanByIdQueryHandler(
    BillingDbContext context) : IRequestHandler<GetPlanByIdQuery, Result<SubscriptionPlanDto>>
{
    public async Task<Result<SubscriptionPlanDto>> Handle(
        GetPlanByIdQuery request, CancellationToken cancellationToken)
    {
        var plan = await context.SubscriptionPlans.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == request.Id, cancellationToken);

        if (plan is null)
            return Result.Failure<SubscriptionPlanDto>(BillingErrors.PlanNotFound);

        var subscriberCount = await context.TenantSubscriptions
            .IgnoreQueryFilters()
            .CountAsync(s => s.SubscriptionPlanId == plan.Id && s.Status == SubscriptionStatus.Active,
                cancellationToken);

        return Result.Success(plan.ToDto(subscriberCount));
    }
}
