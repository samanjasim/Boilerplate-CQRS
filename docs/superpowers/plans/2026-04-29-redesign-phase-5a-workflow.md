# Phase 5a Workflow — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Bring all 6 workflow pages onto J4 Spectrum tokens with two earned structural changes (command-center inbox with row-level SLA pressure; sticky right-rail instance detail) and chrome polish on the designer with state-type tinting.

**Architecture:** Frontend-heavy redesign with two new BE query handlers (`GetInboxStatusCountsQuery`, `GetInstanceStatusCountsQuery`) for hero counts. No schema changes. Reuse the existing workflow route/API/query layout, `MetricCard`, `DesignerCanvas readOnly`, audit `JsonView`, and the shared table/card/status primitives before creating local components. All touched visible strings must have EN + AR + KU keys with no `defaultValue` fallback in committed UI code.

**Tech Stack:** .NET 10 / EF Core / xUnit (BE); React 19 + TypeScript 5 + Tailwind 4 + TanStack Query + ReactFlow + i18next + shadcn/ui (FE).

**Spec:** [`docs/superpowers/specs/2026-04-29-redesign-phase-5a-workflow-design.md`](../specs/2026-04-29-redesign-phase-5a-workflow-design.md)

**Branch:** `fe/phase-5-design` (already created off `origin/main`).

**Cadence:** Land commits directly on the branch. One commit per task (no per-task feature branches).

**Real entity names** (from codebase exploration; differ from spec):
- BE entity is `ApprovalTask` (not `WorkflowTask`); SLA field is `DueDate` (not `SlaDueAt`).
- `InstanceStatus` enum has `Active`, `Completed`, `Cancelled` only — no `Failed`. Spec's "Failed-or-cancelled" bucket renders as just `Cancelled`.
- Inbox endpoint authorizes with `WorkflowPermissions.ActOnTask`; instances endpoint with `WorkflowPermissions.View`.
- Existing workflow test helper is `WorkflowEngineTestFactory.CreateDb()` (not `NewDb()`).
- Existing FE translations live in `boilerplateFE/src/i18n/locales/{en,ar,ku}/translation.json` under the root `workflow` object; there are no `src/locales/workflow.json` files.
- `WorkflowInstanceDto` does not exist today; use `WorkflowInstanceSummary` unless the implementation explicitly adds a new FE type.
- There is no frontend `PERMISSIONS.System.SuperAdmin`; treat SuperAdmin as the existing product pages do (`!user?.tenantId`) or use an existing role signal if one is added later.
- There is no shared `JsonView` under `components/common`; the current reusable implementation is `boilerplateFE/src/features/audit-logs/components/JsonView.tsx` and accepts `payload`, not `value`.
- There is no shadcn `collapsible` component in `boilerplateFE/src/components/ui`; use a small local state toggle or add the primitive as its own explicit task before using it.

---

## Task 1: Foundation — semantic token additions

**Files:**
- Modify: `boilerplateFE/src/styles/index.css` (add `--state-warn-bg` / `--state-warn-fg` semantic tokens if not present, expose `--shell-header-h` if not exposed)

- [ ] **Step 1: Verify `--state-warn-*` tokens** — `grep -n "state-warn" boilerplateFE/src/styles/index.css` from repo root.
  - If present, skip Step 2.
  - If absent, proceed.

- [ ] **Step 2: Add amber pressure-chip semantic tokens** to `:root` and `.dark` blocks in `boilerplateFE/src/styles/index.css`:

```css
:root {
  /* … existing tokens … */
  --state-warn-bg: color-mix(in oklch, var(--color-amber-500) 18%, var(--background));
  --state-warn-fg: color-mix(in oklch, var(--color-amber-700) 90%, var(--foreground));
  --state-warn-border: color-mix(in oklch, var(--color-amber-500) 35%, transparent);
}
.dark {
  /* … existing tokens … */
  --state-warn-bg: color-mix(in oklch, var(--color-amber-400) 12%, var(--background));
  --state-warn-fg: color-mix(in oklch, var(--color-amber-200) 90%, var(--foreground));
  --state-warn-border: color-mix(in oklch, var(--color-amber-400) 30%, transparent);
}
```

- [ ] **Step 3: Verify `--shell-header-h` exposure** — `grep -n "shell-header-h" boilerplateFE/src/styles/index.css boilerplateFE/src/components/layout` from repo root.
  - If exposed, note the value and skip Step 4.
  - If absent, proceed.

- [ ] **Step 4: Expose shell header height** — Find the floating-glass shell's header container in `boilerplateFE/src/components/layout/MainLayout/`. Add a CSS var on the layout root:

```tsx
<div
  className="…"
  style={{ '--shell-header-h': '4rem' } as CSSProperties}
>
```

Use the actual current header height value — measure via `getBoundingClientRect` or read the existing Tailwind class. If the header is `h-16`, value is `4rem`.

- [ ] **Step 5: Type-check** — `cd boilerplateFE && npm run build` (must pass; no behavior change yet).

- [ ] **Step 6: Commit**

```bash
git add boilerplateFE/src/styles/index.css boilerplateFE/src/components/layout
git commit -m "feat(fe/styles): add state-warn semantic tokens and shell header height var

Pressure chips on the workflow inbox need amber tinting that stays
preset-aware. Shell header height becomes a CSS var so sticky right
rails (Phase 5a instance detail) can reuse the offset without
hardcoding."
```

---

## Task 2: BE — GetInboxStatusCountsQuery handler + endpoint + tests

**Files:**
- Create: `boilerplateBE/src/modules/Starter.Module.Workflow/Application/DTOs/InboxStatusCountsDto.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.Workflow/Application/Queries/GetInboxStatusCounts/GetInboxStatusCountsQuery.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.Workflow/Application/Queries/GetInboxStatusCounts/GetInboxStatusCountsQueryHandler.cs`
- Modify: `boilerplateBE/src/modules/Starter.Module.Workflow/WorkflowModule.cs` (only if `TimeProvider` is not already registered globally)
- Modify: `boilerplateBE/src/modules/Starter.Module.Workflow/Controllers/WorkflowController.cs` (add endpoint)
- Create: `boilerplateBE/tests/Starter.Api.Tests/Workflow/GetInboxStatusCountsQueryHandlerTests.cs`

- [ ] **Step 1: Write the DTO** — `boilerplateBE/src/modules/Starter.Module.Workflow/Application/DTOs/InboxStatusCountsDto.cs`:

```csharp
namespace Starter.Module.Workflow.Application.DTOs;

public sealed record InboxStatusCountsDto(int Overdue, int DueToday, int Upcoming);
```

- [ ] **Step 2: Write the query record** — `Application/Queries/GetInboxStatusCounts/GetInboxStatusCountsQuery.cs`:

```csharp
using MediatR;
using Starter.Module.Workflow.Application.DTOs;
using Starter.Shared.Results;

namespace Starter.Module.Workflow.Application.Queries.GetInboxStatusCounts;

public sealed record GetInboxStatusCountsQuery() : IRequest<Result<InboxStatusCountsDto>>;
```

- [ ] **Step 3: Write the failing handler test** — `boilerplateBE/tests/Starter.Api.Tests/Workflow/GetInboxStatusCountsQueryHandlerTests.cs`. Use the existing `WorkflowEngineTestFactory` for the in-memory DbContext:

```csharp
using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Time.Testing;
using Moq;
using Starter.Application.Common.Interfaces;
using Starter.Module.Workflow.Application.Queries.GetInboxStatusCounts;
using Starter.Module.Workflow.Domain.Entities;
using Xunit;

namespace Starter.Api.Tests.Workflow;

public sealed class GetInboxStatusCountsQueryHandlerTests
{
    [Fact]
    public async Task Buckets_PendingTasks_ByDueDate_ForCurrentUser()
    {
        var fixed_now = new DateTimeOffset(2026, 5, 1, 10, 0, 0, TimeSpan.Zero);
        var fakeTime = new FakeTimeProvider(fixed_now);
        var userId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        await using var db = WorkflowEngineTestFactory.CreateDb();

        // Three tasks for current user: 1 overdue, 1 due today, 1 upcoming
        db.ApprovalTasks.AddRange(
            ApprovalTaskTestFactory.Pending(userId, dueDate: fixed_now.AddHours(-2).UtcDateTime),
            ApprovalTaskTestFactory.Pending(userId, dueDate: fixed_now.AddHours(3).UtcDateTime),
            ApprovalTaskTestFactory.Pending(userId, dueDate: fixed_now.AddDays(2).UtcDateTime),
            // No-due-date task counts as Upcoming
            ApprovalTaskTestFactory.Pending(userId, dueDate: null),
            // Different user: should NOT count
            ApprovalTaskTestFactory.Pending(otherUserId, dueDate: fixed_now.AddHours(-1).UtcDateTime)
        );
        await db.SaveChangesAsync();

        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(x => x.UserId).Returns(userId);

        var handler = new GetInboxStatusCountsQueryHandler(db, currentUser.Object, fakeTime);
        var result = await handler.Handle(new GetInboxStatusCountsQuery(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value.Overdue);
        Assert.Equal(1, result.Value.DueToday);
        Assert.Equal(2, result.Value.Upcoming);
    }

    [Fact]
    public async Task IgnoresCompletedAndCancelledTasks()
    {
        var fixed_now = new DateTimeOffset(2026, 5, 1, 10, 0, 0, TimeSpan.Zero);
        var fakeTime = new FakeTimeProvider(fixed_now);
        var userId = Guid.NewGuid();
        await using var db = WorkflowEngineTestFactory.CreateDb();

        db.ApprovalTasks.AddRange(
            ApprovalTaskTestFactory.Completed(userId, dueDate: fixed_now.AddHours(-1).UtcDateTime),
            ApprovalTaskTestFactory.Cancelled(userId, dueDate: fixed_now.AddHours(-1).UtcDateTime)
        );
        await db.SaveChangesAsync();

        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(x => x.UserId).Returns(userId);

        var handler = new GetInboxStatusCountsQueryHandler(db, currentUser.Object, fakeTime);
        var result = await handler.Handle(new GetInboxStatusCountsQuery(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(0, result.Value.Overdue);
        Assert.Equal(0, result.Value.DueToday);
        Assert.Equal(0, result.Value.Upcoming);
    }
}
```

If `ApprovalTaskTestFactory` does not exist, create it inline in the same file as a static class with `Pending`, `Completed`, and `Cancelled` factory methods that wrap `ApprovalTask.Create(...)` and then call `.Complete()` / `.Cancel()` as needed. Use existing test files in the same folder (e.g. `BatchExecuteTasksTests.cs`) as a reference for the factory pattern.

- [ ] **Step 4: Run tests to verify they fail**

Run: `cd boilerplateBE && dotnet test --filter "FullyQualifiedName~GetInboxStatusCountsQueryHandlerTests"`
Expected: build error — handler doesn't exist yet.

- [ ] **Step 5: Write the handler** — `Application/Queries/GetInboxStatusCounts/GetInboxStatusCountsQueryHandler.cs`:

```csharp
using MediatR;
using Microsoft.EntityFrameworkCore;
using Starter.Application.Common.Interfaces;
using Starter.Module.Workflow.Application.DTOs;
using Starter.Module.Workflow.Constants;
using Starter.Module.Workflow.Domain.Enums;
using Starter.Module.Workflow.Infrastructure.Persistence;
using Starter.Shared.Results;
using TaskStatus = Starter.Module.Workflow.Domain.Enums.TaskStatus;

namespace Starter.Module.Workflow.Application.Queries.GetInboxStatusCounts;

internal sealed class GetInboxStatusCountsQueryHandler(
    WorkflowDbContext context,
    ICurrentUserService currentUser,
    TimeProvider time)
    : IRequestHandler<GetInboxStatusCountsQuery, Result<InboxStatusCountsDto>>
{
    public async Task<Result<InboxStatusCountsDto>> Handle(
        GetInboxStatusCountsQuery request,
        CancellationToken cancellationToken)
    {
        if (currentUser.UserId is null)
            return Result.Failure<InboxStatusCountsDto>(Error.Unauthorized());

        var userId = currentUser.UserId.Value;
        var now = time.GetUtcNow();
        var endOfToday = now.Date.AddDays(1);

        var pendingTasks = context.ApprovalTasks
            .AsNoTracking()
            .Where(t => t.Status == TaskStatus.Pending
                && (t.AssigneeUserId == userId || t.OriginalAssigneeUserId == userId));

        var overdue = await pendingTasks.CountAsync(
            t => t.DueDate.HasValue && t.DueDate.Value < now.UtcDateTime,
            cancellationToken);

        var dueToday = await pendingTasks.CountAsync(
            t => t.DueDate.HasValue
                && t.DueDate.Value >= now.UtcDateTime
                && t.DueDate.Value < endOfToday.UtcDateTime,
            cancellationToken);

        // Upcoming = everything pending that is not Overdue and not DueToday
        // (includes tasks with DueDate >= tomorrow AND tasks with no DueDate at all).
        var totalPending = await pendingTasks.CountAsync(cancellationToken);
        var upcoming = totalPending - overdue - dueToday;

        return Result.Success(new InboxStatusCountsDto(overdue, dueToday, upcoming));
    }
}
```

If the actual `WorkflowDbContext` namespace differs, fix the using. The exact `TaskStatus` enum lives at `Starter.Module.Workflow.Domain.Enums.TaskStatus` — verify.

- [ ] **Step 6: Verify or add `TimeProvider` DI registration** — run `rg -n "TimeProvider.System|AddSingleton\\(TimeProvider" boilerplateBE/src`. If no registration exists, add this in `WorkflowModule.ConfigureServices` before scoped workflow services:

```csharp
services.AddSingleton(TimeProvider.System);
```

This is required because `GetInboxStatusCountsQueryHandler` constructor-injects `TimeProvider`. Existing workflow event handlers also depend on it, so this is a production-readiness fix, not feature creep.

- [ ] **Step 7: Add controller endpoint** — modify `boilerplateBE/src/modules/Starter.Module.Workflow/Controllers/WorkflowController.cs`. Add the using at the top:

```csharp
using Starter.Module.Workflow.Application.Queries.GetInboxStatusCounts;
```

Add the endpoint after the existing `GetPendingTaskCount` method (around line 205):

```csharp
[HttpGet("tasks/status-counts")]
[Authorize(Policy = WorkflowPermissions.ActOnTask)]
[ProducesResponseType(typeof(ApiResponse<InboxStatusCountsDto>), StatusCodes.Status200OK)]
public async Task<IActionResult> GetInboxStatusCounts(CancellationToken ct = default)
{
    var result = await Mediator.Send(new GetInboxStatusCountsQuery(), ct);
    return HandleResult(result);
}
```

- [ ] **Step 8: Run tests and verify they pass**

Run: `cd boilerplateBE && dotnet test --filter "FullyQualifiedName~GetInboxStatusCountsQueryHandlerTests"`
Expected: 2 passing tests.

- [ ] **Step 9: Build full solution to ensure controller change compiles**

Run: `cd boilerplateBE && dotnet build`
Expected: 0 errors.

- [ ] **Step 10: Commit**

```bash
git add boilerplateBE/src/modules/Starter.Module.Workflow boilerplateBE/tests/Starter.Api.Tests/Workflow/GetInboxStatusCountsQueryHandlerTests.cs
git commit -m "feat(workflow): add inbox status counts endpoint

Returns Overdue/DueToday/Upcoming bucket counts for the current
user's pending approval tasks. Used by the redesigned WorkflowInboxPage
hero strip. Tenant-scoped by the WorkflowDbContext global filter."
```

---

## Task 3: BE — GetInstanceStatusCountsQuery handler + endpoint + tests

**Files:**
- Create: `boilerplateBE/src/modules/Starter.Module.Workflow/Application/DTOs/InstanceStatusCountsDto.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.Workflow/Application/Queries/GetInstanceStatusCounts/GetInstanceStatusCountsQuery.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.Workflow/Application/Queries/GetInstanceStatusCounts/GetInstanceStatusCountsQueryHandler.cs`
- Modify: `boilerplateBE/src/modules/Starter.Module.Workflow/Controllers/WorkflowController.cs`
- Create: `boilerplateBE/tests/Starter.Api.Tests/Workflow/GetInstanceStatusCountsQueryHandlerTests.cs`

- [ ] **Step 1: Write the DTO** — `boilerplateBE/src/modules/Starter.Module.Workflow/Application/DTOs/InstanceStatusCountsDto.cs`:

```csharp
namespace Starter.Module.Workflow.Application.DTOs;

public sealed record InstanceStatusCountsDto(
    int Active,
    int Awaiting,
    int Completed,
    int Cancelled);
```

- [ ] **Step 2: Write the query record** — `Application/Queries/GetInstanceStatusCounts/GetInstanceStatusCountsQuery.cs`. Mirror the existing `GetWorkflowInstancesQuery` parameters so the FE can reuse the same filter object:

```csharp
using MediatR;
using Starter.Module.Workflow.Application.DTOs;
using Starter.Shared.Results;

namespace Starter.Module.Workflow.Application.Queries.GetInstanceStatusCounts;

public sealed record GetInstanceStatusCountsQuery(
    Guid? StartedByUserId = null,
    string? EntityType = null,
    string? State = null
) : IRequest<Result<InstanceStatusCountsDto>>;
```

Note: `TenantId` is not exposed as a parameter because tenant scoping comes from the `WorkflowDbContext` global query filter; super-admin's tenant filter on the FE is for a different purpose (cross-tenant aggregate views) which we intentionally are not building yet. Do not include `Status` in this count query: the cards are themselves the status distribution. They should honor entity/user/state filters, but not the currently selected status filter.

- [ ] **Step 3: Write the failing handler test** — `boilerplateBE/tests/Starter.Api.Tests/Workflow/GetInstanceStatusCountsQueryHandlerTests.cs`:

```csharp
using System;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using Starter.Application.Common.Interfaces;
using Starter.Module.Workflow.Application.Queries.GetInstanceStatusCounts;
using Starter.Module.Workflow.Domain.Enums;
using Xunit;

namespace Starter.Api.Tests.Workflow;

public sealed class GetInstanceStatusCountsQueryHandlerTests
{
    [Fact]
    public async Task Buckets_Active_Awaiting_Completed_Cancelled()
    {
        var userId = Guid.NewGuid();
        await using var db = WorkflowEngineTestFactory.CreateDb();

        // Active without pending task
        var activeNoTask = WorkflowInstanceTestFactory.Create(
            startedByUserId: userId, status: InstanceStatus.Active);
        // Active with pending task (Awaiting)
        var activeWithTask = WorkflowInstanceTestFactory.Create(
            startedByUserId: userId, status: InstanceStatus.Active);
        var pending = ApprovalTaskTestFactory.Pending(
            assigneeUserId: userId, instanceId: activeWithTask.Id);
        // Completed
        var completed = WorkflowInstanceTestFactory.Create(
            startedByUserId: userId, status: InstanceStatus.Completed);
        // Cancelled
        var cancelled = WorkflowInstanceTestFactory.Create(
            startedByUserId: userId, status: InstanceStatus.Cancelled);

        db.WorkflowInstances.AddRange(activeNoTask, activeWithTask, completed, cancelled);
        db.ApprovalTasks.Add(pending);
        await db.SaveChangesAsync();

        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(x => x.UserId).Returns(userId);

        var handler = new GetInstanceStatusCountsQueryHandler(db, currentUser.Object);
        var result = await handler.Handle(new GetInstanceStatusCountsQuery(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value.Active);
        Assert.Equal(1, result.Value.Awaiting);
        Assert.Equal(1, result.Value.Completed);
        Assert.Equal(1, result.Value.Cancelled);
    }

    [Fact]
    public async Task StartedByUserId_FiltersToCurrentUserStartedInstances()
    {
        var userId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        await using var db = WorkflowEngineTestFactory.CreateDb();

        db.WorkflowInstances.AddRange(
            WorkflowInstanceTestFactory.Create(startedByUserId: userId, status: InstanceStatus.Active),
            WorkflowInstanceTestFactory.Create(startedByUserId: userId, status: InstanceStatus.Completed),
            WorkflowInstanceTestFactory.Create(startedByUserId: otherUserId, status: InstanceStatus.Active),
            WorkflowInstanceTestFactory.Create(startedByUserId: otherUserId, status: InstanceStatus.Cancelled)
        );
        await db.SaveChangesAsync();

        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(x => x.UserId).Returns(userId);

        var handler = new GetInstanceStatusCountsQueryHandler(db, currentUser.Object);
        var result = await handler.Handle(
            new GetInstanceStatusCountsQuery(StartedByUserId: userId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value.Active);
        Assert.Equal(1, result.Value.Completed);
        Assert.Equal(0, result.Value.Awaiting);
        Assert.Equal(0, result.Value.Cancelled);
    }

    [Fact]
    public async Task EntityTypeFilter_ScopesCountsToOneEntityType()
    {
        var userId = Guid.NewGuid();
        await using var db = WorkflowEngineTestFactory.CreateDb();

        db.WorkflowInstances.AddRange(
            WorkflowInstanceTestFactory.Create(startedByUserId: userId, entityType: "Invoice", status: InstanceStatus.Active),
            WorkflowInstanceTestFactory.Create(startedByUserId: userId, entityType: "Invoice", status: InstanceStatus.Completed),
            WorkflowInstanceTestFactory.Create(startedByUserId: userId, entityType: "Product", status: InstanceStatus.Active)
        );
        await db.SaveChangesAsync();

        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(x => x.UserId).Returns(userId);

        var handler = new GetInstanceStatusCountsQueryHandler(db, currentUser.Object);
        var result = await handler.Handle(
            new GetInstanceStatusCountsQuery(EntityType: "Invoice"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value.Active);
        Assert.Equal(1, result.Value.Completed);
    }
}
```

If `WorkflowInstanceTestFactory` does not exist, create it as a static class colocated with the existing test files. Use the same shape as `ApprovalTaskTestFactory` from Task 2.

- [ ] **Step 4: Run tests and verify they fail**

Run: `cd boilerplateBE && dotnet test --filter "FullyQualifiedName~GetInstanceStatusCountsQueryHandlerTests"`
Expected: build error — handler does not exist yet.

- [ ] **Step 5: Write the handler** — `Application/Queries/GetInstanceStatusCounts/GetInstanceStatusCountsQueryHandler.cs`:

```csharp
using MediatR;
using Microsoft.EntityFrameworkCore;
using Starter.Application.Common.Interfaces;
using Starter.Module.Workflow.Application.DTOs;
using Starter.Module.Workflow.Infrastructure.Persistence;
using Starter.Shared.Results;
using TaskStatus = Starter.Module.Workflow.Domain.Enums.TaskStatus;

namespace Starter.Module.Workflow.Application.Queries.GetInstanceStatusCounts;

internal sealed class GetInstanceStatusCountsQueryHandler(
    WorkflowDbContext context,
    ICurrentUserService currentUser)
    : IRequestHandler<GetInstanceStatusCountsQuery, Result<InstanceStatusCountsDto>>
{
    public async Task<Result<InstanceStatusCountsDto>> Handle(
        GetInstanceStatusCountsQuery request,
        CancellationToken cancellationToken)
    {
        var instances = context.WorkflowInstances.AsNoTracking().AsQueryable();

        var startedByUserId = request.StartedByUserId;
        if (!currentUser.HasPermission(WorkflowPermissions.ViewAllTasks))
        {
            if (currentUser.UserId is null)
                return Result.Failure<InstanceStatusCountsDto>(Error.Unauthorized());
            startedByUserId = currentUser.UserId;
        }

        if (startedByUserId is { } uid)
            instances = instances.Where(i => i.StartedByUserId == uid);
        if (!string.IsNullOrEmpty(request.EntityType))
            instances = instances.Where(i => i.EntityType == request.EntityType);
        if (!string.IsNullOrEmpty(request.State))
            instances = instances.Where(i => i.CurrentState == request.State);

        var statusGroups = await instances
            .GroupBy(i => i.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var dict = statusGroups.ToDictionary(x => x.Status, x => x.Count);

        var totalActive = dict.GetValueOrDefault(InstanceStatus.Active);
        var awaitingInstanceIds = await instances
            .Where(i => i.Status == InstanceStatus.Active)
            .Where(i => context.ApprovalTasks.Any(
                t => t.InstanceId == i.Id && t.Status == TaskStatus.Pending))
            .Select(i => i.Id)
            .Distinct()
            .CountAsync(cancellationToken);

        var dto = new InstanceStatusCountsDto(
            Active: totalActive - awaitingInstanceIds,
            Awaiting: awaitingInstanceIds,
            Completed: dict.GetValueOrDefault(InstanceStatus.Completed),
            Cancelled: dict.GetValueOrDefault(InstanceStatus.Cancelled));

        return Result.Success(dto);
    }
}
```

