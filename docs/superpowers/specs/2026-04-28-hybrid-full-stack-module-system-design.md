# Hybrid Full-Stack Module System Design

**Date:** 2026-04-28
**Status:** Draft approved for planning. Open decisions resolved 2026-04-28 (see §14).
**Scope:** Backend .NET modules, React web modules, Flutter mobile modules, source and package distribution, module selection, dependency validation, and guardrails.

---

## 1. Context

The boilerplate is moving from "module-shaped source folders" toward a product-grade module system. The current architecture already has strong foundations:

- Backend modules implement `IModule`, own DbContexts, migrations, permissions, seeders, and services.
- Web modules register slots and optional capabilities through `src/lib/extensions`.
- Mobile has a module folder pattern and shell contribution model.
- `scripts/modules.json` and `rename.ps1` can remove source modules from generated projects.

The remaining gap is that the module system is not yet the single source of truth. Some core files still import optional modules directly, route and navigation registration are partially hardcoded, dependency selections are not strict enough, and package distribution is not yet a first-class design constraint.

The commercial goal changes the target: modules must work both as editable source for internal/client projects and as paid packages for organizations, teams, or customers who should not receive source by default. Higher-tier customers may later receive the same module as source. That means source and package delivery must share one contract, not become two parallel architectures.

---

## 2. Goals

1. **Hybrid distribution:** every optional module can be delivered as source, backend package, web package, mobile package, or a full-stack package set.
2. **One module identity:** a module has one stable id, manifest, permission namespace, capability surface, version, and dependency graph regardless of distribution mode.
3. **Clean core:** core projects and shells do not import optional module internals except through controlled module host/bootstrap files.
4. **Full-stack composition:** backend, web, and mobile contributions are enabled/disabled together from the catalog, with platform-specific support for partial delivery when needed.
5. **Strict dependency behavior:** missing module dependencies fail clearly during generation/startup, or are auto-included by an explicit CLI mode.
6. **Commercial readiness:** package feeds and source delivery can coexist; switching a customer from package to source does not require app code rewrites.
7. **Killer-test confidence:** generated apps build with all modules, no modules, and representative subsets across backend, web, and mobile.

---

## 3. Non-Goals

- No runtime plugin marketplace in the first implementation. Modules are selected at build/generation time.
- No dynamic untrusted code loading.
- No microservice extraction in this phase. The module shape should make extraction easier later, but deployment stays a modular monolith.
- No license enforcement service in this phase. The design should leave room for package feed/license gating, but implementation starts with module composition.
- No rewrite of all existing modules before the host contracts exist. Existing modules migrate incrementally.

---

## 4. Core Decision

Use a **contract-first hybrid module model**.

Each optional module is described by a catalog entry and implements platform-specific contracts:

- Backend: `IModule` plus optional extension contracts for bus, health checks, API metadata, migrations, seeders, and package metadata.
- Web: `WebModule` registration object that contributes routes, nav entries, slots, capabilities, i18n bundles, and optional providers.
- Mobile: `AppModule` registration object that contributes DI bindings, routes/pages, nav items, slots, permissions, localization, and feature flags.

Source and package modules expose the same module id and public registration contract. The host does not care whether the module came from `src/modules/Starter.Module.Workflow`, a NuGet package, `src/features/workflow`, an npm package, `lib/modules/workflow`, or a pub package.

---

## 5. Module Catalog

`scripts/modules.json` evolves from a removal manifest into the module catalog.

Example:

```json
{
  "workflow": {
    "id": "workflow",
    "displayName": "Workflow & Approvals",
    "version": "1.0.0",
    "required": false,
    "dependencies": ["commentsActivity", "communication"],
    "capabilities": ["IWorkflowService"],
    "permissions": [
      "Workflows.View",
      "Workflows.ManageDefinitions",
      "Workflows.Start",
      "Workflows.ActOnTask"
    ],
    "backend": {
      "sourceProject": "Starter.Module.Workflow",
      "packageId": "Starter.Module.Workflow",
      "registrationType": "Starter.Module.Workflow.WorkflowModule"
    },
    "web": {
      "sourceFeature": "workflow",
      "packageId": "@starter/module-workflow",
      "registrationExport": "workflowModule"
    },
    "mobile": {
      "sourceModule": "workflow",
      "packageId": "starter_module_workflow",
      "registrationSymbol": "WorkflowModule"
    },
    "tests": {
      "backendFolder": "Workflow"
    }
  }
}
```

Catalog rules:

- `id` is stable and lower camel case.
- `dependencies` are module ids, not project names.
- Package ids are optional until that module has a packaged distribution.
- A module can support only some platforms, but that absence is explicit in the catalog.
- The catalog is used by generation, docs, dependency validation, and architecture tests.

---

## 6. Backend Design

### 6.1 Module Host

`Starter.Api` should depend on a neutral module host, not on optional module namespaces.

Current problem examples:

- `Program.cs` imports Workflow infrastructure to register its outbox.
- AI is referenced by the API project but missing from the module catalog.

Target shape:

```csharp
var catalog = ModuleCatalog.Load(builder.Configuration);
var modules = ModuleHost.Discover(catalog);
var orderedModules = ModuleHost.ResolveOrder(modules, catalog);

builder.Services.AddApplication(orderedModules.Assemblies);
builder.Services.AddInfrastructure(builder.Configuration, orderedModules.Assemblies);
builder.Services.AddModules(orderedModules, builder.Configuration);
```

### 6.2 Backend Module Contract

Keep `IModule` as the base contract, then add optional small interfaces instead of growing one large interface forever:

- `IModule`: identity, services, permissions, default role permissions, migrate, seed.
- `IModuleBusContributor`: register MassTransit consumers, outboxes, endpoint policies, and bus filters.
- `IModuleHealthContributor`: register health checks.
- `IModuleApiContributor`: expose application parts or API metadata if needed.
- `IModuleDependencyMetadata`: dependency ids, when not supplied purely by catalog.

The module host calls these interfaces when present. Core no longer passes module-specific callbacks such as `bus.AddWorkflowOutbox()`.

### 6.3 Source and Package Discovery

Source mode:

- API project references source module projects.
- Assemblies are discovered from app dependencies/base directory.
- Module instances are created through `IModule`.

Package mode:

- API project references NuGet packages.
- Package assemblies are discovered the same way.
- Module instances are created through the same `IModule`.

No code path should branch on "source vs package" after assemblies are loaded.

### 6.4 Migrations

Each backend module continues to own its DbContext and migration history table.

Package modules ship migrations in their assembly. Source modules ship migrations in their project. The module's `MigrateAsync` owns applying them.

Migration safety remains a separate production-hardening track, but the module system must preserve per-module migration ownership.

### 6.5 Dependencies

Dependency handling becomes strict:

- During generation, selecting `workflow` without `commentsActivity` or `communication` either fails or auto-includes dependencies based on a CLI flag.
- During backend startup, if an installed module declares a dependency that is not installed, startup fails with a clear message naming the missing module.
- Silent skips are not allowed.

Recommended CLI behavior:

- Default: auto-include dependencies and print the resulting selection.
- Strict mode: fail if the requested module list omits dependencies.

---

## 7. Web Design

### 7.1 Web Module Host

Core React should render registered contributions instead of importing optional feature internals.

The only source-mode imports from optional web modules should live in `src/config/modules.config.ts` or a generated equivalent. Package-mode imports should also live there.

Target:

```ts
import { workflowModule } from '@/features/workflow';
// or package mode:
import { workflowModule } from '@starter/module-workflow';

const enabledModules: WebModule[] = [workflowModule];
registerAllModules(enabledModules);
```

### 7.2 Web Module Contract

Extend the existing `AppModule` shape into a typed `WebModule`:

```ts
export interface WebModule {
  id: string;
  register(ctx: WebModuleContext): void;
}
```

`WebModuleContext` exposes stable registration APIs:

- `registerRoute(routeContribution)`
- `registerNavGroup(navGroupContribution)`
- `registerSlot(slotId, entry)`
- `registerCapability(key, implementation)`
- `registerI18n(locale, resources)`
- `registerProvider(component)`

Existing slot registration remains, but routes and navigation move out of core hardcoded blocks.

### 7.3 Routes

Core route config renders:

- Core routes declared directly.
- Module routes from `getModuleRoutes()`.

Module routes contain:

- path
- lazy component loader
- permission requirement
- layout region/public/protected classification
- optional feature flag gate

This removes module-specific lazy imports from `routes.tsx`.

### 7.4 Navigation

Core sidebar renders:

- Core nav groups declared directly.
- Module nav group contributions from `getModuleNavGroups(ctx)`.

The nav context includes permissions, tenant scope, feature flags, translation, and optional module-provided hook data. A module that needs a badge, such as Workflow pending tasks, owns the badge hook inside its nav contribution. Core no longer imports `usePendingTaskCount`.

### 7.5 Assets and i18n

Source modules keep assets/translations under their feature folder. Package modules expose them from npm package exports.

The web host registers translation resources before app render. Missing translation keys remain covered by the existing i18n drift work, but the module system must make module translations explicit.

