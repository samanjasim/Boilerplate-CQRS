# Module Development Guide

**Audience:** Developers working on this boilerplate — whether you're adding a feature to core, extending an existing module, or creating a new domain module.

**Prerequisites:** Read `docs/superpowers/specs/2026-04-07-true-modularity-refactor.md` first to understand the architectural decisions.

---

## Table of Contents

- [A. Is It Core or a Module? — Decision Framework](#a-is-it-core-or-a-module--decision-framework)
- [B. Adding Something to Core](#b-adding-something-to-core)
- [C. Extending an Existing Module](#c-extending-an-existing-module)
- [D. Creating a New Module](#d-creating-a-new-module)
- [E. The "Don't" Rules — Architectural Discipline](#e-the-dont-rules--architectural-discipline)
- [F. Testing a Module](#f-testing-a-module)
- [G. Common Cross-Module Patterns (Cookbook)](#g-common-cross-module-patterns-cookbook)

---

## A. Is It Core or a Module? — Decision Framework

Before adding a new feature, answer these three questions in order. If you answer "yes" to any question that points to **core**, stop there — the feature is core.

### Question 1: Will this feature be in >90% of projects built from this boilerplate?

- **Yes** → Core candidate, continue to Q2
- **No** → Module candidate, continue to Q2

### Question 2: Does removing this feature make sense for any realistic product?

- **Yes, some products would definitely not need it** → Module candidate, continue to Q3
- **No, every product would want it** → **Core**, stop

### Question 3: Does any core code need to directly depend on this feature's entities or services?

- **Yes, and the dependency is unavoidable** (auth middleware, core interceptors, lifecycle handlers) → **Core**, stop
- **Yes, but the dependency could go through events or capability contracts** → Module, but you'll need to design the abstraction
- **No, nothing in core touches it** → **Module**

### Current classification (reference)

| Feature | Q1 (>90%?) | Q2 (Removal realistic?) | Q3 (Unavoidable core deps?) | Verdict |
|---------|-----------|------------------------|----------------------------|---------|
| Files | Yes | No | Yes (tenant branding) | **Core** |
| Notifications | Yes | No | Yes (user lifecycle events) | **Core** |
| FeatureFlags | Yes | No | Yes (plan gating in handlers) | **Core** |
| ApiKeys | Yes | No | Yes (auth middleware) | **Core** |
| AuditLogs | Yes | No | Yes (core interceptor) | **Core** |
| Reports | Yes | No | Yes (shared ExportButton) | **Core** |
| Billing | No | Yes (POS, ERP, internal tools) | No (via events) | **Module** |
| Webhooks | No | Yes (internal/closed systems) | No | **Module** |
| ImportExport | No | Yes (small apps, MVPs) | No | **Module** |
| E-commerce Orders | No | Yes (non-commerce apps) | No | **Module** |
| POS Sales | No | Yes (non-retail apps) | No | **Module** |

### Red flags that mean something should be core

- Core auth/security middleware needs to query it
- Core domain events need to write to its tables
- It's referenced by 3+ other modules (promote to shared abstraction or core)
- It defines types used by core handlers
- Every app built from this boilerplate would include it

### Red flags that mean something should be a module

- It has a specific business domain (e-commerce, POS, CRM, LMS)
- It's used by <70% of likely consumers
- It has external integrations (payment gateway, shipping API, OAuth provider)
- Some customers would pay extra for it, others wouldn't want it at all
- It has its own lifecycle (can be added/removed without affecting core workflows)

---

## B. Adding Something to Core

> **Where contracts live (post-B1c, locked in by `AbstractionsPurityTests`):**
> - **Module-provided capabilities** (interfaces whose implementation lives in an optional module — `IBillingProvider`, `IWebhookPublisher`, `IImportExportRegistry`, `IQuotaChecker`) live in `Starter.Abstractions/Capabilities/`. Core may inject them; a Null Object fallback is registered when the providing module is absent.
> - **Core-provided infrastructure contracts** (interfaces whose implementation always ships with core — `IFileService`, `INotificationService`, `IFeatureFlagService`, `ICacheService`, `IUsageTracker`, `IEmailService`, etc.) stay in `Starter.Application/Common/Interfaces/`. They are always available, so they don't need a Null Object and don't need to be in the pure-contracts project.
>
> The split exists because `Starter.Abstractions` has very narrow allowed dependencies (only `Starter.Domain` plus pure `Microsoft.Extensions.*.Abstractions` packages), while `Starter.Application` may pull in MediatR, EF Core abstractions, etc. See [system-design.md](./system-design.md) for the full project graph.

When you've determined a feature is core, follow these rules:

### File locations

| Type | Location |
|------|----------|
| Entities | `boilerplateBE/src/Starter.Domain/Common/` or a domain-specific subfolder like `Identity/Entities/`, `Tenants/Entities/`, `ApiKeys/Entities/` — ONLY for truly core entities |
| Cross-module integration events (Pattern 2) | `boilerplateBE/src/Starter.Application/Common/Events/` |
| Capability contracts (module-provided) | `boilerplateBE/src/Starter.Abstractions/Capabilities/I{Service}.cs` |
| Infrastructure contracts (core-provided) | `boilerplateBE/src/Starter.Application/Common/Interfaces/I{Service}.cs` |
| Service implementations | `boilerplateBE/src/Starter.Infrastructure/Services/` |
| Command/query handlers | `boilerplateBE/src/Starter.Application/Features/{FeatureName}/` |
| Controllers | `boilerplateBE/src/Starter.Api/Controllers/{FeatureName}Controller.cs` |
| Permissions | `boilerplateBE/src/Starter.Shared/Constants/Permissions.cs` (add inner class) |
| Role mappings | `boilerplateBE/src/Starter.Shared/Constants/Roles.cs` (add to `GetRolePermissions`) |
| DbSets | `ApplicationDbContext` only (`IApplicationDbContext` exposes a small set of typed DbSets, but most access uses `Set<T>()`) |
| EF configurations | `boilerplateBE/src/Starter.Infrastructure/Persistence/Configurations/` |
| Seed data | `boilerplateBE/src/Starter.Infrastructure/Persistence/Seeds/DataSeeder.cs` |

> **Never put a module-owned entity in `Starter.Domain/`.** If a feature is optional enough to become a module, its entities live under `src/modules/Starter.Module.{Name}/Domain/`. `Starter.Domain/` is reserved for types that every build of the boilerplate ships with.
| Frontend code | `boilerplateFE/src/features/{feature-name}/` (no `modules.config.ts` entry — always active) |

### Steps

1. **Design the entity first** — start with the domain model. What does the entity own? What are its invariants?
2. **Define the public interface** — if the feature will ever be called by a module, define `I{Service}` in `Starter.Abstractions/Capabilities/` now. It's cheap to do upfront and impossible to retrofit without breaking changes.
3. **Implement in `Starter.Infrastructure`** — the concrete class lives here. Register in `AddInfrastructure` as the real implementation.
4. **Add handlers in `Starter.Application/Features/`** — following the existing CQRS pattern (Command, Handler, Validator, DTO, Mapper).
5. **Add the controller** with proper `[Authorize(Policy = ...)]` attributes.
6. **Add permissions** to `Permissions.cs` and map to roles in `Roles.cs`.
7. **Add DbSets** to `IApplicationDbContext` and `ApplicationDbContext`, plus the EF configuration.
8. **Seed data** in `DataSeeder` if the feature needs initial data.
9. **Add frontend feature folder** in `features/{name}/` with `api/`, `pages/`, `components/`.
10. **Add routes** in `routes.tsx` (unconditional — it's core).
11. **Add sidebar entry** in `Sidebar.tsx` (unconditional).
12. **Mirror permissions** in `boilerplateFE/src/constants/permissions.ts`.

### The "future-proofing" question

Before finalizing, ask: **Will this feature ever need to be called from a module?**

- If the implementation will always live in core, the interface stays in `Starter.Application/Common/Interfaces/` — that's the natural home for core-provided infrastructure contracts (`IFileService`, `INotificationService`, etc.). Modules can still inject them, because every module project transitively reaches Application via `Starter.Abstractions.Web → Starter.Application`.
- If the implementation might come from a module (i.e. the contract is the API surface that modules implement), put it in `Starter.Abstractions/Capabilities/` and register a Null Object fallback in `Starter.Infrastructure/Capabilities/NullObjects/`.

The first case is by far the more common one. Don't put an interface in `Starter.Abstractions` "just in case" — it constrains what types you can use in the signature (only domain types and primitives are allowed there).

---

## C. Extending an Existing Module

When you need to add functionality to an existing module (e.g., adding a new report type to Reports, or a new webhook event to Webhooks):

### Rules

1. **Stay within the module's project.** Do not add files to core.
2. **Use the module's own DbContext** (if it has one). Never reach into `ApplicationDbContext` for data not owned by your module.
3. **For data owned by core** (users, tenants, roles), use the reader services (`ITenantReader`, `IUserReader`, `IRoleReader`) — never inject `ApplicationDbContext` from a module.
4. **For data owned by another module**, either:
   - Declare a dependency on that module (hard coupling) via `IModule.Dependencies`
   - Subscribe to its domain events (loose coupling — preferred)
   - Use a reader service if the other module exposes one
5. **Permissions** for new functionality go in the module's `Constants/{Module}Permissions.cs`
6. **Role mappings** go in the module's `{Module}Module.GetDefaultRolePermissions()`
7. **Seed data** goes in the module's `{Module}Module.SeedDataAsync()` using its own DbContext
8. **New UI contributions** register to existing slots via the module's `features/{module}/index.ts`
9. **New API endpoints** go in the module's `Api/Controllers/` (auto-discovered by the module loader)
10. **New events the module publishes** go in the module's `Contracts/Events/` and should be consumable by other modules

### Example: adding a new webhook event type

Suppose Billing wants to publish a `subscription.upgraded` webhook when a tenant upgrades their plan.

1. **Webhooks module already exposes** `IWebhookPublisher` as a capability in `Starter.Abstractions/Capabilities/`
2. **Billing module's `ChangePlanCommandHandler`** injects `IWebhookPublisher`
3. After saving the plan change, it calls:
   ```csharp
   await webhooks.PublishAsync("subscription.upgraded", tenantId, new { oldPlan, newPlan }, ct);
   ```
4. If Webhooks module is installed, the publisher hands the payload off to MassTransit and the consumer delivers it
5. If Webhooks module is NOT installed, `NullWebhookPublisher` (registered by core in `AddCapabilities`) silently no-ops with a debug log

**Nothing in Webhooks module changes.** The event type is just a string. The Webhooks module's UI for managing endpoints already allows subscribing to any event type.

> **Preferred alternative:** instead of injecting `IWebhookPublisher` directly, publish a domain event (`PlanChangedEvent`) via `IPublishEndpoint`. The Webhooks module subscribes via `IConsumer<PlanChangedEvent>` and dispatches the webhook itself. This is looser coupling — Billing doesn't even need to know Webhooks exists. See pattern G.5.

### Example: adding a new column to an existing entity

Suppose you want to add `Tags` to the Webhook endpoints.

1. Edit the entity in `Starter.Module.Webhooks/Domain/Entities/WebhookEndpoint.cs`
2. Add a new migration: `dotnet ef migrations add AddTagsToWebhookEndpoint --context WebhooksDbContext --project src/modules/Starter.Module.Webhooks`
3. The migration goes in the module's own migrations folder and its own `__EFMigrationsHistory_Webhooks` table
4. Update DTOs, handlers, frontend components as needed — all within the module
5. **Core is untouched.**

---

## D. Creating a New Module

Follow this checklist when adding a new domain module (e-commerce, CRM, POS, LMS, etc.).

### Backend

1. **Create the project** at `boilerplateBE/src/modules/Starter.Module.{Name}/`

2. **Project references** — reference `Starter.Abstractions.Web` (which transitively pulls in `Starter.Abstractions`, `Starter.Application`, `Starter.Domain`, `Starter.Shared`). If your module ships an `IConsumer<T>` or otherwise needs to register MassTransit infrastructure, also reference `Starter.Abstractions.Messaging` — that's where `IModuleBusContributor` lives (Tier 2.5 Theme 5). Plus standard framework packages your module needs (EF Core, MassTransit, etc.). Do NOT reference `Starter.Infrastructure` or any other module project — those edges break the killer test.

   ```xml
   <ItemGroup>
     <FrameworkReference Include="Microsoft.AspNetCore.App" />
   </ItemGroup>
   <ItemGroup>
     <PackageReference Include="MassTransit" />
     <PackageReference Include="MassTransit.EntityFrameworkCore" />
     <PackageReference Include="Microsoft.EntityFrameworkCore.Relational" />
     <PackageReference Include="Npgsql.EntityFrameworkCore.PostgreSQL" />
   </ItemGroup>
   <ItemGroup>
     <ProjectReference Include="..\..\Starter.Abstractions.Web\Starter.Abstractions.Web.csproj" />
     <!-- Add only if your module ships consumers / needs IModuleBusContributor -->
     <ProjectReference Include="..\..\Starter.Abstractions.Messaging\Starter.Abstractions.Messaging.csproj" />
   </ItemGroup>
   ```

3. **Folder structure** (matches the real Billing/Webhooks/ImportExport modules):
   ```
   Starter.Module.{Name}/
   ├── Application/         # Commands, queries, event handlers, DTOs
   │   ├── Commands/{ActionName}/{ActionName}Command.cs + Handler.cs + Validator.cs
   │   ├── Queries/{QueryName}/{QueryName}Query.cs + Handler.cs
   │   ├── EventHandlers/   # IConsumer<{EventType}> classes (intra-module OR core integration events)
   │   ├── Abstractions/    # Module-internal interfaces (e.g. IImportRowProcessor) — NOT capability contracts
   │   └── DTOs/
   ├── Constants/
   │   └── {Name}Permissions.cs
   ├── Controllers/         # Auto-discovered via AddApplicationPart in Program.cs
   │   └── {Name}Controller.cs
   ├── Domain/              # Module-owned types — NEVER put these in Starter.Domain
   │   ├── Entities/        # Your aggregate roots, child entities
   │   ├── Enums/           # Module-internal enums (contract-adjacent enums go in Starter.Abstractions.Capabilities)
   │   ├── Errors/          # {Name}Errors.cs static class
   │   └── Events/          # Intra-module domain events (raised via AggregateRoot.RaiseDomainEvent)
   ├── Infrastructure/
   │   ├── Configurations/  # IEntityTypeConfiguration<T> classes
   │   ├── Persistence/
   │   │   └── {Name}DbContext.cs
   │   └── Services/        # Module-private services (e.g., MockBillingProvider) + IUsageMetricCalculator impl
   └── {Name}Module.cs      # implements IModule
   ```

   **Where module-owned types live — locked in by the module type relocation refactor:**
   - Entities, module-private enums, errors, and intra-module domain events → inside the module's `Domain/` folder. `Starter.Domain/` contains ONLY core entities (User, Role, Tenant, FileMetadata, ApiKey, FeatureFlag, etc.).
   - Contract-adjacent value types (enums or records used in a capability contract signature like `IBillingProvider` or `FieldDefinition`) → `Starter.Abstractions/Capabilities/`. Examples: `BillingInterval`, `FieldType`, `FieldDefinition`, `EntityImportExportDefinition`.
   - Module-internal interfaces that aren't capabilities (e.g. `IImportRowProcessor` implemented by row processors inside the module) → the module's `Application/Abstractions/` folder.
   - Cross-module integration event types (published by core and consumed asynchronously via MassTransit outbox) → `Starter.Application/Common/Events/`. This is for Pattern-2 events only; Pattern-1 direct capability calls don't need a new event type.

   See [cross-module-communication.md](./cross-module-communication.md) for the full decision tree on where each kind of type belongs.

4. **Create the DbContext** implementing `IModuleDbContext`:
   ```csharp
   using System.Reflection;
   using Microsoft.EntityFrameworkCore;
   using Starter.Abstractions.Modularity;
   using Starter.Application.Common.Interfaces;

   public sealed class {Name}DbContext : DbContext, IModuleDbContext
   {
       private readonly ICurrentUserService? _currentUserService;

       // EF evaluates this per query; must be a property for parameterization
       private Guid? CurrentTenantId => _currentUserService?.TenantId;

       public {Name}DbContext(
           DbContextOptions<{Name}DbContext> options,
           ICurrentUserService? currentUserService = null)   // optional so EF design-time tooling works
           : base(options)
       {
           _currentUserService = currentUserService;
       }

       public DbSet<Product> Products => Set<Product>();
       // ... other entities

       protected override void OnModelCreating(ModelBuilder modelBuilder)
       {
           base.OnModelCreating(modelBuilder);

           // No MassTransit outbox tables here — all outbox bookkeeping lives on
           // ApplicationDbContext. Modules publishing events use IPublishEndpoint;
           // the events land in the core outbox transactionally.

           modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());

           // Tenant query filter for any tenant-scoped entities
           modelBuilder.Entity<Product>().HasQueryFilter(p =>
               CurrentTenantId == null || p.TenantId == CurrentTenantId);
       }
   }
   ```

5. **Register DbContext** in `{Name}Module.ConfigureServices`:
   ```csharp
   services.AddDbContext<{Name}DbContext>(options =>
   {
       options.UseNpgsql(
           configuration.GetConnectionString("DefaultConnection"),
           npgsqlOptions =>
           {
               npgsqlOptions.MigrationsHistoryTable("__EFMigrationsHistory_{Name}");
               npgsqlOptions.MigrationsAssembly(typeof({Name}DbContext).Assembly.FullName);
               npgsqlOptions.EnableRetryOnFailure(
                   maxRetryCount: 3,
                   maxRetryDelay: TimeSpan.FromSeconds(10),
                   errorCodesToAdd: ["40001"]);
           });
   });
   ```

6. **Implement `IModule`:**
   - `ConfigureServices(IServiceCollection, IConfiguration)` — register DbContext + capability implementations + module-private services. **Do not call `services.AddMassTransit` here** — the host already registers the bus once in `Starter.Infrastructure`; module-side `AddMassTransit` is a duplicate registration that breaks the outbox.
   - `GetPermissions()` — yield `(name, description, module)` tuples for the seeder
   - `GetDefaultRolePermissions()` — map permissions to system roles for the seeder
   - `MigrateAsync(IServiceProvider, CancellationToken)` — call `Database.MigrateAsync()` on the module's own DbContext (the default interface implementation is a no-op, override only if your module has a DbContext)
   - `SeedDataAsync(IServiceProvider)` — seed initial data using the module's own DbContext

   Both `MigrateAsync` and `SeedDataAsync` are orchestrated by `DataSeeder.SeedAsync` in `Starter.Infrastructure.Persistence.Seeds`. The order is: core migrate → core seeds → for each module (migrate → seed). Both are gated by `DatabaseSettings:SeedDataOnStartup`.

6a. **Implement `IModuleBusContributor` (only if you ship `IConsumer<T>` or need a per-DbContext outbox).** As of Tier 2.5 Theme 5, the host no longer scans module assemblies for consumers — modules opt in here. Add the contract on the same module class:

   ```csharp
   using MassTransit;
   using Starter.Abstractions.Modularity;

   public sealed class {Name}Module : IModule, IModuleBusContributor
   {
       // ...

       public void ConfigureBus(IBusRegistrationConfigurator bus)
       {
           bus.AddConsumers(typeof({Name}Module).Assembly);

           // Only register a per-DbContext outbox here if your module publishes events
           // from inside its own DbContext transaction — most modules use
           // IIntegrationEventCollector against ApplicationDbContext instead. See
           // WorkflowModule for the per-DbContext outbox example.
       }
   }
   ```

   The architecture test `ModuleRegistryTests.Modules_with_MassTransit_consumers_implement_IModuleBusContributor` fails the build if a module ships an `IConsumer<T>` without declaring the contract — those consumers would otherwise be dead at runtime.

7. **For cross-module needs**, inject from core abstractions:
   - **Tenant/user/role data** → `ITenantReader`, `IUserReader`, `IRoleReader`
   - **Sync quota checks** → `IQuotaChecker`
   - **File operations** → `IFileService`
   - **Notifications** → `INotificationService`
   - **Publishing events** → `IPublishEndpoint` from MassTransit
   - **Subscribing to events** → create `IConsumer<{Event}>` in `Application/EventHandlers/`

8. **Add `ProjectReference`** in `Starter.Api.csproj` and register in `.sln`.

9. **Add the catalog entry** in `modules.catalog.json` (repo root) and run `npm run generate:modules`. This regenerates four artifacts from a single source of truth:

   | Artifact | Path | Consumed by |
   |----------|------|-------------|
   | `ModuleRegistry.g.cs` | `boilerplateBE/src/Starter.Api/Modularity/` | `Program.cs` (replaces reflection-based `ModuleLoader.DiscoverModules()` in production startup) |
   | `modules.generated.ts` | `boilerplateFE/src/config/` | `modules.config.ts` re-exports it; main.tsx + sidebar + routes consume the public surface |
   | `modules.config.dart` | `boilerplateMobile/lib/app/` | mobile shell |
   | `eslint.config.modules.json` | `boilerplateFE/` | `eslint.config.js` reads it for the `no-restricted-imports` patterns |

   CI fails on drift via the `modules-codegen-drift` job in `.github/workflows/modularity.yml`. Never hand-edit the generated files — change the catalog and regenerate.

### Frontend

1. **Create the feature folder** at `boilerplateFE/src/features/{feature-name}/`

2. **Folder structure:**
   ```
   features/{feature-name}/
   ├── api/              # TanStack Query hooks, API calls
   ├── components/       # Components (including slot entries)
   ├── pages/            # Top-level pages
   ├── hooks/            # Feature-specific hooks
   ├── types/            # TypeScript types
   ├── utils/            # Helpers
   └── index.ts          # Module registration
   ```

3. **`index.ts`** exports a module object with a `register()` function. The shape must match the `AppModule` interface declared in `src/config/modules.config.ts` (`{ name: string; register(): void }`):
   ```typescript
   import { lazy } from 'react';
   import { registerSlot, registerCapability } from '@/lib/extensions';

   export const myModule = {
     name: 'myModule',
     register() {
       // Register UI slot entries — core <Slot id="..."/> picks these up
       registerSlot('tenant-detail-tabs', {
         id: 'myModule.myTab',
         module: 'myModule',
         order: 50,
         label: () => 'My Tab',
         permission: 'MyModule.View',
         component: lazy(() => import('./components/MyTab')),
       });

       // Register capability implementations (if any)
       registerCapability('useMyHook', useMyHookImpl);
     },
   };
   ```

4. **Do NOT hand-edit `boilerplateFE/src/config/modules.config.ts` or `modules.generated.ts`.** Instead, add your module to `modules.catalog.json` (repo root) and run `npm run generate:modules`. The generator emits both files: `modules.generated.ts` holds the import + `enabledModules` array + `ModuleName` union + `activeModules` literal + `registerAllModules()`; `modules.config.ts` is a thin re-export shim that callers (`main.tsx`, sidebar, routes) import from. Both `activeModules` flags and the `enabledModules` array stay in sync because both derive from a single `enabledIds` set inside the generated file. `scripts/rename.ps1 -Modules None` regenerates these to a no-modules build.

5. **If your module adds new slots** that don't exist yet, extend `SlotMap` in `src/lib/extensions/slot-map.ts`. Each slot id has a typed prop contract:
   ```typescript
   export interface SlotMap {
     'tenant-detail-tabs': { tenantId: string; tenantName: string };
     'users-list-toolbar': { onRefresh: () => void };
     'my-new-slot': { /* the props your slot host will pass */ };
   }
   ```
   Core then renders `<Slot id="my-new-slot" props={{...}}/>` somewhere; your module's `registerSlot('my-new-slot', { component: ... })` entry receives those props.

6. **Routes** — add the module's pages to `boilerplateFE/src/routes/routes.tsx` using the conditional `lazy`/`NullPage` pattern that mirrors how Billing/Webhooks/ImportExport pages are wired today:
   ```typescript
   const MyModulePage = activeModules.myModule
     ? lazy(() => import('@/features/my-feature/pages/MyModulePage'))
     : NullPage;
   ```
   This is necessary because TypeScript's static `lazy(() => import('@/features/my-feature/...'))` would fail to resolve when the feature folder is deleted in a `-Modules None` build. The `rename.ps1` script also rewrites these dead lazy imports to point at `NullPage` automatically when excluding the module — see Step 6 of the script.

### Module manifest

Add an entry to `modules.catalog.json`. The catalog is the single source of truth — `npm run generate:modules` reads it to emit `ModuleRegistry.g.cs`, `modules.generated.ts`, `modules.config.dart`, and `eslint.config.modules.json`. The `rename.ps1` script also reads it when generating a fresh app — entries with `required: false` are honored by the `-Modules` parameter (`All`/`None`/comma-separated names).

```json
"myModule": {
  "displayName": "My Module",
  "version": "1.0.0",
  "supportedPlatforms": ["backend", "web"],   // include "mobile" if you ship a Dart counterpart
  "backendModule": "Starter.Module.MyModule",
  "frontendFeature": "my-feature",
  "mobileModule": "MyModule",                 // optional, omit if no mobile entry
  "mobileFolder": "my_module",                // optional, omit if no mobile entry
  "configKey": "myModule",
  "required": false,
  "dependencies": [],
  "description": "Brief description, ideally mentioning which slot/capability the module participates in."
}
```

After editing, run `npm run generate:modules` to regenerate the four bootstrap artifacts. CI's `modules-codegen-drift` job fails the PR if you skip this step.

Only optional modules live in `modules.catalog.json`. Core features such as Files, Notifications, FeatureFlags, ApiKeys, AuditLogs, and Reports ship with every build and are not in this catalog.

### First-run checklist

After creating the module:

1. ☐ `dotnet build` succeeds with 0 warnings, 0 errors
2. ☐ `npm run build` succeeds
3. ☐ `dotnet test --filter AbstractionsPurityTests` passes — confirms no forbidden references leaked into the contracts project
4. ☐ Backend startup logs show your migrations applied (only if your module has its own DbContext): look for the `__EFMigrationsHistory_MyModule` table in PostgreSQL
5. ☐ Backend startup logs show `Seeding module Starter.Module.MyModule`
6. ☐ Module's controllers appear in Swagger at `/swagger`
7. ☐ Module's permissions appear in the Roles page in the UI
8. ☐ Module's UI slot entries render in the appropriate core pages (e.g. tab button on `/tenants/{id}` if registered to `tenant-detail-tabs`)
9. ☐ **Killer test:** generate a fresh app with the module excluded — `pwsh ./scripts/rename.ps1 -Name "Smoke" -OutputDir "c:/tmp" -Modules "None"` (or omit your module from a comma-separated list). Then `cd c:/tmp/Smoke/Smoke-BE && dotnet build` and `cd c:/tmp/Smoke/Smoke-FE && npm install && npm run build`. Both must succeed.

If the killer test fails, you have a hidden cross-dependency. Find it before merging. The most common causes are: a core file importing from `@/features/{your-module}/...` (caught by the ESLint rule), or a core handler injecting one of your module's concrete types instead of going through a capability contract / event.

---

## E. The "Don't" Rules — Architectural Discipline

These rules prevent the boilerplate from drifting back into a monolith. Some are enforced by tests (`AbstractionsPurityTests`) or lint (`no-restricted-imports`); the rest rely on code review.

1. **Don't import from module folders in core code.** Ever. Core files never write `using Starter.Module.*` or `import '@/features/{moduleFeature}'`. The frontend ESLint rule `no-restricted-imports` blocks this for the 3 module folders.

2. **Don't inject `ApplicationDbContext` from a module.** Use the module's own DbContext. When you need core data (tenant, user, role) inside a module, use the reader services (`ITenantReader`, `IUserReader`, `IRoleReader`) — not direct DbContext injection.

3. **Don't add module-specific typed DbSets to `IApplicationDbContext`.** That interface only exposes the small set of core entities used by `Set<T>()` callers. Modules query their own DbContexts.

4. **Don't reference another module's concrete types.** Use events, capability contracts in `Starter.Abstractions/Capabilities/`, or shared abstractions. If two modules constantly need each other's types, the abstraction should move down to core.

5. **Don't do cross-module `Include()` or `Join()`.** Each module's entities live in its own DbContext, so cross-context joins aren't even compileable. Use reader services or denormalized snapshots maintained via events. Both `GetAllSubscriptionsQueryHandler` and `GetAllWebhookEndpointsQueryHandler` are reference implementations of the "fetch ids first, paginate locally, back-fill projection" pattern.

6. **Don't handle cross-module side effects synchronously.** Publish a domain event via `IPublishEndpoint` and let the other module subscribe via `IConsumer<TEvent>`. Synchronous calls couple you to the other module's availability and break the killer test.

7. **Don't put feature-specific constants in `Starter.Shared/Constants/`.** Module permissions go in the module's own `Constants/{Module}Permissions.cs`. Only constants used by core OR by ≥2 modules belong in `Starter.Shared`.

8. **Don't hardcode `activeModules.X` checks in core pages for slot-driven content.** Use `<Slot>` instead, and gate visibility with `hasSlotEntries('slot-id')` if the host needs to know whether anything is registered. The whole point of slots is that core doesn't know which modules are installed. (Sidebar/routes still legitimately reference `activeModules.X` because they're the module-loading bootstrap layer — `rename.ps1` is aware of those callsites.)

9. **Don't call `services.AddScoped<IBillingProvider, ...>` (or any module-provided capability) in core.** Core registers Null Object fallbacks via `TryAddScoped`/`TryAddSingleton` in `Starter.Infrastructure.DependencyInjection.AddCapabilities()`. Modules register their real implementations later via `AddScoped<IBillingProvider, MockBillingProvider>()` in `BillingModule.ConfigureServices`, which replaces the null object because module `ConfigureServices` runs after `AddInfrastructure`. Core-provided contracts (`IFileService`, `INotificationService`, `IFeatureFlagService`, etc.) register their real implementations in core normally — there's no Null Object for those.

10. **Don't add MassTransit outbox tables to a module's DbContext.** All outbox traffic — including events published from module DbContext scopes — flows through the single outbox registered against `ApplicationDbContext` in `Starter.Infrastructure/DependencyInjection.cs`. Module `OnModelCreating` should NOT call `AddInboxStateEntity()`/`AddOutboxMessageEntity()`/`AddOutboxStateEntity()`. Consolidating in one context keeps retry + dedup bookkeeping simple.

11. **Don't share entities between modules.** If two modules need the same concept, either:
    - It's actually core (promote it to `Starter.Domain`)
    - Each module has its own copy with module-specific concerns

12. **Don't skip the "killer test"** before merging a module change. Run `pwsh ./scripts/rename.ps1 -Name "Smoke" -OutputDir "c:/tmp" -Modules "None"`, then build the generated backend and frontend. Both must succeed. This is the only reliable way to catch hidden cross-dependencies.

13. **Don't use `IgnoreQueryFilters()` in modules without a comment explaining why.** Tenant isolation is enforced by query filters; bypassing them is a security concern. The legitimate uses are platform-admin queries, uniqueness checks across all tenants, and inbox/outbox bookkeeping.

14. **Don't create an `INotificationHandler<T>` in one module that consumes a domain event raised by another module.** This creates a hidden compile-time coupling — the consuming module gains a `using` statement pointing at the other module's event type, and the killer test fails when the publishing module is removed. The former `WebhookBillingEventHandler` was the last example of this anti-pattern in the codebase; it was deleted during the module type relocation refactor. See [cross-module-communication.md §6](./cross-module-communication.md#6-anti-patterns) for the full rationale. The correct alternatives are Pattern 1 (capability call — publishing module calls `IWebhookPublisher.PublishAsync` directly) or Pattern 2 (integration event in `Starter.Application/Common/Events/` consumed via `IConsumer<T>`).

15. **Don't put module-owned types in `Starter.Domain/`.** Entities, module-private enums, errors, and intra-module domain events all live inside the module's own `Domain/` folder. `Starter.Domain/` is reserved for types that every build of the boilerplate ships with (User, Role, Tenant, FileMetadata, ApiKey, FeatureFlag, etc.). If you find yourself adding a `Starter.Domain/{YourModule}/` folder, stop — that's always wrong.

---

## F. Testing a Module

### Architecture tests (already in place)

`boilerplateBE/tests/Starter.Api.Tests/Architecture/AbstractionsPurityTests.cs` enforces the dependency rules for `Starter.Abstractions` via reflection (`Assembly.GetReferencedAssemblies()`). It runs on every `dotnet test` and fails CI if a forbidden reference creeps in.

When you add a new architectural rule (e.g. "modules must not reference each other"), add a new test alongside `AbstractionsPurityTests` in the same folder.

### Unit tests

Per-module test projects don't exist yet in this boilerplate. When a module grows enough to need them, add `boilerplateBE/tests/Starter.Module.{Name}.Tests/` and follow the existing `Starter.Api.Tests` setup (xUnit, Moq, FluentAssertions). The pattern stays the same: test handlers in isolation with mocked dependencies, test domain entities' invariants directly.

### Integration tests

For a module that needs an integration test, use a test fixture that spins up **only the module's DbContext** and the capability contracts it depends on — not the full app. Use an in-memory SQLite or Testcontainers for real PostgreSQL. Mock cross-module capabilities (`IQuotaChecker` → unlimited, `IWebhookPublisher` → no-op) — those are the same Null Object types core uses for module-absent runs.

### Regression test (the killer test)

**Every module change must pass this test before merging:**

```bash
pwsh ./scripts/rename.ps1 -Name "Smoke" -OutputDir "c:/tmp" -Modules "None"
cd c:/tmp/Smoke/Smoke-BE && dotnet build
cd c:/tmp/Smoke/Smoke-FE && npm install && npm run build
```

Both `dotnet build` and `npm run build` MUST succeed with the module excluded. If either fails, there's a hidden cross-dependency introduced by your change. The failure usually points at the offending file directly:
- Backend: a `using Starter.Module.X` somewhere it shouldn't be, or a handler injecting a module type
- Frontend: an `import '@/features/{module}/...'` from a core file (the ESLint rule `no-restricted-imports` should also be flagging this)

Restore by deleting the generated `c:/tmp/Smoke/` directory. Consider running this in CI on every PR.

### Manual smoke test

With the module disabled (use the no-modules build above):

1. Start the backend — no errors in logs, `__EFMigrationsHistory` exists but no `__EFMigrationsHistory_{Module}` is created (because the module isn't loaded)
2. Start the frontend — no TypeScript errors
3. Log in — works
4. Navigate through all core pages — all render
5. Module's UI is absent (no tab in `tenant-detail-tabs`, no toolbar button in `users-list-toolbar`, no sidebar entry, no routes)
6. Module's API endpoints return 404 (the controller class doesn't exist in this build)
7. Features that depend on the module degrade gracefully via Null Objects (e.g. `IQuotaChecker` returns unlimited; `IBillingProvider` writes throw `CapabilityNotInstalledException` which the global middleware maps to 501)

Re-enable the module by regenerating with `-Modules All`:

1. Restart — backend startup logs show `Seeding module Starter.Module.MyModule`
2. Module's tables appear in PostgreSQL alongside its own `__EFMigrationsHistory_MyModule`
3. Module's UI slot entries appear
4. Module's API returns 200

---

## G. Common Cross-Module Patterns (Cookbook)

A reference of how to do common things correctly.

### G.1 "My module needs to know the tenant's name"

Inject `ITenantReader`, call `GetAsync(tenantId)`. Returns `TenantSummary` DTO with id, name, slug, status. One query per request, batchable via `GetManyAsync`.

```csharp
public class MyHandler(ITenantReader tenants) {
    public async Task Handle(MyCommand cmd, CancellationToken ct) {
        var tenant = await tenants.GetAsync(cmd.TenantId, ct);
        if (tenant is null) return Result.Failure("Tenant not found");
        // ... use tenant.Name, tenant.Slug, etc.
    }
}
```

### G.2 "My module needs to react when a tenant is registered"

Create an `IConsumer<TenantRegisteredEvent>` in `Application/EventHandlers/` and make sure your module class implements `IModuleBusContributor` calling `bus.AddConsumers(typeof(MyModule).Assembly)` (Tier 2.5 Theme 5 — the host no longer scans module assemblies). MassTransit then runs the consumer asynchronously via the outbox with the default retry policy.

```csharp
public class DoSomethingWhenTenantRegistered(MyDbContext db)
    : IConsumer<TenantRegisteredEvent>
{
    public async Task Consume(ConsumeContext<TenantRegisteredEvent> ctx) {
        // Your logic here. Runs in a background scope with its own transaction.
        db.MyTable.Add(new MyEntity(ctx.Message.TenantId, ...));
        await db.SaveChangesAsync(ctx.CancellationToken);
    }
}
```

### G.3 "My module has a plan-limited action (e.g., max orders per month)"

Inject `IQuotaChecker`, call `CheckAsync(tenantId, "my_metric")`. Returns `QuotaResult`. If Billing is installed, this reads plan limits. If not, it's unlimited.

```csharp
public class CreateOrderHandler(IQuotaChecker quotas, MyDbContext db) {
    public async Task<Result<Guid>> Handle(CreateOrderCommand cmd, CancellationToken ct) {
        var check = await quotas.CheckAsync(cmd.TenantId, "orders.max_per_month", 1, ct);
        if (!check.Allowed)
            return Result.Failure<Guid>($"Plan limit: {check.Current}/{check.Limit}");

        // ... create order
    }
}
```

### G.4 "My module needs to increment usage tracking"

Inject `IUsageTracker`, call `IncrementAsync(tenantId, metric)`. Null implementation no-ops when Billing is not installed.

```csharp
await usage.IncrementAsync(cmd.TenantId, "orders.created", 1, ct);
```

### G.5 "My module needs to publish a webhook when something happens"

**Default: inject `IWebhookPublisher` and call it directly (Pattern 1).** This is the canonical use case for a capability call.

```csharp
public class CreateOrderHandler(MyDbContext db, IWebhookPublisher webhookPublisher) {
    public async Task<Result<Guid>> Handle(CreateOrderCommand cmd, CancellationToken ct) {
        // ... save the order via db ...
        await db.SaveChangesAsync(ct);

        await webhookPublisher.PublishAsync(
            eventType: "order.created",
            tenantId: cmd.TenantId,
            data: new { orderId = order.Id, total = order.Total },
            cancellationToken: ct);

        return Result.Success(order.Id);
    }
}
```

`IWebhookPublisher` lives in `Starter.Abstractions.Capabilities` and is registered with a `NullWebhookPublisher` fallback in core. When the Webhooks module is installed, its real `WebhookPublisher` replaces the fallback; when not, the call is a silent no-op. Your module has zero compile-time reference to the Webhooks module either way.

**Why not use `IPublishEndpoint.Publish` + integration event instead?** Because that would require adding a new event type to `Starter.Application/Common/Events/`, which means core grows by one type per module-module interaction. Pattern 1 keeps the growth inside your own module — each new event is just another string and another `PublishAsync` call.

**When you SHOULD use Pattern 2 (integration event) instead:** if three or more modules need to react asynchronously to the same trigger — e.g. an analytics module AND an audit module AND a webhook module all want to know about "order created". At that point the event type earns its place in `Common/Events/` and every interested module becomes an `IConsumer<OrderCreatedEvent>`. See [cross-module-communication.md §4](./cross-module-communication.md#4-using-pattern-2-to-extend-pattern-1--the-hybrid-case) for the migration pattern.

**Never** create an `INotificationHandler<T>` in the Webhooks module that consumes your module's domain event type — that's the anti-pattern from Section E, rule 14.

### G.6 "My module needs to notify the user"

**Default: inject `INotificationService` directly.** Notifications is core, so the service is always available — no Null Object needed.

```csharp
public class MyHandler(INotificationService notifications) {
    public async Task Handle(MyCommand cmd, CancellationToken ct) {
        await notifications.SendAsync(
            userId: cmd.UserId,
            type: NotificationType.OrderPlaced,
            message: "Your order has been placed",
            cancellationToken: ct);
    }
}
```

Use Pattern 2 (integration event) only if multiple modules should react to the same trigger with their own notifications — in that case, publish `OrderCreatedEvent` and let each module's `IConsumer<OrderCreatedEvent>` send its own notification.

### G.7 "My module needs to upload a file"

Inject `IFileService`, call `UploadAsync(stream, metadata)`. Always available (Files is core).

```csharp
public class MyHandler(IFileService files) {
    public async Task Handle(MyCommand cmd, CancellationToken ct) {
        var fileRef = await files.UploadAsync(new UploadRequest {
            Stream = cmd.FileStream,
            FileName = cmd.FileName,
            ContentType = cmd.ContentType,
            Category = FileCategory.Invoice,
        }, ct);
        // Store fileRef.Id in your entity
    }
}
```

### G.8 "My module has a UI that should appear inside a core page"

Register a slot entry in the module's `features/{name}/index.ts`. Core renders `<Slot id="..." props={...}/>` which picks up your entry automatically.

```typescript
// features/my-module/index.ts
registerSlot('tenant-detail-tabs', {
  id: 'myModule.myTab',
  module: 'myModule',
  order: 50,
  label: () => 'My Tab',
  permission: 'MyModule.View',
  component: lazy(() => import('./components/MyTab')),
});
```

### G.9 "My module needs a hook that core code will use"

Register a capability via `registerCapability('useMyHook', impl)`. Core provides a stub that returns a safe default.

```typescript
// In core (defines the contract)
// boilerplateFE/src/lib/extensions/capabilities.ts
interface Capabilities {
  useMyHook?: (key: string) => MyResult;
}

export function useMyHook(key: string): MyResult {
  return caps.useMyHook?.(key) ?? defaultResult;
}

// In the module (provides implementation)
registerCapability('useMyHook', (key) => { /* real impl */ });
```

### G.10 "My module needs to store something in the database"

Add the entity to **your module's DbContext**. Add a migration for your module. Never touch `ApplicationDbContext`.

```bash
dotnet ef migrations add AddMyEntity \
  --context MyModuleDbContext \
  --project src/modules/Starter.Module.MyModule \
  --startup-project src/Starter.Api
```

The migration goes in your module's own `Infrastructure/Persistence/Migrations/` folder and updates `__EFMigrationsHistory_MyModule`.

### G.11 "My module needs to display data from another module"

Don't query the other module's DbContext directly. Either:

**Option A: Subscribe to events and keep a local copy**
- Listen to the other module's events
- Store the data you need in your own tables (denormalized snapshot)
- Update on change events

**Option B: Expose a reader service from the other module**
- Other module defines `IOtherModuleReader` in `Starter.Abstractions`
- Other module provides implementation
- Your module injects and calls the reader

**Option C: Declare a hard dependency**
- Your module's `IModule.Dependencies` lists the other module
- You can reference its `Contracts/` project directly
- Only do this for very tight coupling (rare)

### G.12 "Two modules need to cooperate on a workflow"

Use the **orchestration via events** pattern:

1. Module A publishes a started event (`WorkflowStartedEvent`)
2. Module B listens, does its work, publishes a progress event
3. Module A listens to progress, decides what's next
4. Each module's state is in its own DbContext
5. MassTransit's saga support can help for complex workflows

If the workflow is truly cross-module and complex, consider whether it should be a new module itself (the "workflow module") that orchestrates the others.

### G.13 "My module needs to react to something that happens in another module"

**Don't subscribe to the other module's domain event.** That's the anti-pattern from Section E rule 14 — it creates a hidden compile-time coupling between the two modules.

Instead, look at who's doing the triggering and pick one of two approaches:

**Option A — the other module calls you via a capability (Pattern 1).** The natural direction: the publishing module reaches out through a capability contract you expose in `Starter.Abstractions.Capabilities`. Example: the Billing module's `ChangePlanCommandHandler` calls `IWebhookPublisher.PublishAsync("subscription.changed", ...)` — the Webhooks module's real implementation is what actually dispatches. Webhooks has zero knowledge of Billing.

To use this approach for a new module: define an interface in `Starter.Abstractions.Capabilities/` that represents the action you want to perform, implement it in your module, register it in `ConfigureServices`, and ask the publishing module to inject and call it. Core provides a Null Object fallback so the publishing module works whether you're installed or not.

**Option B — both modules subscribe to a core integration event (Pattern 2).** If the trigger is owned by core (not by another module), or if three-plus modules all want to react to the same trigger, define the event type in `Starter.Application/Common/Events/` and have each module provide its own `IConsumer<T>`. Example: the Billing module's `CreateFreeTierSubscriptionOnTenantRegistered` consumes `TenantRegisteredEvent` (published by `RegisterTenantCommandHandler` in core).

**The forbidden approach:** `IConsumer<SubscriptionChangedEvent>` in Module A, where `SubscriptionChangedEvent` is defined inside Module B. The `using Starter.Module.B.Domain.Events;` is the smell. See [cross-module-communication.md](./cross-module-communication.md) for the full decision tree.

---

## Appendix: Architectural Tests in Place

These tests enforce the rules automatically. If you add new rules, add corresponding tests in the same folder.

### Backend — `tests/Starter.Api.Tests/Architecture/AbstractionsPurityTests.cs`

Uses pure reflection (`Assembly.GetReferencedAssemblies()`) to assert that `Starter.Abstractions` only references its allowlisted dependencies. No NetArchTest package needed.

```csharp
private static readonly string[] ForbiddenAssemblyPrefixes =
[
    "Starter.Domain",
    "Starter.Application",
    "Starter.Infrastructure",
    "Starter.Shared",
    "Starter.Abstractions.Web",
    "Microsoft.AspNetCore",
    "Microsoft.EntityFrameworkCore",
    "MassTransit",
];

private static readonly string[] AllowedAssemblyPrefixes =
[
    "Starter.Abstractions",
    "System",
    "netstandard",
    "Microsoft.Extensions.Configuration.Abstractions",
    "Microsoft.Extensions.DependencyInjection.Abstractions",
    "Microsoft.Extensions.Primitives",
];

[Fact]
public void Starter_Abstractions_must_not_depend_on_forbidden_assemblies()
{
    var abstractionsAssembly = typeof(ICapability).Assembly;
    var referencedNames = abstractionsAssembly.GetReferencedAssemblies();

    var violations = referencedNames
        .Where(r => ForbiddenAssemblyPrefixes.Any(p => r.Name?.StartsWith(p, StringComparison.Ordinal) == true))
        .Select(r => r.Name)
        .ToList();

    Assert.True(violations.Count == 0, /* ... */);
}
```

A second test asserts that every reference must be on the allowlist — so adding a new dependency is a deliberate, visible change. To allow a new package, edit the `AllowedAssemblyPrefixes` array. To forbid a new one, edit `ForbiddenAssemblyPrefixes`.

Future tests worth adding (not yet in place):
- `Starter.Application` must not reference `Starter.Module.*` or `Starter.Abstractions.Web`
- `Starter.Infrastructure` must not reference `Starter.Module.*`
- `Starter.Domain` must not reference any other Starter project

### Frontend — `boilerplateFE/eslint.config.js`

The flat config blocks core files from importing module folders, including `import type` (intentional — type coupling is still coupling). As of Tier 2.5 Theme 5, the restricted patterns and allowlist files are **generated** from `modules.catalog.json` into `eslint.config.modules.json`; the ESLint config reads the JSON at startup so adding/removing a module re-flows the rule automatically.

```javascript
import { readFileSync } from 'node:fs'
import { fileURLToPath } from 'node:url'
import { dirname, resolve } from 'node:path'

const __dirname = dirname(fileURLToPath(import.meta.url))
const moduleConfig = JSON.parse(
  readFileSync(resolve(__dirname, 'eslint.config.modules.json'), 'utf8'),
)

export default defineConfig([
  {
    files: ['**/*.{ts,tsx}'],
    rules: {
      'no-restricted-imports': ['error', {
        patterns: [{
          group: moduleConfig.restrictedPatterns,
          message: 'Do not import optional module features from core. Register routes, nav, slots, and capabilities through src/config/modules.config.ts and src/lib/modules instead.',
        }],
      }],
    },
  },
  {
    // Allowlist: the module folders themselves, plus the generated bootstrap config.
    files: moduleConfig.allowlistFiles,
    rules: { 'no-restricted-imports': 'off' },
  },
])
```

Edit `modules.catalog.json` and run `npm run generate:modules` to refresh `eslint.config.modules.json`. The `modules-codegen-drift` CI job fails if the JSON falls out of sync.

### Where messaging contracts live — `Starter.Abstractions.Messaging`

`IModuleBusContributor` (the contract every module implements when it ships an `IConsumer<T>`) lives in `boilerplateBE/src/Starter.Abstractions.Messaging/Modularity/`. This thin project references only `MassTransit` and exists so optional modules can opt into bus configuration without taking a heavy `Starter.Infrastructure` dependency. The host (`Starter.Infrastructure.DependencyInjection`) iterates `IModuleBusContributor` implementations via the `configureBus` callback wired in `Program.cs`. `AbstractionsPurityTests` continues to forbid MassTransit inside the pure `Starter.Abstractions` project — only `Starter.Abstractions.Messaging` carries it.

---

**Questions? Unclear? Rule you want added?** Open a discussion in the repo and update this guide.
