using Starter.Module.Billing.Domain.Entities;

namespace Starter.Module.Billing.Application.DTOs;

public static class TenantSubscriptionMapper
{
    public static TenantSubscriptionDto ToDto(this TenantSubscription entity)
    {
        return new TenantSubscriptionDto(
            Id: entity.Id,
            TenantId: entity.TenantId,
            SubscriptionPlanId: entity.SubscriptionPlanId,
            PlanName: entity.Plan?.Name ?? string.Empty,
            PlanSlug: entity.Plan?.Slug ?? string.Empty,
            Status: entity.Status,
            LockedMonthlyPrice: entity.LockedMonthlyPrice,
            LockedAnnualPrice: entity.LockedAnnualPrice,
            Currency: entity.Currency,
            BillingInterval: entity.BillingInterval,
            CurrentPeriodStart: entity.CurrentPeriodStart,
            CurrentPeriodEnd: entity.CurrentPeriodEnd,
            CanceledAt: entity.CanceledAt,
            AutoRenew: entity.AutoRenew,
            CreatedAt: entity.CreatedAt);
    }
}
