using MediatR;
using Microsoft.EntityFrameworkCore;
using Starter.Application.Common.Interfaces;
using Starter.Application.Features.Billing.DTOs;
using Starter.Domain.Billing.Errors;
using Starter.Domain.Billing.Enums;
using Starter.Shared.Results;

namespace Starter.Application.Features.Billing.Queries.GetPlanById;

internal sealed class GetPlanByIdQueryHandler(
    IApplicationDbContext context) : IRequestHandler<GetPlanByIdQuery, Result<SubscriptionPlanDto>>
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
