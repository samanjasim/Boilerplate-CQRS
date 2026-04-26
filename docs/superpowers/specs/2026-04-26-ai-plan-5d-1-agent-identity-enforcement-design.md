# AI Module — Plan 5d-1: Agent Identity + Enforcement (Design)

Status: **Spec / pre-plan**
Sequence: follows 5c-2 (Agent Templates), precedes 5d-2 (Safety + Moderation).
Branch: `feature/ai-phase-5d-1`.

---

## 1. Purpose

Plan 5d in the [revised AI vision](./2026-04-23-ai-module-vision-revised-design.md) bundles four sub-systems: agent service-account identity, role assignment, cost & rate enforcement, the `[DangerousAction]` human-approval pause, and the input/output content-moderation pipeline. The combined surface is too large for one plan, so 5d is split:

| Sub-plan | Scope |
|---|---|
| **5d-1 (this doc)** | Agent principal, role assignment, cost caps, rate limits, billing/feature-flag integration |
| 5d-2 (next) | `[DangerousAction]` human-approval pause, input/output moderation pipeline (`Standard` / `ChildSafe` / `ProfessionalModerated`) |

5d-1 is the *foundation*: it establishes agents as first-class principals with their own identity, permissions, budget, and rate. 5d-2 then layers safety controls on top of that principal.

---

## 2. Locked Decisions

These were settled during brainstorming and override any earlier proposal in this spec series.

1. **Agent principal lives in the AI module's DbContext as `AiAgentPrincipal`** — *not* a row in the central `User` table. (Earlier draft proposed `User.IsAgent=true`; rejected after scale + Auth-coupling review.)
2. **Hybrid-intersection security**: agent principal has its own roles; tool dispatch under an interactive chat applies the *intersection* of agent permissions and chat-caller permissions; an operational agent (no caller) acts under agent permissions only.
3. **Dual-attribution audit**: every audit row written from inside an agent run records both the agent (`AgentPrincipalId`) and the human caller (`OnBehalfOfUserId`, nullable for operational runs).
4. **Two-tier cost cap**: plan ceiling (set by superadmin via `SubscriptionPlan.Features` JSON) + per-agent cap (set by tenant on `AiAssistant`). No separate tenant-override tier in 5d-1; tenant override is purely additive and ships with 5f's settings UI.
5. **Cost cap unit is USD**, computed from token counts × pricing in a new `AiModelPricing` lookup table.
6. **Cost cap windows**: daily and monthly, both, calendar UTC.
7. **Behavior on cap hit**: hard block (`AgentRunStatus.CostCapExceeded`), friendly message to the human caller, full truth in the run trace.
8. **Atomic check-and-increment** for cap accounting (Redis Lua / DB conditional update). Read-then-write would race under concurrency.
9. **Pricing source**: hardcoded `AiModelPricing` seed, superadmin-editable via API.
10. **Rate limit is per-agent requests-per-minute**, sliding window in Redis; nullable on the agent (null = inherit from a tenant-default feature flag, default unlimited).

---

## 3. Architecture Overview

```
                 ┌────────────────────────────────────┐
chat caller ───►│  AiChatController / event trigger  │
                 └─────────────────┬──────────────────┘
                                   │ AgentRunContext
                                   ▼
                 ┌────────────────────────────────────┐
                 │  CostCapEnforcingAgentRuntime       │  decorator over IAiAgentRuntime
                 │   pre-step: rate-limit check        │
                 │   pre-step: cost-cap check + claim  │
                 │   post-step: write AiUsageLog       │
                 └─────────────────┬──────────────────┘
                                   │ unchanged interface
                                   ▼
                 ┌────────────────────────────────────┐
                 │ Provider runtime (5a)              │
                 │  → tool call                       │
                 └─────────────────┬──────────────────┘
                                   ▼
                 ┌────────────────────────────────────┐
                 │  AgentToolDispatcher               │
                 │   permission via IExecutionContext │ ← intersection
                 │   audit dual-attribution           │
                 │   ISender.Send(command, ct)        │
                 └────────────────────────────────────┘
```

Three new infrastructure pieces, all behind interfaces so 5d-2 / 5f / 7b can extend without rework:

