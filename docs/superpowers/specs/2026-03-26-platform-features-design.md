# Platform Features Design Spec

**Date:** 2026-03-26
**Updated:** 2026-03-28
**Scope:** 7 features for the Starter boilerplate (SaaS launch, boilerplate product, internal foundation)

**Stack:**
- Backend: .NET 10, Clean Architecture, CQRS/MediatR, EF Core/PostgreSQL, MassTransit/RabbitMQ, Redis
- Frontend: React 19, Vite, TypeScript, Tailwind CSS 4, Shadcn/Radix UI, React Query, Zustand, i18next (en/ar/ku), React Hook Form + Zod

## Status & Execution Order

| Order | Feature | Status | Dependencies |
|-------|---------|--------|-------------|
| 1 | API Key Management | ✅ Complete | None |
| 2 | OpenTelemetry | ✅ Complete | None |
| 3 | Tenant Feature Flags | 🔄 In Progress | None |
| 4 | Billing / Subscriptions | ⬜ Planned | Feature Flags (plan→flag sync) |
| 5 | Webhook System | ⬜ Planned | None |
| 6 | Dashboard Analytics | ⬜ Planned | None |
| 7 | Data Import/Export | ⬜ Planned | None |

**Dropped:** Push Notifications — primarily a mobile concern, low value for web-only boilerplate.

### Key Decisions (2026-03-28)

- **Execution order** changed to SaaS fundamentals first: Feature Flags → Billing → Webhooks → Analytics → Import/Export
- **Billing** will use abstract `IBillingProvider` with mock provider (no Stripe dependency). Wire Stripe later.
- **Feature Flags** serves as infrastructure for Billing — flags define both boolean toggles AND quota/limit values (Integer type). Billing auto-sets tenant overrides from plan entitlements via `SubscriptionChangedEvent`.
- **Feature Flags enforcement** — 3 reference handler implementations (CreateUser, UploadFile, CreateApiKey) with developer docs for adding more. Optimistic race condition strategy; upgrade to Redis atomic counters documented for Billing.
- **Tenants can self-manage** flags within plan limits (opt-out of features, not opt-in beyond plan ceiling). Plan ceiling enforcement added when Billing arrives.
- **API key access** to feature flags works automatically — `IFeatureFlagService` resolves via `ICurrentUserService.TenantId` which `ApiKeyAuthenticationHandler` sets.

### System Ownership Boundaries

```
Feature Flags owns:
  • Flag definitions (feature_flags table) + per-tenant overrides (tenant_feature_flags table)
  • Resolution: tenant override → platform default, Redis cached (5-min TTL)
  • IFeatureFlagService API + enforcement guard pattern in handlers
  • 11 seed flags for all system features

Billing/Subscriptions will own:
  • SubscriptionPlan.Features JSON → flag key/value mapping
  • SubscriptionChangedEvent → SetTenantOverrideCommand (auto-sync plan entitlements)
  • OverrideSource enum (Manual vs PlanSubscription) on TenantFeatureFlag via migration
  • Usage tracking counters (Redis atomic INCR/DECR for real-time dashboard)
  • Plan ceiling enforcement (tenants cannot exceed plan limits)
```

---

## Feature 1: API Key Management

**Purpose:** Service-to-service authentication via API keys. Tenants create keys with scoped permissions.

### ERD

```
ApiKey : AggregateRoot<Guid>
├── Id (Guid, PK)
├── TenantId (Guid, FK → Tenant)
├── Name (string) — "Production key", "Staging key"
├── KeyPrefix (string, 8 chars, UNIQUE) — visible identifier: "sk_live_Ab"
├── KeyHash (string) — BCrypt hash of full key
├── Scopes (string[]) — ["reports:read", "webhooks:manage"]
├── ExpiresAt (DateTime?)
├── LastUsedAt (DateTime?)
├── IsRevoked (bool)
├── CreatedBy (Guid, FK → User)
├── CreatedAt, ModifiedAt
```

### API Endpoints

| Method | Path | Description |
|--------|------|-------------|
| POST | `/api/v1/api-keys` | Create key (returns full key once) |
| GET | `/api/v1/api-keys` | List keys (prefix only) |
| DELETE | `/api/v1/api-keys/{id}` | Revoke key |
| PATCH | `/api/v1/api-keys/{id}` | Update name/scopes |

### Authentication Flow

1. Client sends `X-Api-Key: sk_live_AbCdEf...` header
2. `ApiKeyAuthenticationHandler` extracts prefix → finds `ApiKey` by prefix → verifies BCrypt hash
3. Checks: not revoked, not expired, tenant active
4. Creates `ClaimsPrincipal` with tenant_id + scopes as permission claims
5. Runs as composite auth scheme alongside JWT

