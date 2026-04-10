using MediatR;
using Microsoft.EntityFrameworkCore;
using Starter.Abstractions.Capabilities;
using Starter.Application.Common.Interfaces;
using Starter.Module.Billing.Domain.Entities;
using Starter.Module.Billing.Domain.Enums;
using Starter.Module.Billing.Domain.Errors;
using Starter.Module.Billing.Infrastructure.Persistence;
using Starter.Shared.Results;

namespace Starter.Module.Billing.Application.Commands.ChangePlan;

internal sealed class ChangePlanCommandHandler(
    BillingDbContext context,
    ICurrentUserService currentUser,
    IBillingProvider billingProvider,
    IWebhookPublisher webhookPublisher) : IRequestHandler<ChangePlanCommand, Result>
{
    public async Task<Result> Handle(ChangePlanCommand request, CancellationToken cancellationToken)
    {
        var tenantId = request.TenantId ?? currentUser.TenantId;
        if (tenantId is null)
            return Result.Failure(BillingErrors.SubscriptionNotFound);

        var subscription = await context.TenantSubscriptions
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(s => s.TenantId == tenantId, cancellationToken);
        if (subscription is null)
            return Result.Failure(BillingErrors.SubscriptionNotFound);

        if (subscription.SubscriptionPlanId == request.PlanId)
            return Result.Failure(BillingErrors.AlreadyOnPlan);

        var newPlan = await context.SubscriptionPlans
            .FirstOrDefaultAsync(p => p.Id == request.PlanId && p.IsActive, cancellationToken);
        if (newPlan is null)
            return Result.Failure(BillingErrors.PlanNotFound);

        var interval = request.Interval ?? subscription.BillingInterval;

        DateTime periodStart;
        DateTime periodEnd;
        decimal proratedAmount = 0;

        if (!string.IsNullOrEmpty(subscription.ExternalSubscriptionId))
        {
            var result = await billingProvider.ChangeSubscriptionAsync(
                subscription.ExternalSubscriptionId,
                newPlan.Slug,
                interval,
                cancellationToken);
            periodStart = result.NewPeriodStart;
            periodEnd = result.NewPeriodEnd;
            proratedAmount = result.ProratedAmount;
        }
        else
        {
            periodStart = DateTime.UtcNow;
            periodEnd = interval == BillingInterval.Annual
                ? periodStart.AddYears(1)
                : periodStart.AddMonths(1);
        }

        var oldPlanId = subscription.SubscriptionPlanId;

        subscription.ChangePlan(
            newPlan.Id,
            newPlan.MonthlyPrice,
            newPlan.AnnualPrice,
            newPlan.Currency,
            interval,
            periodStart,
            periodEnd);

        var price = interval == BillingInterval.Annual ? newPlan.AnnualPrice : newPlan.MonthlyPrice;
        var effectivePrice = proratedAmount > 0 ? proratedAmount : price;

        if (effectivePrice > 0)
        {
            var payment = PaymentRecord.Create(
                tenantId.Value,
                subscription.Id,
                effectivePrice,
                newPlan.Currency,
                PaymentStatus.Pending,
                null,
                null,
                $"Plan change to {newPlan.Name}",
                periodStart,
                periodEnd);

            context.PaymentRecords.Add(payment);
        }

        await context.SaveChangesAsync(cancellationToken);

        // Cross-module side effect via capability. If the Webhooks module is
        // installed, this dispatches a "subscription.changed" event to any
        // subscribed webhook endpoints. If not, NullWebhookPublisher silently
        // no-ops — Billing doesn't care which case applies.
        await webhookPublisher.PublishAsync(
            eventType: "subscription.changed",
            tenantId: tenantId,
            data: new
            {
                tenantId = tenantId,
                oldPlanId = oldPlanId,
                newPlanId = newPlan.Id
            },
            cancellationToken: cancellationToken);

        return Result.Success();
    }
}