| Service | Interface | Purpose |
|---|---|---|
| Execution context | `IExecutionContext` | Returns `(UserId?, AgentPrincipalId?, Permissions)` for the current MediatR-Send. Default impl reads from `ICurrentUserService` (unchanged HTTP path); agent loop installs an `AmbientExecutionContext` with both principals. |
| Cost cap resolver | `ICostCapResolver` | `(TenantId, AssistantId) → EffectiveCaps { MonthlyUsd?, DailyUsd?, Rpm? }`. Reads plan via `IFeatureFlagService`, applies per-agent overrides. |
| Cap accountant | `ICostCapAccountant` | `TryClaimAsync(tenantId, assistantId, estimatedUsd, window, capUsd)`, `RollbackClaimAsync(... claimId)` (for compensating a partial claim if a sibling window or rate-limit fails after the first window succeeded), `RecordActualAsync(...)` (reconciles estimate vs actual cost after the provider call returns). Atomic, Redis-backed. |

---

## 4. Domain Model

### 4.1 New entities (AI module DbContext)

```
AiAgentPrincipal
  Id              Guid (PK)
  AiAssistantId   Guid (FK → AiAssistant, unique)
  TenantId        Guid (FK)
  IsActive        bool
  CreatedAt       DateTimeOffset
  RevokedAt       DateTimeOffset?

AiAgentRole
  Id                  Guid (PK)
  AgentPrincipalId    Guid (FK → AiAgentPrincipal)
  RoleId              Guid (FK → Role, in core)
  AssignedAt          DateTimeOffset
  AssignedByUserId    Guid (FK → User)
  -- unique (AgentPrincipalId, RoleId)

AiModelPricing
  Id                       Guid (PK)
  Provider                 AiProviderType (enum)
  Model                    string (max 200)
  InputUsdPer1KTokens      decimal(18,8)
  OutputUsdPer1KTokens     decimal(18,8)
  IsActive                 bool
  EffectiveFrom            DateTimeOffset
  CreatedByUserId          Guid?
  -- unique (Provider, Model, EffectiveFrom)
```

Notes:
- `AiAgentRole.RoleId`, `AiAgentPrincipal.TenantId`, and `AiAgentRole.AssignedByUserId` reference rows in `ApplicationDbContext` (core), but EF cannot enforce cross-context navigation. They are stored as bare `Guid` values with **no EF navigation property**; integrity is enforced by command validators (`role exists`, `tenant exists`, `assigner is current tenant admin`). This matches the existing `AiUsageLog.TenantId` pattern from 4b. Database-level FK constraints are not declared because the two contexts may eventually live in separate schemas/databases — the module boundary forbids the assumption.
- Permission resolution for an agent principal is performed by a new helper (`IAgentPermissionResolver` in `Starter.Application.Common.Interfaces`), implemented in `Starter.Infrastructure.Identity` (which already owns role-permission joins). It loads `Role` + `RolePermission` from `ApplicationDbContext`. The AI module never queries core tables directly.
- `AiModelPricing` is versioned by `EffectiveFrom`. Cost calculation picks the pricing row with the latest `EffectiveFrom <= now` for `(Provider, Model)`. This lets superadmin set a future-dated price change without a flag day.
- All four new entities (`AiAgentPrincipal`, `AiAgentRole`, `AiModelPricing`, `AiRoleMetadata`) live in `Starter.Module.AI/Domain/Entities/` and are configured in `AiDbContext`. The only Core change is the audit-log dual-attribution columns (§4.2) — agent-specific concerns (including the role-assignability flag, see §5.2) stay in the AI module.

### 4.2 Modifications

`AiAssistant` (existing, AI module):
- `MonthlyCostCapUsd` `decimal(18,4)?` — null = inherit (no per-agent monthly cap)
- `DailyCostCapUsd` `decimal(18,4)?` — null = inherit
- `RequestsPerMinute` `int?` — null = inherit
- Note: there is **no** `AgentPrincipalId` column on `AiAssistant`; the relationship is owned by `AiAgentPrincipal.AiAssistantId` (1-to-1, principal owns the FK). This avoids a circular FK on creation.

