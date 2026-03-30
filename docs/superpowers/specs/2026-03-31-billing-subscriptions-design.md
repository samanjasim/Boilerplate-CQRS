# Billing & Subscriptions — Design Specification

## Overview

A feature-flag-driven billing system where subscription plans are "feature flag presets." Each plan maps flag keys to entitlement values. When a tenant changes plans, a domain event syncs the plan's limits into tenant feature flag overrides. All quota enforcement uses the existing `IFeatureFlagService` — billing doesn't duplicate enforcement logic.

Usage tracking uses Redis atomic counters (INCR/DECR) for O(1) reads instead of COUNT/SUM queries. A periodic reconciliation job ensures eventual consistency between Redis counters and the database.

## Decisions

| Decision | Choice | Rationale |
|----------|--------|-----------|
| Mock vs real provider | MockBillingProvider (instant activation) | No Stripe dependency in boilerplate. Real provider wired later via `IBillingProvider` interface. |
| Plan tiers | 4: Free / Starter / Pro / Enterprise | Granular market fit. Free = trial, Starter = small team, Pro = growing, Enterprise = large org. |
| Default plan on registration | Auto-assign Free | No friction at signup. Tenants upgrade from Settings → Billing. |
| Plan change timing | Immediate | Limits update instantly. Proration comes with real billing provider. |
| Usage tracking | Redis atomic counters | O(1) reads. Periodic DB reconciliation. Auto-rebuild on cache miss. |
| Plan management | Full SuperAdmin UI | CRUD for plans with feature flag mapping editor. |
| Overage handling | Soft block | Existing data kept. New creates blocked until under limit. Dashboard warning. |

## Architecture

### System Boundaries

**Billing owns:**
- SubscriptionPlan definitions (name, price, features JSON)
- TenantSubscription lifecycle (create, change, cancel)
- PaymentRecord history
- IBillingProvider abstraction + MockBillingProvider
- SubscriptionChangedEvent → SyncPlanFeaturesHandler (sync overrides)
- IUsageTracker service (Redis counters)
- Reconciliation background job
- Billing API endpoints + frontend pages

**Feature Flags owns (unchanged):**
- Flag definitions + per-tenant overrides + resolution + caching
- IFeatureFlagService API
- Enforcement in handlers (calls IUsageTracker instead of COUNT queries)

**Integration contract:**
- Billing writes TenantFeatureFlag overrides with `OverrideSource.PlanSubscription`
- Feature flag resolution is unaware of billing — it just reads overrides
- Usage counters are a separate service consumed by both billing (dashboard) and enforcement (handlers)

### New Shared Service: IUsageTracker

```csharp
public interface IUsageTracker
{
    Task<long> GetAsync(Guid tenantId, string metric, CancellationToken ct = default);
    Task IncrementAsync(Guid tenantId, string metric, long amount = 1, CancellationToken ct = default);
    Task DecrementAsync(Guid tenantId, string metric, long amount = 1, CancellationToken ct = default);
    Task SetAsync(Guid tenantId, string metric, long value, CancellationToken ct = default);
    Task<Dictionary<string, long>> GetAllAsync(Guid tenantId, CancellationToken ct = default);
}
```

**Redis key format:** `usage:{tenantId}:{metric}`
**Supported metrics:** `users`, `storage_bytes`, `api_keys`, `reports_active`
**Cache miss behavior:** Auto-rebuild from DB (COUNT/SUM), SET result, return. This is the ONLY time DB aggregation runs.
**TTL:** 24 hours on auto-rebuilt keys. No TTL on actively maintained counters.

## Entities

### SubscriptionPlan (AggregateRoot, platform-level, no TenantId)

| Field | Type | Notes |
|-------|------|-------|
| Id | Guid | PK |
| Name | string | "Free", "Starter", "Pro", "Enterprise" |
| Slug | string | Unique, URL-safe: "free", "starter", "pro", "enterprise" |
| Description | string? | Plan description for pricing page |
| MonthlyPrice | decimal | Display price (no real billing in mock) |
| AnnualPrice | decimal | Display price for annual billing |
| Currency | string | Default "USD" |
| Features | string (jsonb) | `{"users.max_count": "5", "files.max_storage_mb": "1024", ...}` |
| IsFree | bool | Distinguishes free tier (no payment required) |
| IsActive | bool | Soft-delete / deactivate |
| DisplayOrder | int | Sorting on pricing page |
| TrialDays | int | Default 0 for Free, configurable for paid plans |
| CreatedAt | DateTime | |
| ModifiedAt | DateTime? | |

**No global query filter** — plans are platform-level, visible to all.

### TenantSubscription (AggregateRoot, has TenantId)

