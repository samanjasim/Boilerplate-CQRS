using Starter.Domain.Common;

namespace Starter.Domain.Billing.Entities;

public sealed class PlanPriceHistory : BaseEntity
{
    public Guid SubscriptionPlanId { get; private set; }
    public decimal MonthlyPrice { get; private set; }
    public decimal AnnualPrice { get; private set; }
    public string Currency { get; private set; } = default!;
    public Guid ChangedBy { get; private set; }
    public string? Reason { get; private set; }
    public DateTime EffectiveFrom { get; private set; }

    public SubscriptionPlan Plan { get; private set; } = default!;

    private PlanPriceHistory() { }

    private PlanPriceHistory(
        Guid id,
        Guid subscriptionPlanId,
        decimal monthlyPrice,
        decimal annualPrice,
        string currency,
        Guid changedBy,
        string? reason,
        DateTime effectiveFrom) : base(id)
    {
        SubscriptionPlanId = subscriptionPlanId;
        MonthlyPrice = monthlyPrice;
        AnnualPrice = annualPrice;
        Currency = currency;
        ChangedBy = changedBy;
        Reason = reason;
        EffectiveFrom = effectiveFrom;
    }

    public static PlanPriceHistory Create(
        Guid subscriptionPlanId,
        decimal monthlyPrice,
        decimal annualPrice,
        string currency,
        Guid changedBy,
        string? reason,
        DateTime? effectiveFrom = null)
    {
        return new PlanPriceHistory(
            Guid.NewGuid(),
            subscriptionPlanId,
            monthlyPrice,
            annualPrice,
            currency.Trim().ToUpperInvariant(),
            changedBy,
            reason?.Trim(),
            effectiveFrom ?? DateTime.UtcNow);
    }
}
