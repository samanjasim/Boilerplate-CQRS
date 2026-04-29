# Tier 2.5 — Theme 4: Cross-Platform Permission Codegen

> **Status:** Designed, not yet executing. Pick this up after Theme 2 has been merged and is running. Spec: [`2026-04-29-modularity-tier-2-5-hardening.md`](../specs/2026-04-29-modularity-tier-2-5-hardening.md) §2 Theme 4.

**Goal:** Eliminate the hand-synchronized triple of permission constants — `boilerplateBE/src/Starter.Shared/Constants/Permissions.cs` + per-module `*Permissions.cs`, `boilerplateFE/src/constants/permissions.ts`, `boilerplateMobile/lib/core/permissions/permissions.dart` — with a single source of truth (BE) and a generator that emits the FE and mobile artifacts. CI fails if the generated files drift from source.

**Why now.** Every audit (BE/FE/mobile/cross-cutting) flagged this as a fragility. A permission seeded by the BE but missing in the FE map renders a hidden action; missing in mobile is the same bug for the mobile shell. Theme 3 enforced naming convention but not cross-platform sync.

**Architectural decision.** Backend is source of truth. C# constant strings parsed → emit TypeScript and Dart constants. Reasoning:

- **BE is where permissions are *defined* and *seeded*.** Adding a permission means adding a `const string` in C#, then registering it in `Permissions.GetAllWithMetadata()` (core) or in the module's `IModule.GetPermissions()`. The string flows through the seeder into `Permissions` table rows, then into `RolePermission` mappings. The BE constant is also the policy name used by `[Authorize(Policy = ...)]`. Every other use of the string (FE checks, mobile checks) is *mirroring*, not authoring.
- **FE/mobile artifacts are derived consumers.** The TypeScript and Dart files exist to give each runtime a strongly-typed handle on the same string. Hand-editing these files to add a permission and forgetting BE means the seeder never creates the row, which means the policy doesn't exist, which means the FE check is meaningless.
- **Catalog as alternate source rejected.** Putting permissions in `modules.catalog.json` means rewiring how the seeder reads them (it currently uses `IModule.GetPermissions()` reflectively). More work for no benefit, since BE still needs a constant for `[Authorize]`.

---

## Implementation choices

### Generator location

`scripts/generators/permissions.ts` — one Node script, runs in CI and locally. Same directory will host the Theme 5 module-registry generator, so they share helpers.

Why TypeScript: project already has Node + tsx (`boilerplateFE/`). Avoids adding a Roslyn-based generator project (heavyweight) and keeps the same toolchain across themes.

### Parsing strategy

Regex over C# source files. The format of permission constants is rigid and stable:

```csharp
public const string Foo = "Module.Action";
```

A single regex captures `(name, value)` per line. Test with the existing files. If a future contributor adds non-trivial syntax (interpolation, computed values), the test catches it.

The full pattern set:
- Core: `boilerplateBE/src/Starter.Shared/Constants/Permissions.cs` — single file, multiple nested static classes.
- Modules: `boilerplateBE/src/modules/Starter.Module.*/Constants/*Permissions.cs` — one file per module.

### Output format

**FE — `boilerplateFE/src/constants/permissions.generated.ts`:**

```ts
// AUTO-GENERATED. Do not edit. Regenerate with `node scripts/generators/permissions.ts`.
// Source: boilerplateBE/src/Starter.Shared/Constants/Permissions.cs and module *Permissions.cs files.
export const PERMISSIONS = {
  Users: {
    View: 'Users.View',
    Show: 'Users.Show',
    // …
  },
  Billing: {
    View: 'Billing.View',
    // …
  },
  // …
} as const;

export type PermissionString =
  | 'Users.View'
  | 'Users.Show'
  // …
  ;
```

The existing `boilerplateFE/src/constants/permissions.ts` becomes a one-line re-export so consumers don't need import-path changes:

```ts
export { PERMISSIONS, type PermissionString } from './permissions.generated';
```

