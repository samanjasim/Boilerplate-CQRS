# Tier 2.5 — Themes 1 + 3: Catalog v2 Schema + Architecture Test Gap Closure

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add four nullable v2 fields to `modules.catalog.json` (`version`, `coreCompat`, `packageId`, `supportedPlatforms`), then close the silent-failure gaps surfaced by the post-Tier-2 audit with a focused set of architecture tests on backend, frontend, and mobile.

**Architecture:** Pure additive. The catalog gains four fields; modules already-existing `IModule.Version` value gets mirrored into the catalog. Tests are added to `boilerplateBE/tests/Starter.Api.Tests/Architecture/`, plus a small ESLint config tweak on the frontend and a new Dart test on mobile. No behavior changes to the running application.

**Tech Stack:** xUnit + FluentAssertions (BE), `System.Text.Json` (catalog parsing), `NetArchTest` (existing pattern), ESLint flat config (FE), Dart `package:test` (mobile).

**Spec reference:** [2026-04-29-modularity-tier-2-5-hardening.md](../specs/2026-04-29-modularity-tier-2-5-hardening.md) Themes 1 + 3.

---

## File Structure

**Catalog & docs:**
- Modify: `modules.catalog.json` — add `version`, `supportedPlatforms` to every entry; update `_comment` to describe v2 fields.

**Backend tests** (all under `boilerplateBE/tests/Starter.Api.Tests/Architecture/`):
- Modify: `CatalogConsistencyTests.cs` — add v2 schema tests (5 new `[Fact]`s).
- Create: `ModuleNameUniquenessTests.cs` — module Name uniqueness across the assembly set.
- Create: `ModulePermissionTests.cs` — permission uniqueness, naming convention, role-mapping integrity.

**Backend production code** (small):
- Modify: `boilerplateBE/src/Starter.Abstractions/Modularity/ModuleLoader.cs` — replace `ToDictionary` with explicit duplicate-detection that throws a helpful error.

**Frontend:**
- Modify: `boilerplateFE/eslint.config.js` — extend `no-restricted-imports` to also block `import type` from optional modules.

**Mobile:**
- Create: `boilerplateMobile/test/architecture/module_isolation_test.dart` — assert `lib/core/**` and `lib/app/shell/**` never import `lib/modules/**` (except `lib/app/modules.config.dart`, which is the registry).

---

## Task 1: Catalog v2 — populate `version` and `supportedPlatforms`

**Files:**
- Modify: `modules.catalog.json`

- [ ] **Step 1: Update each module entry**

For every entry (`ai`, `billing`, `webhooks`, `importExport`, `products`, `commentsActivity`, `communication`, `workflow`):
- Add `"version": "1.0.0"` (matches the hardcoded `IModule.Version` in each `*Module.cs`).
- Add `"supportedPlatforms"` as an array. Inferred from existing path-bearing fields:
  - Always includes `"backend"` (every module has `backendModule`).
  - Includes `"web"` iff `frontendFeature` is set.
  - Includes `"mobile"` iff `mobileModule` is set.

Resulting platform sets:
- `ai` → `["backend"]`
- `billing` → `["backend", "web", "mobile"]`
- `webhooks` → `["backend", "web"]`
- `importExport` → `["backend", "web"]`
- `products` → `["backend", "web"]`
- `commentsActivity` → `["backend", "web"]`
- `communication` → `["backend", "web"]`
- `workflow` → `["backend", "web"]`

- [ ] **Step 2: Update the `_comment` block**

Append to the existing `_comment` string a paragraph describing the v2 fields:

```
"_comment": "Module catalog (single source of truth). … <existing text> …
  V2 fields (added 2026-04-29 in Tier 2.5):
    - version: required semver string per module.
    - supportedPlatforms: required array; one or more of \"backend\", \"web\", \"mobile\".
    - coreCompat: optional semver range (e.g. \">=1.0.0 <2.0.0\"); enforced in Tier 3.
    - packageId: optional object with optional \"nuget\", \"npm\", \"pub\" keys; emitted by Tier 3 generators.
  CatalogConsistencyTests enforces the schema."
```

- [ ] **Step 3: Commit**

```bash
git add modules.catalog.json
git commit -m "chore(modules): catalog v2 — add version and supportedPlatforms"
```

---

## Task 2: BE test — `version` is required and valid semver