Verify the navigation property name is `WorkflowInstances` on `WorkflowDbContext` and not `Instances` or similar. Same for `ApprovalTasks`. Look at `WorkflowDbContext.cs` to confirm `DbSet` names.

- [ ] **Step 6: Add controller endpoint** — modify the same `WorkflowController.cs` from Task 2. Add using:

```csharp
using Starter.Module.Workflow.Application.Queries.GetInstanceStatusCounts;
```

Add the endpoint after `GetWorkflowInstances` (search for `[HttpGet("instances")]` to locate it). Place the new endpoint immediately after to keep instance routes grouped:

```csharp
[HttpGet("instances/status-counts")]
[Authorize(Policy = WorkflowPermissions.View)]
[ProducesResponseType(typeof(ApiResponse<InstanceStatusCountsDto>), StatusCodes.Status200OK)]
public async Task<IActionResult> GetInstanceStatusCounts(
    [FromQuery] Guid? startedByUserId = null,
    [FromQuery] string? entityType = null,
    [FromQuery] string? state = null,
    CancellationToken ct = default)
{
    var result = await Mediator.Send(
        new GetInstanceStatusCountsQuery(startedByUserId, entityType, state), ct);
    return HandleResult(result);
}
```

- [ ] **Step 7: Run tests and verify they pass**

Run: `cd boilerplateBE && dotnet test --filter "FullyQualifiedName~GetInstanceStatusCountsQueryHandlerTests"`
Expected: 3 passing tests.

- [ ] **Step 8: Build full solution**

Run: `cd boilerplateBE && dotnet build`
Expected: 0 errors.

- [ ] **Step 9: Commit**

```bash
git add boilerplateBE/src/modules/Starter.Module.Workflow boilerplateBE/tests/Starter.Api.Tests/Workflow/GetInstanceStatusCountsQueryHandlerTests.cs
git commit -m "feat(workflow): add instance status counts endpoint

Returns Active/Awaiting/Completed/Cancelled counts honoring the
existing list-page scoping filters (startedByUserId, entityType, state).
Awaiting is derived from Active + has-pending-task to avoid an
enum split. Used by the redesigned WorkflowInstancesPage hero strip."
```

---

## Task 4: FE — API endpoints, types, query hooks

**Files:**
- Modify: `boilerplateFE/src/config/api.config.ts` (add 2 endpoints)
- Modify: `boilerplateFE/src/lib/query/keys.ts` (add stable status-count query keys)
- Modify: `boilerplateFE/src/types/workflow.types.ts` (add 2 types)
- Modify: `boilerplateFE/src/features/workflow/api/workflow.api.ts` (add 2 methods)
- Modify: `boilerplateFE/src/features/workflow/api/workflow.queries.ts` (add 2 hooks)

- [ ] **Step 1: Add endpoint constants** — modify `boilerplateFE/src/config/api.config.ts` `WORKFLOW` object. Add inside the existing block (around line 219):

```ts
WORKFLOW: {
  // … existing fields …
  TASKS_STATUS_COUNTS: '/workflow/tasks/status-counts',
  INSTANCES_STATUS_COUNTS: '/workflow/instances/status-counts',
  // … remaining fields …
},
```

- [ ] **Step 2: Add types** — modify `boilerplateFE/src/types/workflow.types.ts`. Append:

```ts
export interface InboxStatusCounts {
  overdue: number;
  dueToday: number;
  upcoming: number;
}

export interface InstanceStatusCounts {
  active: number;
  awaiting: number;
  completed: number;
  cancelled: number;
}
```

- [ ] **Step 3: Add stable query keys** — modify `boilerplateFE/src/lib/query/keys.ts` inside the `workflow` namespace:

```ts
instances: {
  all: ['workflow', 'instances'] as const,
  list: (params?: Record<string, unknown>) => ['workflow', 'instances', 'list', params] as const,
  statusCounts: (params?: Record<string, unknown>) =>
    ['workflow', 'instances', 'status-counts', params ?? {}] as const,
  byId: (instanceId: string | undefined) => ['workflow', 'instances', 'byId', instanceId] as const,
  status: (entityType: string, entityId: string) =>
    ['workflow', 'instances', 'status', entityType, entityId] as const,
  history: (instanceId: string) => ['workflow', 'instances', 'history', instanceId] as const,
},
tasks: {
  all: ['workflow', 'tasks'] as const,
  list: (params?: Record<string, unknown>) => ['workflow', 'tasks', 'list', params] as const,
  count: () => ['workflow', 'tasks', 'count'] as const,
  statusCounts: () => ['workflow', 'tasks', 'status-counts'] as const,
},
```

Preserve the existing keys and add only `statusCounts`.

- [ ] **Step 4: Add API methods** — modify `boilerplateFE/src/features/workflow/api/workflow.api.ts`. Add the import at the top:

```ts
import type {
  // … existing imports …
  InboxStatusCounts,
  InstanceStatusCounts,
} from '@/types/workflow.types';
```

Add the methods to the exported object:

```ts
getInboxStatusCounts: () =>
  apiClient
    .get<ApiResponse<InboxStatusCounts>>(API_ENDPOINTS.WORKFLOW.TASKS_STATUS_COUNTS)
    .then((r) => r.data.data),

getInstanceStatusCounts: (params?: {
  startedByUserId?: string;
  entityType?: string;
  state?: string;
}) =>
  apiClient
    .get<ApiResponse<InstanceStatusCounts>>(API_ENDPOINTS.WORKFLOW.INSTANCES_STATUS_COUNTS, { params })
    .then((r) => r.data.data),
```

- [ ] **Step 5: Add query hooks** — modify `boilerplateFE/src/features/workflow/api/workflow.queries.ts`. Add hooks near the other workflow query hooks:

```ts
export function useInboxStatusCounts() {
  return useQuery({
    queryKey: queryKeys.workflow.tasks.statusCounts(),
    queryFn: () => workflowApi.getInboxStatusCounts(),
    staleTime: 30_000,
  });
}

export function useInstanceStatusCounts(params?: {
  startedByUserId?: string;
  entityType?: string;
  state?: string;
}) {
  return useQuery({
    queryKey: queryKeys.workflow.instances.statusCounts(params as Record<string, unknown> | undefined),
    queryFn: () => workflowApi.getInstanceStatusCounts(params),
    staleTime: 30_000,
  });
}
```

- [ ] **Step 6: Invalidate status-count queries from existing mutations** — in `useExecuteTask`, `useBatchExecuteTasks`, `useStartWorkflow`, `useCancelWorkflow`, and `useTransitionWorkflow`, add invalidations for the new keys alongside the existing `tasks.all` / `instances.all` invalidations:

```ts
queryClient.invalidateQueries({ queryKey: queryKeys.workflow.tasks.statusCounts() });
queryClient.invalidateQueries({ queryKey: queryKeys.workflow.instances.all });
```

For instance counts, invalidating `queryKeys.workflow.instances.all` is enough if the status-count key stays under that prefix; for clarity add a comment or explicit invalidation if TanStack matching is narrowed later.

- [ ] **Step 7: Type-check**

Run: `cd boilerplateFE && npm run build`
Expected: 0 errors.

- [ ] **Step 8: Commit**

```bash
git add boilerplateFE/src/config/api.config.ts boilerplateFE/src/lib/query/keys.ts boilerplateFE/src/types/workflow.types.ts boilerplateFE/src/features/workflow/api
git commit -m "feat(fe/workflow): add status-counts API client and hooks

Wires the two new BE endpoints into the workflow API client +
TanStack Query hooks. 30-second staleTime matches the Phase 4
Billing precedent for status-count hero strips."
```

---

## Task 5: FE — i18n keys for the cluster

**Files:**
- Modify: `boilerplateFE/src/i18n/locales/en/translation.json`
- Modify: `boilerplateFE/src/i18n/locales/ar/translation.json`
- Modify: `boilerplateFE/src/i18n/locales/ku/translation.json`

- [ ] **Step 1: Locate the existing `workflow` object** — `rg -n '"workflow": \\{' boilerplateFE/src/i18n/locales/*/translation.json` from repo root. Add keys into the existing nested objects; do not create separate namespace files.

The snippets below are shown as `jsonc` only so existing-key comments can be explanatory. The actual locale files are strict JSON: do not paste comments or trailing commas.

- [ ] **Step 2: Add EN keys** — append into the existing `workflow` namespace:

```jsonc
{
  "workflow": {
    // … existing keys …
    "inbox": {
      // … existing keys …
      "statusCounts": {
        "overdue": "Overdue",
        "overdueEyebrow": "Past their due date",
        "dueToday": "Due today",
        "dueTodayEyebrow": "Triage these first",
        "upcoming": "Upcoming",
        "upcomingEyebrow": "On track"
      },
      "sla": {
        "header": "SLA",
        "dueIn": "Due in {{relative}}",
        "overdue": "{{relative}} overdue",
        "onTrack": "On track",
        "noSla": "No SLA"
      },
      "actOn": "Act",
      "raised": "Raised",
      "priority": {
        "high": "High priority",
        "medium": "Medium priority",
        "low": "Low priority"
      }
    },
    "instances": {
      // … existing keys …
      "statusCounts": {
        "active": "Active",
        "activeEyebrow": "In flight",
        "awaiting": "Awaiting action",
        "awaitingEyebrow": "Pending approver",
        "completed": "Completed",
        "completedEyebrow": "Finished",
        "cancelled": "Cancelled",
        "cancelledEyebrow": "Stopped or rolled back"
      }
    },
    "definitions": {
      // … existing keys …
      "statusValue": {
        "active": "Active",
        "inactive": "Inactive"
      }
    },
    "detail": {
      // … existing keys …
      "metadata": {
        "copy": "Copy",
        "copied": "Copied",
        "instanceId": "Instance ID",
        "entityId": "Entity ID",
        "definitionLink": "View definition",
        "tenantId": "Tenant"
      },
      "pendingActionTitle": "Action required"
    },
    "designer": {
      // … existing keys …
      "stateType": {
        "initial": "Initial state",
        "humanTask": "Human task",
        "final": "Final state",
        "other": "State"
      }
    }
  }
}
```

- [ ] **Step 3: Add AR keys** (Arabic) — same structure. Use translations:

```jsonc
{
  "workflow": {
    "inbox": {
      "statusCounts": {
        "overdue": "متأخرة",
        "overdueEyebrow": "تجاوزت موعد الاستحقاق",
        "dueToday": "مستحقة اليوم",
        "dueTodayEyebrow": "ابدأ بمعالجتها أولاً",
        "upcoming": "قادمة",
        "upcomingEyebrow": "ضمن الجدول الزمني"
      },
      "sla": {
        "header": "اتفاقية الخدمة",
        "dueIn": "تستحق خلال {{relative}}",
        "overdue": "متأخرة بـ {{relative}}",
        "onTrack": "ضمن الجدول",
        "noSla": "بدون موعد محدد"
      },
      "actOn": "إجراء",
      "raised": "أُنشئ",
      "priority": {
        "high": "أولوية عالية",
        "medium": "أولوية متوسطة",
        "low": "أولوية منخفضة"
      }
    },
    "instances": {
      "statusCounts": {
        "active": "نشطة",
        "activeEyebrow": "قيد التنفيذ",
        "awaiting": "بانتظار إجراء",
        "awaitingEyebrow": "بانتظار الموافقة",
        "completed": "مكتملة",
        "completedEyebrow": "منتهية",
        "cancelled": "ملغاة",
        "cancelledEyebrow": "متوقفة أو مُلغاة"
      }
    },
    "definitions": {
      "statusValue": {
        "active": "نشطة",
        "inactive": "غير نشطة"
      }
    },
    "detail": {
      "metadata": {
        "copy": "نسخ",
        "copied": "تم النسخ",
        "instanceId": "معرّف النسخة",
        "entityId": "معرّف العنصر",
        "definitionLink": "عرض التعريف",
        "tenantId": "المستأجر"
      },
      "pendingActionTitle": "إجراء مطلوب"
    },
    "designer": {
      "stateType": {
        "initial": "حالة بدء",
        "humanTask": "مهمة بشرية",
        "final": "حالة نهائية",
        "other": "حالة"
      }
    }
  }
}
```

- [ ] **Step 4: Add KU keys** (Kurdish Sorani) — same structure:

```jsonc
{
  "workflow": {
    "inbox": {
      "statusCounts": {
        "overdue": "بەسەرچوو",
        "overdueEyebrow": "لە کاتی دیاریکراو تێپەڕیوە",
        "dueToday": "ئەمڕۆ کۆتایی دێت",
        "dueTodayEyebrow": "سەرەتا ئەمانە ئەنجام بدە",
        "upcoming": "داهاتوو",
        "upcomingEyebrow": "لەسەر ڕێگا"
      },
      "sla": {
        "header": "SLA",
        "dueIn": "کۆتایی دێت لە {{relative}}",
        "overdue": "{{relative}} دواکەوتووە",
        "onTrack": "لەسەر ڕێگا",
        "noSla": "بێ کات"
      },
      "actOn": "کردار",
      "raised": "دروستکراوە",
      "priority": {
        "high": "گرنگیی بەرز",
        "medium": "گرنگیی مامناوەند",
        "low": "گرنگیی نزم"
      }
    },
    "instances": {
      "statusCounts": {
        "active": "چالاک",
        "activeEyebrow": "لە جێبەجێکردندا",
        "awaiting": "چاوەڕێی کردار",
        "awaitingEyebrow": "چاوەڕێی پەسەندکردن",
        "completed": "تەواوبووە",
        "completedEyebrow": "کۆتایی هاتووە",
        "cancelled": "هەڵوەشێنراوە",
        "cancelledEyebrow": "ڕاگیراوە یان گەڕێنراوەتەوە"
      }
    },
    "definitions": {
      "statusValue": {
        "active": "چالاک",
        "inactive": "ناچالاک"
      }
    },
    "detail": {
      "metadata": {
        "copy": "لەبەرگرتنەوە",
        "copied": "لەبەرگیرایەوە",
        "instanceId": "ناسنامەی نموونە",
        "entityId": "ناسنامەی ئەنتیتی",
        "definitionLink": "بینینی پێناسە",
        "tenantId": "بەکارهێنەر"
      },
      "pendingActionTitle": "کرداری پێویست"
    },
    "designer": {
      "stateType": {
        "initial": "دۆخی سەرەتا",
        "humanTask": "ئەرکی مرۆیی",
        "final": "دۆخی کۆتایی",
        "other": "دۆخ"
      }
    }
  }
}
```

- [ ] **Step 5: Run i18n verification**

