# Module System Tier 2 Source Composition Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make source-mode module composition catalog-driven across backend, React web, and Flutter mobile while keeping generated apps clean, buildable, and free of optional-module core imports.

**Architecture:** Keep `modules.catalog.json` as the source of truth and evolve `rename.ps1` into a source-mode composer with validation and generated platform config files. Add a typed React module registry for routes, nav, slots, and capabilities, then migrate optional routes/nav out of core. Keep backend runtime dependencies strict only for hard dependencies, and keep mobile's existing registry while adding duplicate-id protection and import guardrails.

**Tech Stack:** PowerShell 7, .NET 10, xUnit + FluentAssertions, React 19 + TypeScript + React Router, ESLint, Flutter/Dart + flutter_test, existing module catalog and rename script.

---

## Pre-flight

Run this work in a dedicated branch/worktree from current `main` after Tier 1 has landed. The Codex worktree already has a branch ready (`codex/modularity-tier-2`) — use that. If starting fresh elsewhere:

```bash
git status --short
git checkout -b codex/modularity-tier-2 origin/main
```

If using the existing Codex worktree, confirm the branch is already dedicated and clean:

```bash
git status --short
git log -1 --oneline
```

Expected before Task 1: no uncommitted changes except this plan if it is being edited.

---

## File Structure

**New files:**

| Path | Purpose |
|---|---|
| `boilerplateFE/src/lib/modules/web-module.ts` | Shared frontend module contracts for routes, nav, slots, capabilities, and providers. |
| `boilerplateFE/src/lib/modules/registry.ts` | Runtime web module registry, duplicate guards, and read APIs for routes/nav. |
| `boilerplateFE/src/lib/modules/index.ts` | Public exports for web module primitives. |
| `boilerplateFE/src/features/workflow/components/WorkflowPendingTaskBadge.tsx` | Module-owned nav badge component so core no longer imports Workflow query logic. |
| `boilerplateMobile/test/core/modularity/module_import_guard_test.dart` | Static guard that core mobile files do not import optional module folders directly. |

**Renamed files:**

| From | To | Reason |
|---|---|---|
| `boilerplateFE/src/features/billing/index.ts` | `boilerplateFE/src/features/billing/index.tsx` | Module entry contributes JSX route objects. |
| `boilerplateFE/src/features/webhooks/index.ts` | `boilerplateFE/src/features/webhooks/index.tsx` | Module entry contributes JSX route objects. |
| `boilerplateFE/src/features/import-export/index.ts` | `boilerplateFE/src/features/import-export/index.tsx` | Module entry contributes JSX route objects. |
| `boilerplateFE/src/features/products/index.ts` | `boilerplateFE/src/features/products/index.tsx` | Module entry contributes JSX route objects. |
| `boilerplateFE/src/features/comments-activity/index.ts` | `boilerplateFE/src/features/comments-activity/index.tsx` | Keep all optional web module entries consistent. |
| `boilerplateFE/src/features/communication/index.ts` | `boilerplateFE/src/features/communication/index.tsx` | Module entry contributes JSX route objects. |
| `boilerplateFE/src/features/workflow/index.ts` | `boilerplateFE/src/features/workflow/index.tsx` | Module entry contributes JSX route objects and badge component. |

**Modified files:**

| Path | Change |
|---|---|
| `modules.catalog.json` | No schema change expected; used by new validations. |
| `scripts/rename.ps1` | Add composer-style helper functions, unknown-id checks, artifact validation, generated FE/mobile config output, and remove old route import rewrite. |
| `boilerplateBE/tests/Starter.Api.Tests/Architecture/CatalogConsistencyTests.cs` | Add catalog uniqueness and platform artifact tests. |
| `boilerplateFE/src/config/modules.config.ts` | Convert to generated-compatible typed config using `WebModule` and `registerWebModules`. |
| `boilerplateFE/src/routes/routes.tsx` | Replace optional imports/conditionals with `createRoutes()` and `getModuleRoutes()`. |
| `boilerplateFE/src/routes/index.tsx` | Create browser router lazily inside `AppRouter` after module bootstrap has run. |
| `boilerplateFE/src/components/layout/MainLayout/useNavGroups.ts` | Replace optional nav blocks with `getModuleNavGroups(ctx)`. |
| `boilerplateFE/src/components/layout/MainLayout/Sidebar.tsx` | Render optional module badge components as well as numeric badges. |
| `boilerplateFE/src/components/layout/MainLayout/MorePanel.tsx` | Render optional module badge components in overflow panel. |
| `boilerplateFE/eslint.config.js` | Expand optional import guard and remove `routes.tsx` from the allowlist. |
| `boilerplateMobile/lib/core/modularity/app_module.dart` | Fix catalog path comment. |
| `boilerplateMobile/lib/app/modules.config.dart` | Fix catalog path comment and keep generated-compatible markers. |
| `boilerplateMobile/lib/core/modularity/module_registry.dart` | Add duplicate module id validation before topological sort. |
| `boilerplateMobile/test/core/modularity/module_registry_test.dart` | Add duplicate module id test. |
| `docs/architecture/module-development.md` | Update stale `scripts/modules.json` references to `modules.catalog.json`. |
| `docs/architecture/domain-module-example.md` | Update stale `scripts/modules.json` references to `modules.catalog.json`. |
| `docs/testing/sessions/2026-04-16-module-audit.md` | Update historical wording only if this doc is used as current guidance; otherwise leave historical notes alone. |

---

## Task 1: Strengthen Catalog Consistency Tests

**Files:**
- Modify: `boilerplateBE/tests/Starter.Api.Tests/Architecture/CatalogConsistencyTests.cs`

- [ ] **Step 1: Add failing catalog tests**

Open `boilerplateBE/tests/Starter.Api.Tests/Architecture/CatalogConsistencyTests.cs` and add these test methods after `Module_ids_are_lower_camel_case()`:

```csharp
[Fact]
public void Config_keys_are_present_and_unique()
{
    using var doc = JsonDocument.Parse(File.ReadAllText(CatalogPath));
    var configKeys = new Dictionary<string, string>(StringComparer.Ordinal);
    var problems = new List<string>();

    foreach (var module in ModuleEntries(doc))
    {
        if (!module.Value.TryGetProperty("configKey", out var configKeyProp) ||
            configKeyProp.ValueKind != JsonValueKind.String ||
            string.IsNullOrWhiteSpace(configKeyProp.GetString()))
        {
            problems.Add($"'{module.Name}' is missing a non-empty configKey");
            continue;
        }

        var configKey = configKeyProp.GetString()!;
        if (configKeys.TryGetValue(configKey, out var owner))
        {
            problems.Add($"'{module.Name}' and '{owner}' share configKey '{configKey}'");
        }
        else
        {
            configKeys[configKey] = module.Name;
        }
    }

    problems.Should().BeEmpty(
        "rename.ps1 emits activeModules keys from configKey, so keys must be stable and unique.");
}

[Fact]
public void Declared_backend_module_projects_exist()
{
    using var doc = JsonDocument.Parse(File.ReadAllText(CatalogPath));
    var repoRoot = GetRepoRoot();
    var problems = new List<string>();

    foreach (var module in ModuleEntries(doc))
    {
        var backendModule = ReadOptionalString(module.Value, "backendModule");
        if (backendModule is null) continue;

        var expectedPath = Path.Combine(repoRoot, "boilerplateBE", "src", "modules", backendModule);
        if (!Directory.Exists(expectedPath))
        {
            problems.Add($"'{module.Name}.backendModule' points to missing project folder '{expectedPath}'");
        }
    }

    problems.Should().BeEmpty(
        "catalog backendModule values are used by generated-app composition and must resolve in the template.");
}

[Fact]
public void Declared_frontend_features_have_module_entrypoints()
{
    using var doc = JsonDocument.Parse(File.ReadAllText(CatalogPath));
    var repoRoot = GetRepoRoot();
    var problems = new List<string>();

    foreach (var module in ModuleEntries(doc))
    {
        var feature = ReadOptionalString(module.Value, "frontendFeature");
        if (feature is null) continue;

        var featurePath = Path.Combine(repoRoot, "boilerplateFE", "src", "features", feature);
        var indexTs = Path.Combine(featurePath, "index.ts");
        var indexTsx = Path.Combine(featurePath, "index.tsx");

        if (!Directory.Exists(featurePath))
        {
            problems.Add($"'{module.Name}.frontendFeature' points to missing folder '{featurePath}'");
            continue;
        }

        if (!File.Exists(indexTs) && !File.Exists(indexTsx))
        {
            problems.Add($"'{module.Name}.frontendFeature' must expose index.ts or index.tsx in '{featurePath}'");
        }
    }

    problems.Should().BeEmpty(
        "generated modules.config.ts imports selected web modules from their feature entrypoints.");
}

[Fact]
public void Declared_mobile_modules_have_matching_folder_and_entrypoint()
{
    using var doc = JsonDocument.Parse(File.ReadAllText(CatalogPath));
    var repoRoot = GetRepoRoot();
    var problems = new List<string>();

    foreach (var module in ModuleEntries(doc))
    {
        var mobileModule = ReadOptionalString(module.Value, "mobileModule");
        var mobileFolder = ReadOptionalString(module.Value, "mobileFolder");

        if (mobileModule is null && mobileFolder is null) continue;
        if (mobileModule is null || mobileFolder is null)
        {
            problems.Add($"'{module.Name}' must define both mobileModule and mobileFolder, or neither");
            continue;
        }

        var moduleFile = ToSnakeCase(mobileModule) + ".dart";
        var expectedPath = Path.Combine(repoRoot, "boilerplateMobile", "lib", "modules", mobileFolder, moduleFile);

        if (!File.Exists(expectedPath))
        {
            problems.Add($"'{module.Name}' mobile entrypoint missing at '{expectedPath}'");
        }
    }

    problems.Should().BeEmpty(
        "generated modules.config.dart imports selected mobile modules from catalog metadata.");
}
```

Then add these helper methods before `FindCatalogPath()`:

```csharp
private static IEnumerable<JsonProperty> ModuleEntries(JsonDocument doc) =>
    doc.RootElement.EnumerateObject().Where(p => !p.Name.StartsWith("_"));

private static string? ReadOptionalString(JsonElement element, string propertyName)
{
    if (!element.TryGetProperty(propertyName, out var property)) return null;
    if (property.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined) return null;
    if (property.ValueKind != JsonValueKind.String) return null;

    var value = property.GetString();
    return string.IsNullOrWhiteSpace(value) ? null : value;
}

private static string ToSnakeCase(string value)
{
    var chars = new List<char>(value.Length + 8);
    for (var i = 0; i < value.Length; i++)
    {
        var c = value[i];
        if (char.IsUpper(c) && i > 0)
        {
            chars.Add('_');
        }

        chars.Add(char.ToLowerInvariant(c));
    }

    return new string(chars.ToArray());
}

private static string GetRepoRoot()
{
    var catalog = new FileInfo(CatalogPath);
    return catalog.Directory?.FullName
        ?? throw new DirectoryNotFoundException("Unable to resolve repository root from " + CatalogPath);
}
```

- [ ] **Step 2: Run tests and verify current failure**

Run:

```bash
dotnet test boilerplateBE/tests/Starter.Api.Tests/Starter.Api.Tests.csproj --filter CatalogConsistencyTests
```

Expected: failure on `Declared_frontend_features_have_module_entrypoints` after module `index.ts` files are renamed later, or pass if both `.ts` and `.tsx` are accepted. If it passes immediately, keep the tests; they still protect future catalog drift.

- [ ] **Step 3: Keep test helpers compiling**

If the compiler reports missing namespaces, add these usings at the top of `CatalogConsistencyTests.cs`:

```csharp
using System.Collections.Generic;
using System.Linq;
```

- [ ] **Step 4: Re-run catalog tests**

Run:

```bash
dotnet test boilerplateBE/tests/Starter.Api.Tests/Starter.Api.Tests.csproj --filter CatalogConsistencyTests
```

Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add boilerplateBE/tests/Starter.Api.Tests/Architecture/CatalogConsistencyTests.cs
git commit -m "test(modules): validate catalog platform artifacts"
```

---

## Task 2: Add Web Module Contract and Registry

**Files:**
- Create: `boilerplateFE/src/lib/modules/web-module.ts`
- Create: `boilerplateFE/src/lib/modules/registry.ts`
- Create: `boilerplateFE/src/lib/modules/index.ts`
- Modify: `boilerplateFE/src/config/modules.config.ts`
- Rename/modify: optional web module `index.ts` files to `index.tsx`

- [ ] **Step 1: Create web module contract**

Create `boilerplateFE/src/lib/modules/web-module.ts`:

```ts
import type { ComponentType } from 'react';
import type { RouteObject } from 'react-router-dom';
import type { LucideIcon } from 'lucide-react';
import type { TFunction } from 'i18next';
import type { SlotEntry, SlotId } from '@/lib/extensions';

export type ModuleRouteRegion = 'public' | 'protected';

export interface ModuleRouteContribution {
  id: string;
  region: ModuleRouteRegion;
  route: RouteObject;
  order?: number;
}

export interface ModuleNavItem {
  label: string;
  icon: LucideIcon;
  path: string;
  end?: boolean;
  badge?: number;
  Badge?: ComponentType;
}

export interface ModuleNavGroup {
  id: string;
  label?: string;
  order?: number;
  items: ModuleNavItem[];
}

export type ModuleNavGroupBody = Omit<ModuleNavGroup, 'id' | 'order'>;

export interface ModuleNavContext {
  t: TFunction;
  hasPermission(permission: string): boolean;
  tenantScoped: boolean;
  isFeatureEnabled(key: string): boolean;
}

export interface ModuleNavGroupContribution {
  id: string;
  order?: number;
  build(ctx: ModuleNavContext): ModuleNavGroupBody | null | undefined;
}

export type CoreNavGroupId = 'top' | 'people' | 'content' | 'platform';

export interface ModuleNavItemContribution {
  id: string;
  order?: number;
  build(ctx: ModuleNavContext): ModuleNavItem | null | undefined;
}

export interface WebModuleContext {
  registerRoute(contribution: ModuleRouteContribution): void;
  registerNavGroup(contribution: ModuleNavGroupContribution): void;
  registerNavItem(groupId: CoreNavGroupId, contribution: ModuleNavItemContribution): void;
  registerSlot<S extends SlotId>(slot: S, entry: SlotEntry<S>): void;
  registerCapability<F extends (...args: never[]) => unknown>(key: string, implementation: F): void;
}

export interface WebModule {
  id: string;
  register(ctx: WebModuleContext): void;
}
```

- [ ] **Step 2: Create web registry**

Create `boilerplateFE/src/lib/modules/registry.ts`:

```ts
import type {
  CoreNavGroupId,
  ModuleNavContext,
  ModuleNavGroup,
  ModuleNavGroupContribution,
  ModuleNavItem,
  ModuleNavItemContribution,
  ModuleRouteContribution,
  ModuleRouteRegion,
  WebModule,
  WebModuleContext,
} from './web-module';
import { registerCapability } from '@/lib/extensions/capabilities';
import { registerSlot, type SlotEntry, type SlotId } from '@/lib/extensions/slots';

const registeredModuleIds = new Set<string>();
const routeContributions = new Map<string, ModuleRouteContribution>();
const navGroupContributions = new Map<string, ModuleNavGroupContribution>();
const navItemContributions = new Map<CoreNavGroupId, Map<string, ModuleNavItemContribution>>();

function ensureUnique(map: Map<string, unknown>, id: string, kind: string): void {
  if (map.has(id)) {
    throw new Error(`Duplicate web module ${kind} id '${id}'. Contribution ids must be unique.`);
  }
}

export function registerWebModules(modules: WebModule[]): void {
  registeredModuleIds.clear();
  routeContributions.clear();
  navGroupContributions.clear();
  navItemContributions.clear();

  for (const mod of modules) {
    if (registeredModuleIds.has(mod.id)) {
      throw new Error(`Duplicate web module id '${mod.id}'. Module ids must match modules.catalog.json keys.`);
    }

    registeredModuleIds.add(mod.id);

    const ctx: WebModuleContext = {
      registerRoute(contribution) {
        ensureUnique(routeContributions, contribution.id, 'route');
        routeContributions.set(contribution.id, contribution);
      },
      registerNavGroup(contribution) {
        ensureUnique(navGroupContributions, contribution.id, 'nav group');
        navGroupContributions.set(contribution.id, contribution);
      },
      registerNavItem(groupId, contribution) {
        let bucket = navItemContributions.get(groupId);
        if (!bucket) {
          bucket = new Map();
          navItemContributions.set(groupId, bucket);
        }
        ensureUnique(bucket, contribution.id, `nav item (${groupId})`);
        bucket.set(contribution.id, contribution);
      },
      registerSlot<S extends SlotId>(slot: S, entry: SlotEntry<S>) {
        registerSlot(slot, entry);
      },
      registerCapability(key, implementation) {
        registerCapability(key, implementation);
      },
    };

    mod.register(ctx);
  }
}

export function getModuleRoutes(region: ModuleRouteRegion) {
  return [...routeContributions.values()]
    .filter((contribution) => contribution.region === region)
    .sort((a, b) => (a.order ?? 100) - (b.order ?? 100))
    .map((contribution) => contribution.route);
}

export function getModuleNavGroups(ctx: ModuleNavContext): ModuleNavGroup[] {
  return [...navGroupContributions.values()]
    .map((contribution) => {
      const body = contribution.build(ctx);
      if (!body || body.items.length === 0) return undefined;
      return {
        id: contribution.id,
        order: contribution.order,
        ...body,
      };
    })
    .filter((group): group is ModuleNavGroup => Boolean(group && group.items.length > 0))
    .sort((a, b) => (a.order ?? 100) - (b.order ?? 100));
}

export function getModuleNavItems(groupId: CoreNavGroupId, ctx: ModuleNavContext): ModuleNavItem[] {
  const bucket = navItemContributions.get(groupId);
  if (!bucket) return [];
  return [...bucket.values()]
    .map((contribution) => {
      const item = contribution.build(ctx);
      return item ? { item, order: contribution.order ?? 100 } : undefined;
    })
    .filter((entry): entry is { item: ModuleNavItem; order: number } => Boolean(entry))
    .sort((a, b) => a.order - b.order)
    .map((entry) => entry.item);
}

export function getRegisteredModuleIds(): string[] {
  return [...registeredModuleIds];
}
```

- [ ] **Step 3: Create public module exports**

Create `boilerplateFE/src/lib/modules/index.ts`:

```ts
export {
  registerWebModules,
  getModuleRoutes,
  getModuleNavGroups,
  getModuleNavItems,
  getRegisteredModuleIds,
} from './registry';
export type {
  CoreNavGroupId,
  ModuleNavContext,
  ModuleNavGroup,
  ModuleNavGroupBody,
  ModuleNavGroupContribution,
  ModuleNavItem,
  ModuleNavItemContribution,
  ModuleRouteContribution,
  ModuleRouteRegion,
  WebModule,
  WebModuleContext,
} from './web-module';
```

- [ ] **Step 4: Rename optional web module entrypoints**

Run:

```bash
git mv boilerplateFE/src/features/billing/index.ts boilerplateFE/src/features/billing/index.tsx
git mv boilerplateFE/src/features/webhooks/index.ts boilerplateFE/src/features/webhooks/index.tsx
git mv boilerplateFE/src/features/import-export/index.ts boilerplateFE/src/features/import-export/index.tsx
git mv boilerplateFE/src/features/products/index.ts boilerplateFE/src/features/products/index.tsx
git mv boilerplateFE/src/features/comments-activity/index.ts boilerplateFE/src/features/comments-activity/index.tsx
git mv boilerplateFE/src/features/communication/index.ts boilerplateFE/src/features/communication/index.tsx
git mv boilerplateFE/src/features/workflow/index.ts boilerplateFE/src/features/workflow/index.tsx
```

- [ ] **Step 5: Update module entrypoints to the typed contract**

For each renamed optional module entrypoint, change the export shape from `name/register()` to `id/register(ctx)`. Keep existing slot registrations by replacing `registerSlot(...)` with `ctx.registerSlot(...)`.

Example for `boilerplateFE/src/features/products/index.tsx`:

```ts
import { lazy } from 'react';
import type { WebModule } from '@/lib/modules';