**Files:**
- Modify: `boilerplateBE/tests/Starter.Api.Tests/Architecture/CatalogConsistencyTests.cs`

- [ ] **Step 1: Add the test method**

Append inside the class (after `Declared_mobile_modules_have_matching_folder_and_entrypoint`):

```csharp
[Fact]
public void Every_module_declares_a_valid_semver_version()
{
    using var doc = JsonDocument.Parse(File.ReadAllText(CatalogPath));
    var problems = new List<string>();

    foreach (var module in ModuleEntries(doc))
    {
        var version = ReadOptionalString(module.Value, "version");
        if (version is null)
        {
            problems.Add($"'{module.Name}' is missing required 'version' (Tier 2.5 schema v2)");
            continue;
        }

        if (!IsSimpleSemver(version))
        {
            problems.Add($"'{module.Name}.version' = '{version}' is not MAJOR.MINOR.PATCH semver");
        }
    }

    problems.Should().BeEmpty(
        "modules.catalog.json schema v2 requires every module to declare a semver version. " +
        "See spec §2 Theme 1.");
}

private static bool IsSimpleSemver(string value)
{
    var parts = value.Split('.');
    if (parts.Length != 3) return false;
    return parts.All(p => p.Length > 0 && p.All(char.IsDigit));
}
```

- [ ] **Step 2: Run it**

```bash
cd boilerplateBE && dotnet test tests/Starter.Api.Tests/Starter.Api.Tests.csproj --filter "FullyQualifiedName~CatalogConsistencyTests.Every_module_declares_a_valid_semver_version" --nologo
```

Expected: PASS (Task 1 already populated `version` for every module).

- [ ] **Step 3: Verify the test would fail on bad data**

Temporarily corrupt `modules.catalog.json` (set `ai.version` to `"1.0"`), re-run, confirm FAIL. Restore the value.

- [ ] **Step 4: Commit**

```bash
git add boilerplateBE/tests/Starter.Api.Tests/Architecture/CatalogConsistencyTests.cs
git commit -m "test(modules): catalog version field is required semver"
```

---

## Task 3: BE test — `supportedPlatforms` is required and consistent with path fields

**Files:**
- Modify: `boilerplateBE/tests/Starter.Api.Tests/Architecture/CatalogConsistencyTests.cs`

- [ ] **Step 1: Add the test method**

```csharp
[Fact]
public void supportedPlatforms_matches_declared_path_fields()
{
    using var doc = JsonDocument.Parse(File.ReadAllText(CatalogPath));
    var allowed = new HashSet<string>(StringComparer.Ordinal) { "backend", "web", "mobile" };
    var problems = new List<string>();

    foreach (var module in ModuleEntries(doc))
    {
        var platforms = ReadStringArray(module.Value, "supportedPlatforms");
        if (platforms is null)
        {
            problems.Add($"'{module.Name}' is missing required 'supportedPlatforms' (Tier 2.5 schema v2)");
            continue;
        }

        if (platforms.Count == 0)
        {
            problems.Add($"'{module.Name}.supportedPlatforms' must contain at least one platform");
            continue;
        }

        foreach (var p in platforms)
        {
            if (!allowed.Contains(p))
                problems.Add($"'{module.Name}.supportedPlatforms' contains unknown platform '{p}'");
        }

        var hasBackend = ReadOptionalString(module.Value, "backendModule") is not null;
        var hasWeb = ReadOptionalString(module.Value, "frontendFeature") is not null;
        var hasMobile = ReadOptionalString(module.Value, "mobileModule") is not null;

        if (hasBackend && !platforms.Contains("backend"))
            problems.Add($"'{module.Name}' declares backendModule but 'backend' is not in supportedPlatforms");
        if (hasWeb && !platforms.Contains("web"))
            problems.Add($"'{module.Name}' declares frontendFeature but 'web' is not in supportedPlatforms");
        if (hasMobile && !platforms.Contains("mobile"))
            problems.Add($"'{module.Name}' declares mobileModule but 'mobile' is not in supportedPlatforms");

        if (platforms.Contains("backend") && !hasBackend)
            problems.Add($"'{module.Name}' lists 'backend' in supportedPlatforms but has no backendModule");
        if (platforms.Contains("web") && !hasWeb)
            problems.Add($"'{module.Name}' lists 'web' in supportedPlatforms but has no frontendFeature");
        if (platforms.Contains("mobile") && !hasMobile)
            problems.Add($"'{module.Name}' lists 'mobile' in supportedPlatforms but has no mobileModule");
    }

    problems.Should().BeEmpty(
        "supportedPlatforms must reflect what the module actually ships, in both directions. " +
        "Drift here causes generated apps to import modules that don't exist on the target platform.");
}

private static IReadOnlyList<string>? ReadStringArray(JsonElement element, string propertyName)
{
    if (!element.TryGetProperty(propertyName, out var prop)) return null;
    if (prop.ValueKind != JsonValueKind.Array) return null;
    return prop.EnumerateArray()
        .Where(e => e.ValueKind == JsonValueKind.String)
        .Select(e => e.GetString()!)
        .ToList();
}
```