`AiUsageLog` (existing, AI module — added in 4b observability):
- `AiAssistantId` `Guid?` (nullable so older rows and non-agent calls remain valid)
- `AgentPrincipalId` `Guid?` (nullable; populated only for agent-driven calls)
- New index: `(TenantId, AiAssistantId, CreatedAt DESC)` for window aggregation
- New index: `(TenantId, CreatedAt DESC)` for tenant-wide aggregation

`AuditLog` (existing, **core**, `Starter.Domain`):
- `OnBehalfOfUserId` `Guid?`
- `AgentPrincipalId` `Guid?`
- `AgentRunId` `Guid?`

These three nullable columns are the **only** core changes 5d-1 makes; everything else (including the role-assignability flag, see §5.2) lives in the AI module to respect the Core/Module boundary rule. The audit columns are module-agnostic metadata (in the same family as the existing `CorrelationId` column) and additive — no existing pipeline breaks.

### 4.3 Enums / errors

New enum value (existing `AgentRunStatus`):
- `CostCapExceeded`
- `RateLimitExceeded`

New `AiAgentErrors`:
- `AgentPrincipalNotFound(Guid)`
- `CostCapExceeded(string tier, decimal capUsd, decimal currentUsd)`
- `RateLimitExceeded(int rpm)`
- `PricingMissing(AiProviderType, string model)`
- `AgentRoleAssignmentNotPermitted(Guid roleId)` — see §5.5

---

## 5. Agent Principal & Security Model

### 5.1 Lifecycle

When an `AiAssistant` is created (template-install OR custom-create), the same handler creates a paired `AiAgentPrincipal` in the same EF transaction:

```
CreateAssistantHandler(/InstallTemplateHandler):
  assistant = new AiAssistant(...)
  principal = new AiAgentPrincipal(assistantId: assistant.Id, tenantId, isActive: assistant.IsActive)
  await dbContext.SaveChangesAsync()    -- single transaction
```

When an assistant is deactivated (`IsActive=false`), the paired principal is also deactivated. When an assistant is hard-deleted, the principal is hard-deleted (cascade); audit rows survive because they store the GUIDs as values, not navigation properties.

There is no login flow, no password, no email, no session, no 2FA, no refresh-token row for agent principals — by construction. The Auth pipeline never sees them. No guards needed.

### 5.2 Role assignment

New endpoints (§7) let tenant admins assign roles to an agent principal. The role list available for assignment is the same list available for human users — `Role.IsActive AND Role.TenantId IN (currentTenant, null)`.

**Constraint: agents cannot be assigned `SuperAdmin` or `TenantAdmin`.** A new AI-module table `AiRoleMetadata { RoleId, IsAgentAssignable }` gates this. Default behaviour when no row exists for a `RoleId`: assignable. The 5d-1 seed inserts rows for the SuperAdmin and TenantAdmin core roles with `IsAgentAssignable = false`. The validator (§7.1 `POST /assistants/{id}/roles`) consults `AiRoleMetadata` and rejects with `AgentRoleAssignmentNotPermitted` if the matched row is `false`.

**Why it lives in the AI module, not on the core `Role` entity:** "is this role assignable to a non-human principal" is an AI-specific business rule. Core knows nothing about agent principals; putting the flag on `Role` would leak module vocabulary into core and violate the Core/Module boundary rule. The `AiRoleMetadata` table is the right shape — it lets future modules (e.g., a hypothetical "service account" module) layer their own role-assignability rules in the same way without editing core.

**Rationale for the constraint itself:** preventing the trivial privilege escalation where a tenant admin gives the Tutor agent `TenantAdmin` and any user who can chat with Tutor inherits it through intersection (which collapses to `caller_perms ∩ tenant_admin_perms = caller_perms` — silently fine for that case, but enables the operational variant where the agent runs without a caller and acquires full admin power). Belt-and-suspenders against accidental escalation.

### 5.3 Execution context

New abstraction in `Starter.Application.Common.Interfaces`:

```csharp
public interface IExecutionContext
{
    Guid? UserId { get; }                 // chat caller, or null for operational
    Guid? AgentPrincipalId { get; }       // agent acting, or null for HTTP
    Guid? TenantId { get; }
    bool HasPermission(string permission);
}
```