**Mobile — `boilerplateMobile/lib/core/permissions/permissions.generated.dart`:**

```dart
// AUTO-GENERATED. Do not edit. Regenerate with `node scripts/generators/permissions.ts`.
// Source: boilerplateBE/src/Starter.Shared/Constants/Permissions.cs and module *Permissions.cs files.
class Permissions {
  Permissions._();

  // ─── Users ───
  static const String usersView = 'Users.View';
  static const String usersShow = 'Users.Show';
  // …

  // ─── Billing ───
  static const String billingView = 'Billing.View';
  // …
}
```

Existing `permissions.dart` becomes a one-line `export 'permissions.generated.dart';` so consumers stay stable.

### Catalog awareness for module permissions

The generator must know which modules a generated app *includes* — otherwise `permissions.generated.ts` for a `-Modules None` app would still reference Billing constants. Two options:

- **(a) Always emit all permissions** — the boilerplate template repo always has all modules; the rename script handles selection. The generator runs against the boilerplate, so it always sees everything.
- **(b) Filter by selection during rename** — `rename.ps1` re-runs the generator after stripping unselected modules.

Choose **(a)**. The template repo always has all permissions; rename then strips unselected modules' source files (and their `*Permissions.cs`), so the next generator run in the generated app naturally filters down. We just need the generator script in the generated app, which `rename.ps1` already copies via `scripts/`.

---

## File structure

**Create:**
- `scripts/generators/permissions.ts` — the generator script.
- `scripts/generators/lib/parse-cs-permissions.ts` — shared C# permission-constant parser.
- `boilerplateFE/src/constants/permissions.generated.ts` — checked-in generated artifact.
- `boilerplateMobile/lib/core/permissions/permissions.generated.dart` — checked-in generated artifact.

**Modify:**
- `boilerplateFE/src/constants/permissions.ts` — replace body with `export { PERMISSIONS, type PermissionString } from './permissions.generated';` plus any backwards-compat aliases.
- `boilerplateMobile/lib/core/permissions/permissions.dart` — replace body with `export 'permissions.generated.dart';` plus any pre-existing helper functions.
- `.github/workflows/modularity.yml` — add a `permissions-codegen-drift` job that runs the generator and asserts `git diff --exit-code` is clean.
- `package.json` (repo root, create if absent) — add `"generate:permissions"` script and `tsx` devDep so the generator can be run with `npm run generate:permissions`.
- `boilerplateBE/tests/Starter.Api.Tests/Architecture/PermissionCodegenTests.cs` — defense-in-depth: enumerate BE permissions via reflection, assert every one appears as a string literal in `permissions.generated.ts` and `permissions.generated.dart`.

---

## Tasks

### Task 1 — Generator scaffolding

- Add a root `package.json` (if not present) with `"scripts": { "generate:permissions": "tsx scripts/generators/permissions.ts" }` and a `devDependencies` block containing `tsx` and `typescript`.
- Create `scripts/generators/lib/parse-cs-permissions.ts`. Exports `parsePermissionFile(absPath) → Array<{ moduleGroup: string, name: string, value: string }>`. Walks each `static class X { public const string Y = "Z"; }` and emits records.
- Unit-test the parser against fixture strings inline (no fixture files; keep the test file self-contained).

### Task 2 — TypeScript emitter

- `scripts/generators/permissions.ts` reads:
  - `boilerplateBE/src/Starter.Shared/Constants/Permissions.cs`
  - All `boilerplateBE/src/modules/*/Constants/*Permissions.cs` files (glob).
- Emits `boilerplateFE/src/constants/permissions.generated.ts` per the format above.
- Strict ordering: alphabetical by module group, then by permission name within group. Stable output across runs.
- Top-of-file header comment that includes the source file list and a hash of the inputs (so a contributor diffing the file can quickly tell what source changed).

### Task 3 — Dart emitter