- [ ] **Step 2: Run it**

```bash
cd boilerplateBE && dotnet test tests/Starter.Api.Tests/Starter.Api.Tests.csproj --filter "FullyQualifiedName~CatalogConsistencyTests.supportedPlatforms" --nologo
```

Expected: PASS.

- [ ] **Step 3: Verify the test would fail on bad data**

Temporarily remove `"web"` from `webhooks.supportedPlatforms`. Confirm FAIL with the right message. Restore.

- [ ] **Step 4: Commit**

```bash
git add boilerplateBE/tests/Starter.Api.Tests/Architecture/CatalogConsistencyTests.cs
git commit -m "test(modules): supportedPlatforms required and consistent with path fields"
```

---

## Task 4: BE test — dependency platform compatibility

**Files:**
- Modify: `boilerplateBE/tests/Starter.Api.Tests/Architecture/CatalogConsistencyTests.cs`

- [ ] **Step 1: Add the test method**

```csharp
[Fact]
public void Catalog_dependencies_are_platform_compatible()
{
    using var doc = JsonDocument.Parse(File.ReadAllText(CatalogPath));
    var modulePlatforms = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
    foreach (var module in ModuleEntries(doc))
    {
        modulePlatforms[module.Name] = ReadStringArray(module.Value, "supportedPlatforms") ?? Array.Empty<string>();
    }

    var problems = new List<string>();
    foreach (var module in ModuleEntries(doc))
    {
        if (!module.Value.TryGetProperty("dependencies", out var deps) || deps.ValueKind != JsonValueKind.Array)
            continue;

        var consumerPlatforms = modulePlatforms[module.Name];
        foreach (var dep in deps.EnumerateArray())
        {
            var depId = dep.GetString();
            if (string.IsNullOrEmpty(depId)) continue;
            if (!modulePlatforms.TryGetValue(depId, out var providerPlatforms)) continue; // covered by other test

            foreach (var platform in consumerPlatforms)
            {
                if (!providerPlatforms.Contains(platform))
                {
                    problems.Add(
                        $"'{module.Name}' supports '{platform}' and depends on '{depId}', " +
                        $"but '{depId}' does not support '{platform}' (supports: {string.Join(",", providerPlatforms)}).");
                }
            }
        }
    }

    problems.Should().BeEmpty(
        "A module that supports a platform cannot depend on a module that does not. " +
        "Otherwise generation succeeds but the platform build fails on missing imports.");
}
```

- [ ] **Step 2: Run it**

```bash
cd boilerplateBE && dotnet test tests/Starter.Api.Tests/Starter.Api.Tests.csproj --filter "FullyQualifiedName~Catalog_dependencies_are_platform_compatible" --nologo
```

Expected: PASS.

- [ ] **Step 3: Verify the test would fail**

Temporarily change `workflow.dependencies` to `["ai"]` (workflow ships web; ai is backend-only). Confirm FAIL with helpful message. Restore.

- [ ] **Step 4: Commit**

```bash
git add boilerplateBE/tests/Starter.Api.Tests/Architecture/CatalogConsistencyTests.cs
git commit -m "test(modules): dependency platform compatibility"
```

---

## Task 5: BE test — `coreCompat` and `packageId` shape (defensive, fields not yet used)

**Files:**
- Modify: `boilerplateBE/tests/Starter.Api.Tests/Architecture/CatalogConsistencyTests.cs`

- [ ] **Step 1: Add the two test methods**

