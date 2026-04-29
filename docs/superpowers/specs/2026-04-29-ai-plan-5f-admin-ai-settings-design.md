# AI Module - Plan 5f: Admin AI Settings Backend Design

Status: **Spec / pre-plan**
Date: **2026-04-29**
Branch: `feature/ai-phase-5f`
Size: **M**

Follows:
- Plan 5d-1 Agent Identity + Cost Enforcement
- Plan 5d-2 Safety + Content Moderation
- Plan 5e Bundled Platform Agents
- Modularity Tier 1 module-host cleanup

Precedes:
- Plan 5g Gemini provider adapter
- Plan 7b Advanced Admin Pages / AI Settings UI
- Plan 8f End-customer API Foundation

---

## 1. Purpose

Plan 5f adds the backend source of truth for tenant AI administration. It gives the platform and each tenant a clear, auditable way to decide:

- who pays for provider calls
- which providers and models are allowed
- which tenant defaults are used when an assistant does not specify its own model or safety preset
- how much AI usage a tenant may consume
- how the tenant's AI should present itself
- which public widgets and publishable widget keys exist for later public API enforcement

The design deliberately keeps UI out of scope. Plan 7b surfaces these APIs. Plan 8f uses the widget credential contract for public API authentication and quota enforcement.

---

## 2. Core Decision

Use **AI-owned settings and widget primitives** inside `Starter.Module.AI`.

Do not store this as generic `SystemSetting` rows, and do not reuse core `ApiKey` for browser widgets. AI settings have domain behavior: provider-key fallback policy, entitlement clamping, model-default resolution, safety fallback, brand prompt injection, and public-widget quota semantics. Keeping them in the AI module gives the runtime one coherent settings contract without making core tenancy or API keys understand AI-specific rules.

### 2.1 Phase boundary

5f is load-bearing for authenticated/internal AI runtime:

- provider credential resolution
- tenant model defaults
- tenant default safety fallback
- tenant self-limit and platform-credit accounting
- tenant brand prompt injection

5f is contract-ready for public widget runtime:

- create widgets
- create/rotate/revoke widget credentials
- store origin pins
- store public quota buckets

5f does **not** authenticate anonymous/public requests. Plan 8f owns public route prefixes, anonymous persona resolution, origin enforcement, widget credential authentication, and public rate limiting.

---

## 3. Locked Decisions From Brainstorming

1. **Default provider policy is `PlatformOnly`.** A tenant starts on platform provider keys and platform-controlled AI credit unless explicitly changed.
2. **Provider policy supports three modes now:**
   - `PlatformOnly`
   - `TenantKeysAllowed`
   - `TenantKeysRequired`
3. **Subscription and feature flags are the entitlement source.** AI settings only express tenant choices and self-limits beneath those ceilings.
4. **Cost accounting has two lenses:**
   - total AI usage, which applies to all runs including BYOK
   - platform-credit usage, which applies only when platform credentials pay for the run
5. **BYOK still counts.** Tenant-owned provider keys avoid platform-credit spend but still count toward tenant governance, per-agent caps, abuse controls, and usage analytics.
6. **Brand profile is tenant-wide by default.** Persona stays about audience, safety, and visibility. Brand profile is how the tenant's AI presents itself.
7. **Public widget keys are separate from core API keys.** They are publishable browser credentials for routing, origin pinning, quota attribution, and revocation. They never grant backend permissions.
8. **Plan 8f must consume 5f's widget contract.** The revised AI vision is updated with this forward link.

---

## 4. Entitlement Model

Plan 5d-1 already put AI limits into subscription-plan features and feature flags:

- `ai.cost.tenant_monthly_usd`
- `ai.cost.tenant_daily_usd`
- `ai.agents.max_count`
- `ai.agents.operational_enabled`
- `ai.agents.requests_per_minute_default`

5f extends that model instead of bypassing it.

### 4.1 New feature flags / plan features

Add these platform-controlled entitlements:

| Key | Type | Meaning |
|---|---|---|
| `ai.cost.platform_monthly_usd` | decimal | Monthly platform-credit spend ceiling for platform credentials. |
| `ai.cost.platform_daily_usd` | decimal | Daily platform-credit spend ceiling for platform credentials. |
| `ai.provider_keys.byok_enabled` | bool | Whether tenant-owned provider credentials may be configured. |
| `ai.widgets.enabled` | bool | Whether public AI widgets may be configured. |
| `ai.widgets.max_count` | int | Maximum public widgets for the tenant. |
| `ai.widgets.monthly_tokens` | int | Tenant-wide public-widget monthly token ceiling. |
| `ai.widgets.daily_tokens` | int | Tenant-wide public-widget daily token ceiling. |
| `ai.widgets.requests_per_minute` | int | Default public-widget request rate ceiling. |
| `ai.providers.allowed` | JSON string array | Optional provider allowlist. Empty means all registered providers. |
| `ai.models.allowed` | JSON string array | Optional model allowlist. Empty means all configured/priced models. |

Subscription sync remains the owner of plan-provided values. Manual tenant feature-flag overrides remain the mechanism for custom deals.

### 4.2 Effective cap hierarchy

Runtime computes effective limits from highest authority to lowest:

```text
effective_total_cap =
  min(subscription_total_cap, tenant_total_self_cap, agent_cap)

effective_platform_credit_cap =
  min(subscription_platform_credit_cap, tenant_platform_credit_self_cap, agent_cap)

effective_widget_cap =
  min(subscription_widget_cap, tenant_widget_default_cap, widget_cap)
```

The subscription/feature-flag tier is never exceeded by tenant settings. Validators reject tenant self-limits that are higher than resolved entitlements.

---

## 5. Data Model

All entities live under `Starter.Module.AI`.

### 5.1 `AiTenantSettings`

One row per tenant.

| Field | Notes |
|---|---|
| `TenantId` | Required, unique. |
| `RequestedProviderCredentialPolicy` | `PlatformOnly`, `TenantKeysAllowed`, `TenantKeysRequired`. Defaults to `PlatformOnly`. |
| `DefaultSafetyPreset` | Fallback only after assistant override and persona safety. |
| `MonthlyCostCapUsd` | Tenant self-limit for total AI spend. Nullable means inherit entitlement. |
| `DailyCostCapUsd` | Tenant self-limit for total AI spend. Nullable means inherit entitlement. |
| `PlatformMonthlyCostCapUsd` | Tenant self-limit for platform-credit spend. Nullable means inherit entitlement. |
| `PlatformDailyCostCapUsd` | Tenant self-limit for platform-credit spend. Nullable means inherit entitlement. |
| `RequestsPerMinute` | Tenant self-limit for internal AI requests. Nullable means inherit entitlement. |
| `PublicMonthlyTokenCap` | Tenant default public-widget monthly token self-limit. |
| `PublicDailyTokenCap` | Tenant default public-widget daily token self-limit. |
| `PublicRequestsPerMinute` | Tenant default public-widget RPM self-limit. |
| `AssistantDisplayName` | Tenant-wide AI display name. |
| `Tone` | Short tone/voice descriptor. |
| `AvatarFileId` | Optional file id, validated through existing file ownership/read rules. |
| `BrandInstructions` | Long-form tenant brand guidelines for prompt injection. |
| Audit fields | Existing module conventions. |

Missing row behavior is defined: default settings resolve as `PlatformOnly`, `DefaultSafetyPreset = Standard`, and null self-limits.

### 5.2 `AiProviderCredential`

Tenant-owned provider credential. Platform master credentials remain in appsettings/user-secrets for 5f.

| Field | Notes |
|---|---|
| `TenantId` | Required. |
| `Provider` | `OpenAI`, `Anthropic`, `Ollama`; Gemini arrives in 5g through the same enum seam. |
| `DisplayName` | Admin-friendly name. |
| `EncryptedSecret` | Encrypted via the existing credential-encryption pattern. |
| `KeyPrefix` | Safe prefix/fingerprint for masked display. |
| `Status` | `Active`, `Revoked`. |
| `LastValidatedAt` | Set by test-connection endpoint. |
| `LastUsedAt` | Updated when runtime resolves this credential. |
| Audit fields | Existing module conventions. |

