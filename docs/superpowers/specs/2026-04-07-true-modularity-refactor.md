# True Modularity Refactor — Design Spec

**Date:** 2026-04-07
**Status:** Approved, in execution
**Related plan:** `docs/superpowers/plans/2026-04-07-true-modularity-refactor.md` (to be generated from the internal plan file)

---

## 1. Context

### The problem

The boilerplate currently has 9 "modules" extracted (Files, Notifications, FeatureFlags, ApiKeys, AuditLogs, Reports, Billing, Webhooks, ImportExport). The module system works — each has its own project, permissions, seed data, and can be added/removed via the rename script.

**But none of them are actually removable.** Previous testing exposed the reality: every module has cross-dependencies with core or with other modules, so removing one causes compilation or runtime failures. The `-Modules` parameter in the rename script builds, but in practice all 9 modules remain required.

The root causes fall into exactly four categories of cross-dependency, each requiring a different architectural pattern.

### The goal

Refactor the boilerplate so it supports:

1. **Stripped-down solutions** — build a POS app from the boilerplate without Billing, Webhooks, or their code/tables
2. **Domain modules** (e-commerce, CRM, LMS) — add custom modules without touching core files
3. **Independent module development** — modules evolve independently with their own tests, migrations, releases
4. **Future package distribution** — modules as NuGet/npm packages with optional source-code protection
5. **Clean code** — no null checks, no hacks, no conditional imports; boundaries enforced at compile time

### Why now

Agencies evaluating the boilerplate as a sellable product need to see genuine modularity. The current state is "monolith with module aesthetics" — building on it as an agency product requires the real thing. This refactor is also a prerequisite for the eventual CLI tool (`starter new`, `starter module add`) and package distribution.

---

## 2. The Four Dependency Categories

Every cross-dependency in the current codebase falls into exactly one of four categories. Naming them is the most important step, because each needs a different pattern. Mixing patterns is fine; mixing them *per category* is the trap.

| # | Category | Example | Pattern |
|---|----------|---------|---------|
| **C1** | Core needs a capability that a module owns synchronously | `RegisterUserCommandHandler` checks quota via `IFeatureFlagService` | **Capability contracts** (Null Object + DI) |
| **C2** | Core action triggers module side effects asynchronously | Tenant registered → Billing creates subscription | **Domain events + MassTransit outbox** |
| **C3** | Core database schema knows about module tables | `ApplicationDbContext` has `WebhookEndpoint` query filter | **Per-module DbContext** |
| **C4** | Core UI references module UI components | `TenantDetailPage` imports `SubscriptionTab` | **Slot registry + capability hooks** |

These four patterns are the only tools needed. No runtime plugin loader. No shared mutable registry at runtime. No magic.

---

## 3. Final Module Classification

After honest analysis of dependencies and realistic usage patterns:

### Core (6 features — always present, NOT modules)

These are **infrastructure** for every app built on the boilerplate:

| Feature | Rationale |
|---------|-----------|
| **Files** | Every SaaS uploads files. Tenant branding (logos, favicons) uses it directly. |
| **Notifications** | Every app notifies users. Core user lifecycle (registration, password reset) triggers notifications. |
| **FeatureFlags** | Fundamental infrastructure for plans, gating, A/B tests. Core handlers check flags for quota limits. |
| **ApiKeys** | Core auth middleware validates API keys. Security concern — can't be optional. |
| **AuditLogs** | Compliance requirement for B2B. Core interceptor writes audit entries automatically. |
| **Reports** | Async export (CSV, PDF) needed almost everywhere. Shared `ExportButton` depends on it. |

These features live in `Starter.Application/Features/`, with entities in `Starter.Domain` and tables in `ApplicationDbContext`. The earlier "module" extraction for these 6 is **partially reverted** — their services move back into `Starter.Infrastructure`, their feature folders stay in `features/` as core features.

### Optional modules (3 features — truly removable)