```csharp
[Fact]
public void coreCompat_when_present_is_a_non_empty_string()
{
    // Tier 3 will add a real semver-range parser. Tier 2.5 only validates the field's shape
    // so authors don't introduce typos before the enforcer exists.
    using var doc = JsonDocument.Parse(File.ReadAllText(CatalogPath));
    var problems = new List<string>();

    foreach (var module in ModuleEntries(doc))
    {
        if (!module.Value.TryGetProperty("coreCompat", out var prop)) continue;
        if (prop.ValueKind == JsonValueKind.Null) continue;

        if (prop.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(prop.GetString()))
            problems.Add($"'{module.Name}.coreCompat' must be a non-empty string when present");
    }

    problems.Should().BeEmpty();
}

[Fact]
public void packageId_keys_match_supportedPlatforms()
{
    using var doc = JsonDocument.Parse(File.ReadAllText(CatalogPath));
    var allowedKeys = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        { "nuget", "backend" },
        { "npm", "web" },
        { "pub", "mobile" },
    };
    var problems = new List<string>();

    foreach (var module in ModuleEntries(doc))
    {
        if (!module.Value.TryGetProperty("packageId", out var prop)) continue;
        if (prop.ValueKind == JsonValueKind.Null) continue;
        if (prop.ValueKind != JsonValueKind.Object)
        {
            problems.Add($"'{module.Name}.packageId' must be an object when present");
            continue;
        }

        var platforms = ReadStringArray(module.Value, "supportedPlatforms") ?? Array.Empty<string>();
        foreach (var key in prop.EnumerateObject())
        {
            if (!allowedKeys.TryGetValue(key.Name, out var requiredPlatform))
            {
                problems.Add($"'{module.Name}.packageId' has unknown key '{key.Name}'; allowed: {string.Join(",", allowedKeys.Keys)}");
                continue;
            }
            if (!platforms.Contains(requiredPlatform))
            {
                problems.Add(
                    $"'{module.Name}.packageId.{key.Name}' is set but supportedPlatforms does not include '{requiredPlatform}'");
            }
            if (key.Value.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(key.Value.GetString()))
            {
                problems.Add($"'{module.Name}.packageId.{key.Name}' must be a non-empty string");
            }
        }
    }

    problems.Should().BeEmpty();
}
```

- [ ] **Step 2: Run them**

```bash
cd boilerplateBE && dotnet test tests/Starter.Api.Tests/Starter.Api.Tests.csproj --filter "FullyQualifiedName~CatalogConsistencyTests.coreCompat_when_present|FullyQualifiedName~CatalogConsistencyTests.packageId_keys" --nologo
```

Expected: PASS (no module sets either field yet).

- [ ] **Step 3: Spot-check failure mode**

Temporarily add `"packageId": { "pub": "starter_module_ai" }` to `ai` (which is backend-only). Confirm packageId test FAILS. Restore.

- [ ] **Step 4: Commit**

```bash
git add boilerplateBE/tests/Starter.Api.Tests/Architecture/CatalogConsistencyTests.cs
git commit -m "test(modules): coreCompat shape + packageId/supportedPlatforms alignment"
```

---

## Task 6: BE — improve `ModuleLoader.ResolveOrder` duplicate-name error

**Files:**
- Modify: `boilerplateBE/src/Starter.Abstractions/Modularity/ModuleLoader.cs:59`
- Modify: `boilerplateBE/tests/Starter.Api.Tests/Architecture/ModuleLoaderTests.cs`

The current `modules.ToDictionary(m => m.Name)` throws `ArgumentException` with a generic "An item with the same key has already been added" message. Replace with explicit duplicate detection.

- [ ] **Step 1: Write the failing test in `ModuleLoaderTests.cs`**

Add inside the existing class:

```csharp
[Fact]
public void ResolveOrder_throws_helpful_error_when_two_modules_share_a_name()
{
    var modules = new List<IModule>
    {
        new FakeModule("DuplicateName"),
        new FakeModule("DuplicateName"),
    };

    var act = () => ModuleLoader.ResolveOrder(modules);

    act.Should().Throw<InvalidOperationException>()
        .WithMessage("*duplicate*'DuplicateName'*");
}
```

- [ ] **Step 2: Run it**