### Key Files

- New: `Domain/ApiKeys/Entities/ApiKey.cs`
- New: `Application/Features/ApiKeys/` (Commands + Queries)
- New: `Infrastructure.Identity/Authentication/ApiKeyAuthenticationHandler.cs`
- New: `Infrastructure/Persistence/Configurations/ApiKeyConfiguration.cs`
- New: `Api/Controllers/ApiKeysController.cs`
- Modify: `Api/Configurations/AuthenticationConfiguration.cs` (add composite scheme)

### Permissions

`ApiKeys.View`, `ApiKeys.Create`, `ApiKeys.Delete`, `ApiKeys.Update`

### Frontend

**Page:** Settings → API Keys tab (new tab in existing settings page)

**Components:**
- `ApiKeysList` — Table with columns: Name, Prefix, Scopes (badges), Created, Last Used, Expires, Status. Actions: Revoke, Edit.
- `CreateApiKeyDialog` — Form: name (text), scopes (multi-select checkboxes), expiration (date picker, optional). On submit → show full key in a copy-to-clipboard modal (shown once).
- `ApiKeySecretDisplay` — One-time secret display with copy button and warning banner.
- `EditApiKeyDialog` — Update name/scopes only.

**API hooks (React Query):**
- `useApiKeys()` — list query
- `useCreateApiKey()` — mutation, returns full key
- `useRevokeApiKey()` — mutation
- `useUpdateApiKey()` — mutation

**i18n keys:** `apiKeys.title`, `apiKeys.create`, `apiKeys.copyWarning`, `apiKeys.scopes.*`, etc.

**Frontend files:**
- `features/api-keys/pages/ApiKeysPage.tsx`
- `features/api-keys/components/ApiKeysList.tsx`, `CreateApiKeyDialog.tsx`, `ApiKeySecretDisplay.tsx`
- `features/api-keys/api/api-keys.api.ts`, `api-keys.queries.ts`
- `lib/validation/schemas/api-key.schema.ts`

---

## Feature 2: OpenTelemetry (Full Observability)

**Purpose:** Distributed traces, custom metrics, and log correlation via OTLP. Replaces CorrelationIdMiddleware.

### Architecture

- **Traces:** Auto-instrument ASP.NET Core, EF Core, HttpClient, MassTransit. Custom spans for business ops.
- **Metrics:** Request rate, error rate, latency histograms, active users gauge, queue depth, cache hit/miss.
- **Logs:** Bridge Serilog → OTLP via `Serilog.Sinks.OpenTelemetry`. Keep Serilog as API.
- **Export:** OTLP to configurable endpoint.

### Configuration

```json
"OpenTelemetry": {
  "Enabled": true,
  "ServiceName": "starter-api",
  "OtlpEndpoint": "http://localhost:4317",
  "TracingEnabled": true,
  "MetricsEnabled": true,
  "LogsEnabled": true
}
```

### Docker

Add Jaeger all-in-one container: port 16686 (UI), 4317 (OTLP gRPC).

### Key Files

- New: `Api/Configurations/OpenTelemetryConfiguration.cs`
- New: `Infrastructure/Settings/OpenTelemetrySettings.cs`
- Modify: `Program.cs` (register OTel)
- Modify: Serilog config (add OTLP sink)
- Remove: `CorrelationIdMiddleware.cs` (replaced by W3C trace context)
- Modify: `docker-compose.yml` (add Jaeger)

### NuGet Packages

`OpenTelemetry.Extensions.Hosting`, `OpenTelemetry.Instrumentation.AspNetCore`, `OpenTelemetry.Instrumentation.EntityFrameworkCore`, `OpenTelemetry.Instrumentation.Http`, `MassTransit.OpenTelemetry`, `OpenTelemetry.Exporter.OpenTelemetryProtocol`, `Serilog.Sinks.OpenTelemetry`

### Frontend

**No frontend UI required.** OpenTelemetry is backend infrastructure only. Traces/metrics/logs are viewed in Jaeger UI (dev) or external observability platform (prod). No configuration pages needed — settings are in `appsettings.json`.

---

## Feature 3: Tenant-Level Feature Flags

**Purpose:** Key-value flags per tenant for feature rollout, plan gating, A/B testing.

### ERD

