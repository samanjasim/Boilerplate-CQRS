# Module System Tier 1 — Host Cleanup Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Eliminate core→module leakage in `Starter.Api`, add strict module-dependency validation at both CLI generation time and backend startup, move the catalog to repo root, and add `Starter.Module.AI` to it. Closes Phase 1 of the [Hybrid Full-Stack Module System Design](../specs/2026-04-28-hybrid-full-stack-module-system-design.md) per the [resolved decisions in §14](../specs/2026-04-28-hybrid-full-stack-module-system-design.md#14-resolved-decisions-2026-04-28).

**Architecture:** Introduce one new optional module contract (`IModuleBusContributor`) so that Workflow's MassTransit outbox configuration moves from `Program.cs` into the Workflow module itself, with the API project iterating discovered contributors via the existing `configureBus` callback hook. Strict dependency checks fail loud at both startup (`ModuleLoader.ResolveOrder`) and generation (`rename.ps1`), with self-healing error messages. Catalog moves from `scripts/modules.json` to `/modules.catalog.json` and gains a `dependencies` array per module. No new abstractions beyond what each scope item demands — defer the other contributor interfaces (`IModuleHealthContributor`, `IModuleApiContributor`, `IModuleDependencyMetadata`) until a second consumer needs them.

**Tech Stack:** .NET 10, xUnit + FluentAssertions + NetArchTest.Rules (existing arch-test pattern), MassTransit (existing), PowerShell 7 (rename.ps1), JSON catalog.

---

## Pre-flight: Worktree

This plan touches the API project, a module, the abstractions, the test project, and the rename script. Use a dedicated worktree:

```bash
git worktree add -b module-system/tier-1-host-cleanup ../boilerplate-cqrs-tier-1
cd ../boilerplate-cqrs-tier-1
```

If executing in the current branch (`codex/modularity-review`) is acceptable, skip the worktree — but the plan was written assuming a clean dedicated branch.

## File Structure

**New files:**

| Path | Purpose |
|---|---|
| `/modules.catalog.json` | Module catalog at repo root (replaces `scripts/modules.json`). Adds `dependencies` field per module and `Starter.Module.AI` entry. |
| `boilerplateBE/src/Starter.Infrastructure/Modularity/IModuleBusContributor.cs` | Optional module-host contract for MassTransit bus configuration. Lives in Infrastructure because MassTransit is forbidden from Starter.Abstractions. |
| `boilerplateBE/tests/Starter.Api.Tests/Architecture/ModuleIsolationTests.cs` | NetArchTest: `Starter.Api` types cannot depend on `Starter.Module.*` namespaces. |
| `boilerplateBE/tests/Starter.Api.Tests/Architecture/ModuleLoaderTests.cs` | xUnit: `ModuleLoader.ResolveOrder` throws when a declared dependency is not installed. |

**Modified files:**

| Path | Change |
|---|---|
| `scripts/modules.json` | **Deleted** (content moves to `/modules.catalog.json`). |
| `scripts/rename.ps1` | Update catalog path constant (`scripts/modules.json` → `modules.catalog.json` at repo root), add strict dependency validation with self-healing error message. |
| `boilerplateBE/src/Starter.Abstractions/Modularity/ModuleLoader.cs` | `ResolveOrder` throws on missing declared dependency (today silently skips). |
| `boilerplateBE/src/Starter.Api/Program.cs` | Remove `using Starter.Module.Workflow.Infrastructure;` (line 7) and `bus.AddWorkflowOutbox()` (line 51). Iterate `IModuleBusContributor` from discovered modules instead. |
| `boilerplateBE/src/Starter.Api/Program.Tooling.cs` | Same pattern as `Program.cs`. |
| `boilerplateBE/src/modules/Starter.Module.Workflow/WorkflowModule.cs` | Implement `IModuleBusContributor` and own its outbox configuration. |
| `boilerplateBE/src/modules/Starter.Module.Workflow/Infrastructure/WorkflowMassTransitExtensions.cs` | **Deleted** (body inlined into `WorkflowModule.ConfigureBus`). |

**Not changed (deliberately):**

- `boilerplateBE/src/Starter.Api/Starter.Api.csproj` keeps every `<ProjectReference>` to module projects. They are required for `ModuleLoader.DiscoverModules()` to find module assemblies in the bin output. The arch test enforces *type-level* purity, not assembly-level — which is the right granularity.
- `IModule` contract is unchanged. `Dependencies` already exists; we just enforce it.

