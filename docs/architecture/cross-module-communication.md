# Cross-Module Communication

**Audience:** Anyone adding code that crosses a module boundary — inside a module calling core, inside core calling a module, or between modules.

**Prerequisites:** Read [system-design.md](./system-design.md) first to understand the project graph, then [module-development-guide.md](./module-development-guide.md) for the per-module conventions.

---

## Overview — three patterns, one rule

> When a module needs something to happen outside its own boundaries, pick exactly one of these three patterns. Never use a fourth.

### The decision tree

```
Is the interaction RECEIVING data from another module (or from core)?
│
├── YES → Use a Reader Service
│         (ITenantReader / IUserReader / IRoleReader / future readers)
│         → Pattern 3 below.
│
└── NO (the module is TRIGGERING something elsewhere):
    │
    ├── Does the trigger have a known, single consumer with a
    │   well-defined contract?
    │   (e.g. "dispatch a webhook", "send a notification", "upload a file",
    │   "check a quota", "track usage")
    │   └── YES → Use a Capability Contract call
    │             (IWebhookPublisher, INotificationService, IFileService,
    │              IQuotaChecker, IUsageTracker, ...)
    │             → Pattern 1 below. DEFAULT CHOICE.
    │
    └── Does the trigger have 0..N consumers, need async delivery,
        or need transactional-outbox reliability?
        (e.g. "tenant registered — Billing, Notifications, and any
         future module may each want to react")
        └── YES → Use an Integration Event via IPublishEndpoint
                  → Pattern 2 below.
```

### Why three, not four?

Every cross-module interaction in this codebase fits one of these three patterns. We explicitly forbid a fourth pattern — **cross-module MediatR `INotificationHandler<T>` handlers that subscribe to another module's domain event**. See [§6 Anti-patterns](#6-anti-patterns).

---

## 1. Pattern 1 — Capability contract calls (the default)

**Use when:** Module A needs a specific side effect in Module B (or in core). The side effect is well-defined and named: "dispatch a webhook", "send a push notification", "upload a file", "check a plan quota".

### How it works

1. Module B (the provider) exposes an interface in `Starter.Abstractions/Capabilities/`. The interface has a primitive-or-self-contained signature — the types it mentions must live inside `Starter.Abstractions` itself, because `Starter.Abstractions` has zero project references.
2. Core registers a **Null Object fallback** via `TryAddScoped` (or `TryAddSingleton`) in `Starter.Infrastructure.DependencyInjection.AddCapabilities()`. The fallback either silently no-ops (e.g. `NullWebhookPublisher`) or throws `CapabilityNotInstalledException` on writes (e.g. `NullBillingProvider`, surfaced as HTTP 501 by the exception middleware).
3. Module B (if installed) registers its real implementation via `services.AddScoped<IBillingProvider, MockBillingProvider>()` in `BillingModule.ConfigureServices`. Because module `ConfigureServices` runs AFTER `AddInfrastructure`, the real registration wins.
4. Module A (the consumer, which can be core or another module) injects the interface and calls it unconditionally. No null checks, no feature-flag branching.

### Canonical example

The Billing module dispatches a webhook when a subscription changes:

```csharp
// src/modules/Starter.Module.Billing/Application/Commands/ChangePlan/ChangePlanCommandHandler.cs

internal sealed class ChangePlanCommandHandler(
    BillingDbContext context,
    ICurrentUserService currentUser,
    IBillingProvider billingProvider,
    IWebhookPublisher webhookPublisher)   // ← capability from Starter.Abstractions.Capabilities
    : IRequestHandler<ChangePlanCommand, Result>
{
    public async Task<Result> Handle(ChangePlanCommand request, CancellationToken ct)
    {
        // ... existing plan-change logic ...
        var oldPlanId = subscription.SubscriptionPlanId;
        subscription.ChangePlan(newPlan.Id, /* ... */);
        await context.SaveChangesAsync(ct);

        // Cross-module side effect via capability. Webhooks installed?
        // dispatches. Not installed? NullWebhookPublisher silently no-ops.
        await webhookPublisher.PublishAsync(
            eventType: "subscription.changed",
            tenantId: tenantId,
            data: new { tenantId, oldPlanId, newPlanId = newPlan.Id },
            cancellationToken: ct);

        return Result.Success();
    }
}
```