```bash
cd boilerplateBE && dotnet test tests/Starter.Api.Tests/Starter.Api.Tests.csproj --filter "FullyQualifiedName~ResolveOrder_throws_helpful_error_when_two_modules_share_a_name" --nologo
```

Expected: FAIL — current error is `ArgumentException` ("An item with the same key …"), wrong type and wrong message.

- [ ] **Step 3: Fix `ModuleLoader.ResolveOrder`**

Replace the line:

```csharp
var moduleMap = modules.ToDictionary(m => m.Name);
```

with:

```csharp
var moduleMap = new Dictionary<string, IModule>(modules.Count, StringComparer.Ordinal);
foreach (var module in modules)
{
    if (!moduleMap.TryAdd(module.Name, module))
    {
        throw new InvalidOperationException(
            $"Two modules declare the same Name 'duplicate name {module.Name}'. " +
            $"IModule.Name is the lookup key for dependency resolution; duplicates would silently overwrite. " +
            $"First-registered type: {moduleMap[module.Name].GetType().FullName}; " +
            $"conflict: {module.GetType().FullName}.");
    }
}
```

Wait — re-read that error message. The wording `"duplicate name {module.Name}"` is awkward. Use:

```csharp
throw new InvalidOperationException(
    $"Two modules declare the same Name 'duplicate '{module.Name}'. " +
    ...
```

Final form:

```csharp
var moduleMap = new Dictionary<string, IModule>(modules.Count, StringComparer.Ordinal);
foreach (var module in modules)
{
    if (!moduleMap.TryAdd(module.Name, module))
    {
        throw new InvalidOperationException(
            $"Two modules declare the same duplicate Name '{module.Name}'. " +
            $"IModule.Name is the lookup key for dependency resolution; duplicates would silently overwrite. " +
            $"First-registered type: {moduleMap[module.Name].GetType().FullName}; " +
            $"conflict: {module.GetType().FullName}.");
    }
}
```

- [ ] **Step 4: Run the new test + the existing tests**

```bash
cd boilerplateBE && dotnet test tests/Starter.Api.Tests/Starter.Api.Tests.csproj --filter "FullyQualifiedName~ModuleLoaderTests" --nologo
```

Expected: ALL PASS.

- [ ] **Step 5: Commit**

```bash
git add boilerplateBE/src/Starter.Abstractions/Modularity/ModuleLoader.cs boilerplateBE/tests/Starter.Api.Tests/Architecture/ModuleLoaderTests.cs
git commit -m "fix(modules): ModuleLoader throws helpful error on duplicate Name"
```

---

## Task 7: BE — module `Name` uniqueness across the real assembly set

**Files:**
- Create: `boilerplateBE/tests/Starter.Api.Tests/Architecture/ModuleNameUniquenessTests.cs`

- [ ] **Step 1: Create the file**

```csharp
using FluentAssertions;
using Starter.Abstractions.Modularity;
using Xunit;

namespace Starter.Api.Tests.Architecture;

public class ModuleNameUniquenessTests
{
    [Fact]
    public void All_loaded_modules_have_unique_names()
    {
        var modules = ModuleLoader.DiscoverModules();
        var duplicates = modules
            .GroupBy(m => m.Name, StringComparer.Ordinal)
            .Where(g => g.Count() > 1)
            .Select(g => $"'{g.Key}' is declared by: {string.Join(", ", g.Select(m => m.GetType().FullName))}")
            .ToList();

        duplicates.Should().BeEmpty(
            "IModule.Name keys the dependency-resolution dictionary in ModuleLoader.ResolveOrder. " +
            "Two modules sharing a name would silently overwrite one another. See spec §2 Theme 3.");
    }

    [Fact]
    public void All_loaded_modules_have_a_non_empty_name()
    {
        var modules = ModuleLoader.DiscoverModules();
        var bad = modules.Where(m => string.IsNullOrWhiteSpace(m.Name))
            .Select(m => m.GetType().FullName)
            .ToList();

        bad.Should().BeEmpty();
    }
}
```

- [ ] **Step 2: Run it**

```bash
cd boilerplateBE && dotnet test tests/Starter.Api.Tests/Starter.Api.Tests.csproj --filter "FullyQualifiedName~ModuleNameUniquenessTests" --nologo
```

Expected: PASS.

- [ ] **Step 3: Commit**

