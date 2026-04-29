# Catalog-Driven Source Composition Design

**Date:** 2026-04-29
**Status:** Draft for user review
**Tier:** Tier 2 after `feat(modules): Tier 1 - hybrid module system foundation`
**Scope:** Source-mode module composition across backend, React web, Flutter mobile, catalog-driven generation, guardrails, and smoke verification.

---

## 1. Context

Tier 1 moved the module system in the right direction:

- `modules.catalog.json` is now the top-level module catalog.
- Backend module discovery and ordering fail loudly for missing hard runtime dependencies.
- `Starter.Api` no longer needs to call Workflow-specific MassTransit extensions directly.
- `IModuleBusContributor` gives modules a focused backend host extension point.
- The catalog distinguishes composition-time dependencies from hard runtime module dependencies.

The remaining modularity gap is now mostly source-mode composition:

- React `routes.tsx` still imports optional feature pages directly.
- React navigation still needs a first-class module contribution path.
- Frontend and mobile config files are hand-maintained instead of generated from the catalog.
- Mobile is close to the right registry model, but docs and guardrails still assume older catalog paths.
- `rename.ps1` is still both the generator and the composition engine, which is acceptable for now but should not become the permanent architecture.

Tier 2 should finish the source-mode story before package distribution work begins.

---

## 2. Goals

1. **Catalog-driven source composition:** selected module ids from `modules.catalog.json` determine generated backend, web, and mobile module configuration.
2. **Clean React core:** React routes, sidebar/navigation, and shell code do not import optional feature internals outside generated module config.
3. **Clean mobile shell:** Flutter shell/core code does not import optional modules outside generated `lib/app/modules.config.dart`.
4. **Generated-app identity safety:** generated apps use the renamed app identity, not leaked template-only `Starter` assumptions.
5. **Early failure:** invalid module ids, missing dependencies, duplicate contributions, and missing platform artifacts fail before or during startup with actionable messages.
6. **Future generator extraction:** composition logic is shaped so it can later move from `rename.ps1` into a CLI or desktop generator without changing module contracts.
7. **Killer-test confidence:** source-mode generated apps build with all optional modules, no optional modules, and representative subsets.

---

## 3. Non-Goals

- No NuGet/npm/pub package distribution in Tier 2.
- No license enforcement.
- No runtime marketplace or dynamic untrusted module loading.
- No new backend contributor interfaces unless a real second use case appears.
- No full permission-code generation across web/mobile yet.
- No rewrite of every module UI. Existing modules migrate to the new contribution contract incrementally inside this tier.

---

## 4. Core Decision

Tier 2 uses **catalog-driven source composition**.

`modules.catalog.json` stays the source of truth for optional module identity and platform artifacts. `rename.ps1` remains the first implementation vehicle, but the module composition logic should be isolated behind generator-like functions:

- read catalog
- validate requested module ids
- resolve dependency rules
- validate platform artifacts
- emit platform module config files
- remove excluded module source and tests

The generated application should consume stable module contracts. It should not care that `rename.ps1` produced the config today. Later, a CLI or desktop generator can call the same conceptual composer and produce the same outputs.

---

## 5. Template vs Generated App Boundary

`Starter.*` is the boilerplate/template identity, not the generated application identity.

Tier 2 must make this boundary explicit:

- Template files can reference `Starter.*` because they live in the boilerplate.
- Generated apps must use the renamed project identity in namespaces, package names, import paths, docs snippets copied into the app, module config files, and build metadata.
- The catalog describes template artifacts, such as `Starter.Module.Products`, because the composer starts from the template.
- The composer is responsible for translating template artifact names into generated-app-safe output.
- Any hardcoded `Starter` assumptions inside generation logic must live in named template constants or helper functions, not scattered through module composition code.

This keeps the current script honest and protects the later move to a CLI or desktop generator.

---

## 6. Catalog Shape

The existing catalog keys remain valid:

```json
{
  "products": {
    "displayName": "Products",
    "backendModule": "Starter.Module.Products",
    "frontendFeature": "products",
    "mobileModule": null,
    "mobileFolder": null,
    "configKey": "products",
    "required": false,
    "dependencies": []
  }
}
```

Tier 2 treats these fields as generation inputs:

- `backendModule`: template backend project and namespace stem.
- `frontendFeature`: template web feature folder under `src/features`.
- `mobileModule`: Dart class instantiated in generated `modules.config.dart`.
- `mobileFolder`: template mobile module folder under `lib/modules`.
- `configKey`: stable generated config key.
- `dependencies`: composition-time dependency ids, not backend hard runtime dependency names.
- `required`: whether the module is always shipped and ignored by optional selection.