---

## 8. Mobile Design

Mobile uses the same module identity and catalog.

Target shape:

```dart
final modules = <AppModule>[
  WorkflowModule(),
  BillingModule(),
];

registerAppModules(modules);
```

Mobile module contributions:

- DI registrations
- routes/pages
- shell navigation items
- permission constants or generated permission bridge
- localization bundles
- feature flags
- optional slots/capabilities for cross-module surfaces

Source mode imports from `lib/modules/workflow`. Package mode imports from `package:starter_module_workflow/starter_module_workflow.dart`.

The shell must not import optional module internals outside the module configuration file generated from the catalog.

---

## 9. Generation and Selection

`rename.ps1` can remain the first implementation vehicle, but its conceptual role changes from "delete excluded folders" to "generate a selected module graph."

Supported modes:

```powershell
./scripts/rename.ps1 -Name "App" -Modules "workflow,billing"
./scripts/rename.ps1 -Name "App" -Modules "None"
./scripts/rename.ps1 -Name "App" -Modules "workflow" -ModuleDistribution "source"
./scripts/rename.ps1 -Name "App" -Modules "workflow" -ModuleDistribution "package"
./scripts/rename.ps1 -Name "App" -Modules "workflow" -DependencyMode "Strict"
```

Initial implementation can use source mode only while shaping the host contracts, but the catalog schema and config generation should reserve package fields from the start.

Generated outputs:

- Backend project references or package references.
- Web source imports or npm package imports.
- Mobile source imports or pub package imports.
- Active module lists for each platform.
- Removed source folders and tests for excluded source modules.
- Clear dependency report.

---

## 10. Guardrails

Backend:

- Test that core assemblies do not reference `Starter.Module.*`.
- Test that `Starter.Api` contains no `using Starter.Module.*`.
- Test that all discovered module dependencies are installed.
- Keep `AbstractionsPurityTests`.
- Keep messaging tests that ban `IPublishEndpoint` in handlers.

Web:

- ESLint rule or test that core folders cannot import `@/features/{optional-module}` outside the module config/bootstrap file.
- Test that every active module id in `modules.config.ts` exists in the catalog.
- Test that route/nav contributions can be empty when no modules are active.

Mobile:

- Static check that shell/core code does not import optional module packages except through generated module config.
- Test that selected modules produce a valid route/nav registry.

Generation:

- Smoke builds for `All`, `None`, and representative subsets.
- Include a source-mode smoke build first.
- Add package-mode smoke builds once package artifacts exist.

---

## 11. Migration Plan

### Phase 1: Source-mode host cleanup

- Add backend module host extension points.
- Move Workflow bus/outbox registration behind a module contributor.
- Add AI to the catalog or explicitly reclassify it.
- Make dependency validation strict.
- Move web route and nav module contributions behind registries.
- Remove core imports of optional frontend modules.
- Run `-Modules None` source-mode killer test.

### Phase 2: Catalog-driven generation

- Upgrade `modules.json` schema.
- Generate backend/web/mobile module config from the catalog.
- Add dependency auto-include and strict modes.
- Add architecture tests and frontend/mobile import guardrails.

### Phase 3: Package-ready contracts

- Stabilize NuGet, npm, and pub package naming conventions.
- Ensure module assets, translations, migrations, and permissions are exposed through package-friendly APIs.
- Create one pilot package module, likely Products or CommentsActivity, because they are useful but not as operationally heavy as AI.

### Phase 4: Full-stack package pilot

- Build one module as source and package.
- Verify a generated app can use source mode and package mode with the same module id.
- Document package-to-source migration.

### Phase 5: Commercial delivery workflow

- Document private feed publishing.
- Document source delivery.
- Add version compatibility rules between boilerplate core and module packages.
- Add release checklist for module packages.

---

## 12. Success Criteria

The redesign is successful when:

1. `Starter.Api` and backend core projects have no optional module imports.
2. React core routes/sidebar have no optional module feature imports outside module bootstrap.
3. Flutter shell/core has no optional module imports outside module config.
4. `scripts/modules.json` is the source of truth for module identity, dependencies, and platform deliverables.
5. Missing dependencies fail or are auto-included explicitly.
6. Source-mode killer tests pass for all, none, and selected subsets.
7. At least one module can be consumed as source or as package without changing consuming app code beyond generated config/package references.
8. Docs explain how to create, ship, package, and optionally sell source for a module.

---

## 13. Open Decisions for Planning

These should be resolved in the implementation plan:

1. Whether dependency auto-include or strict failure is the default CLI behavior.
2. Whether `modules.json` remains under `scripts/` or moves to a top-level `modules.catalog.json`.
3. Which pilot module becomes the first packaged module.
4. Whether web module package format should be ESM-only or dual ESM/CJS. Recommendation: ESM-only for Vite.
5. How version compatibility is expressed, for example `coreRange: ">=1.0.0 <2.0.0"`.

---

## 14. Resolved Decisions (2026-04-28)

Each entry records the choice, the reasoning, and whether it lands in Tier 1 (immediate, source-mode host cleanup) or is recorded for Phase 3+.

### D1. Dependency selection: strict by default, opt-in auto-include

The CLI fails when a selected module's dependencies are not also selected. The error message names the missing modules and prints the corrected command. An `-AutoIncludeDependencies` flag opts into automatic resolution and prints the resulting module set.

```
./scripts/rename.ps1 -Modules "workflow"
  → Fails: "Module 'workflow' requires 'commentsActivity', 'communication'.
            Re-run with: -Modules 'workflow,commentsActivity,communication'
            Or pass: -AutoIncludeDependencies"
```

**Reasoning:** The CLI runs at project bootstrap, not in a tight loop. A one-time error that teaches the dependency graph is cheaper than silent expansion that surprises someone six months later. Self-healing error message keeps friction under ten seconds.

**Tier 1 scope:** Implement strict failure with helpful error. Defer `-AutoIncludeDependencies` until a real workflow demands it.

### D2. Catalog location: top-level `modules.catalog.json`

Move the catalog from `scripts/modules.json` to the repository root as `modules.catalog.json`.

**Reasoning:** §5 designates the catalog as the single source of truth for generation, docs, dependency validation, architecture tests, and FE/BE/mobile module config. The `scripts/` location implies tooling-only ownership, which is no longer accurate. Top-level placement signals peer status with `package.json` and `Starter.sln`.

**Tier 1 scope:** File move, update `rename.ps1` path constant, update doc references.

### D3. First packaged pilot: Products

Products is the first module to be delivered as both source and package in Phase 3.

**Reasoning:**
- Self-contained domain (CRUD + image upload + demo seed).
- Exercises capability consumption (`IQuotaChecker`, `IWebhookPublisher`) — proves package-mode modules can depend on core capabilities without source access.
- Backend + Web only, no mobile contribution — keeps the first pilot to two platforms instead of three.
- Commercially relevant — e-commerce is sellable, so the pilot doubles as product.
- Lower stakes than Billing, AI, or Workflow if the v1 contract has rough edges.

CommentsActivity is the recommended **second** pilot once the contract is proven, because it exposes four services and stress-tests multi-service packages.

**Tier 1 scope:** None. Recorded for Phase 3.

### D4. Web package format: ESM-only

Web modules are published as ESM only. No dual ESM/CJS, no CommonJS fallback.

**Reasoning:** Vite is ESM-native, the boilerplate has no SSR or Next.js, and dual-package hazard is not worth solving for a problem the boilerplate's target stack does not have.

**Tier 1 scope:** None. Recorded for Phase 3.

### D5. Version compatibility: `coreCompat` semver range, runtime + install-time enforcement

The catalog gains one field per module:

```json
"workflow": {
  "coreCompat": ">=1.0.0 <2.0.0",
  ...
}
```

Enforcement per platform:

- **Backend:** module assembly carries `[CoreCompat(">=1.0.0 <2.0.0")]`. The module host fails startup with a clear message when the loaded `Starter.Core` assembly version does not satisfy the range.
- **Web:** npm `peerDependencies` on `@starter/core`. npm enforces at install.
- **Mobile:** pub `dependencies` constraint on `starter_core`. pub enforces at `flutter pub get`.

**Reasoning:** Semver range is the universal primitive across NuGet, npm, and pub. Runtime enforcement on backend catches assembly mismatches that NuGet resolution can miss (stale local caches, manual DLL drops). Install-time enforcement on web and mobile is free from existing package managers.

**Tier 1 scope:** None. The `coreCompat` field is added the day Phase 3 starts — adding it earlier with no enforcer creates a rotting field that gets copied without thought.

### Summary

| # | Decision | Tier 1 |
|---|---|---|
| D1 | Strict deps with helpful error, `-AutoIncludeDependencies` deferred | Yes |
| D2 | Move catalog to `/modules.catalog.json` | Yes |
| D3 | Products as first packaged pilot | Phase 3 |
| D4 | ESM-only web packages | Phase 3 |
| D5 | `coreCompat` semver, runtime + install enforcement | Phase 3 |