| Field | Type | Notes |
|-------|------|-------|
| Id | Guid | PK |
| TenantId | Guid | FK → Tenant, unique (one active subscription per tenant) |
| SubscriptionPlanId | Guid | FK → SubscriptionPlan |
| Status | SubscriptionStatus | Trialing, Active, PastDue, Canceled, Expired |
| ExternalCustomerId | string? | Stripe customer ID (null for mock) |
| ExternalSubscriptionId | string? | Stripe subscription ID (null for mock) |
| BillingInterval | BillingInterval | Monthly, Annual |
| CurrentPeriodStart | DateTime | |
| CurrentPeriodEnd | DateTime | |
| TrialEndAt | DateTime? | |
| CanceledAt | DateTime? | |
| AutoRenew | bool | Default true |
| CreatedAt | DateTime | |
| ModifiedAt | DateTime? | |

**Global query filter:** `TenantId == null || s.TenantId == TenantId`
**Unique constraint:** One active subscription per tenant (TenantId unique where Status != Canceled/Expired)

### PaymentRecord (BaseEntity, has TenantId)

| Field | Type | Notes |
|-------|------|-------|
| Id | Guid | PK |
| TenantId | Guid | FK → Tenant |
| TenantSubscriptionId | Guid | FK → TenantSubscription |
| Amount | decimal | |
| Currency | string | |
| Status | PaymentStatus | Pending, Completed, Failed, Refunded |
| ExternalPaymentId | string? | Stripe payment intent ID |
| InvoiceUrl | string? | Link to invoice PDF |
| Description | string? | "Pro Plan - Monthly" |
| PeriodStart | DateTime | Billing period this covers |
| PeriodEnd | DateTime | |
| CreatedAt | DateTime | |

**Global query filter:** Standard tenant filter.

### TenantFeatureFlag (existing — migration adds column)

| New Field | Type | Notes |
|-----------|------|-------|
| OverrideSource | OverrideSource | `Manual = 0`, `PlanSubscription = 1`. Default: Manual |

### Enums

```csharp
public enum SubscriptionStatus { Trialing = 0, Active = 1, PastDue = 2, Canceled = 3, Expired = 4 }
public enum BillingInterval { Monthly = 0, Annual = 1 }
public enum PaymentStatus { Pending = 0, Completed = 1, Failed = 2, Refunded = 3 }
public enum OverrideSource { Manual = 0, PlanSubscription = 1 }
```

## User Flows

### Flow 1: New Tenant Registration

```
1. User submits register-tenant form
2. RegisterTenantCommandHandler creates Tenant + User (existing)
3. NEW: After SaveChanges, assign Free plan:
   a. Find SubscriptionPlan where IsFree == true
   b. Create TenantSubscription(tenantId, planId=Free, status=Active)
   c. Raise SubscriptionChangedEvent(tenantId, null, freePlanId)
4. SyncPlanFeaturesHandler processes event:
   a. Load Free plan Features JSON
   b. For each flag: create TenantFeatureFlag with source=PlanSubscription
5. Initialize usage counters: usage:{tenantId}:users = 1
6. User verifies email, logs in → sees Free plan limits
```

### Flow 2: Plan Upgrade/Downgrade

```
1. Tenant admin calls POST /billing/change-plan { planId }
2. ChangePlanCommandHandler:
   a. Validate: plan exists, active, different from current
   b. Call IBillingProvider.ChangeSubscriptionAsync()
   c. MockBillingProvider: returns success immediately
   d. Update TenantSubscription (planId, period dates)
   e. Create PaymentRecord (mock: computed proration amount)
   f. Raise SubscriptionChangedEvent(tenantId, oldPlanId, newPlanId)
3. SyncPlanFeaturesHandler:
   a. Load new plan Features JSON
   b. For each flag in new plan:
      - Override exists with source=PlanSubscription → UPDATE value
      - Override exists with source=Manual → SKIP (tenant opted out)
      - No override → INSERT with source=PlanSubscription
   c. Remove PlanSubscription overrides for flags NOT in new plan
   d. Invalidate feature flag cache: RemoveByPrefix("ff:{tenantId}")
4. Next request: new limits in effect
```

### Flow 3: Usage Counter Guard (replaces COUNT queries)

```
On entity create (user, file, API key):
  1. Read limit: IFeatureFlagService.GetValueAsync<int>("users.max_count")
  2. Read current: IUsageTracker.GetAsync(tenantId, "users")
     - Redis hit → return counter value (O(1))
     - Redis miss → COUNT from DB, SET in Redis, return
  3. If current >= limit → return QuotaExceeded error
  4. Proceed with create
  5. IUsageTracker.IncrementAsync(tenantId, "users")

On entity delete:
  1. Proceed with delete
  2. IUsageTracker.DecrementAsync(tenantId, "users")
```

