# Cross-Module Communication

**Audience:** Anyone adding code that crosses a module boundary — inside a module calling core, inside core calling a module, or between modules.

**Prerequisites:** Read [system-design.md](./system-design.md) first to understand the project graph, then [module-development.md](./module-development.md) for the per-module conventions.

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

## 2. Pattern 2 — Integration events via `IIntegrationEventCollector` (for fan-out)

**Use when:** 0..N modules may react to the same trigger, and the reactions should happen asynchronously with transactional-outbox reliability.

> ⚠️ **Publish via `IIntegrationEventCollector`, never `IPublishEndpoint` from a MediatR handler.**
> Direct `IPublishEndpoint` use from HTTP-request code routes through whichever
> `IScopedBusContextProvider<IBus>` was registered **last**. When the Workflow module
> registers its own `AddEntityFrameworkOutbox<WorkflowDbContext>`, its provider
> overrides core's — and events published from core handlers are silently written
> into `WorkflowDbContext`'s outbox, which is never saved. Events disappear with
> no error. See the commit message on PR #18 for the forensic writeup.
>
> Inside a MassTransit consumer (already in an MT pipeline), `IPublishEndpoint` is
> fine — MT's `OutboxSendContext` is already wired to the correct DbContext.

### How it works

1. The event type lives in `Starter.Application/Common/Events/` — the neutral shared location alongside `TenantRegisteredEvent`, `UserRegisteredEvent`, etc. It implements `IDomainEvent`.
2. The handler injects `IIntegrationEventCollector` (defined in the Application layer — keeps `Application` MassTransit-free) and calls `Schedule(new MyEvent(...))`. The collector is a scoped in-memory accumulator; it does **not** touch the bus.
3. The handler then calls `SaveChangesAsync()` on `IApplicationDbContext`. An EF `SaveChangesInterceptor` (`IntegrationEventOutboxInterceptor`) fires **before** EF emits SQL, lazily resolves the concrete `EntityFrameworkScopedBusContextProvider<IBus, ApplicationDbContext>` from DI (bypassing the overridable abstract slot), and calls `Publish(evt, evtType)` on **that** provider — so the outbox rows land in `ApplicationDbContext` alongside the business data.
4. A background `BusOutboxDeliveryService<ApplicationDbContext>` drains `OutboxMessage` to the broker; `OutboxState` tracks progress; `InboxState` dedupes retries.
5. Consumers implement `IConsumer<MyEvent>` anywhere. MassTransit's assembly scanning discovers them automatically at startup (`AddConsumers(moduleAssemblies)` in `DependencyInjection.cs`).

### Canonical example

Core publishes `TenantRegisteredEvent`; the Billing module consumes it to provision a free-tier subscription:

```csharp
// Core: src/Starter.Application/Features/Tenants/Commands/RegisterTenant/RegisterTenantCommandHandler.cs
internal sealed class RegisterTenantCommandHandler(
    IApplicationDbContext context,
    IIntegrationEventCollector eventCollector,  // ← collector, not IPublishEndpoint
    IPasswordService passwordService,
    /* ... */) : IRequestHandler<RegisterTenantCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(RegisterTenantCommand cmd, CancellationToken ct)
    {
        var tenant = Tenant.Create(cmd.Name, cmd.Slug);
        context.Tenants.Add(tenant);

        // Scheduled into an in-memory list on the request-scoped collector.
        // The IntegrationEventOutboxInterceptor picks it up during SavingChangesAsync
        // and atomically writes the outbox row into ApplicationDbContext.
        eventCollector.Schedule(new TenantRegisteredEvent(
            tenant.Id, tenant.Name, tenant.Slug, cmd.OwnerUserId, DateTime.UtcNow));

        await context.SaveChangesAsync(ct);   // ← business data + outbox row commit atomically
        return Result.Success(tenant.Id);
    }
}
```

