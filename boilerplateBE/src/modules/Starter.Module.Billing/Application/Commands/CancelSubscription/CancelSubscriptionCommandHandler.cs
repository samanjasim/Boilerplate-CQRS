using MediatR;
using Microsoft.EntityFrameworkCore;
using Starter.Abstractions.Capabilities;
using Starter.Application.Common.Interfaces;
using Starter.Module.Billing.Domain.Enums;
using Starter.Module.Billing.Domain.Errors;
using Starter.Module.Billing.Infrastructure.Persistence;
using Starter.Shared.Results;

namespace Starter.Module.Billing.Application.Commands.CancelSubscription;

internal sealed class CancelSubscriptionCommandHandler(
    BillingDbContext context,
    ICurrentUserService currentUser,
    IBillingProvider billingProvider,
    IWebhookPublisher webhookPublisher) : IRequestHandler<CancelSubscriptionCommand, Result>
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

        // Cross-module side effect via capability. Webhooks module picks this
        // up if installed; NullWebhookPublisher no-ops otherwise.
        await webhookPublisher.PublishAsync(
            eventType: "subscription.canceled",
            tenantId: tenantId,
            data: new
            {
                tenantId = tenantId,
                subscriptionId = subscription.Id
            },
            cancellationToken: cancellationToken);

        return Result.Success();
    }
}