5f uses one active credential per tenant/provider. Rotate revokes the old active credential and creates a new active row.

### 5.3 `AiModelDefault`

Default model configuration by tenant and agent class.

| Field | Notes |
|---|---|
| `TenantId` | Required. |
| `AgentClass` | `Chat`, `ToolAgent`, `Insight`, `RagHelper`, `Embedding`. |
| `Provider` | Provider selected for that class. |
| `Model` | Model id sent to provider. |
| `MaxTokens` | Optional class default. |
| `Temperature` | Optional class default. |

Unique index: `(TenantId, AgentClass)`.

### 5.4 `AiPublicWidget`

Tenant-owned public surface definition. This is not a public API implementation.

| Field | Notes |
|---|---|
| `TenantId` | Required. |
| `Name` | Admin-facing widget name. |
| `Status` | `Active`, `Paused`, `Archived`. |
| `AllowedOrigins` | Normalized JSON list. Exact origins only, no wildcard in 5f. |
| `DefaultAssistantId` | Optional. Must belong to the same tenant. |
| `DefaultPersonaSlug` | Defaults to `anonymous`; may be `client` or another end-customer persona. |
| `MonthlyTokenCap` | Per-widget self-limit, clamped by widget entitlement. |
| `DailyTokenCap` | Per-widget self-limit, clamped by widget entitlement. |
| `RequestsPerMinute` | Per-widget RPM self-limit, clamped by widget entitlement. |
| `MetadataJson` | Reserved for Plan 7b/8f display config. |

### 5.5 `AiWidgetCredential`

Publishable widget credential. It is visible to browsers and must never authorize normal backend APIs.

| Field | Notes |
|---|---|
| `TenantId` | Duplicated for efficient scoping and audit. |
| `WidgetId` | Required FK to `AiPublicWidget`. |
| `KeyPrefix` | Stored for lookup and masked display. |
| `KeyHash` | Full key hash only. Full key returned once on create/rotate. |
| `Status` | `Active`, `Revoked`. |
| `ExpiresAt` | Optional. |
| `LastUsedAt` | Reserved for 8f public enforcement. |
| Audit fields | Existing module conventions. |

Multiple active credentials are allowed per widget so admins can rotate without downtime. Normal cleanup can revoke old credentials.

### 5.6 `AiUsageLog` additions

Add:

| Field | Notes |
|---|---|
| `ProviderCredentialSource` | `Platform`, `Tenant`. This answers who paid the provider call. |
| `ProviderCredentialId` | Nullable. Set when a tenant-owned provider key pays for the call. |

Widget-specific usage attribution can add `AiPublicWidgetId` and/or `UsageSurface` in 8f when public routes exist. 5f only needs provider credential source to support platform-credit accounting.

---

## 6. Runtime Policy Flow

### 6.1 Provider credential resolution

Introduce an `IAiProviderCredentialResolver` used by provider creation/runtime code.

Resolution:

1. Resolve tenant settings; missing row defaults to `PlatformOnly`.
2. Resolve feature flags:
   - `ai.provider_keys.byok_enabled`
   - `ai.providers.allowed`
   - `ai.models.allowed`
3. If BYOK is disabled, force effective policy to `PlatformOnly`.
4. Validate provider against `ai.providers.allowed`.
5. Apply provider policy:
   - `PlatformOnly`: use platform appsettings/user-secret key.
   - `TenantKeysAllowed`: use active tenant credential if present, otherwise platform fallback.
   - `TenantKeysRequired`: require active tenant credential; no platform fallback.
6. Return a resolved credential result with:
   - provider
   - secret
   - `ProviderCredentialSource`
   - `ProviderCredentialId?`

Failures happen before any provider call:

- BYOK disabled by plan
- provider not allowed by plan
- tenant key required but missing
- provider key not configured
- encrypted credential cannot be decrypted

### 6.2 Model resolution

Introduce an `IAiModelDefaultResolver` used anywhere an assistant/provider helper currently falls back to appsettings.

Resolution:

1. Assistant explicit provider/model wins.
2. `AiModelDefault` for tenant and agent class wins next.
3. Platform appsettings/provider default is final fallback.
4. Validate resolved provider/model against allowed providers/models feature flags.

Agent classes:

- `Chat`
- `ToolAgent`
- `Insight`
- `RagHelper`
- `Embedding`

Initial mapping:

| Call site | Agent class |
|---|---|
| normal chat assistant | `Chat` |
| `AssistantExecutionMode.Agent` / tool loop | `ToolAgent` |
| future embedded insights | `Insight` |
| query rewriting, contextual rewrite, reranking, classifiers | `RagHelper` |
| embedding service | `Embedding` |

### 6.3 Cost enforcement

Extend `CostCapResolver` from the current two-tier shape to include:

- subscription/feature-flag total cap
- tenant total self-limit
- assistant cap
- subscription/feature-flag platform-credit cap
- tenant platform-credit self-limit
- assistant cap

`CostCapEnforcingAgentRuntime` uses the resolved provider credential source:

- all runs check total cap
- platform-key runs also check platform-credit cap
- tenant-key runs do not consume platform-credit cap
- all runs remain logged and visible per tenant, per agent, provider, model, and credential source

The existing Redis accountant can add a platform-credit namespace:

```text
ai:cost:{tenantId}:{assistantId}:...
ai:platform-cost:{tenantId}:{assistantId}:...
```

This preserves concurrent enforcement without changing the usage-log source of truth.

### 6.4 Safety resolution

Extend safety fallback order:

1. `AiAssistant.SafetyPresetOverride`
2. resolved `AiPersona.SafetyPreset`
3. `AiTenantSettings.DefaultSafetyPreset`
4. platform seeded profile for `Standard`

Tenant default safety must not weaken an assistant or persona override. It only fills gaps.

### 6.5 Brand prompt injection

Add a small tenant brand clause to the effective system prompt when tenant settings include brand profile fields.

Rules:

- Use tenant brand profile as default identity and style.
- Do not override explicit assistant specialization. The assistant prompt remains authoritative.
- Keep injection concise to avoid prompt bloat.
- Include `AssistantDisplayName`, `Tone`, and `BrandInstructions`; `AvatarFileId` is API metadata for UI, not prompt text.

Example shape:

```text
Tenant AI brand profile:
- Name: Acme Assistant
- Tone: clear, concise, warm
- Brand guidance: ...
```

---

## 7. Admin API Surface

Add `AiSettingsController` under:

```text
/api/v{version}/ai/settings
```

Authorization:

- reads: `Ai.ManageSettings` for settings/config, `Ai.ViewUsage` where returning usage context
- writes: `Ai.ManageSettings`
- platform admin may pass `tenantId` where cross-tenant management is needed
- tenant users operate on their own tenant

### 7.1 Tenant settings

| Method | Route | Purpose |
|---|---|---|
| `GET` | `/ai/settings` | Return requested settings plus resolved entitlements/effective policy. |
| `PUT` | `/ai/settings` | Upsert tenant settings/self-limits/brand/default safety. |

The read response should include both:

- plan entitlement values
- tenant requested values
- effective resolved values

Plan 7b needs this to render "your plan allows X; you configured Y".

### 7.2 Provider credentials

| Method | Route | Purpose |
|---|---|---|
| `GET` | `/ai/settings/provider-credentials` | List masked credentials. |
| `POST` | `/ai/settings/provider-credentials` | Create active credential for provider from a submitted secret; return masked metadata only. |
| `POST` | `/ai/settings/provider-credentials/{id}/rotate` | Replace submitted secret; return masked metadata only. |
| `POST` | `/ai/settings/provider-credentials/{id}/revoke` | Revoke credential. |
| `POST` | `/ai/settings/provider-credentials/{id}/test` | Validate credential without saving usage. |

Creation and rotation fail when `ai.provider_keys.byok_enabled=false`.

### 7.3 Model defaults

| Method | Route | Purpose |
|---|---|---|
| `GET` | `/ai/settings/model-defaults` | List tenant defaults and platform fallbacks. |
| `PUT` | `/ai/settings/model-defaults/{agentClass}` | Upsert default for one agent class. |