---

## Task 1: Move catalog to repo root, add AI, add dependencies

**Files:**
- Create: `modules.catalog.json` (repo root)
- Modify: `scripts/rename.ps1` (line 358)
- Delete: `scripts/modules.json`

- [ ] **Step 1: Create `/modules.catalog.json` at repo root**

Write this exact content to `/modules.catalog.json` (note: AI added with `testsFolder: "Ai"`, every entry now has a `dependencies` array, only Workflow has non-empty deps per the design doc §5):

```json
{
  "_comment": "Module catalog (single source of truth). Used by rename.ps1, ModuleLoader, and architecture tests. Removable by passing -Modules None (strip all), -Modules commentsActivity,communication (keep only these), or -Modules All (default). Files, Notifications, FeatureFlags, ApiKeys, AuditLogs, and Reports are core and always ship. The dependencies array is enforced by both the CLI and ModuleLoader.ResolveOrder at startup; missing dependencies fail loud. The testsFolder field names the module's test subfolder under tests/Starter.Api.Tests/ so the rename script deletes orphan tests alongside the module.",
  "ai": {
    "displayName": "AI Integration",
    "backendModule": "Starter.Module.AI",
    "testsFolder": "Ai",
    "configKey": "ai",
    "required": false,
    "dependencies": [],
    "description": "AI assistants, RAG ingestion, agents, providers (OpenAI, Anthropic), eval harness. Implements IAiService; null fallback returns empty results."
  },
  "billing": {
    "displayName": "Billing",
    "backendModule": "Starter.Module.Billing",
    "frontendFeature": "billing",
    "mobileModule": "BillingModule",
    "mobileFolder": "billing",
    "configKey": "billing",
    "required": false,
    "dependencies": [],
    "description": "Subscription plans, payments, free-tier auto-provisioning. Implements IBillingProvider; null fallback returns 501. Frontend slot: tenant-detail-tabs. Mobile: plans list + subscription view."
  },
  "webhooks": {
    "displayName": "Webhooks",
    "backendModule": "Starter.Module.Webhooks",
    "frontendFeature": "webhooks",
    "configKey": "webhooks",
    "required": false,
    "dependencies": [],
    "description": "Webhook endpoints, delivery, admin dashboard. Implements IWebhookPublisher; null fallback is a silent no-op. No mobile counterpart (admin-only feature)."
  },
  "importExport": {
    "displayName": "Import / Export",
    "backendModule": "Starter.Module.ImportExport",
    "frontendFeature": "import-export",
    "configKey": "importExport",
    "required": false,
    "dependencies": [],
    "description": "Data import/export with async processing. Implements IImportExportRegistry; null fallback returns empty lists. Frontend slot: users-list-toolbar. No mobile counterpart (admin-only feature)."
  },
  "products": {
    "displayName": "Products",
    "backendModule": "Starter.Module.Products",
    "frontendFeature": "products",
    "configKey": "products",
    "required": false,
    "dependencies": [],
    "description": "E-commerce product catalog with CRUD, image upload, demo catalog seeding. Uses IQuotaChecker + IWebhookPublisher capabilities. Frontend slots: tenant-detail-tabs, dashboard-cards."
  },
  "commentsActivity": {
    "displayName": "Comments & Activity",
    "backendModule": "Starter.Module.CommentsActivity",
    "frontendFeature": "comments-activity",
    "testsFolder": "CommentsActivity",
    "configKey": "commentsActivity",
    "required": false,
    "dependencies": [],
    "description": "Entity-scoped comments, activity feed, reactions, watchers, mentions. Implements ICommentableEntityRegistry, ICommentService, IActivityService, IEntityWatcherService; null fallbacks return empty results. Frontend slot: entity-detail-timeline. No mobile counterpart."
  },
  "communication": {
    "displayName": "Communication",
    "backendModule": "Starter.Module.Communication",
    "frontendFeature": "communication",
    "testsFolder": "Communication",
    "configKey": "communication",
    "required": false,
    "dependencies": [],
    "description": "Multi-channel messaging: email (SMTP), Slack, Telegram, Discord, Teams; templates, trigger rules, delivery log. Implements IMessageDispatcher, ICommunicationEventNotifier, ITemplateRegistrar; null fallbacks are silent no-ops. Frontend slot: dashboard-cards. No mobile counterpart."
  },
  "workflow": {
    "displayName": "Workflow & Approvals",
    "backendModule": "Starter.Module.Workflow",
    "frontendFeature": "workflow",
    "testsFolder": "Workflow",
    "configKey": "workflow",
    "required": false,
    "dependencies": ["commentsActivity", "communication"],
    "description": "Configurable state-machine workflows with approval chains, task inbox, and process automation. Depends on commentsActivity (registers WorkflowInstance as commentable entity) and communication (registers email templates via ITemplateRegistrar). Implements IWorkflowService; null fallback returns empty results. Frontend: inbox page, admin definitions, entity status panel slot."
  }
}
```

