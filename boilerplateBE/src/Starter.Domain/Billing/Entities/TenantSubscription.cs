using Starter.Domain.Billing.Enums;
using Starter.Domain.Billing.Events;
using Starter.Domain.Common;

namespace Starter.Domain.Billing.Entities;

public sealed class TenantSubscription : AggregateRoot
{
    public Guid TenantId { get; private set; }
    public Guid SubscriptionPlanId { get; private set; }
    public SubscriptionStatus Status { get; private set; }
    public decimal LockedMonthlyPrice { get; private set; }
    public decimal LockedAnnualPrice { get; private set; }
    public string Currency { get; private set; } = default!;
    public string? ExternalCustomerId { get; private set; }
    public string? ExternalSubscriptionId { get; private set; }
    public BillingInterval BillingInterval { get; private set; }
    public DateTime CurrentPeriodStart { get; private set; }
    public DateTime CurrentPeriodEnd { get; private set; }
    public DateTime? TrialEndAt { get; private set; }
    public DateTime? CanceledAt { get; private set; }
    public bool AutoRenew { get; private set; }

    public SubscriptionPlan Plan { get; private set; } = default!;

    private readonly List<PaymentRecord> _payments = [];
    public IReadOnlyCollection<PaymentRecord> Payments => _payments.AsReadOnly();

    private TenantSubscription() { }

    private TenantSubscription(
        Guid id,
        Guid tenantId,
        Guid subscriptionPlanId,
        SubscriptionStatus status,
        decimal lockedMonthlyPrice,
        decimal lockedAnnualPrice,
        string currency,
        BillingInterval billingInterval,
        DateTime currentPeriodStart,
        DateTime currentPeriodEnd,
        DateTime? trialEndAt,
        bool autoRenew) : base(id)
    {
        TenantId = tenantId;
        SubscriptionPlanId = subscriptionPlanId;
        Status = status;
        LockedMonthlyPrice = lockedMonthlyPrice;
        LockedAnnualPrice = lockedAnnualPrice;
        Currency = currency;
        BillingInterval = billingInterval;
        CurrentPeriodStart = currentPeriodStart;
        CurrentPeriodEnd = currentPeriodEnd;
        TrialEndAt = trialEndAt;
        AutoRenew = autoRenew;
    }

    public static TenantSubscription Create(
        Guid tenantId,
        Guid subscriptionPlanId,
        decimal lockedMonthlyPrice,
        decimal lockedAnnualPrice,
        string currency,
        BillingInterval billingInterval,
        DateTime currentPeriodStart,
        DateTime currentPeriodEnd,
        DateTime? trialEndAt,
        bool autoRenew)
    {
        var status = trialEndAt.HasValue ? SubscriptionStatus.Trialing : SubscriptionStatus.Active;

        var subscription = new TenantSubscription(
            Guid.NewGuid(),
            tenantId,
            subscriptionPlanId,
            status,
            lockedMonthlyPrice,
            lockedAnnualPrice,
            currency.Trim().ToUpperInvariant(),
            billingInterval,
            currentPeriodStart,
            currentPeriodEnd,
            trialEndAt,
            autoRenew);

        subscription.RaiseDomainEvent(new SubscriptionChangedEvent(tenantId, null, subscriptionPlanId));

        return subscription;
    }

    public void ChangePlan(
        Guid newPlanId,
        decimal lockedMonthlyPrice,
        decimal lockedAnnualPrice,
        string currency,
        BillingInterval billingInterval,
        DateTime currentPeriodStart,
        DateTime currentPeriodEnd)
    {
        var oldPlanId = SubscriptionPlanId;

        SubscriptionPlanId = newPlanId;
        LockedMonthlyPrice = lockedMonthlyPrice;
        LockedAnnualPrice = lockedAnnualPrice;
        Currency = currency.Trim().ToUpperInvariant();
        BillingInterval = billingInterval;
        CurrentPeriodStart = currentPeriodStart;
        CurrentPeriodEnd = currentPeriodEnd;
        Status = SubscriptionStatus.Active;
        CanceledAt = null;
        ModifiedAt = DateTime.UtcNow;

        RaiseDomainEvent(new SubscriptionChangedEvent(TenantId, oldPlanId, newPlanId));
    }

    public void Cancel()
    {
        Status = SubscriptionStatus.Canceled;
        CanceledAt = DateTime.UtcNow;
        AutoRenew = false;
        ModifiedAt = DateTime.UtcNow;

        RaiseDomainEvent(new SubscriptionCanceledEvent(TenantId, Id));
    }

    public void SetExternalIds(string? externalCustomerId, string? externalSubscriptionId)
    {
        ExternalCustomerId = externalCustomerId?.Trim();
        ExternalSubscriptionId = externalSubscriptionId?.Trim();
        ModifiedAt = DateTime.UtcNow;
    }
}