### Flow 4: SuperAdmin Plan Management

```
1. SuperAdmin → Billing → Plans page
2. CRUD operations on SubscriptionPlan:
   - Create: name, slug, prices, trial days, feature mapping
   - Edit: update any field including features JSON
   - Deactivate: soft-disable (existing tenants keep plan, no new signups)
3. Feature mapping UI:
   - Dropdown of all feature flags (from GetAll)
   - For each selected flag: input for value (type-aware: toggle for boolean, number input for integer)
   - Saves as Features JSON on the plan
4. Editing a plan does NOT retroactively update existing tenants
   - To push changes: SuperAdmin clicks "Resync All Tenants" button
   - This raises SubscriptionChangedEvent for each tenant on that plan
```

### Flow 5: Reconciliation Background Job

```
Every 6 hours (configurable via system setting):
  1. For each active tenant:
     a. Query DB: COUNT(users), SUM(file.size), COUNT(api_keys), COUNT(active_reports)
     b. Compare with Redis counters
     c. If mismatch > threshold: SET Redis to DB value, log discrepancy
  2. Ensures eventual consistency
  3. Handles edge cases: Redis restart, counter drift from failed operations
```

## API Endpoints

### Public
| Method | Path | Description |
|--------|------|-------------|
| GET | `/api/v1/billing/plans` | List active plans (pricing page) |

### Tenant Admin (requires auth + billing permissions)
| Method | Path | Description |
|--------|------|-------------|
| GET | `/api/v1/billing/subscription` | Current tenant subscription + usage |
| POST | `/api/v1/billing/change-plan` | Change to different plan |
| POST | `/api/v1/billing/cancel` | Cancel subscription |
| GET | `/api/v1/billing/payments` | Payment history (paginated) |
| GET | `/api/v1/billing/usage` | Current usage counters |

### SuperAdmin (requires Billing.* permissions)
| Method | Path | Description |
|--------|------|-------------|
| GET | `/api/v1/billing/plans/manage` | List all plans (including inactive) |
| POST | `/api/v1/billing/plans` | Create plan |
| PUT | `/api/v1/billing/plans/{id}` | Update plan |
| DELETE | `/api/v1/billing/plans/{id}` | Deactivate plan |
| POST | `/api/v1/billing/plans/{id}/resync` | Resync all tenants on this plan |
| GET | `/api/v1/billing/tenants/{tenantId}/subscription` | View tenant's subscription |
| POST | `/api/v1/billing/tenants/{tenantId}/change-plan` | Change tenant's plan |

### Webhook (future — for real billing providers)
| Method | Path | Description |
|--------|------|-------------|
| POST | `/api/v1/billing/webhooks` | Webhook receiver (signature validation) |

## Permissions

```csharp
public static class Billing
{
    public const string View = "Billing.View";           // See own subscription + usage
    public const string Manage = "Billing.Manage";       // Change plan, cancel
    public const string ViewPlans = "Billing.ViewPlans"; // SuperAdmin: see all plans
    public const string ManagePlans = "Billing.ManagePlans"; // SuperAdmin: CRUD plans
    public const string ManageTenantSubscriptions = "Billing.ManageTenantSubscriptions"; // SuperAdmin: change tenant plans
}
```

**Role mapping:**
- Admin: `Billing.View`, `Billing.Manage`
- SuperAdmin: All billing permissions
- User: `Billing.View` (read-only)

## Provider Abstraction

```csharp
public interface IBillingProvider
{
    Task<CreateSubscriptionResult> CreateSubscriptionAsync(Guid tenantId, string planSlug, BillingInterval interval, CancellationToken ct = default);
    Task<ChangeSubscriptionResult> ChangeSubscriptionAsync(string externalSubscriptionId, string newPlanSlug, CancellationToken ct = default);
    Task CancelSubscriptionAsync(string externalSubscriptionId, bool atPeriodEnd = true, CancellationToken ct = default);
    Task<bool> ValidateWebhookAsync(string payload, string signature, CancellationToken ct = default);
}

public record CreateSubscriptionResult(string ExternalSubscriptionId, string ExternalCustomerId, DateTime PeriodStart, DateTime PeriodEnd);
public record ChangeSubscriptionResult(DateTime NewPeriodStart, DateTime NewPeriodEnd, decimal ProratedAmount);
```

**MockBillingProvider:** Returns instant success. Generates mock external IDs (`mock_sub_{guid}`, `mock_cust_{guid}`). Period = now → +30 days (monthly) or +365 days (annual). Proration = simple day-based calculation.