Default registration: `HttpExecutionContext` — wraps `ICurrentUserService`, returns `UserId` only. This is what every existing MediatR handler sees for HTTP requests. **No code change** for the 99% case.

Inside `IAiAgentRuntime.RunAsync` (the agent loop), the runtime installs `AmbientExecutionContext` via `AsyncLocal<IExecutionContext>` for the duration of each tool dispatch. The dispatcher reads `IExecutionContext` (DI-resolved each call) which checks the ambient first, falls back to HTTP. `AgentToolDispatcher` reads `IExecutionContext` instead of `ICurrentUserService`:

```csharp
// before: if (!currentUser.HasPermission(def.RequiredPermission)) Failure(...)
// after:  if (!executionContext.HasPermission(def.RequiredPermission)) Failure(...)
```

`AmbientExecutionContext.HasPermission(p)` returns:
- If both `UserId` and `AgentPrincipalId` set: `agentPerms.Contains(p) AND callerPerms.Contains(p)` — intersection.
- If only `AgentPrincipalId` set (operational): `agentPerms.Contains(p)`.
- If only `UserId` set: should not happen inside the agent loop; treated as defensive `false`.

Permission caches:
- Caller permissions are already in the JWT claims set (no DB round-trip).
- Agent permissions resolve via `AiAgentPrincipal → AiAgentRole → Role → RolePermission → Permission`, cached per-agent for the duration of one agent run (`PerRunPermissionCache` scoped service). Refresh between runs.

### 5.4 Audit dual-attribution

The existing audit-log writer (`IAuditLogger` or equivalent) gets an overload that accepts `(actorUserId, agentPrincipalId, agentRunId, ...)`. Inside the agent loop, the runtime sets these on every tool dispatch via the same ambient context. From outside the loop, the existing single-actor signature is unchanged.

Admin UI work for "Tutor (acting for Alice)" rendering is out of scope for 5d-1 (lives in 7a/7b); the columns ship now so the UI work can be additive.

### 5.5 Permissions to add (`AiPermissions`)

Existing `AiPermissions` follows a flat naming convention (`Ai.ManageAssistants`, `Ai.ViewUsage`, etc.). New permissions match that style.

| Permission | Scope | Purpose |
|---|---|---|
| `Ai.AssignAgentRole` | Tenant admin | Assign / unassign agent principal roles |
| `Ai.ManageAgentBudget` | Tenant admin | Set per-agent cost caps and rate limit |
| `Ai.ManagePricing` | Superadmin | Edit `AiModelPricing` |

Read endpoints (usage rollups, role lists, budget queries) reuse the existing `Ai.ViewUsage` and `Ai.ManageAssistants` permissions — no new read permissions required.

Plan-feature limits (`ai.cost.tenant_monthly_usd`, `ai.agents.max_count`, `ai.agents.operational_enabled`) are gated by the existing plan-management permission — no new permission required.

---

## 6. Cost Caps & Billing Integration

### 6.1 Tier model

Two tiers, lowest wins:

| Tier | Owner | Storage | Validation |
|---|---|---|---|
| Plan ceiling | Superadmin | `SubscriptionPlan.Features` JSON keys (see §6.3) | Set per plan tier; no validation needed (it *is* the ceiling) |
| Per-agent cap | Tenant admin | `AiAssistant.MonthlyCostCapUsd`, `DailyCostCapUsd`, `RequestsPerMinute` | Validator rejects setting any cap higher than the corresponding plan ceiling for the tenant's current plan |

Tenant cap (third tier) is intentionally absent in 5d-1; it ships in 5f as a `TenantFeatureFlag` override of the same plan keys, which the existing `IFeatureFlagService` already resolves transparently. No 5d-1 code blocks this future addition.

### 6.2 Effective cap resolution

`ICostCapResolver.ResolveAsync(tenantId, assistantId, ct) → EffectiveCaps`:

```
plan_monthly  = IFeatureFlagService.GetValueAsync<decimal>("ai.cost.tenant_monthly_usd")
plan_daily    = IFeatureFlagService.GetValueAsync<decimal>("ai.cost.tenant_daily_usd")
plan_rpm      = IFeatureFlagService.GetValueAsync<int>("ai.agents.requests_per_minute_default")
agent         = await dbContext.AiAssistants.FindAsync(assistantId)

monthly = min_nonnull(plan_monthly, agent.MonthlyCostCapUsd)
daily   = min_nonnull(plan_daily,   agent.DailyCostCapUsd)
rpm     = min_nonnull(plan_rpm,     agent.RequestsPerMinute)
```

`min_nonnull` treats `null` as "no constraint at this tier." If both tiers are null for a dimension, that dimension is uncapped (rare; superadmin oversight).

Cached 60 s via the codebase's canonical `ICacheService` wrapper (matches `CachingEmbeddingService` precedent in the AI module). Invalidated explicitly when `AiAssistant` is updated (new `AssistantUpdatedEvent` raised from `SetBudget`) or when `TenantSubscription.SubscriptionPlanId` changes (existing `SubscriptionChangedEvent`). The cap accountant (§6.5) and rate limiter intentionally bypass `ICacheService` and use `IConnectionMultiplexer` directly because they need atomic Lua scripts / sorted-set ops that the wrapper does not expose; this is the only place 5d-1 reaches into raw Redis and the rationale is documented inline at the call site.

### 6.3 Plan-feature seed

Added to `BillingModule.SeedDataAsync` (existing extensibility point):

```
Free:
  ai.cost.tenant_monthly_usd        = 0
  ai.cost.tenant_daily_usd          = 0
  ai.agents.max_count               = 0
  ai.agents.operational_enabled     = false
  ai.agents.requests_per_minute_default = 0

Basic:
  ai.cost.tenant_monthly_usd        = 20
  ai.cost.tenant_daily_usd          = 2
  ai.agents.max_count               = 3
  ai.agents.operational_enabled     = false
  ai.agents.requests_per_minute_default = 10

Pro:
  ai.cost.tenant_monthly_usd        = 200
  ai.cost.tenant_daily_usd          = 20
  ai.agents.max_count               = 20
  ai.agents.operational_enabled     = true
  ai.agents.requests_per_minute_default = 60

Enterprise:
  ai.cost.tenant_monthly_usd        = 2000
  ai.cost.tenant_daily_usd          = 200
  ai.agents.max_count               = 200
  ai.agents.operational_enabled     = true
  ai.agents.requests_per_minute_default = 300
```

These are seed defaults — superadmin can edit any value via the existing plan-management endpoints. Each key gets a corresponding `FeatureFlag` row (`Category = Ai`, with `IsSystem = true`) in the same seed.

### 6.4 Cap-check pipeline

Implemented as a decorator: `CostCapEnforcingAgentRuntime : IAiAgentRuntime` registered ahead of the concrete runtime.

**Per-step flow:**

```
1. estimated_cost  = pricing.input  * inputTokens(messages)
                   + pricing.output * agent.MaxTokens
   (input tokens are counted; output tokens use MaxTokens as upper bound — fail-safe)

2. caps = await capResolver.ResolveAsync(tenantId, assistantId)

3. // atomic check + claim
   monthly = await accountant.TryClaimAsync(tenantId, assistantId, estimated_cost, Window.Monthly, caps.MonthlyUsd)
   if (!monthly.Granted) → emit step event, return AgentRunStatus.CostCapExceeded

   daily = await accountant.TryClaimAsync(tenantId, assistantId, estimated_cost, Window.Daily, caps.DailyUsd)
   if (!daily.Granted) → roll back monthly claim, emit step event, return CostCapExceeded

4. // rate limit
   rpm = await accountant.TryAcquireRateAsync(assistantId, caps.Rpm)
   if (!rpm.Granted) → roll back claims, emit step event, return RateLimitExceeded

5. await innerRuntime.RunStepAsync(...)

6. // reconcile actual cost (overcommit reclaim)
   actual = pricing.input * actualInputTokens + pricing.output * actualOutputTokens
   delta  = actual - estimated_cost          // typically negative
   await accountant.RecordActualAsync(tenantId, assistantId, delta, ...)
   write AiUsageLog row (assistantId, agentPrincipalId, tokens, EstimatedCost = actual)
```