```bash
git add boilerplateBE/tests/Starter.Api.Tests/Architecture/ModuleNameUniquenessTests.cs
git commit -m "test(modules): IModule.Name uniqueness across discovered modules"
```

---

## Task 8: BE — permission uniqueness, naming convention, role-mapping integrity

**Files:**
- Create: `boilerplateBE/tests/Starter.Api.Tests/Architecture/ModulePermissionTests.cs`

- [ ] **Step 1: Create the file**

```csharp
using System.Text.RegularExpressions;
using FluentAssertions;
using Starter.Abstractions.Modularity;
using Starter.Shared.Constants;
using Xunit;

namespace Starter.Api.Tests.Architecture;

public class ModulePermissionTests
{
    private static readonly Regex PermissionPattern = new(@"^[A-Z][A-Za-z0-9]*\.[A-Z][A-Za-z0-9]*$", RegexOptions.Compiled);

    [Fact]
    public void Module_permission_names_are_unique_across_modules_and_core()
    {
        var modules = ModuleLoader.DiscoverModules();
        var owners = new Dictionary<string, string>(StringComparer.Ordinal);
        var duplicates = new List<string>();

        // Seed with core permissions so module collisions with core are caught too.
        foreach (var (name, _, module) in Permissions.GetAllWithMetadata())
        {
            owners[name] = $"core/{module}";
        }

        foreach (var module in modules)
        {
            foreach (var (name, _, _) in module.GetPermissions())
            {
                if (owners.TryGetValue(name, out var owner))
                    duplicates.Add($"'{name}' declared by both {owner} and {module.GetType().FullName}");
                else
                    owners[name] = module.GetType().FullName!;
            }
        }

        duplicates.Should().BeEmpty(
            "Two modules (or a module and core) declaring the same permission string causes the seeder " +
            "to upsert one and silently lose the other. See spec §2 Theme 3.");
    }

    [Fact]
    public void Module_permission_names_match_naming_convention()
    {
        var modules = ModuleLoader.DiscoverModules();
        var bad = new List<string>();

        foreach (var module in modules)
        {
            foreach (var (name, _, _) in module.GetPermissions())
            {
                if (!PermissionPattern.IsMatch(name))
                    bad.Add($"'{name}' from {module.GetType().FullName} does not match {{Module}}.{{Action}} (PascalCase)");
            }
        }

        bad.Should().BeEmpty(
            "Permission strings must match the documented {Module}.{Action} convention so the seeder, " +
            "policy provider, and frontend permission map all agree on the shape. " +
            "See Starter.Shared/Constants/Permissions.cs header.");
    }

    [Fact]
    public void Default_role_permissions_reference_real_permissions()
    {
        var modules = ModuleLoader.DiscoverModules();
        var orphans = new List<string>();

        foreach (var module in modules)
        {
            var declared = module.GetPermissions().Select(p => p.Name).ToHashSet(StringComparer.Ordinal);

            foreach (var (role, perms) in module.GetDefaultRolePermissions())
            {
                foreach (var perm in perms)
                {
                    if (!declared.Contains(perm))
                        orphans.Add($"{module.GetType().FullName}: role '{role}' references undeclared permission '{perm}'");
                }
            }
        }

        orphans.Should().BeEmpty(
            "GetDefaultRolePermissions() may only reference permissions returned by GetPermissions() " +
            "on the same module. Typos here are silently dropped by the seeder.");
    }
}
```

- [ ] **Step 2: Run it**

```bash
cd boilerplateBE && dotnet test tests/Starter.Api.Tests/Starter.Api.Tests.csproj --filter "FullyQualifiedName~ModulePermissionTests" --nologo
```

Expected: PASS. If any module's permissions violate the convention, **stop and fix the module** rather than relax the test — this is the audit's "naming inconsistency" finding.

- [ ] **Step 3: If any test fails, fix the underlying module(s)**

Likely candidates from the audit: permission count varies wildly across modules; some modules may use `Comments.View` vs `CommentsActivity.View`. Bring them into convention. Document any rename in the commit message.

- [ ] **Step 4: Commit**

```bash
git add boilerplateBE/tests/Starter.Api.Tests/Architecture/ModulePermissionTests.cs
# plus any module Permissions.cs files you had to fix
git commit -m "test(modules): permission uniqueness, naming convention, role-mapping integrity"
```

---