- [ ] **Step 2: Update `rename.ps1` catalog path**

Open `scripts/rename.ps1` line 358 and change the path constant. Replace this exact line:

```powershell
$modulesJsonPath = Join-Path (Join-Path $RepoRoot "scripts") "modules.json"
```

With:

```powershell
$modulesJsonPath = Join-Path $RepoRoot "modules.catalog.json"
```

- [ ] **Step 3: Delete the old `scripts/modules.json`**

```bash
rm scripts/modules.json
```

- [ ] **Step 4: Smoke test — rename.ps1 reads the new catalog**

This step does **not** generate a test app — it only verifies the script can parse the new file and discovers the new AI entry. Run from the repo root:

```bash
pwsh -NoProfile -Command "
  \$cfg = Get-Content modules.catalog.json -Raw | ConvertFrom-Json
  \$names = \$cfg.PSObject.Properties | Where-Object { \$_.Name -ne '_comment' } | Select-Object -ExpandProperty Name
  Write-Host 'Modules in catalog:' (\$names -join ', ')
  if (\$names -notcontains 'ai') { Write-Error 'AI missing from catalog'; exit 1 }
  if ((\$cfg.workflow.dependencies -join ',') -ne 'commentsActivity,communication') { Write-Error 'workflow.dependencies wrong'; exit 1 }
  Write-Host 'OK'
"
```

Expected output:
```
Modules in catalog: ai, billing, webhooks, importExport, products, commentsActivity, communication, workflow
OK
```

- [ ] **Step 5: Commit**

```bash
git add modules.catalog.json scripts/rename.ps1
git rm scripts/modules.json
git commit -m "feat(modules): move catalog to repo root and add AI entry

Catalog is now the single source of truth (see spec §5 + §14 D2). Adds
Starter.Module.AI entry that was previously missing, and a dependencies
array per module. Workflow declares its real deps on commentsActivity
and communication.

Refs: docs/superpowers/specs/2026-04-28-hybrid-full-stack-module-system-design.md"
```

---

## Task 2: Strict dependency validation in `ModuleLoader.ResolveOrder`

**Files:**
- Create: `boilerplateBE/tests/Starter.Api.Tests/Architecture/ModuleLoaderTests.cs`
- Modify: `boilerplateBE/src/Starter.Abstractions/Modularity/ModuleLoader.cs` (lines 83-87)

- [ ] **Step 1: Write the failing test**

Create `boilerplateBE/tests/Starter.Api.Tests/Architecture/ModuleLoaderTests.cs` with this exact content:

```csharp
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Starter.Abstractions.Modularity;
using Xunit;

namespace Starter.Api.Tests.Architecture;

public class ModuleLoaderTests
{
    [Fact]
    public void ResolveOrder_throws_when_a_declared_dependency_is_not_installed()
    {
        var moduleA = new FakeModule("A", dependencies: ["B"]);
        var modules = new List<IModule> { moduleA };

        var act = () => ModuleLoader.ResolveOrder(modules);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*'A'*'B'*not installed*");
    }

    [Fact]
    public void ResolveOrder_succeeds_when_all_declared_dependencies_are_installed()
    {
        var moduleA = new FakeModule("A", dependencies: ["B"]);
        var moduleB = new FakeModule("B");
        var modules = new List<IModule> { moduleA, moduleB };

        var ordered = ModuleLoader.ResolveOrder(modules);

        ordered.Select(m => m.Name).Should().Equal("B", "A");
    }

    [Fact]
    public void ResolveOrder_includes_installed_module_names_in_error_message()
    {
        var moduleA = new FakeModule("A", dependencies: ["Missing"]);
        var moduleB = new FakeModule("B");
        var modules = new List<IModule> { moduleA, moduleB };

        var act = () => ModuleLoader.ResolveOrder(modules);

        act.Should().Throw<InvalidOperationException>()
            .Where(e => e.Message.Contains("A") && e.Message.Contains("B"));
    }

    private sealed class FakeModule : IModule
    {
        public FakeModule(string name, params string[] dependencies)
        {
            Name = name;
            Dependencies = dependencies;
        }

        public string Name { get; }
        public string DisplayName => Name;
        public string Version => "1.0.0";
        public IReadOnlyList<string> Dependencies { get; }

        public IServiceCollection ConfigureServices(IServiceCollection services, IConfiguration configuration)
            => services;

        public IEnumerable<(string Name, string Description, string Module)> GetPermissions() => [];

        public IEnumerable<(string Role, string[] Permissions)> GetDefaultRolePermissions() => [];
    }
}
```

- [ ] **Step 2: Run the tests and verify they fail**

```bash
cd boilerplateBE
dotnet test tests/Starter.Api.Tests/Starter.Api.Tests.csproj \
  --filter "FullyQualifiedName~ModuleLoaderTests" --nologo
```

Expected: `ResolveOrder_throws_when_a_declared_dependency_is_not_installed` and `ResolveOrder_includes_installed_module_names_in_error_message` both **fail** because the current code silently skips missing deps. `ResolveOrder_succeeds_when_all_declared_dependencies_are_installed` passes (already-correct behavior).

- [ ] **Step 3: Modify `ModuleLoader.ResolveOrder` to throw on missing dependency**

Open `boilerplateBE/src/Starter.Abstractions/Modularity/ModuleLoader.cs` and find the inner `Visit` function (around lines 83-87) that currently reads:

```csharp
foreach (var dep in module.Dependencies)
{
    if (moduleMap.TryGetValue(dep, out var depModule))
    {
        Visit(depModule);
    }
}
```

Replace with:

```csharp
foreach (var dep in module.Dependencies)
{
    if (!moduleMap.TryGetValue(dep, out var depModule))
    {
        throw new InvalidOperationException(
            $"Module '{module.Name}' declares a dependency on '{dep}', but '{dep}' is not installed. " +
            $"Installed modules: {string.Join(", ", moduleMap.Keys)}.");
    }

    Visit(depModule);
}
```

- [ ] **Step 4: Run the tests and verify they pass**

```bash
cd boilerplateBE
dotnet test tests/Starter.Api.Tests/Starter.Api.Tests.csproj \
  --filter "FullyQualifiedName~ModuleLoaderTests" --nologo
```

Expected: all three `ModuleLoaderTests` pass.

- [ ] **Step 5: Run the full backend test suite to confirm no regression**

```bash
cd boilerplateBE
dotnet test tests/Starter.Api.Tests/Starter.Api.Tests.csproj --nologo
```

Expected: every test that ran before still passes. (If any pre-existing tests were inadvertently relying on `ResolveOrder` silently dropping a missing dep, they would fail here — none are expected, but verify.)

- [ ] **Step 6: Commit**

```bash
git add boilerplateBE/src/Starter.Abstractions/Modularity/ModuleLoader.cs \
        boilerplateBE/tests/Starter.Api.Tests/Architecture/ModuleLoaderTests.cs
git commit -m "feat(modularity): fail loud when a module's declared dependency is missing

ModuleLoader.ResolveOrder used to silently skip missing dependencies in
the topological sort; that hid configuration drift between the catalog
and what's actually loaded. Now throws InvalidOperationException with
the offending module, missing dep name, and the full list of installed
modules.

Refs: spec §6.5"
```

---

## Task 3: `IModuleBusContributor` + Workflow migration + arch test

This is the largest task. We add the new contract, migrate Workflow off `Program.cs`, delete the obsolete extension class, and prove isolation with a new arch test. The arch test is written first (TDD) and will fail until the migration completes — the steps interleave to keep each commit green.

