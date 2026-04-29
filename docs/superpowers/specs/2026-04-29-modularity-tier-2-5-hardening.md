# Modularity Tier 2.5 — Hardening & Pre-Tier 3 Readiness

**Date:** 2026-04-29
**Status:** Approved scope, in execution
**Predecessor:** [Tier 1 host cleanup](../plans/2026-04-28-module-system-tier-1-host-cleanup.md), [Tier 2 source composition](../plans/2026-04-29-module-system-tier-2-source-composition.md)
**Successor:** Tier 3 — Package-ready contracts (deferred until this lands)

---

## 1. Why this exists

Tiers 1 and 2 delivered the architectural backbone: a module catalog (`modules.catalog.json`), a backend `IModule` contract with dependency resolution, a frontend slot/registry system, and the start of a mobile module shell. The system *works* end-to-end, but four parallel audits (backend, frontend, mobile, cross-cutting) surfaced ~80 concrete weaknesses that fall into three buckets:

1. **Silent failures** — typos in permission strings, duplicate module names, missing capability registrations, and translation drift all produce broken behavior with zero tests catching them.
2. **Source-tree assumptions** — file globs, manual config arrays, hardcoded ESLint rules, and hand-synchronized permission constants across three platforms all assume code lives in this repo, not in a NuGet/npm/pub package.
3. **Inconsistencies between modules** — patterns adopted by one module and ignored by others (`IModuleBusContributor`, `SeedDataAsync`, health checks, feature-flag gating).

Shipping Tier 3 (NuGet/npm/pub packaging) directly on top of this state would mean the package contracts inherit every weakness. Tier 2.5 is the consolidation pass: close the audit gaps, harden the patterns, and prove they hold under stress, *before* the package abstraction is layered on.

This spec is a living roadmap. Each theme below ships as its own PR with its own implementation plan in `docs/superpowers/plans/`. Themes are independently shippable in any order, but the recommended sequence is given at the bottom.

---

## 2. The six themes

### Theme 1 — Catalog schema v2 (foundation)

**Problem.** The catalog is the single source of truth for module identity, but it lacks the metadata Tier 3 will need: per-module `version`, `coreCompat` semver range, per-platform `packageId`, and explicit `supportedPlatforms`. Spec D5 deferred these fields to avoid a "rotting field" — but the alternative is retrofitting them across every module the day Tier 3 starts, which is when the cost is highest.

**Scope.** Add the four fields as **nullable**, alongside a `CatalogConsistencyTests` schema-completeness test. No behavior change. Modules that don't use them stay nullable; modules that do are validated.

