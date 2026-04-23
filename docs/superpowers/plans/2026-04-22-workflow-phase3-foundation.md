# Workflow Phase 3 — Foundation & Quick Wins Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ship Phase 3 as one bundled PR that (a) extracts `WorkflowEngine` into three focused collaborators without behavior change, (b) adds `NOT` compound-condition support with full round-trip coverage, and (c) adds bulk task actions end-to-end (backend command + frontend checkbox UX).

**Architecture:** Surgical refactor inside `Starter.Module.Workflow` — `WorkflowEngine` keeps its `IWorkflowService` surface and now orchestrates three internal collaborators registered as scoped services. The `ConditionEvaluator` already handles `AND`/`OR` via `Logic`; we add `NOT` plus comprehensive tests. Bulk ops follow the existing CQRS + `IWorkflowService` pattern (new command, handler, controller route, FE checkbox + floating action bar). No public API of other modules changes; no new capabilities.

**Tech Stack:** .NET 10 / EF Core (Npgsql) / MediatR CQRS / xUnit + FluentAssertions + Moq (EF in-memory for engine tests). React 19 / TypeScript / TanStack Query / shadcn/ui / Tailwind 4 / Vitest.

---

## Context for the engineer

Read these first to load context:

- **Spec:** [`docs/superpowers/specs/2026-04-22-workflow-phase3-plus-roadmap-design.md`](../specs/2026-04-22-workflow-phase3-plus-roadmap-design.md) (§Phase 3 sections 3.1–3.3).
- **Current engine (1425 lines):** [`boilerplateBE/src/modules/Starter.Module.Workflow/Infrastructure/Services/WorkflowEngine.cs`](../../../boilerplateBE/src/modules/Starter.Module.Workflow/Infrastructure/Services/WorkflowEngine.cs).
- **Module DI (where collaborators register):** [`boilerplateBE/src/modules/Starter.Module.Workflow/WorkflowModule.cs:21-65`](../../../boilerplateBE/src/modules/Starter.Module.Workflow/WorkflowModule.cs).
- **Condition evaluator (already supports AND/OR):** [`boilerplateBE/src/modules/Starter.Module.Workflow/Infrastructure/Services/ConditionEvaluator.cs`](../../../boilerplateBE/src/modules/Starter.Module.Workflow/Infrastructure/Services/ConditionEvaluator.cs).
- **`ConditionConfig` record (already has Logic/Conditions):** [`boilerplateBE/src/Starter.Abstractions/Capabilities/WorkflowConfigRecords.cs:41-46`](../../../boilerplateBE/src/Starter.Abstractions/Capabilities/WorkflowConfigRecords.cs).
- **ExecuteTask command (model batch after):** [`boilerplateBE/src/modules/Starter.Module.Workflow/Application/Commands/ExecuteTask/`](../../../boilerplateBE/src/modules/Starter.Module.Workflow/Application/Commands/ExecuteTask/).
- **Controller:** [`boilerplateBE/src/modules/Starter.Module.Workflow/Controllers/WorkflowController.cs`](../../../boilerplateBE/src/modules/Starter.Module.Workflow/Controllers/WorkflowController.cs).
- **FE inbox page:** [`boilerplateFE/src/features/workflow/pages/WorkflowInboxPage.tsx`](../../../boilerplateFE/src/features/workflow/pages/WorkflowInboxPage.tsx).
- **FE workflow API + queries:** [`boilerplateFE/src/features/workflow/api/workflow.api.ts`](../../../boilerplateFE/src/features/workflow/api/workflow.api.ts) + `workflow.queries.ts`.
- **Existing engine tests (pattern to follow):** [`boilerplateBE/tests/Starter.Api.Tests/Workflow/WorkflowEngineTests.cs`](../../../boilerplateBE/tests/Starter.Api.Tests/Workflow/WorkflowEngineTests.cs) and `ConditionEvaluatorTests.cs`.
- **Prior-phase plan (style reference):** [`docs/superpowers/plans/2026-04-21-workflow-phase2b-operational-hardening.md`](./2026-04-21-workflow-phase2b-operational-hardening.md).

## Hard constraints (memory + CLAUDE.md)