Run: `cd boilerplateFE && npm run build`
Expected: 0 errors. (If there's an i18n key consistency check script — `npm run check:i18n` or similar — run that too. Check `package.json` scripts.)

- [ ] **Step 6: Commit**

```bash
git add boilerplateFE/src/i18n/locales/en/translation.json \
        boilerplateFE/src/i18n/locales/ar/translation.json \
        boilerplateFE/src/i18n/locales/ku/translation.json
git commit -m "feat(fe/workflow): add Phase 5a translation keys (EN, AR, KU)

~28 new keys covering inbox status counts, SLA pressure labels,
priority labels, instance status counts, definition status pill,
instance metadata rail copy, designer state-type tooltips. All
three locales land inline (Phase 3/4 cadence)."
```

---

## Task 6: FE — InboxStatusHero component

**Files:**
- Create: `boilerplateFE/src/features/workflow/components/InboxStatusHero.tsx`

- [ ] **Step 1: Create the component** — `boilerplateFE/src/features/workflow/components/InboxStatusHero.tsx`:

```tsx
import { useTranslation } from 'react-i18next';
import { MetricCard } from '@/components/common';
import { cn } from '@/lib/utils';
import { useInboxStatusCounts } from '../api/workflow.queries';

export function InboxStatusHero() {
  const { t } = useTranslation();
  const { data, isLoading } = useInboxStatusCounts();

  if (isLoading || !data) {
    return null;
  }

  const total = data.overdue + data.dueToday + data.upcoming;
  if (total === 0) {
    // Defer to the existing inbox EmptyState; don't render an empty hero.
    return null;
  }

  const showOverdue = data.overdue > 0;
  const showDueToday = data.dueToday > 0;
  const showUpcoming = data.upcoming > 0;

  const visibleCount = [showOverdue, showDueToday, showUpcoming].filter(Boolean).length;

  return (
    <div
      className={cn(
        'mb-6 grid gap-4',
        visibleCount === 1 && 'sm:grid-cols-1',
        visibleCount === 2 && 'sm:grid-cols-2',
        visibleCount === 3 && 'sm:grid-cols-2 lg:grid-cols-3',
      )}
    >
      {showOverdue && (
        <MetricCard
          label={t('workflow.inbox.statusCounts.overdue')}
          eyebrow={t('workflow.inbox.statusCounts.overdueEyebrow')}
          value={data.overdue}
          tone="destructive"
          emphasis={data.overdue > 0}
        />
      )}
      {showDueToday && (
        <MetricCard
          label={t('workflow.inbox.statusCounts.dueToday')}
          eyebrow={t('workflow.inbox.statusCounts.dueTodayEyebrow')}
          value={data.dueToday}
          tone="active"
          emphasis={data.dueToday > 0}
        />
      )}
      {showUpcoming && (
        <MetricCard
          label={t('workflow.inbox.statusCounts.upcoming')}
          eyebrow={t('workflow.inbox.statusCounts.upcomingEyebrow')}
          value={data.upcoming}
        />
      )}
    </div>
  );
}
```

- [ ] **Step 2: Type-check**

Run: `cd boilerplateFE && npm run build`
Expected: 0 errors.

- [ ] **Step 3: Commit**

```bash
git add boilerplateFE/src/features/workflow/components/InboxStatusHero.tsx
git commit -m "feat(fe/workflow): add InboxStatusHero component

Three-card hero with collapse-when-zero behavior. Mirrors
SubscriptionStatusHero (Phase 4 Billing). When all counts are
zero, hides entirely and defers to the existing EmptyState."
```

---

## Task 7: FE — InboxTaskRow component (per-row SLA pressure)

**Files:**
- Create: `boilerplateFE/src/features/workflow/components/InboxTaskRow.tsx`
- Create: `boilerplateFE/src/features/workflow/utils/sla-pressure.ts`

- [ ] **Step 1: Create SLA pressure helper** — `boilerplateFE/src/features/workflow/utils/sla-pressure.ts`:

```ts
import { formatDistanceToNowStrict } from 'date-fns';
import type { PendingTaskSummary } from '@/types/workflow.types';

export type SlaPressure = 'overdue' | 'dueToday' | 'onTrack' | 'noSla';

export interface SlaState {
  pressure: SlaPressure;
  /** Display string like "due in 2 hours" or "3 days overdue". null if no SLA. */
  label: string | null;
  /** Bar fill percent 0..100. null if no SLA. */
  fillPercent: number | null;
  /** Priority bucket derived from pressure (no `priority` field on the BE today). */
  priority: 'high' | 'medium' | 'low';
}

export function deriveSlaState(
  task: PendingTaskSummary,
  now: Date = new Date(),
): SlaState {
  if (!task.dueDate) {
    return { pressure: 'noSla', label: null, fillPercent: null, priority: 'low' };
  }

  const due = new Date(task.dueDate);
  const created = new Date(task.createdAt);
  const totalWindow = due.getTime() - created.getTime();
  const elapsed = now.getTime() - created.getTime();
  const fillPercent = totalWindow > 0
    ? Math.max(0, Math.min(100, (elapsed / totalWindow) * 100))
    : 100;

  const isOverdue = task.isOverdue ?? due.getTime() < now.getTime();
  const endOfToday = new Date(now);
  endOfToday.setHours(23, 59, 59, 999);
  const isDueToday = !isOverdue && due.getTime() <= endOfToday.getTime();

  if (isOverdue) {
    return {
      pressure: 'overdue',
      label: formatDistanceToNowStrict(due, { addSuffix: false }),
      fillPercent,
      priority: 'high',
    };
  }
  if (isDueToday) {
    return {
      pressure: 'dueToday',
      label: formatDistanceToNowStrict(due, { addSuffix: false }),
      fillPercent,
      priority: 'medium',
    };
  }
  return {
    pressure: 'onTrack',
    label: formatDistanceToNowStrict(due, { addSuffix: false }),
    fillPercent,
    priority: 'low',
  };
}
```

- [ ] **Step 2: Create the row component** — `boilerplateFE/src/features/workflow/components/InboxTaskRow.tsx`:

```tsx
import { useTranslation } from 'react-i18next';
import { AlertCircle, Clock } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Checkbox } from '@/components/ui/checkbox';
import { TableCell, TableRow } from '@/components/ui/table';
import { cn } from '@/lib/utils';
import { formatDate } from '@/utils/format';
import type { PendingTaskSummary } from '@/types/workflow.types';
import { deriveSlaState, type SlaPressure } from '../utils/sla-pressure';

interface InboxTaskRowProps {
  task: PendingTaskSummary;
  selected: boolean;
  bulkEligible: boolean;
  onToggleSelect: (id: string) => void;
  onAct: (task: PendingTaskSummary) => void;
}

const PRIORITY_DOT: Record<SlaPressure, string> = {
  overdue: 'bg-destructive',
  dueToday: 'bg-[var(--state-warn-fg)]',
  onTrack: 'bg-muted-foreground/40',
  noSla: 'bg-muted-foreground/20',
};

const PRESSURE_CHIP: Record<SlaPressure, string> = {
  overdue: 'bg-destructive/10 text-destructive border-destructive/30',
  dueToday: 'bg-[var(--state-warn-bg)] text-[var(--state-warn-fg)] border-[var(--state-warn-border)]',
  onTrack: 'bg-muted/50 text-muted-foreground border-border',
  noSla: 'bg-muted/30 text-muted-foreground/70 border-border',
};

const SLA_BAR_FILL: Record<SlaPressure, string> = {
  overdue: 'bg-destructive',
  dueToday: 'bg-[var(--state-warn-fg)]',
  onTrack: 'bg-emerald-500',
  noSla: 'bg-muted-foreground/20',
};

export function InboxTaskRow({
  task,
  selected,
  bulkEligible,
  onToggleSelect,
  onAct,
}: InboxTaskRowProps) {
  const { t } = useTranslation();
  const sla = deriveSlaState(task);

  const pressureLabel = sla.label
    ? sla.pressure === 'overdue'
      ? t('workflow.inbox.sla.overdue', { relative: sla.label })
      : t('workflow.inbox.sla.dueIn', { relative: sla.label })
    : t('workflow.inbox.sla.noSla');

  return (
    <TableRow data-pressure={sla.pressure}>
      <TableCell className="w-[40px]">
        <Checkbox
          checked={selected}
          disabled={!bulkEligible}
          onCheckedChange={() => onToggleSelect(task.taskId)}
          aria-label={t('workflow.inbox.select')}
        />
      </TableCell>
      <TableCell>
        <div className="flex items-start gap-2">
          <span
            className={cn('mt-1.5 h-2 w-2 rounded-full shrink-0', PRIORITY_DOT[sla.pressure])}
            aria-hidden
          />
          <div className="min-w-0 flex-1 space-y-1.5">
            <div className="flex items-center gap-2 flex-wrap">
              <span className="font-medium text-foreground truncate">
                {task.entityDisplayName ?? task.entityId.slice(0, 8) + '…'}
              </span>
              <Badge
                variant="outline"
                className="text-[var(--tinted-fg)] border-[var(--active-border)]"
              >
                {task.definitionName}
              </Badge>
            </div>
            <div className="text-xs text-muted-foreground">
              {task.stepName}
            </div>
            {sla.fillPercent !== null && (
              <div className="h-1 w-full overflow-hidden rounded-full bg-muted">
                <div
                  className={cn('h-full transition-all', SLA_BAR_FILL[sla.pressure])}
                  style={{ width: `${sla.fillPercent}%` }}
                />
              </div>
            )}
          </div>
        </div>
      </TableCell>
      <TableCell className="w-[180px]">
        <span
          className={cn(
            'inline-flex items-center gap-1.5 rounded-full border px-2 py-0.5 text-xs',
            PRESSURE_CHIP[sla.pressure],
          )}
        >
          {sla.pressure === 'overdue' ? (
            <AlertCircle className="h-3 w-3" />
          ) : (
            <Clock className="h-3 w-3" />
          )}
          {pressureLabel}
        </span>
      </TableCell>
      <TableCell className="w-[160px] text-xs text-muted-foreground">
        {formatDate(task.createdAt)}
      </TableCell>
      <TableCell className="w-[100px] text-end">
        <Button size="sm" onClick={() => onAct(task)}>
          {t('workflow.inbox.actOn')}
        </Button>
      </TableCell>
    </TableRow>
  );
}
```

- [ ] **Step 3: Type-check**

Run: `cd boilerplateFE && npm run build`
Expected: 0 errors.

- [ ] **Step 4: Commit**

```bash
git add boilerplateFE/src/features/workflow/components/InboxTaskRow.tsx boilerplateFE/src/features/workflow/utils/sla-pressure.ts
git commit -m "feat(fe/workflow): add InboxTaskRow with SLA pressure signaling

Per-row priority dot, definition chip (tinted), SLA progress bar,
and pressure chip (overdue/due-today/on-track/no-SLA). Pressure
derived from task.dueDate + createdAt; priority derived from
pressure since the BE has no priority field. Uses --state-warn-*
semantic tokens added in Task 1."
```

---

## Task 8: FE — Wire hero + InboxTaskRow into WorkflowInboxPage

**Files:**
- Modify: `boilerplateFE/src/features/workflow/pages/WorkflowInboxPage.tsx`

- [ ] **Step 1: Read current page** — `Read` `boilerplateFE/src/features/workflow/pages/WorkflowInboxPage.tsx` in full.

- [ ] **Step 2: Add imports** — replace the existing imports header:

```tsx
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Inbox, Users, X, Plus } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Checkbox } from '@/components/ui/checkbox';
import { Spinner } from '@/components/ui/spinner';
import {
  Table, TableBody, TableHead, TableHeader, TableRow,
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
import { InboxStatusHero } from '../components/InboxStatusHero';
import { InboxTaskRow } from '../components/InboxTaskRow';
import type { PendingTaskSummary, BatchExecuteResult } from '@/types/workflow.types';
```

- [ ] **Step 3: Replace the table body** — locate the existing `<TableBody>` block. Replace each row's contents with:

```tsx
<TableBody>
  {tasks.map((task) => (
    <InboxTaskRow
      key={task.taskId}
      task={task}
      selected={selectedIds.has(task.taskId)}
      bulkEligible={!requiresForm(task)}
      onToggleSelect={toggleOne}
      onAct={setSelectedTask}
    />
  ))}
</TableBody>
```

Keep the existing `<TableHeader>` but replace its row with the new column headers (matching `InboxTaskRow`'s 5 cells):

```tsx
<TableHeader>
  <TableRow>
    <TableHead className="w-[40px]">
      <Checkbox
        checked={allSelected}
        onCheckedChange={toggleAll}
        aria-label={t('workflow.inbox.selectAll')}
      />
    </TableHead>
    <TableHead>{t('workflow.inbox.request')}</TableHead>
    <TableHead className="w-[180px]">{t('workflow.inbox.sla.header')}</TableHead>
    <TableHead className="w-[160px]">{t('workflow.inbox.raised')}</TableHead>
    <TableHead className="w-[100px]" />
  </TableRow>
</TableHeader>
```

The existing inline-table render of the 6 fields gets removed; the existing inline `removeTableCell` imports — `TableCell` is no longer needed in the page itself; remove it from imports if so.

- [ ] **Step 4: Insert hero above the delegation banner** — find the JSX render. The page returns roughly:

```tsx
return (
  <div className="space-y-6">
    <PageHeader … />
    {/* delegation banner */}
    {/* table or empty state */}
  </div>
);
```

Insert `<InboxStatusHero />` directly under `<PageHeader>` and above the delegation banner.

- [ ] **Step 5: Type-check + lint**

Run: `cd boilerplateFE && npm run build && npm run lint`
Expected: 0 errors / 0 warnings.

- [ ] **Step 6: Commit**

```bash
git add boilerplateFE/src/features/workflow/pages/WorkflowInboxPage.tsx
git commit -m "feat(fe/workflow): redesign inbox with status hero and SLA pressure rows

Adds the 3-card status-distribution strip (Overdue/Due today/Upcoming)
with collapse-when-zero. Replaces inline table-row rendering with
InboxTaskRow, which surfaces priority, definition chip (tinted), SLA
progress bar, and pressure chip per task. Bulk-select, delegation
banner, and new-request dialog unchanged."
```

---

## Task 9: FE — InstancesStatusHero component

**Files:**
- Create: `boilerplateFE/src/features/workflow/components/InstancesStatusHero.tsx`

- [ ] **Step 1: Create the component** — `boilerplateFE/src/features/workflow/components/InstancesStatusHero.tsx`:

```tsx
import { useTranslation } from 'react-i18next';
import { MetricCard } from '@/components/common';
import { cn } from '@/lib/utils';
import { useInstanceStatusCounts } from '../api/workflow.queries';

interface InstancesStatusHeroProps {
  startedByUserId?: string;
  entityType?: string;
  state?: string;
}

export function InstancesStatusHero({
  startedByUserId,
  entityType,
  state,
}: InstancesStatusHeroProps) {
  const { t } = useTranslation();
  const { data, isLoading } = useInstanceStatusCounts({
    startedByUserId,
    entityType,
    state,
  });

  if (isLoading || !data) return null;

  const total = data.active + data.awaiting + data.completed + data.cancelled;
  if (total === 0) return null;

  const cards: Array<{
    show: boolean;
    label: string;
    eyebrow: string;
    value: number;
    tone?: 'active' | 'destructive';
    emphasis?: boolean;
  }> = [
    {
      show: data.active > 0,
      label: t('workflow.instances.statusCounts.active'),
      eyebrow: t('workflow.instances.statusCounts.activeEyebrow'),
      value: data.active,
      tone: 'active',
      emphasis: true,
    },
    {
      show: data.awaiting > 0,
      label: t('workflow.instances.statusCounts.awaiting'),
      eyebrow: t('workflow.instances.statusCounts.awaitingEyebrow'),
      value: data.awaiting,
    },
    {
      show: data.completed > 0,
      label: t('workflow.instances.statusCounts.completed'),
      eyebrow: t('workflow.instances.statusCounts.completedEyebrow'),
      value: data.completed,
    },
    {
      show: data.cancelled > 0,
      label: t('workflow.instances.statusCounts.cancelled'),
      eyebrow: t('workflow.instances.statusCounts.cancelledEyebrow'),
      value: data.cancelled,
      tone: 'destructive',
    },
  ];

  const visible = cards.filter((c) => c.show);

  return (
    <div
      className={cn(
        'mb-6 grid gap-4',
        visible.length === 1 && 'sm:grid-cols-1',
        visible.length === 2 && 'sm:grid-cols-2',
        visible.length === 3 && 'sm:grid-cols-3',
        visible.length === 4 && 'sm:grid-cols-2 lg:grid-cols-4',
      )}
    >
      {visible.map((c) => (
        <MetricCard
          key={c.label}
          label={c.label}
          eyebrow={c.eyebrow}
          value={c.value}
          tone={c.tone}
          emphasis={c.emphasis}
        />
      ))}
    </div>
  );
}
```

- [ ] **Step 2: Type-check**

Run: `cd boilerplateFE && npm run build`
Expected: 0 errors.

- [ ] **Step 3: Commit**

```bash
git add boilerplateFE/src/features/workflow/components/InstancesStatusHero.tsx
git commit -m "feat(fe/workflow): add InstancesStatusHero component

Four-card status hero (Active/Awaiting/Completed/Cancelled) with
collapse-when-zero. Honors the page's existing startedByUserId,
entityType, and state scoping filters by passing them through to
useInstanceStatusCounts."
```

---

## Task 10: FE — Wire hero + tinted definition chip into WorkflowInstancesPage

**Files:**
- Modify: `boilerplateFE/src/features/workflow/pages/WorkflowInstancesPage.tsx`

- [ ] **Step 1: Read current page** — `Read` it in full.

- [ ] **Step 2: Add imports**:

```tsx
import { InstancesStatusHero } from '../components/InstancesStatusHero';
```

- [ ] **Step 3: Insert hero above the filter row** — find the JSX section that starts the filter Card / div. Insert above:

```tsx
<InstancesStatusHero
  startedByUserId={startedByUserId}
  entityType={entityTypeFilter || undefined}
/>
```

Do not pass `statusFilter`; the hero is the status distribution and would become self-filtering. If a future state filter is added, pass it as `state={stateFilter || undefined}`.

- [ ] **Step 4: Tint the definition-name cell** — find the `TableCell` that renders `instance.definitionName`. Wrap with a Badge:

```tsx
<TableCell>
  <Badge
    variant="outline"
    className="text-[var(--tinted-fg)] border-[var(--active-border)]"
  >
    {instance.definitionName}
  </Badge>
</TableCell>
```

Ensure `Badge` is imported.

- [ ] **Step 5: Type-check + lint**

Run: `cd boilerplateFE && npm run build && npm run lint`
Expected: 0 errors.

- [ ] **Step 6: Commit**

```bash
git add boilerplateFE/src/features/workflow/pages/WorkflowInstancesPage.tsx
git commit -m "feat(fe/workflow): add status hero and tinted definition chip on instances list

Four-card status strip honoring the existing filters. Definition-name
cell becomes a tinted chip (var(--tinted-fg)) matching Phase 2 platform
admin tenant chip treatment."
```

---

## Task 11: FE — WorkflowStatusHeader shared component

**Files:**
- Create: `boilerplateFE/src/features/workflow/components/WorkflowStatusHeader.tsx`

- [ ] **Step 1: Create the component**:

```tsx
import { type ReactNode } from 'react';
import { Badge } from '@/components/ui/badge';
import { Card, CardContent } from '@/components/ui/card';
import { cn } from '@/lib/utils';

type BadgeVariant = 'default' | 'secondary' | 'destructive' | 'outline';

export interface StatusHeaderChip {
  icon?: ReactNode;
  label: string;
  /** When true, applies tinted treatment matching definition/tenant chips. */
  tinted?: boolean;
}

interface WorkflowStatusHeaderProps {
  title: string;
  status: string;
  statusVariant: BadgeVariant;
  chips?: StatusHeaderChip[];
  actions?: ReactNode;
  className?: string;
}

export function WorkflowStatusHeader({
  title,
  status,
  statusVariant,
  chips = [],
  actions,
  className,
}: WorkflowStatusHeaderProps) {
  return (
    <Card variant="glass" className={cn(className)}>
      <CardContent className="flex flex-wrap items-start justify-between gap-4 py-5">
        <div className="min-w-0 flex-1 space-y-2">
          <div className="flex items-center gap-3 flex-wrap">
            <h1 className="text-2xl font-semibold gradient-text truncate">{title}</h1>
            <Badge variant={statusVariant}>{status}</Badge>
          </div>
          {chips.length > 0 && (
            <div className="flex items-center gap-2 flex-wrap text-xs">
              {chips.map((chip, idx) => (
                <span
                  key={idx}
                  className={cn(
                    'inline-flex items-center gap-1.5 rounded-full border px-2 py-0.5',
                    chip.tinted
                      ? 'text-[var(--tinted-fg)] border-[var(--active-border)]'
                      : 'text-muted-foreground border-border',
                  )}
                >
                  {chip.icon}
                  {chip.label}
                </span>
              ))}
            </div>
          )}
        </div>
        {actions && <div className="flex items-center gap-2 shrink-0">{actions}</div>}
      </CardContent>
    </Card>
  );
}
```

- [ ] **Step 2: Type-check**

Run: `cd boilerplateFE && npm run build`
Expected: 0 errors.

- [ ] **Step 3: Commit**

```bash
git add boilerplateFE/src/features/workflow/components/WorkflowStatusHeader.tsx
git commit -m "feat(fe/workflow): add WorkflowStatusHeader shared component

Glass header card with gradient-text title, status pill, optional
chips (tinted or muted), and optional actions slot. Used on
Instance Detail right rail and Definition Detail header."
```

---

## Task 12: FE — InstanceMetadataRail component

**Files:**
- Create: `boilerplateFE/src/features/workflow/components/InstanceMetadataRail.tsx`

- [ ] **Step 1: Create the component**:

```tsx
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Link } from 'react-router-dom';
import { Calendar, Check, Copy, GitBranch, User } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
import { ROUTES } from '@/config';
import { STATUS_BADGE_VARIANT } from '@/constants/status';
import { cn } from '@/lib/utils';
import { formatDate, formatDateTime } from '@/utils/format';
import type { PendingTaskSummary, WorkflowInstanceSummary } from '@/types/workflow.types';
import { WorkflowStatusHeader } from './WorkflowStatusHeader';

interface InstanceMetadataRailProps {
  instance: WorkflowInstanceSummary;
  myTask: PendingTaskSummary | null;
  isSuperAdmin: boolean;
  onAct: (task: PendingTaskSummary) => void;
  className?: string;
}

function CopyableField({ label, value }: { label: string; value: string | null | undefined }) {
  const { t } = useTranslation();
  const [copied, setCopied] = useState(false);

  if (!value) return null;

  const onCopy = async () => {
    await navigator.clipboard.writeText(value);
    setCopied(true);
    setTimeout(() => setCopied(false), 1500);
  };

  return (
    <div className="group flex items-start justify-between gap-2 py-1.5">
      <div className="min-w-0 flex-1">
        <div className="text-[10px] uppercase tracking-wide text-muted-foreground/70">{label}</div>
        <div className="text-xs font-medium text-foreground truncate">{value}</div>
      </div>
      <button
        type="button"
        onClick={onCopy}
        className="opacity-0 group-hover:opacity-100 transition-opacity text-muted-foreground hover:text-foreground"
        aria-label={t('workflow.detail.metadata.copy')}
      >
        {copied ? <Check className="h-3 w-3" /> : <Copy className="h-3 w-3" />}
      </button>
    </div>
  );
}

export function InstanceMetadataRail({
  instance,
  myTask,
  isSuperAdmin,
  onAct,
  className,
}: InstanceMetadataRailProps) {
  const { t } = useTranslation();

  const statusVariant = STATUS_BADGE_VARIANT[instance.status] ?? 'outline';
  const tenantId =
    'tenantId' in instance
      ? (instance as WorkflowInstanceSummary & { tenantId?: string | null }).tenantId
      : null;

  return (
    <div
      className={cn('space-y-4 lg:sticky', className)}
      style={{ top: 'calc(var(--shell-header-h, 4rem) + 1.5rem)' }}
    >
      <WorkflowStatusHeader
        title={instance.entityDisplayName ?? instance.entityId.slice(0, 8) + '…'}
        status={t(`workflow.status.${instance.status.toLowerCase()}`)}
        statusVariant={statusVariant}
        chips={[
          { icon: <GitBranch className="h-3 w-3" />, label: instance.definitionName, tinted: true },
          ...(instance.startedByDisplayName
            ? [{ icon: <User className="h-3 w-3" />, label: instance.startedByDisplayName }]
            : []),
          { icon: <Calendar className="h-3 w-3" />, label: formatDate(instance.startedAt) },
        ]}
      />

      {myTask && (
        <Card className="border-primary/30 bg-[var(--active-bg)]/30">
          <CardContent className="space-y-3 py-4">
            <div>
              <div className="text-xs uppercase tracking-wide text-muted-foreground">
                {t('workflow.detail.pendingActionTitle')}
              </div>
              <div className="text-sm font-semibold text-foreground mt-1">{myTask.stepName}</div>
            </div>
            <div className="flex items-center gap-2">
              <Button size="sm" onClick={() => onAct(myTask)} className="flex-1">
                {t('workflow.inbox.actOn')}
              </Button>
            </div>
          </CardContent>
        </Card>
      )}

      <Card variant="glass">
        <CardContent className="py-4 space-y-1">
          <CopyableField
            label={t('workflow.detail.metadata.instanceId')}
            value={instance.instanceId}
          />
          <CopyableField
            label={t('workflow.detail.metadata.entityId')}
            value={instance.entityId}
          />
          <Link
            to={ROUTES.WORKFLOWS.getDefinitionDetail(instance.definitionId)}
            className="text-xs text-primary hover:underline inline-block pt-1"
          >
            {t('workflow.detail.metadata.definitionLink')}
          </Link>
          {instance.completedAt && (
            <div className="pt-2 border-t border-border mt-2">
              <div className="text-[10px] uppercase tracking-wide text-muted-foreground/70">
                {t('workflow.detail.completedAt')}
              </div>
              <div className="text-xs text-foreground">{formatDateTime(instance.completedAt)}</div>
            </div>
          )}
          {isSuperAdmin && tenantId && (
            <div className="pt-2 border-t border-border mt-2">
              <div className="text-[10px] uppercase tracking-wide text-muted-foreground/70">
                {t('workflow.detail.metadata.tenantId')}
              </div>
              <Badge variant="outline" className="text-[var(--tinted-fg)] border-[var(--active-border)]">
                {tenantId}
              </Badge>
            </div>
          )}
        </CardContent>
      </Card>
    </div>
  );
}
```

The BE/FE summary type does not expose `tenantId` today, so the tenant chip is deliberately defensive and renders only if a future DTO includes it. Do not access `instance.tenantId` directly; TypeScript will fail.

- [ ] **Step 2: Type-check**

Run: `cd boilerplateFE && npm run build`
Expected: 0 errors.

- [ ] **Step 3: Commit**

```bash
git add boilerplateFE/src/features/workflow/components/InstanceMetadataRail.tsx
git commit -m "feat(fe/workflow): add InstanceMetadataRail component

Right-rail container for the redesigned instance detail page.
Sticky on lg+ via --shell-header-h offset. Renders status header,
pending action card (when assignee), and copyable metadata fields
with hover-reveal copy buttons (matches Phase 2 audit treatment)."
```

---

## Task 13: FE — WorkflowInstanceDetailPage 2-col restructure

**Files:**
- Modify: `boilerplateFE/src/features/workflow/pages/WorkflowInstanceDetailPage.tsx`

- [ ] **Step 1: Read current page** — `Read` `WorkflowInstanceDetailPage.tsx` in full.

- [ ] **Step 2: Replace imports**:

```tsx
import { useState } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { AlertCircle } from 'lucide-react';
import { Card, CardContent } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Spinner } from '@/components/ui/spinner';
import { ConfirmDialog, PageHeader } from '@/components/common';
import { Slot } from '@/lib/extensions';
import { ApprovalDialog } from '../components/ApprovalDialog';
import { InstanceMetadataRail } from '../components/InstanceMetadataRail';
import { WorkflowStepTimeline } from '../components/WorkflowStepTimeline';
import {
  useWorkflowInstance,
  useWorkflowDefinition,
  useWorkflowHistory,
  useCancelWorkflow,
  usePendingTasks,
} from '../api';
import { usePermissions } from '@/hooks';
import { useAuthStore, selectUser } from '@/stores';
import { PERMISSIONS } from '@/constants';
import { formatDateTime } from '@/utils/format';
import type { PendingTaskSummary } from '@/types/workflow.types';
```

(Adjust to match the actual current imports — keep what's needed.)

- [ ] **Step 3: Replace the JSX layout** — find the current return block. Replace with:

```tsx
return (
  <div className="space-y-6">
    <PageHeader
      title={instance.definitionName}
      breadcrumbs={[
        { to: '/workflows/instances', label: t('workflow.instances.title') },
        { label: instance.entityDisplayName ?? instance.entityId.slice(0, 8) },
      ]}
    />

    {canResubmit && (
      <Card>
        <CardContent className="py-5">
          <div className="flex items-start gap-3">
            <AlertCircle className="h-5 w-5 text-amber-500 shrink-0 mt-0.5" />
            <div>
              <h3 className="text-sm font-semibold text-foreground">
                {t('workflow.detail.returnedForRevision')}
              </h3>
              <p className="text-sm text-muted-foreground mt-1">
                {t('workflow.detail.returnedForRevisionDesc')}
              </p>
            </div>
          </div>
        </CardContent>
      </Card>
    )}

    <div className="grid grid-cols-1 gap-6 lg:grid-cols-[minmax(0,1fr)_320px]">
      <InstanceMetadataRail
        instance={instance}
        myTask={myTask}
        isSuperAdmin={isSuperAdmin}
        onAct={setSelectedTask}
        className="lg:order-2"
      />

      <div className="min-w-0 space-y-6 lg:order-1">
        <section className="space-y-3">
          <h2 className="text-base font-semibold text-foreground">
            {t('workflow.detail.stepHistory')}
          </h2>
          {definition?.states ? (
            <Card>
              <CardContent className="py-5">
                <WorkflowStepTimeline
                  instanceId={instanceId!}
                  currentState={instance.currentState}
                  states={definition.states}
                  instanceStatus={instance.status}
                />
              </CardContent>
            </Card>
          ) : historyLoading ? (
            <div className="flex justify-center py-6">
              <Spinner size="md" />
            </div>
          ) : (
            <Card>
              <CardContent className="py-5">
                {/* keep existing fallback timeline render */}
              </CardContent>
            </Card>
          )}
        </section>

        <section className="space-y-3">
          <h2 className="text-base font-semibold text-foreground">
            {t('workflow.detail.comments')}
          </h2>
          <Slot
            id="entity-detail-timeline"
            props={{ entityType: 'WorkflowInstance', entityId: instance.instanceId }}
          />
        </section>
      </div>

    </div>

    <ConfirmDialog … />
    {selectedTask && <ApprovalDialog … />}
  </div>
);
```

Preserve the existing fallback-history render (the one that maps over `history` records) — keep it as the second branch of the timeline section. Delete the old standalone status header card and pending action card sections; both are now rendered inside `InstanceMetadataRail`.

- [ ] **Step 4: Determine `isSuperAdmin`** — add near other hook calls, mirroring the product detail page pattern:

```tsx
const { hasPermission } = usePermissions();
const user = useAuthStore(selectUser);
const isSuperAdmin = !user?.tenantId;
```

There is no frontend `PERMISSIONS.System.SuperAdmin` constant today. Keep the existing `hasPermission(PERMISSIONS.Workflows.Cancel)` / `hasPermission(PERMISSIONS.Workflows.ViewAllTasks)` checks for workflow actions.

- [ ] **Step 5: Type-check + lint**

Run: `cd boilerplateFE && npm run build && npm run lint`
Expected: 0 errors.

- [ ] **Step 6: Commit**

```bash
git add boilerplateFE/src/features/workflow/pages/WorkflowInstanceDetailPage.tsx
git commit -m "feat(fe/workflow): restructure instance detail to 2-col with sticky right rail

Main column scrolls (timeline → comments); right rail is sticky on
lg+ with status header, pending action (if assignee), and copyable
metadata. Approve/reject buttons stay in view as the user scrolls
form data — matching Linear / GitHub issue convention. Stacks to
single column on <lg with the rail content first."
```

---

## Task 14: FE — WorkflowStepTimeline token sweep

**Files:**
- Modify: `boilerplateFE/src/features/workflow/components/WorkflowStepTimeline.tsx`

- [ ] **Step 1: Read the file** — `Read` `WorkflowStepTimeline.tsx`.

- [ ] **Step 2: Replace any hardcoded shades** — search for and replace:
  - `bg-primary-{50..950}`, `text-primary-{50..950}`, `border-primary-{50..950}` → `bg-primary`, `text-primary`, `border-primary` or semantic token vars
  - `bg-muted/30` form-data sub-cards → keep `bg-muted/30` (already semantic)
  - `bg-amber-*` literal classes → `bg-[var(--state-warn-bg)]`

Use `Edit` with `replace_all` only when the replacement is genuinely identical across all occurrences; otherwise per-occurrence edits.

- [ ] **Step 3: Type-check + visual verification**

Run: `cd boilerplateFE && npm run build`
Expected: 0 errors. Visual: in Playwright (test app), open an instance detail and confirm the timeline renders with no missing colors.

- [ ] **Step 4: Commit**

```bash
git add boilerplateFE/src/features/workflow/components/WorkflowStepTimeline.tsx
git commit -m "style(fe/workflow): sweep WorkflowStepTimeline to semantic tokens

Replaces hardcoded primary-* shades with semantic tokens. No
behavior change. Matches the J4 Spectrum token discipline from
Phase 0 onward."
```

---

## Task 15: FE — WorkflowDefinitionsPage polish

**Files:**
- Modify: `boilerplateFE/src/features/workflow/pages/WorkflowDefinitionsPage.tsx`

- [ ] **Step 1: Read the file**.

- [ ] **Step 2: Fix the abused status keys** — find the row's status cell:

```tsx
<TableCell>
  <Badge variant={STATUS_BADGE_VARIANT[def.isActive ? 'Active' : 'Inactive'] ?? 'outline'}>
    {def.isActive
      ? t('workflow.definitions.statusValue.active')
      : t('workflow.definitions.statusValue.inactive')}
  </Badge>
</TableCell>
```

(Replaces the inline `def.isActive ? 'default' : 'secondary'` mapping and the `t('workflow.definitions.activate')` / `deactivate` abuse.)

Add `STATUS_BADGE_VARIANT` import:
```tsx
import { STATUS_BADGE_VARIANT } from '@/constants/status';
```

- [ ] **Step 3: Token sweep** — scan for any hardcoded primary shades; replace.

- [ ] **Step 4: Type-check + lint**

Run: `cd boilerplateFE && npm run build && npm run lint`
Expected: 0 errors.

- [ ] **Step 5: Commit**

```bash
git add boilerplateFE/src/features/workflow/pages/WorkflowDefinitionsPage.tsx
git commit -m "style(fe/workflow): polish definitions list with proper status badge mapping

Replaces inline isActive ? 'default' : 'secondary' with shared
STATUS_BADGE_VARIANT lookup, and fixes the abused activate/deactivate
i18n keys (those are button labels, not status labels) by switching
to workflow.definitions.statusValue.{active,inactive}."
```

---

## Task 16: FE — WorkflowDefinitionDetailPage polish + read-only canvas embed

**Files:**
- Modify: `boilerplateFE/src/features/workflow/pages/WorkflowDefinitionDetailPage.tsx`
- Possibly modify: `boilerplateFE/src/features/workflow/components/designer/DesignerCanvas.tsx` (verify `readOnly` prop hides toolbar/sidepanel — read first)

- [ ] **Step 1: Read both files**.

- [ ] **Step 2: Replace the page header block** — wherever the `<PageHeader title={...}>` appears at the top, replace with `<WorkflowStatusHeader>`:

```tsx
<WorkflowStatusHeader
  title={def.displayName ?? def.name}
  status={def.isActive
    ? t('workflow.definitions.statusValue.active')
    : t('workflow.definitions.statusValue.inactive')}
  statusVariant={STATUS_BADGE_VARIANT[def.isActive ? 'Active' : 'Inactive'] ?? 'outline'}
  chips={[
    { icon: <Layers className="h-3 w-3" />, label: def.entityType, tinted: true },
    { label: t('workflow.definitions.steps') + ': ' + def.stepCount },
    { label: def.isTemplate
        ? t('workflow.definitions.systemTemplate')
        : t('workflow.definitions.customized') },
  ]}
  actions={
    <>
      {/* keep existing Edit / Clone / Designer buttons */}
    </>
  }
/>
```

- [ ] **Step 3: Embed read-only DesignerCanvas in the Overview tab** — create a tiny local preview component in `WorkflowDefinitionDetailPage.tsx` (or extract to `components/DefinitionCanvasPreview.tsx` if the page gets crowded). `DesignerCanvas` already wraps `ReactFlowProvider`; hydrate the Zustand designer store before rendering it:

```tsx
import { useEffect } from 'react';
import { DesignerCanvas } from '../components/designer/DesignerCanvas';
import { useDesignerStore } from '../components/designer/hooks/useDesignerStore';

function DefinitionCanvasPreview({
  states,
  transitions,
}: {
  states: WorkflowStateConfig[];
  transitions: WorkflowTransitionConfig[];
}) {
  const load = useDesignerStore((s) => s.load);

  useEffect(() => {
    load(states ?? [], transitions ?? []);
  }, [load, states, transitions]);

  return <DesignerCanvas readOnly />;
}

// inside Overview tab JSX:
<Card variant="glass">
  <CardContent className="p-0">
    <div className="min-h-[420px] max-h-[60vh]">
      <DefinitionCanvasPreview states={def.states ?? []} transitions={def.transitions ?? []} />
    </div>
  </CardContent>
</Card>
```

The `DesignerCanvas` consumes a Zustand store (`useDesignerStore`); the page must call `load(states, transitions)` on mount so the store is hydrated. Match the pattern used in `WorkflowDefinitionDesignerPage.tsx` (`useEffect` that calls `load(...)` once).

If `DesignerCanvas`' `readOnly` prop does not currently suppress toolbar/sidepanel rendering, that's fine — the toolbar/sidepanel are siblings on the Designer page, not children of the canvas. Just embed the canvas alone here.

- [ ] **Step 4: Add a local raw-JSON toggle** below the canvas, using the existing audit `<JsonView>` component:

```tsx
import { JsonView } from '@/features/audit-logs/components/JsonView';

const [showRawJson, setShowRawJson] = useState(false);

<div className="space-y-3">
  <Button
    type="button"
    variant="ghost"
    size="sm"
    onClick={() => setShowRawJson((v) => !v)}
  >
    {t('workflow.detail.viewRawJson')}
  </Button>
  {showRawJson && (
    <JsonView payload={{ states: def.states, transitions: def.transitions }} />
  )}
</div>
```

- [ ] **Step 5: Token sweep on the Analytics tab content** — `<WorkflowAnalyticsTab>` and its children: replace any hardcoded primary shades.

- [ ] **Step 6: Type-check + lint**

Run: `cd boilerplateFE && npm run build && npm run lint`
Expected: 0 errors.

- [ ] **Step 7: Commit**

```bash
git add boilerplateFE/src/features/workflow/pages/WorkflowDefinitionDetailPage.tsx boilerplateFE/src/features/workflow/components
git commit -m "feat(fe/workflow): polish definition detail with status header and read-only canvas

Glass header card with gradient title + status pill + tinted entity-type
chip. Overview tab gains a read-only DesignerCanvas mini-preview
(consuming the same Zustand store) plus a collapsible raw-JSON view.
Analytics tab tokens swept; structure unchanged."
```

---

## Task 17: FE — Designer chrome polish (toolbar, side panel, read-only banner)

**Files:**
- Modify: `boilerplateFE/src/features/workflow/components/designer/DesignerToolbar.tsx`
- Modify: `boilerplateFE/src/features/workflow/components/designer/SidePanel.tsx`
- Modify: `boilerplateFE/src/features/workflow/pages/WorkflowDefinitionDesignerPage.tsx` (read-only banner styling)

- [ ] **Step 1: Read all three files**.

- [ ] **Step 2: Toolbar** — wrap the toolbar's outermost div with `surface-glass`:

```tsx
<div className="surface-glass border-b border-border px-4 py-2 flex items-center gap-2">
  {/* existing buttons */}
</div>
```

Make the primary "Save" button use the gradient treatment. If a `btn-primary-gradient` utility class exists, apply it; otherwise use `Button variant="default"` (which already has the copper-fill primary treatment).

- [ ] **Step 3: SidePanel** — add `surface-glass` to the panel's root container:

```tsx
<aside className="surface-glass border-l border-border w-[320px] shrink-0 overflow-y-auto">
  {/* existing content */}
</aside>
```

- [ ] **Step 4: Read-only banner** — in `WorkflowDefinitionDesignerPage.tsx`, replace the existing banner Card:

```tsx
{readOnly && (
  <Card variant="glass" className="m-4">
    <CardContent className="py-4 flex items-center justify-between gap-4">
      <div>
        <h3 className="text-sm font-semibold">{t('workflow.designer.template.readOnlyTitle')}</h3>
        <p className="text-xs text-muted-foreground">{t('workflow.designer.template.readOnlyBody')}</p>
      </div>
      <Button onClick={handleClone} disabled={cloning}>
        {t('workflow.designer.template.cloneToEdit')}
      </Button>
    </CardContent>
  </Card>
)}
```

(Just adds `variant="glass"` — `Card` already supports it from Phase 2.)

- [ ] **Step 5: Type-check + lint**

Run: `cd boilerplateFE && npm run build && npm run lint`
Expected: 0 errors.

- [ ] **Step 6: Commit**

```bash
git add boilerplateFE/src/features/workflow/components/designer/DesignerToolbar.tsx boilerplateFE/src/features/workflow/components/designer/SidePanel.tsx boilerplateFE/src/features/workflow/pages/WorkflowDefinitionDesignerPage.tsx
git commit -m "style(fe/workflow): glass-treat designer toolbar, side panel, and read-only banner

Adds surface-glass treatment to the designer chrome surfaces.
No behavior change to drag/drop, save, navigate-away guard, or
auto-layout — chrome only."
```

---

## Task 18: FE — Designer canvas dot grid + StateNode tinting + TransitionEdge sweep

**Files:**
- Modify: `boilerplateFE/src/features/workflow/components/designer/DesignerCanvas.tsx`
- Modify: `boilerplateFE/src/features/workflow/components/designer/StateNode.tsx`
- Modify: `boilerplateFE/src/features/workflow/components/designer/TransitionEdge.tsx`

- [ ] **Step 1: Read all three files**.

- [ ] **Step 2: Add dot grid background to canvas** — in `DesignerCanvas.tsx`, find the ReactFlow wrapper element. Add the dot-grid CSS via a className + style. If the container is `<div className="…">…<ReactFlow … /></div>`, change to:

```tsx
<div
  className="h-full w-full relative"
  style={{
    backgroundImage: 'radial-gradient(var(--border) 1px, transparent 1px)',
    backgroundSize: '14px 14px',
  }}
>
  <ReactFlow … />
</div>
```

(ReactFlow itself draws into this container; the dot grid sits below the nodes.)

If ReactFlow already renders its own `<Background variant="dots" />` component, prefer using that with semantic-token color instead:
```tsx
<Background variant={BackgroundVariant.Dots} gap={14} size={1} color="var(--border)" />
```

- [ ] **Step 3: StateNode type-aware tinting** — in `StateNode.tsx`, derive the tint from the node's `data.type`:

```tsx
import { useTranslation } from 'react-i18next';

const TYPE_TINT: Record<string, { dot: string; ring: string; tooltipKey: string }> = {
  Initial:   { dot: 'bg-emerald-500', ring: 'ring-emerald-500/40 border-emerald-500/40 bg-emerald-50 dark:bg-emerald-950/20', tooltipKey: 'workflow.designer.stateType.initial' },
  HumanTask: { dot: 'bg-primary',     ring: 'ring-primary/40 border-primary/40 bg-[var(--active-bg)]/40',                     tooltipKey: 'workflow.designer.stateType.humanTask' },
  Final:     { dot: 'bg-muted-foreground/60', ring: 'border-border bg-muted/40',                                                tooltipKey: 'workflow.designer.stateType.final' },
};
const FALLBACK_TINT = { dot: 'bg-muted-foreground/40', ring: 'border-border bg-card', tooltipKey: 'workflow.designer.stateType.other' };

export function StateNode({ data, selected }: NodeProps) {
  const { t } = useTranslation();
  const tint = TYPE_TINT[data.type] ?? FALLBACK_TINT;

  return (
    <div
      title={t(tint.tooltipKey)}
      className={cn(
        'rounded-lg border-2 px-3 py-2 min-w-[120px] text-sm font-medium transition-shadow',
        tint.ring,
        selected && 'shadow-[var(--glow-primary-sm)]',
      )}
    >
      <div className="flex items-center gap-2">
        <span className={cn('h-2 w-2 rounded-full shrink-0', tint.dot)} />
        <span className="truncate">{data.displayName ?? data.name}</span>
      </div>
      {/* keep existing handle elements */}
    </div>
  );
}
```

Adapt to the actual props shape (`NodeProps<StateNodeData>`); preserve the existing Handle elements (input/output ports) — only the visual wrapper and dot/ring change.

- [ ] **Step 4: TransitionEdge token sweep** — in `TransitionEdge.tsx`, replace any hardcoded gray strokes:

```tsx
const stroke = selected ? 'var(--primary)' : 'var(--muted-foreground)';
```

Apply via the `style` prop on the path element. No structural change.

- [ ] **Step 5: Type-check + lint**

Run: `cd boilerplateFE && npm run build && npm run lint`
Expected: 0 errors.

- [ ] **Step 6: Commit**

```bash
git add boilerplateFE/src/features/workflow/components/designer
git commit -m "feat(fe/workflow): designer state-type tinting and dot-grid canvas

Adds subtle radial dot grid background to the canvas and type-aware
tinting on state nodes (Initial=emerald, HumanTask=copper, Final=neutral)
using J4 spectrum companion scales already in the theme. Tooltips
identify state type. TransitionEdge tokens swept. Drag/drop, hit
targets, dirty tracking, and auto-layout unchanged."
```

---

## Task 19: Verification — full lint + typecheck + build + Playwright run in test app

**Files:** none (verification-only)

- [ ] **Step 1: FE final lint + typecheck + build**

```bash
cd boilerplateFE
npm run lint
npm run build
```

Expected: `npm run lint` reports 0 errors/0 warnings; `npm run build` exits 0. Existing Vite chunk-size warnings may still appear and are not part of this redesign unless new chunks regress sharply.

- [ ] **Step 2: BE final build**

```bash
cd boilerplateBE
dotnet build
dotnet test --filter "FullyQualifiedName~Workflow"
```

Expected: 0 errors; all workflow tests pass (existing + 5 new from Tasks 2 & 3). Existing NU1902/NU1903 package warnings may still appear and are not part of this redesign unless they change from baseline.

- [ ] **Step 3: Locate or set up the test app per CLAUDE.md "Post-Feature Testing Workflow"**

Free ports check:
```bash
lsof -iTCP -sTCP:LISTEN -nP | awk '{print $9}' | grep -oE '[0-9]+$' | sort -un
```

If `_testJ4visual` test app already exists from prior phases, copy the Phase 5a FE diff over it for hot-reload. Otherwise create a new test app:
```bash
cd /Users/samanjasim/Projects/forme/cqrs/boilerplate-cqrs-fe
scripts/rename.ps1 -Name "_testPhase5aWorkflow" -OutputDir "." -Modules "All" -IncludeMobile:$false
```
Then follow steps 3–7 from CLAUDE.md.

- [ ] **Step 4: Seed multi-status data** — create at least:
  - 5 pending tasks for one user (1 overdue, 1 due-today, 3 upcoming including 1 with no SLA)
  - 4 instances spanning Active / Awaiting / Completed / Cancelled
  - 2 definitions (1 active, 1 inactive)
  - 1 returned-for-revision instance to verify the resubmit notice
  - 1 instance whose definition uses each state type (Initial, HumanTask, Final)

- [ ] **Step 5: Playwright verification per page** (use chrome-devtools-mcp or playwright):
  - Login as the test user.
  - **Inbox** — verify hero counts match table reality. SLA bars render. Pressure chips show correct color (red/amber/green/muted). Definition chips are tinted. Bulk-select still works.
  - **Inbox empty state** — clear the user's tasks; verify hero hides and EmptyState shows.
  - **Instances list** — verify 4-card hero renders and respects the my-requests-only toggle (counts change when toggled). Definition-name cell is a tinted chip.
  - **Instance Detail** — verify right rail is sticky on `lg+` (window ≥ 1024px). Approve/reject buttons stay in view while scrolling form data. Layout collapses to single column on `<lg`.
  - **Returned-for-revision instance** — verify the AlertCircle banner sits above the 2-col grid.
  - **Definitions list** — verify status badges show the new active/inactive treatment with proper translations.
  - **Definition Detail** — verify the read-only mini-canvas renders with state-type tinting. Switch tabs to Analytics and verify it works.
  - **Designer** — open an editable definition. Verify drag/drop, edit, save, navigate-away guard all work identically. Verify state-type tinting renders for Initial / HumanTask / Final on a real definition. Verify the read-only template banner clones correctly.
  - **AR locale** — switch to AR. Verify RTL renders correctly across all 6 pages, especially the SLA bar direction and the sticky right rail (becomes sticky-left in RTL — verify).
  - **KU locale** — switch to KU. Verify translations land.

Capture screenshots of each page in EN + AR for the PR description.

- [ ] **Step 6: Fix findings inline** — any visual regressions or broken behavior found in Step 5: fix in the worktree source. For FE-only, copy files to test app for hot reload. For BE, regenerate test app per CLAUDE.md.

- [ ] **Step 7: No commit unless fixes are needed** (verification-only task).

---

## Task 20: Final code review + PR

**Files:** none (review + push)

- [ ] **Step 1: Run code review** — invoke `superpowers:requesting-code-review` on the diff vs `origin/main`. Address blocker items inline.

- [ ] **Step 2: Verify final commit count and shape**:

```bash
git log --oneline origin/main..HEAD
```

Expected: ~18 commits (one per task, plus the spec commit).

- [ ] **Step 3: Push + create PR**:

```bash
git push -u origin fe/phase-5-design
```

```bash
gh pr create --title "feat(fe): Phase 5a Workflow — workflow cluster (6 pages + read-only canvas embed)" --body "$(cat <<'EOF'
## Summary
- Brings all 6 workflow pages onto J4 Spectrum tokens.
- Two earned structural changes: command-center inbox with row-level SLA pressure, and sticky right-rail instance detail.
- Designer gains chrome polish + state-type tinting (Initial=emerald, HumanTask=copper, Final=neutral) with zero ergonomic regression.
- Two new BE query handlers (`GetInboxStatusCounts`, `GetInstanceStatusCounts`) for hero counts. No schema changes.

## Spec & Plan
- Spec: [docs/superpowers/specs/2026-04-29-redesign-phase-5a-workflow-design.md](docs/superpowers/specs/2026-04-29-redesign-phase-5a-workflow-design.md)
- Plan: [docs/superpowers/plans/2026-04-29-redesign-phase-5a-workflow.md](docs/superpowers/plans/2026-04-29-redesign-phase-5a-workflow.md)

## Test plan
- [x] BE: `dotnet test --filter "FullyQualifiedName~Workflow"` passes
- [x] FE: `npm run lint` clean, `npm run build` clean
- [x] Test app exercise: inbox / instances / instance detail / definitions / definition detail / designer
- [x] EN + AR + KU locale verification
- [ ] Reviewer: confirm right rail stickiness and RTL behavior on instance detail

## Phase 5 next steps
This is the first of three Phase 5 PRs. After ship:
- **Phase 5b** — Communication cluster (5 pages + dialogs)
- **Phase 5c** — Webhooks + Import/Export + Comments-Activity slots + Onboarding wizard

## Out of scope (locked at brainstorm)
No new functionality. No animated active-state pulse. No 2-col on Definition Detail. No mobile-specific reflow beyond `lg+` breakpoint stacking.
EOF
)"
```

---

## Spec coverage check

Mapping each spec section to the task that implements it:

- §1 Goal — Tasks 8 (inbox), 13 (instance detail), 18 (designer)
- §2 Non-goals — none implemented; explicit exclusions
- §3.1 InboxPage — Tasks 6 (hero), 7 (row), 8 (page integration)
- §3.2 InstanceDetailPage — Tasks 11 (header), 12 (rail), 13 (layout), 14 (timeline tokens)
- §3.3 InstancesPage — Tasks 9 (hero), 10 (page integration)
- §3.4 DefinitionsPage — Task 15
- §3.5 DefinitionDetailPage — Tasks 11 (header reuse), 16 (page integration + read-only canvas)
- §3.6 DesignerPage — Tasks 17 (chrome), 18 (canvas grid + state node tinting + edges)
- §4.1 InboxStatusCounts BE — Task 2
- §4.2 InstanceStatusCounts BE — Task 3
- §4.3 No DTO additions — confirmed during plan
- §5.1 New components — Tasks 7 (InboxTaskRow), 11 (WorkflowStatusHeader), 12 (InstanceMetadataRail)
- §5.2 Reused components — covered across tasks; no separate task
- §5.3 New hooks — Task 4
- §6 Tokens / styling — Tasks 1 (foundation), 14 (timeline), 18 (designer)
- §7 Translations — Task 5
- §8 Permissions — none (reuse `Workflows.View` and `Workflows.ActOnTask`)
- §9 Testing — Tasks 2, 3 (handler tests); Task 19 (Playwright)
- §10 Branch / PR — Task 20
- §11 Open questions — resolved in Tasks 7 (priority from SLA), 12 (tenant chip gated on `instance.tenantId`), 1 (`--state-warn-*` if absent), 1 (`--shell-header-h` if absent)
- §12 Phase 5b/5c — out of scope; mentioned in PR body (Task 20)

All spec sections are covered.

## Open issues to resolve during implementation

Pre-flight verifications that the executing agent should perform on Task 1 / 2 / 3 to avoid surprises:

1. **`ApprovalTaskTestFactory` / `WorkflowInstanceTestFactory`** — neither exists as a shared helper today. Create them inline in the new test files as private/static helpers, using `ApprovalTask.Create(...)`, `WorkflowInstance.Create(...)`, `.Complete(...)`, and `.Cancel(...)` so tests compile against actual domain APIs.
2. **`TimeProvider` DI** — no global registration is visible in the current tree. Task 2 explicitly verifies and adds `services.AddSingleton(TimeProvider.System)` if still absent.
3. **`WorkflowInstanceSummary.tenantId`** — absent today. `InstanceMetadataRail` must use a defensive `'tenantId' in instance` check and render nothing until the DTO grows.
4. **`Card variant="glass"`** — confirm Card already supports the `glass` variant (added in Phase 2 per CLAUDE.md). If absent, add it as part of Task 11.
5. **`--shell-header-h`** — Task 1 verifies and adds if absent.
6. **Raw JSON preview** — use audit `JsonView` with `payload`; do not import nonexistent `components/common/JsonView` or `components/ui/collapsible`.

---

## Execution

Plan complete and saved to `docs/superpowers/plans/2026-04-29-redesign-phase-5a-workflow.md`.
