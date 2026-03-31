using Starter.Domain.Billing.Enums;

namespace Starter.Application.Common.Interfaces;

public interface IBillingProvider
{
    Task<CreateSubscriptionResult> CreateSubscriptionAsync(Guid tenantId, string planSlug, BillingInterval interval, CancellationToken ct = default);
    Task<ChangeSubscriptionResult> ChangeSubscriptionAsync(string externalSubscriptionId, string newPlanSlug, BillingInterval interval, CancellationToken ct = default);
    Task CancelSubscriptionAsync(string externalSubscriptionId, CancellationToken ct = default);
}

public sealed record CreateSubscriptionResult(string ExternalSubscriptionId, string ExternalCustomerId, DateTime PeriodStart, DateTime PeriodEnd);
public sealed record ChangeSubscriptionResult(DateTime NewPeriodStart, DateTime NewPeriodEnd, decimal ProratedAmount);