Billing has ZERO compile-time reference to the Webhooks module. The only thing Billing knows is `IWebhookPublisher`, which lives in `Starter.Abstractions.Capabilities`. Removing the Webhooks module from the build removes only Webhooks' DLL — Billing continues to work, and every call to `PublishAsync` becomes a silent no-op via `NullWebhookPublisher`.

### What happens when the provider module is absent

The Null Object fallback runs. The behavior depends on the contract:

| Contract | Null behavior |
|---|---|
| `IBillingProvider` | Throws `CapabilityNotInstalledException` → HTTP 501 via the global exception middleware |
| `IWebhookPublisher` | Silent no-op (logs at Debug) |
| `IImportExportRegistry` | Returns empty collections |
| `IQuotaChecker` | Returns `QuotaResult.Unlimited()` |
| `IUsageMetricCalculator` | Not registered → `UsageTrackerService` returns 0 for that metric |

Each Null Object is registered with the **same lifetime** as the real implementation it stands in for, so swapping them doesn't shift lifetimes.

### When NOT to use Pattern 1

- When the trigger might have 3+ consumers → use Pattern 2 (fan-out).
- When you need async delivery with outbox-backed retries → use Pattern 2.
- When the interaction is a **read**, not a trigger → use Pattern 3.

### Scalability — why this is the default

Each new capability adds **one interface** in `Starter.Abstractions.Capabilities` + **one Null Object** in `Starter.Infrastructure/Capabilities/NullObjects/`. **Core grows by exactly one file per capability, not one file per event.** Modules that provide the capability live entirely in their own project; modules that consume just inject the interface. Zero cross-module compile-time references.

For a codebase with 20 modules and 5 distinct triggers each, Pattern 1 adds 100 capability interfaces to `Starter.Abstractions` — each small (usually one method) and self-contained. Pattern 2 would add 100 event types to a shared location plus a consumer per subscribing module. Pattern 1 scales linearly with the capabilities, not the cartesian product of modules × events.

---

## 2. Pattern 2 — Integration events via `IPublishEndpoint` (for fan-out)

**Use when:** 0..N modules may react to the same trigger, and the reactions should happen asynchronously with transactional-outbox reliability.

### How it works

1. The event type lives in `Starter.Application/Common/Events/` — the neutral shared location alongside `TenantRegisteredEvent`, `UserRegisteredEvent`, etc.
2. The publishing code (core or a module) injects `IPublishEndpoint` from MassTransit and calls `Publish(new MyEvent(...))` inside the same EF Core transaction as the business write. MassTransit's EF Core outbox extension writes the event into the `OutboxMessage` table atomically with `SaveChangesAsync`.
3. A background MassTransit dispatcher reads `OutboxMessage`, delivers to every registered `IConsumer<MyEvent>`, and marks the message delivered in `OutboxState`. `InboxState` deduplicates retries.
4. Consumers implement `IConsumer<MyEvent>` anywhere. MassTransit's assembly scanning discovers them automatically at startup (`AddConsumers(moduleAssemblies)` in `DependencyInjection.cs`).

### Canonical example

Core publishes `TenantRegisteredEvent`; the Billing module consumes it to provision a free-tier subscription:

```csharp
// Core: src/Starter.Application/Features/Tenants/Commands/RegisterTenant/RegisterTenantCommandHandler.cs
public async Task<Result<Guid>> Handle(RegisterTenantCommand cmd, CancellationToken ct)
{
    var tenant = Tenant.Create(cmd.Name, cmd.Slug, cmd.OwnerUserId);
    db.Tenants.Add(tenant);

    // Published into the outbox atomically with the tenant write
    await publishEndpoint.Publish(new TenantRegisteredEvent(
        tenant.Id, tenant.Name, tenant.Slug, cmd.OwnerUserId, DateTime.UtcNow
    ), ct);

    await db.SaveChangesAsync(ct);   // ← outbox row committed with the tenant
    return Result.Success(tenant.Id);
}
```

```csharp
// Billing module: src/modules/Starter.Module.Billing/Application/EventHandlers/
//                CreateFreeTierSubscriptionOnTenantRegistered.cs
internal sealed class CreateFreeTierSubscriptionOnTenantRegistered(
    BillingDbContext context,
    IUsageTracker usageTracker,
    IWebhookPublisher webhookPublisher,
    ILogger<CreateFreeTierSubscriptionOnTenantRegistered> logger)
    : IConsumer<TenantRegisteredEvent>
{
    public async Task Consume(ConsumeContext<TenantRegisteredEvent> ctx)
    {
        // Manual idempotency check — see system-design.md §8 for why
        if (await context.TenantSubscriptions.AnyAsync(s => s.TenantId == ctx.Message.TenantId, ctx.CancellationToken))
            return;

        // ... provision free-tier subscription ...
        await context.SaveChangesAsync(ctx.CancellationToken);
    }
}
```