Validators reject providers/models outside `ai.providers.allowed` and `ai.models.allowed`.

### 7.4 Public widgets

| Method | Route | Purpose |
|---|---|---|
| `GET` | `/ai/settings/widgets` | List tenant public widgets. |
| `POST` | `/ai/settings/widgets` | Create widget. |
| `PUT` | `/ai/settings/widgets/{id}` | Update widget config/origins/quotas/status. |
| `POST` | `/ai/settings/widgets/{id}/credentials` | Create widget credential and return full key once. |
| `POST` | `/ai/settings/widgets/{id}/credentials/{credentialId}/rotate` | Add/replace credential. |
| `POST` | `/ai/settings/widgets/{id}/credentials/{credentialId}/revoke` | Revoke credential. |

Widget creation fails when `ai.widgets.enabled=false`. Widget count and quotas are clamped by `ai.widgets.*` feature flags.

### 7.5 Error shape

Use explicit domain errors:

- `AiSettings.ByokDisabledByPlan`
- `AiSettings.ProviderNotAllowed`
- `AiSettings.ModelNotAllowed`
- `AiSettings.TenantKeyRequired`
- `AiSettings.SelfLimitExceedsEntitlement`
- `AiSettings.WidgetDisabledByPlan`
- `AiSettings.WidgetQuotaExceedsEntitlement`
- `AiSettings.WidgetLimitExceeded`
- `AiSettings.InvalidOrigin`
- `AiSettings.ProviderCredentialNotFound`
- `AiSettings.WidgetNotFound`

---

## 8. Security And Privacy

### 8.1 Provider secrets

- Encrypt at rest with the existing credential-encryption pattern used elsewhere in the boilerplate.
- Never return full secrets after create/rotate.
- Mask reads with provider, display name, prefix/fingerprint, status, validation state, last used.
- Do not log decrypted secrets.
- Test-connection endpoint should call a cheap provider operation or SDK validation path and record only result metadata.

### 8.2 Widget credentials

Widget credentials are publishable browser keys. They are not secrets in the traditional backend sense.

Therefore:

- Store only prefix + hash.
- Return full key once on create/rotate.
- Never attach permission scopes.
- Never authenticate normal backend controllers.
- Require origin pinning on widgets.
- Plan 8f must enforce request origin, widget status, credential status, persona/assistant binding, rate limit, and public quota before invoking AI.

### 8.3 Tenant isolation

All new entities are tenant-scoped except platform/default static metadata. Use AI module global tenant filters consistent with existing `AiAssistant`, `AiPersona`, and `AiUsageLog` behavior. Platform admins may query with explicit tenant ids only through handlers that perform authorization checks.

---

## 9. Integration Points

### 9.1 Existing cost-cap runtime

`CostCapResolver` and `CostCapEnforcingAgentRuntime` remain the enforcement seam. 5f extends inputs and accounting, rather than adding a parallel limiter.

### 9.2 Existing feature flags and billing

`IFeatureFlagService` remains the resolved entitlement interface. Plan-feature sync remains in Billing. AI settings validators ask the feature-flag service for current ceilings before accepting tenant choices.

### 9.3 Existing safety pipeline

`SafetyProfileResolver` remains the safety seam. 5f adds tenant default safety as a fallback tier.

### 9.4 Existing provider adapters

OpenAI, Anthropic, and Ollama providers should resolve credentials through the new resolver. 5g Gemini plugs into the same resolver and does not need a separate credential-management design.

### 9.5 Existing usage logs

`AiUsageLog` stays the primary analytics table. 5f adds credential source attribution so platform-credit billing can be separated from BYOK usage.

---

## 10. Testing Strategy

### 10.1 Unit and handler tests

