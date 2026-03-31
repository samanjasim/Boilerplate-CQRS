using Starter.Domain.Billing.Enums;

namespace Starter.Application.Features.Billing.DTOs;

public sealed record SubscriptionSummaryDto(
    Guid TenantId,
    string TenantName,
    string? TenantSlug,
    Guid SubscriptionPlanId,
    string PlanName,
    string PlanSlug,
    SubscriptionStatus Status,
    BillingInterval BillingInterval,
    DateTime CurrentPeriodStart,
    DateTime CurrentPeriodEnd,
    long UsersCount,
    int MaxUsers,
    long StorageUsedMb,
    long MaxStorageMb,
    PaymentStatus? LatestPaymentStatus,
    DateTime CreatedAt);