### What happens when a consumer is absent

The event is still published to the outbox, the dispatcher finds zero registered consumers for that message type, and marks it delivered without dispatching. No error, no retry storm. The original business write is unaffected.

### When NOT to use Pattern 2

- When you have a **single** known consumer → use Pattern 1 (capability call). The outbox overhead and the shared-location event type are unnecessary.
- When the interaction needs **synchronous** completion (e.g. "check this quota before I proceed") → use Pattern 1.
- When the interaction is a **read** → use Pattern 3.

### When to escalate from Pattern 1 to Pattern 2

Three signals:

1. You find yourself writing the same `IWebhookPublisher.PublishAsync("foo.bar", ...)` (or similar) in 3+ command handlers with near-identical payloads.
2. You want async delivery with automatic retries, not inline execution.
3. The consumer list is genuinely open — analytics might want to log it, audit might want to record it, reporting might want to aggregate it, and so on.

When all three apply, define an integration event in `Starter.Application/Common/Events/` and switch the publishers over.

### Scalability consideration

Each new integration event adds **one event type** to `Starter.Application/Common/Events/`. That file ships with every build of the boilerplate, even builds that don't include the subscribing modules. That's fine for truly shared events (tenant registration, user registration) but can get noisy if you reach for Pattern 2 for every cross-module trigger. **Pattern 1 is strictly better when the consumer count is exactly 1**, because Pattern 1 adds nothing to core for that single-consumer case.

---

## 3. Pattern 3 — Reader services for cross-module reads

**Use when:** Module A needs to READ data owned by core (tenant name, user email, role details).

> **Never** when Module A needs to read data owned by Module B. That's a code smell — it means Module A has fate-coupled itself to Module B. See [§6 Anti-patterns](#6-anti-patterns).

### How it works

1. Core defines a reader interface in `Starter.Abstractions/Readers/` with a flat DTO return type (`TenantSummary`, `UserSummary`, `RoleSummary`). The DTOs expose only the fields modules are allowed to see — no navigation properties, no entity tracking.
2. Core implements the reader in `Starter.Infrastructure/Readers/` using `IApplicationDbContext`, `IgnoreQueryFilters().AsNoTracking()`, and `Select(...)` projections.
3. Modules inject the reader interface and call `GetAsync(id)` or `GetManyAsync(ids)`. Each call is one SQL query; batching is available via `GetManyAsync` for N+1 avoidance.

### Canonical example

The Webhooks module looks up a user's email to include in a delivery attempt:

```csharp
// src/modules/Starter.Module.Webhooks/Application/EventHandlers/WebhookUserEventHandler.cs
internal sealed class WebhookUserEventHandler(
    IUserReader userReader,            // ← reader from Starter.Abstractions.Readers
    IWebhookPublisher webhookPublisher)
    : INotificationHandler<UserCreatedEvent>
{
    public async Task Handle(UserCreatedEvent evt, CancellationToken ct)
    {
        var user = await userReader.GetAsync(evt.UserId, ct);
        if (user is null) return;

        await webhookPublisher.PublishAsync("user.created", user.TenantId,
            new { userId = user.Id, email = user.Email, displayName = user.DisplayName },
            ct);
    }
}
```

The handler never touches `IApplicationDbContext` and never references core entity types. The `UserSummary` DTO is the entire surface area.

### Why not inject `IApplicationDbContext`

Direct injection couples the module to core's EF model. If core renames a field or adds a navigation property, the module breaks at compile time. Reader services limit the surface to small, explicit DTOs that are stable across refactors.

### Adding a new reader

When you need a reader that doesn't exist yet:

1. Define the interface + summary DTO in `src/Starter.Abstractions/Readers/I{Entity}Reader.cs`. The DTO fields must use primitive or Abstractions-owned types (no `Starter.Domain.*` types — `Starter.Abstractions` has zero project references).
2. Implement the reader in `src/Starter.Infrastructure/Readers/{Entity}Reader.cs`.
3. Register it in `AddCapabilities()` in `Starter.Infrastructure.DependencyInjection`.

---

## 4. Using Pattern 2 to extend Pattern 1 — the hybrid case

