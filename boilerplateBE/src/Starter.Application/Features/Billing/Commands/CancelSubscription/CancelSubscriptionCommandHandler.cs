using MediatR;
using Microsoft.EntityFrameworkCore;
using Starter.Application.Common.Interfaces;
using Starter.Domain.Billing.Enums;
using Starter.Domain.Billing.Errors;
using Starter.Shared.Results;

namespace Starter.Application.Features.Billing.Commands.CancelSubscription;

internal sealed class CancelSubscriptionCommandHandler(
    IApplicationDbContext context,
    ICurrentUserService currentUser,
    IBillingProvider billingProvider) : IRequestHandler<CancelSubscriptionCommand, Result>
{
    public async Task<Result> Handle(CancelSubscriptionCommand request, CancellationToken cancellationToken)
    {
        var tenantId = request.TenantId ?? currentUser.TenantId;
        if (tenantId is null)
            return Result.Failure(BillingErrors.SubscriptionNotFound);

        var subscription = await context.TenantSubscriptions
            .IgnoreQueryFilters()
            .Include(s => s.Plan)
            .FirstOrDefaultAsync(s => s.TenantId == tenantId, cancellationToken);
        if (subscription is null)
            return Result.Failure(BillingErrors.SubscriptionNotFound);

        if (subscription.Plan.IsFree)
            return Result.Failure(BillingErrors.CannotCancelFreePlan);

        if (!string.IsNullOrEmpty(subscription.ExternalSubscriptionId))
        {
            await billingProvider.CancelSubscriptionAsync(
                subscription.ExternalSubscriptionId,
                cancellationToken);
        }

        subscription.Cancel();

        var freePlan = await context.SubscriptionPlans
            .FirstOrDefaultAsync(p => p.IsFree && p.IsActive, cancellationToken);
        if (freePlan is not null)
        {
            var now = DateTime.UtcNow;
            subscription.ChangePlan(
                freePlan.Id,
                freePlan.MonthlyPrice,
                freePlan.AnnualPrice,
                freePlan.Currency,
                BillingInterval.Monthly,
                now,
                now.AddYears(100));
        }

        await context.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
