# Tier 2.5 — Theme 5: Module Bootstrap Consolidation

> **Status:** Designed, not yet executing. Pick this up after Theme 4 has been merged. Spec: [`2026-04-29-modularity-tier-2-5-hardening.md`](../specs/2026-04-29-modularity-tier-2-5-hardening.md) §2 Theme 5.

**Goal:** Replace three platform-specific module-discovery mechanisms with a generated registry sourced from `modules.catalog.json`. Today, BE uses a filesystem glob (`*.Module.*.dll`), FE uses a manual array in `modules.config.ts`, and mobile uses hardcoded constructor calls in `modules.config.dart`. After this theme: one catalog → one generator → three platform-specific registry files. The codepath in source mode is identical to package mode (Tier 3 just changes the generator's input).

**Why this is the heaviest theme.** Every other theme adds tests or generates derived artifacts. Theme 5 changes how every platform's host bootstraps modules. The blast radius is wide. The Theme 2 killer-test matrix catches regressions in CI; that's the safety net that makes this theme tractable.

---

## Architectural decision

**One generator, three emitters.** `scripts/generators/modules.ts` reads the catalog and emits:

- `boilerplateBE/src/Starter.Api/Modularity/ModuleRegistry.g.cs` — static class returning instantiated `IModule[]`.
- `boilerplateFE/src/config/modules.generated.ts` — `enabledModules` array + `ModuleName` union.
- `boilerplateMobile/lib/app/modules.config.dart` — already an entry point; regenerate it directly.

Existing public files (`modules.config.ts` for FE) become re-exports.

The generator builds on `scripts/generators/lib/` from Theme 4.

---

## Why each platform's current discovery is wrong for Tier 3

| Platform | Today | Why it breaks for packages |
|----------|-------|---------------------------|
| Backend | `Directory.GetFiles(baseDir, "*.Module.*.dll")` then `Activator.CreateInstance` | NuGet package DLLs may live outside the host's bin folder; module assembly may have a different name pattern (e.g. `MyAgency.Module.X.dll`). Filesystem assumption is brittle. |
| Frontend | Manual `enabledModules` array + hardcoded `ModuleName` union type | An npm-installed module can't auto-register; `ModuleName` is a closed union the consumer must edit. |
| Mobile | Manual `BillingModule()` instantiation | Pub-installed module can't auto-register; consumer must edit `modules.config.dart`. |

**Theme 5 fix is platform-neutral.** A generator owns module composition; the *input* is the catalog. In Tier 3, the input is enriched with package metadata (`packageId.nuget`, etc., already reserved in catalog v2 from Theme 1). The generator's emitter logic doesn't change.

---

## Task breakdown

### Phase A — Backend

**File:** `boilerplateBE/src/Starter.Api/Modularity/ModuleRegistry.g.cs` (new, generated, gitignored optional — see below).

Generated output:

```csharp
// AUTO-GENERATED. Do not edit. Regenerate with `npm run generate:modules`.
// Source: modules.catalog.json
namespace Starter.Api.Modularity;

public static class ModuleRegistry
{
    public static IReadOnlyList<IModule> All()
    {
        return new IModule[]
        {
            new Starter.Module.AI.AIModule(),
            new Starter.Module.Billing.BillingModule(),
            new Starter.Module.Webhooks.WebhooksModule(),
            new Starter.Module.ImportExport.ImportExportModule(),
            new Starter.Module.Products.ProductsModule(),
            new Starter.Module.CommentsActivity.CommentsActivityModule(),
            new Starter.Module.Communication.CommunicationModule(),
            new Starter.Module.Workflow.WorkflowModule(),
        };
    }
}
```

**Caller migration:**

- `Program.cs` (or wherever `ModuleLoader.DiscoverModules()` is called for production startup) → `ModuleRegistry.All()` instead.
- `ModuleLoader.DiscoverModules()` stays for tests that need reflection-based discovery (the existing `ModuleNameUniquenessTests`, `ModulePermissionTests`, etc.).

**Catalog → C# class-name resolution:** the catalog's `backendModule` field is the project name (`Starter.Module.AI`) but the emitter needs the *class* name (`AIModule`). Discover by reading the project's `*Module.cs` file (one per module by convention) and parsing the type name. Add a catalog field if this proves fragile (`backendModuleClass`); for now, the convention `Starter.Module.X.XModule` is consistent and parsable.

**Architecture test (new):**

```csharp
[Fact]
public void ModuleRegistry_All_returns_the_same_set_as_DiscoverModules()
{
    var registry = ModuleRegistry.All().Select(m => m.GetType().FullName).ToHashSet();
    var discovered = ModuleLoader.DiscoverModules().Select(m => m.GetType().FullName).ToHashSet();
    registry.Should().BeEquivalentTo(discovered,
        "the generated registry must include every IModule that reflection can find. " +
        "If they diverge, the generator missed a module or the test assembly is leaking.");
}
```

This is the safety net catching generator bugs without blocking the migration.

**Should `ModuleRegistry.g.cs` be checked in?** Yes. Same reasoning as Theme 4: derivative files are committed; CI gates drift. Not gitignored.

---

### Phase B — Frontend

**File:** `boilerplateFE/src/config/modules.generated.ts` (new, generated):

```ts
// AUTO-GENERATED. Do not edit. Regenerate with `npm run generate:modules`.
// Source: modules.catalog.json
import billing from '@/features/billing';
import webhooks from '@/features/webhooks';
import importExport from '@/features/import-export';
import products from '@/features/products';
import commentsActivity from '@/features/comments-activity';
import communication from '@/features/communication';
import workflow from '@/features/workflow';

export const enabledModules = [
  billing,
  webhooks,
  importExport,
  products,
  commentsActivity,
  communication,
  workflow,
];

export type ModuleName =
  | 'billing'
  | 'webhooks'
  | 'importExport'
  | 'products'
  | 'commentsActivity'
  | 'communication'
  | 'workflow';
```

**Migration:**

`boilerplateFE/src/config/modules.config.ts` becomes:

```ts
export { enabledModules, type ModuleName } from './modules.generated';
// preserve any non-generated helpers here (none today, but room for human-edited config knobs)
```

**ESLint rule update:** `eslint.config.js` currently hardcodes `@/features/billing`, `@/features/webhooks`, etc. as restricted patterns. Generate this list too — emit `eslint.modules.generated.json` and have `eslint.config.js` read it. (Or just regenerate the whole `eslint.config.js`; the file is small. Pick whichever feels less magical.)

---

### Phase C — Mobile

`boilerplateMobile/lib/app/modules.config.dart` is regenerated wholesale from the catalog. No re-export indirection; this file is already an entry point.

```dart
// AUTO-GENERATED. Do not edit. Regenerate with `npm run generate:modules`.
// Source: modules.catalog.json
import 'package:starter_mobile/core/modularity/app_module.dart';
import 'package:starter_mobile/modules/billing/billing_module.dart';
// + any future mobile modules from catalog.supportedPlatforms = mobile

List<AppModule> activeModules() => [
  BillingModule(),
];
```

The generator filters catalog entries by `supportedPlatforms.includes("mobile")`. As Theme 6 adds Communication's mobile counterpart, this file gains the entry automatically.

---

### Phase D — Adopt `IModuleBusContributor` consistently

**Audit finding:** today only `WorkflowModule` implements `IModuleBusContributor`. Other modules with consumers (Communication has `SendEmailConsumer`, etc.) register them in some other way — likely directly in `Starter.Api`'s `Program.cs` or `Starter.Infrastructure`'s `DependencyInjection.cs`. Find every cross-module bus registration and move it behind the contract.

**Architecture test (new):**

```csharp
[Fact]
public void Modules_with_MassTransit_consumers_implement_IModuleBusContributor()
{
    var moduleAssemblies = ModuleLoader.DiscoverModules()
        .Select(m => m.GetType().Assembly)
        .Distinct();

    var problems = new List<string>();
    foreach (var asm in moduleAssemblies)
    {
        var consumers = asm.GetTypes().Where(t =>
            t.GetInterfaces().Any(i => i.IsGenericType &&
                i.GetGenericTypeDefinition() == typeof(MassTransit.IConsumer<>)));

        if (!consumers.Any()) continue;

        var moduleType = asm.GetTypes().FirstOrDefault(t =>
            typeof(IModule).IsAssignableFrom(t) && !t.IsAbstract);

        if (moduleType is null) continue;

        if (!typeof(IModuleBusContributor).IsAssignableFrom(moduleType))
        {
            problems.Add($"{moduleType.FullName} declares MassTransit consumer(s) " +
                $"({string.Join(", ", consumers.Select(c => c.Name))}) but does not " +
                $"implement IModuleBusContributor. Move the consumer registration " +
                $"into the module via the contract.");
        }
    }

    problems.Should().BeEmpty();
}
```

This test will fail on whichever modules need migration; fix them inline as part of Theme 5.

---

## File structure

**Create:**
- `scripts/generators/modules.ts` — main generator (reads catalog, emits 3 files).
- `scripts/generators/lib/parse-csharp-class.ts` — finds the `*Module` class name in a project (used by the BE emitter).
- `boilerplateBE/src/Starter.Api/Modularity/ModuleRegistry.g.cs` — generated, checked in.
- `boilerplateFE/src/config/modules.generated.ts` — generated, checked in.
- `boilerplateBE/tests/Starter.Api.Tests/Architecture/ModuleRegistryTests.cs` — registry-vs-discovered drift test + `IModuleBusContributor` test.

**Modify:**
- `boilerplateBE/src/Starter.Api/Program.cs` — call `ModuleRegistry.All()` instead of `ModuleLoader.DiscoverModules()` for production startup.
- `boilerplateFE/src/config/modules.config.ts` — re-export from generated.
- `boilerplateMobile/lib/app/modules.config.dart` — replace hand-edited content with generated. (Existing pattern of `activeModules()` returning a list stays.)
- `boilerplateFE/eslint.config.js` — drive restricted patterns from generator.
- `.github/workflows/modularity.yml` — add `modules-codegen-drift` job (mirrors the `permissions-codegen-drift` job from Theme 4).
- `package.json` (root) — add `"generate:modules": "tsx scripts/generators/modules.ts"`.
- Each module that has MassTransit consumers but no `IModuleBusContributor` impl — add it. (Discover via the new architecture test.)

---

## Tasks

### Phase A — Backend

1. Implement the generator's BE emitter, including the C# class-name resolver.
2. Generate `ModuleRegistry.g.cs`. Commit it.
3. Add `ModuleRegistryTests.ModuleRegistry_All_returns_the_same_set_as_DiscoverModules`.
4. Migrate `Program.cs` and any other production caller to use `ModuleRegistry.All()`. Keep `DiscoverModules()` for tests.
5. Verify locally with `dotnet test --filter "FullyQualifiedName~Architecture"` (all green) and `dotnet build` for the host.
6. Commit.

### Phase B — Frontend

1. Implement the generator's TS emitter.
2. Generate `modules.generated.ts`. Commit it.
3. Update `modules.config.ts` to re-export.
4. Drive `eslint.config.js`'s pattern list from generator output.
5. Verify with `npm run lint && npm run build`.
6. Commit.

### Phase C — Mobile

1. Implement the generator's Dart emitter.
2. Regenerate `modules.config.dart`. Diff against the hand-edited version — must be functionally identical.
3. Verify with `flutter analyze` and `flutter test test/core/modularity/`.
4. Commit.

### Phase D — IModuleBusContributor adoption

1. Add `Modules_with_MassTransit_consumers_implement_IModuleBusContributor` test.
2. Run the test — it fails on every module that needs migration.
3. For each failing module, add `IModuleBusContributor` implementation and move the consumer-registration code from wherever it lives today into the contract method.
4. Re-run test — green.
5. Commit each module migration as a separate sub-commit (small, easy to revert if one breaks).

### Phase E — CI gate

1. Add `modules-codegen-drift` job to `.github/workflows/modularity.yml`. Same pattern as Theme 4's permissions drift gate: run generator, `git diff --exit-code`, fail if dirty.
2. Trigger the workflow on the PR; confirm all matrix jobs (backend-killer, frontend-killer, mobile-killer, modules-codegen-drift, permissions-codegen-drift) pass.

---

## Verification

- [ ] All Theme 2 killer-test matrix jobs pass.
- [ ] `dotnet test --filter "Architecture"` — all green; new `ModuleRegistryTests` and `IModuleBusContributor` test included.
- [ ] `npm run lint && npm run build` clean.
- [ ] `flutter analyze && flutter test` clean.
- [ ] Removing a module from the catalog → re-running generator → all three platforms' generated files reflect the change.
- [ ] `Program.cs` no longer references `ModuleLoader.DiscoverModules()` in production paths.

---

## Out of scope

- **Hot-reload of modules** — out of scope. Module discovery is at startup.
- **Module versioning checks at runtime** — that's `coreCompat` enforcement, lives in Tier 3.
- **Replacing `ModuleLoader.DiscoverModules` entirely** — keep it for test-time reflection. Production uses `ModuleRegistry.All()`.
- **Removing `*.Module.*.dll` glob from `ModuleLoader`** — keep, since `DiscoverModules()` still has callers (tests). Add a comment that production uses the generated registry.

---

## Risks and mitigations

| Risk | Mitigation |
|------|------------|
| Generator misses a module class with non-standard naming | The convention `Starter.Module.X.XModule` holds for all 8 today. Architecture test (registry-vs-discovered) catches divergence. |
| `IModuleBusContributor` migration breaks consumer registration | Phase D commits one module at a time; revert is safe. Killer-test matrix catches if a module's consumers stop working in source-mode generation. |
| ESLint config codegen feels magical | Alternative: keep `eslint.config.js` hand-maintained but add a test that asserts the catalog's `frontendFeature` set matches the rule's pattern set. Either is fine; pick during execution. |
| Removing the filesystem glob breaks downstream tooling someone else added | The glob-based `DiscoverModules()` stays. Only the production caller migrates. |

---

## After this ships

Tier 3 starts with: every platform's host already reads its module set from a generated artifact. The Tier 3 work is to extend the generator's *input* schema with package coordinates (already reserved in catalog v2 via Theme 1), and to extend the *emitter* to output package-references instead of source imports. Same generator file, additive changes.