Sometimes you start with Pattern 1 (direct capability call) and later realize you need fan-out. The migration is straightforward:

1. Keep the existing capability contract — it stays useful for the "single consumer" case.
2. Define a new integration event in `Starter.Application/Common/Events/`.
3. Change the publishing handler to `Publish(new MyEvent(...))` via `IPublishEndpoint` in place of the direct capability call.
4. Create an `IConsumer<MyEvent>` in each consuming module (including the one that used to be called directly). Each consumer does the work that used to happen inline.

There's no hurry to migrate. Pattern 1 is fine until the third consumer shows up.

---

## 5. Decision examples — real cases from this codebase

| Scenario | Pattern | Reason |
|---|---|---|
| Billing dispatches a webhook when a subscription changes | **1** (capability) | Single named side effect; webhook dispatch is a well-defined capability |
| Billing dispatches a webhook when a subscription is canceled | **1** (capability) | Same as above — distinct event name but same mechanism |
| Billing dispatches a webhook after provisioning a free-tier subscription | **1** (capability) | Fires from inside a `IConsumer<TenantRegisteredEvent>` handler, then calls the capability |
| Core provisions a free-tier subscription when a tenant is registered | **2** (integration event) | Tenant registration is a fan-out point — Billing may react, Notifications may react, future modules may react |
| Core sends a notification when a user is created | **2** (integration event) | Same fan-out reasoning; `UserRegisteredEvent` is in `Common/Events/` |
| Webhooks looks up a user's email to include in a delivery | **3** (reader service) | Read-only access to core data |
| Billing checks if a feature flag is enabled for a tenant | **1** (capability via `IFeatureFlagService`) | `IFeatureFlagService` is a core-provided capability (lives in `Starter.Application/Common/Interfaces/` because its impl is always core-owned) |
| UsageTrackerService self-heals a metric from the database | **1** (capability via `IUsageMetricCalculator`) | Each module registers its own calculator for its own metric; core dispatches by metric name |
| E-commerce (future) checks quota when creating an order | **1** (capability via `IQuotaChecker`) | Single-consumer check; Billing module may provide the real implementation |
| E-commerce (future) wants POS module to reserve inventory | **1** (capability) | Add an `IInventoryReserver` contract; POS provides real impl, core provides a Null Object |
| Analytics (future) wants to log every subscription change | **2** (integration event) | Multiple potential consumers (analytics + audit + reporting) — convert the existing capability call to an integration event at that point |

---

## 6. Anti-patterns

These used to exist in this codebase, were removed during the module relocation refactor, and are now forbidden.

### ❌ Cross-module `INotificationHandler<T>` consuming another module's domain event

This was `WebhookBillingEventHandler` before the refactor. It had:

```csharp
// BAD — this file has been DELETED
internal sealed class WebhookBillingEventHandler(IWebhookPublisher publisher)
    : INotificationHandler<SubscriptionChangedEvent>   // ← Billing's event type
{
    public Task Handle(SubscriptionChangedEvent evt, CancellationToken ct)
        => publisher.PublishAsync("subscription.changed", evt.TenantId, /* ... */, ct);
}
```

The Webhooks module had `using Starter.Domain.Billing.Events;` — a hidden compile-time coupling. If the Billing module was removed from the build, this file failed to compile.

**The fix** was to delete `WebhookBillingEventHandler` and have Billing's command handlers call `IWebhookPublisher.PublishAsync` directly (Pattern 1). The responsibility inversion makes Webhooks ignorant of Billing's existence. Adding new subscription-related webhook events in the future just means adding another `PublishAsync` call in a Billing handler — no Webhooks changes.

**The rule:** modules never subscribe to each other's domain events via MediatR. Intra-module `INotificationHandler<T>` is fine (e.g. `SyncPlanFeaturesHandler` inside Billing listening to its own `SubscriptionChangedEvent`). Cross-module is forbidden.

### ❌ Module-owned types in `Starter.Domain/`

Before the refactor, `Starter.Domain/{Billing,Webhooks,ImportExport}/` contained the four Billing entities, two Webhooks entities, and one ImportExport entity. This meant:
- Dead code compiled into every build
- `Starter.Domain.dll` couldn't be shipped independently
- A developer browsing `Starter.Domain` saw folders for modules not installed

**The fix** was Phases 2–4 of the relocation refactor — every module-owned type lives under `src/modules/Starter.Module.{Name}/Domain/` now. `Starter.Domain/` contains only core entities.