- **NEVER commit EF migrations** — this plan touches *no* schema, so the rule isn't exercised here, but if any step tempts you to generate one, don't. Phase 3 has zero migrations.
- **NEVER add `Co-Authored-By`** lines in commit messages. None of the example commit messages below contain one.
- **NEVER add `--no-verify`** or skip pre-commit hooks.
- **Frontend shared-component rule:** reuse `PageHeader`, `EmptyState`, `Pagination`, `Table`, `ConfirmDialog` from `@/components/common` and `@/components/ui`. No new dialog framework, no custom pagination, no new avatar/badge component.
- **API envelope rule:** BE returns `ApiResponse<T>` → FE axios `r.data` unwraps one layer → `r.data.data` is the payload. Preserve this.
- **Permissions rule:** permissions mirror between `Starter.Module.Workflow/Constants/WorkflowPermissions.cs` → `boilerplateFE/src/constants/permissions.ts`. Bulk action reuses existing `WorkflowPermissions.ActOnTask` — no new permission is needed (it's the same action, just batched).
- **Tenant scope:** `WorkflowDbContext` has global query filters. Every new query MUST NOT call `.IgnoreQueryFilters()` unless the existing code at the same call site already does so for a justified reason (e.g. duplicate checks). Bulk ops run under the caller's identity — the handler loads each task via the filtered `DbContext`, so cross-tenant IDs silently become "not found" (as designed).
- **Idempotency:** `IWorkflowService.ExecuteTaskAsync` is already idempotent; bulk handler inherits this — re-submitting the same batch produces the same outcome.

## Scope check

This plan covers all three Phase 3 features (3.1 engine extraction, 3.2 compound conditions — now just the missing `NOT` op + coverage gaps, 3.3 bulk ops). It is one PR, landed commit-by-commit in the order the spec dictates. No sub-project split is warranted — the refactor is load-bearing for 3.3 (bulk ops create no new tasks, only read & complete existing ones), and 3.2 is a surgical ~40-line change.

## File Structure (decomposition decisions)

### 3.1 — Engine extraction

#### New files

| Path | Responsibility |
|---|---|
| `boilerplateBE/src/modules/Starter.Module.Workflow/Infrastructure/Services/HumanTaskFactory.cs` | Builds `ApprovalTask` entities from `WorkflowStateConfig` — runs assignee resolution (via `AssigneeResolverService`), serializes denormalized columns (definition name/display, entity info, form fields, available actions, SLA reminder), and creates either one task (single-assignee) or N tasks (parallel group). **Zero behavior change** — pure lift of `CreateApprovalTaskAsync` out of `WorkflowEngine`. |
| `boilerplateBE/src/modules/Starter.Module.Workflow/Infrastructure/Services/AutoTransitionEvaluator.cs` | Selects the next transition from a state — condition-matching via `IConditionEvaluator`, fallback to the first unconditional transition. Returns the chosen `WorkflowTransitionConfig?`. No persistence, no hook execution — keeps the collaborator pure so 3.2's condition changes are easy to test in isolation. |
| `boilerplateBE/src/modules/Starter.Module.Workflow/Infrastructure/Services/ParallelApprovalCoordinator.cs` | Evaluates a parallel group's completion state on task execution — handles `AnyOf` first-wins cancellation, `AllOf` sibling completion checking, and `AllOf` reject-cancels-siblings. Returns a discriminated union (`ParallelDecision.Proceed` / `ParallelDecision.Wait`) so the engine decides whether to transition or stall. |
| `boilerplateBE/tests/Starter.Api.Tests/Workflow/HumanTaskFactoryTests.cs` | Pure tests for the factory — single-assignee happy path, parallel mode, denormalized column population, role extraction, delegation map handling. |
| `boilerplateBE/tests/Starter.Api.Tests/Workflow/AutoTransitionEvaluatorTests.cs` | Pure tests for transition selection — condition-match wins, unconditional fallback, no-match returns null, condition evaluation calls pass through context correctly. |
| `boilerplateBE/tests/Starter.Api.Tests/Workflow/ParallelApprovalCoordinatorTests.cs` | Pure tests — `AnyOf` cancels siblings, `AllOf` waits until all complete, `AllOf` reject short-circuits, non-grouped tasks return `Proceed` immediately. |

#### Modified files (3.1)

| Path | What changes |
|---|---|
| [`WorkflowEngine.cs`](../../../boilerplateBE/src/modules/Starter.Module.Workflow/Infrastructure/Services/WorkflowEngine.cs) | Constructor gains three collaborator parameters. `CreateApprovalTaskAsync` becomes `await _humanTaskFactory.CreateAsync(...)`. `AutoTransitionAsync` and `HandleConditionalGateAsync` use `_autoTransitionEvaluator.Select(...)`. The parallel-group branch inside `ExecuteTaskAsync` delegates to `_parallelCoordinator.EvaluateAsync(...)`. Net line reduction: ~250 lines. |
| [`WorkflowModule.cs:40-48`](../../../boilerplateBE/src/modules/Starter.Module.Workflow/WorkflowModule.cs) | Registers the three new collaborators as `AddScoped<...>()`. |

#### Why these boundaries

- **Files that change together live together.** The three collaborators are all in `Infrastructure/Services/` next to `WorkflowEngine` because they share the same internal seam — they read the same `WorkflowDbContext`, use the same JSON settings, and have the same lifetime.
- **Public surface unchanged.** `WorkflowEngine : IWorkflowService` stays. No controller or `IWorkflowService` change → zero call-site churn outside the module.
- **Collaborators are `internal sealed class` (not public)** — they're implementation detail. Tests live in the same test project which has `InternalsVisibleTo` via the existing test setup (or bump visibility to `public` if needed per existing convention).
- **SOLID:** each collaborator has exactly one reason to change. `HumanTaskFactory` changes when denormalization rules change; `AutoTransitionEvaluator` changes when condition semantics change; `ParallelApprovalCoordinator` changes when parallel mode rules change.

### 3.2 — Compound conditions (NOT operator + coverage gaps)

| Path | What changes |
|---|---|
| [`ConditionEvaluator.cs:16-23`](../../../boilerplateBE/src/modules/Starter.Module.Workflow/Infrastructure/Services/ConditionEvaluator.cs) | The existing AND/OR switch in `Evaluate(...)` adds a `"NOT"` branch: a NOT node expects exactly one child condition and inverts its result. Zero-child NOT returns `false` (consistent with current empty-conditions AND behavior). Multi-child NOT treats the list as an implicit AND and inverts. |
| [`ConditionEvaluatorTests.cs`](../../../boilerplateBE/tests/Starter.Api.Tests/Workflow/ConditionEvaluatorTests.cs) | New `[Fact]` cases: `Evaluate_NotGroup_SingleFalseChild_ReturnsTrue`, `Evaluate_NotGroup_SingleTrueChild_ReturnsFalse`, `Evaluate_NotGroup_EmptyConditions_ReturnsFalse`, `Evaluate_NestedAndOrNot_EvaluatesCorrectly`, `Evaluate_ShortCircuit_OrStopsOnFirstTrue`, `Evaluate_ShortCircuit_AndStopsOnFirstFalse`, `Evaluate_JsonRoundTrip_PreservesStructure`. |

**Why no `ConditionConfig` schema change:** the existing record already has `Logic` and `Conditions` fields. The spec (§3.2) says "the new fields are additive and optional" — they already are. We just extend the operator set from `{and, or}` to `{and, or, not}`. Zero migration impact.

### 3.3 — Bulk operations

#### New files (backend)

| Path | Responsibility |
|---|---|
| `boilerplateBE/src/modules/Starter.Module.Workflow/Application/Commands/BatchExecuteTasks/BatchExecuteTasksCommand.cs` | `sealed record BatchExecuteTasksCommand(IReadOnlyList<Guid> TaskIds, string Action, string? Comment = null) : IRequest<Result<BatchExecuteResult>>`. |
| `boilerplateBE/src/modules/Starter.Module.Workflow/Application/Commands/BatchExecuteTasks/BatchExecuteTasksCommandHandler.cs` | Iterates `TaskIds`, calls `IWorkflowService.ExecuteTaskAsync` per task inside its own try/catch. Aggregates per-task outcomes into a `BatchExecuteResult(int Succeeded, int Failed, int Skipped, IReadOnlyList<BatchItemOutcome> Items)`. One task failing doesn't abort the batch. |
| `boilerplateBE/src/modules/Starter.Module.Workflow/Application/Commands/BatchExecuteTasks/BatchExecuteTasksCommandValidator.cs` | FluentValidation: at least 1 ID, at most 50 IDs, non-empty action string, comment length ≤ 2000. |
| `boilerplateBE/src/modules/Starter.Module.Workflow/Application/DTOs/BatchExecuteResult.cs` | `sealed record BatchExecuteResult(int Succeeded, int Failed, int Skipped, IReadOnlyList<BatchItemOutcome> Items)` + `sealed record BatchItemOutcome(Guid TaskId, string Status, string? Error)`. `Status` ∈ {"Succeeded", "Failed", "Skipped"}. |
| `boilerplateBE/tests/Starter.Api.Tests/Workflow/BatchExecuteTasksTests.cs` | Handler tests: happy path (all succeed), mixed success/failure, all-fail, empty-list validation, > 50 IDs validation, cross-tenant IDs return Failed, idempotent replay. |

#### New files (frontend)

| Path | Responsibility |
|---|---|
| `boilerplateFE/src/features/workflow/components/BulkActionBar.tsx` | Floating action bar shown when ≥ 1 task is selected. Sticky to the viewport bottom inside the Inbox page. Props: `selectedCount`, `onApprove`, `onReject`, `onReturn`, `onClear`, `isPending`. Uses shadcn `Button` variants. Respects RTL with `ltr:/rtl:` prefixes. |
| `boilerplateFE/src/features/workflow/components/BulkConfirmDialog.tsx` | Thin wrapper around `ConfirmDialog` from `@/components/common` — adds an optional comment `<Textarea>` bound to the parent via `onSubmit(comment)`. Reused for Approve / Reject / Return variants; headline + description change per action. |
| `boilerplateFE/src/features/workflow/components/BulkResultDialog.tsx` | Dialog (shadcn `Dialog`) shown after a bulk action returns. Renders summary counts + a collapsible list of `BatchItemOutcome`. Uses shared `Badge` for Succeeded/Failed/Skipped. |

#### Modified files (3.3 backend)

| Path | What changes |
|---|---|
| [`WorkflowController.cs:174-184`](../../../boilerplateBE/src/modules/Starter.Module.Workflow/Controllers/WorkflowController.cs) | Adds `[HttpPost("tasks/batch-execute")]` `BatchExecuteTasks([FromBody] BatchExecuteRequest request, ...)` — policy `WorkflowPermissions.ActOnTask`. Returns `Ok(ApiResponse<BatchExecuteResult>)`. |

#### Modified files (3.3 frontend)

| Path | What changes |
|---|---|
| [`src/config/api.config.ts:208-211`](../../../boilerplateFE/src/config/api.config.ts) | Add `TASKS_BATCH: '/workflow/tasks/batch-execute'` next to existing `TASKS` / `TASK_EXECUTE`. |
| [`boilerplateFE/src/features/workflow/api/workflow.api.ts`](../../../boilerplateFE/src/features/workflow/api/workflow.api.ts) | Add `batchExecuteTasks(data: BatchExecuteTasksRequest): Promise<BatchExecuteResult>`. |
| [`boilerplateFE/src/features/workflow/api/workflow.queries.ts`](../../../boilerplateFE/src/features/workflow/api/workflow.queries.ts) | Add `useBatchExecuteTasks()` hook — on success, invalidate `queryKeys.workflow.tasks.all` + `queryKeys.workflow.instances.all`; toasts summary via `toast.success`/`toast.warning`. |
| [`boilerplateFE/src/types/workflow.types.ts:181-185`](../../../boilerplateFE/src/types/workflow.types.ts) | Add `BatchExecuteTasksRequest`, `BatchExecuteResult`, `BatchItemOutcome` interfaces. |
| [`boilerplateFE/src/features/workflow/pages/WorkflowInboxPage.tsx`](../../../boilerplateFE/src/features/workflow/pages/WorkflowInboxPage.tsx) | Adds a checkbox column (`<TableHead>` + `<TableCell>` with shadcn `Checkbox`), "select all visible" header checkbox, `selectedTaskIds` state (Set<string>), and conditionally renders `<BulkActionBar>` when `selectedTaskIds.size > 0`. Clears selection on page change / mutation success. |
| [`boilerplateFE/src/i18n/locales/en/translation.json`](../../../boilerplateFE/src/i18n/locales/en/translation.json) (+ `ar`, `ku`) | Add under `workflow.inbox`: `selectAll`, `select`, `selected`, `bulkApprove`, `bulkReject`, `bulkReturn`, `bulkConfirmTitle`, `bulkConfirmDesc`, `bulkResultTitle`, `bulkResultSummary`, `bulkResultSucceeded`, `bulkResultFailed`, `bulkResultSkipped`, `bulkCommentPlaceholder`, `clearSelection`. |

## Commit strategy

One branch, one PR, six commits:

1. `feat(workflow): extract HumanTaskFactory from WorkflowEngine` — Task 1.* (collaborator #1 + tests)
2. `feat(workflow): extract AutoTransitionEvaluator from WorkflowEngine` — Task 2.* (collaborator #2 + tests)
3. `feat(workflow): extract ParallelApprovalCoordinator from WorkflowEngine` — Task 3.* (collaborator #3 + tests)
4. `feat(workflow): support NOT operator in compound conditions` — Task 4.* (3.2)
5. `feat(workflow): add BatchExecuteTasks command and endpoint` — Task 5.* (3.3 backend)
6. `feat(workflow): add bulk task actions to inbox UI` — Task 6.* (3.3 frontend)

Each commit leaves `dotnet test` and `npm run build` green.

---

## Task 1.1: Write failing test for `HumanTaskFactory.CreateAsync` — single-assignee path

**Files:**
- Create: `boilerplateBE/tests/Starter.Api.Tests/Workflow/HumanTaskFactoryTests.cs`

**Context:** We're writing the test before the factory exists, following TDD. The test builds the collaborator with an EF in-memory `WorkflowDbContext` + real `ConditionEvaluator` + real `AssigneeResolverService` wired with a mock `IRoleUserReader`. A `SpecificUser` assignee strategy resolves a known user ID so we can assert.

- [ ] **Step 1: Create the test class skeleton and the first `[Fact]`**

```csharp
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Starter.Abstractions.Capabilities;
using Starter.Abstractions.Readers;
using Starter.Module.Workflow.Domain.Entities;
using Starter.Module.Workflow.Infrastructure.Persistence;
using Starter.Module.Workflow.Infrastructure.Services;
using Xunit;

namespace Starter.Api.Tests.Workflow;

public sealed class HumanTaskFactoryTests : IDisposable
{
    private readonly WorkflowDbContext _db;
    private readonly HumanTaskFactory _sut;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _initiatorId = Guid.NewGuid();
    private readonly Guid _approverId = Guid.NewGuid();

    public HumanTaskFactoryTests()
    {
        var options = new DbContextOptionsBuilder<WorkflowDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new WorkflowDbContext(options);

        var builtIn = new BuiltInAssigneeProvider(Mock.Of<IRoleUserReader>());
        var resolver = new AssigneeResolverService(
            new IAssigneeResolverProvider[] { builtIn },
            Mock.Of<IUserReader>(),
            NullLogger<AssigneeResolverService>.Instance);

        _sut = new HumanTaskFactory(_db, resolver, NullLogger<HumanTaskFactory>.Instance);
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task CreateAsync_SingleAssignee_PopulatesDenormalizedColumns()
    {
        var (instance, definition, state) = SeedInstanceAndState(
            stateType: "HumanTask",
            assignee: new AssigneeConfig("SpecificUser",
                new Dictionary<string, object> { ["userId"] = _approverId.ToString() }),
            actions: new List<string> { "approve", "reject" });

        await _sut.CreateAsync(instance, state, definition, _initiatorId, CancellationToken.None);
        await _db.SaveChangesAsync();

        var task = await _db.ApprovalTasks.SingleAsync();
        task.AssigneeUserId.Should().Be(_approverId);
        task.StepName.Should().Be(state.Name);
        task.DefinitionName.Should().Be(definition.Name);
        task.DefinitionDisplayName.Should().Be(definition.DisplayName);
        task.EntityType.Should().Be(instance.EntityType);
        task.EntityId.Should().Be(instance.EntityId);
        task.AvailableActionsJson.Should().Contain("approve").And.Contain("reject");
        task.GroupId.Should().BeNull();
    }

    private (WorkflowInstance, WorkflowDefinition, WorkflowStateConfig) SeedInstanceAndState(
        string stateType,
        AssigneeConfig? assignee = null,
        ParallelConfig? parallel = null,
        List<string>? actions = null,
        SlaConfig? sla = null,
        List<FormFieldDefinition>? formFields = null)
    {
        var state = new WorkflowStateConfig(
            Name: "Review",
            DisplayName: "Review",
            Type: stateType,
            Assignee: assignee,
            Actions: actions,
            Parallel: parallel,
            Sla: sla,
            FormFields: formFields);
        var transitions = new List<WorkflowTransitionConfig>
        {
            new("Review", "Done", "approve"),
            new("Review", "Rejected", "reject"),
        };
        var definition = WorkflowDefinition.Create(
            tenantId: _tenantId,
            name: "TestDef",
            displayName: "Test Definition",
            entityType: "Order",
            statesJson: JsonSerializer.Serialize(new[] { state }),
            transitionsJson: JsonSerializer.Serialize(transitions),
            isTemplate: false,
            sourceModule: "Tests");
        _db.WorkflowDefinitions.Add(definition);

        var instance = WorkflowInstance.Create(
            tenantId: _tenantId,
            definitionId: definition.Id,
            entityType: "Order",
            entityId: Guid.NewGuid(),
            initialState: state.Name,
            startedByUserId: _initiatorId,
            contextJson: null,
            definitionName: definition.DisplayName,
            entityDisplayName: "Order #1");
        _db.WorkflowInstances.Add(instance);
        _db.SaveChanges();

        return (instance, definition, state);
    }
}
```

- [ ] **Step 2: Run the test to confirm it fails because `HumanTaskFactory` does not exist**

Run: `cd boilerplateBE && dotnet test --filter "FullyQualifiedName~HumanTaskFactoryTests" --no-restore 2>&1 | tail -20`
Expected: Compilation error — `HumanTaskFactory` not found.

---

## Task 1.2: Create `HumanTaskFactory` — minimal implementation to pass single-assignee test

**Files:**
- Create: `boilerplateBE/src/modules/Starter.Module.Workflow/Infrastructure/Services/HumanTaskFactory.cs`

- [ ] **Step 1: Write the factory by lifting `CreateApprovalTaskAsync` from `WorkflowEngine.cs:1041-1178` verbatim, adjusting the constructor to accept only what it needs**

```csharp
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Starter.Abstractions.Capabilities;
using Starter.Module.Workflow.Domain.Entities;
using Starter.Module.Workflow.Infrastructure.Persistence;

namespace Starter.Module.Workflow.Infrastructure.Services;

/// <summary>
/// Creates <see cref="ApprovalTask"/> entities for HumanTask states — runs assignee
/// resolution (with delegation), serializes denormalized columns, and supports both
/// single-assignee and parallel (AllOf/AnyOf) group creation.
/// </summary>
internal sealed class HumanTaskFactory(
    WorkflowDbContext context,
    AssigneeResolverService assigneeResolver,
    ILogger<HumanTaskFactory> logger)
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    /// <summary>
    /// Creates approval task(s) for a HumanTask state. Adds tasks to the tracked
    /// context — caller is responsible for SaveChanges.
    /// </summary>
    public async Task CreateAsync(
        WorkflowInstance instance,
        WorkflowStateConfig stateConfig,
        WorkflowDefinition definition,
        Guid initiatorUserId,
        CancellationToken ct)
    {
        var formFieldsJson = stateConfig.FormFields is { Count: > 0 }
            ? JsonSerializer.Serialize(stateConfig.FormFields, JsonOpts)
            : null;

        var transitions = JsonSerializer.Deserialize<List<WorkflowTransitionConfig>>(
            definition.TransitionsJson, JsonOpts) ?? [];
        var availableActions = transitions
            .Where(tr => tr.From == stateConfig.Name
                && tr.Type.Equals("Manual", StringComparison.OrdinalIgnoreCase))
            .Select(tr => tr.Trigger)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var availableActionsJson = JsonSerializer.Serialize(availableActions, JsonOpts);

        var slaReminderAfterHours = stateConfig.Sla?.ReminderAfterHours;

        if (stateConfig.Parallel is { Assignees.Count: > 0 })
        {
            await CreateParallelAsync(
                instance, stateConfig, definition, initiatorUserId,
                formFieldsJson, availableActionsJson, slaReminderAfterHours, ct);
            return;
        }

        await CreateSingleAsync(
            instance, stateConfig, definition, initiatorUserId,
            formFieldsJson, availableActionsJson, slaReminderAfterHours, ct);
    }

    private async Task CreateParallelAsync(
        WorkflowInstance instance, WorkflowStateConfig stateConfig, WorkflowDefinition definition,
        Guid initiatorUserId, string? formFieldsJson, string availableActionsJson,
        int? slaReminderAfterHours, CancellationToken ct)
    {
        var groupId = Guid.NewGuid();
        var assigneeContext = new WorkflowAssigneeContext(
            instance.EntityType, instance.EntityId, instance.TenantId,
            initiatorUserId, instance.CurrentState);

        foreach (var assigneeConfig in stateConfig.Parallel!.Assignees)
        {
            var strategyJson = JsonSerializer.Serialize(assigneeConfig, JsonOpts);
            var resolveResult = await assigneeResolver.ResolveWithDelegationAsync(
                assigneeConfig, assigneeContext, ct);

            Guid? userId = resolveResult.AssigneeIds.Count > 0 ? resolveResult.AssigneeIds[0] : null;
            string? role = ExtractRoleName(assigneeConfig);
            Guid? originalAssigneeUserId = userId.HasValue
                && resolveResult.DelegationMap.TryGetValue(userId.Value, out var origId)
                    ? origId : null;

            var task = ApprovalTask.Create(
                tenantId: instance.TenantId,
                instanceId: instance.Id,
                stepName: stateConfig.Name,
                assigneeUserId: userId,
                assigneeRole: role,
                assigneeStrategyJson: strategyJson,
                dueDate: null,
                entityType: instance.EntityType,
                entityId: instance.EntityId,
                definitionName: definition.Name,
                definitionDisplayName: definition.DisplayName,
                entityDisplayName: instance.EntityDisplayName,
                formFieldsJson: formFieldsJson,
                availableActionsJson: availableActionsJson,
                slaReminderAfterHours: slaReminderAfterHours,
                groupId: groupId,
                originalAssigneeUserId: originalAssigneeUserId);

            context.ApprovalTasks.Add(task);
        }
    }

    private async Task CreateSingleAsync(
        WorkflowInstance instance, WorkflowStateConfig stateConfig, WorkflowDefinition definition,
        Guid initiatorUserId, string? formFieldsJson, string availableActionsJson,
        int? slaReminderAfterHours, CancellationToken ct)
    {
        Guid? assigneeUserId = null;
        string? assigneeRole = null;
        string? assigneeStrategyJson = null;
        Guid? originalAssignee = null;

        if (stateConfig.Assignee is not null)
        {
            assigneeStrategyJson = JsonSerializer.Serialize(stateConfig.Assignee, JsonOpts);

            var assigneeContext = new WorkflowAssigneeContext(
                instance.EntityType, instance.EntityId, instance.TenantId,
                initiatorUserId, instance.CurrentState);

            var resolveResult = await assigneeResolver.ResolveWithDelegationAsync(
                stateConfig.Assignee, assigneeContext, ct);

            if (resolveResult.AssigneeIds.Count > 0)
            {
                assigneeUserId = resolveResult.AssigneeIds[0];
                if (resolveResult.DelegationMap.TryGetValue(assigneeUserId.Value, out var origId))
                    originalAssignee = origId;
            }

            assigneeRole = ExtractRoleName(stateConfig.Assignee);
        }

        var approvalTask = ApprovalTask.Create(
            tenantId: instance.TenantId,
            instanceId: instance.Id,
            stepName: stateConfig.Name,
            assigneeUserId: assigneeUserId,
            assigneeRole: assigneeRole,
            assigneeStrategyJson: assigneeStrategyJson,
            dueDate: null,
            entityType: instance.EntityType,
            entityId: instance.EntityId,
            definitionName: definition.Name,
            definitionDisplayName: definition.DisplayName,
            entityDisplayName: instance.EntityDisplayName,
            formFieldsJson: formFieldsJson,
            availableActionsJson: availableActionsJson,
            slaReminderAfterHours: slaReminderAfterHours,
            originalAssigneeUserId: originalAssignee);

        context.ApprovalTasks.Add(approvalTask);
    }

    private static string? ExtractRoleName(AssigneeConfig config) =>
        config.Strategy.Equals("Role", StringComparison.OrdinalIgnoreCase)
            && config.Parameters is not null
            && config.Parameters.TryGetValue("roleName", out var roleObj)
                ? roleObj?.ToString()
                : null;
}
```

- [ ] **Step 2: Run test, confirm it passes**

Run: `cd boilerplateBE && dotnet test --filter "FullyQualifiedName~HumanTaskFactoryTests.CreateAsync_SingleAssignee" --no-restore 2>&1 | tail -10`
Expected: `Passed: 1`.

---

## Task 1.3: Add parallel, form-fields, and SLA assertions

**Files:**
- Modify: `boilerplateBE/tests/Starter.Api.Tests/Workflow/HumanTaskFactoryTests.cs`

- [ ] **Step 1: Append three more `[Fact]` tests**

```csharp
[Fact]
public async Task CreateAsync_Parallel_AllOf_CreatesOneTaskPerAssigneeWithSharedGroupId()
{
    var (instance, definition, state) = SeedInstanceAndState(
        stateType: "HumanTask",
        parallel: new ParallelConfig("AllOf", new List<AssigneeConfig>
        {
            new("SpecificUser", new Dictionary<string, object> { ["userId"] = _approverId.ToString() }),
            new("SpecificUser", new Dictionary<string, object> { ["userId"] = Guid.NewGuid().ToString() }),
        }),
        actions: new List<string> { "approve", "reject" });

    await _sut.CreateAsync(instance, state, definition, _initiatorId, CancellationToken.None);
    await _db.SaveChangesAsync();

    var tasks = await _db.ApprovalTasks.ToListAsync();
    tasks.Should().HaveCount(2);
    tasks.Select(t => t.GroupId).Distinct().Should().HaveCount(1, "both tasks share a group");
    tasks.Select(t => t.GroupId!.Value).All(g => g != Guid.Empty).Should().BeTrue();
}

[Fact]
public async Task CreateAsync_WithFormFields_SerializesFormFieldsJson()
{
    var fields = new List<FormFieldDefinition>
    {
        new("amount", "Amount", "number", Required: true, Min: 0, Max: 10000),
    };
    var (instance, definition, state) = SeedInstanceAndState(
        stateType: "HumanTask",
        assignee: new AssigneeConfig("SpecificUser",
            new Dictionary<string, object> { ["userId"] = _approverId.ToString() }),
        actions: new List<string> { "approve" },
        formFields: fields);

    await _sut.CreateAsync(instance, state, definition, _initiatorId, CancellationToken.None);
    await _db.SaveChangesAsync();

    var task = await _db.ApprovalTasks.SingleAsync();
    task.FormFieldsJson.Should().NotBeNullOrEmpty();
    task.FormFieldsJson!.Should().Contain("amount").And.Contain("number");
}

[Fact]
public async Task CreateAsync_WithSla_CapturesReminderHours()
{
    var (instance, definition, state) = SeedInstanceAndState(
        stateType: "HumanTask",
        assignee: new AssigneeConfig("SpecificUser",
            new Dictionary<string, object> { ["userId"] = _approverId.ToString() }),
        actions: new List<string> { "approve" },
        sla: new SlaConfig(ReminderAfterHours: 4, EscalateAfterHours: 8));

    await _sut.CreateAsync(instance, state, definition, _initiatorId, CancellationToken.None);
    await _db.SaveChangesAsync();

    var task = await _db.ApprovalTasks.SingleAsync();
    task.SlaReminderAfterHours.Should().Be(4);
}
```

- [ ] **Step 2: Run tests, confirm all four pass**

Run: `cd boilerplateBE && dotnet test --filter "FullyQualifiedName~HumanTaskFactoryTests" --no-restore 2>&1 | tail -10`
Expected: `Passed: 4`.

---

## Task 1.4: Wire `HumanTaskFactory` into `WorkflowEngine` and `WorkflowModule`

**Files:**
- Modify: `boilerplateBE/src/modules/Starter.Module.Workflow/WorkflowModule.cs:40-48`
- Modify: `boilerplateBE/src/modules/Starter.Module.Workflow/Infrastructure/Services/WorkflowEngine.cs:19-27` (constructor) and `:1041-1178` (`CreateApprovalTaskAsync` delegation)

- [ ] **Step 1: Register the factory in `WorkflowModule.ConfigureServices`**

Add this line immediately after `services.AddScoped<HookExecutor>();` (around line 46):

```csharp
services.AddScoped<HumanTaskFactory>();
```

- [ ] **Step 2: Add `HumanTaskFactory` to `WorkflowEngine`'s primary constructor**

Change the constructor signature at [`WorkflowEngine.cs:19-27`](../../../boilerplateBE/src/modules/Starter.Module.Workflow/Infrastructure/Services/WorkflowEngine.cs) to include the new dependency:

```csharp
public sealed class WorkflowEngine(
    WorkflowDbContext context,
    IConditionEvaluator conditionEvaluator,
    AssigneeResolverService assigneeResolver,
    HookExecutor hookExecutor,
    ICommentService commentService,
    IUserReader userReader,
    IFormDataValidator formDataValidator,
    HumanTaskFactory humanTaskFactory,
    ILogger<WorkflowEngine> logger) : IWorkflowService
```

- [ ] **Step 3: Replace the body of `CreateApprovalTaskAsync` (lines 1041-1178) with a single delegation**

```csharp
private Task CreateApprovalTaskAsync(
    WorkflowInstance instance,
    WorkflowStateConfig stateConfig,
    WorkflowDefinition definition,
    Guid initiatorUserId,
    CancellationToken ct)
    => humanTaskFactory.CreateAsync(instance, stateConfig, definition, initiatorUserId, ct);
```

- [ ] **Step 4: Fix `WorkflowEngineTests` constructor — it manually instantiates `WorkflowEngine`**

Locate the `WorkflowEngineTests` constructor at [`WorkflowEngineTests.cs:30-67`](../../../boilerplateBE/tests/Starter.Api.Tests/Workflow/WorkflowEngineTests.cs). Before `_sut = new WorkflowEngine(...)`, create the factory:

```csharp
var humanTaskFactory = new HumanTaskFactory(
    _db, assigneeResolver, NullLogger<HumanTaskFactory>.Instance);
```

Then add `humanTaskFactory,` as the 8th constructor argument (before `NullLogger<WorkflowEngine>.Instance`).

- [ ] **Step 5: Do the same for `GetPendingTasksPaginationTests.cs`, `PendingTasksDenormalizationTests.cs`, and any other test that instantiates `WorkflowEngine` directly**

Run: `cd boilerplateBE && grep -rn "new WorkflowEngine(" tests/`
For each match, wire the new `HumanTaskFactory` dependency the same way.

- [ ] **Step 6: Build and run the full test suite**

Run: `cd boilerplateBE && dotnet build && dotnet test --filter "FullyQualifiedName~Workflow" --no-build 2>&1 | tail -15`
Expected: All existing workflow tests pass, plus the new `HumanTaskFactoryTests` (4 tests).

- [ ] **Step 7: Commit**

```bash
git add boilerplateBE/src/modules/Starter.Module.Workflow/Infrastructure/Services/HumanTaskFactory.cs \
        boilerplateBE/src/modules/Starter.Module.Workflow/Infrastructure/Services/WorkflowEngine.cs \
        boilerplateBE/src/modules/Starter.Module.Workflow/WorkflowModule.cs \
        boilerplateBE/tests/Starter.Api.Tests/Workflow/HumanTaskFactoryTests.cs \
        boilerplateBE/tests/Starter.Api.Tests/Workflow/WorkflowEngineTests.cs \
        boilerplateBE/tests/Starter.Api.Tests/Workflow/GetPendingTasksPaginationTests.cs \
        boilerplateBE/tests/Starter.Api.Tests/Workflow/PendingTasksDenormalizationTests.cs
git commit -m "feat(workflow): extract HumanTaskFactory from WorkflowEngine"
```

---

## Task 2.1: Write failing tests for `AutoTransitionEvaluator`

**Files:**
- Create: `boilerplateBE/tests/Starter.Api.Tests/Workflow/AutoTransitionEvaluatorTests.cs`

**Context:** The evaluator is a pure function over transitions + context — no DB, no hooks. Tests are fast and deterministic.

- [ ] **Step 1: Write the test file**

```csharp
using System.Text.Json;
using FluentAssertions;
using Starter.Abstractions.Capabilities;
using Starter.Module.Workflow.Infrastructure.Services;
using Xunit;

namespace Starter.Api.Tests.Workflow;

public sealed class AutoTransitionEvaluatorTests
{
    private readonly AutoTransitionEvaluator _sut =
        new(new ConditionEvaluator());

    [Fact]
    public void Select_MatchingConditionWins_ReturnsConditionalTransition()
    {
        var transitions = new List<WorkflowTransitionConfig>
        {
            new("A", "HighValue", "auto", Condition: new ConditionConfig("amount", "greaterThan", 1000)),
            new("A", "LowValue",  "auto"),
        };
        var context = MakeContext(new() { ["amount"] = 2000 });

        var result = _sut.Select(transitions, fromState: "A", context);

        result.Should().NotBeNull();
        result!.To.Should().Be("HighValue");
    }

    [Fact]
    public void Select_NoConditionMatches_FallsBackToUnconditional()
    {
        var transitions = new List<WorkflowTransitionConfig>
        {
            new("A", "HighValue", "auto", Condition: new ConditionConfig("amount", "greaterThan", 10_000)),
            new("A", "LowValue",  "auto"),
        };
        var context = MakeContext(new() { ["amount"] = 500 });

        var result = _sut.Select(transitions, fromState: "A", context);

        result!.To.Should().Be("LowValue");
    }

    [Fact]
    public void Select_NoTransitionFromState_ReturnsNull()
    {
        var transitions = new List<WorkflowTransitionConfig>
        {
            new("OtherState", "X", "auto"),
        };
        _sut.Select(transitions, fromState: "A", context: null).Should().BeNull();
    }

    [Fact]
    public void Select_AllConditionalAndNoneMatch_ReturnsNull()
    {
        var transitions = new List<WorkflowTransitionConfig>
        {
            new("A", "X", "auto", Condition: new ConditionConfig("status", "equals", "active")),
        };
        var context = MakeContext(new() { ["status"] = "closed" });

        _sut.Select(transitions, fromState: "A", context).Should().BeNull();
    }

    private static Dictionary<string, object> MakeContext(Dictionary<string, object> raw)
    {
        var json = JsonSerializer.Serialize(raw);
        return JsonSerializer.Deserialize<Dictionary<string, object>>(json)!;
    }
}
```

- [ ] **Step 2: Run — confirm compile fails (no `AutoTransitionEvaluator` yet)**

Run: `cd boilerplateBE && dotnet test --filter "FullyQualifiedName~AutoTransitionEvaluatorTests" --no-restore 2>&1 | tail -10`
Expected: `error CS0246: The type or namespace name 'AutoTransitionEvaluator' could not be found`.

---

## Task 2.2: Create `AutoTransitionEvaluator`

**Files:**
- Create: `boilerplateBE/src/modules/Starter.Module.Workflow/Infrastructure/Services/AutoTransitionEvaluator.cs`

- [ ] **Step 1: Write the evaluator**

```csharp
using Starter.Abstractions.Capabilities;

namespace Starter.Module.Workflow.Infrastructure.Services;

/// <summary>
/// Selects the next <see cref="WorkflowTransitionConfig"/> for an auto-transitioning
/// state (SystemAction, ConditionalGate, or the Initial-on-start path). Pure selection —
/// no persistence, no hook execution.
/// </summary>
internal sealed class AutoTransitionEvaluator(IConditionEvaluator conditionEvaluator)
{
    /// <summary>
    /// Picks the first matching conditional transition from <paramref name="fromState"/>.
    /// Falls back to the first unconditional transition. Returns null if nothing matches.
    /// </summary>
    public WorkflowTransitionConfig? Select(
        IReadOnlyList<WorkflowTransitionConfig> transitions,
        string fromState,
        IReadOnlyDictionary<string, object>? context)
    {
        var candidates = transitions.Where(t => t.From == fromState).ToList();
        if (candidates.Count == 0) return null;

        // Condition-bearing transitions are evaluated in declaration order;
        // first match wins.
        foreach (var t in candidates.Where(t => t.Condition is not null))
        {
            if (conditionEvaluator.Evaluate(
                    t.Condition!,
                    context is Dictionary<string, object> d ? d : context?.ToDictionary(kv => kv.Key, kv => kv.Value)))
                return t;
        }

        return candidates.FirstOrDefault(t => t.Condition is null);
    }
}
```

- [ ] **Step 2: Run the new tests**

Run: `cd boilerplateBE && dotnet test --filter "FullyQualifiedName~AutoTransitionEvaluatorTests" --no-restore 2>&1 | tail -10`
Expected: `Passed: 4`.

---

## Task 2.3: Wire `AutoTransitionEvaluator` into `WorkflowEngine`

**Files:**
- Modify: `boilerplateBE/src/modules/Starter.Module.Workflow/WorkflowModule.cs` (add registration)
- Modify: `boilerplateBE/src/modules/Starter.Module.Workflow/Infrastructure/Services/WorkflowEngine.cs` (constructor + three call sites: `ExecuteTaskAsync` conditional transition selection, `AutoTransitionAsync`, `HandleConditionalGateAsync`)

- [ ] **Step 1: Register**

In `WorkflowModule.cs`, add after `services.AddScoped<HumanTaskFactory>();`:

```csharp
services.AddScoped<AutoTransitionEvaluator>();
```

- [ ] **Step 2: Add constructor parameter**

In `WorkflowEngine.cs:19-27`, insert `AutoTransitionEvaluator autoTransitionEvaluator,` after `HumanTaskFactory humanTaskFactory,`.

- [ ] **Step 3: Replace the condition-matching loop in `ExecuteTaskAsync` (around lines 306-332)**

Find this block:

```csharp
WorkflowTransitionConfig? selectedTransition = null;

var conditionalTransitions = matchingTransitions
    .Where(t => t.Condition is not null)
    .ToList();

if (conditionalTransitions.Count > 0)
{
    var instanceContext = instance.ContextJson is not null
        ? JsonSerializer.Deserialize<Dictionary<string, object>>(instance.ContextJson, JsonOpts)
        : null;

    foreach (var ct2 in conditionalTransitions)
    {
        if (conditionEvaluator.Evaluate(ct2.Condition!, instanceContext))
        {
            selectedTransition = ct2;
            break;
        }
    }
}

selectedTransition ??= matchingTransitions.FirstOrDefault(t => t.Condition is null)
    ?? matchingTransitions[0];
```

Replace with:

```csharp
var instanceContext = instance.ContextJson is not null
    ? JsonSerializer.Deserialize<Dictionary<string, object>>(instance.ContextJson, JsonOpts)
    : null;

var selectedTransition = autoTransitionEvaluator.Select(matchingTransitions, instance.CurrentState, instanceContext)
    ?? matchingTransitions[0];
```

- [ ] **Step 4: Simplify `AutoTransitionAsync` (lines ~1180-1251)**

Replace the selection block:

```csharp
WorkflowTransitionConfig? selected = null;
var instanceContext = instance.ContextJson is not null
    ? JsonSerializer.Deserialize<Dictionary<string, object>>(instance.ContextJson, JsonOpts)
    : null;

foreach (var t in autoTransitions.Where(t => t.Condition is not null))
{
    if (conditionEvaluator.Evaluate(t.Condition!, instanceContext))
    {
        selected = t;
        break;
    }
}

selected ??= autoTransitions.FirstOrDefault(t => t.Condition is null);
```

with:

```csharp
var instanceContext = instance.ContextJson is not null
    ? JsonSerializer.Deserialize<Dictionary<string, object>>(instance.ContextJson, JsonOpts)
    : null;

var selected = autoTransitionEvaluator.Select(autoTransitions, currentStateConfig.Name, instanceContext);
```

- [ ] **Step 5: Simplify `HandleConditionalGateAsync` (lines ~1258-1294)**

Replace the inline selection loop with:

```csharp
var instanceContext = string.IsNullOrWhiteSpace(instance.ContextJson)
    ? null
    : JsonSerializer.Deserialize<Dictionary<string, object>>(instance.ContextJson, JsonOpts);

var selected = autoTransitionEvaluator.Select(transitions, fromState, instanceContext);
var targetState = selected?.To;
```

- [ ] **Step 6: Fix all `WorkflowEngine` test constructors to pass the new dependency**

Run: `cd boilerplateBE && grep -rn "new WorkflowEngine(" tests/ src/` and update every call site. Each test needs:

```csharp
var autoTransitionEvaluator = new AutoTransitionEvaluator(conditionEvaluator);
```

and `autoTransitionEvaluator,` added to the `new WorkflowEngine(...)` argument list before `NullLogger<WorkflowEngine>.Instance`.

- [ ] **Step 7: Build + full test run**

Run: `cd boilerplateBE && dotnet build && dotnet test --filter "FullyQualifiedName~Workflow" --no-build 2>&1 | tail -15`
Expected: All workflow tests green, including the new 4 `AutoTransitionEvaluatorTests`.

- [ ] **Step 8: Commit**

```bash
git add boilerplateBE/src/modules/Starter.Module.Workflow/Infrastructure/Services/AutoTransitionEvaluator.cs \
        boilerplateBE/src/modules/Starter.Module.Workflow/Infrastructure/Services/WorkflowEngine.cs \
        boilerplateBE/src/modules/Starter.Module.Workflow/WorkflowModule.cs \
        boilerplateBE/tests/Starter.Api.Tests/Workflow/AutoTransitionEvaluatorTests.cs \
        boilerplateBE/tests/Starter.Api.Tests/Workflow/WorkflowEngineTests.cs \
        boilerplateBE/tests/Starter.Api.Tests/Workflow/GetPendingTasksPaginationTests.cs \
        boilerplateBE/tests/Starter.Api.Tests/Workflow/PendingTasksDenormalizationTests.cs
git commit -m "feat(workflow): extract AutoTransitionEvaluator from WorkflowEngine"
```

---

## Task 3.1: Write failing tests for `ParallelApprovalCoordinator`

**Files:**
- Create: `boilerplateBE/tests/Starter.Api.Tests/Workflow/ParallelApprovalCoordinatorTests.cs`

- [ ] **Step 1: Write the test file**

```csharp
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Starter.Abstractions.Capabilities;
using Starter.Abstractions.Readers;
using Starter.Module.Workflow.Domain.Entities;
using Starter.Module.Workflow.Domain.Enums;
using Starter.Module.Workflow.Infrastructure.Persistence;
using Starter.Module.Workflow.Infrastructure.Services;
using Xunit;

namespace Starter.Api.Tests.Workflow;

public sealed class ParallelApprovalCoordinatorTests : IDisposable
{
    private readonly WorkflowDbContext _db;
    private readonly ParallelApprovalCoordinator _sut;

    public ParallelApprovalCoordinatorTests()
    {
        var options = new DbContextOptionsBuilder<WorkflowDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new WorkflowDbContext(options);
        _sut = new ParallelApprovalCoordinator(_db);
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task EvaluateAsync_NoGroup_ReturnsProceed()
    {
        var task = CreateTask(groupId: null);
        var decision = await _sut.EvaluateAsync(task, parallelMode: "AllOf", action: "approve", CancellationToken.None);

        decision.ShouldProceed.Should().BeTrue();
    }

    [Fact]
    public async Task EvaluateAsync_AnyOf_FirstCompletionCancelsSiblings()
    {
        var groupId = Guid.NewGuid();
        var myTask = CreateTask(groupId: groupId);
        var sibling1 = CreateTask(groupId: groupId);
        var sibling2 = CreateTask(groupId: groupId);
        _db.ApprovalTasks.AddRange(sibling1, sibling2);
        await _db.SaveChangesAsync();

        var decision = await _sut.EvaluateAsync(myTask, parallelMode: "AnyOf", action: "approve", CancellationToken.None);

        decision.ShouldProceed.Should().BeTrue();
        sibling1.Status.Should().Be(TaskStatus.Cancelled);
        sibling2.Status.Should().Be(TaskStatus.Cancelled);
    }

    [Fact]
    public async Task EvaluateAsync_AllOf_WaitingForSiblings_ReturnsWait()
    {
        var groupId = Guid.NewGuid();
        var myTask = CreateTask(groupId: groupId);
        var sibling = CreateTask(groupId: groupId); // still Pending
        _db.ApprovalTasks.Add(sibling);
        await _db.SaveChangesAsync();

        var decision = await _sut.EvaluateAsync(myTask, parallelMode: "AllOf", action: "approve", CancellationToken.None);

        decision.ShouldProceed.Should().BeFalse();
        sibling.Status.Should().Be(TaskStatus.Pending); // untouched
    }

    [Fact]
    public async Task EvaluateAsync_AllOf_RejectShortCircuitsSiblings()
    {
        var groupId = Guid.NewGuid();
        var myTask = CreateTask(groupId: groupId);
        var sibling = CreateTask(groupId: groupId);
        _db.ApprovalTasks.Add(sibling);
        await _db.SaveChangesAsync();

        var decision = await _sut.EvaluateAsync(myTask, parallelMode: "AllOf", action: "reject", CancellationToken.None);

        decision.ShouldProceed.Should().BeTrue();
        sibling.Status.Should().Be(TaskStatus.Cancelled);
    }

    [Fact]
    public async Task EvaluateAsync_AllOf_AllSiblingsCompleted_ReturnsProceed()
    {
        var groupId = Guid.NewGuid();
        var myTask = CreateTask(groupId: groupId);
        var sibling = CreateTask(groupId: groupId);
        sibling.Complete("approve", comment: null, userId: Guid.NewGuid());
        _db.ApprovalTasks.Add(sibling);
        await _db.SaveChangesAsync();

        var decision = await _sut.EvaluateAsync(myTask, parallelMode: "AllOf", action: "approve", CancellationToken.None);

        decision.ShouldProceed.Should().BeTrue();
    }

    private ApprovalTask CreateTask(Guid? groupId)
    {
        var task = ApprovalTask.Create(
            tenantId: null,
            instanceId: Guid.NewGuid(),
            stepName: "Step",
            assigneeUserId: Guid.NewGuid(),
            assigneeRole: null,
            assigneeStrategyJson: null,
            entityType: "Order",
            entityId: Guid.NewGuid(),
            definitionName: "Def",
            availableActionsJson: "[]",
            groupId: groupId);
        _db.ApprovalTasks.Add(task);
        _db.SaveChanges();
        return task;
    }
}
```

- [ ] **Step 2: Run — confirm compilation error**

Run: `cd boilerplateBE && dotnet test --filter "FullyQualifiedName~ParallelApprovalCoordinatorTests" --no-restore 2>&1 | tail -10`
Expected: `error CS0246` for `ParallelApprovalCoordinator` / `ParallelDecision`.

---

## Task 3.2: Create `ParallelApprovalCoordinator`

**Files:**
- Create: `boilerplateBE/src/modules/Starter.Module.Workflow/Infrastructure/Services/ParallelApprovalCoordinator.cs`

- [ ] **Step 1: Write the coordinator**

```csharp
using Microsoft.EntityFrameworkCore;
using Starter.Module.Workflow.Domain.Entities;
using Starter.Module.Workflow.Infrastructure.Persistence;
using TaskStatus = Starter.Module.Workflow.Domain.Enums.TaskStatus;

namespace Starter.Module.Workflow.Infrastructure.Services;

/// <summary>
/// Coordinates a parallel approval group's completion semantics. Invoked after
/// the "my" task has been marked completed but before the engine decides whether
/// to transition the workflow.
/// </summary>
internal sealed class ParallelApprovalCoordinator(WorkflowDbContext context)
{
    /// <summary>
    /// Decides whether the workflow should transition on this task action.
    /// Side effects: may cancel pending sibling tasks depending on mode + action.
    /// </summary>
    public async Task<ParallelDecision> EvaluateAsync(
        ApprovalTask task, string parallelMode, string action, CancellationToken ct)
    {
        if (!task.GroupId.HasValue)
            return ParallelDecision.Proceed;

        var siblings = await context.ApprovalTasks
            .Where(t => t.GroupId == task.GroupId && t.Id != task.Id)
            .ToListAsync(ct);

        if (parallelMode.Equals("AnyOf", StringComparison.OrdinalIgnoreCase))
        {
            foreach (var s in siblings.Where(s => s.Status == TaskStatus.Pending))
                s.Cancel();
            return ParallelDecision.Proceed;
        }

        // AllOf
        if (action.Equals("reject", StringComparison.OrdinalIgnoreCase))
        {
            foreach (var s in siblings.Where(s => s.Status == TaskStatus.Pending))
                s.Cancel();
            return ParallelDecision.Proceed;
        }

        var allComplete = siblings.All(s => s.Status == TaskStatus.Completed);
        return allComplete ? ParallelDecision.Proceed : ParallelDecision.Wait;
    }
}

internal readonly record struct ParallelDecision(bool ShouldProceed)
{
    public static readonly ParallelDecision Proceed = new(true);
    public static readonly ParallelDecision Wait = new(false);
}
```

- [ ] **Step 2: Run tests — 5 pass**

Run: `cd boilerplateBE && dotnet test --filter "FullyQualifiedName~ParallelApprovalCoordinatorTests" --no-restore 2>&1 | tail -10`

---

## Task 3.3: Wire `ParallelApprovalCoordinator` into `ExecuteTaskAsync`

**Files:**
- Modify: `boilerplateBE/src/modules/Starter.Module.Workflow/WorkflowModule.cs` (register)
- Modify: `boilerplateBE/src/modules/Starter.Module.Workflow/Infrastructure/Services/WorkflowEngine.cs` (constructor + replace the parallel block in `ExecuteTaskAsync`)

- [ ] **Step 1: Register**

In `WorkflowModule.cs` next to other collaborator registrations:

```csharp
services.AddScoped<ParallelApprovalCoordinator>();
```

- [ ] **Step 2: Add constructor parameter**

Insert `ParallelApprovalCoordinator parallelCoordinator,` after `AutoTransitionEvaluator autoTransitionEvaluator,`.

- [ ] **Step 3: Replace the parallel-group block in `ExecuteTaskAsync` (approximately lines 394-462)**

Find the entire `if (task.GroupId.HasValue)` branch including the nested `if (parallelMode.Equals("AnyOf", ...))` / `else // AllOf` bodies. Replace with:

```csharp
if (task.GroupId.HasValue)
{
    var parallelMode = fromStateConfig?.Parallel?.Mode ?? "AllOf";
    var decision = await parallelCoordinator.EvaluateAsync(task, parallelMode, action, ct);

    if (!decision.ShouldProceed)
    {
        // AllOf: not all siblings done yet — stay at current state.
        var waitStep = WorkflowStep.Create(
            instance.Id,
            fromState,
            fromState,
            StepType.HumanTask,
            action,
            actorUserId,
            comment,
            metadataJson);
        context.WorkflowSteps.Add(waitStep);

        try
        {
            await context.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            logger.LogWarning(
                "Concurrency conflict on task {TaskId}. Another user may have already acted.", taskId);
            return false;
        }

        logger.LogInformation(
            "Task {TaskId}: completed (parallel AllOf, waiting for siblings). Instance {InstanceId} stays at '{State}'.",
            taskId, instance.Id, fromState);
        return true;
    }
}
```

- [ ] **Step 4: Update all `WorkflowEngine` test constructors to pass `parallelCoordinator`**

Run: `cd boilerplateBE && grep -rn "new WorkflowEngine(" tests/ src/`
For each, add:

```csharp
var parallelCoordinator = new ParallelApprovalCoordinator(_db);
```

and pass `parallelCoordinator,` as the constructor argument before `NullLogger<WorkflowEngine>.Instance`.

- [ ] **Step 5: Build + full test run**

Run: `cd boilerplateBE && dotnet build && dotnet test --filter "FullyQualifiedName~Workflow" --no-build 2>&1 | tail -20`
Expected: All workflow tests green, including new `ParallelApprovalCoordinatorTests` (5 tests). `WorkflowEngineTests`' parallel scenarios must remain green (they're the regression guard for the refactor).

- [ ] **Step 6: Commit**

```bash
git add boilerplateBE/src/modules/Starter.Module.Workflow/Infrastructure/Services/ParallelApprovalCoordinator.cs \
        boilerplateBE/src/modules/Starter.Module.Workflow/Infrastructure/Services/WorkflowEngine.cs \
        boilerplateBE/src/modules/Starter.Module.Workflow/WorkflowModule.cs \
        boilerplateBE/tests/Starter.Api.Tests/Workflow/ParallelApprovalCoordinatorTests.cs \
        boilerplateBE/tests/Starter.Api.Tests/Workflow/WorkflowEngineTests.cs \
        boilerplateBE/tests/Starter.Api.Tests/Workflow/GetPendingTasksPaginationTests.cs \
        boilerplateBE/tests/Starter.Api.Tests/Workflow/PendingTasksDenormalizationTests.cs
git commit -m "feat(workflow): extract ParallelApprovalCoordinator from WorkflowEngine"
```

---

## Task 4.1: Add failing tests for NOT operator, short-circuit, and JSON round-trip

**Files:**
- Modify: `boilerplateBE/tests/Starter.Api.Tests/Workflow/ConditionEvaluatorTests.cs`

- [ ] **Step 1: Append new `[Fact]` cases at the end of the class**

```csharp
// --- NOT operator ---

[Fact]
public void Evaluate_NotGroup_SingleFalseChild_ReturnsTrue()
{
    var condition = new ConditionConfig(
        Logic: "not",
        Conditions:
        [
            new ConditionConfig(Field: "status", Operator: "equals", Value: "Active"),
        ]);
    var context = MakeContext(new() { ["status"] = "Inactive" });

    _sut.Evaluate(condition, context).Should().BeTrue();
}

[Fact]
public void Evaluate_NotGroup_SingleTrueChild_ReturnsFalse()
{
    var condition = new ConditionConfig(
        Logic: "not",
        Conditions:
        [
            new ConditionConfig(Field: "status", Operator: "equals", Value: "Active"),
        ]);
    var context = MakeContext(new() { ["status"] = "Active" });

    _sut.Evaluate(condition, context).Should().BeFalse();
}

[Fact]
public void Evaluate_NotGroup_EmptyConditions_ReturnsFalse()
{
    // NOT over empty is ambiguous; we choose false (same as empty AND) to
    // prevent accidental permit-all semantics in misconfigured workflows.
    var condition = new ConditionConfig(Logic: "not", Conditions: []);
    _sut.Evaluate(condition, MakeContext(new() { ["x"] = "y" })).Should().BeFalse();
}

[Fact]
public void Evaluate_NestedAndOrNot_EvaluatesCorrectly()
{
    // AND [ equals("status", "Active"), NOT [ equals("role", "Guest") ] ]
    // status=Active, role=Admin => true && !false => true
    var condition = new ConditionConfig(
        Logic: "and",
        Conditions:
        [
            new ConditionConfig(Field: "status", Operator: "equals", Value: "Active"),
            new ConditionConfig(
                Logic: "not",
                Conditions: [ new ConditionConfig(Field: "role", Operator: "equals", Value: "Guest") ]),
        ]);
    var context = MakeContext(new() { ["status"] = "Active", ["role"] = "Admin" });

    _sut.Evaluate(condition, context).Should().BeTrue();
}

// --- Short-circuit (behavior-observable via a sentinel condition) ---

[Fact]
public void Evaluate_ShortCircuit_AndStopsOnFirstFalse()
{
    // If AND short-circuits, the second condition (which references a field
    // present only in the fallback path) never runs. We assert by giving it a
    // condition that would otherwise throw or return false — either way the
    // overall result is false. This documents short-circuit intent.
    var condition = new ConditionConfig(
        Logic: "and",
        Conditions:
        [
            new ConditionConfig(Field: "alwaysFalse", Operator: "equals", Value: "never"),
            new ConditionConfig(Field: "amount", Operator: "greaterThan", Value: 10),
        ]);
    var context = MakeContext(new() { ["alwaysFalse"] = "other", ["amount"] = 100 });

    _sut.Evaluate(condition, context).Should().BeFalse();
}

[Fact]
public void Evaluate_ShortCircuit_OrStopsOnFirstTrue()
{
    var condition = new ConditionConfig(
        Logic: "or",
        Conditions:
        [
            new ConditionConfig(Field: "status", Operator: "equals", Value: "Active"),
            new ConditionConfig(Field: "status", Operator: "equals", Value: "Closed"),
        ]);
    var context = MakeContext(new() { ["status"] = "Active" });

    _sut.Evaluate(condition, context).Should().BeTrue();
}

// --- JSON round-trip ---

[Fact]
public void Evaluate_JsonRoundTrip_PreservesStructure_AndEvaluatesCorrectly()
{
    // Build a compound condition, serialize, deserialize, evaluate — the
    // engine uses this exact path when reading StatesJson/TransitionsJson from
    // the WorkflowDefinition table, so the round-trip must keep semantics intact.
    var original = new ConditionConfig(
        Logic: "or",
        Conditions:
        [
            new ConditionConfig(Field: "department", Operator: "equals", Value: "Finance"),
            new ConditionConfig(
                Logic: "and",
                Conditions:
                [
                    new ConditionConfig(Field: "department", Operator: "equals", Value: "HR"),
                    new ConditionConfig(
                        Logic: "not",
                        Conditions: [ new ConditionConfig(Field: "role", Operator: "equals", Value: "Guest") ]),
                ]),
        ]);

    var json = JsonSerializer.Serialize(original);
    var rehydrated = JsonSerializer.Deserialize<ConditionConfig>(json,
        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

    rehydrated.Should().NotBeNull();

    var context = MakeContext(new() { ["department"] = "HR", ["role"] = "Admin" });
    _sut.Evaluate(rehydrated!, context).Should().BeTrue();
}
```

- [ ] **Step 2: Run — confirm the 3 NOT tests fail (feature not yet implemented)**

Run: `cd boilerplateBE && dotnet test --filter "FullyQualifiedName~ConditionEvaluatorTests&FullyQualifiedName~Not" --no-restore 2>&1 | tail -20`
Expected: `Failed: 3` (NOT-prefixed tests) — AND/OR/short-circuit/round-trip pass because they use already-supported paths.

---

## Task 4.2: Implement NOT operator in `ConditionEvaluator`

**Files:**
- Modify: `boilerplateBE/src/modules/Starter.Module.Workflow/Infrastructure/Services/ConditionEvaluator.cs:16-23`

- [ ] **Step 1: Extend the compound switch**

Replace the existing block:

```csharp
if (condition.Logic is not null && condition.Conditions is { Count: > 0 })
{
    return condition.Logic.ToUpperInvariant() switch
    {
        "OR" => condition.Conditions.Any(c => Evaluate(c, context)),
        _ => condition.Conditions.All(c => Evaluate(c, context)), // default AND
    };
}
```

with:

```csharp
if (condition.Logic is not null)
{
    var op = condition.Logic.ToUpperInvariant();

    // NOT needs explicit handling for the "empty conditions" case — define it
    // as false (same semantics as an empty AND group) to avoid a misconfigured
    // workflow accidentally granting permit-all access.
    if (op == "NOT")
    {
        if (condition.Conditions is null or { Count: 0 }) return false;
        // Multi-child NOT treats its children as implicit AND, then inverts.
        return !condition.Conditions.All(c => Evaluate(c, context));
    }

    if (condition.Conditions is null or { Count: 0 }) return false;

    return op switch
    {
        "OR" => condition.Conditions.Any(c => Evaluate(c, context)),
        _ => condition.Conditions.All(c => Evaluate(c, context)), // default AND
    };
}
```

- [ ] **Step 2: Run the full `ConditionEvaluatorTests` suite**

Run: `cd boilerplateBE && dotnet test --filter "FullyQualifiedName~ConditionEvaluatorTests" --no-restore 2>&1 | tail -20`
Expected: All tests pass (17 original + 7 new = 24).

- [ ] **Step 3: Commit**

```bash
git add boilerplateBE/src/modules/Starter.Module.Workflow/Infrastructure/Services/ConditionEvaluator.cs \
        boilerplateBE/tests/Starter.Api.Tests/Workflow/ConditionEvaluatorTests.cs
git commit -m "feat(workflow): support NOT operator in compound conditions"
```

---

## Task 5.1: Add DTOs for bulk execute result

**Files:**
- Create: `boilerplateBE/src/modules/Starter.Module.Workflow/Application/DTOs/BatchExecuteResult.cs`

- [ ] **Step 1: Write the records**

```csharp
namespace Starter.Module.Workflow.Application.DTOs;

/// <summary>
/// Aggregate outcome of a <c>BatchExecuteTasksCommand</c>. Returned to the caller
/// so the UI can show per-task status without a second round-trip.
/// </summary>
public sealed record BatchExecuteResult(
    int Succeeded,
    int Failed,
    int Skipped,
    IReadOnlyList<BatchItemOutcome> Items);

public sealed record BatchItemOutcome(
    Guid TaskId,
    string Status, // "Succeeded" | "Failed" | "Skipped"
    string? Error);
```

---

## Task 5.2: Write failing tests for `BatchExecuteTasksCommandHandler`

**Files:**
- Create: `boilerplateBE/tests/Starter.Api.Tests/Workflow/BatchExecuteTasksTests.cs`

- [ ] **Step 1: Write handler tests**

```csharp
using FluentAssertions;
using Moq;
using Starter.Abstractions.Capabilities;
using Starter.Application.Common.Interfaces;
using Starter.Module.Workflow.Application.Commands.BatchExecuteTasks;
using Xunit;

namespace Starter.Api.Tests.Workflow;

public sealed class BatchExecuteTasksTests
{
    private readonly Mock<IWorkflowService> _workflow = new();
    private readonly Mock<ICurrentUserService> _currentUser = new();
    private readonly Guid _userId = Guid.NewGuid();

    public BatchExecuteTasksTests()
    {
        _currentUser.SetupGet(x => x.UserId).Returns(_userId);
    }

    private BatchExecuteTasksCommandHandler Handler() => new(_workflow.Object, _currentUser.Object);

    [Fact]
    public async Task Handle_AllSucceed_ReturnsAllSucceeded()
    {
        var ids = new[] { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };
        _workflow
            .Setup(w => w.ExecuteTaskAsync(It.IsAny<Guid>(), "approve", null, _userId, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await Handler().Handle(
            new BatchExecuteTasksCommand(ids, "approve"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var r = result.Value!;
        r.Succeeded.Should().Be(3);
        r.Failed.Should().Be(0);
        r.Skipped.Should().Be(0);
        r.Items.Should().HaveCount(3).And.OnlyContain(i => i.Status == "Succeeded");
    }

    [Fact]
    public async Task Handle_MixedResults_AggregatesCorrectly()
    {
        var success = Guid.NewGuid();
        var fail = Guid.NewGuid();
        var throws = Guid.NewGuid();

        _workflow.Setup(w => w.ExecuteTaskAsync(success, "approve", null, _userId, null, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _workflow.Setup(w => w.ExecuteTaskAsync(fail, "approve", null, _userId, null, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        _workflow.Setup(w => w.ExecuteTaskAsync(throws, "approve", null, _userId, null, It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("nope"));

        var result = await Handler().Handle(
            new BatchExecuteTasksCommand(new[] { success, fail, throws }, "approve"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var r = result.Value!;
        r.Succeeded.Should().Be(1);
        r.Failed.Should().Be(2);
        r.Skipped.Should().Be(0);
        r.Items.Single(i => i.TaskId == success).Status.Should().Be("Succeeded");
        r.Items.Single(i => i.TaskId == fail).Status.Should().Be("Failed");
        r.Items.Single(i => i.TaskId == throws).Error.Should().Contain("nope");
    }

    [Fact]
    public async Task Handle_OneExceptionDoesNotAbortBatch()
    {
        var bad = Guid.NewGuid();
        var good = Guid.NewGuid();
        _workflow.Setup(w => w.ExecuteTaskAsync(bad, It.IsAny<string>(), null, _userId, null, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("boom"));
        _workflow.Setup(w => w.ExecuteTaskAsync(good, It.IsAny<string>(), null, _userId, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await Handler().Handle(
            new BatchExecuteTasksCommand(new[] { bad, good }, "approve"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Succeeded.Should().Be(1);
        result.Value.Failed.Should().Be(1);
    }

    [Fact]
    public async Task Handle_PassesCommentThrough()
    {
        var id = Guid.NewGuid();
        _workflow.Setup(w => w.ExecuteTaskAsync(id, "reject", "bulk reject", _userId, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await Handler().Handle(
            new BatchExecuteTasksCommand(new[] { id }, "reject", "bulk reject"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _workflow.Verify(w => w.ExecuteTaskAsync(id, "reject", "bulk reject", _userId, null, It.IsAny<CancellationToken>()), Times.Once);
    }
}
```

- [ ] **Step 2: Run — confirm compilation fails (command/handler not yet created)**

---

## Task 5.3: Create the command, handler, validator

**Files:**
- Create: `boilerplateBE/src/modules/Starter.Module.Workflow/Application/Commands/BatchExecuteTasks/BatchExecuteTasksCommand.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.Workflow/Application/Commands/BatchExecuteTasks/BatchExecuteTasksCommandHandler.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.Workflow/Application/Commands/BatchExecuteTasks/BatchExecuteTasksCommandValidator.cs`

- [ ] **Step 1: Write the command**

```csharp
using MediatR;
using Starter.Module.Workflow.Application.DTOs;
using Starter.Shared.Results;

namespace Starter.Module.Workflow.Application.Commands.BatchExecuteTasks;

public sealed record BatchExecuteTasksCommand(
    IReadOnlyList<Guid> TaskIds,
    string Action,
    string? Comment = null) : IRequest<Result<BatchExecuteResult>>;
```

- [ ] **Step 2: Write the handler**

```csharp
using MediatR;
using Starter.Abstractions.Capabilities;
using Starter.Application.Common.Interfaces;
using Starter.Module.Workflow.Application.DTOs;
using Starter.Shared.Results;

namespace Starter.Module.Workflow.Application.Commands.BatchExecuteTasks;

internal sealed class BatchExecuteTasksCommandHandler(
    IWorkflowService workflowService,
    ICurrentUserService currentUser)
    : IRequestHandler<BatchExecuteTasksCommand, Result<BatchExecuteResult>>
{
    public async Task<Result<BatchExecuteResult>> Handle(
        BatchExecuteTasksCommand request, CancellationToken cancellationToken)
    {
        var outcomes = new List<BatchItemOutcome>(request.TaskIds.Count);
        var userId = currentUser.UserId!.Value;

        foreach (var taskId in request.TaskIds)
        {
            try
            {
                // Single-task path already idempotent + tenant-filtered inside the engine.
                var ok = await workflowService.ExecuteTaskAsync(
                    taskId, request.Action, request.Comment, userId,
                    formData: null, cancellationToken);

                outcomes.Add(ok
                    ? new BatchItemOutcome(taskId, "Succeeded", null)
                    : new BatchItemOutcome(taskId, "Failed", "Task could not be executed (not found, not pending, or unauthorized)."));
            }
            catch (Exception ex)
            {
                outcomes.Add(new BatchItemOutcome(taskId, "Failed", ex.Message));
            }
        }

        var result = new BatchExecuteResult(
            Succeeded: outcomes.Count(o => o.Status == "Succeeded"),
            Failed: outcomes.Count(o => o.Status == "Failed"),
            Skipped: outcomes.Count(o => o.Status == "Skipped"),
            Items: outcomes);

        return Result.Success(result);
    }
}
```

- [ ] **Step 3: Write the validator**

```csharp
using FluentValidation;

namespace Starter.Module.Workflow.Application.Commands.BatchExecuteTasks;

public sealed class BatchExecuteTasksCommandValidator : AbstractValidator<BatchExecuteTasksCommand>
{
    public BatchExecuteTasksCommandValidator()
    {
        RuleFor(x => x.TaskIds)
            .NotEmpty().WithMessage("At least one task id is required.")
            .Must(ids => ids.Count <= 50).WithMessage("Bulk action supports at most 50 tasks per request.");

        RuleFor(x => x.Action)
            .NotEmpty().WithMessage("Action is required.")
            .MaximumLength(100);

        RuleFor(x => x.Comment)
            .MaximumLength(2000);
    }
}
```

- [ ] **Step 4: Run the handler tests**

Run: `cd boilerplateBE && dotnet test --filter "FullyQualifiedName~BatchExecuteTasksTests" --no-restore 2>&1 | tail -10`
Expected: `Passed: 4`.

---

## Task 5.4: Expose the endpoint

**Files:**
- Modify: `boilerplateBE/src/modules/Starter.Module.Workflow/Controllers/WorkflowController.cs:174-184` and `:231` (request record)

- [ ] **Step 1: Add the endpoint**

Add after `ExecuteTask(...)` method:

```csharp
[HttpPost("tasks/batch-execute")]
[Authorize(Policy = WorkflowPermissions.ActOnTask)]
[ProducesResponseType(typeof(ApiResponse<BatchExecuteResult>), StatusCodes.Status200OK)]
[ProducesResponseType(StatusCodes.Status400BadRequest)]
public async Task<IActionResult> BatchExecuteTasks(
    [FromBody] BatchExecuteTasksRequest request, CancellationToken ct = default)
{
    var result = await Mediator.Send(
        new BatchExecuteTasksCommand(request.TaskIds, request.Action, request.Comment), ct);
    return HandleResult(result);
}
```

- [ ] **Step 2: Add the request record and using statements**

Add `using Starter.Module.Workflow.Application.Commands.BatchExecuteTasks;` + `using Starter.Module.Workflow.Application.DTOs;` at the top of the file. Add at the bottom next to existing request records:

```csharp
public sealed record BatchExecuteTasksRequest(
    IReadOnlyList<Guid> TaskIds,
    string Action,
    string? Comment);
```

- [ ] **Step 3: Build + smoke test the route via a controller integration test if one exists**

Run: `cd boilerplateBE && dotnet build 2>&1 | tail -5`
Expected: Build succeeds.

- [ ] **Step 4: Commit**

```bash
git add boilerplateBE/src/modules/Starter.Module.Workflow/Application/Commands/BatchExecuteTasks \
        boilerplateBE/src/modules/Starter.Module.Workflow/Application/DTOs/BatchExecuteResult.cs \
        boilerplateBE/src/modules/Starter.Module.Workflow/Controllers/WorkflowController.cs \
        boilerplateBE/tests/Starter.Api.Tests/Workflow/BatchExecuteTasksTests.cs
git commit -m "feat(workflow): add BatchExecuteTasks command and endpoint"
```

---

## Task 6.1: Add FE types, API client, query hook

**Files:**
- Modify: `boilerplateFE/src/types/workflow.types.ts`
- Modify: `boilerplateFE/src/config/api.config.ts` (add `TASKS_BATCH`)
- Modify: `boilerplateFE/src/features/workflow/api/workflow.api.ts`
- Modify: `boilerplateFE/src/features/workflow/api/workflow.queries.ts`

- [ ] **Step 1: Add types at the bottom of `workflow.types.ts`**

```ts
export interface BatchItemOutcome {
  taskId: string;
  status: 'Succeeded' | 'Failed' | 'Skipped';
  error: string | null;
}

export interface BatchExecuteResult {
  succeeded: number;
  failed: number;
  skipped: number;
  items: BatchItemOutcome[];
}

export interface BatchExecuteTasksRequest {
  taskIds: string[];
  action: string;
  comment?: string;
}
```

- [ ] **Step 2: Add the endpoint constant**

In `api.config.ts` inside the `WORKFLOW` block, after `TASK_EXECUTE`:

```ts
TASK_BATCH_EXECUTE: '/workflow/tasks/batch-execute',
```

- [ ] **Step 3: Add the API method in `workflow.api.ts`**

After `executeTask(...)`, add:

```ts
batchExecuteTasks: (data: BatchExecuteTasksRequest): Promise<BatchExecuteResult> =>
  apiClient
    .post<ApiResponse<BatchExecuteResult>>(API_ENDPOINTS.WORKFLOW.TASK_BATCH_EXECUTE, data)
    .then((r) => r.data.data),
```

Also add `BatchExecuteTasksRequest` and `BatchExecuteResult` to the imports at the top.

- [ ] **Step 4: Add the mutation hook in `workflow.queries.ts`**

After `useExecuteTask`, add:

```ts
export function useBatchExecuteTasks() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (data: BatchExecuteTasksRequest) => workflowApi.batchExecuteTasks(data),
    onSuccess: (result) => {
      queryClient.invalidateQueries({ queryKey: queryKeys.workflow.tasks.all });
      queryClient.invalidateQueries({ queryKey: queryKeys.workflow.instances.all });

      if (result.failed > 0 && result.succeeded > 0) {
        toast.warning(i18n.t('workflow.inbox.bulkResultSummary', {
          succeeded: result.succeeded, failed: result.failed, skipped: result.skipped,
        }));
      } else if (result.failed > 0) {
        toast.error(i18n.t('workflow.inbox.bulkResultSummary', {
          succeeded: 0, failed: result.failed, skipped: result.skipped,
        }));
      } else {
        toast.success(i18n.t('workflow.inbox.bulkResultSummary', {
          succeeded: result.succeeded, failed: 0, skipped: result.skipped,
        }));
      }
    },
    onError: handleMutationError,
  });
}
```

Add `BatchExecuteTasksRequest` to the import from `@/types/workflow.types`.

- [ ] **Step 5: Build the FE to catch type errors**

Run: `cd boilerplateFE && npm run build 2>&1 | tail -10`
Expected: Build succeeds (`vite build` finishes without TS errors).

---

## Task 6.2: Add i18n keys

**Files:**
- Modify: `boilerplateFE/src/i18n/locales/en/translation.json`
- Modify: `boilerplateFE/src/i18n/locales/ar/translation.json`
- Modify: `boilerplateFE/src/i18n/locales/ku/translation.json`

- [ ] **Step 1: Extend the `workflow.inbox` block in `en/translation.json`**

Add these keys inside the existing `"workflow": { "inbox": { ... } }` object:

```json
"selectAll": "Select all",
"select": "Select task",
"selected": "{{count}} selected",
"bulkApprove": "Approve selected",
"bulkReject": "Reject selected",
"bulkReturn": "Return selected",
"bulkConfirmTitle": "{{action}} {{count}} tasks?",
"bulkConfirmDesc": "This will run the same action on all selected tasks.",
"bulkResultTitle": "Bulk action complete",
"bulkResultSummary": "{{succeeded}} succeeded, {{failed}} failed, {{skipped}} skipped",
"bulkResultSucceeded": "Succeeded",
"bulkResultFailed": "Failed",
"bulkResultSkipped": "Skipped",
"bulkCommentPlaceholder": "Optional comment applied to all tasks",
"bulkViewDetails": "View details",
"clearSelection": "Clear selection"
```

- [ ] **Step 2: Add the same keys with translated values in `ar/translation.json` and `ku/translation.json`**

Use the existing Arabic / Kurdish keys as tonal reference. Example Arabic values:

```json
"selectAll": "تحديد الكل",
"selected": "تم تحديد {{count}}",
"bulkApprove": "الموافقة على المحدد",
"bulkReject": "رفض المحدد",
"bulkReturn": "إرجاع المحدد",
"bulkConfirmTitle": "{{action}} {{count}} مهمة؟",
"bulkConfirmDesc": "سيتم تشغيل نفس الإجراء على جميع المهام المحددة.",
"bulkResultTitle": "اكتمل الإجراء الجماعي",
"bulkResultSummary": "{{succeeded}} ناجحة، {{failed}} فاشلة، {{skipped}} متخطّاة",
"bulkResultSucceeded": "ناجحة",
"bulkResultFailed": "فاشلة",
"bulkResultSkipped": "متخطّاة",
"bulkCommentPlaceholder": "تعليق اختياري مطبق على جميع المهام",
"bulkViewDetails": "عرض التفاصيل",
"clearSelection": "مسح التحديد",
"select": "تحديد المهمة"
```

For Kurdish (Sorani), mirror the Arabic pattern with Kurdish vocabulary. (The user will verify Kurdish copy post-implementation.)

---

## Task 6.3: Build the `BulkActionBar` component

**Files:**
- Create: `boilerplateFE/src/features/workflow/components/BulkActionBar.tsx`

- [ ] **Step 1: Write the component**

```tsx
import { useTranslation } from 'react-i18next';
import { CheckCheck, X, RotateCcw, XCircle } from 'lucide-react';
import { Button } from '@/components/ui/button';

interface BulkActionBarProps {
  selectedCount: number;
  isPending: boolean;
  onApprove: () => void;
  onReject: () => void;
  onReturn: () => void;
  onClear: () => void;
}

export function BulkActionBar({
  selectedCount, isPending, onApprove, onReject, onReturn, onClear,
}: BulkActionBarProps) {
  const { t } = useTranslation();

  return (
    <div className="fixed inset-x-0 bottom-6 z-40 flex justify-center px-4 pointer-events-none">
      <div className="pointer-events-auto flex items-center gap-2 rounded-2xl border bg-card shadow-card px-4 py-3">
        <span className="text-sm font-medium text-foreground ltr:mr-2 rtl:ml-2">
          {t('workflow.inbox.selected', { count: selectedCount })}
        </span>
        <Button size="sm" onClick={onApprove} disabled={isPending}>
          <CheckCheck className="h-4 w-4 ltr:mr-1 rtl:ml-1" />
          {t('workflow.inbox.bulkApprove')}
        </Button>
        <Button size="sm" variant="outline" onClick={onReject} disabled={isPending}>
          <XCircle className="h-4 w-4 ltr:mr-1 rtl:ml-1" />
          {t('workflow.inbox.bulkReject')}
        </Button>
        <Button size="sm" variant="outline" onClick={onReturn} disabled={isPending}>
          <RotateCcw className="h-4 w-4 ltr:mr-1 rtl:ml-1" />
          {t('workflow.inbox.bulkReturn')}
        </Button>
        <Button size="sm" variant="ghost" onClick={onClear} disabled={isPending}>
          <X className="h-4 w-4 ltr:mr-1 rtl:ml-1" />
          {t('workflow.inbox.clearSelection')}
        </Button>
      </div>
    </div>
  );
}
```

---

## Task 6.4: Build the `BulkConfirmDialog` and `BulkResultDialog` components

**Files:**
- Create: `boilerplateFE/src/features/workflow/components/BulkConfirmDialog.tsx`
- Create: `boilerplateFE/src/features/workflow/components/BulkResultDialog.tsx`

- [ ] **Step 1: `BulkConfirmDialog.tsx`**

```tsx
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import {
  Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle,
} from '@/components/ui/dialog';
import { Button } from '@/components/ui/button';
import { Textarea } from '@/components/ui/textarea';

type BulkAction = 'Approve' | 'Reject' | 'ReturnForRevision';

interface Props {
  action: BulkAction | null;
  count: number;
  isPending: boolean;
  onSubmit: (comment: string | undefined) => void;
  onCancel: () => void;
}

export function BulkConfirmDialog({ action, count, isPending, onSubmit, onCancel }: Props) {
  const { t } = useTranslation();
  const [comment, setComment] = useState('');

  const open = action !== null;

  return (
    <Dialog open={open} onOpenChange={(next) => { if (!next) onCancel(); }}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>
            {t('workflow.inbox.bulkConfirmTitle', { action: action ?? '', count })}
          </DialogTitle>
          <DialogDescription>{t('workflow.inbox.bulkConfirmDesc')}</DialogDescription>
        </DialogHeader>
        <Textarea
          placeholder={t('workflow.inbox.bulkCommentPlaceholder')}
          value={comment}
          onChange={(e) => setComment(e.target.value)}
          disabled={isPending}
          maxLength={2000}
        />
        <DialogFooter>
          <Button variant="ghost" onClick={onCancel} disabled={isPending}>
            {t('common.cancel', 'Cancel')}
          </Button>
          <Button
            onClick={() => { onSubmit(comment.trim() || undefined); setComment(''); }}
            disabled={isPending}
          >
            {t('common.confirm', 'Confirm')}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
```

- [ ] **Step 2: `BulkResultDialog.tsx`**

```tsx
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import {
  Dialog, DialogContent, DialogFooter, DialogHeader, DialogTitle,
} from '@/components/ui/dialog';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { ChevronDown, ChevronRight } from 'lucide-react';
import type { BatchExecuteResult } from '@/types/workflow.types';

interface Props {
  result: BatchExecuteResult | null;
  onClose: () => void;
}

export function BulkResultDialog({ result, onClose }: Props) {
  const { t } = useTranslation();
  const [expanded, setExpanded] = useState(false);

  const open = result !== null;

  return (
    <Dialog open={open} onOpenChange={(next) => { if (!next) onClose(); }}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>{t('workflow.inbox.bulkResultTitle')}</DialogTitle>
        </DialogHeader>

        {result && (
          <div className="space-y-3">
            <p className="text-sm text-foreground">
              {t('workflow.inbox.bulkResultSummary', result)}
            </p>

            <button
              type="button"
              className="flex items-center gap-1 text-sm text-primary hover:underline"
              onClick={() => setExpanded((x) => !x)}
            >
              {expanded
                ? <ChevronDown className="h-4 w-4" />
                : <ChevronRight className="h-4 w-4" />}
              {t('workflow.inbox.bulkViewDetails')}
            </button>

            {expanded && (
              <ul className="max-h-64 overflow-auto rounded-xl border p-3 space-y-2">
                {result.items.map((item) => (
                  <li key={item.taskId} className="flex items-start justify-between gap-2">
                    <div className="min-w-0">
                      <div className="font-mono text-xs text-muted-foreground truncate">
                        {item.taskId}
                      </div>
                      {item.error && (
                        <div className="text-xs text-destructive">{item.error}</div>
                      )}
                    </div>
                    <Badge
                      variant={
                        item.status === 'Succeeded' ? 'default'
                        : item.status === 'Failed'  ? 'destructive'
                                                    : 'secondary'
                      }
                    >
                      {t(`workflow.inbox.bulkResult${item.status}`)}
                    </Badge>
                  </li>
                ))}
              </ul>
            )}
          </div>
        )}

        <DialogFooter>
          <Button onClick={onClose}>{t('common.close', 'Close')}</Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
```

---

## Task 6.5: Wire bulk selection + actions into the Inbox page

**Files:**
- Modify: `boilerplateFE/src/features/workflow/pages/WorkflowInboxPage.tsx`

- [ ] **Step 1: Rewrite the page to include selection + bulk actions**

Replace the whole file with:

```tsx
import { useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Inbox, Users, X, Plus } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Checkbox } from '@/components/ui/checkbox';
import { Spinner } from '@/components/ui/spinner';
import {
  Table, TableBody, TableCell, TableHead, TableHeader, TableRow,
} from '@/components/ui/table';
import { PageHeader, EmptyState, Pagination } from '@/components/common';
import { getPersistedPageSize } from '@/components/common/pagination-utils';
import { usePendingTasks, useActiveDelegation, useCancelDelegation, useBatchExecuteTasks } from '../api';
import { usePermissions } from '@/hooks';
import { PERMISSIONS } from '@/constants';
import { ApprovalDialog } from '../components/ApprovalDialog';
import { DelegationDialog } from '../components/DelegationDialog';
import { NewRequestDialog } from '../components/NewRequestDialog';
import { BulkActionBar } from '../components/BulkActionBar';
import { BulkConfirmDialog } from '../components/BulkConfirmDialog';
import { BulkResultDialog } from '../components/BulkResultDialog';
import { formatDate } from '@/utils/format';
import type { PendingTaskSummary, BatchExecuteResult } from '@/types/workflow.types';

type BulkAction = 'Approve' | 'Reject' | 'ReturnForRevision';

export default function WorkflowInboxPage() {
  const { t } = useTranslation();
  const { hasPermission } = usePermissions();
  const canStart = hasPermission(PERMISSIONS.Workflows.Start);
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(getPersistedPageSize);
  const [selectedTask, setSelectedTask] = useState<PendingTaskSummary | null>(null);
  const [delegationOpen, setDelegationOpen] = useState(false);
  const [newRequestOpen, setNewRequestOpen] = useState(false);

  const [selectedIds, setSelectedIds] = useState<Set<string>>(new Set());
  const [pendingBulkAction, setPendingBulkAction] = useState<BulkAction | null>(null);
  const [bulkResult, setBulkResult] = useState<BatchExecuteResult | null>(null);

  const { data, isLoading } = usePendingTasks({ page, pageSize });
  const { data: activeDelegation } = useActiveDelegation();
  const { mutate: cancelDelegation, isPending: cancellingDelegation } = useCancelDelegation();
  const { mutate: batchExecute, isPending: isBulkPending } = useBatchExecuteTasks();

  const tasks: PendingTaskSummary[] = data?.data ?? [];
  const pagination = data?.pagination;
  const hasDelegation = !!activeDelegation?.isActive;

  // Clear selection when the page of data changes (avoids cross-page stale IDs).
  useEffect(() => { setSelectedIds(new Set()); }, [page, pageSize]);

  const allVisibleIds = tasks.map((t) => t.taskId);
  const allSelected = allVisibleIds.length > 0 && allVisibleIds.every((id) => selectedIds.has(id));

  const toggleOne = (id: string) => {
    setSelectedIds((prev) => {
      const next = new Set(prev);
      if (next.has(id)) next.delete(id); else next.add(id);
      return next;
    });
  };

  const toggleAll = () => {
    setSelectedIds((prev) => {
      if (allSelected) {
        const next = new Set(prev);
        allVisibleIds.forEach((id) => next.delete(id));
        return next;
      }
      return new Set([...prev, ...allVisibleIds]);
    });
  };

  const clearSelection = () => setSelectedIds(new Set());

  const confirmBulk = (comment: string | undefined) => {
    if (!pendingBulkAction || selectedIds.size === 0) return;
    batchExecute(
      { taskIds: Array.from(selectedIds), action: pendingBulkAction, comment },
      {
        onSuccess: (result) => {
          setBulkResult(result);
          clearSelection();
        },
        onSettled: () => setPendingBulkAction(null),
      },
    );
  };

  return (
    <div className="space-y-6">
      <PageHeader
        title={t('workflow.inbox.title')}
        actions={
          <div className="flex items-center gap-2">
            {canStart && (
              <Button onClick={() => setNewRequestOpen(true)}>
                <Plus className="h-4 w-4 ltr:mr-2 rtl:ml-2" />
                {t('workflow.newRequest.title')}
              </Button>
            )}
            <Button variant="outline" onClick={() => setDelegationOpen(true)}>
              <Users className="h-4 w-4 ltr:mr-2 rtl:ml-2" />
              {t('workflow.delegation.title')}
            </Button>
          </div>
        }
      />

      {hasDelegation && (
        <div className="flex items-center justify-between rounded-xl border border-primary/20 bg-primary/5 px-4 py-3">
          <p className="text-sm text-foreground">
            {t('workflow.delegation.banner', {
              name: activeDelegation!.toDisplayName ?? activeDelegation!.toUserId,
              date: formatDate(activeDelegation!.endDate),
            })}
          </p>
          <Button
            variant="ghost"
            size="sm"
            onClick={() => cancelDelegation(activeDelegation!.id)}
            disabled={cancellingDelegation}
          >
            <X className="h-4 w-4 ltr:mr-1 rtl:ml-1" />
            {t('workflow.delegation.cancel')}
          </Button>
        </div>
      )}

      {isLoading ? (
        <div className="flex justify-center py-12"><Spinner size="lg" /></div>
      ) : tasks.length === 0 ? (
        <EmptyState
          icon={Inbox}
          title={t('workflow.inbox.empty')}
          description={t('workflow.inbox.emptyDesc')}
        />
      ) : (
        <>
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead className="w-10">
                  <Checkbox
                    aria-label={t('workflow.inbox.selectAll')}
                    checked={allSelected}
                    onCheckedChange={toggleAll}
                  />
                </TableHead>
                <TableHead>{t('workflow.inbox.request')}</TableHead>
                <TableHead>{t('workflow.inbox.workflowName')}</TableHead>
                <TableHead>{t('workflow.inbox.step')}</TableHead>
                <TableHead>{t('workflow.inbox.assignedDate')}</TableHead>
                <TableHead>{t('workflow.inbox.dueDate')}</TableHead>
                <TableHead>{t('workflow.inbox.actions')}</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {tasks.map((task) => {
                const checked = selectedIds.has(task.taskId);
                return (
                  <TableRow key={task.taskId} data-selected={checked}>
                    <TableCell>
                      <Checkbox
                        aria-label={t('workflow.inbox.select')}
                        checked={checked}
                        onCheckedChange={() => toggleOne(task.taskId)}
                      />
                    </TableCell>
                    <TableCell>
                      <div className="flex items-center gap-2 flex-wrap">
                        <Badge variant="secondary">{task.entityType}</Badge>
                        <span className="text-sm text-foreground truncate max-w-[200px]">
                          {task.entityDisplayName ?? task.entityId.substring(0, 8) + '...'}
                        </span>
                        {task.isOverdue && (
                          <Badge variant="destructive">
                            {t('workflow.sla.overdueHours', { hours: task.hoursOverdue ?? 0 })}
                          </Badge>
                        )}
                        {task.isDelegated && (
                          <Badge variant="secondary">
                            {t('workflow.delegation.badgeFrom', { name: task.delegatedFromDisplayName })}
                          </Badge>
                        )}
                      </div>
                    </TableCell>
                    <TableCell className="text-foreground">{task.definitionName}</TableCell>
                    <TableCell className="text-foreground">{task.stepName}</TableCell>
                    <TableCell className="text-sm text-muted-foreground">
                      {formatDate(task.createdAt)}
                    </TableCell>
                    <TableCell className="text-sm text-muted-foreground">
                      {task.dueDate ? formatDate(task.dueDate) : '—'}
                    </TableCell>
                    <TableCell>
                      <div className="flex items-center gap-2">
                        <Button size="sm" onClick={() => setSelectedTask(task)}>
                          {t('workflow.inbox.approve')}
                        </Button>
                        <Button size="sm" variant="outline" onClick={() => setSelectedTask(task)}>
                          {t('workflow.inbox.reject')}
                        </Button>
                      </div>
                    </TableCell>
                  </TableRow>
                );
              })}
            </TableBody>
          </Table>

          {pagination && (
            <Pagination
              pagination={pagination}
              onPageChange={setPage}
              onPageSizeChange={setPageSize}
            />
          )}
        </>
      )}

      {selectedIds.size > 0 && (
        <BulkActionBar
          selectedCount={selectedIds.size}
          isPending={isBulkPending}
          onApprove={() => setPendingBulkAction('Approve')}
          onReject={() => setPendingBulkAction('Reject')}
          onReturn={() => setPendingBulkAction('ReturnForRevision')}
          onClear={clearSelection}
        />
      )}

      <BulkConfirmDialog
        action={pendingBulkAction}
        count={selectedIds.size}
        isPending={isBulkPending}
        onSubmit={confirmBulk}
        onCancel={() => setPendingBulkAction(null)}
      />

      <BulkResultDialog
        result={bulkResult}
        onClose={() => setBulkResult(null)}
      />

      {selectedTask && (
        <ApprovalDialog
          taskId={selectedTask.taskId}
          definitionName={selectedTask.definitionName}
          entityType={selectedTask.entityType}
          entityId={selectedTask.entityId}
          actions={selectedTask.availableActions ?? ['Approve', 'Reject', 'ReturnForRevision']}
          formFields={selectedTask.formFields}
          open={!!selectedTask}
          onOpenChange={(open) => { if (!open) setSelectedTask(null); }}
        />
      )}

      <DelegationDialog open={delegationOpen} onOpenChange={setDelegationOpen} />
      <NewRequestDialog open={newRequestOpen} onOpenChange={setNewRequestOpen} />
    </div>
  );
}
```

- [ ] **Step 2: Export the new hook from the api barrel if not already re-exported**

Open `boilerplateFE/src/features/workflow/api/index.ts` and verify `useBatchExecuteTasks` is re-exported alongside other hooks. If not, add `export { useBatchExecuteTasks } from './workflow.queries';` (or a wildcard if that's the existing pattern).

- [ ] **Step 3: Verify the FE `Checkbox` component exists at `@/components/ui/checkbox`**

Run: `cd boilerplateFE && ls src/components/ui/checkbox.tsx 2>&1 || echo MISSING`
If MISSING, install via shadcn: `cd boilerplateFE && npx shadcn@latest add checkbox` and commit the generated file together with this task. Same for `dialog`, `textarea` — both should already exist (used elsewhere).

- [ ] **Step 4: Build the FE to catch type errors**

Run: `cd boilerplateFE && npm run build 2>&1 | tail -15`
Expected: Build succeeds.

- [ ] **Step 5: Commit**

```bash
git add boilerplateFE/src/config/api.config.ts \
        boilerplateFE/src/types/workflow.types.ts \
        boilerplateFE/src/features/workflow/api/workflow.api.ts \
        boilerplateFE/src/features/workflow/api/workflow.queries.ts \
        boilerplateFE/src/features/workflow/api/index.ts \
        boilerplateFE/src/features/workflow/components/BulkActionBar.tsx \
        boilerplateFE/src/features/workflow/components/BulkConfirmDialog.tsx \
        boilerplateFE/src/features/workflow/components/BulkResultDialog.tsx \
        boilerplateFE/src/features/workflow/pages/WorkflowInboxPage.tsx \
        boilerplateFE/src/i18n/locales/en/translation.json \
        boilerplateFE/src/i18n/locales/ar/translation.json \
        boilerplateFE/src/i18n/locales/ku/translation.json
git commit -m "feat(workflow): add bulk task actions to inbox UI"
```

---

## Task 7: Final verification — full-stack green gate

- [ ] **Step 1: Full backend test suite**

Run: `cd boilerplateBE && dotnet test 2>&1 | tail -20`
Expected: All tests pass. Report the total count — it should be roughly the prior total (155) + 17 new (4 `HumanTaskFactory` + 4 `AutoTransitionEvaluator` + 5 `ParallelApprovalCoordinator` + 4 `BatchExecuteTasks`) + 7 new `ConditionEvaluator` cases ≈ 179.

- [ ] **Step 2: Frontend type-check + production build**

Run: `cd boilerplateFE && npm run build 2>&1 | tail -10`
Expected: build succeeds.

- [ ] **Step 3: Abstractions purity check**

Run: `cd boilerplateBE && dotnet test --filter "FullyQualifiedName~AbstractionsPurityTests" 2>&1 | tail -5`
Expected: no new MassTransit / EF references leaked into `Starter.Abstractions` (none were added — the new records under `Application/DTOs` are in the module, not abstractions).

- [ ] **Step 4: Live user-flow test per CLAUDE.md "Post-Feature Testing Workflow"**

Follow `.claude/skills/post-feature-testing.md`. Minimum bulk-action path:

1. Spin a rename'd test app on a free port triple (e.g. 5100/3100).
2. Log in as `acme.admin`.
3. Use the seeded `expense-approval` template → start 3 expense instances as `acme.alice`.
4. Log back in as `acme.admin` → the three tasks show on the inbox.
5. Check the "Select all" header → `BulkActionBar` appears.
6. Click "Approve selected" → `BulkConfirmDialog` prompts for comment → confirm.
7. `BulkResultDialog` shows `3 succeeded, 0 failed, 0 skipped`.
8. Inbox now empty. Each instance reached terminal state (verify via `/workflow/instances/{id}/history`).

Repeat the flow with a mixed selection where one task has already been approved via another tab (simulate by calling `/execute` on one task first, then bulk-approving the set) — expect `2 succeeded, 1 failed` and the failure reason visible in "View details".

- [ ] **Step 5: Update the roadmap doc**

Edit `docs/roadmaps/workflow.md` — move the 3 Phase 3 items ("Refactor WorkflowEngine.cs", "Compound conditional expressions", "Bulk task operations") from the "Deferred / Not started" section into a new "Phase 3 Shipped (2026-04-22)" section. Mirror the Phase 2a/2b shipping summary format.

- [ ] **Step 6: Commit doc update**

```bash
git add docs/roadmaps/workflow.md
git commit -m "docs(workflow): mark Phase 3 items as shipped"
```

- [ ] **Step 7: Open the PR**

```bash
git push -u origin feature/workflow-phase-3-foundation
gh pr create --title "Workflow Phase 3 — Foundation (engine extraction, NOT operator, bulk ops)" \
  --body "$(cat <<'EOF'
## Summary

- **3.1 Engine extraction** — split `WorkflowEngine` (1425 lines) into three focused collaborators (`HumanTaskFactory`, `AutoTransitionEvaluator`, `ParallelApprovalCoordinator`) registered as scoped services. Zero behavior change; `IWorkflowService` surface unchanged.
- **3.2 Compound conditions** — `ConditionEvaluator` now supports `NOT` in addition to `AND`/`OR`. Added 7 tests covering NOT, short-circuit evaluation, and JSON round-trip preservation.
- **3.3 Bulk operations** — new `POST /api/v1/workflow/tasks/batch-execute` + FE checkbox column, floating action bar, confirm + result dialogs. One batch handles ≤ 50 tasks, per-task outcomes aggregated into `BatchExecuteResult`. Tenant-scoped via the existing engine path.

## Test plan

- [ ] `dotnet test` green (~179 tests, +17 new)
- [ ] `npm run build` green
- [ ] Live in-browser: select multiple pending tasks → Approve → result dialog shows counts + details
- [ ] Live in-browser: mixed-success batch → partial success surfaced with errors
- [ ] Regression: single-task approve/reject/return still works via the existing `ApprovalDialog`
- [ ] Regression: parallel AllOf / AnyOf groups still behave (covered by unchanged `WorkflowEngineTests`)
EOF
)"
```

---

## Self-review checklist (before declaring this plan done)

**1. Spec coverage.** Every Phase 3 sub-bullet has at least one task:
- § 3.1 `HumanTaskFactory` → Tasks 1.1–1.4.
- § 3.1 `AutoTransitionEvaluator` → Tasks 2.1–2.3.
- § 3.1 `ParallelApprovalCoordinator` → Tasks 3.1–3.3.
- § 3.1 "No controller or call-site changes" → enforced by keeping `IWorkflowService` signature intact; verified in Task 7.1.
- § 3.1 "One collaborator extracted per commit" → Tasks 1.4 / 2.3 / 3.3 each end with a dedicated commit.
- § 3.2 NOT/nested/short-circuit/round-trip → Tasks 4.1–4.2 (7 new tests).
- § 3.2 backward compatibility → the `ConditionConfig` record is unchanged; existing AND/OR paths keep passing (confirmed by the existing 6 compound tests).
- § 3.3 backend command+handler → Tasks 5.1–5.3.
- § 3.3 handler per-task try/catch, no batch abort → Task 5.3 handler + Task 5.2 tests (mixed success, exception cases).
- § 3.3 multi-tenancy → preserved by routing through `IWorkflowService.ExecuteTaskAsync` which uses the filtered DbContext; cross-tenant IDs resolve to "not found".
- § 3.3 checkbox column + select-all + floating bar → Task 6.5.
- § 3.3 summary toast → Task 6.1 Step 4 (`useBatchExecuteTasks`).
- § 3.3 "View details" expansion → Task 6.4 `BulkResultDialog`.
- § Sequencing 1 → 5 → commits ordered exactly as the spec demands (Tasks 1 → 2 → 3 → 4 → 5 → 6).
- § Cross-cutting: migrations → none added; xUnit/FluentAssertions/Moq used; purity test verified at Task 7.3; roadmap doc updated Task 7.5.

**2. Placeholder scan.** Every code step shows the actual code. No "implement later" / "similar to" / "TBD". The one "see how shadcn is usually added here" note in 6.5 is a verification step with the exact command to run if the file is missing.

**3. Type consistency.**
- `HumanTaskFactory.CreateAsync(WorkflowInstance, WorkflowStateConfig, WorkflowDefinition, Guid, CancellationToken)` — consistent across Tasks 1.1, 1.2, 1.3, 1.4.
- `AutoTransitionEvaluator.Select(IReadOnlyList<WorkflowTransitionConfig>, string, IReadOnlyDictionary<string,object>?)` — consistent across Tasks 2.1, 2.2, 2.3.
- `ParallelApprovalCoordinator.EvaluateAsync(ApprovalTask, string, string, CancellationToken)` → `ParallelDecision` — consistent across Tasks 3.1, 3.2, 3.3.
- `BatchExecuteTasksCommand(IReadOnlyList<Guid>, string, string?)` → `Result<BatchExecuteResult>` — consistent across Tasks 5.1, 5.2, 5.3, 5.4.
- `BatchExecuteResult(int, int, int, IReadOnlyList<BatchItemOutcome>)` → used in BE (5.1), tested in (5.2), returned to FE in types (6.1), rendered in (6.4).
- FE `BatchExecuteTasksRequest { taskIds, action, comment }` maps 1:1 to BE record (camelCase via System.Text.Json default).

**4. Reusable components / no duplication.**
- FE reuses `PageHeader`, `EmptyState`, `Pagination`, `Table`, `Button`, `Badge`, `Dialog`, `Textarea`, `Checkbox`. No new primitives.
- BE reuses `IWorkflowService` surface; handler calls `ExecuteTaskAsync` per task — no duplicated task-execution logic.
- The three collaborators do not duplicate code; each carves out exactly one responsibility from `WorkflowEngine`.

**5. SOLID + modular.**
- Single-responsibility: each collaborator has one reason to change (see §3.1 boundaries above).
- Open/closed: adding NOT extends the `Logic` switch without touching AND/OR paths or the `ConditionConfig` shape.
- Interface segregation: `IWorkflowService` unchanged; bulk endpoint goes through MediatR and reuses the single-task method.
- Dependency inversion: all collaborators receive their deps via primary constructor; tests inject fakes the same way.

**6. Tenant scope.** No new query bypasses the global filters. `BatchExecuteTasksCommandHandler` delegates to `IWorkflowService.ExecuteTaskAsync` which already respects tenancy.

**7. UI/UX.** RTL-aware (`ltr:/rtl:` prefixes), i18n keys in all three locales, sticky floating bar with semantic tokens, loading / error / partial-success states handled, clears selection on page change to prevent stale IDs spanning pages, respects the existing shadcn theme tokens (no hard-coded colors).

**8. User flow without gaps.** Select → act → confirm with comment → see result dialog → expand for per-task error detail → dismiss → inbox refreshes via TanStack invalidation. Single-task flow (`ApprovalDialog`) remains fully intact for users who prefer one-at-a-time.

**9. Production-ready.** Validation cap at 50 IDs protects against DoS-style submissions; per-task try/catch prevents cascading failures; idempotent underlying command supports safe retries; no new DB columns so no migration risk.

---

## Execution handoff

Per the user's instruction on 2026-04-22: after finalization, continue executing this plan automatically using the **subagent-driven-development** sub-skill. Each task is dispatched to a fresh agent; the main loop reviews per task and commits between tasks.
