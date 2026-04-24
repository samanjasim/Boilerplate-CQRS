# Feature Flags Developer Guide

## Overview

Feature flags allow platform-level feature definitions with per-tenant value overrides. Two entity types:
- `FeatureFlag` — platform-level definition (key, name, default value, type, category, isSystem). No TenantId, no global query filter.
- `TenantFeatureFlag` — per-tenant override (tenantId, featureFlagId, value). Has TenantId with standard global query filter.

Resolution chain: **Tenant override → Platform default**

Cached per-tenant in Redis (`ff:{tenantId}`, 5-min TTL). Cache invalidated on any write via `RemoveByPrefixAsync("ff")`.

## Adding a New Feature Flag

### Step 1: Add seed data

In `Starter.Infrastructure/Persistence/Seeds/DataSeeder.cs`, add to the `flags` array in `SeedFeatureFlagsAsync`:

```csharp
FeatureFlag.Create("module.feature_name", "Display Name", "Description", "default_value", FlagValueType.Boolean, "category", false),
```

Parameters: `key` (dot notation, lowercase), `name`, `description`, `defaultValue` (string), `valueType` (Boolean/String/Integer/Json), `category`, `isSystem`.

### Step 2: Add enforcement in command handler

Inject `IFeatureFlagService flags` into the handler's primary constructor. Add check at the start of `Handle`.

### Step 3: (Optional) Add frontend display

If the flag should be visible in the admin UI, no code changes needed — it auto-appears in the Feature Flags page.

## Enforcement Patterns

### Boolean Gate — Check if a feature is enabled

```csharp
if (!await flags.IsEnabledAsync("api_keys.enabled", cancellationToken))
    return Result.Failure<T>(FeatureFlagErrors.FeatureDisabled("API Keys"));
```

### Quota Check — Enforce a numeric limit

```csharp
var maxUsers = await flags.GetValueAsync<int>("users.max_count", cancellationToken);
var currentCount = await context.Users.CountAsync(cancellationToken);
if (currentCount >= maxUsers)
    return Result.Failure<T>(FeatureFlagErrors.QuotaExceeded("users", maxUsers));
```

### Combined Gate + Quota

```csharp
if (!await flags.IsEnabledAsync("api_keys.enabled", cancellationToken))
    return Result.Failure<T>(FeatureFlagErrors.FeatureDisabled("API Keys"));

var maxKeys = await flags.GetValueAsync<int>("api_keys.max_count", cancellationToken);
var currentCount = await context.ApiKeys.CountAsync(k => !k.IsRevoked, cancellationToken);
if (currentCount >= maxKeys)
    return Result.Failure<T>(FeatureFlagErrors.QuotaExceeded("API keys", maxKeys));
```

## Existing Enforced Flags

| Flag Key | Type | Default | Enforced In | Status |
|----------|------|---------|-------------|--------|
| `users.max_count` | Integer | 100 | RegisterUserCommandHandler | ✅ Enforced |
| `files.max_upload_size_mb` | Integer | 50 | UploadFileCommandHandler | ✅ Enforced |
| `files.max_storage_mb` | Integer | 5120 | UploadFileCommandHandler | ✅ Enforced |
| `api_keys.enabled` | Boolean | true | CreateApiKeyCommandHandler | ✅ Enforced |
| `api_keys.max_count` | Integer | 10 | CreateApiKeyCommandHandler | ✅ Enforced |
| `users.invitations_enabled` | Boolean | true | CreateInvitationCommandHandler | ⬜ TODO |
| `reports.enabled` | Boolean | true | RequestReportCommandHandler | ⬜ TODO |
| `reports.max_concurrent` | Integer | 3 | RequestReportCommandHandler | ⬜ TODO |
| `reports.pdf_export` | Boolean | true | RequestReportCommandHandler | ⬜ TODO |
| `ui.maintenance_mode` | Boolean | false | Middleware | ⬜ TODO |
| `billing.enabled` | Boolean | false | Billing feature (future) | ⬜ Placeholder |

## Cache Behavior

- **Key format:** `ff:{tenantId}` for tenant users, `ff:platform` for platform admins
- **TTL:** 5 minutes
- **Invalidation:** `InvalidateCacheAsync()` calls `RemoveByPrefixAsync("ff")` which clears ALL tenant caches. Called automatically by Update, Delete, SetOverride, and RemoveOverride command handlers.
- **First request:** Cache miss → queries both tables → builds resolved map → caches → returns
- **Subsequent requests (within 5 min):** Returns from Redis cache directly

## Race Conditions & Performance

**Strategy:** Optimistic — accept small overages on concurrent requests.

Example: If max users = 100 and two requests arrive simultaneously when count = 99, both may succeed, resulting in 101 users. The next request will correctly enforce the limit.

**Why this is acceptable:**
- Most SaaS apps tolerate small temporary overages
- The alternative (distributed locks) adds latency to every write
- Billing reconciliation catches overages at billing cycle boundaries

**Upgrade path (for Billing feature):**
- Add Redis atomic counters: `usage:{tenantId}:{resource}` with `INCR`/`DECR`
- Check counter instead of `COUNT(*)` query
- Periodically reconcile counters with DB counts
- This eliminates both the race condition and the count query overhead

## API Key Access

API key authenticated requests automatically resolve feature flags correctly. The `ApiKeyAuthenticationHandler` sets `TenantId` on `ICurrentUserService`, and `IFeatureFlagService` uses `ICurrentUserService.TenantId` for resolution. No special configuration needed.

## Billing Integration Contract

When implementing the Billing/Subscriptions feature, these changes connect it to Feature Flags:

### 1. Add OverrideSource to TenantFeatureFlag

Add migration with new `OverrideSource` enum:
```csharp
public enum OverrideSource { Manual = 0, PlanSubscription = 1 }
```
Add `OverrideSource` property to `TenantFeatureFlag` entity.

### 2. SubscriptionPlan.Features JSON

Each plan defines feature entitlements:
```json
{"users.max_count": "50", "reports.pdf_export": "true", "files.max_storage_mb": "20480"}
```

### 3. SyncPlanFeaturesHandler

Listen to `SubscriptionChangedEvent`. For each key in `plan.Features`:
- Call `SetTenantOverrideCommand` with `OverrideSource = PlanSubscription`
- Only overwrite existing overrides with `OverrideSource = PlanSubscription`
- Preserve `Manual` overrides (tenant opted out of a feature)

### 4. Tenant self-management rules

- Tenants can disable features (set `false` for boolean, lower value for integer)
- Tenants cannot exceed plan entitlements (enforced by SetTenantOverride handler after Billing adds ceiling logic)
- Manual overrides are preserved across plan changes