| Module | Realistic removal scenario |
|--------|---------------------------|
| **Billing** | Internal ERP, POS, or self-hosted free tool doesn't have paying customers |
| **Webhooks** | Internal tools, closed systems, no external integrations |
| **ImportExport** | Simple apps without bulk operations (small CRMs, tools, MVPs) |

These get the full modular treatment: own DbContext, own migrations, own feature folder on the frontend, distributable as NuGet/npm packages later.

### Future domain modules (added per project)

- E-commerce (Products, Orders, Cart)
- POS (Sale, Receipt, Till)
- CRM (Leads, Contacts, Deals)
- LMS (Courses, Lessons, Enrollment)
- Anything custom

These are built using the same patterns established for the 3 optional modules above.

---

## 4. Architectural Decisions

Every decision here has trade-offs. This section captures why each choice won over alternatives.

### Decision 0: Rename `Starter.Module.Abstractions` → `Starter.Abstractions`

**Decision:** The existing `Starter.Module.Abstractions` project is renamed to `Starter.Abstractions` as part of Phase 1. Its scope broadens from "module contracts" to "contracts for the entire boilerplate" — it now holds capability interfaces (`IFileService`, `IQuotaChecker`, etc.), domain events, reader services, and the existing `IModule`/`ITenantEntity` types.

**Why:** After the refactor, this project is no longer *just* about modules — it's the contracts package that core, modules, and consumers all reference. The name should reflect that. It also aligns with ABP's naming convention (`Volo.Abp.Abstractions`).

### Decision 1: Per-module DbContext (for optional modules only)

**Decision:** Billing, Webhooks, and ImportExport each get their own `DbContext` class, sharing the same PostgreSQL database and connection string but maintaining their own `__EFMigrationsHistory_{ModuleName}` table.

**Alternatives considered:**

| Option | Verdict |
|--------|---------|
| Per-module DbContext (ABP/OrchardCore pattern) | **Chosen** — true isolation, clean removal |
| Single DbContext with runtime contributors | Rejected — modules still need to compile into core assembly; orphan tables on removal |
| Single DbContext with namespace isolation only | Rejected — doesn't actually isolate anything, just naming convention |
| Per-module physical databases (microservices-style) | Rejected — massive refactor, shared-data pain, transactional consistency issues |

**Trade-offs:**

**Pros:**
- True isolation — each context knows only its own tables
- Independent migrations — no merge conflicts on `ApplicationDbContextModelSnapshot.cs`
- Clean removal — drop module tables without touching others
- Parallel team development — teams own their schemas
- Path to microservices — each context is already isolated

**Cons (and mitigations):**
- **No cross-context EF joins** → Use reader services (`ITenantReader`, `IUserReader`) for cross-context data. This is the biggest discipline change; see Section 5.
- **No EF navigation properties across modules** → Use foreign key IDs + reader lookups, or denormalized snapshots updated via events
- **~500ms startup cost per module** → Acceptable (one-time, cached)
- **~50-100 MB extra memory** → Acceptable on modern servers
- **Transactions across contexts need explicit coordination** → Solved by the transactional outbox (Decision 2)

**Precedent:** ABP Commercial, OrchardCore, and Shopware 6 all use per-module DbContext in production at enterprise scale. It's a solved pattern.

### Decision 2: MassTransit Transactional Outbox

**Decision:** Use MassTransit's built-in `AddEntityFrameworkOutbox` for reliable cross-module event delivery.

**Alternatives considered:**

| Option | Verdict |
|--------|---------|
| MassTransit outbox | **Chosen** — already in project, production-tested, ~10 lines of config |
| Custom outbox implementation | Rejected — reinventing the wheel, ~200 lines to maintain |
| In-process MediatR events (no outbox) | Rejected — no consistency guarantee with per-module DbContexts; handler failures = orphan data |

**Trade-offs:**

