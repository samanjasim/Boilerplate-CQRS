using Starter.Domain.Common;

namespace Starter.Module.Billing.Domain.Entities;

public sealed class SubscriptionPlan : AggregateRoot
{
    public string Name { get; private set; } = default!;
    public string Slug { get; private set; } = default!;
    public string? Description { get; private set; }
    public string? Translations { get; private set; }
    public decimal MonthlyPrice { get; private set; }
    public decimal AnnualPrice { get; private set; }
    public string Currency { get; private set; } = default!;
    public string? Features { get; private set; }
    public bool IsFree { get; private set; }
    public bool IsActive { get; private set; }
    public bool IsPublic { get; private set; }
    public int DisplayOrder { get; private set; }
    public int TrialDays { get; private set; }

    private readonly List<TenantSubscription> _subscriptions = [];
    public IReadOnlyCollection<TenantSubscription> Subscriptions => _subscriptions.AsReadOnly();

    private readonly List<PlanPriceHistory> _priceHistory = [];
    public IReadOnlyCollection<PlanPriceHistory> PriceHistory => _priceHistory.AsReadOnly();

    private SubscriptionPlan() { }

    private SubscriptionPlan(
        Guid id,
        string name,
        string slug,
        string? description,
        string? translations,
        decimal monthlyPrice,
        decimal annualPrice,
        string currency,
        string? features,
        bool isFree,
        bool isPublic,
        int displayOrder,
        int trialDays) : base(id)
    {
        Name = name;
        Slug = slug;
        Description = description;
        Translations = translations;
        MonthlyPrice = monthlyPrice;
        AnnualPrice = annualPrice;
        Currency = currency;
        Features = features;
        IsFree = isFree;
        IsActive = true;
        IsPublic = isPublic;
        DisplayOrder = displayOrder;
        TrialDays = trialDays;
    }

    public static SubscriptionPlan Create(
        string name,
        string slug,
        string? description,
        string? translations,
        decimal monthlyPrice,
        decimal annualPrice,
        string currency,
        string? features,
        bool isFree,
        bool isPublic,
        int displayOrder,
        int trialDays)
    {
        return new SubscriptionPlan(
            Guid.NewGuid(),
            name.Trim(),
            slug.Trim().ToLowerInvariant(),
            description?.Trim(),
            translations,
            monthlyPrice,
            annualPrice,
            currency.Trim().ToUpperInvariant(),
            features,
            isFree,
            isPublic,
            displayOrder,
            trialDays);
    }

    public void Update(
        string name,
        string? description,
        string? translations,
        decimal monthlyPrice,
        decimal annualPrice,
        string? features,
        bool isPublic,
        int displayOrder,
        int trialDays)
    {
        Name = name.Trim();
        Description = description?.Trim();
        Translations = translations;
        MonthlyPrice = monthlyPrice;
        AnnualPrice = annualPrice;
        Features = features;
        IsPublic = isPublic;
        DisplayOrder = displayOrder;
        TrialDays = trialDays;
        ModifiedAt = DateTime.UtcNow;
    }

    public void Deactivate()
    {
        IsActive = false;
        ModifiedAt = DateTime.UtcNow;
    }

    public void Activate()
    {
        IsActive = true;
        ModifiedAt = DateTime.UtcNow;
    }
}