```
FeatureFlag : AggregateRoot<Guid> (platform definition — NO TenantId, excluded from global query filters)
├── Id (Guid, PK)
├── Key (string, unique) — "billing.enabled", "reports.pdf_export"
├── Name (string)
├── Description (string?)
├── DefaultValue (string) — "false", "100", "basic"
├── ValueType (enum: Boolean, String, Integer, Json)
├── Category (string?)
├── IsSystem (bool)
├── CreatedAt, ModifiedAt

TenantFeatureFlag : BaseEntity<Guid> (per-tenant override)
├── Id (Guid, PK)
├── TenantId (Guid, FK → Tenant)
├── FeatureFlagId (Guid, FK → FeatureFlag)
├── Value (string)
├── CreatedAt, ModifiedAt
├── UNIQUE(TenantId, FeatureFlagId)
```

**Tenant scoping note:** `FeatureFlag` is a platform-level entity with no `TenantId`. It must be excluded from global query filters in `ApplicationDbContext` (no `.HasQueryFilter()` applied). `TenantFeatureFlag` has `TenantId` and uses the standard tenant filter.

**Tenant self-management:** Tenant admins can toggle flags within limits (opt-out of features they're entitled to). Cannot enable beyond plan ceiling (ceiling enforcement added when Billing arrives). System flags (`IsSystem = true`) are platform-admin only.

### Resolution

Tenant override → Platform default. Cached per-tenant in Redis (`ff:{tenantId}`, 5 min TTL). Cache busted on update.

### Enforcement

Handlers inject `IFeatureFlagService` and check before allowing actions:
- **Boolean gate:** `IsEnabledAsync("feature.key")` → block if disabled
- **Quota check:** `GetValueAsync<int>("resource.max_count")` → compare against `COUNT(*)` query

**Reference implementations:** CreateUserCommandHandler, UploadFileCommandHandler, CreateApiKeyCommandHandler. See `docs/feature-flags.md` for patterns.

### Seed Flags

11 flags covering: `users.max_count`, `users.invitations_enabled`, `files.max_upload_size_mb`, `files.max_storage_mb`, `reports.enabled`, `reports.max_concurrent`, `reports.pdf_export`, `api_keys.enabled`, `api_keys.max_count`, `ui.maintenance_mode`, `billing.enabled`.

### API Endpoints

| Method | Path | Description |
|--------|------|-------------|
| GET | `/api/v1/feature-flags` | List flags with resolved values |
| GET | `/api/v1/feature-flags/{key}` | Get single flag value |
| POST | `/api/v1/feature-flags` | Create flag (platform admin) |
| PUT | `/api/v1/feature-flags/{id}` | Update flag definition |
| DELETE | `/api/v1/feature-flags/{id}` | Delete non-system flag |
| PUT | `/api/v1/feature-flags/{id}/tenants/{tenantId}` | Set tenant override |
| DELETE | `/api/v1/feature-flags/{id}/tenants/{tenantId}` | Remove override |

### Service Interface

```csharp
public interface IFeatureFlagService
{
    Task<bool> IsEnabledAsync(string key);
    Task<T> GetValueAsync<T>(string key);
    Task<Dictionary<string, string>> GetAllAsync();
}
```

### Key Files

- New: `Domain/FeatureFlags/Entities/FeatureFlag.cs`, `TenantFeatureFlag.cs`
- New: `Domain/FeatureFlags/Enums/FlagValueType.cs`
- New: `Application/Features/FeatureFlags/` (Commands + Queries)
- New: `Application/Common/Interfaces/IFeatureFlagService.cs`
- New: `Infrastructure/Services/FeatureFlagService.cs`
- New: `Api/Controllers/FeatureFlagsController.cs`

### Permissions

`FeatureFlags.View`, `FeatureFlags.Create`, `FeatureFlags.Update`, `FeatureFlags.Delete`, `FeatureFlags.ManageTenantOverrides`

### Frontend

**Page:** Settings → Feature Flags tab (new tab)

**Components:**
- `FeatureFlagsList` — Table grouped by category. Columns: Key, Name, Type, Default Value, Tenant Override (if exists), Actions.
- `CreateFeatureFlagDialog` — Form: key (text, dot-notation), name, description, type (select: Boolean/String/Integer/Json), default value, category, isSystem checkbox. Platform admin only.
- `EditFeatureFlagDialog` — Update name, description, default value, category.
- `TenantOverrideDialog` — Set override value for a specific tenant. Shows current default and lets admin enter tenant-specific value.
- `FeatureFlagToggle` — Inline toggle for boolean flags (quick enable/disable).

**Platform admin view:** All flags + tenant overrides management.
**Tenant admin view:** Read-only list of flags with their resolved values. Can override non-system flags.

**API hooks:**
- `useFeatureFlags()` — list with resolved values
- `useCreateFeatureFlag()`, `useUpdateFeatureFlag()`, `useDeleteFeatureFlag()` — mutations
- `useSetTenantOverride()`, `useRemoveTenantOverride()` — mutations

**Client-side feature check hook:**
- `useFeatureFlag(key)` — returns resolved value, cached via React Query

**Frontend files:**
- `features/feature-flags/pages/FeatureFlagsPage.tsx`
- `features/feature-flags/components/` (List, Create, Edit, Override, Toggle)
- `features/feature-flags/api/feature-flags.api.ts`, `feature-flags.queries.ts`
- `hooks/useFeatureFlag.ts` — reusable hook for checking flags anywhere in the app

---

## Feature 4: Webhook System

**Purpose:** Tenants register URLs to receive HTTP callbacks on domain events with HMAC signing and retry.

### ERD

```
WebhookEndpoint : AggregateRoot<Guid>
├── Id (Guid, PK)
├── TenantId (Guid, FK → Tenant)
├── Url (string)
├── SecretHash (string) — BCrypt hash of signing secret (show-once like API keys)
├── Description (string?)
├── Events (string[]) — ["user.created", "report.completed"]
├── IsActive (bool)
├── CreatedBy (Guid, FK → User)
├── CreatedAt, ModifiedAt

WebhookDelivery : BaseEntity<Guid>
├── Id (Guid, PK)
├── TenantId (Guid, FK → Tenant) — denormalized for tenant-scoped queries
├── WebhookEndpointId (Guid, FK → WebhookEndpoint)
├── EventType (string)
├── Payload (jsonb)
├── ResponseStatusCode (int?)
├── ResponseBody (string?, max 1KB)
├── Attempt (int)
├── NextRetryAt (DateTime?)
├── Status (enum: Pending, Delivered, Failed, Exhausted)
├── CreatedAt, DeliveredAt
```

**Secret handling:** Signing secret is generated on creation, hashed with BCrypt, full secret shown once to user. For HMAC signing at delivery time, the consumer needs the plaintext secret — store it AES-256 encrypted at rest (encryption key in config), decrypt only during delivery. `SecretHash` is for display/identification only.

**Retry mechanism:** Uses MassTransit scheduled re-publish with `IMessageScheduler`. After max 3 attempts, status → `Exhausted`. If an endpoint has 5+ consecutive exhausted deliveries, auto-disable the endpoint and notify the tenant.

**Initial webhook event types:** `user.created`, `user.updated`, `user.deleted`, `file.uploaded`, `file.deleted`, `report.completed`, `report.failed`, `tenant.updated`, `invitation.accepted`. Defined as constants in `WebhookEventTypes` static class.

### Event Flow

1. Domain event fires → `WebhookEventHandler` maps to webhook event type
2. Publishes `DeliverWebhookMessage` to MassTransit
3. `DeliverWebhookConsumer` finds matching endpoints → HTTP POST with HMAC signature
4. Retry: 3 attempts, exponential backoff (1min, 5min, 30min)

### HMAC Signing

`HMAC-SHA256(secret, timestamp + "." + jsonPayload)` → `X-Webhook-Signature-256: t={ts},v1={sig}`

### API Endpoints

| Method | Path | Description |
|--------|------|-------------|
| POST | `/api/v1/webhooks` | Register endpoint |
| GET | `/api/v1/webhooks` | List endpoints |
| GET | `/api/v1/webhooks/{id}` | Get endpoint details |
| PUT | `/api/v1/webhooks/{id}` | Update endpoint |
| DELETE | `/api/v1/webhooks/{id}` | Delete endpoint |
| GET | `/api/v1/webhooks/{id}/deliveries` | Delivery log (paginated) |
| POST | `/api/v1/webhooks/{id}/test` | Send test event |
| GET | `/api/v1/webhooks/events` | List available event types |

### Key Files

- New: `Domain/Webhooks/Entities/WebhookEndpoint.cs`, `WebhookDelivery.cs`
- New: `Domain/Webhooks/Enums/WebhookDeliveryStatus.cs`
- New: `Application/Features/Webhooks/` (Commands + Queries)
- New: `Infrastructure/Consumers/DeliverWebhookConsumer.cs`
- New: `Infrastructure/Services/WebhookService.cs`
- New: `Api/Controllers/WebhooksController.cs`

### Permissions

`Webhooks.View`, `Webhooks.Create`, `Webhooks.Update`, `Webhooks.Delete`

### Frontend

**Page:** Settings → Webhooks tab (new tab)

**Components:**
- `WebhookEndpointsList` — Table: URL (truncated), Events (badges), Status (active/inactive toggle), Created, Actions.
- `CreateWebhookDialog` — Form: URL (text, validated), description, events (multi-select checkboxes from available event types). On submit → show signing secret once (same copy-to-clipboard pattern as API keys).
- `EditWebhookDialog` — Update URL, description, events, active status.
- `WebhookDeliveriesLog` — Expandable panel per endpoint showing delivery history: event type, status badge (Delivered/Failed/Exhausted), attempt count, timestamp, response code. Click to expand shows payload + response body.
- `TestWebhookButton` — Sends test event to endpoint, shows result.
- `WebhookSecretDisplay` — One-time secret display (reuse `ApiKeySecretDisplay` pattern).

**API hooks:**
- `useWebhookEndpoints()`, `useWebhookDeliveries(endpointId)`
- `useCreateWebhook()`, `useUpdateWebhook()`, `useDeleteWebhook()`
- `useTestWebhook()`, `useWebhookEventTypes()`

**Frontend files:**
- `features/webhooks/pages/WebhooksPage.tsx`
- `features/webhooks/components/` (List, Create, Edit, DeliveriesLog, Test)
- `features/webhooks/api/webhooks.api.ts`, `webhooks.queries.ts`

---

## Feature 5: Billing / Subscriptions

**Purpose:** Abstract billing layer (`IBillingProvider`) with Stripe default. Manage plans, subscriptions, payments.

### ERD

```
SubscriptionPlan
├── Id (Guid, PK)
├── Name (string)
├── Slug (string, unique)
├── Description (string?)
├── MonthlyPriceAmount (decimal)
├── YearlyPriceAmount (decimal?)
├── Currency (string, default "usd")
├── Features (jsonb) — {"maxUsers": 10, "maxStorage": "5GB"}
├── IsActive (bool)
├── SortOrder (int)
├── ExternalPlanId (string?)
├── CreatedAt, ModifiedAt

TenantSubscription
├── Id (Guid, PK)
├── TenantId (Guid, FK → Tenant, unique)
├── PlanId (Guid, FK → SubscriptionPlan)
├── Status (enum: Active, PastDue, Canceled, Trialing, Paused)
├── BillingInterval (enum: Monthly, Yearly)
├── CurrentPeriodStart (DateTime)
├── CurrentPeriodEnd (DateTime)
├── CancelAtPeriodEnd (bool)
├── TrialEndsAt (DateTime?)
├── ExternalSubscriptionId (string?)
├── ExternalCustomerId (string?)
├── CreatedAt, ModifiedAt

PaymentHistory
├── Id (Guid, PK)
├── TenantId (Guid, FK → Tenant)
├── Amount (decimal)
├── Currency (string)
├── Status (enum: Succeeded, Failed, Refunded, Pending)
├── Description (string)
├── ExternalPaymentId (string?)
├── CreatedAt
```

### Abstraction

**Implementation approach:** Abstract `IBillingProvider` interface with `MockBillingProvider` as default. Wire Stripe later by swapping the implementation. This keeps the boilerplate usable without a Stripe account.

```csharp
public interface IBillingProvider
{
    Task<string> CreateCustomerAsync(Tenant tenant);
    Task<BillingSession> CreateCheckoutSessionAsync(string customerId, string planId, string interval);
    Task<BillingSubscription> GetSubscriptionAsync(string subscriptionId);
    Task CancelSubscriptionAsync(string subscriptionId, bool atPeriodEnd);
    Task<BillingPortalSession> CreatePortalSessionAsync(string customerId);
    Task<bool> ValidateWebhookAsync(string payload, string signature);
}
```

**MockBillingProvider:** Simulates checkout by directly setting subscription status. No external HTTP calls. Plans are managed via seed data. Useful for development, demos, and testing the full subscription flow without Stripe.

### API Endpoints

| Method | Path | Description |
|--------|------|-------------|
| GET | `/api/v1/billing/plans` | List plans (public) |
| GET | `/api/v1/billing/subscription` | Current tenant subscription |
| POST | `/api/v1/billing/checkout` | Create checkout session |
| POST | `/api/v1/billing/portal` | Customer portal session |
| POST | `/api/v1/billing/cancel` | Cancel subscription |
| POST | `/api/v1/billing/webhooks` | Provider webhook receiver |

**Feature flag integration:** When subscription changes (via billing webhook), publish `SubscriptionChangedEvent` domain event. `SyncPlanFeaturesHandler` listens and upserts tenant feature flag overrides from `SubscriptionPlan.Features` JSON. This is a domain event handler, not a direct service call — keeps billing and feature flags decoupled.

**Auth bypass:** `POST /api/v1/billing/webhooks` is `[AllowAnonymous]` — authenticates via `IBillingProvider.ValidateWebhookAsync()` (Stripe signature verification) instead of JWT/API key.

**Entity base classes:**
- `SubscriptionPlan` : `AggregateRoot<Guid>` (no TenantId — platform-level, excluded from tenant query filter)
- `TenantSubscription` : `AggregateRoot<Guid>` (has TenantId)
- `PaymentHistory` : `BaseEntity<Guid>` (has TenantId, no domain events needed)

### Key Files

- New: `Domain/Billing/Entities/SubscriptionPlan.cs`, `TenantSubscription.cs`, `PaymentHistory.cs`
- New: `Domain/Billing/Enums/SubscriptionStatus.cs`, `BillingInterval.cs`, `PaymentStatus.cs`
- New: `Domain/Billing/Events/SubscriptionChangedEvent.cs`
- New: `Application/Features/Billing/` (Commands + Queries)
- New: `Application/Features/Billing/EventHandlers/SyncPlanFeaturesHandler.cs`
- New: `Application/Common/Interfaces/IBillingProvider.cs`
- New: `Infrastructure/Services/Billing/MockBillingProvider.cs` (default, no external deps)
- New: `Infrastructure/Services/Billing/StripeBillingProvider.cs` (optional, wire later)
- New: `Infrastructure/Consumers/ProcessBillingWebhookConsumer.cs`
- New: `Api/Controllers/BillingController.cs`
- Modify: `Domain/FeatureFlags/Entities/TenantFeatureFlag.cs` — Add `OverrideSource` enum (Manual, PlanSubscription)
- New migration: Add `override_source` column to `tenant_feature_flags`

### Feature Flag Integration (critical dependency)

Billing MUST implement these to connect with the Feature Flags system built in Feature 3:

1. **`SubscriptionPlan.Features`** — JSON column mapping flag keys to plan-entitled values:
   ```json
   {"users.max_count": "50", "reports.pdf_export": "true", "files.max_storage_mb": "20480"}
   ```

2. **`SyncPlanFeaturesHandler`** — Listens to `SubscriptionChangedEvent`, iterates `plan.Features`, calls `SetTenantOverrideCommand` for each key/value with `OverrideSource = PlanSubscription`

3. **Override source logic** — Plan syncs only overwrite `PlanSubscription`-sourced overrides. Manual tenant overrides are preserved. If a tenant manually set a lower value (opt-out), the plan sync does NOT raise it back up.

4. **Usage counters** (optional upgrade) — Redis atomic counters `usage:{tenantId}:{resource}` for real-time dashboard. Periodically reconciled with DB counts. Replaces COUNT queries in handlers for high-throughput scenarios.

### Permissions

`Billing.ViewPlans`, `Billing.ViewSubscription`, `Billing.ManageSubscription`, `Billing.ViewPayments`

### Frontend

**Pages:**
- **Pricing Page** (`/pricing`) — Public page showing available plans with feature comparison grid. Highlight current plan if logged in.
- **Billing Settings** (Settings → Billing tab) — Current plan, usage, next billing date, payment history.

**Components:**
- `PricingCards` — Plan cards with monthly/yearly toggle. Each card: name, price, feature list, CTA button. Highlight recommended plan.
- `CurrentPlanCard` — Shows current plan name, status badge (Active/Trialing/PastDue), billing interval, period dates, cancel-at-period-end warning.
- `PaymentHistoryTable` — Table: date, amount, status badge, description, invoice link.
- `ChangePlanDialog` — Shows upgrade/downgrade options with proration info. Redirects to Stripe Checkout.
- `CancelSubscriptionDialog` — Confirmation dialog with "cancel at period end" explanation.
- `ManagePaymentButton` — Opens Stripe Customer Portal in new tab.
- `TrialBanner` — Top banner showing trial days remaining (conditional on subscription status).

**User flows:**
1. New tenant → Pricing page → Select plan → Stripe Checkout (external) → Redirect back → Subscription active
2. Existing tenant → Settings/Billing → Change plan → Stripe Checkout → Updated
3. Cancel → Settings/Billing → Cancel → Confirmation → Canceled at period end

**API hooks:**
- `useBillingPlans()`, `useSubscription()`, `usePaymentHistory()`
- `useCreateCheckout()`, `useCreatePortalSession()`, `useCancelSubscription()`

**Frontend files:**
- `features/billing/pages/PricingPage.tsx`, `BillingSettingsPage.tsx`
- `features/billing/components/` (PricingCards, CurrentPlan, PaymentHistory, ChangePlan, Cancel, TrialBanner)
- `features/billing/api/billing.api.ts`, `billing.queries.ts`

---

## Feature 6: Dashboard Analytics

**Purpose:** Real-time aggregation over existing data for frontend charts. No new entities.

### API Endpoints

| Method | Path | Description |
|--------|------|-------------|
| GET | `/api/v1/analytics/overview` | KPI cards (users, files, reports, invitations) |
| GET | `/api/v1/analytics/users` | Registration trends, by status, login frequency |
| GET | `/api/v1/analytics/audit` | Activity over time, by action type, top users |
| GET | `/api/v1/analytics/files` | Upload trends, by category, storage usage |
| GET | `/api/v1/analytics/reports` | By type, status, avg generation time |

**Shared query params:** `?from=&to=&interval=day|week|month`

**Implementation:** CQRS query handlers with EF Core GROUP BY projections. Add indexes on `CreatedAt` where missing. Tenant-scoped via existing global query filters.

### Key Files

- New: `Application/Features/Analytics/Queries/` (5 query handlers)
- New: `Api/Controllers/AnalyticsController.cs`

### Permissions

`Analytics.View`

### Frontend

**Page:** Dashboard (enhance existing dashboard page with charts)

**Chart library:** Install `recharts` (most popular React charting lib, works well with Tailwind/Shadcn).

**Components:**
- `OverviewKPIs` — Row of stat cards (total users, active users 30d, total files, storage, reports, invitations). Reuse existing stat card pattern.
- `UserRegistrationChart` — Line/area chart: registrations over time (day/week/month interval selector).
- `UserStatusDistribution` — Donut/pie chart: active vs suspended vs deactivated users.
- `AuditActivityChart` — Bar chart: audit actions over time, grouped by action type.
- `FileUploadChart` — Line chart: uploads over time with category breakdown (stacked area).
- `StorageUsageChart` — Gauge or progress bar showing storage consumed.
- `ReportStatusChart` — Stacked bar: reports by type and status.
- `DateRangeFilter` — Shared date range picker (from/to) + interval selector (day/week/month). Used across all charts.

**Layout:** Dashboard page with responsive grid (2 columns desktop, 1 column mobile). KPI cards on top, charts below.

**API hooks:**
- `useAnalyticsOverview(from, to)`
- `useAnalyticsUsers(from, to, interval)`
- `useAnalyticsAudit(from, to, interval)`
- `useAnalyticsFiles(from, to, interval)`
- `useAnalyticsReports(from, to, interval)`

**Frontend files:**
- Modify: `features/dashboard/pages/DashboardPage.tsx` (add charts)
- New: `features/dashboard/components/charts/` (OverviewKPIs, UserRegistration, UserStatus, AuditActivity, FileUpload, StorageUsage, ReportStatus, DateRangeFilter)
- New: `features/dashboard/api/analytics.api.ts`, `analytics.queries.ts`
- Install: `recharts` package

---

## ~~Feature 7: Push Notifications~~ (DROPPED)

> **Decision (2026-03-28):** Dropped from scope. Primarily a mobile concern with low value for a web-only boilerplate. Can be added later if mobile app support is needed.

---

## Feature 7: Data Import/Export

**Purpose:** Bulk CSV + JSON import/export for all major entities. Builds on the existing async report generation pattern — no new job entity or duplicate infrastructure.

### Architecture — Reuse Existing Pattern

The project already has a complete async job infrastructure via Reports:
- `ReportRequest` entity with status tracking (Pending → Processing → Completed → Failed)
- `GenerateReportConsumer` MassTransit consumer
- `IMessagePublisher` for fire-and-forget publishing
- `INotificationService` + `IRealtimeService` (Ably) for completion push
- Polling via list endpoint + signed S3 download URLs
- `ExportService` for CSV/PDF generation

**Approach: Extend, don't duplicate.**

1. **Extend `ReportType` enum** to add import/export types: `UsersExport`, `RolesExport`, `UsersImport`, `RolesImport`, etc.
2. **Extend `ReportFormat` enum** to add `Json` format
3. **Extend `ReportRequest` entity** with optional import-specific fields:
   - `SourceFileId` (Guid?) — uploaded file for imports (distinct from output `FileId`)
   - `TotalRecords`, `ProcessedRecords`, `FailedRecords` (int?) — progress tracking
   - `ErrorLog` (jsonb?) — per-row import errors
   - Add `PartialSuccess` to `ReportStatus` enum
4. **Extend `ExportService`** to add `GenerateJson<T>()` method
5. **New consumers:** `ImportDataConsumer`, `ExportDataConsumer` — follow the exact same pattern as `GenerateReportConsumer` (status transitions, `IgnoreQueryFilters()`, notification on completion)
6. **Reuse existing endpoints pattern:** Import/export jobs appear in the Reports list or have their own controller that delegates to the same infrastructure

### Entity Mapper Interface (new)

```csharp
public interface IEntityImportExportMapper<T>
{
    string EntityType { get; }
    IEnumerable<string> Columns { get; }
    T MapFromRow(Dictionary<string, string> row);
    Dictionary<string, string> MapToRow(T entity);
    ValidationResult ValidateRow(Dictionary<string, string> row);
}
```

### API Endpoints

| Method | Path | Description |
|--------|------|-------------|
| POST | `/api/v1/import-export/export` | Start export (creates ReportRequest, publishes message) |
| POST | `/api/v1/import-export/import` | Start import (multipart upload, creates ReportRequest) |
| GET | `/api/v1/import-export/jobs` | List import/export jobs (queries ReportRequest by type) |
| GET | `/api/v1/import-export/jobs/{id}` | Job status + error log |
| GET | `/api/v1/import-export/jobs/{id}/download` | Download result (reuses signed URL pattern) |
| GET | `/api/v1/import-export/templates/{entityType}` | Download empty CSV/JSON template |

### Limits

Max import file size: 50MB. Max rows per import: 100,000. Max concurrent jobs per tenant: 3. Export streams data in chunks.

### Key Files

- **Modify:** `Domain/Common/ReportRequest.cs` (add SourceFileId, TotalRecords, ProcessedRecords, FailedRecords, ErrorLog)
- **Modify:** `Domain/Common/Enums/ReportType.cs` (add import/export types)
- **Modify:** `Domain/Common/Enums/ReportFormat.cs` (add Json)
- **Modify:** `Domain/Common/Enums/ReportStatus.cs` (add PartialSuccess)
- **Modify:** `Infrastructure/Services/ExportService.cs` (add GenerateJson)
- **Modify:** `Infrastructure/DependencyInjection.cs` (register new consumers)
- New: `Application/Features/ImportExport/` (Commands + Queries)
- New: `Application/Common/Interfaces/IEntityImportExportMapper.cs`
- New: `Infrastructure/Services/ImportExport/` (mapper per entity: Users, Roles, Tenants, Settings, AuditLogs, Files)
- New: `Infrastructure/Consumers/ImportDataConsumer.cs`, `ExportDataConsumer.cs`
- New: `Api/Controllers/ImportExportController.cs`
- **Reuse as-is:** `IMessagePublisher`, `IFileService`, `INotificationService`, `IRealtimeService`, `IStorageService`

### Permissions

`ImportExport.Import`, `ImportExport.Export`

### Frontend

**Page:** Settings → Import/Export tab (new tab)

**Components:**
- `ImportExportPage` — Two sections: Export (top), Import (bottom).
- `ExportSection` — Entity type selector (Users, Roles, Tenants, Settings, AuditLogs, Files), format selector (CSV/JSON), optional date range filter, "Start Export" button. Shows progress via job status.
- `ImportSection` — Entity type selector, format selector, file upload (drag-drop, reuse `FileUpload` component), "Start Import" button.
- `ImportExportJobsList` — Table of recent jobs: type (Import/Export badge), entity type, format, status badge (Pending/Processing/Completed/Failed/PartialSuccess), progress (processed/total), created date. Completed exports have download link. Failed/partial imports show error log.
- `ImportErrorLog` — Expandable panel showing per-row errors: row number, field, error message.
- `DownloadTemplateButton` — Per entity type, downloads empty CSV/JSON template for import reference.

**Real-time updates:** Job status auto-refreshes via React Query invalidation when Ably notification arrives (reuse existing report notification pattern).

**API hooks:**
- `useStartExport()`, `useStartImport()` — mutations
- `useImportExportJobs()` — list query
- `useImportExportJob(id)` — detail query
- `useDownloadTemplate(entityType)` — download

**Frontend files:**
- `features/import-export/pages/ImportExportPage.tsx`
- `features/import-export/components/` (ExportSection, ImportSection, JobsList, ErrorLog, DownloadTemplate)
- `features/import-export/api/import-export.api.ts`, `import-export.queries.ts`