Tier 2 may keep the flat schema. A nested schema can wait until package distribution begins.

---

## 7. Source-Mode Composer

`rename.ps1` should evolve from deletion script to source-mode composer.

The composer flow:

1. Load `modules.catalog.json`.
2. Validate every requested module id.
3. Resolve selected optional modules.
4. Validate catalog dependency rules.
5. Validate selected platform artifacts exist in the template.
6. Copy the boilerplate into the target app.
7. Remove excluded backend projects, web feature folders, mobile module folders, and orphan tests.
8. Generate active module config files for the copied app.
9. Run or print smoke-check instructions for the generated app.

Generated outputs:

- Backend project/test removal for excluded source modules.
- Web `src/config/modules.config.ts`.
- Mobile `lib/app/modules.config.dart`.
- A clear module composition report printed by the generator.

The implementation can remain in PowerShell for now, but generator responsibilities should be grouped into small functions so the behavior can be ported later.

---

## 8. Backend Design

Backend is mostly stable after Tier 1.

Tier 2 backend work is limited to:

- Keep `ModuleLoader.ResolveOrder` strict for hard runtime dependencies.
- Keep `IModule.Dependencies` reserved for rare hard startup dependencies.
- Keep soft cross-module relationships expressed through capability contracts and null-object fallbacks.
- Add or expand catalog consistency tests for backend artifacts.
- Ensure generated apps do not retain excluded module projects, solution entries, tests, or project references.
- Keep `Starter.Api` and backend core projects free of optional `Starter.Module.*` type-level dependencies.

Do not add `IModuleHealthContributor`, `IModuleApiContributor`, or `IModuleDependencyMetadata` in Tier 2 unless an implementation task reveals a concrete second consumer.

---

## 9. Web Design

React gets the main Tier 2 architectural work.

### 9.1 Web Module Contract

Replace the loose local shape in `modules.config.ts` with a shared typed contract:

```ts
export interface WebModule {
  id: string;
  register(ctx: WebModuleContext): void;
}
```

`WebModuleContext` exposes stable registration APIs:

- `registerRoute(routeContribution)`
- `registerNavGroup(navGroupContribution)` or `registerNavItem(navItemContribution)`
- `registerSlot(slotId, entry)`
- `registerCapability(key, implementation)`
- `registerProvider(component)`
- `registerI18n(locale, resources)` when module i18n is migrated

Existing slot registration can be adapted behind this context so modules do not all need to change at once.

### 9.2 Generated Module Config

Generated `src/config/modules.config.ts` imports only selected optional modules.

Source-mode example:

```ts
import { productsModule } from '@/features/products';
import { workflowModule } from '@/features/workflow';

export const activeModules = {
  products: true,
  workflow: true,
} as const;

export const enabledModules: WebModule[] = [
  productsModule,
  workflowModule,
];
```

When no optional modules are selected, `enabledModules` is an empty typed array and no optional feature imports remain.

### 9.3 Routes

`routes.tsx` should render:

- core public routes
- core protected routes
- module public route contributions
- module protected route contributions

Optional page lazy imports move into each module entry or route contribution file.

Core should ask the registry for module routes:

```ts
const moduleRoutes = getModuleRoutes();
```

Core must not check `activeModules.workflow` or import `@/features/workflow/...`.

### 9.4 Navigation

Sidebar/navigation should render:

- core nav groups declared by core
- module nav groups/items from the web module registry

Module nav contributions own their labels, icons, paths, permissions, and optional badge hooks. For example, Workflow owns pending-task badge behavior instead of core importing a Workflow hook.

### 9.5 Slots and Capabilities

Existing slot APIs stay, but the preferred path becomes registration through `WebModuleContext`.

This lets package-mode later expose the same registration object from an npm package without changing shell code.

---

## 10. Mobile Design

Mobile already has a useful `AppModule` and `ModuleRegistry` shape.

Tier 2 mobile work:

- Generate `lib/app/modules.config.dart` from the catalog and selected module ids.
- Fix docs/comments that still reference `scripts/modules.json`.
- Keep optional imports isolated to `modules.config.dart` and optional module folders.
- Add duplicate module id validation if not already present.
- Add a static import guard for shell/core code.
- Verify `activeModules()` can be empty and the app still builds/runs with core features only.

Mobile `dependencies` should follow the same rule as backend runtime dependencies: use them only for hard runtime ordering needs. Composition-time dependency guidance remains in the catalog.

---