- Missing `AiTenantSettings` resolves to `PlatformOnly`.
- BYOK disabled by feature flag forces effective `PlatformOnly`.
- `TenantKeysAllowed` uses active tenant key when present.
- `TenantKeysAllowed` falls back to platform key when tenant key is absent.
- `TenantKeysRequired` fails before provider call when key is absent.
- Tenant self-limit greater than entitlement is rejected.
- Widget quota greater than entitlement is rejected.
- Widget count greater than entitlement is rejected.
- Disallowed provider/model is rejected.
- Widget origins normalize and reject invalid origins.
- Provider credentials are encrypted/masked on reads.
- Widget credentials are hashed/masked on reads.
- Rotate/revoke updates active credential state correctly.

### 10.2 Runtime tests

- Assistant explicit model beats tenant model default.
- Tenant model default beats platform provider default.
- Total cap applies to platform-key and tenant-key runs.
- Platform-credit cap applies only to `ProviderCredentialSource.Platform`.
- Usage log records tenant, assistant, provider, model, cost, `ProviderCredentialSource`, and tenant provider credential id where applicable.
- Safety fallback order is assistant override, persona, tenant default, platform standard.
- Brand profile appears in effective prompt without replacing assistant prompt.

### 10.3 Acid tests

Add `Plan5fAcidTests` covering:

1. Default tenant policy is `PlatformOnly`.
2. A plan with BYOK disabled cannot create tenant provider credentials.
3. `TenantKeysRequired` fails fast with no tenant key.
4. Platform-credit counter is not consumed by a BYOK run.
5. Widget keys cannot authenticate normal API-key-protected controllers.
6. Widget configuration exists but public request enforcement is deferred to 8f.

---

## 11. Documentation Updates

This design is the source of truth for Plan 5f.

Also update `docs/superpowers/specs/2026-04-23-ai-module-vision-revised-design.md`:

- Plan 5f row should mention provider policy, BYOK, platform-credit accounting, and AI-owned widget credentials.
- Plan 8f row should explicitly say it consumes 5f `AiPublicWidget` and `AiWidgetCredential` records for public API authentication, origin pinning, and public quota enforcement.

This forward link is required so Plan 8f does not invent a second widget-key system.

---

## 12. Out Of Scope

- No frontend UI.
- No public anonymous API endpoints.
- No embeddable script.
- No public chat portal.
- No migration of platform master provider keys from appsettings/user-secrets into DB.
- No marketplace billing automation.
- No Gemini adapter implementation; 5g consumes the credential resolver later.
- No multi-modal provider settings; Plan 10 owns image/audio/video surfaces.

---

## 13. Implementation Notes For The Plan

Likely code areas:

- `Starter.Module.AI/Domain/Entities`
- `Starter.Module.AI/Domain/Enums`
- `Starter.Module.AI/Domain/Errors`
- `Starter.Module.AI/Infrastructure/Configurations`
- `Starter.Module.AI/Infrastructure/Persistence/AiDbContext`
- `Starter.Module.AI/Application/Commands/...`
- `Starter.Module.AI/Application/Queries/...`
- `Starter.Module.AI/Application/Services/...`
- `Starter.Module.AI/Infrastructure/Services/...`
- `Starter.Module.AI/Infrastructure/Providers/...`
- `Starter.Module.AI/Controllers/AiSettingsController.cs`
- `Starter.Module.Billing/BillingModule.cs` plan-feature seed
- AI tests under `tests/Starter.Api.Tests/Ai`

Keep the implementation modular: settings resolution, provider credential resolution, model-default resolution, cap resolution, and widget credential management should be separate services/handlers.

---

## 14. Success Criteria

Plan 5f is done when:

1. Tenant AI settings can be read/upserted with entitlement-aware validation.
2. BYOK provider credentials can be created, tested, rotated, revoked, masked, and used by internal runtime.
3. Provider policy correctly supports `PlatformOnly`, `TenantKeysAllowed`, and `TenantKeysRequired`.
4. Tenant model defaults are used when assistants have no explicit provider/model.
5. Tenant default safety preset participates in the fallback chain.
6. Tenant brand profile is injected into internal agent prompts.
7. Total usage and platform-credit usage are enforced and attributable.
8. Public widgets and widget credentials can be managed but do not authenticate public calls until 8f.
9. Revised AI vision forward-links 8f to the 5f widget contract.
10. The acid tests prove the phase boundary and billing/accounting behavior.