### ❌ Cross-module `Include()` or `Join()` in a single EF query

Structurally impossible now because each module has its own `DbContext`. Historically, a query like `db.Subscriptions.Include(s => s.Tenant)` would join `TenantSubscription` (Billing) with `Tenant` (core) in one SQL statement. Today `BillingDbContext` doesn't know about `Tenant`.

**The fix** is Pattern 3 (reader service) for on-demand lookups, or Pattern 2 (integration event + denormalized snapshot) for hot read paths that can't tolerate an extra round trip. See `GetAllSubscriptionsQueryHandler` for a canonical "fetch ids, paginate locally, back-fill via reader" rewrite.

### ❌ Sharing an entity type across modules

If two modules think they need the same concept, either (a) promote it to core (`Starter.Domain`) if it's genuinely shared, or (b) each module has its own flat copy of the relevant fields. Sharing entities creates fate-coupling — a schema change in one module breaks the other.

### ❌ Cross-context `IApplicationDbContext` injection inside a module

A module's handlers should inject the module's own `DbContext` for module-owned data and reader services (Pattern 3) for core data. Injecting `IApplicationDbContext` directly is allowed for genuinely cross-cutting reads where no reader exists yet, but every such injection should be justified — prefer adding a reader service over growing the set of handlers that know about core's EF model.

---

## 7. Cheat sheet

A quick-reference for day-to-day use.

```
I need to do X from my module...

   ...and X is "trigger a webhook"              → IWebhookPublisher.PublishAsync     (Pattern 1)
   ...and X is "send a notification"            → INotificationService.SendAsync     (Pattern 1)
   ...and X is "upload a file"                  → IFileService.UploadAsync           (Pattern 1)
   ...and X is "check a quota"                  → IQuotaChecker.CheckAsync           (Pattern 1)
   ...and X is "track usage"                    → IUsageTracker.IncrementAsync
                                                    + IUsageMetricCalculator         (Pattern 1)
   ...and X is "process a subscription change"  → IBillingProvider.ChangeSubscriptionAsync
                                                                                     (Pattern 1)
   ...and X is "evaluate a feature flag"        → IFeatureFlagService.IsEnabledAsync (Pattern 1)

   ...and X is "get the tenant name"            → ITenantReader.GetAsync             (Pattern 3)
   ...and X is "get a user's email"             → IUserReader.GetAsync               (Pattern 3)
   ...and X is "get a role's details"           → IRoleReader.GetAsync               (Pattern 3)

   ...and X is "react to tenant registration"   → IConsumer<TenantRegisteredEvent>   (Pattern 2)
   ...and X is "react to user creation"         → IConsumer<UserRegisteredEvent>     (Pattern 2)
   ...and X is "react to file upload"           → IConsumer<FileUploadedEvent>       (Pattern 2)

   ...and X is "publish an event I own that
                exactly one other module
                consumes"                       → Direct capability call (Pattern 1).
                                                  Do NOT add a new event type.

   ...and X is "publish an event I own that
                multiple modules may consume"   → IPublishEndpoint.Publish + new event type in
                                                  Starter.Application/Common/Events/  (Pattern 2)
```

---

## 8. Enforcement

These patterns are enforced at four layers — see [system-design.md §10](./system-design.md#10-architecture-rules-and-how-theyre-enforced) for details:

- **Compile-time (project references)**: `Starter.Abstractions` has zero project references; `Starter.Domain` has zero references; module projects only reference `Starter.Abstractions.Web`.
- **Test-time (`AbstractionsPurityTests`)**: reflection-based check at `tests/Starter.Api.Tests/Architecture/AbstractionsPurityTests.cs` fails CI if `Starter.Abstractions.dll` acquires a forbidden reference.
- **Lint-time (frontend `no-restricted-imports`)**: ESLint blocks core FE files from importing from `@/features/{billing,webhooks,import-export}/*`.
- **Manual (killer test)**: `pwsh ./scripts/rename.ps1 -Modules None` generates a fresh app with every module excluded; both backend and frontend must build cleanly.

If you add a new cross-module interaction and the killer test fails, you've probably used the forbidden fourth pattern (cross-module `INotificationHandler<T>`) or leaked a module type into `Starter.Domain`. Go back to this doc and pick the right pattern.

---

**Questions? New pattern you want to add?** Update this doc in the same PR as the code change. Anti-patterns are only useful if we keep the list of "forbidden things" current.
