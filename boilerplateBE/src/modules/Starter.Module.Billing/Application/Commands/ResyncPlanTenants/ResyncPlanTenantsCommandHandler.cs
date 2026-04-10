using MediatR;
using Microsoft.EntityFrameworkCore;
using Starter.Module.Billing.Domain.Enums;
using Starter.Module.Billing.Domain.Errors;
using Starter.Module.Billing.Infrastructure.Persistence;
using Starter.Shared.Results;

namespace Starter.Module.Billing.Application.Commands.ResyncPlanTenants;

internal sealed class ResyncPlanTenantsCommandHandler(
    BillingDbContext context) : IRequestHandler<ResyncPlanTenantsCommand, Result<int>>
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
            // Only raise domain event to resync feature flags — don't touch prices/dates
            subscription.ResyncFeatures();
        }

        await context.SaveChangesAsync(cancellationToken);
        return Result.Success(subscriptions.Count);
    }
}