**Fields to add:**
- `version` — required, semver string (e.g. `"1.0.0"`). Mirrors what each `IModule.Version` already returns.
- `coreCompat` — optional, semver range string (e.g. `">=1.0.0 <2.0.0"`). Validated when present; ignored at runtime until Tier 3 adds the enforcer.
- `packageId` — optional object: `{ "nuget": "...", "npm": "...", "pub": "..." }`. Each value optional; only validated when the corresponding platform is in `supportedPlatforms`.
- `supportedPlatforms` — required, array of `"backend" | "web" | "mobile"`. Catches "module is silently mobile-incomplete" today (e.g., the catalog declares `mobileFolder` but `supportedPlatforms` doesn't include `"mobile"`).

**Why this first.** Every other theme either uses these fields (Theme 3 validates them, Theme 4 generates from them, Theme 5 reads them) or benefits from their existence. Cheap and unblocking.

---

### Theme 2 — CI-gated killer tests

**Problem.** The `-Modules None` "killer test" — generate an app with all optional modules stripped, then build it — is the single most reliable check that the modularity boundaries hold. It is currently enforced by human discipline. Cross-cutting audit confirmed `.github/workflows/ci.yml` does not run it. Tests that depend on humans don't run when the human is on vacation.

**Scope.** Add a CI job that, for a matrix of module sets:
- `None` (only core)
- `All` (every optional module)
- A representative dependency-chain subset (e.g. `workflow,commentsActivity,communication`)

…runs `scripts/rename.ps1` against a temp directory, then `dotnet build`, `npm run build`, and `flutter analyze` against the generated app. Failure blocks the PR.

**Stretch:** also generate a "minimal valid" set per dependency edge (e.g. drop `commentsActivity` and verify `workflow` *correctly fails* generation with the helpful error message Tier 1 added).

**Why second.** Themes 4–6 are intrusive. Without CI guarding the killer matrix, regressions land silently while we're focused on the change in front of us.

---

### Theme 3 — Architecture test gap closure

**Problem.** Each audit found 3–5 silent-failure modes with no test coverage. A handful of small additive tests close most of them.

**Tests to add (backend):**
- `Module_Names_are_unique` — `ModuleLoader.ResolveOrder` keys a dictionary on `IModule.Name`; duplicates silently overwrite.
- `Permission_names_are_unique_across_modules` — two modules can today define the same permission string; `DataSeeder` upserts and the second wins silently.
- `Permission_string_format_matches_naming_convention` — every permission must be `{Module}.{Action}` (rejects `Permissions.Foo.Bar.Baz`-shaped drift).
- `Catalog_dependency_platforms_are_compatible` — if module A lists module B as a dependency and A supports backend, B must also support backend.
- `Every_declared_role_permission_is_a_real_permission` — `GetDefaultRolePermissions` skips unknown permission strings silently; catch typos at test time.
- *(Deferred to a later sub-PR within Theme 3)* `Capability_registration_coverage` — for each module that claims to implement a capability contract, assert it actually registered an impl. Requires per-module manifests; defer.

**Tests to add (frontend):**
- ESLint `no-restricted-imports` extended with `patterns[].importNames` to also block `import type` from optional modules. Currently only runtime imports are blocked, so a core file can still couple itself to a module's exported types.

**Tests to add (mobile):**
- A Dart analyzer custom rule (or test-harness script) preventing `lib/core/**` and `lib/app/shell/**` from importing `lib/modules/**` except through `lib/app/modules.config.dart`. Mirror of the existing backend `ModuleIsolationTests` and frontend ESLint rule.

**Why right after Theme 1.** Several of these tests validate the new Theme 1 fields (e.g. dependency platform compatibility). Bundling them keeps the schema self-checking.

---

### Theme 4 — Cross-platform permission codegen

**Problem.** `Permissions.cs` (BE), `permissions.ts` (FE), and `permissions.dart` (mobile) carry identical permission strings, kept in sync by hand. Every audit flagged this. The header of each file even says "Keep in sync manually." Drift here causes "permission seeded but UI doesn't render the action" bugs that are hard to spot in review.

**Scope.** Make `Permissions.cs` (and each module's `*Permissions.cs`) the source of truth, then generate `permissions.ts` and `permissions.dart` at build time. Two reasonable implementations:

- **(a) MSBuild target** — runs a small generator project that emits both files into the FE/mobile source trees. CI fails if the checked-in files don't match the generated output.
- **(b) Standalone Node script** — `scripts/generate-permissions.{ts,js}` that reads BE source files via Roslyn or a regex parse and writes the FE/mobile files. Run as a pre-commit hook + CI check.

Choose (a) for tighter integration with the build, (b) for cross-language flexibility. Plan should pick.

**Why before module bootstrap consolidation.** Theme 5 will move module discovery off the filesystem and into a generated registry — at that point we already have a generation pipeline running, so adding permissions to it is incremental.

---

### Theme 5 — Module bootstrap consolidation

**Problem.** Three platforms, three discovery mechanisms, all source-tree-bound:

- **Backend** (`ModuleLoader.DiscoverModules`): filesystem glob `*.Module.*.dll` from `AppDomain.CurrentDomain.BaseDirectory`, then `Activator.CreateInstance` on every `IModule`-implementing type. Won't work for NuGet packages whose DLLs land outside the host's bin folder; also swallows `ReflectionTypeLoadException` silently.
- **Frontend** (`src/config/modules.config.ts`): a manual `enabledModules` array. The `ModuleName` type is hardcoded.
- **Mobile** (`lib/app/modules.config.dart`): manual `BillingModule()` instantiation. Adding a second module requires hand-editing the file.

**Scope.** Replace all three with a generated registry sourced from `modules.catalog.json` (continuing the Theme 4 generation pipeline):

- **Backend:** generate a `ModuleRegistry.g.cs` that lists module types by reference (still source-mode for now; package-mode unlocks in Tier 3 by replacing the generator's source with package metadata). Replace `DiscoverModules` with `ModuleRegistry.Modules`. Remove the `ReflectionTypeLoadException` swallow — it's now unreachable, and if it fires it should fail loud.
- **Frontend:** generate `modules.generated.ts` exposing both `enabledModules` and a `ModuleName` union type. The hand-edited `modules.config.ts` becomes a one-liner re-export.
- **Mobile:** generate `modules.config.dart` from the catalog instead of hand-editing.

**Also in scope:** adopt `IModuleBusContributor` consistently. Currently only Workflow uses it; Communication has consumers but `Starter.Api` registers them directly. Move every module's bus/consumer registration behind the contract. Add an architecture test that fails if a module declares a `*Consumer` type but doesn't implement `IModuleBusContributor`.

**Why fifth.** Heaviest theme. Builds on Themes 1, 3, and 4. Sets up Tier 3 directly: in package mode, the only thing that changes is the input to the generator.

---

### Theme 6 — Mobile second-module + capability contract pattern

**Problem.** The catalog has 8 modules. Only Billing has a mobile counterpart. Every mobile-modularity claim is theoretical until a second module proves the patterns scale. Mobile capability contracts are also nascent — only `AnalyticsCollector` exists, and `NullAnalyticsCollector` is untested.

**Scope.** Add Communication as the second mobile module:

- Scaffold `lib/modules/communication/` with the same structure as `lib/modules/billing/`.
- Define a meaningful capability contract (e.g. `IPushNotificationCarrier` — Communication module implements it; null fallback in core stays silent).
- Add a slot contribution (e.g. on the profile page, show "Communication preferences").
- Add the catalog mobile fields (`mobileModule: "CommunicationModule"`, `mobileFolder: "communication"`, `supportedPlatforms` includes `"mobile"`).
- Write a test that boots the app with Communication disabled and verifies no crash, no missing slot.
- Write a test that boots with both Billing and Communication enabled and verifies dependency-order DI resolution.
- Document the mobile module author guide using both modules as reference.

**Why last.** This theme stresses every pattern Themes 1–5 establish. Doing it last surfaces real friction; doing it first means re-doing the work as patterns shift.

---

## 3. Recommended order

```
Theme 1 (catalog v2 schema)
    └──> Theme 3 (architecture test gap closure)
            └──> Theme 2 (CI killer-test matrix)
                    └──> Theme 4 (permission codegen)
                            └──> Theme 5 (module bootstrap consolidation)
                                    └──> Theme 6 (mobile second module)
```

Themes 1 + 3 ship together as one PR (small, tightly coupled — Theme 3 validates Theme 1's new fields). Themes 2, 4, 5, 6 each ship as standalone PRs.

---

## 4. Out of scope (defer until Tier 3 or beyond)

- **DTO codegen across BE / FE / mobile** — bigger initiative; deserves its own roadmap.
- **i18n / asset / template loading rewrites** — the abstractions are needed, but the right abstraction depends on the package format, which Tier 3 establishes. Doing it now risks rewriting twice.
- **`-AutoIncludeDependencies` flag** in `rename.ps1` — small, deferred from Tier 1, no urgency.
- **`MigrationsAssembly` pluggability** — pure Tier 3 concern.
- **AI template / agent template file-based loading** — pure Tier 3 concern.
- **Versioning / boilerplate-version embedding in generated apps** — wait until Theme 1's `version` field is in use.

---

## 5. Success criteria

Tier 2.5 is complete when:

1. `modules.catalog.json` schema is locked at v2; every module declares `version`, `supportedPlatforms`; tests prevent regression.
2. `dotnet build` + `npm run build` + `flutter analyze` all pass on `-Modules None`, `-Modules All`, and at least one mixed subset, gated by CI.
3. Permission strings cannot drift across BE / FE / mobile — a single source of truth generates the others; CI fails on diff.
4. No platform discovers modules by filesystem glob or hand-maintained array; all three use a generated registry sourced from the catalog.
5. The architecture test suite catches: duplicate module names, duplicate permission names, dependency platform mismatch, ESLint type-import leaks, mobile core→module imports outside `modules.config.dart`.
6. At least two mobile modules exist and the modularity invariants hold for both.

When all six are met, Tier 3 starts on a foundation where the package abstraction is the only new variable.

---

## 6. References

- **Predecessor specs:**
  - [`2026-04-07-true-modularity-refactor.md`](./2026-04-07-true-modularity-refactor.md) — original four-category vision
  - [`2026-04-28-hybrid-full-stack-module-system-design.md`](./2026-04-28-hybrid-full-stack-module-system-design.md) — Tier 1/2/3 design and resolved decisions
- **Predecessor plans:**
  - [`2026-04-28-module-system-tier-1-host-cleanup.md`](../plans/2026-04-28-module-system-tier-1-host-cleanup.md)
  - [`2026-04-29-module-system-tier-2-source-composition.md`](../plans/2026-04-29-module-system-tier-2-source-composition.md)
- **Tier 2.5 implementation plans (this spec):**
  - [Themes 1+3](../plans/2026-04-29-modularity-tier-2-5-themes-1-and-3.md) — catalog v2 schema + arch test gap closure ✅ shipped (PR #39)
  - [Theme 2](../plans/2026-04-29-modularity-tier-2-5-theme-2.md) — CI killer-test matrix
  - [Theme 4](../plans/2026-04-29-modularity-tier-2-5-theme-4.md) — cross-platform permission codegen
  - [Theme 5](../plans/2026-04-29-modularity-tier-2-5-theme-5.md) — module bootstrap consolidation
  - [Theme 6](../plans/2026-04-29-modularity-tier-2-5-theme-6.md) — mobile second module + capability contracts
- **Architecture tests:** `boilerplateBE/tests/Starter.Api.Tests/Architecture/`
- **Module contract:** `boilerplateBE/src/Starter.Abstractions/Modularity/IModule.cs`
- **Catalog:** `modules.catalog.json` (repo root)
