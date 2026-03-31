# Billing & Subscriptions Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implement a feature-flag-driven billing system where subscription plans are "feature flag presets" — plans sync limits into tenant feature flag overrides, with Redis usage counters for O(1) enforcement.

**Architecture:** SubscriptionPlan holds a Features JSON mapping flag keys to values. On plan change, SyncPlanFeaturesHandler writes TenantFeatureFlag overrides with OverrideSource.PlanSubscription. IUsageTracker (Redis INCR/DECR) replaces COUNT/SUM queries. IBillingProvider abstraction with MockBillingProvider for instant activation.

**Tech Stack:** .NET 10, EF Core, PostgreSQL, Redis, MediatR, FluentValidation, React 19, TanStack Query, shadcn/ui, i18next

**Spec:** `docs/superpowers/specs/2026-03-31-billing-subscriptions-design.md`

---

## File Structure

### Backend — New Files

```
boilerplateBE/src/Starter.Domain/Billing/
├── Entities/
│   ├── SubscriptionPlan.cs
│   ├── TenantSubscription.cs
│   ├── PaymentRecord.cs
│   └── PlanPriceHistory.cs
├── Enums/
│   ├── SubscriptionStatus.cs
│   ├── BillingInterval.cs
│   └── PaymentStatus.cs
├── Errors/
│   └── BillingErrors.cs
└── Events/
    ├── SubscriptionChangedEvent.cs
    └── SubscriptionCanceledEvent.cs

boilerplateBE/src/Starter.Domain/FeatureFlags/Enums/
└── OverrideSource.cs                    (NEW)

boilerplateBE/src/Starter.Application/Common/Interfaces/
├── IUsageTracker.cs                     (NEW)
└── IBillingProvider.cs                  (NEW)

boilerplateBE/src/Starter.Application/Features/Billing/
├── DTOs/
│   ├── SubscriptionPlanDto.cs
│   ├── SubscriptionPlanMapper.cs
│   ├── TenantSubscriptionDto.cs
│   ├── TenantSubscriptionMapper.cs
│   ├── PaymentRecordDto.cs
│   ├── PaymentRecordMapper.cs
│   └── UsageDto.cs
├── Commands/
│   ├── CreatePlan/
│   │   ├── CreatePlanCommand.cs
│   │   ├── CreatePlanCommandHandler.cs
│   │   └── CreatePlanCommandValidator.cs
│   ├── UpdatePlan/
│   │   ├── UpdatePlanCommand.cs
│   │   ├── UpdatePlanCommandHandler.cs
│   │   └── UpdatePlanCommandValidator.cs
│   ├── DeactivatePlan/
│   │   ├── DeactivatePlanCommand.cs
│   │   └── DeactivatePlanCommandHandler.cs
│   ├── ChangePlan/
│   │   ├── ChangePlanCommand.cs
│   │   ├── ChangePlanCommandHandler.cs
│   │   └── ChangePlanCommandValidator.cs
│   ├── CancelSubscription/
│   │   ├── CancelSubscriptionCommand.cs
│   │   └── CancelSubscriptionCommandHandler.cs
│   └── ResyncPlanTenants/
│       ├── ResyncPlanTenantsCommand.cs
│       └── ResyncPlanTenantsCommandHandler.cs
├── Queries/
│   ├── GetPlans/
│   │   ├── GetPlansQuery.cs
│   │   └── GetPlansQueryHandler.cs
│   ├── GetPlanById/
│   │   ├── GetPlanByIdQuery.cs
│   │   └── GetPlanByIdQueryHandler.cs
│   ├── GetSubscription/
│   │   ├── GetSubscriptionQuery.cs
│   │   └── GetSubscriptionQueryHandler.cs
│   ├── GetPayments/
│   │   ├── GetPaymentsQuery.cs
│   │   └── GetPaymentsQueryHandler.cs
│   └── GetUsage/
│       ├── GetUsageQuery.cs
│       └── GetUsageQueryHandler.cs
└── EventHandlers/
    └── SyncPlanFeaturesHandler.cs

boilerplateBE/src/Starter.Infrastructure/
├── Persistence/Configurations/
│   ├── SubscriptionPlanConfiguration.cs  (NEW)
│   ├── TenantSubscriptionConfiguration.cs (NEW)
│   ├── PaymentRecordConfiguration.cs     (NEW)
│   └── PlanPriceHistoryConfiguration.cs  (NEW)
└── Services/
    ├── UsageTrackerService.cs            (NEW)
    └── MockBillingProvider.cs            (NEW)

boilerplateBE/src/Starter.Api/Controllers/
└── BillingController.cs                  (NEW)
```

### Backend — Modified Files

```
boilerplateBE/src/Starter.Application/Common/Interfaces/IApplicationDbContext.cs   (+4 DbSets)
boilerplateBE/src/Starter.Infrastructure/Persistence/ApplicationDbContext.cs        (+4 DbSets, +2 query filters)
boilerplateBE/src/Starter.Infrastructure/Persistence/Seeds/DataSeeder.cs           (+SeedBillingPlansAsync)
boilerplateBE/src/Starter.Infrastructure/Persistence/Configurations/TenantFeatureFlagConfiguration.cs (+OverrideSource column)
boilerplateBE/src/Starter.Domain/FeatureFlags/Entities/TenantFeatureFlag.cs        (+OverrideSource property)
boilerplateBE/src/Starter.Shared/Constants/Permissions.cs                          (+Billing module)
boilerplateBE/src/Starter.Shared/Constants/Roles.cs                                (+Billing perms to roles)
boilerplateBE/src/Starter.Infrastructure/DependencyInjection.cs                    (+service registrations)
boilerplateBE/src/Starter.Application/Features/Auth/Commands/Register/RegisterUserCommandHandler.cs     (→IUsageTracker)
boilerplateBE/src/Starter.Application/Features/Files/Commands/UploadFile/UploadFileCommandHandler.cs    (→IUsageTracker)
boilerplateBE/src/Starter.Application/Features/ApiKeys/Commands/CreateApiKey/CreateApiKeyCommandHandler.cs (→IUsageTracker)
boilerplateBE/src/Starter.Application/Features/Tenants/Commands/RegisterTenant/RegisterTenantCommandHandler.cs (+auto-assign Free plan)
```

### Frontend — New Files

```
boilerplateFE/src/types/billing.types.ts
boilerplateFE/src/features/billing/
├── api/
│   ├── billing.api.ts
│   ├── billing.queries.ts
│   └── index.ts
├── pages/
│   ├── BillingPage.tsx           (tenant: subscription + usage + payments)
│   ├── BillingPlansPage.tsx      (SuperAdmin: plan CRUD)
│   └── PricingPage.tsx           (public: plan comparison)
├── components/
│   ├── PlanCard.tsx
│   ├── UsageBar.tsx
│   ├── PlanSelectorModal.tsx
│   ├── CreatePlanDialog.tsx
│   ├── EditPlanDialog.tsx
│   └── FeatureMappingEditor.tsx
└── index.ts
```

### Frontend — Modified Files

```
boilerplateFE/src/config/api.config.ts         (+billing endpoints)
boilerplateFE/src/config/routes.config.ts       (+billing routes)
boilerplateFE/src/routes/routes.tsx             (+billing pages)
boilerplateFE/src/constants/permissions.ts      (+billing permissions)
boilerplateFE/src/components/layout/MainLayout/Sidebar.tsx  (+billing nav item)
boilerplateFE/src/lib/query/keys.ts             (+billing query keys)
boilerplateFE/src/i18n/locales/en/translation.json  (+billing translations)
boilerplateFE/src/i18n/locales/ar/translation.json  (+billing translations)
boilerplateFE/src/i18n/locales/ku/translation.json  (+billing translations)
```

---

## Task 1: Domain Enums + OverrideSource

**Files:**
- Create: `boilerplateBE/src/Starter.Domain/Billing/Enums/SubscriptionStatus.cs`
- Create: `boilerplateBE/src/Starter.Domain/Billing/Enums/BillingInterval.cs`
- Create: `boilerplateBE/src/Starter.Domain/Billing/Enums/PaymentStatus.cs`
- Create: `boilerplateBE/src/Starter.Domain/FeatureFlags/Enums/OverrideSource.cs`
- Modify: `boilerplateBE/src/Starter.Domain/FeatureFlags/Entities/TenantFeatureFlag.cs`

- [ ] **Step 1:** Create `SubscriptionStatus.cs`

```csharp
namespace Starter.Domain.Billing.Enums;

public enum SubscriptionStatus
{
    Trialing = 0,
    Active = 1,
    PastDue = 2,
    Canceled = 3,
    Expired = 4
}
```

- [ ] **Step 2:** Create `BillingInterval.cs`

```csharp
namespace Starter.Domain.Billing.Enums;

public enum BillingInterval
{
    Monthly = 0,
    Annual = 1
}
```

- [ ] **Step 3:** Create `PaymentStatus.cs`

```csharp
namespace Starter.Domain.Billing.Enums;

public enum PaymentStatus
{
    Pending = 0,
    Completed = 1,
    Failed = 2,
    Refunded = 3
}
```

- [ ] **Step 4:** Create `OverrideSource.cs`

```csharp
namespace Starter.Domain.FeatureFlags.Enums;

public enum OverrideSource
{
    Manual = 0,
    PlanSubscription = 1
}
```

- [ ] **Step 5:** Add `OverrideSource` property to `TenantFeatureFlag.cs`

Add after the `Value` property:

```csharp
public OverrideSource Source { get; private set; } = OverrideSource.Manual;
```

Update the `Create` factory method to accept optional source parameter:

```csharp
public static TenantFeatureFlag Create(Guid tenantId, Guid featureFlagId, string value, OverrideSource source = OverrideSource.Manual)
{
    return new TenantFeatureFlag(Guid.NewGuid(), tenantId, featureFlagId, value) { Source = source };
}
```

Add update method:

```csharp
public void UpdateValue(string value, OverrideSource source)
{
    Value = value;
    Source = source;
    ModifiedAt = DateTime.UtcNow;
}
```

- [ ] **Step 6:** Update `TenantFeatureFlagConfiguration.cs` to map the new column

Add after the `Value` property mapping:

```csharp
builder.Property(t => t.Source).HasColumnName("override_source").HasDefaultValue(OverrideSource.Manual).IsRequired();
```

- [ ] **Step 7:** Verify build

Run: `dotnet build` from `boilerplateBE/`
Expected: 0 errors

- [ ] **Step 8:** Commit

```
feat(domain): add billing enums and OverrideSource to TenantFeatureFlag
```

---

## Task 2: Domain Entities (SubscriptionPlan, TenantSubscription, PaymentRecord, PlanPriceHistory)

**Files:**
- Create: `boilerplateBE/src/Starter.Domain/Billing/Entities/SubscriptionPlan.cs`
- Create: `boilerplateBE/src/Starter.Domain/Billing/Entities/TenantSubscription.cs`
- Create: `boilerplateBE/src/Starter.Domain/Billing/Entities/PaymentRecord.cs`
- Create: `boilerplateBE/src/Starter.Domain/Billing/Entities/PlanPriceHistory.cs`
- Create: `boilerplateBE/src/Starter.Domain/Billing/Errors/BillingErrors.cs`
- Create: `boilerplateBE/src/Starter.Domain/Billing/Events/SubscriptionChangedEvent.cs`
- Create: `boilerplateBE/src/Starter.Domain/Billing/Events/SubscriptionCanceledEvent.cs`

- [ ] **Step 1:** Create `SubscriptionPlan.cs`

```csharp
using Starter.Domain.Common;

namespace Starter.Domain.Billing.Entities;

public sealed class SubscriptionPlan : AggregateRoot
{
    public string Name { get; private set; } = default!;
    public string Slug { get; private set; } = default!;
    public string? Description { get; private set; }
    public string? Translations { get; private set; }
    public decimal MonthlyPrice { get; private set; }
    public decimal AnnualPrice { get; private set; }
    public string Currency { get; private set; } = "USD";
    public string Features { get; private set; } = "{}";
    public bool IsFree { get; private set; }
    public bool IsActive { get; private set; } = true;
    public bool IsPublic { get; private set; } = true;
    public int DisplayOrder { get; private set; }
    public int TrialDays { get; private set; }

    private readonly List<TenantSubscription> _subscriptions = [];
    public IReadOnlyCollection<TenantSubscription> Subscriptions => _subscriptions.AsReadOnly();

    private readonly List<PlanPriceHistory> _priceHistory = [];
    public IReadOnlyCollection<PlanPriceHistory> PriceHistory => _priceHistory.AsReadOnly();

    private SubscriptionPlan() { }

    private SubscriptionPlan(
        Guid id, string name, string slug, string? description, string? translations,
        decimal monthlyPrice, decimal annualPrice, string currency, string features,
        bool isFree, bool isPublic, int displayOrder, int trialDays) : base(id)
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
        IsPublic = isPublic;
        DisplayOrder = displayOrder;
        TrialDays = trialDays;
    }

    public static SubscriptionPlan Create(
        string name, string slug, string? description, string? translations,
        decimal monthlyPrice, decimal annualPrice, string currency, string features,
        bool isFree, bool isPublic, int displayOrder, int trialDays = 0)
    {
        return new SubscriptionPlan(Guid.NewGuid(), name, slug.Trim().ToLowerInvariant(),
            description, translations, monthlyPrice, annualPrice, currency, features,
            isFree, isPublic, displayOrder, trialDays);
    }

    public void Update(
        string name, string? description, string? translations, decimal monthlyPrice,
        decimal annualPrice, string currency, string features, bool isPublic, int displayOrder, int trialDays)
    {
        Name = name;
        Description = description;
        Translations = translations;
        MonthlyPrice = monthlyPrice;
        AnnualPrice = annualPrice;
        Currency = currency;
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
```