### 6.5 Atomic accountant — implementation

Redis Lua script per-window key (`ai:cost:{tenantId}:{assistantId}:monthly:{yyyy-MM}`):

```lua
local current = tonumber(redis.call("GET", KEYS[1]) or "0")
local cap     = tonumber(ARGV[1])
local claim   = tonumber(ARGV[2])
if current + claim > cap then
  return {0, current, cap}
end
redis.call("INCRBYFLOAT", KEYS[1], claim)
redis.call("EXPIRE", KEYS[1], ARGV[3])     -- e.g. 35 * 86400 for monthly safety
return {1, current + claim, cap}
```

Cap convention: `cap = 0` blocks all spend (correct for Free plan), `cap > 0` is the USD ceiling. There is intentionally **no** "uncapped" sentinel — every plan tier carries an explicit ceiling. If a superadmin wants to express "effectively uncapped," they set a very large value (e.g., `1_000_000`). Negative caps are rejected by the plan-management validator.

Rate-limit uses Redis sliding-window pattern (`INCR` per-second bucket + sum last 60 buckets), already used elsewhere in the codebase (per `WebhookDeliveryService` rate guard).

**Failure mode.** If Redis is unavailable, the accountant fails *closed* (returns `Granted=false`, error code `AccountantUnavailable`). The existing health-check on Redis already exposes this state. We accept brief unavailability of agents over an over-bill incident.

### 6.6 Ground-truth reconciliation

Redis is the hot path. The truth lives in `AiUsageLog`. A nightly job (`AiCostReconciliationJob`, existing observability slot) sums `AiUsageLog` per tenant/window and overwrites the Redis counter, correcting any drift from crashed runs that claimed without recording. Same job emits a metric `ai_cost_drift_usd` for monitoring.

### 6.7 Caller-visible error

When a run terminates with `CostCapExceeded` or `RateLimitExceeded`, the chat layer renders a friendly localised message (resx key `ai.errors.cost_cap_exceeded` / `ai.errors.rate_limit_exceeded`):

> "This assistant has reached its monthly limit. Contact your admin to adjust budget."

The `RunTrace` payload (already exposed to admins via `AiActivityViews` per Plan 4b-4) carries the structured tier + cap + current values for forensics.

---

## 7. API Surface

All routes follow `api/v{version}/ai/...` and inherit the existing `BaseApiController` + `[Authorize]` + version conventions.

### 7.1 Tenant-admin endpoints

| Method | Route | Purpose | Permission |
|---|---|---|---|
| `PUT` | `/ai/assistants/{id}/budget` | Set per-agent caps `(MonthlyCostCapUsd?, DailyCostCapUsd?, RequestsPerMinute?)`; nulls clear the override | `Ai.ManageAgentBudget` |
| `GET` | `/ai/assistants/{id}/budget` | Read effective caps + current usage | `Ai.ViewUsage` |
| `POST` | `/ai/assistants/{id}/roles` | Assign role(s) to the agent principal | `Ai.AssignAgentRole` |
| `DELETE` | `/ai/assistants/{id}/roles/{roleId}` | Unassign | `Ai.AssignAgentRole` |
| `GET` | `/ai/assistants/{id}/roles` | List assigned roles | `Ai.ManageAssistants` |
| `GET` | `/ai/assistants/{id}/usage?window=daily\|monthly` | Per-agent usage rollup (cost + tokens + run count) | `Ai.ViewUsage` |
| `GET` | `/ai/usage/me` | Tenant-wide rollup with effective plan caps | `Ai.ViewUsage` |

### 7.2 Superadmin endpoints

| Method | Route | Purpose | Permission |
|---|---|---|---|
| `GET` | `/ai/pricing` | List active pricing entries | `Ai.ManagePricing` |
| `POST` | `/ai/pricing` | Upsert a pricing entry (creates a new versioned row) | `Ai.ManagePricing` |
| `DELETE` | `/ai/pricing/{id}` | Soft-deactivate (set `IsActive=false`); cannot hard-delete history | `Ai.ManagePricing` |

