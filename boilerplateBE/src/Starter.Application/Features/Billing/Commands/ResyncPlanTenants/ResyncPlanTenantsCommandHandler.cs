using MediatR;
using Microsoft.EntityFrameworkCore;
using Starter.Application.Common.Interfaces;
using Starter.Domain.Billing.Errors;
using Starter.Domain.Billing.Enums;
using Starter.Shared.Results;

namespace Starter.Application.Features.Billing.Commands.ResyncPlanTenants;

internal sealed class ResyncPlanTenantsCommandHandler(
    IApplicationDbContext context) : IRequestHandler<ResyncPlanTenantsCommand, Result<int>>
{
    public async Task<Result<int>> Handle(ResyncPlanTenantsCommand request, CancellationToken cancellationToken)
    {
        var plan = await context.SubscriptionPlans
            .FirstOrDefaultAsync(p => p.Id == request.PlanId, cancellationToken);
        if (plan is null)
            return Result.Failure<int>(BillingErrors.PlanNotFound);

        var subscriptions = await context.TenantSubscriptions
            .IgnoreQueryFilters()
            .Where(s => s.SubscriptionPlanId == request.PlanId
                && s.Status != SubscriptionStatus.Canceled
                && s.Status != SubscriptionStatus.Expired)
            .ToListAsync(cancellationToken);

        foreach (var subscription in subscriptions)
        {
            subscription.ChangePlan(
                plan.Id,
                plan.MonthlyPrice,
                plan.AnnualPrice,
                plan.Currency,
                subscription.BillingInterval,
                subscription.CurrentPeriodStart,
                subscription.CurrentPeriodEnd);
        }

        await context.SaveChangesAsync(cancellationToken);
        return Result.Success(subscriptions.Count);
    }
}