## Task 9: FE — extend ESLint to block `import type` from optional modules

**Files:**
- Modify: `boilerplateFE/eslint.config.js`

- [ ] **Step 1: Read the current rule**

Open `boilerplateFE/eslint.config.js`. Find the `no-restricted-imports` rule that lists `@/features/billing`, `@/features/webhooks`, etc. as restricted patterns.

- [ ] **Step 2: Update the rule**

For each restricted pattern entry, add `"allowTypeImports": false` (the ESLint-flat-config equivalent — actual property name in `@typescript-eslint/no-restricted-imports` is `allowTypeImports`). If using the core rule (`no-restricted-imports`), switch to `@typescript-eslint/no-restricted-imports` which supports the option.

Example transformation (conceptual):

```js
// before
{
  patterns: [
    { group: ['@/features/billing/**'], message: '...' },
    // ...
  ]
}
// after
{
  patterns: [
    { group: ['@/features/billing/**'], message: '...', allowTypeImports: false },
    // ...
  ]
}
```

If the rule is the base `no-restricted-imports`, replace it with `@typescript-eslint/no-restricted-imports` and add the typescript-eslint plugin if not already loaded.

- [ ] **Step 3: Verify on a planted bad import**

In a core file (e.g. `boilerplateFE/src/components/layout/MainLayout/Sidebar.tsx`), temporarily add:

```ts
import type { BillingPlan } from '@/features/billing/types';
```

Run `npm run lint`. Expect it to fail. Remove the planted import.

- [ ] **Step 4: Run full lint**

```bash
cd boilerplateFE && npm run lint
```

Expected: PASS (the codebase shouldn't have any type imports from optional modules in core files; if it does, fix them — that's an audit finding).

- [ ] **Step 5: Commit**

```bash
git add boilerplateFE/eslint.config.js
git commit -m "chore(fe): block 'import type' from optional modules in core files"
```

---

## Task 10: Mobile — Dart test enforcing core/shell module isolation

**Files:**
- Create: `boilerplateMobile/test/architecture/module_isolation_test.dart`

- [ ] **Step 1: Create the file**

```dart
import 'dart:io';
import 'package:test/test.dart';

void main() {
  group('Mobile module isolation', () {
    test('lib/core and lib/app/shell never import lib/modules/* '
         'except through lib/app/modules.config.dart', () {
      final repoRoot = _findRepoRoot();
      final libDir = Directory('${repoRoot.path}/boilerplateMobile/lib');
      final allowedRegistry = '${libDir.path}/app/modules.config.dart';

      final scannedDirs = [
        Directory('${libDir.path}/core'),
        Directory('${libDir.path}/app/shell'),
      ];

      final modulesImportRegex = RegExp(
        r'''import\s+['"](?:package:[a-z_]+/)?modules/''',
      );

      final violations = <String>[];
      for (final dir in scannedDirs) {
        if (!dir.existsSync()) continue;
        for (final entity in dir.listSync(recursive: true)) {
          if (entity is! File) continue;
          if (!entity.path.endsWith('.dart')) continue;
          if (entity.path == allowedRegistry) continue;

          final lines = entity.readAsLinesSync();
          for (var i = 0; i < lines.length; i++) {
            if (modulesImportRegex.hasMatch(lines[i])) {
              violations.add('${entity.path}:${i + 1}: ${lines[i].trim()}');
            }
          }
        }
      }

      expect(
        violations,
        isEmpty,
        reason: 'lib/core and lib/app/shell must not import lib/modules/* directly. '
                'Use the slot/registry contracts from lib/core/modularity instead. '
                'The only exemption is lib/app/modules.config.dart (the registry itself). '
                'Mirror of backend ModuleIsolationTests and frontend ESLint no-restricted-imports.',
      );
    });
  });
}

Directory _findRepoRoot() {
  var current = Directory.current;
  for (var i = 0; i < 8; i++) {
    if (File('${current.path}/modules.catalog.json').existsSync()) return current;
    final parent = current.parent;
    if (parent.path == current.path) break;
    current = parent;
  }
  throw StateError('Could not find repo root (modules.catalog.json) walking up from ${Directory.current.path}');
}
```

- [ ] **Step 2: Run it**

```bash
cd boilerplateMobile && flutter test test/architecture/module_isolation_test.dart
```