**Files:**
- Create: `boilerplateBE/src/Starter.Infrastructure/Modularity/IModuleBusContributor.cs`
- Create: `boilerplateBE/tests/Starter.Api.Tests/Architecture/ModuleIsolationTests.cs`
- Modify: `boilerplateBE/src/modules/Starter.Module.Workflow/WorkflowModule.cs`
- Modify: `boilerplateBE/src/Starter.Api/Program.cs`
- Modify: `boilerplateBE/src/Starter.Api/Program.Tooling.cs`
- Delete: `boilerplateBE/src/modules/Starter.Module.Workflow/Infrastructure/WorkflowMassTransitExtensions.cs`

- [ ] **Step 1: Write the failing module-isolation arch test**

Create `boilerplateBE/tests/Starter.Api.Tests/Architecture/ModuleIsolationTests.cs` with this exact content:

```csharp
using FluentAssertions;
using NetArchTest.Rules;
using Starter.Api.Configurations;
using Xunit;

namespace Starter.Api.Tests.Architecture;

public class ModuleIsolationTests
{
    private static readonly string[] OptionalModuleNamespaces =
    [
        "Starter.Module.AI",
        "Starter.Module.Billing",
        "Starter.Module.CommentsActivity",
        "Starter.Module.Communication",
        "Starter.Module.ImportExport",
        "Starter.Module.Products",
        "Starter.Module.Webhooks",
        "Starter.Module.Workflow",
    ];

    [Fact]
    public void Starter_Api_must_not_use_types_from_optional_modules()
    {
        // Use any public type from Starter.Api to grab its assembly.
        var apiAssembly = typeof(OpenTelemetryConfiguration).Assembly;

        var result = Types.InAssembly(apiAssembly)
            .Should()
            .NotHaveDependencyOnAny(OptionalModuleNamespaces)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            "Starter.Api is the module host — it must compose modules through neutral contracts " +
            "(IModule, IModuleBusContributor, etc.) and never reference optional module internals. " +
            "If this fails, move the offending logic into the relevant module and have it implement " +
            "the appropriate host contract. See docs/superpowers/specs/2026-04-28-hybrid-full-stack-module-system-design.md §6. " +
            "Offending types: " + (result.FailingTypes is null
                ? "<none>"
                : string.Join(", ", result.FailingTypes.Select(t => t.FullName))));
    }
}
```

- [ ] **Step 2: Run the new arch test and verify it fails**

```bash
cd boilerplateBE
dotnet test tests/Starter.Api.Tests/Starter.Api.Tests.csproj \
  --filter "FullyQualifiedName~ModuleIsolationTests" --nologo
```

Expected: **FAIL** because `Program.cs` line 7 (`using Starter.Module.Workflow.Infrastructure;`) and line 51 (`bus.AddWorkflowOutbox()`) cause `Starter.Api` to depend on `Starter.Module.Workflow`. The failure message should list at least one `Program`-related type.

- [ ] **Step 3: Define `IModuleBusContributor`**

Create `boilerplateBE/src/Starter.Infrastructure/Modularity/IModuleBusContributor.cs` with this exact content:

```csharp
using MassTransit;

namespace Starter.Infrastructure.Modularity;

/// <summary>
/// Optional module-host contract. Modules that need to register MassTransit infrastructure
/// (e.g. an additional EF Core outbox bound to the module's DbContext, custom consumers,
/// endpoint policies) implement this alongside <see cref="Starter.Abstractions.Modularity.IModule"/>.
/// The module host invokes <see cref="ConfigureBus"/> for every registered contributor inside
/// the bus configuration callback. Core code never references module-specific extension methods.
/// </summary>
public interface IModuleBusContributor
{
    void ConfigureBus(IBusRegistrationConfigurator bus);
}
```

- [ ] **Step 4: Make `WorkflowModule` implement `IModuleBusContributor`**

Open `boilerplateBE/src/modules/Starter.Module.Workflow/WorkflowModule.cs`. Add the using import at the top of the file (alongside existing usings):

```csharp
using MassTransit;
using Starter.Infrastructure.Modularity;
using Starter.Module.Workflow.Infrastructure;
```

Update the class declaration (around line 14) to also implement the new interface:

```csharp
public sealed class WorkflowModule : IModule, IModuleBusContributor
```

Add this method to the class body (place it next to `ConfigureServices`):