const TenantProductsTab = lazy(() =>
  import('./components/TenantProductsTab').then((m) => ({ default: m.TenantProductsTab })),
);

const ProductsDashboardCard = lazy(() =>
  import('./components/ProductsDashboardCard').then((m) => ({ default: m.ProductsDashboardCard })),
);

export const productsModule: WebModule = {
  id: 'products',
  register(ctx): void {
    ctx.registerSlot('tenant-detail-tabs', {
      id: 'products.tenant-products',
      module: 'products',
      order: 40,
      label: () => 'Products',
      permission: 'Products.View',
      component: TenantProductsTab,
    });

    ctx.registerSlot('dashboard-cards', {
      id: 'products.dashboard-card',
      module: 'products',
      order: 10,
      permission: 'Products.View',
      component: ProductsDashboardCard,
    });
  },
};
```

Apply these exact export-shape edits to the other module entrypoints:

```text
billing/index.tsx:
- add: import type { WebModule } from '@/lib/modules';
- remove: import { registerSlot } from '@/lib/extensions';
- change: export const billingModule = {
- to:     export const billingModule: WebModule = {
- change: name: 'billing',
- to:     id: 'billing',
- change: register(): void {
- to:     register(ctx): void {
- change every: registerSlot(
- to:           ctx.registerSlot(
```

```text
webhooks/index.tsx:
- add: import type { WebModule } from '@/lib/modules';
- change: export const webhooksModule = {
- to:     export const webhooksModule: WebModule = {
- change: name: 'webhooks',
- to:     id: 'webhooks',
- keep:   register(): void { }
```

```text
import-export/index.tsx:
- add: import type { WebModule } from '@/lib/modules';
- remove: import { registerSlot } from '@/lib/extensions';
- change: export const importExportModule = {
- to:     export const importExportModule: WebModule = {
- change: name: 'importExport',
- to:     id: 'importExport',
- change: register(): void {
- to:     register(ctx): void {
- change every: registerSlot(
- to:           ctx.registerSlot(
```

```text
comments-activity/index.tsx:
- add: import type { WebModule } from '@/lib/modules';
- remove: import { registerSlot } from '@/lib/extensions';
- change: export const commentsActivityModule = {
- to:     export const commentsActivityModule: WebModule = {
- change: name: 'commentsActivity',
- to:     id: 'commentsActivity',
- change: register(): void {
- to:     register(ctx): void {
- change every: registerSlot(
- to:           ctx.registerSlot(
```

```text
communication/index.tsx:
- add: import type { WebModule } from '@/lib/modules';
- remove: import { registerSlot } from '@/lib/extensions';
- change: export const communicationModule = {
- to:     export const communicationModule: WebModule = {
- change: name: 'communication',
- to:     id: 'communication',
- change: register(): void {
- to:     register(ctx): void {
- change every: registerSlot(
- to:           ctx.registerSlot(
```

```text
workflow/index.tsx:
- add: import type { WebModule } from '@/lib/modules';
- remove: import { registerSlot } from '@/lib/extensions';
- change: export const workflowModule = {
- to:     export const workflowModule: WebModule = {
- change: name: 'workflow',
- to:     id: 'workflow',
- change: register(): void {
- to:     register(ctx): void {
- change every: registerSlot(
- to:           ctx.registerSlot(
```

- [ ] **Step 6: Update modules config to use the registry**

Replace `boilerplateFE/src/config/modules.config.ts` with the shape below. **`enabledModules` is the single source of truth**; `isModuleActive()` and the literal `activeModules` map both derive from it, so the generator can never produce drift between them:

```ts
import { billingModule } from '@/features/billing';
import { webhooksModule } from '@/features/webhooks';
import { importExportModule } from '@/features/import-export';
import { productsModule } from '@/features/products';
import { commentsActivityModule } from '@/features/comments-activity';
import { communicationModule } from '@/features/communication';
import { workflowModule } from '@/features/workflow';
import { registerWebModules, type WebModule } from '@/lib/modules';

/**
 * Optional module registry. This file is generated by the source-mode composer
 * in generated apps. In the template, all source modules are active.
 *
 * Core features (Files, Notifications, FeatureFlags, ApiKeys, AuditLogs,
 * Reports) are not in this list because they ship with every build.
 */

// Static catalog union — does not vary by selection. The generator always
// emits all 8 ids so that callers using ModuleName get a stable type.
export type ModuleName =
  | 'ai'
  | 'billing'
  | 'webhooks'
  | 'importExport'
  | 'products'
  | 'commentsActivity'
  | 'communication'
  | 'workflow';

// Source of truth for what's active in this build. The generator emits
// only the imports for modules included via `-Modules`; everything else
// is absent from this list (and its source files have been deleted).
export const enabledModules: WebModule[] = [
  billingModule,
  webhooksModule,
  importExportModule,
  productsModule,
  commentsActivityModule,
  communicationModule,
  workflowModule,
];

const enabledIds = new Set<string>(enabledModules.map((m) => m.id));

export function isModuleActive(module: ModuleName): boolean {
  return enabledIds.has(module);
}

// Frozen literal view derived from enabledModules. Kept for callers that
// read the map directly (e.g. `activeModules.workflow`); cannot drift from
// enabledModules because every flag is computed from enabledIds.
export const activeModules: Readonly<Record<ModuleName, boolean>> = Object.freeze({
  ai: isModuleActive('ai'),
  billing: isModuleActive('billing'),
  webhooks: isModuleActive('webhooks'),
  importExport: isModuleActive('importExport'),
  products: isModuleActive('products'),
  commentsActivity: isModuleActive('commentsActivity'),
  communication: isModuleActive('communication'),
  workflow: isModuleActive('workflow'),
});

export function registerAllModules(): void {
  registerWebModules(enabledModules);
}
```

Note on AI module: `ai` has no `frontendFeature` in the catalog, so it never appears in `enabledModules`. `activeModules.ai` will always be `false` in this map. That's correct — there's no web surface for the AI module to be "active" on. Backend AI activation is independent and handled by backend module discovery.

- [ ] **Step 7: Run frontend type check/build**

Run:

```bash
cd boilerplateFE
npm run build
```

Expected: PASS. At this point routes/nav still use `activeModules`, so this is a safe compatibility checkpoint.

- [ ] **Step 8: Commit**

```bash
git add boilerplateFE/src/lib/modules \
        boilerplateFE/src/config/modules.config.ts \
        boilerplateFE/src/features/billing/index.tsx \
        boilerplateFE/src/features/webhooks/index.tsx \
        boilerplateFE/src/features/import-export/index.tsx \
        boilerplateFE/src/features/products/index.tsx \
        boilerplateFE/src/features/comments-activity/index.tsx \
        boilerplateFE/src/features/communication/index.tsx \
        boilerplateFE/src/features/workflow/index.tsx
git add -u boilerplateFE/src/features
git commit -m "feat(web-modules): add typed web module registry"
```

---

## Task 3: Refactor Rename Script Into Validating Source Composer

**Files:**
- Modify: `scripts/rename.ps1`

- [ ] **Step 1: Add composer helper functions**

In `scripts/rename.ps1`, add these functions immediately before `# ── Module selection preflight`:

```powershell
function Get-CatalogModuleProperties {
    param([object]$Catalog)

    return @($Catalog.PSObject.Properties | Where-Object { -not $_.Name.StartsWith("_") })
}

function ConvertTo-SnakeCase {
    param([Parameter(Mandatory = $true)][string]$Value)

    return (($Value -creplace '([A-Z])', '_$1').TrimStart('_').ToLower())
}

function Get-WebModuleSymbol {
    param([Parameter(Mandatory = $true)][string]$ConfigKey)

    return "$($ConfigKey)Module"
}

function Resolve-ModuleSelection {
    param(
        [string]$RequestedModules,
        [string[]]$AllOptional
    )

    if (-not $RequestedModules -or $RequestedModules -eq "All") {
        return @{
            Included = @($AllOptional)
            Excluded = @()
        }
    }

    if ($RequestedModules -eq "None") {
        return @{
            Included = @()
            Excluded = @($AllOptional)
        }
    }

    $requested = @($RequestedModules -split "," | ForEach-Object { $_.Trim() } | Where-Object { $_ })
    $validLookup = @{}
    foreach ($moduleId in $AllOptional) {
        $validLookup[$moduleId.ToLowerInvariant()] = $moduleId
    }

    $unknown = @()
    $included = @()
    foreach ($moduleId in $requested) {
        $key = $moduleId.ToLowerInvariant()
        if (-not $validLookup.ContainsKey($key)) {
            $unknown += $moduleId
        } else {
            $included += $validLookup[$key]
        }
    }

    if ($unknown.Count -gt 0) {
        $lines = @()
        $lines += ""
        $lines += "ERROR: Unknown module id(s): $($unknown -join ', ')"
        $lines += "Valid optional module ids: $($AllOptional -join ', ')"
        $lines += ""
        Write-Error ($lines -join [Environment]::NewLine)
        exit 1
    }

    $included = @($included | Sort-Object -Unique)
    $excluded = @($AllOptional | Where-Object { $_ -notin $included })

    return @{
        Included = $included
        Excluded = $excluded
    }
}

function Assert-ModuleDependencies {
    param(
        [object]$Catalog,
        [string[]]$IncludedOptional
    )

    $selectedSet = @{}
    foreach ($moduleId in @($IncludedOptional)) { $selectedSet[$moduleId] = $true }

    $missing = @{}
    foreach ($moduleId in @($IncludedOptional)) {
        $entry = $Catalog.$moduleId
        if ($null -eq $entry.dependencies) { continue }
        foreach ($dep in @($entry.dependencies)) {
            if (-not $selectedSet.ContainsKey($dep)) {
                if (-not $missing.ContainsKey($moduleId)) {
                    $missing[$moduleId] = New-Object System.Collections.ArrayList
                }
                [void]$missing[$moduleId].Add($dep)
            }
        }
    }

    if ($missing.Count -gt 0) {
        $allMissing = @{}
        foreach ($mod in $missing.Keys) {
            foreach ($dep in $missing[$mod]) { $allMissing[$dep] = $true }
        }
        $resolvedSelection = @(@($IncludedOptional) + @($allMissing.Keys)) | Sort-Object -Unique
        $lines = @()
        $lines += ""
        $lines += "ERROR: One or more selected modules are missing required dependencies."
        $lines += ""
        foreach ($mod in $missing.Keys) {
            $depList = ($missing[$mod] | Sort-Object) -join ", "
            $lines += "  - '$mod' requires: $depList"
        }
        $lines += ""
        $lines += "Re-run with the full set:"
        $lines += "  -Modules `"$($resolvedSelection -join ',')`""
        $lines += ""
        Write-Error ($lines -join [Environment]::NewLine)
        exit 1
    }
}

function Assert-SelectedModuleArtifacts {
    param(
        [object]$Catalog,
        [string[]]$IncludedOptional,
        [string]$SourceBE,
        [string]$SourceFE,
        [string]$SourceMobile,
        [bool]$IncludeMobile
    )

    $problems = @()

    foreach ($moduleId in @($IncludedOptional)) {
        $module = $Catalog.$moduleId

        if ($module.backendModule) {
            $backendPath = Join-Path (Join-Path (Join-Path $SourceBE "src") "modules") $module.backendModule
            if (-not (Test-Path $backendPath)) {
                $problems += "[$moduleId/backend] Missing template project folder: $backendPath"
            }
        }

        if ($module.frontendFeature) {
            $featurePath = Join-Path (Join-Path (Join-Path $SourceFE "src") "features") $module.frontendFeature
            $indexTs = Join-Path $featurePath "index.ts"
            $indexTsx = Join-Path $featurePath "index.tsx"
            if (-not (Test-Path $featurePath)) {
                $problems += "[$moduleId/web] Missing template feature folder: $featurePath"
            } elseif (-not ((Test-Path $indexTs) -or (Test-Path $indexTsx))) {
                $problems += "[$moduleId/web] Missing feature entrypoint: $indexTs or $indexTsx"
            }
        }

        if ($IncludeMobile -and $module.mobileFolder -and $module.mobileModule) {
            $moduleFile = "$(ConvertTo-SnakeCase -Value $module.mobileModule).dart"
            $mobilePath = Join-Path (Join-Path (Join-Path (Join-Path $SourceMobile "lib") "modules") $module.mobileFolder) $moduleFile
            if (-not (Test-Path $mobilePath)) {
                $problems += "[$moduleId/mobile] Missing mobile entrypoint: $mobilePath"
            }
        }
    }

    if ($problems.Count -gt 0) {
        $lines = @()
        $lines += ""
        $lines += "ERROR: Selected module artifacts are missing from the template."
        $lines += $problems
        $lines += ""
        Write-Error ($lines -join [Environment]::NewLine)
        exit 1
    }
}
```

- [ ] **Step 2: Replace module selection preflight body**

Inside the existing `if (Test-Path $modulesJsonPath) { ... }` block, replace the manual property loop, selection logic, and strict dependency validation with:

```powershell
$modulesConfig = Get-Content $modulesJsonPath -Raw | ConvertFrom-Json
$allOptional = @()

foreach ($prop in Get-CatalogModuleProperties -Catalog $modulesConfig) {
    if ($prop.Value.required) {
        $allRequired += $prop.Name
    } else {
        $allOptional += $prop.Name
    }
}

$selection = Resolve-ModuleSelection -RequestedModules $Modules -AllOptional $allOptional
$includedOptional = @($selection.Included)
$excludedModules = @($selection.Excluded)

Assert-ModuleDependencies -Catalog $modulesConfig -IncludedOptional $includedOptional
Assert-SelectedModuleArtifacts `
    -Catalog $modulesConfig `
    -IncludedOptional $includedOptional `
    -SourceBE $SourceBE `
    -SourceFE $SourceFE `
    -SourceMobile $SourceMobile `
    -IncludeMobile $IncludeMobile
```

- [ ] **Step 3: Verify unknown module fails before copy**

Run:

```bash
pwsh -NoProfile -File scripts/rename.ps1 -Name "BadModulesSmoke" -OutputDir "/private/tmp" -Modules "doesNotExist"
```

Expected: FAIL with:

```text
ERROR: Unknown module id(s): doesNotExist
Valid optional module ids: ai, billing, webhooks, importExport, products, commentsActivity, communication, workflow
```

Also verify `/private/tmp/BadModulesSmoke` was not created.

- [ ] **Step 4: Verify missing dependency still fails before copy**

Run:

```bash
pwsh -NoProfile -File scripts/rename.ps1 -Name "MissingDepsSmoke" -OutputDir "/private/tmp" -Modules "workflow"
```

Expected: FAIL with a message naming `workflow`, `commentsActivity`, and `communication`. Also verify `/private/tmp/MissingDepsSmoke` was not created.

- [ ] **Step 5: Commit**

```bash
git add scripts/rename.ps1
git commit -m "feat(generator): validate module selection before copy"
```

---

## Task 4: Generate Web and Mobile Module Config Files

**Files:**
- Modify: `scripts/rename.ps1`
- Modify: `boilerplateMobile/lib/app/modules.config.dart`
- Modify: `boilerplateMobile/lib/core/modularity/app_module.dart`

- [ ] **Step 1: Add config generation functions**

In `scripts/rename.ps1`, add these functions after `Assert-SelectedModuleArtifacts`:

```powershell
function Write-WebModulesConfig {
    param(
        [object]$Catalog,
        [string[]]$AllOptional,
        [string[]]$IncludedOptional,
        [string]$TargetFE
    )

    if (-not (Test-Path $TargetFE)) { return }

    $imports = @()
    $enabled = @()
    foreach ($moduleId in @($IncludedOptional)) {
        $module = $Catalog.$moduleId
        if (-not $module.frontendFeature) { continue }
        $symbol = Get-WebModuleSymbol -ConfigKey $module.configKey
        $imports += "import { $symbol } from '@/features/$($module.frontendFeature)';"
        $enabled += "  $symbol,"
    }

    # Static ModuleName union: always all catalog ids, regardless of selection.
    $moduleNameUnion = @()
    $allIds = @($AllOptional | ForEach-Object { $Catalog.$_.configKey })
    for ($i = 0; $i -lt $allIds.Count; $i++) {
        $sep = if ($i -eq 0) { '  | ' } else { '  | ' }
        $moduleNameUnion += "$sep'$($allIds[$i])'"
    }

    # Derived activeModules entries — every flag computed via isModuleActive().
    $derivedFlags = @()
    foreach ($id in $allIds) {
        $derivedFlags += "  $($id): isModuleActive('$id'),"
    }

    $contentLines = @()
    $contentLines += $imports
    if ($imports.Count -gt 0) { $contentLines += "" }
    $contentLines += "import { registerWebModules, type WebModule } from '@/lib/modules';"
    $contentLines += ""
    $contentLines += "/**"
    $contentLines += " * Optional module registry generated by rename.ps1 from modules.catalog.json."
    $contentLines += " * Do not hand-edit generated apps; change the catalog or generator instead."
    $contentLines += " *"
    $contentLines += " * enabledModules is the single source of truth. isModuleActive() and the"
    $contentLines += " * activeModules literal are both derived from it, so the two cannot drift."
    $contentLines += " */"
    $contentLines += "export type ModuleName ="
    $contentLines += $moduleNameUnion
    $contentLines += "  ;"
    $contentLines += ""
    $contentLines += "export const enabledModules: WebModule[] = ["
    $contentLines += $enabled
    $contentLines += "];"
    $contentLines += ""
    $contentLines += "const enabledIds = new Set<string>(enabledModules.map((m) => m.id));"
    $contentLines += ""
    $contentLines += "export function isModuleActive(module: ModuleName): boolean {"
    $contentLines += "  return enabledIds.has(module);"
    $contentLines += "}"
    $contentLines += ""
    $contentLines += "export const activeModules: Readonly<Record<ModuleName, boolean>> = Object.freeze({"
    $contentLines += $derivedFlags
    $contentLines += "});"
    $contentLines += ""
    $contentLines += "export function registerAllModules(): void {"
    $contentLines += "  registerWebModules(enabledModules);"
    $contentLines += "}"

    $modulesConfigTsPath = Join-Path (Join-Path (Join-Path $TargetFE "src") "config") "modules.config.ts"
    Set-Content -Path $modulesConfigTsPath -Value ($contentLines -join [Environment]::NewLine) -NoNewline
}

function Write-MobileModulesConfig {
    param(
        [object]$Catalog,
        [string[]]$IncludedOptional,
        [string]$TargetMobile,
        [string]$PackageName,
        [bool]$IncludeMobile
    )

    if (-not $IncludeMobile -or -not (Test-Path $TargetMobile)) { return }

    $imports = @()
    $instances = @()
    foreach ($moduleId in @($IncludedOptional)) {
        $module = $Catalog.$moduleId
        if (-not ($module.mobileFolder -and $module.mobileModule)) { continue }

        $moduleFile = "$(ConvertTo-SnakeCase -Value $module.mobileModule).dart"
        $imports += "import 'package:$PackageName/modules/$($module.mobileFolder)/$moduleFile';"
        $instances += "      $($module.mobileModule)(),"
    }

    $contentLines = @()
    $contentLines += "import 'package:$PackageName/core/modularity/app_module.dart';"
    if ($imports.Count -gt 0) {
        $contentLines += ""
        $contentLines += $imports
    }
    $contentLines += ""
    $contentLines += "/// Optional modules generated by rename.ps1 from modules.catalog.json."
    $contentLines += "/// Do not hand-edit generated apps; change the catalog or generator instead."
    $contentLines += "List<AppModule> activeModules() => <AppModule>["
    $contentLines += $instances
    $contentLines += "    ];"

    $mobileModulesConfigPath = Join-Path (Join-Path (Join-Path $TargetMobile "lib") "app") "modules.config.dart"
    Set-Content -Path $mobileModulesConfigPath -Value ($contentLines -join [Environment]::NewLine) -NoNewline
}
```

- [ ] **Step 2: Call config generation after excluded modules are removed**

At the end of the `if ($null -ne $modulesConfig) { ... }` block, after the `foreach ($moduleKey in $excludedModules)` loop and before the `if ($excludedModules.Count -eq 0 ... )` summary, add:

```powershell
    Write-WebModulesConfig `
        -Catalog $modulesConfig `
        -AllOptional $allOptional `
        -IncludedOptional $includedOptional `
        -TargetFE $TargetFE

    Write-MobileModulesConfig `
        -Catalog $modulesConfig `
        -IncludedOptional $includedOptional `
        -TargetMobile $TargetMobile `
        -PackageName $NameSnake `
        -IncludeMobile $IncludeMobile
```

- [ ] **Step 3: Remove old config surgery and route rewrite**

Inside the excluded-module loop, delete the old blocks that:

- set `activeModules.{configKey}` to `false`
- strip imports from `modules.config.ts`
- strip identifiers from `enabledModules`
- rewrite `routes.tsx` lazy imports to `NotFoundPage`
- strip mobile imports/instances from `modules.config.dart`

Keep folder deletion for backend/web/mobile source folders and backend test folders.

- [ ] **Step 4: Update stale mobile comments**

In `boilerplateMobile/lib/core/modularity/app_module.dart`, change:

```dart
/// `scripts/modules.json`.
```

to:

```dart
/// `modules.catalog.json`.
```

In `boilerplateMobile/lib/app/modules.config.dart`, change:

```dart
/// 4. Add a `mobileModule` / `mobileFolder` entry to `scripts/modules.json`
```

to:

```dart
/// 4. Add a `mobileModule` / `mobileFolder` entry to `modules.catalog.json`
```

- [ ] **Step 5: Verify generated `None` config**

Run:

```bash
pwsh -NoProfile -File scripts/rename.ps1 -Name "Tier2NoneSmoke" -OutputDir "/private/tmp" -Modules "None"
sed -n '1,120p' /private/tmp/Tier2NoneSmoke/Tier2NoneSmoke-FE/src/config/modules.config.ts
sed -n '1,80p' /private/tmp/Tier2NoneSmoke/Tier2NoneSmoke-Mobile/lib/app/modules.config.dart
```

Expected FE config:

```ts
import { registerWebModules, type WebModule } from '@/lib/modules';
```

and `enabledModules: WebModule[] = []`.

Expected mobile config:

```dart
List<AppModule> activeModules() => <AppModule>[
    ];
```

- [ ] **Step 6: Verify generated subset config**

Run:

```bash
pwsh -NoProfile -File scripts/rename.ps1 -Name "Tier2WorkflowSmoke" -OutputDir "/private/tmp" -Modules "workflow,commentsActivity,communication"
sed -n '1,160p' /private/tmp/Tier2WorkflowSmoke/Tier2WorkflowSmoke-FE/src/config/modules.config.ts
```

Expected: imports only `commentsActivityModule`, `communicationModule`, and `workflowModule`; flags for all optional modules with only those three set to `true`.

- [ ] **Step 7: Commit**

```bash
git add scripts/rename.ps1 \
        boilerplateMobile/lib/core/modularity/app_module.dart \
        boilerplateMobile/lib/app/modules.config.dart
git commit -m "feat(generator): emit source module configs"
```

---

## Task 5: Move Optional Web Routes Into Module Contributions

**Files:**
- Modify: optional web module `index.tsx` files
- Modify: `boilerplateFE/src/routes/routes.tsx`
- Modify: `boilerplateFE/src/routes/index.tsx`

- [ ] **Step 1: Add route contributions to module entrypoints**

Update each module entrypoint with its routes. Keep existing slot registrations in the same `register(ctx)` method.

For `boilerplateFE/src/features/import-export/index.tsx`, add:

```tsx
import { lazy } from 'react';
import { PermissionGuard } from '@/components/guards';
import { ROUTES } from '@/config';
import { PERMISSIONS } from '@/constants';
import type { WebModule } from '@/lib/modules';
```

Then inside `register(ctx)` add:

```tsx
ctx.registerRoute({
  id: 'importExport.index',
  region: 'protected',
  order: 40,
  route: {
    element: (
      <PermissionGuard
        permissions={[PERMISSIONS.System.ExportData, PERMISSIONS.System.ImportData]}
        mode="any"
      />
    ),
    children: [
      { path: ROUTES.IMPORT_EXPORT, element: <ImportExportPage /> },
    ],
  },
});
```

with:

```tsx
const ImportExportPage = lazy(() => import('./pages/ImportExportPage'));
```

For `boilerplateFE/src/features/webhooks/index.tsx`, register:

```tsx
const WebhooksPage = lazy(() => import('./pages/WebhooksPage'));
const WebhookAdminPage = lazy(() => import('./pages/WebhookAdminPage'));
const WebhookAdminDetailPage = lazy(() => import('./pages/WebhookAdminDetailPage'));

ctx.registerRoute({
  id: 'webhooks.tenant',
  region: 'protected',
  order: 30,
  route: {
    element: <PermissionGuard permission={PERMISSIONS.Webhooks.View} />,
    children: [
      { path: ROUTES.WEBHOOKS, element: <WebhooksPage /> },
    ],
  },
});

ctx.registerRoute({
  id: 'webhooks.admin',
  region: 'protected',
  order: 31,
  route: {
    element: <PermissionGuard permission={PERMISSIONS.Webhooks.ViewPlatform} />,
    children: [
      { path: ROUTES.WEBHOOKS_ADMIN.LIST, element: <WebhookAdminPage /> },
      { path: ROUTES.WEBHOOKS_ADMIN.DETAIL, element: <WebhookAdminDetailPage /> },
    ],
  },
});
```

For `boilerplateFE/src/features/products/index.tsx`, register:

```tsx
const ProductsListPage = lazy(() => import('./pages/ProductsListPage'));
const ProductCreatePage = lazy(() => import('./pages/ProductCreatePage'));
const ProductDetailPage = lazy(() => import('./pages/ProductDetailPage'));

ctx.registerRoute({
  id: 'products.view',
  region: 'protected',
  order: 50,
  route: {
    element: <PermissionGuard permission={PERMISSIONS.Products.View} />,
    children: [
      { path: ROUTES.PRODUCTS.LIST, element: <ProductsListPage /> },
      { path: ROUTES.PRODUCTS.DETAIL, element: <ProductDetailPage /> },
    ],
  },
});

ctx.registerRoute({
  id: 'products.create',
  region: 'protected',
  order: 51,
  route: {
    element: <PermissionGuard permission={PERMISSIONS.Products.Create} />,
    children: [
      { path: ROUTES.PRODUCTS.CREATE, element: <ProductCreatePage /> },
    ],
  },
});
```

For `boilerplateFE/src/features/workflow/index.tsx`, register:

```tsx
const WorkflowInboxPage = lazy(() => import('./pages/WorkflowInboxPage'));
const WorkflowInstancesPage = lazy(() => import('./pages/WorkflowInstancesPage'));
const WorkflowInstanceDetailPage = lazy(() => import('./pages/WorkflowInstanceDetailPage'));
const WorkflowDefinitionsPage = lazy(() => import('./pages/WorkflowDefinitionsPage'));
const WorkflowDefinitionDetailPage = lazy(() => import('./pages/WorkflowDefinitionDetailPage'));
const WorkflowDefinitionDesignerPage = lazy(() => import('./pages/WorkflowDefinitionDesignerPage'));

ctx.registerRoute({
  id: 'workflow.instances',
  region: 'protected',
  order: 60,
  route: {
    element: <PermissionGuard permission={PERMISSIONS.Workflows.View} />,
    children: [
      { path: ROUTES.WORKFLOWS.INBOX, element: <WorkflowInboxPage /> },
      { path: ROUTES.WORKFLOWS.INSTANCES, element: <WorkflowInstancesPage /> },
      { path: ROUTES.WORKFLOWS.INSTANCE_DETAIL, element: <WorkflowInstanceDetailPage /> },
    ],
  },
});

ctx.registerRoute({
  id: 'workflow.definitions',
  region: 'protected',
  order: 61,
  route: {
    element: <PermissionGuard permission={PERMISSIONS.Workflows.ManageDefinitions} />,
    children: [
      { path: ROUTES.WORKFLOWS.DEFINITIONS, element: <WorkflowDefinitionsPage /> },
      { path: ROUTES.WORKFLOWS.DEFINITION_DETAIL, element: <WorkflowDefinitionDetailPage /> },
      { path: ROUTES.WORKFLOWS.DEFINITION_DESIGNER, element: <WorkflowDefinitionDesignerPage /> },
    ],
  },
});
```

For `boilerplateFE/src/features/communication/index.tsx`, register:

```tsx
const ChannelsPage = lazy(() => import('./pages/ChannelsPage'));
const TemplatesPage = lazy(() => import('./pages/TemplatesPage'));
const TriggerRulesPage = lazy(() => import('./pages/TriggerRulesPage'));
const IntegrationsPage = lazy(() => import('./pages/IntegrationsPage'));
const DeliveryLogPage = lazy(() => import('./pages/DeliveryLogPage'));

ctx.registerRoute({
  id: 'communication.main',
  region: 'protected',
  order: 70,
  route: {
    element: <PermissionGuard permission={PERMISSIONS.Communication.View} />,
    children: [
      { path: ROUTES.COMMUNICATION.CHANNELS, element: <ChannelsPage /> },
      { path: ROUTES.COMMUNICATION.TEMPLATES, element: <TemplatesPage /> },
      { path: ROUTES.COMMUNICATION.TRIGGER_RULES, element: <TriggerRulesPage /> },
      { path: ROUTES.COMMUNICATION.INTEGRATIONS, element: <IntegrationsPage /> },
    ],
  },
});

ctx.registerRoute({
  id: 'communication.deliveryLog',
  region: 'protected',
  order: 71,
  route: {
    element: <PermissionGuard permission={PERMISSIONS.Communication.ViewDeliveryLog} />,
    children: [
      { path: ROUTES.COMMUNICATION.DELIVERY_LOG, element: <DeliveryLogPage /> },
    ],
  },
});
```

For `boilerplateFE/src/features/billing/index.tsx`, register:

```tsx
const PricingPage = lazy(() => import('./pages/PricingPage'));
const BillingPage = lazy(() => import('./pages/BillingPage'));
const BillingPlansPage = lazy(() => import('./pages/BillingPlansPage'));
const SubscriptionsPage = lazy(() => import('./pages/SubscriptionsPage'));
const SubscriptionDetailPage = lazy(() => import('./pages/SubscriptionDetailPage'));

ctx.registerRoute({
  id: 'billing.pricing',
  region: 'public',
  order: 20,
  route: { path: ROUTES.PRICING, element: <PricingPage /> },
});

ctx.registerRoute({
  id: 'billing.tenant',
  region: 'protected',
  order: 80,
  route: {
    element: <PermissionGuard permission={PERMISSIONS.Billing.View} />,
    children: [
      { path: ROUTES.BILLING, element: <BillingPage /> },
    ],
  },
});

ctx.registerRoute({
  id: 'billing.plans',
  region: 'protected',
  order: 81,
  route: {
    element: <PermissionGuard permission={PERMISSIONS.Billing.ViewPlans} />,
    children: [
      { path: ROUTES.BILLING_PLANS, element: <BillingPlansPage /> },
    ],
  },
});

ctx.registerRoute({
  id: 'billing.subscriptions',
  region: 'protected',
  order: 82,
  route: {
    element: <PermissionGuard permission={PERMISSIONS.Billing.ManageTenantSubscriptions} />,
    children: [
      { path: ROUTES.SUBSCRIPTIONS.LIST, element: <SubscriptionsPage /> },
      { path: ROUTES.SUBSCRIPTIONS.DETAIL, element: <SubscriptionDetailPage /> },
    ],
  },
});
```

- [ ] **Step 2: Convert routes to lazy route factory**

In `boilerplateFE/src/routes/routes.tsx`:

1. Remove `activeModules` import.
2. Remove every optional module page lazy import.
3. Add:

```ts
import { getModuleRoutes } from '@/lib/modules';
```

4. Change:

```ts
export const routes: RouteObject[] = [
```

to:

```ts
export function createRoutes(): RouteObject[] {
  return [
```

5. In the public layout children, replace the pricing conditional with:

```tsx
      ...getModuleRoutes('public'),
```

6. In the protected `MainLayout` children, delete the whole `// ── Module routes` section and replace it with:

```tsx
          // Module routes registered by active optional modules.
          ...getModuleRoutes('protected'),
```

7. Close the function at the end:

```tsx
  ];
}
```

- [ ] **Step 3: Create router after module bootstrap**

Replace `boilerplateFE/src/routes/index.tsx` with a module-level lazy router. `createBrowserRouter` owns history listeners and is intended to live outside React render — but it must not run at the top-level of this file because static imports execute before `main.tsx` calls `registerAllModules()`. A module-level lazy cache builds the router exactly once on the first `<AppRouter />` render (which happens after `main.tsx` has run):

```tsx
import { createBrowserRouter, RouterProvider } from 'react-router-dom';
import { createRoutes } from './routes';

let cachedRouter: ReturnType<typeof createBrowserRouter> | null = null;

function getRouter() {
  if (!cachedRouter) {
    cachedRouter = createBrowserRouter(createRoutes());
  }
  return cachedRouter;
}

export function AppRouter() {
  return <RouterProvider router={getRouter()} />;
}
```

This avoids `useMemo` (which is documented as a hint, not a guarantee) and keeps the router instance stable across remounts/HMR.

- [ ] **Step 4: Verify routes contain no optional imports**

Run:

```bash
rg -n "@/features/(billing|webhooks|import-export|products|comments-activity|communication|workflow)" boilerplateFE/src/routes boilerplateFE/src/components/layout
```

Expected at this checkpoint: routes should have zero matches; layout still has matches only through nav logic until Task 6.

- [ ] **Step 5: Build frontend**

Run:

```bash
cd boilerplateFE
npm run build
```

Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add boilerplateFE/src/features \
        boilerplateFE/src/routes/routes.tsx \
        boilerplateFE/src/routes/index.tsx
git commit -m "feat(web-modules): compose routes from active modules"
```

---

## Task 6: Move Optional Web Navigation Into Module Contributions

**Files:**
- Create: `boilerplateFE/src/features/workflow/components/WorkflowPendingTaskBadge.tsx`
- Modify: optional web module `index.tsx` files
- Modify: `boilerplateFE/src/components/layout/MainLayout/useNavGroups.ts`
- Modify: `boilerplateFE/src/components/layout/MainLayout/Sidebar.tsx`
- Modify: `boilerplateFE/src/components/layout/MainLayout/MorePanel.tsx`

- [ ] **Step 1: Add Workflow badge component**

Create `boilerplateFE/src/features/workflow/components/WorkflowPendingTaskBadge.tsx`:

```tsx
import { useQuery } from '@tanstack/react-query';
import { API_ENDPOINTS } from '@/config/api.config';
import { apiClient } from '@/lib/axios';
import { queryKeys } from '@/lib/query/keys';
import type { ApiResponse } from '@/types/api.types';

export function WorkflowPendingTaskBadge() {
  const { data = 0 } = useQuery({
    queryKey: queryKeys.workflow.tasks.count(),
    queryFn: () =>
      apiClient
        .get<ApiResponse<number>>(API_ENDPOINTS.WORKFLOW.TASKS_COUNT)
        .then((r) => r.data.data),
  });

  if (data <= 0) return null;
  return (
    <span className="flex h-5 min-w-5 items-center justify-center rounded-full btn-primary-gradient glow-primary-sm px-1.5 text-[10px] font-bold text-primary-foreground font-mono">
      {data > 99 ? '99+' : data}
    </span>
  );
}
```

- [ ] **Step 2: Register nav groups in module entrypoints**

Add nav group registration inside each module's existing `register(ctx)` method.

Workflow:

```tsx
import { ClipboardCheck, GitBranch, History } from 'lucide-react';
import { WorkflowPendingTaskBadge } from './components/WorkflowPendingTaskBadge';

ctx.registerNavGroup({
  id: 'workflow',
  order: 20,
  build(nav) {
    const items = [];

    if (nav.hasPermission(PERMISSIONS.Workflows.View)) {
      items.push({
        label: nav.t('workflow.sidebar.taskInbox'),
        icon: ClipboardCheck,
        path: ROUTES.WORKFLOWS.INBOX,
        Badge: WorkflowPendingTaskBadge,
      });
      items.push({
        label: nav.t('workflow.sidebar.history'),
        icon: History,
        path: ROUTES.WORKFLOWS.INSTANCES,
      });
    }

    if (nav.hasPermission(PERMISSIONS.Workflows.ManageDefinitions)) {
      items.push({
        label: nav.t('workflow.sidebar.definitions'),
        icon: GitBranch,
        path: ROUTES.WORKFLOWS.DEFINITIONS,
      });
    }

    return { label: nav.t('nav.groups.workflow'), items };
  },
});
```

Communication:

```tsx
import { FileText, Link2, MessageSquare, ScrollText, Zap } from 'lucide-react';

ctx.registerNavGroup({
  id: 'communication',
  order: 30,
  build(nav) {
    if (!nav.tenantScoped) return null;

    const items = [];
    if (nav.hasPermission(PERMISSIONS.Communication.View)) {
      items.push({ label: nav.t('nav.channels'), icon: MessageSquare, path: ROUTES.COMMUNICATION.CHANNELS });
      items.push({ label: nav.t('nav.templates'), icon: FileText, path: ROUTES.COMMUNICATION.TEMPLATES });
      items.push({ label: nav.t('nav.triggerRules'), icon: Zap, path: ROUTES.COMMUNICATION.TRIGGER_RULES });
      items.push({ label: nav.t('nav.integrations'), icon: Link2, path: ROUTES.COMMUNICATION.INTEGRATIONS });
    }
    if (nav.hasPermission(PERMISSIONS.Communication.ViewDeliveryLog)) {
      items.push({ label: nav.t('nav.deliveryLog'), icon: ScrollText, path: ROUTES.COMMUNICATION.DELIVERY_LOG });
    }

    return { label: nav.t('nav.groups.communication'), items };
  },
});
```

Products:

```tsx
import { Package } from 'lucide-react';

ctx.registerNavGroup({
  id: 'products',
  order: 40,
  build(nav) {
    const items = nav.hasPermission(PERMISSIONS.Products.View)
      ? [{ label: nav.t('nav.products', 'Products'), icon: Package, path: ROUTES.PRODUCTS.LIST }]
      : [];

    return { label: nav.t('nav.groups.products'), items };
  },
});
```

Billing:

```tsx
import { CreditCard, ListChecks, ReceiptText } from 'lucide-react';

ctx.registerNavGroup({
  id: 'billing',
  order: 50,
  build(nav) {
    const items = [];
    if (nav.hasPermission(PERMISSIONS.Billing.View) && nav.tenantScoped) {
      items.push({ label: nav.t('nav.billing'), icon: CreditCard, path: ROUTES.BILLING, end: true });
    }
    if (nav.hasPermission(PERMISSIONS.Billing.ViewPlans)) {
      items.push({ label: nav.t('nav.billingPlans'), icon: ReceiptText, path: ROUTES.BILLING_PLANS });
    }
    if (nav.hasPermission(PERMISSIONS.Billing.ManageTenantSubscriptions)) {
      items.push({ label: nav.t('nav.subscriptions'), icon: ListChecks, path: ROUTES.SUBSCRIPTIONS.LIST, end: true });
    }

    return { label: nav.t('nav.groups.billing'), items };
  },
});
```

Webhooks tenant nav:

```tsx
import { Webhook } from 'lucide-react';

ctx.registerNavGroup({
  id: 'webhooks',
  order: 60,
  build(nav) {
    if (!nav.tenantScoped || !nav.isFeatureEnabled('webhooks.enabled')) return null;

    const items = nav.hasPermission(PERMISSIONS.Webhooks.View)
      ? [{ label: nav.t('nav.webhooks'), icon: Webhook, path: ROUTES.WEBHOOKS }]
      : [];

    return { label: nav.t('nav.groups.webhooks'), items };
  },
});
```

Import/export:

```tsx
import { ArrowLeftRight } from 'lucide-react';

ctx.registerNavGroup({
  id: 'importExport',
  order: 70,
  build(nav) {
    const canExport = nav.hasPermission(PERMISSIONS.System.ExportData) && nav.isFeatureEnabled('exports.enabled');
    const canImport = nav.hasPermission(PERMISSIONS.System.ImportData) && nav.isFeatureEnabled('imports.enabled');
    const items = canExport || canImport
      ? [{ label: nav.t('nav.importExport'), icon: ArrowLeftRight, path: ROUTES.IMPORT_EXPORT }]
      : [];

    return { label: nav.t('nav.groups.importExport'), items };
  },
});
```

Webhooks platform-admin link extends core's existing `'platform'` nav group instead of producing a second sibling group with the same label. Use `registerNavItem` so the item merges into core's Platform list:

```tsx
ctx.registerNavItem('platform', {
  id: 'webhooks.admin',
  order: 90,
  build(nav) {
    if (!nav.hasPermission(PERMISSIONS.Webhooks.ViewPlatform)) return null;
    return {
      label: nav.t('nav.webhooksAdmin'),
      icon: Webhook,
      path: ROUTES.WEBHOOKS_ADMIN.LIST,
      end: true,
    };
  },
});
```

- [ ] **Step 3: Clean core nav hook**

In `boilerplateFE/src/components/layout/MainLayout/useNavGroups.ts`:

1. Remove imports used only by optional modules:

```ts
ArrowLeftRight,
ClipboardCheck,
CreditCard,
FileText,
GitBranch,
History,
Link2,
ListChecks,
MessageSquare,
Package,
ReceiptText,
ScrollText,
Webhook,
Zap,
```

2. Remove:

```ts
import { useQuery } from '@tanstack/react-query';
import { API_ENDPOINTS } from '@/config/api.config';
import { activeModules, isModuleActive } from '@/config/modules.config';
import { apiClient } from '@/lib/axios';
import { queryKeys } from '@/lib/query/keys';
import type { ApiResponse } from '@/types/api.types';
```

3. Add:

```ts
import {
  getModuleNavGroups,
  getModuleNavItems,
  type ModuleNavContext,
  type ModuleNavGroup,
  type ModuleNavItem,
} from '@/lib/modules';
```

4. Change local type aliases:

```ts
export type SidebarNavItem = ModuleNavItem;
export type SidebarNavGroup = ModuleNavGroup;
```

5. Delete `useWorkflowPendingTaskCount`.

6. Delete all optional module nav blocks from Workflow through Import/Export.

7. Build the shared `ModuleNavContext` once near the top of `useNavGroups()` after the existing inputs (permissions, role, feature flags) are available:

```ts
  const navCtx: ModuleNavContext = {
    t,
    hasPermission,
    tenantScoped,
    isFeatureEnabled: (key) => {
      if (key === 'webhooks.enabled') return webhooksFlag.isEnabled;
      if (key === 'imports.enabled') return importsFlag.isEnabled;
      if (key === 'exports.enabled') return exportsFlag.isEnabled;
      return false;
    },
  };
```

8. After the top group is pushed and before the People group, append all module-contributed groups:

```ts
  groups.push(...getModuleNavGroups(navCtx));
```

   Module ordering inside the sidebar is owned by each module via the `order` field on its group contribution. `useNavGroups` does not gate on `order` ranges — it just delegates ordering to the registry.

9. In the Platform block, remove the hardcoded webhooks admin item. Where the existing block builds the Platform group's items, append module-contributed items before pushing the group:

```ts
    items.push(...getModuleNavItems('platform', navCtx));
    if (items.length > 0) {
      groups.push({ id: 'platform', label: t('nav.groups.platform'), items });
    }
```

   The `'platform'` group id is part of the `CoreNavGroupId` union and is the only addressable extension point for now. People/Content/Top can be added to that union later if a module needs to extend them.

- [ ] **Step 4: Render module badge components in sidebar**

In `boilerplateFE/src/components/layout/MainLayout/Sidebar.tsx`, replace the numeric badge rendering block with:

```tsx
                          {!isCollapsed && item.Badge && <item.Badge />}
                          {!isCollapsed && !item.Badge && item.badge != null && (
                            <span className="flex h-5 min-w-5 items-center justify-center rounded-full btn-primary-gradient glow-primary-sm px-1.5 text-[10px] font-bold text-primary-foreground font-mono">
                              {item.badge > 99 ? '99+' : item.badge}
                            </span>
                          )}
```

- [ ] **Step 5: Render module badge components in overflow panel**

In `boilerplateFE/src/components/layout/MainLayout/MorePanel.tsx`, replace the numeric badge rendering block with:

```tsx
                        {item.Badge && <item.Badge />}
                        {!item.Badge && item.badge != null && (
                          <span className="flex h-5 min-w-5 items-center justify-center rounded-full btn-primary-gradient glow-primary-sm px-1.5 text-[10px] font-bold text-primary-foreground font-mono">
                            {item.badge > 99 ? '99+' : item.badge}
                          </span>
                        )}
```

- [ ] **Step 6: Verify layout has no optional imports**

Run:

```bash
rg -n "@/features/(billing|webhooks|import-export|products|comments-activity|communication|workflow)" boilerplateFE/src/routes boilerplateFE/src/components/layout
```

Expected: no matches.

- [ ] **Step 7: Verify i18n keys used by nav contributions exist**

The new module nav contributions reference translation keys. Missing keys silently render as the literal key string in the UI, so check before build. Run from `boilerplateFE/`:

```bash
KEYS=(
  # workflow
  'workflow.sidebar.taskInbox' 'workflow.sidebar.history' 'workflow.sidebar.definitions'
  # communication
  'nav.channels' 'nav.templates' 'nav.triggerRules' 'nav.integrations' 'nav.deliveryLog'
  # billing
  'nav.billing' 'nav.billingPlans' 'nav.subscriptions'
  # webhooks
  'nav.webhooks' 'nav.webhooksAdmin'
  # import/export
  'nav.importExport'
  # group labels
  'nav.groups.workflow' 'nav.groups.communication' 'nav.groups.products'
  'nav.groups.billing' 'nav.groups.webhooks' 'nav.groups.importExport'
  'nav.groups.platform'
)

for locale in en ar; do
  for key in "${KEYS[@]}"; do
    if ! rg -q "\"${key##*.}\"" "src/i18n/locales/${locale}/translation.json"; then
      echo "MISSING [${locale}]: ${key}"
    fi
  done
done
```

Expected: no `MISSING ...` output. (`nav.products` is excluded from this check because the `t()` call uses an inline fallback `'Products'`.)

If any key is missing, add it to `boilerplateFE/src/i18n/locales/en/translation.json` and `boilerplateFE/src/i18n/locales/ar/translation.json` before continuing. The check is a coarse leaf-name match — open the file and confirm the leaf actually lives under the expected path before declaring it present.

- [ ] **Step 8: Build frontend**

Run:

```bash
cd boilerplateFE
npm run build
```

Expected: PASS.

- [ ] **Step 9: Commit**

```bash
git add boilerplateFE/src/features \
        boilerplateFE/src/components/layout/MainLayout/useNavGroups.ts \
        boilerplateFE/src/components/layout/MainLayout/Sidebar.tsx \
        boilerplateFE/src/components/layout/MainLayout/MorePanel.tsx
git commit -m "feat(web-modules): compose navigation from active modules"
```

---

## Task 7: Tighten Frontend Import Guardrails

**Files:**
- Modify: `boilerplateFE/eslint.config.js`

- [ ] **Step 1: Expand optional feature restrictions**

In `boilerplateFE/eslint.config.js`, replace the restricted import group with:

```js
            group: [
              '@/features/billing',
              '@/features/billing/*',
              '@/features/webhooks',
              '@/features/webhooks/*',
              '@/features/import-export',
              '@/features/import-export/*',
              '@/features/products',
              '@/features/products/*',
              '@/features/comments-activity',
              '@/features/comments-activity/*',
              '@/features/communication',
              '@/features/communication/*',
              '@/features/workflow',
              '@/features/workflow/*',
            ],
            message: 'Do not import optional module features from core. Register routes, nav, slots, and capabilities through src/config/modules.config.ts and src/lib/modules instead.',
```

- [ ] **Step 2: Update the allowlist files block**

Today's allowlist only covers 3 module folders (`billing`, `webhooks`, `import-export`); the other 4 module folders need to be added so internal imports inside `products/`, `comments-activity/`, `communication/`, and `workflow/` still pass lint after the restricted-imports group is expanded in Step 1. Also remove `src/routes/routes.tsx` (Tasks 5–6 ensure routes.tsx no longer needs to import optional features).

Replace the allowlist `files` array with:

```js
files: [
  'src/features/billing/**',
  'src/features/webhooks/**',
  'src/features/import-export/**',
  'src/features/products/**',
  'src/features/comments-activity/**',
  'src/features/communication/**',
  'src/features/workflow/**',
  'src/config/modules.config.ts',
  'src/app/main.tsx',
],
```

Do **not** use a `src/features/*/**` wildcard — that would let any feature folder (including core ones) import any optional module folder, which is exactly the boundary this rule is meant to guard. The explicit per-module list keeps the boundary tight.

- [ ] **Step 3: Run lint**

Run:

```bash
cd boilerplateFE
npm run lint
```

Expected: PASS. If this fails on `routes.tsx` or layout importing optional features, go back to Tasks 5-6 and remove the import.

- [ ] **Step 4: Commit**

```bash
git add boilerplateFE/eslint.config.js
git commit -m "test(web-modules): guard optional feature imports"
```

---

## Task 8: Add Mobile Duplicate Guard and Import Guard

**Files:**
- Modify: `boilerplateMobile/lib/core/modularity/module_registry.dart`
- Modify: `boilerplateMobile/test/core/modularity/module_registry_test.dart`
- Create: `boilerplateMobile/test/core/modularity/module_import_guard_test.dart`

- [ ] **Step 1: Add duplicate module validation**

In `boilerplateMobile/lib/core/modularity/module_registry.dart`, at the start of `_topologicalSort`, after `final byName = <String, AppModule>{};`, replace the loop with:

```dart
    for (final m in modules) {
      if (byName.containsKey(m.name)) {
        throw StateError(
          'Duplicate module id "${m.name}" detected. Module ids must match '
          'modules.catalog.json and be unique in activeModules().',
        );
      }
      byName[m.name] = m;
    }
```

- [ ] **Step 2: Add duplicate module test**

In `boilerplateMobile/test/core/modularity/module_registry_test.dart`, add this test after `throws on missing dependency`:

```dart
    test('throws on duplicate module id', () {
      expect(
        () => registry.init([_FakeModuleA(), _FakeModuleA()], sl),
        throwsA(
          isA<StateError>().having(
            (e) => e.message,
            'message',
            contains('Duplicate module id "module_a"'),
          ),
        ),
      );
    });
```

- [ ] **Step 3: Add mobile import guard test**

Create `boilerplateMobile/test/core/modularity/module_import_guard_test.dart`:

```dart
import 'dart:io';

import 'package:flutter_test/flutter_test.dart';

void main() {
  test('core mobile code does not import optional module folders directly', () {
    final libDir = Directory('lib');
    final offenders = <String>[];

    for (final entity in libDir.listSync(recursive: true)) {
      if (entity is! File || !entity.path.endsWith('.dart')) continue;

      final normalized = entity.path.replaceAll('\\', '/');
      if (normalized == 'lib/app/modules.config.dart') continue;
      if (normalized.startsWith('lib/modules/')) continue;

      final content = entity.readAsStringSync();
      final importsModule = RegExp(
        r"import\s+'package:[^']+/modules/",
      ).hasMatch(content);

      if (importsModule) {
        offenders.add(normalized);
      }
    }

    expect(
      offenders,
      isEmpty,
      reason: 'Optional mobile modules must be imported only from '
          'lib/app/modules.config.dart or from inside lib/modules/**.',
    );
  });
}
```

- [ ] **Step 4: Run mobile tests**

Run:

```bash
cd boilerplateMobile
flutter test test/core/modularity/module_registry_test.dart test/core/modularity/module_import_guard_test.dart
```

Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add boilerplateMobile/lib/core/modularity/module_registry.dart \
        boilerplateMobile/test/core/modularity/module_registry_test.dart \
        boilerplateMobile/test/core/modularity/module_import_guard_test.dart
git commit -m "test(mobile-modules): guard module registry boundaries"
```

---

## Task 9: Update Module Documentation References

**Files:**
- Modify: `docs/architecture/module-development.md`
- Modify: `docs/architecture/domain-module-example.md`
- Modify: `boilerplateMobile/lib/app/modules.config.dart`
- Modify: `boilerplateMobile/lib/core/modularity/app_module.dart`

- [ ] **Step 1: Find stale current-guidance references**

Run:

```bash
rg -n "scripts/modules\\.json" docs boilerplateMobile boilerplateFE scripts modules.catalog.json
```

Expected: references remain in older session history docs and maybe current architecture docs.

- [ ] **Step 2: Update current guidance docs**

In `docs/architecture/module-development.md`, replace current guidance references:

```md
scripts/modules.json
```

with:

```md
modules.catalog.json
```

In `docs/architecture/domain-module-example.md`, replace current guidance references the same way.

Do not rewrite old testing session history unless the wording is presented as current instructions. Historical notes can remain historical.

- [ ] **Step 3: Re-run stale reference scan**

Run:

```bash
rg -n "scripts/modules\\.json" docs/architecture boilerplateMobile boilerplateFE scripts
```

Expected: no matches in current architecture docs, mobile code comments, FE code, or scripts.

- [ ] **Step 4: Commit**

```bash
git add docs/architecture/module-development.md \
        docs/architecture/domain-module-example.md \
        boilerplateMobile/lib/app/modules.config.dart \
        boilerplateMobile/lib/core/modularity/app_module.dart
git commit -m "docs(modules): point guidance at root catalog"
```

---

## Task 10: Source-Mode Killer Smoke Checks

**Files:**
- No source changes expected unless a smoke check exposes a bug.

**Performance note:** This task generates three apps and runs `npm install` for each — on a cold cache that's ~2 min × 3 = 6+ min of dependency download. To amortize, point npm at the host repo's existing cache before starting:

```bash
# Prime npm to use host cache (one-time per shell)
export NPM_CONFIG_CACHE="$HOME/.npm"
# Or for any single command, append `--prefer-offline` to npm install:
#   npm install --prefer-offline
```

`flutter pub get` similarly benefits from a warm `~/.pub-cache`; it's already shared by default. `dotnet build` uses the global NuGet cache automatically.

- [ ] **Step 1: Generate no-module app**

Run:

```bash
pwsh -NoProfile -File scripts/rename.ps1 -Name "Tier2None" -OutputDir "/private/tmp" -Modules "None"
```

Expected:

- backend generated at `/private/tmp/Tier2None/Tier2None-BE`
- frontend generated at `/private/tmp/Tier2None/Tier2None-FE`
- mobile generated at `/private/tmp/Tier2None/Tier2None-Mobile`
- generated FE `modules.config.ts` imports no optional features
- generated mobile `modules.config.dart` imports no optional modules
- final script warning has no leftover `Starter` references

- [ ] **Step 2: Build no-module backend**

Run:

```bash
dotnet build /private/tmp/Tier2None/Tier2None-BE/Tier2None.sln
```

Expected: PASS.

- [ ] **Step 3: Check no-module frontend**

Run:

```bash
cd /private/tmp/Tier2None/Tier2None-FE
npm install
npm run build
npm run lint
```

Expected: PASS. If `npm install` is not available in the environment, run `npm run build` only if `node_modules` was copied or installed; record the limitation.

- [ ] **Step 4: Check no-module mobile**

Run:

```bash
cd /private/tmp/Tier2None/Tier2None-Mobile
flutter pub get
flutter test test/core/modularity/module_registry_test.dart test/core/modularity/module_import_guard_test.dart
```

Expected: PASS. If Flutter SDK is unavailable, record the limitation and at least inspect generated `modules.config.dart`.

- [ ] **Step 5: Generate workflow subset app**

Run:

```bash
pwsh -NoProfile -File scripts/rename.ps1 -Name "Tier2Workflow" -OutputDir "/private/tmp" -Modules "workflow,commentsActivity,communication"
```

Expected:

- FE config imports only `commentsActivityModule`, `communicationModule`, `workflowModule`
- backend removed unrelated optional module projects
- `workflow`, `commentsActivity`, and `communication` remain

- [ ] **Step 6: Build workflow subset backend**

Run:

```bash
dotnet build /private/tmp/Tier2Workflow/Tier2Workflow-BE/Tier2Workflow.sln
```

Expected: PASS.

- [ ] **Step 7: Check workflow subset frontend**

Run:

```bash
cd /private/tmp/Tier2Workflow/Tier2Workflow-FE
npm install
npm run build
npm run lint
```

Expected: PASS.

- [ ] **Step 8: Generate all-module app**

Run:

```bash
pwsh -NoProfile -File scripts/rename.ps1 -Name "Tier2All" -OutputDir "/private/tmp" -Modules "All"
```

Expected: all optional modules remain. FE config imports every optional web module except catalog entries without `frontendFeature` such as `ai`.

- [ ] **Step 9: Build all-module solution**

Run:

```bash
dotnet build /private/tmp/Tier2All/Tier2All-BE/Tier2All.sln
cd /private/tmp/Tier2All/Tier2All-FE
npm install
npm run build
npm run lint
```

Expected: PASS.

- [ ] **Step 10: Fix bugs found by smoke checks**

If any smoke check fails, fix only the narrow cause. Typical expected fixes:

- missing generated import symbol in `modules.config.ts`
- stale direct optional import in routes/nav
- stale project reference after module removal
- generated mobile import path mismatch

After the fix, rerun the failed smoke command and the nearest native check (`dotnet test`, `npm run build`, `flutter test`) before continuing.

- [ ] **Step 11: Commit final smoke fixes**

If fixes were needed:

```bash
git add scripts/rename.ps1 boilerplateFE boilerplateMobile boilerplateBE docs
git commit -m "fix(modules): pass source composition smoke checks"
```

If no fixes were needed, do not create an empty commit.

---

## Task 11: Final Verification

**Files:**
- No source changes expected.

- [ ] **Step 1: Run backend architecture tests**

```bash
dotnet test boilerplateBE/tests/Starter.Api.Tests/Starter.Api.Tests.csproj --filter "CatalogConsistencyTests|ModuleIsolationTests|ModuleLoaderTests"
```

Expected: PASS.

- [ ] **Step 2: Run frontend checks**

```bash
cd boilerplateFE
npm run build
npm run lint
```

Expected: PASS.

- [ ] **Step 3: Run mobile module checks**

```bash
cd boilerplateMobile
flutter test test/core/modularity/module_registry_test.dart test/core/modularity/module_import_guard_test.dart
```

Expected: PASS.

- [ ] **Step 4: Verify no core optional imports remain**

Run from repo root:

```bash
rg -n "@/features/(billing|webhooks|import-export|products|comments-activity|communication|workflow)" boilerplateFE/src/routes boilerplateFE/src/components/layout
rg -n "package:boilerplate_mobile/modules/" boilerplateMobile/lib/app boilerplateMobile/lib/core
```

Expected:

- first command: no matches
- second command: only `boilerplateMobile/lib/app/modules.config.dart`

- [ ] **Step 5: Final status**

```bash
git status --short
```

Expected: clean.

---

## Completion Notes

After implementation, summarize:

- which smoke scenarios passed
- whether any checks were skipped due to local tooling availability
- which direct optional imports were eliminated
- any known deferred items for package-mode Tier 3

Do not claim package readiness. Tier 2 proves catalog-driven source composition only.