- Same generator, second emit pass for `boilerplateMobile/lib/core/permissions/permissions.generated.dart`.
- Naming convention: `lowerCamelCase` Dart identifier from `Module.Action` → `moduleAction`. (`Users.View` → `usersView`.) Group with `// ─── Module ───` separator comments for readability.

### Task 4 — Re-export the generated files from existing public files

- Replace the body of `boilerplateFE/src/constants/permissions.ts` with the one-liner re-export. Preserve any non-permission helpers that file may also export today.
- Same for `boilerplateMobile/lib/core/permissions/permissions.dart`.
- Run `npm run lint` / `npm run build` / `flutter analyze` to confirm no consumer breaks. They shouldn't — the public surface (`PERMISSIONS.Users.View`, `Permissions.usersView`) is identical.

### Task 5 — CI drift gate

- Extend `.github/workflows/modularity.yml` with a new job `permissions-codegen-drift`:

```yaml
permissions-codegen-drift:
  runs-on: ubuntu-latest
  steps:
    - uses: actions/checkout@v4
    - uses: actions/setup-node@v4
      with: { node-version: 20 }
    - run: npm install
    - run: npm run generate:permissions
    - name: Assert no drift
      run: |
        if ! git diff --exit-code; then
          echo "::error::permissions.generated.ts or permissions.generated.dart drifted from BE source. Run 'npm run generate:permissions' and commit."
          exit 1
        fi
```

### Task 6 — Defense-in-depth architecture test

`PermissionCodegenTests.cs`:

- Concatenate all permissions discovered via `Permissions.GetAllWithMetadata()` plus `IModule.GetPermissions()` for every loaded module.
- For each, read the generated TS and Dart files (raw text), assert the permission string appears as a quoted string literal.

This catches: emitter regex breakage, accidental file overwrite, generator dropping a permission silently.

### Task 7 — Pre-commit hook (optional polish)

- Add `.husky/pre-commit` (if husky is set up) that calls `npm run generate:permissions` and re-stages the generated files.
- If husky isn't already in the repo, skip — CI gate is enough.

---

## Verification

- [ ] `npm run generate:permissions` is idempotent (running twice produces no diff).
- [ ] Editing `AiPermissions.cs` (e.g. add `Ai.Foo`) → re-run generator → `permissions.generated.ts` and `permissions.generated.dart` both gain a corresponding entry.
- [ ] CI drift gate flags an uncommitted regeneration as a failure.
- [ ] Architecture test catches a planted bug (e.g., manually delete an entry from `permissions.generated.ts`).
- [ ] Killer-test matrix (Theme 2) still passes — generated apps still build with all module sets.

---

## Out of scope

- **Two-way generation** (FE → BE or mobile → BE). One direction only; BE is canonical.
- **Translation strings for permissions.** That's i18n, separate concern. Each platform owns its translation files.
- **Generating role-permission mappings.** Those live in `IModule.GetDefaultRolePermissions()` and stay BE-only.
- **Generating policy registration code.** ASP.NET Core auth still registers policies dynamically from `Permissions.GetAll()`.

---

## Risks and mitigations

| Risk | Mitigation |
|------|------------|
| Regex parser misses an exotic C# syntax (verbatim strings, interpolation) | Today's `*Permissions.cs` files all use the simple `public const string X = "Y";` form. Theme 3's naming convention test would already reject anything weird. If a contributor introduces variation, the parser test catches it. |
| Generator emits non-deterministic output (hashmap iteration order) | Sort alphabetically before emit; assert idempotency in CI. |
| Re-export causes circular import warning in Vite/Dart | Both ecosystems handle re-exports cleanly. Smoke-tested by `npm run build` and `flutter analyze` in Task 4. |
| Module-permission file added but generator script not updated | The glob `boilerplateBE/src/modules/*/Constants/*Permissions.cs` auto-discovers new files. No manual generator changes needed. |

---

## After this ships

Theme 5 (module bootstrap consolidation) reuses `scripts/generators/lib/` for catalog parsing and re-emits more generated files (TS, Dart) from the same source. Adopt the conventions established here.