- [ ] **Step 2:** Create `TenantSubscription.cs`

```csharp
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
    public string Currency { get; private set; } = "USD";
    public string? ExternalCustomerId { get; private set; }
    public string? ExternalSubscriptionId { get; private set; }
    public BillingInterval BillingInterval { get; private set; }
    public DateTime CurrentPeriodStart { get; private set; }
    public DateTime CurrentPeriodEnd { get; private set; }
    public DateTime? TrialEndAt { get; private set; }
    public DateTime? CanceledAt { get; private set; }
    public bool AutoRenew { get; private set; } = true;

    public SubscriptionPlan Plan { get; private set; } = default!;

    private readonly List<PaymentRecord> _payments = [];
    public IReadOnlyCollection<PaymentRecord> Payments => _payments.AsReadOnly();

    private TenantSubscription() { }

    private TenantSubscription(
        Guid id, Guid tenantId, Guid planId, SubscriptionStatus status,
        decimal lockedMonthlyPrice, decimal lockedAnnualPrice, string currency,
        string? externalCustomerId, string? externalSubscriptionId,
        BillingInterval interval, DateTime periodStart, DateTime periodEnd) : base(id)
    {
        TenantId = tenantId;
        SubscriptionPlanId = planId;
        Status = status;
        LockedMonthlyPrice = lockedMonthlyPrice;
        LockedAnnualPrice = lockedAnnualPrice;
        Currency = currency;
        ExternalCustomerId = externalCustomerId;
        ExternalSubscriptionId = externalSubscriptionId;
        BillingInterval = interval;
        CurrentPeriodStart = periodStart;
        CurrentPeriodEnd = periodEnd;
    }

    public static TenantSubscription Create(
        Guid tenantId, Guid planId, decimal lockedMonthlyPrice, decimal lockedAnnualPrice,
        string currency, BillingInterval interval, DateTime periodStart, DateTime periodEnd,
        string? externalCustomerId = null, string? externalSubscriptionId = null)
    {
        var sub = new TenantSubscription(Guid.NewGuid(), tenantId, planId, SubscriptionStatus.Active,
            lockedMonthlyPrice, lockedAnnualPrice, currency, externalCustomerId, externalSubscriptionId,
            interval, periodStart, periodEnd);

        sub.AddDomainEvent(new SubscriptionChangedEvent(tenantId, null, planId));
        return sub;
    }

    public void ChangePlan(Guid newPlanId, decimal lockedMonthlyPrice, decimal lockedAnnualPrice,
        string currency, DateTime periodStart, DateTime periodEnd,
        string? externalSubscriptionId = null)
    {
        var oldPlanId = SubscriptionPlanId;
        SubscriptionPlanId = newPlanId;
        LockedMonthlyPrice = lockedMonthlyPrice;
        LockedAnnualPrice = lockedAnnualPrice;
        Currency = currency;
        CurrentPeriodStart = periodStart;
        CurrentPeriodEnd = periodEnd;
        ExternalSubscriptionId = externalSubscriptionId;
        Status = SubscriptionStatus.Active;
        CanceledAt = null;
        ModifiedAt = DateTime.UtcNow;

        AddDomainEvent(new SubscriptionChangedEvent(TenantId, oldPlanId, newPlanId));
    }

    public void Cancel()
    {
        Status = SubscriptionStatus.Canceled;
        CanceledAt = DateTime.UtcNow;
        AutoRenew = false;
        ModifiedAt = DateTime.UtcNow;

        AddDomainEvent(new SubscriptionCanceledEvent(TenantId, Id));
    }

    public void SetExternalIds(string customerId, string subscriptionId)
    {
        ExternalCustomerId = customerId;
        ExternalSubscriptionId = subscriptionId;
        ModifiedAt = DateTime.UtcNow;
    }
}
```

- [ ] **Step 3:** Create `PaymentRecord.cs`

```csharp
using Starter.Domain.Billing.Enums;
using Starter.Domain.Common;

namespace Starter.Domain.Billing.Entities;

public sealed class PaymentRecord : BaseEntity
{
    public Guid TenantId { get; private set; }
    public Guid TenantSubscriptionId { get; private set; }
    public decimal Amount { get; private set; }
    public string Currency { get; private set; } = "USD";
    public PaymentStatus Status { get; private set; }
    public string? ExternalPaymentId { get; private set; }
    public string? InvoiceUrl { get; private set; }
    public string? Description { get; private set; }
    public DateTime PeriodStart { get; private set; }
    public DateTime PeriodEnd { get; private set; }

    public TenantSubscription Subscription { get; private set; } = default!;

    private PaymentRecord() { }

    public static PaymentRecord Create(
        Guid tenantId, Guid subscriptionId, decimal amount, string currency,
        PaymentStatus status, string? description, DateTime periodStart, DateTime periodEnd,
        string? externalPaymentId = null, string? invoiceUrl = null)
    {
        return new PaymentRecord
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            TenantSubscriptionId = subscriptionId,
            Amount = amount,
            Currency = currency,
            Status = status,
            Description = description,
            PeriodStart = periodStart,
            PeriodEnd = periodEnd,
            ExternalPaymentId = externalPaymentId,
            InvoiceUrl = invoiceUrl
        };
    }
}
```

- [ ] **Step 4:** Create `PlanPriceHistory.cs`

```csharp
using Starter.Domain.Common;

namespace Starter.Domain.Billing.Entities;

public sealed class PlanPriceHistory : BaseEntity
{
    public Guid SubscriptionPlanId { get; private set; }
    public decimal MonthlyPrice { get; private set; }
    public decimal AnnualPrice { get; private set; }
    public string Currency { get; private set; } = "USD";
    public Guid ChangedBy { get; private set; }
    public string? Reason { get; private set; }
    public DateTime EffectiveFrom { get; private set; }

    public SubscriptionPlan Plan { get; private set; } = default!;

    private PlanPriceHistory() { }

    public static PlanPriceHistory Create(
        Guid planId, decimal monthlyPrice, decimal annualPrice, string currency,
        Guid changedBy, string? reason = null)
    {
        return new PlanPriceHistory
        {
            Id = Guid.NewGuid(),
            SubscriptionPlanId = planId,
            MonthlyPrice = monthlyPrice,
            AnnualPrice = annualPrice,
            Currency = currency,
            ChangedBy = changedBy,
            Reason = reason,
            EffectiveFrom = DateTime.UtcNow
        };
    }
}
```

- [ ] **Step 5:** Create `BillingErrors.cs`

```csharp
using Starter.Shared.Results;

namespace Starter.Domain.Billing.Errors;

public static class BillingErrors
{
    public static readonly Error PlanNotFound = Error.NotFound(
        "Billing.PlanNotFound", "The specified subscription plan was not found.");

    public static readonly Error PlanNotActive = Error.Validation(
        "Billing.PlanNotActive", "This subscription plan is not currently active.");

    public static readonly Error SubscriptionNotFound = Error.NotFound(
        "Billing.SubscriptionNotFound", "No active subscription found for this tenant.");

    public static readonly Error AlreadyOnPlan = Error.Validation(
        "Billing.AlreadyOnPlan", "Tenant is already subscribed to this plan.");

    public static readonly Error SlugAlreadyExists = Error.Conflict(
        "Billing.SlugAlreadyExists", "A subscription plan with this slug already exists.");

    public static readonly Error CannotDeactivateWithSubscribers = Error.Validation(
        "Billing.CannotDeactivateWithSubscribers", "Cannot deactivate a plan that has active subscribers. Move subscribers first.");

    public static readonly Error FreePlanRequired = Error.Validation(
        "Billing.FreePlanRequired", "At least one free plan must remain active for new tenant registration.");

    public static readonly Error CannotCancelFreePlan = Error.Validation(
        "Billing.CannotCancelFreePlan", "Cannot cancel a free plan subscription.");
}
```

- [ ] **Step 6:** Create `SubscriptionChangedEvent.cs`

```csharp
using Starter.Domain.Common;

namespace Starter.Domain.Billing.Events;

public sealed record SubscriptionChangedEvent(Guid TenantId, Guid? OldPlanId, Guid NewPlanId) : IDomainEvent;
```

- [ ] **Step 7:** Create `SubscriptionCanceledEvent.cs`

```csharp
using Starter.Domain.Common;

namespace Starter.Domain.Billing.Events;

public sealed record SubscriptionCanceledEvent(Guid TenantId, Guid SubscriptionId) : IDomainEvent;
```

- [ ] **Step 8:** Verify build

Run: `dotnet build` from `boilerplateBE/`
Expected: 0 errors

- [ ] **Step 9:** Commit

```
feat(domain): add billing entities, errors, and domain events
```

---

## Task 3: EF Configurations + DbContext + Query Filters

**Files:**
- Create: `boilerplateBE/src/Starter.Infrastructure/Persistence/Configurations/SubscriptionPlanConfiguration.cs`
- Create: `boilerplateBE/src/Starter.Infrastructure/Persistence/Configurations/TenantSubscriptionConfiguration.cs`
- Create: `boilerplateBE/src/Starter.Infrastructure/Persistence/Configurations/PaymentRecordConfiguration.cs`
- Create: `boilerplateBE/src/Starter.Infrastructure/Persistence/Configurations/PlanPriceHistoryConfiguration.cs`
- Modify: `boilerplateBE/src/Starter.Application/Common/Interfaces/IApplicationDbContext.cs`
- Modify: `boilerplateBE/src/Starter.Infrastructure/Persistence/ApplicationDbContext.cs`

- [ ] **Step 1:** Create `SubscriptionPlanConfiguration.cs`

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Starter.Domain.Billing.Entities;

namespace Starter.Infrastructure.Persistence.Configurations;