```csharp
public void ConfigureBus(IBusRegistrationConfigurator bus)
{
    // Registers a transactional EF outbox against WorkflowDbContext. With UseBusOutbox(),
    // IPublishEndpoint.Publish and ISendEndpoint.Send calls made while WorkflowDbContext is
    // the active DbContext are queued in the workflow outbox table and committed in the
    // same transaction as WorkflowDbContext.SaveChanges. MassTransit's background delivery
    // service then drains the outbox to the broker.
    bus.AddEntityFrameworkOutbox<WorkflowDbContext>(o =>
    {
        o.QueryDelay = TimeSpan.FromSeconds(1);
        o.UsePostgres();
        o.UseBusOutbox();
    });
}
```

- [ ] **Step 5: Delete the obsolete extension class**

`WorkflowMassTransitExtensions.AddWorkflowOutbox` is no longer called from anywhere — the body is now inlined into `WorkflowModule.ConfigureBus`. Delete the file:

```bash
rm boilerplateBE/src/modules/Starter.Module.Workflow/Infrastructure/WorkflowMassTransitExtensions.cs
```

- [ ] **Step 6: Update `Program.cs` to use the contributor pattern**

Open `boilerplateBE/src/Starter.Api/Program.cs`. Remove this line (line 7):

```csharp
using Starter.Module.Workflow.Infrastructure;
```

Add this import in the same area:

```csharp
using Starter.Infrastructure.Modularity;
```

Find the `AddInfrastructure` call (around line 49–52) which currently reads:

```csharp
builder.Services.AddInfrastructure(
    builder.Configuration,
    moduleAssemblies,
    configureBus: bus => bus.AddWorkflowOutbox());
```

Replace with:

```csharp
builder.Services.AddInfrastructure(
    builder.Configuration,
    moduleAssemblies,
    configureBus: bus =>
    {
        foreach (var contributor in orderedModules.OfType<IModuleBusContributor>())
        {
            contributor.ConfigureBus(bus);
        }
    });
```

`orderedModules` is the local variable already populated on line 39 (`ModuleLoader.ResolveOrder(modules)`) — no other change needed.

- [ ] **Step 7: Update `Program.Tooling.cs` the same way**

Open `boilerplateBE/src/Starter.Api/Program.Tooling.cs`. Remove this line (line 6):

```csharp
using Starter.Module.Workflow.Infrastructure;
```

Add:

```csharp
using Starter.Infrastructure.Modularity;
```

Find the `AddInfrastructure` call (around line 29) which currently reads:

```csharp
services.AddInfrastructure(config, moduleAssemblies, configureBus: bus => bus.AddWorkflowOutbox());
```

Replace with:

```csharp
services.AddInfrastructure(
    config,
    moduleAssemblies,
    configureBus: bus =>
    {
        foreach (var contributor in orderedModules.OfType<IModuleBusContributor>())
        {
            contributor.ConfigureBus(bus);
        }
    });
```

- [ ] **Step 8: Build the backend**

```bash
cd boilerplateBE
dotnet build Starter.sln --nologo
```

Expected: build succeeds. If it fails with `CS0234: type or namespace 'Workflow' does not exist in 'Starter.Module'`, an unmigrated callsite remains — search with `grep -rn "AddWorkflowOutbox\|using Starter.Module.Workflow" src/Starter.Api` and fix.

- [ ] **Step 9: Run the architecture test and verify it now passes**

```bash
cd boilerplateBE
dotnet test tests/Starter.Api.Tests/Starter.Api.Tests.csproj \
  --filter "FullyQualifiedName~ModuleIsolationTests" --nologo
```

Expected: **PASS**.

- [ ] **Step 10: Run the full backend test suite**

```bash
cd boilerplateBE
dotnet test tests/Starter.Api.Tests/Starter.Api.Tests.csproj --nologo
```

Expected: every previously-green test stays green. Pay special attention to `MessagingArchitectureTests` and `AbstractionsPurityTests` — both should still pass.

- [ ] **Step 11: Smoke test the API boots and Workflow's outbox is registered**

Start the API:

```bash
cd boilerplateBE/src/Starter.Api
dotnet run --launch-profile http
```

Expected (in logs): no exceptions during startup, MassTransit logs show **two** EF outboxes registered — one for `ApplicationDbContext` (core) and one for `WorkflowDbContext` (from `WorkflowModule.ConfigureBus`). Hit `Ctrl+C` after confirming.