DTOs live under `Starter.Module.AI.Application.DTOs/Budget/`, `Pricing/`, `Usage/`. Validators per existing conventions.

---

## 8. Migrations & Seed

Two migrations.

**Core migration** (`Starter.Infrastructure`, single migration, name suggestion: `Ai_5d1_DualAttributionAudit`):
- `AuditLog.OnBehalfOfUserId Guid?`
- `AuditLog.AgentPrincipalId Guid?`
- `AuditLog.AgentRunId Guid?`
- (No changes to `Role` — see §5.2: role-assignability lives in the AI module's new `AiRoleMetadata` table.)

**AI-module migration** (`Starter.Module.AI`, name suggestion: `Ai_5d1_AgentIdentityEnforcement`):
- New tables: `AiAgentPrincipal`, `AiAgentRole`, `AiModelPricing`, `AiRoleMetadata`
- Seed `AiRoleMetadata` rows for SuperAdmin and TenantAdmin core roles → `IsAgentAssignable=false` (the seeder looks them up by name in `ApplicationDbContext`)
- `AiAssistant`: add `MonthlyCostCapUsd`, `DailyCostCapUsd`, `RequestsPerMinute`
- `AiUsageLog`: add `AiAssistantId`, `AgentPrincipalId`, plus indexes
- Seed: `AiModelPricing` rows for currently-supported models (OpenAI gpt-4o family, Claude Sonnet/Opus/Haiku, Ollama default = $0/$0, OpenAI `text-embedding-3-*`)
- Seed: `FeatureFlag` rows for the seven `ai.*` keys listed in §6.3 (with `Category="Ai"`, `IsSystem=true`)
- Seed: backfill `AiAgentPrincipal` for any existing `AiAssistant` rows (so installed templates from 5c-2 don't break)

**Plan feature JSON updates** to existing plan seed (`SubscriptionPlan.Features`): add the seven keys per tier per §6.3.

Per CLAUDE.md, this boilerplate **does not commit migrations** — apps generate their own. The migration code (`Up`/`Down`) is implied by the entity changes; do not add migration files to git.

---

## 9. Acid Tests

These must pass before 5d-1 is considered done. They map to the flagship vision's 5d acid tests insofar as 5d-1 alone covers them; the safety-specific ones are 5d-2's responsibility.

### 9.1 Identity + intersection
1. **Caller-restricted intersection.** A user with `Files.Read` chats with an agent that has `Files.Read` AND `Files.Delete` in its assigned role. The agent attempts `delete_file` tool. Dispatcher refuses (caller lacks `Files.Delete`), returns `ToolPermissionDenied`.
2. **Operational agent runs unconstrained by absent caller.** An operational agent (event-triggered, no caller) with `Files.Read` reads a file successfully. The same agent attempts `delete_file` for which it lacks permission → refused with `ToolPermissionDenied`.
3. **Agent principal cannot login.** No login flow exists for `AiAgentPrincipal`; verified by absence of code paths, not by guard. `User` table contains no agent rows.
4. **Assigning SuperAdmin to an agent is rejected.** Validator returns `AgentRoleAssignmentNotPermitted`.

### 9.2 Cost cap
5. **Per-agent monthly cap blocks.** Tenant on Pro plan ($200/mo). Agent has `MonthlyCostCapUsd = 5`. Once accountant sees ≥$5 claimed against agent for current month, next pre-step claim returns `{Granted=false, Tier="agent", CapUsd=5}`. Run terminates `CostCapExceeded`. Friendly chat message displayed.
6. **Plan ceiling blocks.** Tenant on Basic plan ($20/mo). No per-agent cap set. Tenant collectively consumes $19.95. Next agent call's $0.10 estimated cost is refused. Tier reported = `"plan"`.
7. **Cap validator rejects over-plan agent cap.** Setting `MonthlyCostCapUsd = 100` on a Basic-plan tenant ($20 ceiling) is rejected with validation error `"per-agent cap cannot exceed plan ceiling"`.
8. **Concurrent calls do not exceed cap.** 10 concurrent runs each estimating $1.00 against a $5 cap result in exactly 5 successful claims; 5 fail with `CostCapExceeded`. Asserts the Lua script's atomicity.
9. **Reconciliation corrects drift.** Manually corrupt the Redis counter (set to $1000); next reconciliation run resets it to the `AiUsageLog` ground truth.

### 9.3 Rate limit
10. **RPM throttle.** Agent with `RequestsPerMinute = 5`. 6 calls within 60 s. The 6th returns `RateLimitExceeded`. After 60 s of no calls, throttle resets.

### 9.4 Audit
11. **Dual attribution.** Chat with Tutor agent → tool call → `AuditLog` row has `UserId = null` (no human acted directly), `AgentPrincipalId = tutor.PrincipalId`, `OnBehalfOfUserId = chatCallerUserId`, `AgentRunId = currentRun.Id`.
12. **Operational audit.** Event-triggered run → row has `AgentPrincipalId` set, `OnBehalfOfUserId = null`, `AgentRunId` set.

### 9.5 Plan integration
13. **`ai.agents.max_count` enforced on create.** Free plan (`max_count = 0`) → creating any agent (custom OR template install) returns plan-limit error.
14. **`ai.agents.operational_enabled = false` blocks operational install.** A template targeting operational mode cannot be installed on Basic plan.

---

## 10. Out of Scope (explicit)

- **`[DangerousAction]` attribute / human-approval pause** — 5d-2.
- **Input/output content moderation pipeline** — 5d-2.
- **Tenant-cap override tier** — 5f (additive, no 5d-1 rework needed).
- **Admin UI for budget / pricing / role assignment** — 7a (read views) and 7b (editors).
- **Multi-currency** — caps are USD only. Tenant-displayed currency is unaffected.
- **Stripe / billing-provider changes** — caps are policy enforcement, not payment.
- **Backfill of historical `AiUsageLog`** with `AiAssistantId` — null is acceptable for pre-5d-1 rows.
- **Per-tenant currency conversion in pricing** — pricing is USD; converting for display is admin-UI's job.
- **Cost forecasting / projection / soft-warning alerts** — observability follow-up.
- **Token-pool model** — explicitly rejected; USD throughout.
- **Agent self-billing / per-tenant billing reports** — exposed as `/ai/usage/*` queries; building invoice / report UI is not in 5d-1.

---

## 11. Open Questions / Future Work

- **`AiAgentPrincipal.IsActive` vs `AiAssistant.IsActive`.** Currently mirror each other on lifecycle changes. Decision: keep both for now; revisit if a use case for "agent exists but suspended principal" emerges (e.g., temporarily revoking an agent's permissions while keeping the assistant configuration intact).
- **Embedding cost.** Embeddings consumed for RAG today are not attributed to any agent. 5d-1 does not change this — embedding cost remains a tenant-wide line item (`AiUsageLog` with `AiAssistantId = null`). Per-agent attribution requires routing the RAG retrieval through the agent context, which is a 5e/8 question.
- **Permission cache coherence.** The per-run agent-permission cache means a role unassignment mid-run does not take effect until the next run. Acceptable; documented in code.
- **Pricing rounding.** USD is stored to 8 decimal places in pricing, 4 in caps and totals. Cap-check arithmetic uses 8 throughout to avoid premature rounding. Display uses 4 decimals.

---

## 12. Glossary

| Term | Meaning |
|---|---|
| Agent principal | An `AiAgentPrincipal` row representing an agent as a security subject. |
| Caller | The human user who initiated the current chat. `null` for operational agents. |
| Intersection | Permission set = (agent permissions) ∩ (caller permissions); falls back to agent permissions only when no caller. |
| Plan ceiling | A `SubscriptionPlan.Features` JSON value setting the max for a metered AI resource. |
| Per-agent cap | A nullable column on `AiAssistant` overriding default for that agent (must be ≤ plan ceiling). |
| Window | Calendar UTC daily or monthly bucket for cost accounting. |
| Operational agent | An agent invoked by an event/cron trigger with no human caller. |
| Cost cap | USD upper bound on consumption per window. |
| Atomic claim | Redis Lua check-and-increment that prevents race-condition over-spend. |