Expected: PASS.

- [ ] **Step 3: Verify it would fail**

Temporarily add `import '../modules/billing/billing_module.dart';` to `lib/core/modularity/module_registry.dart`. Run the test. Confirm FAIL with a clear violation message. Remove the planted import.

- [ ] **Step 4: Commit**

```bash
git add boilerplateMobile/test/architecture/module_isolation_test.dart
git commit -m "test(mobile): enforce module isolation in lib/core and lib/app/shell"
```

---

## Task 11: Final verification + roll-up commit

- [ ] **Step 1: Run all backend tests**

```bash
cd boilerplateBE && dotnet test tests/Starter.Api.Tests/Starter.Api.Tests.csproj --filter "FullyQualifiedName~Architecture" --nologo
```

Expected: ALL PASS.

- [ ] **Step 2: Run frontend lint + typecheck**

```bash
cd boilerplateFE && npm run lint && npm run build
```

Expected: PASS.

- [ ] **Step 3: Run mobile tests**

```bash
cd boilerplateMobile && flutter analyze && flutter test test/architecture/
```

Expected: PASS.

- [ ] **Step 4: Confirm no regressions in catalog-driven generation**

```bash
cd /tmp && rm -rf tier25-smoke && mkdir tier25-smoke
pwsh -File <repo>/scripts/rename.ps1 -Name "_tier25Smoke" -OutputDir "/tmp/tier25-smoke" -Modules "All" -IncludeMobile:$false 2>&1 | tail -10
```

Expected: rename succeeds. (Skip if `pwsh` isn't installed locally; CI in Theme 2 will gate this.)

- [ ] **Step 5: Push the branch and open the PR**

```bash
git push -u origin codex/modularity-tier-3   # branch name predates Tier 2.5 plan; keep it for now
gh pr create --title "Modularity Tier 2.5 — Themes 1+3: catalog v2 schema + arch test gap closure" \
  --body "$(cat <<'EOF'
## Summary

First PR in the Tier 2.5 hardening series (see [spec](docs/superpowers/specs/2026-04-29-modularity-tier-2-5-hardening.md)).

**Theme 1 — Catalog v2 schema:** added `version` and `supportedPlatforms` (required) to every module entry; reserved `coreCompat` and `packageId` (optional, validated when present, unused at runtime until Tier 3).

**Theme 3 — Architecture test gap closure:**
- Catalog: schema completeness + `supportedPlatforms` consistency + dependency platform compatibility + `coreCompat`/`packageId` shape
- BE: `IModule.Name` uniqueness (real modules + helpful error in `ModuleLoader.ResolveOrder`)
- BE: permission name uniqueness, naming convention, role-mapping integrity
- FE: ESLint blocks `import type` from optional modules (was runtime-only)
- Mobile: new `module_isolation_test.dart` mirrors the BE/FE rule for `lib/core` and `lib/app/shell`

No behavior changes; tests all green; verified each test catches its target failure mode by planting an intentional bad value before reverting.

## Test plan

- [ ] `dotnet test --filter "FullyQualifiedName~Architecture"` green
- [ ] `npm run lint` + `npm run build` green
- [ ] `flutter analyze` + `flutter test test/architecture/` green
- [ ] Spot-check rename `-Modules All` still generates a buildable app
EOF
)"
```

---

## Self-review

**Spec coverage check:**
- ✅ Theme 1 — `version`, `supportedPlatforms` added (Tasks 1–3); `coreCompat`, `packageId` validated-when-present (Task 5).
- ✅ Theme 3 — Module name uniqueness (Tasks 6–7); permission uniqueness + naming + role mapping (Task 8); FE type-import block (Task 9); mobile isolation test (Task 10).

**Deferred from Theme 3 (called out in spec):**
- "Capability registration coverage" — requires per-module manifest; explicitly deferred in the spec.
- "Permission_string_format_matches_naming_convention" — included as Task 8.

**Placeholder scan:** none.

**Type consistency:** all helper methods (`ReadOptionalString`, `ReadStringArray`, `ModuleEntries`, etc.) defined in `CatalogConsistencyTests.cs` and re-used. No mismatched signatures.

---

## Execution

This plan executes inline (auto mode is on). Each task ends with a green test run + a commit. After all tasks, the roll-up Task 11 verifies cross-platform and opens the PR.