If the Workflow outbox is missing, a likely cause is `WorkflowModule` not appearing in `orderedModules.OfType<IModuleBusContributor>()` — check that the `IModuleBusContributor` interface is implemented on `WorkflowModule` (Step 4) and that `Starter.Infrastructure.Modularity` is imported in `WorkflowModule.cs`.

- [ ] **Step 12: Commit**

```bash
git add boilerplateBE/src/Starter.Infrastructure/Modularity/IModuleBusContributor.cs \
        boilerplateBE/src/modules/Starter.Module.Workflow/WorkflowModule.cs \
        boilerplateBE/src/Starter.Api/Program.cs \
        boilerplateBE/src/Starter.Api/Program.Tooling.cs \
        boilerplateBE/tests/Starter.Api.Tests/Architecture/ModuleIsolationTests.cs
git rm boilerplateBE/src/modules/Starter.Module.Workflow/Infrastructure/WorkflowMassTransitExtensions.cs
git commit -m "feat(modularity): introduce IModuleBusContributor; remove core->Workflow leak

Workflow's MassTransit EF outbox now configures itself via the new
IModuleBusContributor contract instead of being called from Program.cs.
Adds an architecture test that fails the build if Starter.Api ever
reintroduces a direct dependency on any Starter.Module.* namespace.

This is the only contributor interface introduced in Tier 1 — others
(IModuleHealthContributor, IModuleApiContributor, IModuleDependencyMetadata)
are deferred until a second consumer needs them.

Refs: spec §6.1, §6.2, §10"
```

---

## Task 4: `rename.ps1` strict dependency validation

