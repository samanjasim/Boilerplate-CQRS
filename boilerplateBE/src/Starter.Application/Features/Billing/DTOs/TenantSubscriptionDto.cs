using Starter.Domain.Billing.Enums;

namespace Starter.Application.Features.Billing.DTOs;

public sealed record TenantSubscriptionDto(
    Guid Id, Guid TenantId, Guid SubscriptionPlanId, string PlanName, string PlanSlug,
    SubscriptionStatus Status, decimal LockedMonthlyPrice, decimal LockedAnnualPrice,
    string Currency, BillingInterval BillingInterval, DateTime CurrentPeriodStart,
    DateTime CurrentPeriodEnd, DateTime? CanceledAt, bool AutoRenew, DateTime CreatedAt);