## 11. Error Handling

Generation should fail before copying when possible.

Required generation errors:

- Unknown requested module id: fail and list valid optional ids.
- Missing catalog dependency: fail and print the corrected `-Modules` list.
- Missing selected backend project: fail with module id and expected path.
- Missing selected web feature folder: fail with module id and expected path.
- Missing selected mobile module folder/class when the catalog declares mobile support.
- Malformed catalog field used by generation: fail with module id and field name.

Runtime guardrails:

- Backend throws for missing hard runtime `IModule.Dependencies`.
- Web registry throws for duplicate module ids.
- Web registry throws for duplicate route ids, nav ids, and slot contribution ids.
- Mobile registry throws for duplicate module ids and missing hard runtime dependencies.

Error messages should name the module id, platform, missing artifact, and likely fix.

---

## 12. Guardrails

Backend:

- `Starter.Api` and core backend projects must not depend on optional module internals.
- Catalog backend module names resolve to real template projects.
- Excluded modules leave no backend project references or test folders in generated apps.

Web:

- Optional feature imports are allowed only from generated `src/config/modules.config.ts` and inside optional feature folders.
- `routes.tsx`, layout, sidebar, and shell code cannot import optional feature folders.
- Every generated web module import resolves to an existing `index.ts`.
- Empty module selection still type-checks.

Mobile:

- Optional module imports are allowed only from generated `lib/app/modules.config.dart` and inside optional module folders.
- Every generated mobile module import resolves to an existing module file/class.
- Empty module selection still analyzes/builds.

Docs:

- New docs must reference `modules.catalog.json`, not `scripts/modules.json`.
- Any generated-app docs or comments should use generated app identity, not template-only `Starter` naming.

---

## 13. Testing

Tier 2 verification should include:

1. Catalog consistency tests:
   - dependencies point to known catalog ids
   - declared backend/web/mobile artifacts exist
   - `configKey` values are unique

2. Web registry tests:
   - duplicate module ids fail
   - duplicate route/nav/slot ids fail
   - empty `enabledModules` is valid

3. Static import guard tests:
   - React core cannot import optional feature folders outside generated config
   - Flutter core cannot import optional module folders outside generated config

4. Generated app smoke tests:
   - `-Modules None`
   - `-Modules All`
   - `-Modules workflow,commentsActivity,communication`
   - optionally `-Modules products` as a package-pilot precursor

5. Build checks for each smoke app:
   - backend build
   - frontend type-check/build
   - mobile analyze/build where practical

The first implementation plan can choose the minimum fast set and document slower checks as manual or CI-only.

---

## 14. Suggested Implementation Slices

Tier 2 should be split into small slices:

1. **Catalog validation and generator cleanup**
   - validate unknown ids and artifact paths
   - isolate composer helper functions
   - update stale docs/comments

2. **Generated web config**
   - emit `modules.config.ts` from selected catalog entries
   - keep existing slot-only module registration working
   - prove `None` creates an empty typed module list

3. **Web route registry**
   - add typed route contributions
   - migrate optional route imports out of `routes.tsx`
   - add duplicate route guard

4. **Web nav registry**
   - add nav contribution model
   - migrate optional sidebar entries and badge hooks
   - add import guard

5. **Generated mobile config and guardrails**
   - emit `modules.config.dart`
   - validate duplicate module ids
   - add mobile import guard

6. **Killer tests**
   - run source-mode generated app smoke checks for all/none/subset
   - document any slow checks that should move to CI

---

## 15. Success Criteria

Tier 2 is complete when:

1. `-Modules None` produces backend, web, and mobile apps with no optional module imports and clean builds/checks.
2. `-Modules All` builds/checks cleanly.
3. `-Modules workflow,commentsActivity,communication` builds/checks cleanly and includes only those optional modules.
4. React `routes.tsx` and sidebar/layout code do not import optional feature folders.
5. Flutter shell/core code does not import optional modules outside generated config.
6. Generated apps use the renamed project identity and do not leak template-only `Starter` assumptions into generated config or generated-app docs.
7. Catalog consistency tests cover dependencies, unique config keys, and declared platform artifacts.
8. The composition logic is documented and structured so it can later move into a CLI or desktop generator.

---

## 16. Deferred Decisions

These are intentionally deferred until package-readiness tiers:

- Nested catalog schema for source/package platform metadata.
- `coreCompat` semver enforcement.
- NuGet/npm/pub package naming and publishing.
- Products package pilot implementation.
- Source-to-package or package-to-source migration workflow.
- License/feed authorization and commercial delivery automation.