**Pros:**
- Guaranteed eventual consistency — event saved atomically with business data
- Automatic retry with exponential backoff
- Request latency decoupled from handler work (background dispatch)
- Production-tested by thousands of apps
- Distributed tracing built-in
- Works with existing MassTransit consumers
- Natural path to RabbitMQ or distributed event bus later

**Cons (and mitigations):**
- ~1-2 second eventual consistency window → Acceptable for SaaS (user doesn't notice)
- Handlers must be idempotent → Standard practice, enforced by MassTransit's `InboxState`
- Extra tables (`OutboxMessage`, `InboxState`) → Auto-managed by MassTransit migrations
- Debugging involves two stack traces → Outbox table itself serves as audit log

**Why not in-process events:** With per-module DbContexts, in-process event handlers that write to a different module's DbContext would be in a different transaction than the command. If the handler fails, you get orphan data (tenant without subscription, for example). The outbox ensures the event is persisted alongside the command, and delivery is retried until the handler succeeds.

### Decision 3: Frontend Slot Registry + Capability Hooks

**Decision:** Core React pages expose typed "slots" where modules register UI components at build time. Cross-feature utilities (hooks, shared components) become "contract components/hooks" with null-safe fallbacks.

**Alternatives considered:**

| Option | Verdict |
|--------|---------|
| Slot registry with typed props per slot | **Chosen** — type-safe, tree-shakable, Shopware-inspired |
| Conditional imports (`activeModules.X ? ... : null`) | Rejected — TypeScript still validates dead imports; fails on module deletion |
| Runtime dynamic `import()` | Rejected — no TS types; runtime errors; bad DX |
| Webpack Module Federation | Rejected — overkill, breaks Vite, complicates dev |
| Core passes children (`<Page tabs={[BillingTab]}/>`) | Rejected — parent still needs to import module components |

**Why it works:**

- **Modules register themselves** via an `index.ts` that calls `registerSlot(...)` before React mounts
- **Core pages render `<Slot id="..." props={...}/>`** — zero knowledge of which modules exist
- **TypeScript enforces prop contracts** via a `SlotMap` type (each slot declares its props)
- **Vite tree-shakes** modules not listed in `modules.config.ts`
- **Removing a module = deleting one import line** + deleting the feature folder

### Decision 4: Reader Services for Cross-Context Data

**Decision:** Create `ITenantReader`, `IUserReader`, `IRoleReader` in `Starter.Abstractions`. These return small read-only DTOs. Modules inject these instead of trying to join to core tables.

**The pattern from ABP Commercial:**

Three layers of cross-context data access, used in combination:

1. **Reader services** (this decision) — live data via DTO, one extra query, simple
2. **Denormalized snapshots** — copy core data into module tables via events (`TenantNameSnapshot`), update on change events
3. **Projection tables** — CQRS read models maintained by event handlers, for heavy reporting

For this refactor, we start with **Layer 1 only**. Upgrade to Layers 2/3 when specific performance needs justify it.

**Why not live EF joins across contexts:**
- Not supported by EF Core (different DbContexts, different model caches)
- Would require dual connection handling in transactions
- Leaks entity graphs across module boundaries (a module that gets `Tenant` can reach to any nav property)
- Forces module to reference core Domain types directly, reintroducing tight coupling

**Trade-offs:**
- **Extra query cost** per request: negligible — readers can batch via `GetManyAsync`
- **Eventual consistency of snapshots** (if used): acceptable for display data
- **More interfaces to maintain** (3 readers): minor, ~50 lines of interface + ~100 lines of impl

---

## 5. How Cross-Module Communication Actually Works

Concrete scenarios from the codebase and proposed new features.

### Scenario A: User registration with quota check

**Context:** `RegisterUserCommandHandler` in core Auth. Before creating the user, it must check if the tenant's plan allows more users.

**Flow:**
1. Handler injects `IQuotaChecker` (capability contract from `Starter.Abstractions`)
2. Calls `quotas.CheckAsync(tenantId, "users.max_count")`
3. If Billing module is installed: `PlanBasedQuotaChecker` reads the tenant's plan, compares to user count
4. If Billing module is NOT installed: `NullQuotaChecker` returns `QuotaResult.Unlimited()` (no limit)
5. Handler proceeds with user creation

**No null checks in the handler.** The null object pattern handles the "capability not installed" case gracefully.

### Scenario B: Tenant registration triggers subscription creation

**Context:** `RegisterTenantCommandHandler` creates a new tenant. Billing module should create a free-plan subscription if installed.

**Flow:**
1. Handler creates tenant entity, saves via `ApplicationDbContext`
2. Publishes `TenantRegisteredEvent(tenantId, tenantName, slug, ownerId, timestamp)` via MassTransit
3. MassTransit outbox writes event to `OutboxMessage` table in the SAME transaction
4. Handler returns success to HTTP client (fast response ~50ms)
5. Background: MassTransit dispatcher reads unprocessed outbox rows
6. If Billing module is installed: `CreateSubscriptionOnTenantRegistered` consumer runs, creates subscription in `BillingDbContext`, marks message processed
7. If Billing is NOT installed: no handler found, message goes unprocessed (MassTransit can be configured to drop unroutable messages or keep them for archaeology)

**No core code knows about Billing.** The event contract lives in `Starter.Abstractions`.

### Scenario C: E-commerce Orders feature with multi-module fan-out

**Context:** A custom `CreateOrderHandler` in a hypothetical `Starter.Module.ECommerce`. When an order is placed:
- Check plan quota (Billing)
- Create the order
- Send notification (Notifications)
- Track usage (Billing)
- Publish webhook (Webhooks)

**Flow — the handler:**

```csharp
public class CreateOrderHandler(
    ECommerceDbContext db,
    IQuotaChecker quotas,           // core capability
    IPublishEndpoint publisher)      // MassTransit
{
    public async Task<Result<Guid>> Handle(CreateOrderCommand cmd, CancellationToken ct)
    {
        var check = await quotas.CheckAsync(cmd.TenantId, "orders.max_per_month", 1, ct);
        if (!check.Allowed)
            return Result.Failure<Guid>($"Plan limit: {check.Current}/{check.Limit}");

        var order = Order.Create(cmd.TenantId, cmd.UserId, cmd.Items);
        db.Orders.Add(order);
        await db.SaveChangesAsync(ct);

        await publisher.Publish(new OrderCreatedEvent(order.Id, cmd.TenantId, cmd.UserId,
            order.Total, order.Currency, order.CreatedAt), ct);

        return Result.Success(order.Id);
    }
}
```

That's the entire handler. Two dependencies. Works with any combination of modules installed.

**Flow — the fan-out:**

Each module that cares registers its own consumer:

```csharp
// Notifications module
public class NotifyOnOrderCreated(INotificationService notifs) : IConsumer<OrderCreatedEvent> {
    public async Task Consume(ConsumeContext<OrderCreatedEvent> ctx) =>
        await notifs.SendAsync(ctx.Message.UserId, NotificationType.OrderPlaced, ...);
}

// Billing module
public class TrackOrderUsage(IUsageTracker usage) : IConsumer<OrderCreatedEvent> {
    public async Task Consume(ConsumeContext<OrderCreatedEvent> ctx) =>
        await usage.IncrementAsync(ctx.Message.TenantId, "orders.created", 1);
}

// Webhooks module
public class DispatchOrderWebhook(IWebhookDispatcher webhooks) : IConsumer<OrderCreatedEvent> {
    public async Task Consume(ConsumeContext<OrderCreatedEvent> ctx) =>
        await webhooks.DispatchAsync(ctx.Message.TenantId, "order.created", ctx.Message);
}
```

Each consumer lives in its own module's assembly. MassTransit discovers them automatically.

**Remove the Notifications module?** Notify consumer disappears. Order creation still works. User just doesn't receive a notification.

**Remove the Webhooks module?** Webhook dispatch stops. Order creation still works. No webhook delivery.

**Add a new handler** (e.g., "send Slack message when high-value order placed")? Create a new consumer in a new module. Zero changes to E-commerce.

---

## 6. Comparison to Enterprise Frameworks

The design above is explicitly inspired by these proven systems:

| System | What we borrow |
|--------|----------------|
| **ABP Commercial** | Module contracts project + per-module DbContext + reader service pattern for cross-context data |
| **OrchardCore** | Feature manager + per-module migrations + `__EFMigrationsHistory_{Module}` pattern |
| **Shopware 6** | Typed slot registry for storefront extensions |
| **Microsoft eShopOnContainers** | Integration events + transactional outbox + MassTransit |
| **NopCommerce** | Event consumers in separate assemblies (but we use MassTransit, not their custom event bus) |

We explicitly **do not** borrow:
- Orchard's "shapes" rendering layer (overkill; React solves this)
- NopCommerce's filesystem-based plugin discovery (fragile)
- ABP's full distributed event bus (unnecessary for single-app scenarios)

---

## 7. Scope

### In-scope (Phases 0-4 of the plan)

- Un-modularize 6 core features (Files, Notifications, FeatureFlags, ApiKeys, AuditLogs, Reports)
- Create `Starter.Abstractions` project with capability contracts, events, reader services
- Null object implementations in core for all capability contracts
- MassTransit outbox configuration
- Refactor cross-module handlers (`RegisterTenantCommandHandler`, etc.) to use events
- Per-module DbContext for Billing, Webhooks, ImportExport
- Frontend slot registry + `<Slot>` component + typed `SlotMap`
- Frontend capability hooks for shared utilities
- Architecture tests (NetArchTest backend, ESLint frontend) to enforce boundaries
- Verification tests including the "killer test" (actually removing a module)

### Out of scope (explicitly deferred)

- NuGet / npm package distribution — defer until Phases 0-4 are proven
- Module CLI scaffolding (`starter module create`) — defer
- Projection tables / CQRS read models — only needed for heavy reporting
- Source code protection / obfuscation — depends on NuGet packaging
- Distributed event bus (RabbitMQ-based cross-service) — in-process outbox is sufficient
- Custom saga orchestration — MassTransit provides this when needed

---

## 8. Success Criteria

After execution is complete:

1. ✅ All 6 core features work identically to before, no regressions
2. ✅ Billing, Webhooks, ImportExport can each be removed individually without breaking the build
3. ✅ Removing a module removes its code, tables, UI, endpoints entirely
4. ✅ Adding a custom domain module (e-commerce, POS) requires zero changes to core files
5. ✅ `dotnet build` + `npm run build` succeed with any combination of modules
6. ✅ Architecture tests enforce the boundaries (NetArchTest backend, ESLint frontend)
7. ✅ Domain events are reliable (transactional outbox, automatic retry)
8. ✅ No cross-module joins needed thanks to reader services + denormalization
9. ✅ Developers can follow the module development guide to create new modules independently

---

## 9. References

- **Plan file:** `C:\Users\saman\.claude\plans\tidy-conjuring-hamster.md` (internal working plan)
- **Developer guide:** `docs/architecture/module-development-guide.md` (how-to for adding/extending)
- **Previous spec:** `docs/superpowers/specs/2026-04-05-module-architecture-design.md` (initial module extraction, superseded by this)
- **ABP Commercial Module System:** https://docs.abp.io/en/abp/latest/Module-Development-Basics
- **OrchardCore Module Manager:** https://docs.orchardcore.net/en/main/docs/reference/modules/
- **MassTransit Outbox:** https://masstransit.io/documentation/configuration/middleware/outbox
- **Shopware 6 App System:** https://developer.shopware.com/docs/guides/plugins/apps/