internal sealed class SubscriptionPlanConfiguration : IEntityTypeConfiguration<SubscriptionPlan>
{
    public void Configure(EntityTypeBuilder<SubscriptionPlan> builder)
    {
        builder.ToTable("subscription_plans");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
        builder.Property(p => p.Slug).HasColumnName("slug").HasMaxLength(100).IsRequired();
        builder.Property(p => p.Description).HasColumnName("description").HasMaxLength(2000);
        builder.Property(p => p.Translations).HasColumnName("translations").HasColumnType("jsonb");
        builder.Property(p => p.MonthlyPrice).HasColumnName("monthly_price").HasPrecision(18, 2).IsRequired();
        builder.Property(p => p.AnnualPrice).HasColumnName("annual_price").HasPrecision(18, 2).IsRequired();
        builder.Property(p => p.Currency).HasColumnName("currency").HasMaxLength(3).HasDefaultValue("USD").IsRequired();
        builder.Property(p => p.Features).HasColumnName("features").HasColumnType("jsonb").IsRequired();
        builder.Property(p => p.IsFree).HasColumnName("is_free").IsRequired();
        builder.Property(p => p.IsActive).HasColumnName("is_active").HasDefaultValue(true).IsRequired();
        builder.Property(p => p.IsPublic).HasColumnName("is_public").HasDefaultValue(true).IsRequired();
        builder.Property(p => p.DisplayOrder).HasColumnName("display_order").IsRequired();
        builder.Property(p => p.TrialDays).HasColumnName("trial_days").HasDefaultValue(0).IsRequired();

        builder.HasIndex(p => p.Slug).IsUnique();
        builder.HasIndex(p => new { p.IsActive, p.IsPublic, p.DisplayOrder });

        builder.HasMany(p => p.Subscriptions)
            .WithOne(s => s.Plan)
            .HasForeignKey(s => s.SubscriptionPlanId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(p => p.PriceHistory)
            .WithOne(h => h.Plan)
            .HasForeignKey(h => h.SubscriptionPlanId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
```

- [ ] **Step 2:** Create `TenantSubscriptionConfiguration.cs`

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Starter.Domain.Billing.Entities;

namespace Starter.Infrastructure.Persistence.Configurations;

internal sealed class TenantSubscriptionConfiguration : IEntityTypeConfiguration<TenantSubscription>
{
    public void Configure(EntityTypeBuilder<TenantSubscription> builder)
    {
        builder.ToTable("tenant_subscriptions");
        builder.HasKey(s => s.Id);

        builder.Property(s => s.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(s => s.SubscriptionPlanId).HasColumnName("subscription_plan_id").IsRequired();
        builder.Property(s => s.Status).HasColumnName("status").IsRequired();
        builder.Property(s => s.LockedMonthlyPrice).HasColumnName("locked_monthly_price").HasPrecision(18, 2).IsRequired();
        builder.Property(s => s.LockedAnnualPrice).HasColumnName("locked_annual_price").HasPrecision(18, 2).IsRequired();
        builder.Property(s => s.Currency).HasColumnName("currency").HasMaxLength(3).HasDefaultValue("USD").IsRequired();
        builder.Property(s => s.ExternalCustomerId).HasColumnName("external_customer_id").HasMaxLength(500);
        builder.Property(s => s.ExternalSubscriptionId).HasColumnName("external_subscription_id").HasMaxLength(500);
        builder.Property(s => s.BillingInterval).HasColumnName("billing_interval").IsRequired();
        builder.Property(s => s.CurrentPeriodStart).HasColumnName("current_period_start").IsRequired();
        builder.Property(s => s.CurrentPeriodEnd).HasColumnName("current_period_end").IsRequired();
        builder.Property(s => s.TrialEndAt).HasColumnName("trial_end_at");
        builder.Property(s => s.CanceledAt).HasColumnName("canceled_at");
        builder.Property(s => s.AutoRenew).HasColumnName("auto_renew").HasDefaultValue(true).IsRequired();

        builder.HasIndex(s => s.TenantId).IsUnique();
        builder.HasIndex(s => new { s.TenantId, s.Status });

        builder.HasMany(s => s.Payments)
            .WithOne(p => p.Subscription)
            .HasForeignKey(p => p.TenantSubscriptionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
```

- [ ] **Step 3:** Create `PaymentRecordConfiguration.cs`

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Starter.Domain.Billing.Entities;

namespace Starter.Infrastructure.Persistence.Configurations;

internal sealed class PaymentRecordConfiguration : IEntityTypeConfiguration<PaymentRecord>
{
    public void Configure(EntityTypeBuilder<PaymentRecord> builder)
    {
        builder.ToTable("payment_records");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(p => p.TenantSubscriptionId).HasColumnName("tenant_subscription_id").IsRequired();
        builder.Property(p => p.Amount).HasColumnName("amount").HasPrecision(18, 2).IsRequired();
        builder.Property(p => p.Currency).HasColumnName("currency").HasMaxLength(3).IsRequired();
        builder.Property(p => p.Status).HasColumnName("status").IsRequired();
        builder.Property(p => p.ExternalPaymentId).HasColumnName("external_payment_id").HasMaxLength(500);
        builder.Property(p => p.InvoiceUrl).HasColumnName("invoice_url").HasMaxLength(2000);
        builder.Property(p => p.Description).HasColumnName("description").HasMaxLength(500);
        builder.Property(p => p.PeriodStart).HasColumnName("period_start").IsRequired();
        builder.Property(p => p.PeriodEnd).HasColumnName("period_end").IsRequired();

        builder.HasIndex(p => p.TenantId);
        builder.HasIndex(p => p.TenantSubscriptionId);
    }
}
```

- [ ] **Step 4:** Create `PlanPriceHistoryConfiguration.cs`

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Starter.Domain.Billing.Entities;

namespace Starter.Infrastructure.Persistence.Configurations;

internal sealed class PlanPriceHistoryConfiguration : IEntityTypeConfiguration<PlanPriceHistory>
{
    public void Configure(EntityTypeBuilder<PlanPriceHistory> builder)
    {
        builder.ToTable("plan_price_history");
        builder.HasKey(h => h.Id);

        builder.Property(h => h.SubscriptionPlanId).HasColumnName("subscription_plan_id").IsRequired();
        builder.Property(h => h.MonthlyPrice).HasColumnName("monthly_price").HasPrecision(18, 2).IsRequired();
        builder.Property(h => h.AnnualPrice).HasColumnName("annual_price").HasPrecision(18, 2).IsRequired();
        builder.Property(h => h.Currency).HasColumnName("currency").HasMaxLength(3).IsRequired();
        builder.Property(h => h.ChangedBy).HasColumnName("changed_by").IsRequired();
        builder.Property(h => h.Reason).HasColumnName("reason").HasMaxLength(500);
        builder.Property(h => h.EffectiveFrom).HasColumnName("effective_from").IsRequired();

        builder.HasIndex(h => h.SubscriptionPlanId);
    }
}
```

- [ ] **Step 5:** Add DbSets to `IApplicationDbContext.cs`

Add these 4 lines to the interface:

```csharp
DbSet<SubscriptionPlan> SubscriptionPlans { get; }
DbSet<TenantSubscription> TenantSubscriptions { get; }
DbSet<PaymentRecord> PaymentRecords { get; }
DbSet<PlanPriceHistory> PlanPriceHistories { get; }
```

- [ ] **Step 6:** Add DbSets and query filters to `ApplicationDbContext.cs`

Add DbSet properties:

```csharp
public DbSet<SubscriptionPlan> SubscriptionPlans => Set<SubscriptionPlan>();
public DbSet<TenantSubscription> TenantSubscriptions => Set<TenantSubscription>();
public DbSet<PaymentRecord> PaymentRecords => Set<PaymentRecord>();
public DbSet<PlanPriceHistory> PlanPriceHistories => Set<PlanPriceHistory>();
```

Add query filters in `OnModelCreating` (SubscriptionPlan and PlanPriceHistory have NO filter — platform-level):

```csharp
modelBuilder.Entity<TenantSubscription>().HasQueryFilter(s =>
    TenantId == null || s.TenantId == TenantId);

modelBuilder.Entity<PaymentRecord>().HasQueryFilter(p =>
    TenantId == null || p.TenantId == TenantId);
```

- [ ] **Step 7:** Verify build

Run: `dotnet build` from `boilerplateBE/`
Expected: 0 errors

- [ ] **Step 8:** Commit

```
feat(infrastructure): add billing EF configurations, DbSets, and query filters
```

---

## Task 4: IUsageTracker + IBillingProvider Interfaces and Implementations

**Files:**
- Create: `boilerplateBE/src/Starter.Application/Common/Interfaces/IUsageTracker.cs`
- Create: `boilerplateBE/src/Starter.Application/Common/Interfaces/IBillingProvider.cs`
- Create: `boilerplateBE/src/Starter.Infrastructure/Services/UsageTrackerService.cs`
- Create: `boilerplateBE/src/Starter.Infrastructure/Services/MockBillingProvider.cs`
- Modify: `boilerplateBE/src/Starter.Infrastructure/DependencyInjection.cs`

- [ ] **Step 1:** Create `IUsageTracker.cs`

```csharp
namespace Starter.Application.Common.Interfaces;

public interface IUsageTracker
{
    Task<long> GetAsync(Guid tenantId, string metric, CancellationToken ct = default);
    Task IncrementAsync(Guid tenantId, string metric, long amount = 1, CancellationToken ct = default);
    Task DecrementAsync(Guid tenantId, string metric, long amount = 1, CancellationToken ct = default);
    Task SetAsync(Guid tenantId, string metric, long value, CancellationToken ct = default);
    Task<Dictionary<string, long>> GetAllAsync(Guid tenantId, CancellationToken ct = default);
}
```

- [ ] **Step 2:** Create `IBillingProvider.cs`

```csharp
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
```

- [ ] **Step 3:** Create `UsageTrackerService.cs`

```csharp
using Microsoft.EntityFrameworkCore;
using Starter.Application.Common.Interfaces;
using StackExchange.Redis;

namespace Starter.Infrastructure.Services;

internal sealed class UsageTrackerService(
    IConnectionMultiplexer redis,
    IApplicationDbContext context) : IUsageTracker
{
    private static string Key(Guid tenantId, string metric) => $"usage:{tenantId}:{metric}";

    private static readonly string[] AllMetrics = ["users", "storage_bytes", "api_keys", "reports_active"];

    public async Task<long> GetAsync(Guid tenantId, string metric, CancellationToken ct = default)
    {
        var db = redis.GetDatabase();
        var key = Key(tenantId, metric);

        var value = await db.StringGetAsync(key);
        if (value.HasValue)
            return (long)value;

        // Cache miss — rebuild from DB
        var count = await RebuildFromDbAsync(tenantId, metric, ct);
        await db.StringSetAsync(key, count, TimeSpan.FromHours(24));
        return count;
    }

    public async Task IncrementAsync(Guid tenantId, string metric, long amount = 1, CancellationToken ct = default)
    {
        var db = redis.GetDatabase();
        var key = Key(tenantId, metric);

        // Ensure key exists (rebuild if needed)
        if (!await db.KeyExistsAsync(key))
        {
            var current = await RebuildFromDbAsync(tenantId, metric, ct);
            await db.StringSetAsync(key, current, TimeSpan.FromHours(24));
        }

        await db.StringIncrementAsync(key, amount);
    }

    public async Task DecrementAsync(Guid tenantId, string metric, long amount = 1, CancellationToken ct = default)
    {
        var db = redis.GetDatabase();
        var key = Key(tenantId, metric);

        if (!await db.KeyExistsAsync(key))
        {
            var current = await RebuildFromDbAsync(tenantId, metric, ct);
            await db.StringSetAsync(key, current, TimeSpan.FromHours(24));
        }

        var newValue = await db.StringDecrementAsync(key, amount);
        if (newValue < 0) await db.StringSetAsync(key, 0);
    }

    public async Task SetAsync(Guid tenantId, string metric, long value, CancellationToken ct = default)
    {
        var db = redis.GetDatabase();
        await db.StringSetAsync(Key(tenantId, metric), value, TimeSpan.FromHours(24));
    }

    public async Task<Dictionary<string, long>> GetAllAsync(Guid tenantId, CancellationToken ct = default)
    {
        var result = new Dictionary<string, long>();
        foreach (var metric in AllMetrics)
            result[metric] = await GetAsync(tenantId, metric, ct);
        return result;
    }

    private async Task<long> RebuildFromDbAsync(Guid tenantId, string metric, CancellationToken ct)
    {
        return metric switch
        {
            "users" => await context.Users.IgnoreQueryFilters()
                .CountAsync(u => u.TenantId == tenantId, ct),
            "storage_bytes" => await context.FileMetadata.IgnoreQueryFilters()
                .Where(f => f.TenantId == tenantId)
                .SumAsync(f => f.Size, ct),
            "api_keys" => await context.ApiKeys.IgnoreQueryFilters()
                .CountAsync(k => k.TenantId == tenantId && !k.IsRevoked, ct),
            "reports_active" => await context.ReportRequests.IgnoreQueryFilters()
                .CountAsync(r => r.TenantId == tenantId, ct),
            _ => 0
        };
    }
}
```

- [ ] **Step 4:** Create `MockBillingProvider.cs`

```csharp
using Starter.Application.Common.Interfaces;
using Starter.Domain.Billing.Enums;

namespace Starter.Infrastructure.Services;

internal sealed class MockBillingProvider : IBillingProvider
{
    public Task<CreateSubscriptionResult> CreateSubscriptionAsync(
        Guid tenantId, string planSlug, BillingInterval interval, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var periodEnd = interval == BillingInterval.Annual ? now.AddYears(1) : now.AddMonths(1);

        return Task.FromResult(new CreateSubscriptionResult(
            ExternalSubscriptionId: $"mock_sub_{Guid.NewGuid():N}",
            ExternalCustomerId: $"mock_cust_{tenantId:N}",
            PeriodStart: now,
            PeriodEnd: periodEnd));
    }

    public Task<ChangeSubscriptionResult> ChangeSubscriptionAsync(
        string externalSubscriptionId, string newPlanSlug, BillingInterval interval, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var periodEnd = interval == BillingInterval.Annual ? now.AddYears(1) : now.AddMonths(1);

        return Task.FromResult(new ChangeSubscriptionResult(
            NewPeriodStart: now,
            NewPeriodEnd: periodEnd,
            ProratedAmount: 0m));
    }

    public Task CancelSubscriptionAsync(string externalSubscriptionId, CancellationToken ct = default)
    {
        return Task.CompletedTask;
    }
}
```

- [ ] **Step 5:** Register services in `DependencyInjection.cs`

Add to the `AddServices` method:

```csharp
services.AddScoped<IUsageTracker, UsageTrackerService>();
services.AddScoped<IBillingProvider, MockBillingProvider>();
```

- [ ] **Step 6:** Verify build

Run: `dotnet build` from `boilerplateBE/`
Expected: 0 errors

- [ ] **Step 7:** Commit

```
feat(infrastructure): add IUsageTracker, IBillingProvider, and implementations
```

---

## Task 5: Permissions + Role Mapping

**Files:**
- Modify: `boilerplateBE/src/Starter.Shared/Constants/Permissions.cs`
- Modify: `boilerplateBE/src/Starter.Shared/Constants/Roles.cs`
- Modify: `boilerplateFE/src/constants/permissions.ts`

- [ ] **Step 1:** Add Billing module to `Permissions.cs`

Add the nested class alongside existing modules:

```csharp
public static class Billing
{
    public const string View = "Billing.View";
    public const string Manage = "Billing.Manage";
    public const string ViewPlans = "Billing.ViewPlans";
    public const string ManagePlans = "Billing.ManagePlans";
    public const string ManageTenantSubscriptions = "Billing.ManageTenantSubscriptions";
}
```

Add to `GetAllWithMetadata()`:

```csharp
yield return (Billing.View, "View subscription and usage", "Billing");
yield return (Billing.Manage, "Change plan and cancel subscription", "Billing");
yield return (Billing.ViewPlans, "View all subscription plans", "Billing");
yield return (Billing.ManagePlans, "Create and manage subscription plans", "Billing");
yield return (Billing.ManageTenantSubscriptions, "Manage tenant subscriptions", "Billing");
```

- [ ] **Step 2:** Add Billing permissions to roles in `Roles.cs`

Admin role — add after existing permissions:

```csharp
// Billing
Permissions.Billing.View,
Permissions.Billing.Manage,
```

User role — add:

```csharp
// Billing
Permissions.Billing.View,
```

SuperAdmin gets all permissions automatically.

- [ ] **Step 3:** Mirror in frontend `permissions.ts`

Add to the permissions constant object:

```typescript
Billing: {
  View: 'Billing.View',
  Manage: 'Billing.Manage',
  ViewPlans: 'Billing.ViewPlans',
  ManagePlans: 'Billing.ManagePlans',
  ManageTenantSubscriptions: 'Billing.ManageTenantSubscriptions',
},
```

- [ ] **Step 4:** Verify builds

Run: `dotnet build` from `boilerplateBE/` AND `npm run build` from `boilerplateFE/`
Expected: 0 errors both

- [ ] **Step 5:** Commit

```
feat(permissions): add billing module permissions and role mappings
```

---

## Task 6: DTOs + Mappers

**Files:**
- Create: `boilerplateBE/src/Starter.Application/Features/Billing/DTOs/SubscriptionPlanDto.cs`
- Create: `boilerplateBE/src/Starter.Application/Features/Billing/DTOs/SubscriptionPlanMapper.cs`
- Create: `boilerplateBE/src/Starter.Application/Features/Billing/DTOs/TenantSubscriptionDto.cs`
- Create: `boilerplateBE/src/Starter.Application/Features/Billing/DTOs/TenantSubscriptionMapper.cs`
- Create: `boilerplateBE/src/Starter.Application/Features/Billing/DTOs/PaymentRecordDto.cs`
- Create: `boilerplateBE/src/Starter.Application/Features/Billing/DTOs/PaymentRecordMapper.cs`
- Create: `boilerplateBE/src/Starter.Application/Features/Billing/DTOs/UsageDto.cs`

- [ ] **Step 1:** Create `SubscriptionPlanDto.cs`

```csharp
namespace Starter.Application.Features.Billing.DTOs;

public sealed record SubscriptionPlanDto(
    Guid Id,
    string Name,
    string Slug,
    string? Description,
    string? Translations,
    decimal MonthlyPrice,
    decimal AnnualPrice,
    string Currency,
    string Features,
    bool IsFree,
    bool IsActive,
    bool IsPublic,
    int DisplayOrder,
    int TrialDays,
    int SubscriberCount,
    DateTime CreatedAt,
    DateTime? ModifiedAt);
```

- [ ] **Step 2:** Create `SubscriptionPlanMapper.cs`

```csharp
using Starter.Domain.Billing.Entities;

namespace Starter.Application.Features.Billing.DTOs;

public static class SubscriptionPlanMapper
{
    public static SubscriptionPlanDto ToDto(this SubscriptionPlan entity, int subscriberCount = 0)
    {
        return new SubscriptionPlanDto(
            Id: entity.Id,
            Name: entity.Name,
            Slug: entity.Slug,
            Description: entity.Description,
            Translations: entity.Translations,
            MonthlyPrice: entity.MonthlyPrice,
            AnnualPrice: entity.AnnualPrice,
            Currency: entity.Currency,
            Features: entity.Features,
            IsFree: entity.IsFree,
            IsActive: entity.IsActive,
            IsPublic: entity.IsPublic,
            DisplayOrder: entity.DisplayOrder,
            TrialDays: entity.TrialDays,
            SubscriberCount: subscriberCount,
            CreatedAt: entity.CreatedAt,
            ModifiedAt: entity.ModifiedAt);
    }
}
```

- [ ] **Step 3:** Create `TenantSubscriptionDto.cs`

```csharp
using Starter.Domain.Billing.Enums;

namespace Starter.Application.Features.Billing.DTOs;

public sealed record TenantSubscriptionDto(
    Guid Id,
    Guid TenantId,
    Guid SubscriptionPlanId,
    string PlanName,
    string PlanSlug,
    SubscriptionStatus Status,
    decimal LockedMonthlyPrice,
    decimal LockedAnnualPrice,
    string Currency,
    BillingInterval BillingInterval,
    DateTime CurrentPeriodStart,
    DateTime CurrentPeriodEnd,
    DateTime? CanceledAt,
    bool AutoRenew,
    DateTime CreatedAt);
```

- [ ] **Step 4:** Create `TenantSubscriptionMapper.cs`

```csharp
using Starter.Domain.Billing.Entities;

namespace Starter.Application.Features.Billing.DTOs;

public static class TenantSubscriptionMapper
{
    public static TenantSubscriptionDto ToDto(this TenantSubscription entity)
    {
        return new TenantSubscriptionDto(
            Id: entity.Id,
            TenantId: entity.TenantId,
            SubscriptionPlanId: entity.SubscriptionPlanId,
            PlanName: entity.Plan?.Name ?? "Unknown",
            PlanSlug: entity.Plan?.Slug ?? "unknown",
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
```

- [ ] **Step 5:** Create `PaymentRecordDto.cs`

```csharp
using Starter.Domain.Billing.Enums;

namespace Starter.Application.Features.Billing.DTOs;

public sealed record PaymentRecordDto(
    Guid Id,
    decimal Amount,
    string Currency,
    PaymentStatus Status,
    string? Description,
    DateTime PeriodStart,
    DateTime PeriodEnd,
    DateTime CreatedAt);
```

- [ ] **Step 6:** Create `PaymentRecordMapper.cs`

```csharp
using Starter.Domain.Billing.Entities;

namespace Starter.Application.Features.Billing.DTOs;

public static class PaymentRecordMapper
{
    public static PaymentRecordDto ToDto(this PaymentRecord entity)
    {
        return new PaymentRecordDto(
            Id: entity.Id,
            Amount: entity.Amount,
            Currency: entity.Currency,
            Status: entity.Status,
            Description: entity.Description,
            PeriodStart: entity.PeriodStart,
            PeriodEnd: entity.PeriodEnd,
            CreatedAt: entity.CreatedAt);
    }
}
```

- [ ] **Step 7:** Create `UsageDto.cs`

```csharp
namespace Starter.Application.Features.Billing.DTOs;

public sealed record UsageDto(
    long Users,
    long StorageBytes,
    long ApiKeys,
    long ReportsActive,
    int MaxUsers,
    long MaxStorageBytes,
    int MaxApiKeys,
    int MaxReports);
```

- [ ] **Step 8:** Verify build, commit

```
feat(billing): add billing DTOs and entity mappers
```

---

## Task 7: Queries (GetPlans, GetPlanById, GetSubscription, GetUsage, GetPayments)

**Files:** Create all query + handler pairs under `boilerplateBE/src/Starter.Application/Features/Billing/Queries/`

- [ ] **Step 1:** Create `GetPlansQuery.cs` + `GetPlansQueryHandler.cs`

```csharp
// GetPlansQuery.cs
using MediatR;
using Starter.Application.Features.Billing.DTOs;
using Starter.Shared.Results;

namespace Starter.Application.Features.Billing.Queries.GetPlans;

public sealed record GetPlansQuery(bool PublicOnly = false, bool IncludeInactive = false) : IRequest<Result<List<SubscriptionPlanDto>>>;
```

```csharp
// GetPlansQueryHandler.cs
using MediatR;
using Microsoft.EntityFrameworkCore;
using Starter.Application.Common.Interfaces;
using Starter.Application.Features.Billing.DTOs;
using Starter.Domain.Billing.Enums;
using Starter.Shared.Results;

namespace Starter.Application.Features.Billing.Queries.GetPlans;

internal sealed class GetPlansQueryHandler(
    IApplicationDbContext context) : IRequestHandler<GetPlansQuery, Result<List<SubscriptionPlanDto>>>
{
    public async Task<Result<List<SubscriptionPlanDto>>> Handle(GetPlansQuery request, CancellationToken cancellationToken)
    {
        var query = context.SubscriptionPlans.AsNoTracking().AsQueryable();

        if (request.PublicOnly)
            query = query.Where(p => p.IsActive && p.IsPublic);
        else if (!request.IncludeInactive)
            query = query.Where(p => p.IsActive);

        var plans = await query.OrderBy(p => p.DisplayOrder).ToListAsync(cancellationToken);

        // Get subscriber counts
        var planIds = plans.Select(p => p.Id).ToList();
        var subscriberCounts = await context.TenantSubscriptions
            .IgnoreQueryFilters()
            .Where(s => planIds.Contains(s.SubscriptionPlanId) && s.Status == SubscriptionStatus.Active)
            .GroupBy(s => s.SubscriptionPlanId)
            .Select(g => new { PlanId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.PlanId, x => x.Count, cancellationToken);

        var dtos = plans.Select(p => p.ToDto(subscriberCounts.GetValueOrDefault(p.Id, 0))).ToList();
        return Result.Success(dtos);
    }
}
```

- [ ] **Step 2:** Create `GetPlanByIdQuery.cs` + `GetPlanByIdQueryHandler.cs`

```csharp
// GetPlanByIdQuery.cs
using MediatR;
using Starter.Application.Features.Billing.DTOs;
using Starter.Shared.Results;

namespace Starter.Application.Features.Billing.Queries.GetPlanById;

public sealed record GetPlanByIdQuery(Guid Id) : IRequest<Result<SubscriptionPlanDto>>;
```

```csharp
// GetPlanByIdQueryHandler.cs
using MediatR;
using Microsoft.EntityFrameworkCore;
using Starter.Application.Common.Interfaces;
using Starter.Application.Features.Billing.DTOs;
using Starter.Domain.Billing.Errors;
using Starter.Domain.Billing.Enums;
using Starter.Shared.Results;

namespace Starter.Application.Features.Billing.Queries.GetPlanById;

internal sealed class GetPlanByIdQueryHandler(
    IApplicationDbContext context) : IRequestHandler<GetPlanByIdQuery, Result<SubscriptionPlanDto>>
{
    public async Task<Result<SubscriptionPlanDto>> Handle(GetPlanByIdQuery request, CancellationToken cancellationToken)
    {
        var plan = await context.SubscriptionPlans.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == request.Id, cancellationToken);

        if (plan is null)
            return Result.Failure<SubscriptionPlanDto>(BillingErrors.PlanNotFound);

        var subscriberCount = await context.TenantSubscriptions
            .IgnoreQueryFilters()
            .CountAsync(s => s.SubscriptionPlanId == plan.Id && s.Status == SubscriptionStatus.Active, cancellationToken);

        return Result.Success(plan.ToDto(subscriberCount));
    }
}
```

- [ ] **Step 3:** Create `GetSubscriptionQuery.cs` + `GetSubscriptionQueryHandler.cs`

```csharp
// GetSubscriptionQuery.cs
using MediatR;
using Starter.Application.Features.Billing.DTOs;
using Starter.Shared.Results;

namespace Starter.Application.Features.Billing.Queries.GetSubscription;

public sealed record GetSubscriptionQuery(Guid? TenantId = null) : IRequest<Result<TenantSubscriptionDto>>;
```

```csharp
// GetSubscriptionQueryHandler.cs
using MediatR;
using Microsoft.EntityFrameworkCore;
using Starter.Application.Common.Interfaces;
using Starter.Application.Features.Billing.DTOs;
using Starter.Domain.Billing.Errors;
using Starter.Shared.Results;

namespace Starter.Application.Features.Billing.Queries.GetSubscription;

internal sealed class GetSubscriptionQueryHandler(
    IApplicationDbContext context,
    ICurrentUserService currentUser) : IRequestHandler<GetSubscriptionQuery, Result<TenantSubscriptionDto>>
{
    public async Task<Result<TenantSubscriptionDto>> Handle(GetSubscriptionQuery request, CancellationToken cancellationToken)
    {
        var tenantId = request.TenantId ?? currentUser.TenantId;
        if (tenantId is null)
            return Result.Failure<TenantSubscriptionDto>(BillingErrors.SubscriptionNotFound);

        var subscription = await context.TenantSubscriptions
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Include(s => s.Plan)
            .FirstOrDefaultAsync(s => s.TenantId == tenantId.Value, cancellationToken);

        if (subscription is null)
            return Result.Failure<TenantSubscriptionDto>(BillingErrors.SubscriptionNotFound);

        return Result.Success(subscription.ToDto());
    }
}
```

- [ ] **Step 4:** Create `GetUsageQuery.cs` + `GetUsageQueryHandler.cs`

```csharp
// GetUsageQuery.cs
using MediatR;
using Starter.Application.Features.Billing.DTOs;
using Starter.Shared.Results;

namespace Starter.Application.Features.Billing.Queries.GetUsage;

public sealed record GetUsageQuery(Guid? TenantId = null) : IRequest<Result<UsageDto>>;
```

```csharp
// GetUsageQueryHandler.cs
using MediatR;
using Starter.Application.Common.Interfaces;
using Starter.Application.Features.Billing.DTOs;
using Starter.Domain.Billing.Errors;
using Starter.Shared.Results;

namespace Starter.Application.Features.Billing.Queries.GetUsage;

internal sealed class GetUsageQueryHandler(
    ICurrentUserService currentUser,
    IUsageTracker usage,
    IFeatureFlagService flags) : IRequestHandler<GetUsageQuery, Result<UsageDto>>
{
    public async Task<Result<UsageDto>> Handle(GetUsageQuery request, CancellationToken cancellationToken)
    {
        var tenantId = request.TenantId ?? currentUser.TenantId;
        if (tenantId is null)
            return Result.Failure<UsageDto>(BillingErrors.SubscriptionNotFound);

        var counters = await usage.GetAllAsync(tenantId.Value, cancellationToken);

        var maxUsers = await flags.GetValueAsync<int>("users.max_count", cancellationToken);
        var maxStorageMb = await flags.GetValueAsync<int>("files.max_storage_mb", cancellationToken);
        var maxApiKeys = await flags.GetValueAsync<int>("api_keys.max_count", cancellationToken);
        var maxReports = await flags.GetValueAsync<int>("reports.max_concurrent", cancellationToken);

        return Result.Success(new UsageDto(
            Users: counters.GetValueOrDefault("users"),
            StorageBytes: counters.GetValueOrDefault("storage_bytes"),
            ApiKeys: counters.GetValueOrDefault("api_keys"),
            ReportsActive: counters.GetValueOrDefault("reports_active"),
            MaxUsers: maxUsers,
            MaxStorageBytes: (long)maxStorageMb * 1024 * 1024,
            MaxApiKeys: maxApiKeys,
            MaxReports: maxReports));
    }
}
```

- [ ] **Step 5:** Create `GetPaymentsQuery.cs` + `GetPaymentsQueryHandler.cs`

```csharp
// GetPaymentsQuery.cs
using MediatR;
using Starter.Application.Common.Models;
using Starter.Application.Features.Billing.DTOs;
using Starter.Shared.Results;

namespace Starter.Application.Features.Billing.Queries.GetPayments;

public sealed record GetPaymentsQuery(int PageNumber = 1, int PageSize = 20, Guid? TenantId = null)
    : IRequest<Result<PaginatedList<PaymentRecordDto>>>;
```

```csharp
// GetPaymentsQueryHandler.cs
using MediatR;
using Microsoft.EntityFrameworkCore;
using Starter.Application.Common.Interfaces;
using Starter.Application.Common.Models;
using Starter.Application.Features.Billing.DTOs;
using Starter.Shared.Results;

namespace Starter.Application.Features.Billing.Queries.GetPayments;

internal sealed class GetPaymentsQueryHandler(
    IApplicationDbContext context,
    ICurrentUserService currentUser) : IRequestHandler<GetPaymentsQuery, Result<PaginatedList<PaymentRecordDto>>>
{
    public async Task<Result<PaginatedList<PaymentRecordDto>>> Handle(GetPaymentsQuery request, CancellationToken cancellationToken)
    {
        var tenantId = request.TenantId ?? currentUser.TenantId;

        var query = context.PaymentRecords.AsNoTracking().AsQueryable();

        if (tenantId.HasValue)
            query = query.IgnoreQueryFilters().Where(p => p.TenantId == tenantId.Value);

        query = query.OrderByDescending(p => p.CreatedAt);

        var totalCount = await query.CountAsync(cancellationToken);
        var payments = await query
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        var dtos = payments.Select(p => p.ToDto()).ToList();
        return Result.Success(PaginatedList<PaymentRecordDto>.Create(
            dtos.AsReadOnly(), totalCount, request.PageNumber, request.PageSize));
    }
}
```

- [ ] **Step 6:** Verify build, commit

```
feat(billing): add billing queries (plans, subscription, usage, payments)
```

---

## Task 8: Commands (CreatePlan, UpdatePlan, DeactivatePlan, ChangePlan, CancelSubscription, ResyncPlanTenants)

**Files:** Create all command + handler + validator triples under `boilerplateBE/src/Starter.Application/Features/Billing/Commands/`

- [ ] **Step 1:** Create `CreatePlanCommand.cs` + handler + validator

```csharp
// CreatePlanCommand.cs
using MediatR;
using Starter.Shared.Results;

namespace Starter.Application.Features.Billing.Commands.CreatePlan;

public sealed record CreatePlanCommand(
    string Name, string Slug, string? Description, string? Translations,
    decimal MonthlyPrice, decimal AnnualPrice, string Currency, string Features,
    bool IsFree, bool IsPublic, int DisplayOrder, int TrialDays) : IRequest<Result<Guid>>;
```

```csharp
// CreatePlanCommandHandler.cs
using MediatR;
using Microsoft.EntityFrameworkCore;
using Starter.Application.Common.Interfaces;
using Starter.Domain.Billing.Entities;
using Starter.Domain.Billing.Errors;
using Starter.Shared.Results;

namespace Starter.Application.Features.Billing.Commands.CreatePlan;

internal sealed class CreatePlanCommandHandler(
    IApplicationDbContext context,
    ICurrentUserService currentUser) : IRequestHandler<CreatePlanCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(CreatePlanCommand request, CancellationToken cancellationToken)
    {
        var slugExists = await context.SubscriptionPlans
            .AnyAsync(p => p.Slug == request.Slug.Trim().ToLowerInvariant(), cancellationToken);

        if (slugExists)
            return Result.Failure<Guid>(BillingErrors.SlugAlreadyExists);

        var plan = SubscriptionPlan.Create(
            request.Name, request.Slug, request.Description, request.Translations,
            request.MonthlyPrice, request.AnnualPrice, request.Currency, request.Features,
            request.IsFree, request.IsPublic, request.DisplayOrder, request.TrialDays);

        // Create initial price history record
        var priceHistory = PlanPriceHistory.Create(
            plan.Id, plan.MonthlyPrice, plan.AnnualPrice, plan.Currency,
            currentUser.UserId ?? Guid.Empty, "Initial plan creation");

        context.SubscriptionPlans.Add(plan);
        context.PlanPriceHistories.Add(priceHistory);
        await context.SaveChangesAsync(cancellationToken);

        return Result.Success(plan.Id);
    }
}
```

```csharp
// CreatePlanCommandValidator.cs
using FluentValidation;

namespace Starter.Application.Features.Billing.Commands.CreatePlan;

public sealed class CreatePlanCommandValidator : AbstractValidator<CreatePlanCommand>
{
    public CreatePlanCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Slug).NotEmpty().MaximumLength(100)
            .Matches(@"^[a-z0-9-]+$").WithMessage("Slug must be lowercase alphanumeric with hyphens only.");
        RuleFor(x => x.MonthlyPrice).GreaterThanOrEqualTo(0);
        RuleFor(x => x.AnnualPrice).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Currency).NotEmpty().MaximumLength(3);
        RuleFor(x => x.Features).NotEmpty();
        RuleFor(x => x.DisplayOrder).GreaterThanOrEqualTo(0);
        RuleFor(x => x.TrialDays).GreaterThanOrEqualTo(0);
    }
}
```

- [ ] **Step 2:** Create `UpdatePlanCommand.cs` + handler + validator (with price history tracking)

```csharp
// UpdatePlanCommand.cs
using MediatR;
using Starter.Shared.Results;

namespace Starter.Application.Features.Billing.Commands.UpdatePlan;

public sealed record UpdatePlanCommand(
    Guid Id, string Name, string? Description, string? Translations,
    decimal MonthlyPrice, decimal AnnualPrice, string Currency, string Features,
    bool IsPublic, int DisplayOrder, int TrialDays, string? PriceChangeReason = null) : IRequest<Result<Unit>>;
```

```csharp
// UpdatePlanCommandHandler.cs
using MediatR;
using Microsoft.EntityFrameworkCore;
using Starter.Application.Common.Interfaces;
using Starter.Domain.Billing.Entities;
using Starter.Domain.Billing.Errors;
using Starter.Shared.Results;

namespace Starter.Application.Features.Billing.Commands.UpdatePlan;

internal sealed class UpdatePlanCommandHandler(
    IApplicationDbContext context,
    ICurrentUserService currentUser) : IRequestHandler<UpdatePlanCommand, Result<Unit>>
{
    public async Task<Result<Unit>> Handle(UpdatePlanCommand request, CancellationToken cancellationToken)
    {
        var plan = await context.SubscriptionPlans
            .FirstOrDefaultAsync(p => p.Id == request.Id, cancellationToken);

        if (plan is null)
            return Result.Failure<Unit>(BillingErrors.PlanNotFound);

        // Track price changes
        var priceChanged = plan.MonthlyPrice != request.MonthlyPrice || plan.AnnualPrice != request.AnnualPrice;
        if (priceChanged)
        {
            var history = PlanPriceHistory.Create(
                plan.Id, plan.MonthlyPrice, plan.AnnualPrice, plan.Currency,
                currentUser.UserId ?? Guid.Empty, request.PriceChangeReason);
            context.PlanPriceHistories.Add(history);
        }

        plan.Update(request.Name, request.Description, request.Translations,
            request.MonthlyPrice, request.AnnualPrice, request.Currency,
            request.Features, request.IsPublic, request.DisplayOrder, request.TrialDays);

        await context.SaveChangesAsync(cancellationToken);
        return Result.Success(Unit.Value);
    }
}
```

```csharp
// UpdatePlanCommandValidator.cs
using FluentValidation;

namespace Starter.Application.Features.Billing.Commands.UpdatePlan;

public sealed class UpdatePlanCommandValidator : AbstractValidator<UpdatePlanCommand>
{
    public UpdatePlanCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.MonthlyPrice).GreaterThanOrEqualTo(0);
        RuleFor(x => x.AnnualPrice).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Currency).NotEmpty().MaximumLength(3);
        RuleFor(x => x.Features).NotEmpty();
    }
}
```

- [ ] **Step 3:** Create `DeactivatePlanCommand.cs` + handler

```csharp
// DeactivatePlanCommand.cs
using MediatR;
using Starter.Shared.Results;

namespace Starter.Application.Features.Billing.Commands.DeactivatePlan;

public sealed record DeactivatePlanCommand(Guid Id) : IRequest<Result<Unit>>;
```

```csharp
// DeactivatePlanCommandHandler.cs
using MediatR;
using Microsoft.EntityFrameworkCore;
using Starter.Application.Common.Interfaces;
using Starter.Domain.Billing.Errors;
using Starter.Domain.Billing.Enums;
using Starter.Shared.Results;

namespace Starter.Application.Features.Billing.Commands.DeactivatePlan;

internal sealed class DeactivatePlanCommandHandler(
    IApplicationDbContext context) : IRequestHandler<DeactivatePlanCommand, Result<Unit>>
{
    public async Task<Result<Unit>> Handle(DeactivatePlanCommand request, CancellationToken cancellationToken)
    {
        var plan = await context.SubscriptionPlans
            .FirstOrDefaultAsync(p => p.Id == request.Id, cancellationToken);

        if (plan is null)
            return Result.Failure<Unit>(BillingErrors.PlanNotFound);

        // Check if free plan — must have at least one active free plan
        if (plan.IsFree)
        {
            var otherFreePlans = await context.SubscriptionPlans
                .CountAsync(p => p.IsFree && p.IsActive && p.Id != plan.Id, cancellationToken);
            if (otherFreePlans == 0)
                return Result.Failure<Unit>(BillingErrors.FreePlanRequired);
        }

        plan.Deactivate();
        await context.SaveChangesAsync(cancellationToken);
        return Result.Success(Unit.Value);
    }
}
```

- [ ] **Step 4:** Create `ChangePlanCommand.cs` + handler + validator

```csharp
// ChangePlanCommand.cs
using MediatR;
using Starter.Domain.Billing.Enums;
using Starter.Shared.Results;

namespace Starter.Application.Features.Billing.Commands.ChangePlan;

public sealed record ChangePlanCommand(
    Guid PlanId, BillingInterval? Interval = null, Guid? TenantId = null) : IRequest<Result<Unit>>;
```

```csharp
// ChangePlanCommandHandler.cs
using MediatR;
using Microsoft.EntityFrameworkCore;
using Starter.Application.Common.Interfaces;
using Starter.Domain.Billing.Entities;
using Starter.Domain.Billing.Enums;
using Starter.Domain.Billing.Errors;
using Starter.Shared.Results;

namespace Starter.Application.Features.Billing.Commands.ChangePlan;

internal sealed class ChangePlanCommandHandler(
    IApplicationDbContext context,
    ICurrentUserService currentUser,
    IBillingProvider billingProvider) : IRequestHandler<ChangePlanCommand, Result<Unit>>
{
    public async Task<Result<Unit>> Handle(ChangePlanCommand request, CancellationToken cancellationToken)
    {
        var tenantId = request.TenantId ?? currentUser.TenantId;
        if (tenantId is null)
            return Result.Failure<Unit>(BillingErrors.SubscriptionNotFound);

        var subscription = await context.TenantSubscriptions
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(s => s.TenantId == tenantId.Value, cancellationToken);

        if (subscription is null)
            return Result.Failure<Unit>(BillingErrors.SubscriptionNotFound);

        if (subscription.SubscriptionPlanId == request.PlanId)
            return Result.Failure<Unit>(BillingErrors.AlreadyOnPlan);

        var newPlan = await context.SubscriptionPlans
            .FirstOrDefaultAsync(p => p.Id == request.PlanId && p.IsActive, cancellationToken);

        if (newPlan is null)
            return Result.Failure<Unit>(BillingErrors.PlanNotFound);

        var interval = request.Interval ?? subscription.BillingInterval;

        // Call billing provider
        var providerResult = await billingProvider.ChangeSubscriptionAsync(
            subscription.ExternalSubscriptionId ?? "", newPlan.Slug, interval, cancellationToken);

        // Update subscription with locked prices
        subscription.ChangePlan(newPlan.Id, newPlan.MonthlyPrice, newPlan.AnnualPrice,
            newPlan.Currency, providerResult.NewPeriodStart, providerResult.NewPeriodEnd);

        // Create payment record
        var effectivePrice = interval == BillingInterval.Annual
            ? newPlan.AnnualPrice : newPlan.MonthlyPrice;

        if (effectivePrice > 0 || providerResult.ProratedAmount != 0)
        {
            var payment = PaymentRecord.Create(
                tenantId.Value, subscription.Id, providerResult.ProratedAmount, newPlan.Currency,
                PaymentStatus.Completed, $"{newPlan.Name} - {interval}",
                providerResult.NewPeriodStart, providerResult.NewPeriodEnd);
            context.PaymentRecords.Add(payment);
        }

        await context.SaveChangesAsync(cancellationToken);
        return Result.Success(Unit.Value);
    }
}
```

```csharp
// ChangePlanCommandValidator.cs
using FluentValidation;

namespace Starter.Application.Features.Billing.Commands.ChangePlan;

public sealed class ChangePlanCommandValidator : AbstractValidator<ChangePlanCommand>
{
    public ChangePlanCommandValidator()
    {
        RuleFor(x => x.PlanId).NotEmpty();
    }
}
```

- [ ] **Step 5:** Create `CancelSubscriptionCommand.cs` + handler

```csharp
// CancelSubscriptionCommand.cs
using MediatR;
using Starter.Shared.Results;

namespace Starter.Application.Features.Billing.Commands.CancelSubscription;

public sealed record CancelSubscriptionCommand(Guid? TenantId = null) : IRequest<Result<Unit>>;
```

```csharp
// CancelSubscriptionCommandHandler.cs
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
    IBillingProvider billingProvider) : IRequestHandler<CancelSubscriptionCommand, Result<Unit>>
{
    public async Task<Result<Unit>> Handle(CancelSubscriptionCommand request, CancellationToken cancellationToken)
    {
        var tenantId = request.TenantId ?? currentUser.TenantId;
        if (tenantId is null)
            return Result.Failure<Unit>(BillingErrors.SubscriptionNotFound);

        var subscription = await context.TenantSubscriptions
            .IgnoreQueryFilters()
            .Include(s => s.Plan)
            .FirstOrDefaultAsync(s => s.TenantId == tenantId.Value, cancellationToken);

        if (subscription is null)
            return Result.Failure<Unit>(BillingErrors.SubscriptionNotFound);

        if (subscription.Plan.IsFree)
            return Result.Failure<Unit>(BillingErrors.CannotCancelFreePlan);

        // Cancel with provider
        if (subscription.ExternalSubscriptionId is not null)
            await billingProvider.CancelSubscriptionAsync(subscription.ExternalSubscriptionId, cancellationToken);

        subscription.Cancel();

        // Downgrade to free plan
        var freePlan = await context.SubscriptionPlans
            .FirstAsync(p => p.IsFree && p.IsActive, cancellationToken);

        var now = DateTime.UtcNow;
        subscription.ChangePlan(freePlan.Id, 0, 0, freePlan.Currency, now, now.AddYears(100));

        await context.SaveChangesAsync(cancellationToken);
        return Result.Success(Unit.Value);
    }
}
```

- [ ] **Step 6:** Create `ResyncPlanTenantsCommand.cs` + handler

```csharp
// ResyncPlanTenantsCommand.cs
using MediatR;
using Starter.Shared.Results;

namespace Starter.Application.Features.Billing.Commands.ResyncPlanTenants;

public sealed record ResyncPlanTenantsCommand(Guid PlanId) : IRequest<Result<int>>;
```

```csharp
// ResyncPlanTenantsCommandHandler.cs
using MediatR;
using Microsoft.EntityFrameworkCore;
using Starter.Application.Common.Interfaces;
using Starter.Domain.Billing.Enums;
using Starter.Domain.Billing.Errors;
using Starter.Domain.Billing.Events;
using Starter.Shared.Results;

namespace Starter.Application.Features.Billing.Commands.ResyncPlanTenants;

internal sealed class ResyncPlanTenantsCommandHandler(
    IApplicationDbContext context,
    ISender mediator) : IRequestHandler<ResyncPlanTenantsCommand, Result<int>>
{
    public async Task<Result<int>> Handle(ResyncPlanTenantsCommand request, CancellationToken cancellationToken)
    {
        var plan = await context.SubscriptionPlans
            .FirstOrDefaultAsync(p => p.Id == request.PlanId, cancellationToken);

        if (plan is null)
            return Result.Failure<int>(BillingErrors.PlanNotFound);

        var subscriptions = await context.TenantSubscriptions
            .IgnoreQueryFilters()
            .Where(s => s.SubscriptionPlanId == request.PlanId && s.Status == SubscriptionStatus.Active)
            .ToListAsync(cancellationToken);

        foreach (var sub in subscriptions)
            sub.AddDomainEvent(new SubscriptionChangedEvent(sub.TenantId, plan.Id, plan.Id));

        await context.SaveChangesAsync(cancellationToken);
        return Result.Success(subscriptions.Count);
    }
}
```

- [ ] **Step 7:** Verify build, commit

```
feat(billing): add billing commands (create/update/deactivate plan, change plan, cancel, resync)
```

---

## Task 9: SyncPlanFeaturesHandler (Domain Event Handler)

**Files:**
- Create: `boilerplateBE/src/Starter.Application/Features/Billing/EventHandlers/SyncPlanFeaturesHandler.cs`

- [ ] **Step 1:** Create `SyncPlanFeaturesHandler.cs`

```csharp
using MediatR;
using Microsoft.EntityFrameworkCore;
using Starter.Application.Common.Interfaces;
using Starter.Domain.Billing.Events;
using Starter.Domain.FeatureFlags.Entities;
using Starter.Domain.FeatureFlags.Enums;
using System.Text.Json;

namespace Starter.Application.Features.Billing.EventHandlers;

internal sealed class SyncPlanFeaturesHandler(
    IApplicationDbContext context,
    IFeatureFlagService flagService) : INotificationHandler<SubscriptionChangedEvent>
{
    public async Task Handle(SubscriptionChangedEvent notification, CancellationToken cancellationToken)
    {
        // Load new plan's features
        var plan = await context.SubscriptionPlans
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == notification.NewPlanId, cancellationToken);

        if (plan is null) return;

        var planFeatures = JsonSerializer.Deserialize<Dictionary<string, string>>(plan.Features)
            ?? new Dictionary<string, string>();

        // Load all feature flags to get IDs by key
        var allFlags = await context.FeatureFlags
            .AsNoTracking()
            .ToDictionaryAsync(f => f.Key, f => f.Id, cancellationToken);

        // Load existing tenant overrides
        var existingOverrides = await context.TenantFeatureFlags
            .IgnoreQueryFilters()
            .Where(t => t.TenantId == notification.TenantId)
            .ToListAsync(cancellationToken);

        var overrideMap = existingOverrides.ToDictionary(o => o.FeatureFlagId);

        // Sync plan features
        foreach (var (flagKey, flagValue) in planFeatures)
        {
            if (!allFlags.TryGetValue(flagKey, out var flagId)) continue;

            if (overrideMap.TryGetValue(flagId, out var existing))
            {
                // Only overwrite PlanSubscription-sourced overrides (preserve Manual)
                if (existing.Source == OverrideSource.PlanSubscription)
                    existing.UpdateValue(flagValue, OverrideSource.PlanSubscription);
            }
            else
            {
                // Create new override
                var newOverride = TenantFeatureFlag.Create(
                    notification.TenantId, flagId, flagValue, OverrideSource.PlanSubscription);
                context.TenantFeatureFlags.Add(newOverride);
            }
        }

        // Remove PlanSubscription overrides for flags NOT in the new plan
        var planFlagIds = planFeatures.Keys
            .Where(k => allFlags.ContainsKey(k))
            .Select(k => allFlags[k])
            .ToHashSet();

        foreach (var existing in existingOverrides)
        {
            if (existing.Source == OverrideSource.PlanSubscription && !planFlagIds.Contains(existing.FeatureFlagId))
                context.TenantFeatureFlags.Remove(existing);
        }

        await context.SaveChangesAsync(cancellationToken);

        // Invalidate feature flag cache for this tenant
        await flagService.InvalidateCacheAsync(notification.TenantId, cancellationToken);
    }
}
```

- [ ] **Step 2:** Verify build, commit

```
feat(billing): add SyncPlanFeaturesHandler to sync plan limits to tenant feature flags
```

---

## Task 10: BillingController

**Files:**
- Create: `boilerplateBE/src/Starter.Api/Controllers/BillingController.cs`

- [ ] **Step 1:** Create `BillingController.cs`

```csharp
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Starter.Application.Features.Billing.Commands.CancelSubscription;
using Starter.Application.Features.Billing.Commands.ChangePlan;
using Starter.Application.Features.Billing.Commands.CreatePlan;
using Starter.Application.Features.Billing.Commands.DeactivatePlan;
using Starter.Application.Features.Billing.Commands.ResyncPlanTenants;
using Starter.Application.Features.Billing.Commands.UpdatePlan;
using Starter.Application.Features.Billing.Queries.GetPayments;
using Starter.Application.Features.Billing.Queries.GetPlanById;
using Starter.Application.Features.Billing.Queries.GetPlans;
using Starter.Application.Features.Billing.Queries.GetSubscription;
using Starter.Application.Features.Billing.Queries.GetUsage;
using Starter.Domain.Billing.Enums;
using Starter.Shared.Constants;

namespace Starter.Api.Controllers;

public sealed class BillingController(ISender mediator) : BaseApiController(mediator)
{
    // ─── Public ───

    [HttpGet("plans")]
    [AllowAnonymous]
    public async Task<IActionResult> GetPlans(CancellationToken ct = default)
    {
        var result = await Mediator.Send(new GetPlansQuery(PublicOnly: true), ct);
        return HandleResult(result);
    }

    // ─── Tenant ───

    [HttpGet("subscription")]
    [Authorize(Policy = Permissions.Billing.View)]
    public async Task<IActionResult> GetSubscription(CancellationToken ct = default)
    {
        var result = await Mediator.Send(new GetSubscriptionQuery(), ct);
        return HandleResult(result);
    }

    [HttpPost("change-plan")]
    [Authorize(Policy = Permissions.Billing.Manage)]
    public async Task<IActionResult> ChangePlan(
        [FromBody] ChangePlanRequest request, CancellationToken ct = default)
    {
        var result = await Mediator.Send(
            new ChangePlanCommand(request.PlanId, request.Interval), ct);
        return HandleResult(result);
    }

    [HttpPost("cancel")]
    [Authorize(Policy = Permissions.Billing.Manage)]
    public async Task<IActionResult> CancelSubscription(CancellationToken ct = default)
    {
        var result = await Mediator.Send(new CancelSubscriptionCommand(), ct);
        return HandleResult(result);
    }

    [HttpGet("payments")]
    [Authorize(Policy = Permissions.Billing.View)]
    public async Task<IActionResult> GetPayments(
        [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        var result = await Mediator.Send(new GetPaymentsQuery(pageNumber, pageSize), ct);
        return HandlePagedResult(result);
    }

    [HttpGet("usage")]
    [Authorize(Policy = Permissions.Billing.View)]
    public async Task<IActionResult> GetUsage(CancellationToken ct = default)
    {
        var result = await Mediator.Send(new GetUsageQuery(), ct);
        return HandleResult(result);
    }

    // ─── SuperAdmin Plan Management ───

    [HttpGet("plans/manage")]
    [Authorize(Policy = Permissions.Billing.ViewPlans)]
    public async Task<IActionResult> GetAllPlans(
        [FromQuery] bool includeInactive = true, CancellationToken ct = default)
    {
        var result = await Mediator.Send(new GetPlansQuery(IncludeInactive: includeInactive), ct);
        return HandleResult(result);
    }

    [HttpGet("plans/{id:guid}")]
    [Authorize(Policy = Permissions.Billing.ViewPlans)]
    public async Task<IActionResult> GetPlan(Guid id, CancellationToken ct = default)
    {
        var result = await Mediator.Send(new GetPlanByIdQuery(id), ct);
        return HandleResult(result);
    }

    [HttpPost("plans/create")]
    [Authorize(Policy = Permissions.Billing.ManagePlans)]
    public async Task<IActionResult> CreatePlan(
        [FromBody] CreatePlanCommand command, CancellationToken ct = default)
    {
        var result = await Mediator.Send(command, ct);
        return HandleResult(result);
    }

    [HttpPut("plans/{id:guid}")]
    [Authorize(Policy = Permissions.Billing.ManagePlans)]
    public async Task<IActionResult> UpdatePlan(
        Guid id, [FromBody] UpdatePlanCommand command, CancellationToken ct = default)
    {
        if (id != command.Id) return BadRequest();
        var result = await Mediator.Send(command, ct);
        return HandleResult(result);
    }

    [HttpDelete("plans/{id:guid}")]
    [Authorize(Policy = Permissions.Billing.ManagePlans)]
    public async Task<IActionResult> DeactivatePlan(Guid id, CancellationToken ct = default)
    {
        var result = await Mediator.Send(new DeactivatePlanCommand(id), ct);
        return HandleResult(result);
    }

    [HttpPost("plans/{id:guid}/resync")]
    [Authorize(Policy = Permissions.Billing.ManagePlans)]
    public async Task<IActionResult> ResyncPlanTenants(Guid id, CancellationToken ct = default)
    {
        var result = await Mediator.Send(new ResyncPlanTenantsCommand(id), ct);
        return HandleResult(result);
    }

    // ─── SuperAdmin Tenant Subscription Management ───

    [HttpGet("tenants/{tenantId:guid}/subscription")]
    [Authorize(Policy = Permissions.Billing.ManageTenantSubscriptions)]
    public async Task<IActionResult> GetTenantSubscription(Guid tenantId, CancellationToken ct = default)
    {
        var result = await Mediator.Send(new GetSubscriptionQuery(tenantId), ct);
        return HandleResult(result);
    }

    [HttpPost("tenants/{tenantId:guid}/change-plan")]
    [Authorize(Policy = Permissions.Billing.ManageTenantSubscriptions)]
    public async Task<IActionResult> ChangeTenantPlan(
        Guid tenantId, [FromBody] ChangePlanRequest request, CancellationToken ct = default)
    {
        var result = await Mediator.Send(
            new ChangePlanCommand(request.PlanId, request.Interval, tenantId), ct);
        return HandleResult(result);
    }
}

// Request DTOs
public sealed record ChangePlanRequest(Guid PlanId, BillingInterval? Interval = null);
```

- [ ] **Step 2:** Verify build, commit

```
feat(api): add BillingController with all billing endpoints
```

---

## Task 11: Seed Data + Auto-Assign Free Plan on Tenant Registration

**Files:**
- Modify: `boilerplateBE/src/Starter.Infrastructure/Persistence/Seeds/DataSeeder.cs`
- Modify: `boilerplateBE/src/Starter.Application/Features/Tenants/Commands/RegisterTenant/RegisterTenantCommandHandler.cs`

- [ ] **Step 1:** Add `SeedSubscriptionPlansAsync` method to `DataSeeder.cs`

Add a new private method and call it from the main seed method:

```csharp
private static async Task SeedSubscriptionPlansAsync(ApplicationDbContext context, ILogger logger)
{
    if (await context.SubscriptionPlans.AnyAsync())
    {
        logger.LogInformation("Subscription plans already seeded");
        return;
    }

    var freeFeatures = @"{""users.max_count"":""5"",""users.invitations_enabled"":""true"",""files.max_storage_mb"":""1024"",""files.max_upload_size_mb"":""10"",""reports.enabled"":""false"",""reports.pdf_export"":""false"",""api_keys.enabled"":""true"",""api_keys.max_count"":""2""}";
    var starterFeatures = @"{""users.max_count"":""25"",""users.invitations_enabled"":""true"",""files.max_storage_mb"":""10240"",""files.max_upload_size_mb"":""25"",""reports.enabled"":""true"",""reports.max_concurrent"":""3"",""reports.pdf_export"":""false"",""api_keys.enabled"":""true"",""api_keys.max_count"":""5""}";
    var proFeatures = @"{""users.max_count"":""100"",""users.invitations_enabled"":""true"",""files.max_storage_mb"":""51200"",""files.max_upload_size_mb"":""50"",""reports.enabled"":""true"",""reports.max_concurrent"":""5"",""reports.pdf_export"":""true"",""api_keys.enabled"":""true"",""api_keys.max_count"":""20""}";
    var enterpriseFeatures = @"{""users.max_count"":""500"",""users.invitations_enabled"":""true"",""files.max_storage_mb"":""204800"",""files.max_upload_size_mb"":""100"",""reports.enabled"":""true"",""reports.max_concurrent"":""10"",""reports.pdf_export"":""true"",""api_keys.enabled"":""true"",""api_keys.max_count"":""50""}";

    var translations = @"{""en"":{""name"":""{0}"",""description"":""{1}""},""ar"":{""name"":""{2}"",""description"":""{3}""},""ku"":{""name"":""{4}"",""description"":""{5}""}}";

    var plans = new[]
    {
        SubscriptionPlan.Create("Free", "free", "Get started with basic features",
            string.Format(translations, "Free", "Get started with basic features", "مجاني", "ابدأ بالميزات الأساسية", "بەخۆڕایی", "دەست پێبکە بە تایبەتمەندییە بنەڕەتییەکان"),
            0, 0, "USD", freeFeatures, true, true, 0),
        SubscriptionPlan.Create("Starter", "starter", "For small teams getting started",
            string.Format(translations, "Starter", "For small teams getting started", "المبتدئ", "للفرق الصغيرة في البداية", "دەستپێکەر", "بۆ تیمە بچووکەکان لە سەرەتادا"),
            29, 290, "USD", starterFeatures, false, true, 1),
        SubscriptionPlan.Create("Pro", "pro", "For growing teams that need advanced features",
            string.Format(translations, "Pro", "For growing teams that need advanced features", "احترافي", "للفرق المتنامية التي تحتاج ميزات متقدمة", "پڕۆ", "بۆ تیمە گەشەسەندەکان"),
            99, 990, "USD", proFeatures, false, true, 2),
        SubscriptionPlan.Create("Enterprise", "enterprise", "For large organizations with advanced needs",
            string.Format(translations, "Enterprise", "For large organizations with advanced needs", "المؤسسات", "للمؤسسات الكبيرة ذات الاحتياجات المتقدمة", "ئینتەرپرایز", "بۆ دامەزراوە گەورەکان"),
            299, 2990, "USD", enterpriseFeatures, false, true, 3),
    };

    context.SubscriptionPlans.AddRange(plans);
    await context.SaveChangesAsync();

    // Create initial price history for each plan
    var systemUserId = (await context.Users.IgnoreQueryFilters().FirstAsync(u => u.Username == "superadmin")).Id;
    foreach (var plan in plans)
    {
        context.PlanPriceHistories.Add(PlanPriceHistory.Create(
            plan.Id, plan.MonthlyPrice, plan.AnnualPrice, plan.Currency, systemUserId, "Initial plan creation"));
    }
    await context.SaveChangesAsync();

    logger.LogInformation("Seeded {Count} subscription plans with price history", plans.Length);
}
```

Call from the main `SeedAsync` method after feature flags are seeded.

- [ ] **Step 2:** Update `RegisterTenantCommandHandler` to auto-assign Free plan

After the user is created and saved, add:

```csharp
// Auto-assign Free plan
var freePlan = await context.SubscriptionPlans
    .FirstOrDefaultAsync(p => p.IsFree && p.IsActive, cancellationToken);

if (freePlan is not null)
{
    var now = DateTime.UtcNow;
    var subscription = TenantSubscription.Create(
        tenant.Id, freePlan.Id, 0, 0, freePlan.Currency,
        BillingInterval.Monthly, now, now.AddYears(100));
    context.TenantSubscriptions.Add(subscription);

    // Initialize usage counter
    await usageTracker.SetAsync(tenant.Id, "users", 1, cancellationToken);
}
```

Add `IUsageTracker usageTracker` to the handler's primary constructor.

- [ ] **Step 3:** Verify build, commit

```
feat(billing): seed 4 subscription plans and auto-assign Free plan on tenant registration
```

---

## Task 12: Refactor Existing Handlers to Use IUsageTracker

**Files:**
- Modify: `boilerplateBE/src/Starter.Application/Features/Auth/Commands/Register/RegisterUserCommandHandler.cs`
- Modify: `boilerplateBE/src/Starter.Application/Features/Files/Commands/UploadFile/UploadFileCommandHandler.cs`
- Modify: `boilerplateBE/src/Starter.Application/Features/ApiKeys/Commands/CreateApiKey/CreateApiKeyCommandHandler.cs`

- [ ] **Step 1:** Update `RegisterUserCommandHandler` — replace COUNT with IUsageTracker

Add `IUsageTracker usageTracker` and `ICurrentUserService currentUser` to primary constructor.

Replace:
```csharp
var maxUsers = await flags.GetValueAsync<int>("users.max_count", cancellationToken);
var currentCount = await context.Users.CountAsync(cancellationToken);
if (currentCount >= maxUsers)
    return Result.Failure<Guid>(FeatureFlagErrors.QuotaExceeded("users", maxUsers));
```

With:
```csharp
var tenantId = currentUser.TenantId;
if (tenantId.HasValue)
{
    var maxUsers = await flags.GetValueAsync<int>("users.max_count", cancellationToken);
    var currentCount = await usageTracker.GetAsync(tenantId.Value, "users", cancellationToken);
    if (currentCount >= maxUsers)
        return Result.Failure<Guid>(FeatureFlagErrors.QuotaExceeded("users", maxUsers));
}
```

After `await context.SaveChangesAsync(cancellationToken)` add:
```csharp
if (tenantId.HasValue)
    await usageTracker.IncrementAsync(tenantId.Value, "users", cancellationToken: cancellationToken);
```

- [ ] **Step 2:** Update `UploadFileCommandHandler` — replace SUM with IUsageTracker

Add `IUsageTracker usageTracker` to primary constructor.

Replace storage check:
```csharp
var maxStorageMb = await flags.GetValueAsync<int>("files.max_storage_mb", cancellationToken);
var usedBytes = await context.FileMetadata.SumAsync(f => f.Size, cancellationToken);
var usedMb = (int)(usedBytes / (1024 * 1024));
if (usedMb + fileSizeMb > maxStorageMb)
    return Result.Failure<FileDto>(FeatureFlagErrors.QuotaExceeded(...));
```

With:
```csharp
var tenantId = currentUser.TenantId;
if (tenantId.HasValue)
{
    var maxStorageMb = await flags.GetValueAsync<int>("files.max_storage_mb", cancellationToken);
    var usedBytes = await usageTracker.GetAsync(tenantId.Value, "storage_bytes", cancellationToken);
    var usedMb = (int)(usedBytes / (1024 * 1024));
    if (usedMb + fileSizeMb > maxStorageMb)
        return Result.Failure<FileDto>(FeatureFlagErrors.QuotaExceeded("storage", maxStorageMb));
}
```

After successful upload add:
```csharp
if (tenantId.HasValue)
    await usageTracker.IncrementAsync(tenantId.Value, "storage_bytes", request.Size, cancellationToken);
```

- [ ] **Step 3:** Update `CreateApiKeyCommandHandler` — replace COUNT with IUsageTracker

Same pattern: replace `context.ApiKeys.CountAsync` with `usageTracker.GetAsync`, add `IncrementAsync` after save.

- [ ] **Step 4:** Verify build, commit

```
refactor(handlers): replace COUNT/SUM queries with IUsageTracker Redis counters
```

---

## Task 13: Frontend — Types, API Config, Query Keys, Permissions

**Files:**
- Create: `boilerplateFE/src/types/billing.types.ts`
- Modify: `boilerplateFE/src/config/api.config.ts`
- Modify: `boilerplateFE/src/lib/query/keys.ts`
- Modify: `boilerplateFE/src/constants/permissions.ts` (if not done in Task 5)

- [ ] **Step 1:** Create `billing.types.ts`

```typescript
export interface SubscriptionPlan {
  id: string;
  name: string;
  slug: string;
  description: string | null;
  translations: string | null;
  monthlyPrice: number;
  annualPrice: number;
  currency: string;
  features: string;
  isFree: boolean;
  isActive: boolean;
  isPublic: boolean;
  displayOrder: number;
  trialDays: number;
  subscriberCount: number;
  createdAt: string;
  modifiedAt: string | null;
}

export interface TenantSubscription {
  id: string;
  tenantId: string;
  subscriptionPlanId: string;
  planName: string;
  planSlug: string;
  status: 'Trialing' | 'Active' | 'PastDue' | 'Canceled' | 'Expired';
  lockedMonthlyPrice: number;
  lockedAnnualPrice: number;
  currency: string;
  billingInterval: 'Monthly' | 'Annual';
  currentPeriodStart: string;
  currentPeriodEnd: string;
  canceledAt: string | null;
  autoRenew: boolean;
  createdAt: string;
}

export interface PaymentRecord {
  id: string;
  amount: number;
  currency: string;
  status: 'Pending' | 'Completed' | 'Failed' | 'Refunded';
  description: string | null;
  periodStart: string;
  periodEnd: string;
  createdAt: string;
}

export interface Usage {
  users: number;
  storageBytes: number;
  apiKeys: number;
  reportsActive: number;
  maxUsers: number;
  maxStorageBytes: number;
  maxApiKeys: number;
  maxReports: number;
}

export interface CreatePlanData {
  name: string;
  slug: string;
  description?: string;
  translations?: string;
  monthlyPrice: number;
  annualPrice: number;
  currency: string;
  features: string;
  isFree: boolean;
  isPublic: boolean;
  displayOrder: number;
  trialDays: number;
}

export interface UpdatePlanData extends CreatePlanData {
  id: string;
  priceChangeReason?: string;
}

export interface ChangePlanData {
  planId: string;
  interval?: 'Monthly' | 'Annual';
}
```

- [ ] **Step 2:** Add billing endpoints to `api.config.ts`

```typescript
BILLING: {
  PLANS: '/Billing/plans',
  PLANS_MANAGE: '/Billing/plans/manage',
  PLANS_CREATE: '/Billing/plans/create',
  PLAN_DETAIL: (id: string) => `/Billing/plans/${id}`,
  PLAN_RESYNC: (id: string) => `/Billing/plans/${id}/resync`,
  SUBSCRIPTION: '/Billing/subscription',
  CHANGE_PLAN: '/Billing/change-plan',
  CANCEL: '/Billing/cancel',
  PAYMENTS: '/Billing/payments',
  USAGE: '/Billing/usage',
  TENANT_SUBSCRIPTION: (tenantId: string) => `/Billing/tenants/${tenantId}/subscription`,
  TENANT_CHANGE_PLAN: (tenantId: string) => `/Billing/tenants/${tenantId}/change-plan`,
},
```

- [ ] **Step 3:** Add billing query keys to `keys.ts`

```typescript
billing: {
  all: ['billing'] as const,
  plans: {
    all: ['billing', 'plans'] as const,
    list: (params?: Record<string, unknown>) => ['billing', 'plans', 'list', params] as const,
    detail: (id: string) => ['billing', 'plans', 'detail', id] as const,
  },
  subscription: {
    all: ['billing', 'subscription'] as const,
    current: () => ['billing', 'subscription', 'current'] as const,
    tenant: (tenantId: string) => ['billing', 'subscription', 'tenant', tenantId] as const,
  },
  usage: {
    all: ['billing', 'usage'] as const,
    current: () => ['billing', 'usage', 'current'] as const,
  },
  payments: {
    all: ['billing', 'payments'] as const,
    list: (params?: Record<string, unknown>) => ['billing', 'payments', 'list', params] as const,
  },
},
```

- [ ] **Step 4:** Verify build, commit

```
feat(frontend): add billing types, API config, and query keys
```

---

## Task 14: Frontend — API Module + React Query Hooks

**Files:**
- Create: `boilerplateFE/src/features/billing/api/billing.api.ts`
- Create: `boilerplateFE/src/features/billing/api/billing.queries.ts`
- Create: `boilerplateFE/src/features/billing/api/index.ts`

- [ ] **Step 1:** Create `billing.api.ts`

```typescript
import { apiClient } from '@/lib/axios';
import { API_ENDPOINTS } from '@/config';
import type { CreatePlanData, UpdatePlanData, ChangePlanData } from '@/types/billing.types';

export const billingApi = {
  // Public
  getPlans: () => apiClient.get(API_ENDPOINTS.BILLING.PLANS).then(r => r.data),

  // Tenant
  getSubscription: () => apiClient.get(API_ENDPOINTS.BILLING.SUBSCRIPTION).then(r => r.data),
  changePlan: (data: ChangePlanData) => apiClient.post(API_ENDPOINTS.BILLING.CHANGE_PLAN, data).then(r => r.data),
  cancelSubscription: () => apiClient.post(API_ENDPOINTS.BILLING.CANCEL).then(r => r.data),
  getPayments: (params?: Record<string, unknown>) => apiClient.get(API_ENDPOINTS.BILLING.PAYMENTS, { params }).then(r => r.data),
  getUsage: () => apiClient.get(API_ENDPOINTS.BILLING.USAGE).then(r => r.data),

  // SuperAdmin
  getAllPlans: (params?: Record<string, unknown>) => apiClient.get(API_ENDPOINTS.BILLING.PLANS_MANAGE, { params }).then(r => r.data),
  getPlanById: (id: string) => apiClient.get(API_ENDPOINTS.BILLING.PLAN_DETAIL(id)).then(r => r.data),
  createPlan: (data: CreatePlanData) => apiClient.post(API_ENDPOINTS.BILLING.PLANS_CREATE, data).then(r => r.data),
  updatePlan: (data: UpdatePlanData) => apiClient.put(API_ENDPOINTS.BILLING.PLAN_DETAIL(data.id), data).then(r => r.data),
  deactivatePlan: (id: string) => apiClient.delete(API_ENDPOINTS.BILLING.PLAN_DETAIL(id)).then(r => r.data),
  resyncPlan: (id: string) => apiClient.post(API_ENDPOINTS.BILLING.PLAN_RESYNC(id)).then(r => r.data),
  getTenantSubscription: (tenantId: string) => apiClient.get(API_ENDPOINTS.BILLING.TENANT_SUBSCRIPTION(tenantId)).then(r => r.data),
  changeTenantPlan: (tenantId: string, data: ChangePlanData) => apiClient.post(API_ENDPOINTS.BILLING.TENANT_CHANGE_PLAN(tenantId), data).then(r => r.data),
};
```

- [ ] **Step 2:** Create `billing.queries.ts`

```typescript
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { queryKeys } from '@/lib/query/keys';
import { billingApi } from './billing.api';
import { toast } from 'sonner';
import i18n from '@/i18n';
import type { CreatePlanData, UpdatePlanData, ChangePlanData } from '@/types/billing.types';

export function usePlans(params?: Record<string, unknown>) {
  return useQuery({
    queryKey: queryKeys.billing.plans.list(params),
    queryFn: () => billingApi.getPlans(),
  });
}

export function useAllPlans(params?: Record<string, unknown>) {
  return useQuery({
    queryKey: queryKeys.billing.plans.list({ ...params, manage: true }),
    queryFn: () => billingApi.getAllPlans(params),
  });
}

export function usePlan(id: string) {
  return useQuery({
    queryKey: queryKeys.billing.plans.detail(id),
    queryFn: () => billingApi.getPlanById(id),
    enabled: !!id,
  });
}

export function useSubscription() {
  return useQuery({
    queryKey: queryKeys.billing.subscription.current(),
    queryFn: () => billingApi.getSubscription(),
  });
}

export function useUsage() {
  return useQuery({
    queryKey: queryKeys.billing.usage.current(),
    queryFn: () => billingApi.getUsage(),
  });
}

export function usePayments(params?: Record<string, unknown>) {
  return useQuery({
    queryKey: queryKeys.billing.payments.list(params),
    queryFn: () => billingApi.getPayments(params),
  });
}

export function useCreatePlan() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (data: CreatePlanData) => billingApi.createPlan(data),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: queryKeys.billing.plans.all });
      toast.success(i18n.t('billing.planCreated'));
    },
  });
}

export function useUpdatePlan() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (data: UpdatePlanData) => billingApi.updatePlan(data),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: queryKeys.billing.plans.all });
      toast.success(i18n.t('billing.planUpdated'));
    },
  });
}

export function useDeactivatePlan() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => billingApi.deactivatePlan(id),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: queryKeys.billing.plans.all });
      toast.success(i18n.t('billing.planDeactivated'));
    },
  });
}

export function useChangePlan() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (data: ChangePlanData) => billingApi.changePlan(data),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: queryKeys.billing.all });
      toast.success(i18n.t('billing.planChanged'));
    },
  });
}

export function useCancelSubscription() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: () => billingApi.cancelSubscription(),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: queryKeys.billing.all });
      toast.success(i18n.t('billing.subscriptionCanceled'));
    },
  });
}

export function useResyncPlan() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => billingApi.resyncPlan(id),
    onSuccess: (_data, _variables) => {
      qc.invalidateQueries({ queryKey: queryKeys.billing.all });
      toast.success(i18n.t('billing.planResynced'));
    },
  });
}
```

- [ ] **Step 3:** Create `index.ts` barrel export

```typescript
export * from './billing.api';
export * from './billing.queries';
```

- [ ] **Step 4:** Verify build, commit

```
feat(frontend): add billing API module and React Query hooks
```

---

## Task 15: Frontend — Routes, Sidebar, i18n

**Files:**
- Modify: `boilerplateFE/src/config/routes.config.ts`
- Modify: `boilerplateFE/src/routes/routes.tsx`
- Modify: `boilerplateFE/src/components/layout/MainLayout/Sidebar.tsx`
- Modify: `boilerplateFE/src/i18n/locales/en/translation.json`
- Modify: `boilerplateFE/src/i18n/locales/ar/translation.json`
- Modify: `boilerplateFE/src/i18n/locales/ku/translation.json`

- [ ] **Step 1:** Add routes to `routes.config.ts`

```typescript
BILLING: '/billing',
BILLING_PLANS: '/billing/plans',
PRICING: '/pricing',
```

- [ ] **Step 2:** Add pages to `routes.tsx`

Add lazy imports:
```typescript
const BillingPage = lazy(() => import('@/features/billing/pages/BillingPage'));
const BillingPlansPage = lazy(() => import('@/features/billing/pages/BillingPlansPage'));
const PricingPage = lazy(() => import('@/features/billing/pages/PricingPage'));
```

Add `/pricing` as a public route (next to landing), and `/billing` + `/billing/plans` as auth routes with PermissionGuard.

- [ ] **Step 3:** Add billing nav item to `Sidebar.tsx`

```typescript
...(hasPermission(PERMISSIONS.Billing.View)
  ? [{ label: t('nav.billing'), icon: CreditCard, path: ROUTES.BILLING }]
  : []),
```

Import `CreditCard` from `lucide-react`.

- [ ] **Step 4:** Add i18n keys for all 3 locales

English (`en/translation.json`) — add `billing` section:
```json
"billing": {
  "title": "Billing",
  "subtitle": "Manage your subscription and billing",
  "currentPlan": "Current Plan",
  "usage": "Usage",
  "payments": "Payment History",
  "changePlan": "Change Plan",
  "cancelSubscription": "Cancel Subscription",
  "cancelConfirm": "Are you sure you want to cancel? You will be downgraded to the Free plan.",
  "upgrade": "Upgrade",
  "downgrade": "Downgrade",
  "currentLabel": "Current",
  "freeLabel": "Free",
  "monthly": "Monthly",
  "annual": "Annual",
  "savePercent": "Save {{percent}}%",
  "perMonth": "/mo",
  "perYear": "/yr",
  "planCreated": "Plan created successfully",
  "planUpdated": "Plan updated successfully",
  "planDeactivated": "Plan deactivated",
  "planChanged": "Plan changed successfully",
  "subscriptionCanceled": "Subscription canceled",
  "planResynced": "Plan tenants resynced",
  "plans": "Plans",
  "plansSubtitle": "Manage subscription plans",
  "createPlan": "Create Plan",
  "editPlan": "Edit Plan",
  "features": "Features",
  "subscribers": "Subscribers",
  "priceHistory": "Price History",
  "resync": "Resync Tenants",
  "pricingTitle": "Choose Your Plan",
  "pricingSubtitle": "Select the plan that best fits your needs",
  "getStarted": "Get Started",
  "usersUsage": "Users",
  "storageUsage": "Storage",
  "apiKeysUsage": "API Keys",
  "noPayments": "No payment history yet"
}
```

Add similar sections for Arabic and Kurdish (translated).

Also add `"billing": "Billing"` to the `nav` section.

- [ ] **Step 5:** Verify build, commit

```
feat(frontend): add billing routes, sidebar nav, and i18n translations
```

---

## Task 16: Frontend — BillingPage (Tenant View)

**Files:**
- Create: `boilerplateFE/src/features/billing/pages/BillingPage.tsx`
- Create: `boilerplateFE/src/features/billing/components/UsageBar.tsx`
- Create: `boilerplateFE/src/features/billing/components/PlanSelectorModal.tsx`

This task creates the tenant admin's billing page showing current subscription, usage bars, and payment history. The page uses `useSubscription()`, `useUsage()`, `usePayments()`, `usePlans()`, and `useChangePlan()` hooks.

**UsageBar component** renders a progress bar showing current/max with label and percentage.

**PlanSelectorModal** shows available plans as cards with monthly/annual toggle and "Select" button.

- [ ] **Step 1:** Create `UsageBar.tsx`, `PlanSelectorModal.tsx`, `BillingPage.tsx`
- [ ] **Step 2:** Verify build, commit

```
feat(frontend): add BillingPage with subscription, usage, and payments
```

---

## Task 17: Frontend — BillingPlansPage (SuperAdmin) + PricingPage (Public)

**Files:**
- Create: `boilerplateFE/src/features/billing/pages/BillingPlansPage.tsx`
- Create: `boilerplateFE/src/features/billing/pages/PricingPage.tsx`
- Create: `boilerplateFE/src/features/billing/components/CreatePlanDialog.tsx`
- Create: `boilerplateFE/src/features/billing/components/EditPlanDialog.tsx`
- Create: `boilerplateFE/src/features/billing/components/FeatureMappingEditor.tsx`
- Create: `boilerplateFE/src/features/billing/components/PlanCard.tsx`

**BillingPlansPage** (SuperAdmin) shows all plans as cards/table with create, edit, deactivate, resync actions. Features the translations editor (tabs per locale) and FeatureMappingEditor (flag key dropdown + value input).

**PricingPage** (public) shows active plans as comparison cards with monthly/annual toggle and "Get Started" / "Upgrade" CTAs.

**PlanCard** is a reusable component rendering a plan with feature list, price, and action button.

- [ ] **Step 1:** Create all components
- [ ] **Step 2:** Verify build, commit

```
feat(frontend): add BillingPlansPage (SuperAdmin) and PricingPage (public)
```

---

## Task 18: Build Verification + Final Commit

- [ ] **Step 1:** Full backend build

Run: `dotnet build` from `boilerplateBE/`
Expected: 0 errors

- [ ] **Step 2:** Full frontend build

Run: `npm run build` from `boilerplateFE/`
Expected: 0 errors

- [ ] **Step 3:** Final commit if any remaining changes

```
chore: billing feature build verification
```

---

## Execution Notes

- **Do NOT create EF migration** in the boilerplate — only when starting post-feature testing
- Tasks 1-12 are backend, Tasks 13-17 are frontend, Task 18 is verification
- Tasks 16-17 have less code detail because UI components vary based on existing patterns — the subagent should reference existing pages like `FeatureFlagsPage.tsx` for component structure
- The `IDomainEvent` interface exists in `Starter.Domain.Common` — check exact import path
- `AddDomainEvent` is a method on `AggregateRoot` — domain events are dispatched by `DomainEventDispatcherInterceptor` on SaveChanges