## Domain Events

| Event | Raised By | Handled By |
|-------|-----------|------------|
| `SubscriptionChangedEvent(TenantId, OldPlanId?, NewPlanId)` | ChangePlanCommandHandler, RegisterTenantCommandHandler | SyncPlanFeaturesHandler |
| `SubscriptionCanceledEvent(TenantId, SubscriptionId)` | CancelSubscriptionCommandHandler | Downgrades tenant to Free plan (raises SubscriptionChangedEvent → SyncPlanFeatures to Free limits) |

## Seed Data

### 4 Subscription Plans

| Plan | Monthly | Annual | Users | Storage | Upload | API Keys | Reports | PDF | Invitations |
|------|---------|--------|-------|---------|--------|----------|---------|-----|-------------|
| Free | $0 | $0 | 5 | 1 GB | 10 MB | 2 | ❌ | ❌ | ✅ |
| Starter | $29 | $290 | 25 | 10 GB | 25 MB | 5 | ✅ | ❌ | ✅ |
| Pro | $99 | $990 | 100 | 50 GB | 50 MB | 20 | ✅ | ✅ | ✅ |
| Enterprise | $299 | $2990 | 500 | 200 GB | 100 MB | 50 | ✅ | ✅ | ✅ |

### Features JSON Example (Pro Plan)
```json
{
  "users.max_count": "100",
  "users.invitations_enabled": "true",
  "files.max_storage_mb": "51200",
  "files.max_upload_size_mb": "50",
  "reports.enabled": "true",
  "reports.max_concurrent": "5",
  "reports.pdf_export": "true",
  "api_keys.enabled": "true",
  "api_keys.max_count": "20"
}
```

## Frontend Pages

### 1. Public Pricing Page (`/pricing`)
- Plan comparison cards (4 columns)
- Feature checklist per plan
- "Get Started" → register-tenant, "Upgrade" → login required
- Accessible from landing page + footer

### 2. Settings → Billing Tab (Tenant Admin)
- Current plan badge + status
- Usage bars: users (X/Y), storage (X/Y GB), API keys (X/Y)
- "Change Plan" button → plan selector modal
- Payment history table
- "Cancel Subscription" with confirmation

### 3. Billing → Plans Page (SuperAdmin)
- Plan cards/table with CRUD
- Feature mapping editor (flag key dropdown + value input)
- Tenant count per plan
- "Resync" button per plan

### 4. Tenant Detail → Subscription Tab (SuperAdmin)
- Current subscription details
- Usage stats for this tenant
- "Change Plan" on behalf of tenant
- Payment history for this tenant

## Migration Plan

1. Add `OverrideSource` column to `tenant_feature_flags` table (default: `Manual`)
2. Create `subscription_plans` table
3. Create `tenant_subscriptions` table (unique on TenantId where status is active)
4. Create `payment_records` table
5. Seed 4 default plans

## Handler Refactoring

These existing handlers will be updated to use `IUsageTracker` instead of COUNT/SUM:

| Handler | Current | After |
|---------|---------|-------|
| RegisterUserCommandHandler | `context.Users.CountAsync()` | `usageTracker.GetAsync(tenantId, "users")` |
| UploadFileCommandHandler | `context.FileMetadata.SumAsync(f => f.Size)` | `usageTracker.GetAsync(tenantId, "storage_bytes")` |
| CreateApiKeyCommandHandler | `context.ApiKeys.CountAsync(k => !k.IsRevoked)` | `usageTracker.GetAsync(tenantId, "api_keys")` |

Each handler also calls `IncrementAsync` after successful creation. Delete handlers call `DecrementAsync`.

## Testing Checklist

- [ ] Register new tenant → auto-assigned Free plan with correct limits
- [ ] Free plan: can create up to 5 users, blocked at 6th
- [ ] Upgrade Free → Pro: limits increase immediately
- [ ] Downgrade Pro → Starter: limits decrease, existing data preserved, new creates blocked if over limit
- [ ] Usage counter accuracy: create 10 users, counter shows 10
- [ ] Delete user: counter decrements
- [ ] Redis restart: counter auto-rebuilds from DB on next access
- [ ] SuperAdmin: create/edit/deactivate plans
- [ ] SuperAdmin: change tenant's plan
- [ ] Feature mapping: changes in plan Features JSON reflect in tenant limits after resync
- [ ] Manual override preserved: tenant opts out of a feature, plan change doesn't re-enable it
- [ ] Public pricing page shows active plans
- [ ] Settings → Billing shows correct usage bars
- [ ] Payment history shows mock records
- [ ] Cancel subscription: status changes, limits revert to Free or block