```csharp
// Billing module: src/modules/Starter.Module.Billing/Application/EventHandlers/
//                CreateFreeTierSubscriptionOnTenantRegistered.cs
internal sealed class CreateFreeTierSubscriptionOnTenantRegistered(
    BillingDbContext context,
    IUsageTracker usageTracker,
    ILogger<CreateFreeTierSubscriptionOnTenantRegistered> logger)
    : IConsumer<TenantRegisteredEvent>
{
    public async Task Consume(ConsumeContext<TenantRegisteredEvent> ctx)
    {
        // MANDATORY idempotency check — see "Consumer idempotency" below.
        // TenantId is the stable correlation key; a duplicate delivery returns silently.
        if (await context.TenantSubscriptions
                .IgnoreQueryFilters()
                .AnyAsync(s => s.TenantId == ctx.Message.TenantId, ctx.CancellationToken))
            return;

        // ... provision free-tier subscription ...
        await context.SaveChangesAsync(ctx.CancellationToken);
    }
}
```

### Consumer idempotency

At-least-once delivery is the guarantee. Exactly-once is **your** responsibility.

The boilerplate convention is a **domain-uniqueness check at the top of every consumer**:

- Pick a stable natural key from the event (`TenantId`, `OrderId`, a composite key).
- Query the consumer's own DbContext with `IgnoreQueryFilters().AnyAsync(...)`.
- If the row already exists → log (optional) and `return` without writing.

Never add a generic "processed messages" table — you duplicate what MassTransit's `InboxState` already does, and you introduce a cross-DbContext coupling. The domain-uniqueness check is simpler and equally correct.

### Retry, dead-letter, and when to throw

Every receive endpoint inherits a default policy configured in `AddMessaging()`:

- **3 in-process retries** at 1 s, 5 s, 15 s (exponential-ish). Handles transient DB jitter, dependency blips.
- **After retries exhaust**, MT NACKs and RabbitMQ auto-routes the message to the endpoint's `_error` queue — that's the dead-letter destination.
- **Circuit breaker**: 15 % failure rate over a 1-minute window (≥10 msgs) trips the endpoint offline for 5 minutes. Protects downstream services from cascading failure.

Individual consumers can override via their own `ConsumerDefinition` (see `DeliverWebhookConsumerDefinition` for an example with a longer retry curve).

**Implication for consumer code:** throwing is the correct way to signal a transient failure. The old "never throw — just log and return" guidance is wrong for retryable errors. Current discipline:

| Situation | What to do |
|---|---|
| Transient failure (DB unreachable, 5xx from dependency) | **Throw** — retry will fire |
| Non-retryable business condition (unknown tenant, feature off) | Log at Info and `return` |
| Precondition failed (already processed — idempotency hit) | `return` silently |
| Poison message (deserialization error, bad payload) | MT handles it automatically — it goes to `_error` on first attempt |

### Correlation

The interceptor derives a deterministic `Guid` from `Activity.Current.TraceId` and stamps it on every scheduled event as both `ConversationId` and `CorrelationId`. All events emitted from one HTTP request share the same ID, so MT traces, consumer logs, and OpenTelemetry spans group the causal chain cleanly. When there's no ambient `Activity` (hosted services, background work) the interceptor falls back to MT's default Guid generation.

### Operational monitoring

The `outbox-delivery-lag` health check at `/health`:

- **Healthy** — backlog is small or young
- **Degraded** (never Unhealthy) — backlog exceeds the thresholds in `Outbox:HealthCheck:*`

Thresholds default to 1000 pending rows / 5 minutes oldest age. Tune in `appsettings.json` per environment. Liveness probes should treat this as informational — a lagging outbox is a delivery problem, not an API-readiness problem.

### Log correlation

A MassTransit consume filter (`LogContextEnrichmentFilter`) pushes three properties into the `ILogger` scope for every consumer invocation:

- `ConversationId` — derived from the originating HTTP request's `Activity.TraceId` by the outbox interceptor. All events emitted from one request share this ID.
- `MessageId` — unique per message.
- `MessageType` — short CLR type name.