`rename.ps1` already filters modules by user selection but does not look at the `dependencies` field. We add a strict check that fails before any file is touched, with a self-healing error message per [§14 D1](../specs/2026-04-28-hybrid-full-stack-module-system-design.md#d1-dependency-selection-strict-by-default-opt-in-auto-include).

PowerShell isn't easily unit-testable in this repo (no Pester suite), so verification is by hand-running the script against representative inputs.

**Files:**
- Modify: `scripts/rename.ps1` (insert dependency check after module-selection block, currently lines 379-393)

- [ ] **Step 1: Add the dependency-validation block to `rename.ps1`**

Open `scripts/rename.ps1` and find the module-selection block that ends around line 393 (the `else` branch closing the case-insensitive filter). Immediately after that block, **before** the file-system stripping logic begins (currently around line 395), insert:

```powershell
# --- Strict dependency validation (D1) ---------------------------------------
# Catalog declares a `dependencies` array per module (module ids it requires).
# If the user selects a module without selecting all of its dependencies, fail
# loud with a self-healing error. -AutoIncludeDependencies is intentionally NOT
# implemented in Tier 1; add it only when a real workflow demands it.

$selectedSet = @{}
foreach ($name in $includedOptional) { $selectedSet[$name] = $true }

$missing = @{}
foreach ($name in $includedOptional) {
    $entry = $modulesConfig.$name
    if ($null -eq $entry.dependencies) { continue }
    foreach ($dep in $entry.dependencies) {
        if (-not $selectedSet.ContainsKey($dep)) {
            if (-not $missing.ContainsKey($name)) { $missing[$name] = New-Object System.Collections.ArrayList }
            [void]$missing[$name].Add($dep)
        }
    }
}

if ($missing.Count -gt 0) {
    $allMissing = @{}
    foreach ($mod in $missing.Keys) {
        foreach ($dep in $missing[$mod]) { $allMissing[$dep] = $true }
    }
    $resolvedSelection = @($includedOptional + $allMissing.Keys) | Sort-Object -Unique
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
```

- [ ] **Step 2: Manual verification — failing case**

From the repo root:

```bash
pwsh -NoProfile -File scripts/rename.ps1 -Name "_check" -OutputDir "/tmp/_check_out" -Modules "workflow" -IncludeMobile:$false
```

Expected: the script exits with a non-zero status and prints (the exact format may vary slightly between PowerShell versions, but content must match):

```
ERROR: One or more selected modules are missing required dependencies.

  - 'workflow' requires: commentsActivity, communication

Re-run with the full set:
  -Modules "commentsActivity,communication,workflow"
```

`/tmp/_check_out` should NOT have been created (the check happens before any file-system action).

- [ ] **Step 3: Manual verification — passing case**

```bash
pwsh -NoProfile -File scripts/rename.ps1 -Name "_check2" -OutputDir "/tmp/_check2_out" -Modules "commentsActivity,communication,workflow" -IncludeMobile:$false
```

Expected: the script proceeds normally and generates `/tmp/_check2_out/_check2/` with the source tree. Cleanup after verification:

```bash
rm -rf /tmp/_check_out /tmp/_check2_out
```

- [ ] **Step 4: Manual verification — `-Modules None` still works**

```bash
pwsh -NoProfile -File scripts/rename.ps1 -Name "_check3" -OutputDir "/tmp/_check3_out" -Modules "None" -IncludeMobile:$false
```

Expected: succeeds (no modules selected → no dependencies to check). Cleanup:

```bash
rm -rf /tmp/_check3_out
```

- [ ] **Step 5: Manual verification — `-Modules All` still works**

```bash
pwsh -NoProfile -File scripts/rename.ps1 -Name "_check4" -OutputDir "/tmp/_check4_out" -Modules "All" -IncludeMobile:$false
```

Expected: succeeds (every optional module selected → all dependencies satisfied). Cleanup:

```bash
rm -rf /tmp/_check4_out
```

- [ ] **Step 6: Commit**

```bash
git add scripts/rename.ps1
git commit -m "feat(rename): strict dependency validation with self-healing error

Selecting a module without its declared dependencies now fails loud
before any file-system change, and the error names the corrected
-Modules invocation. -AutoIncludeDependencies is deliberately NOT
implemented (D1: opt-in is deferred until a real workflow demands it).

Refs: spec §14 D1"
```

---

## Self-Review Checklist

After all four tasks are complete, walk this list before marking the plan done:

**Spec coverage** (every Tier 1 scope item must point to a task):

| Tier 1 scope item | Covered by |
|---|---|
| D2: Move catalog to `/modules.catalog.json` | Task 1 (Steps 1–3) |
| Add `Starter.Module.AI` to the catalog | Task 1 (Step 1) |
| Architecture test: Starter.Api cannot import Starter.Module.* | Task 3 (Steps 1–2, 9) |
| Strict dependency validation at backend startup | Task 2 (Steps 3, 6) |
| `IModuleBusContributor` + extract Workflow's bus call | Task 3 (Steps 3–7) |
| D1: rename.ps1 strict dep failure with helpful error | Task 4 (Steps 1–6) |

**No placeholders:** every code block is complete, every `Run:` command is exact, every expected output is specified.

**Type/symbol consistency:** `IModuleBusContributor.ConfigureBus(IBusRegistrationConfigurator bus)` has the same signature in the contract file (Task 3 Step 3), the `WorkflowModule` implementation (Step 4), and both `Program.cs` callsites (Steps 6–7). `orderedModules` is the same local variable name in both `Program.cs` and `Program.Tooling.cs`.

**Out of scope (explicit):** No `IModuleHealthContributor`, `IModuleApiContributor`, `IModuleDependencyMetadata` interfaces. No `coreCompat` field. No web/mobile changes. No package distribution work. No code generation. No `-AutoIncludeDependencies` flag in `rename.ps1`. All deferred per §14.

**Risk to verify post-merge:**

- The `-Modules None` killer test (spec §10) is **not** added in Tier 1 — that's a Tier 2 deliverable. If you want one extra safety net before merging, run `pwsh scripts/rename.ps1 -Name "_killer" -OutputDir "/tmp/killer" -Modules "None" -IncludeMobile:$false && cd /tmp/killer/_killer/boilerplateBE && dotnet build` and clean up afterwards. Document any failure in the PR description rather than fixing in this branch.

---

## Execution Handoff

Plan complete and saved to `docs/superpowers/plans/2026-04-28-module-system-tier-1-host-cleanup.md`. Two execution options:

1. **Subagent-Driven (recommended)** — I dispatch a fresh subagent per task, review between tasks, fast iteration. Best for catching cross-task drift (e.g., a missing `using` introduced in Task 3 that breaks Task 4's smoke test).

2. **Inline Execution** — Execute tasks in this session using `superpowers:executing-plans`, with checkpoints after Tasks 1, 3, and 4 (Task 2 is small enough to merge into the next checkpoint).

Which approach?