Serilog's `FromLogContext()` enricher picks these up, so every log line inside a consumer carries the correlation tokens. Operational recipe: when debugging a broken request, grep the API logs for its trace ID, pull `ConversationId` from any matching line, then grep the consumer logs for that `ConversationId` to see the full downstream chain.

### External side effects (emails, notifications, webhooks)

Anything that touches a flaky external system should ride on the outbox instead of being called inline after `SaveChangesAsync`:

```csharp
// WRONG — inline after commit. If SMTP is briefly down the tenant has been
// created but can never verify their email.
await context.SaveChangesAsync(ct);
var otpCode = await otpService.GenerateAsync(...);
var emailMessage = emailTemplateService.RenderEmailVerification(...);
await emailService.SendAsync(emailMessage, ct);  // ← no retry, no DLQ

// RIGHT — render synchronously, schedule the dispatch event, commit once.
var otpCode = await otpService.GenerateAsync(...);
var emailMessage = emailTemplateService.RenderEmailVerification(...);
eventCollector.Schedule(new SendEmailRequestedEvent(emailMessage, DateTime.UtcNow));
await context.SaveChangesAsync(ct);   // commits business data + outbox row atomically
```

The `EmailDispatchConsumer` in `Starter.Infrastructure/Consumers/` performs the SMTP call with the bus-level retry and DLQ protection. For other external side effects — HTTP webhooks, SMS, push notifications — follow the same pattern: schedule an integration event, let a consumer handle the external call.

### Consumer retry attempts and user-visible state

Consumers that persist progress state (AI ingestion, import jobs, report generation, etc.) should only flip the domain entity to a terminal `Failed` state on the **last** retry attempt. Flipping on every attempt causes UI flicker — `Processing → Failed → Processing → Failed → Completed` — during a transient outage that recovers.

Pattern (see `ProcessDocumentConsumer.Consume` for the live example):

```csharp
catch (OperationCanceledException) when (ct.IsCancellationRequested)
{
    throw;  // not a failure — just propagate
}
catch (Exception ex)
{
    const int MaxRetries = 3;    // matches the default bus policy
    if (context.GetRetryAttempt() >= MaxRetries)
    {
        entity.MarkFailed(ex.Message);
        await db.SaveChangesAsync(CancellationToken.None);
    }
    throw;  // always propagate — MT decides retry vs dead-letter
}
```

### Event schema evolution

Treat every event as a **public contract** the moment it's published once. The rules:

- **Additive changes only** on the existing type — new properties with sensible defaults.
- **Renaming** a property, **changing** a type, or **removing** a property is a breaking change. Create `TenantRegisteredEventV2` alongside the original and migrate consumers gradually. Delete `V1` only after the `_error` and outbox are fully drained of the old shape.
- **Never change** the CLR namespace + type name on a live event — MT uses that string as the message routing key.

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

### Known follow-ups

Pending production-hardening items — dead-letter replay tooling, published health-check thresholds, analyzer for event contracts, etc. — are tracked in [messaging-followups.md](messaging-followups.md).

### Files involved

If you need to trace the machinery:

| File | Role |
|------|------|
| `Starter.Application/Common/Interfaces/IIntegrationEventCollector.cs` | Application-layer abstraction handlers inject |
| `Starter.Application/Common/Events/*.cs` | Shared event contracts |
| `Starter.Infrastructure/Persistence/Interceptors/IntegrationEventCollector.cs` | Scoped accumulator |
| `Starter.Infrastructure/Persistence/Interceptors/IntegrationEventOutboxInterceptor.cs` | EF interceptor that writes outbox rows |
| `Starter.Infrastructure/DependencyInjection.cs` → `AddMessaging()` | Bus registration, retry / circuit-breaker defaults |
| `Starter.Infrastructure/Messaging/OutboxDeliveryLagHealthCheck.cs` | `/health` probe for delivery lag |

An architecture test (`MessagingArchitectureTests`) asserts `Starter.Application` has no dependency on `MassTransit` — CI fails if a handler tries to inject `IPublishEndpoint` directly.

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
