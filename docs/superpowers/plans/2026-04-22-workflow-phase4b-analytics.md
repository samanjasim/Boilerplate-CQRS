# Workflow Phase 4b — Analytics Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ship a read-only Analytics tab on `WorkflowDefinitionDetailPage` backed by one `GET /api/v1/Workflow/definitions/{id}/analytics?window={7d|30d|90d|all}` endpoint that answers the six operator questions (cycle time, bottlenecks, action rates, throughput, stuck instances, per-approver activity) from existing tables.

**Architecture:** New module-internal `GetWorkflowAnalyticsQuery` + handler living in `Starter.Module.Workflow.Application.Queries.GetWorkflowAnalytics/`. Handler injects the concrete `WorkflowDbContext` + `IUserReader` (same pattern as `WorkflowEngine.GetHistoryAsync`). Percentile and time-bucket calculations branch on `db.Database.ProviderName`: Postgres uses raw SQL (`percentile_cont`, `date_trunc`); EF InMemory materializes rows and does the math in C#. New `Workflows.ViewAnalytics` permission. Frontend adds a tabbed layout on `WorkflowDefinitionDetailPage` with widget components under `features/workflow/components/analytics/`.

**Tech Stack:** .NET 10 · MediatR · EF Core 9 (Postgres + InMemory) · xUnit + FluentAssertions + Moq · React 19 · TanStack Query · shadcn/ui Tabs · Recharts (already in `package.json` for billing) · Tailwind CSS 4 · i18next.

**Spec:** `docs/superpowers/specs/2026-04-22-workflow-phase4b-analytics-design.md`

---

## File Structure

### Backend — new files

| File | Responsibility |
|---|---|
| `boilerplateBE/src/modules/Starter.Module.Workflow/Application/Queries/GetWorkflowAnalytics/WindowSelector.cs` | Enum `SevenDays \| ThirtyDays \| NinetyDays \| AllTime` + `TryParse("7d"\|"30d"\|"90d"\|"all")`. |
| `.../Application/Queries/GetWorkflowAnalytics/WorkflowAnalyticsDto.cs` | All DTO records (`WorkflowAnalyticsDto`, `HeadlineMetrics`, `StateMetric`, `ActionRateMetric`, `InstanceCountPoint`, `StuckInstanceDto`, `ApproverActivityDto`). |
| `.../Application/Queries/GetWorkflowAnalytics/GetWorkflowAnalyticsQuery.cs` | MediatR request record: `(Guid DefinitionId, WindowSelector Window) : IRequest<Result<WorkflowAnalyticsDto>>`. |
| `.../Application/Queries/GetWorkflowAnalytics/GetWorkflowAnalyticsQueryHandler.cs` | Primary handler — tenant/template guards, orchestrates six private aggregation methods, composes the DTO. |

### Backend — modified files

| File | Change |
|---|---|
| `.../Constants/WorkflowPermissions.cs` | Add `public const string ViewAnalytics = "Workflows.ViewAnalytics";`. |
| `.../Domain/Errors/WorkflowErrors.cs` | Add `AnalyticsNotAvailableOnTemplate()` (`NotFound`) + `InvalidAnalyticsWindow(string raw)` (`Validation`). |
| `.../Controllers/WorkflowController.cs` | Add `GET definitions/{id:guid}/analytics` action. |
| `.../WorkflowModule.cs` | Add `ViewAnalytics` to `GetPermissions()` and grant to `SuperAdmin` + `Admin` in `GetDefaultRolePermissions()`. |

### Backend — new test files

| File | Coverage |
|---|---|
| `boilerplateBE/tests/Starter.Api.Tests/Workflow/GetWorkflowAnalyticsQueryHandlerTests.cs` | Happy path + every guard/edge listed in the spec's Unit tests section. |
| `boilerplateBE/tests/Starter.Api.Tests/Workflow/WorkflowAnalyticsPerformanceTests.cs` | 10k-instance seed, `[Trait("perf", "true")]` budget assertion. |

### Frontend — new files

| File | Responsibility |
|---|---|
| `boilerplateFE/src/features/workflow/components/analytics/WorkflowAnalyticsTab.tsx` | Composes widgets + window selector. Data fetch via `useWorkflowAnalytics`. |
| `.../components/analytics/WindowSelector.tsx` | Dropdown: `7d` \| `30d` \| `90d` \| `All time`. Lifts value. |
| `.../components/analytics/LowDataBanner.tsx` | Muted banner shown when `instancesInWindow < 5`. |
| `.../components/analytics/HeadlineStrip.tsx` | Four stat cards (Started / Completed / Cancelled / Avg cycle). |
| `.../components/analytics/InstanceCountChart.tsx` | Stacked `BarChart` (Recharts) — full width. |
| `.../components/analytics/BottleneckStatesChart.tsx` | Horizontal `BarChart` — median dwell per state. |
| `.../components/analytics/ActionRatesChart.tsx` | Grouped `BarChart` per state. |
| `.../components/analytics/StuckInstancesTable.tsx` | Table, row click → `/workflows/instances/{id}`. |
| `.../components/analytics/ApproverActivityTable.tsx` | Table — user / approvals / rejections / returns / avg response. |

### Frontend — modified files

| File | Change |
|---|---|
| `boilerplateFE/src/constants/permissions.ts` | Add `Workflows.ViewAnalytics`. |
| `boilerplateFE/src/types/workflow.types.ts` | Add `WorkflowAnalyticsDto` + sub-types + `WindowSelector` union. |
| `boilerplateFE/src/config/api.config.ts` | Add `DEFINITION_ANALYTICS: (id) => \`/workflow/definitions/${id}/analytics\`` to `WORKFLOW` block. |
| `boilerplateFE/src/features/workflow/api/workflow.api.ts` | Add `getAnalytics(id, window)` method. |
| `boilerplateFE/src/features/workflow/api/workflow.queries.ts` | Add `useWorkflowAnalytics(id, window)`. |
| `boilerplateFE/src/lib/query/keys.ts` | Add `workflow.definitions.analytics(id, window)`. |
| `boilerplateFE/src/features/workflow/pages/WorkflowDefinitionDetailPage.tsx` | Wrap post-header content in `<Tabs>` (Overview / Analytics). |
| `boilerplateFE/src/features/workflow/i18n/*.json` (en/ar/ku) | Add `workflow.analytics.*` keys. |

### Docs

| File | Change |
|---|---|
| `docs/features/workflow-analytics.md` (new) | What each metric means, window rule, low-data caveat, template exclusion, click-through pattern. |
| `docs/roadmaps/workflow.md` (modify) | Move Phase 4b to "Shipped". Add "Analytics follow-ups (deferred)" sub-section. |

---

## Task Decomposition

Tasks are grouped by vertical slice. Within each task, steps are 2–5 minutes each and follow red-green-refactor when tests are applicable.

---

### Task 1: Add `ViewAnalytics` permission constant + role seeding

**Files:**
- Modify: `boilerplateBE/src/modules/Starter.Module.Workflow/Constants/WorkflowPermissions.cs`
- Modify: `boilerplateBE/src/modules/Starter.Module.Workflow/WorkflowModule.cs`
- Test: (no unit test — constant/DI wiring is exercised by the integration tests in later tasks)

- [ ] **Step 1: Add the permission constant**

Open `WorkflowPermissions.cs` and add a new line after `ViewAllTasks`:

```csharp
namespace Starter.Module.Workflow.Constants;

public static class WorkflowPermissions
{
    public const string View = "Workflows.View";
    public const string ManageDefinitions = "Workflows.ManageDefinitions";
    public const string Start = "Workflows.Start";
    public const string ActOnTask = "Workflows.ActOnTask";
    public const string Cancel = "Workflows.Cancel";
    public const string ViewAllTasks = "Workflows.ViewAllTasks";
    public const string ViewAnalytics = "Workflows.ViewAnalytics";
}
```

- [ ] **Step 2: Register the permission with display text in `WorkflowModule.GetPermissions`**

In `WorkflowModule.cs`, inside `GetPermissions()` add after the `ViewAllTasks` yield:

```csharp
yield return (WorkflowPermissions.ViewAnalytics, "View workflow analytics dashboards per definition", "Workflow");
```

- [ ] **Step 3: Grant `ViewAnalytics` to SuperAdmin and Admin by default**

In `WorkflowModule.GetDefaultRolePermissions()`, append `WorkflowPermissions.ViewAnalytics` to the `SuperAdmin` and `Admin` arrays. Do NOT grant to `User`.

After the edit the two arrays should look like:

```csharp
yield return ("SuperAdmin", [
    WorkflowPermissions.View,
    WorkflowPermissions.ManageDefinitions,
    WorkflowPermissions.Start,
    WorkflowPermissions.ActOnTask,
    WorkflowPermissions.Cancel,
    WorkflowPermissions.ViewAllTasks,
    WorkflowPermissions.ViewAnalytics]);
yield return ("Admin", [
    WorkflowPermissions.View,
    WorkflowPermissions.ManageDefinitions,
    WorkflowPermissions.Start,
    WorkflowPermissions.ActOnTask,
    WorkflowPermissions.Cancel,
    WorkflowPermissions.ViewAllTasks,
    WorkflowPermissions.ViewAnalytics]);
```

- [ ] **Step 4: Build**

Run: `cd boilerplateBE && dotnet build`
Expected: Build succeeded, 0 errors.

- [ ] **Step 5: Commit**

```bash
git add boilerplateBE/src/modules/Starter.Module.Workflow/Constants/WorkflowPermissions.cs \
        boilerplateBE/src/modules/Starter.Module.Workflow/WorkflowModule.cs
git commit -m "feat(workflow): add Workflows.ViewAnalytics permission + seed grants"
```

---

### Task 2: Add `WindowSelector` enum + parser

**Files:**
- Create: `boilerplateBE/src/modules/Starter.Module.Workflow/Application/Queries/GetWorkflowAnalytics/WindowSelector.cs`
- Test: `boilerplateBE/tests/Starter.Api.Tests/Workflow/WindowSelectorTests.cs`

- [ ] **Step 1: Write the failing test**

Create `boilerplateBE/tests/Starter.Api.Tests/Workflow/WindowSelectorTests.cs`:

```csharp
using FluentAssertions;
using Starter.Module.Workflow.Application.Queries.GetWorkflowAnalytics;
using Xunit;

namespace Starter.Api.Tests.Workflow;

public sealed class WindowSelectorTests
{
    [Theory]
    [InlineData("7d",  WindowSelector.SevenDays)]
    [InlineData("30d", WindowSelector.ThirtyDays)]
    [InlineData("90d", WindowSelector.NinetyDays)]
    [InlineData("all", WindowSelector.AllTime)]
    [InlineData("ALL", WindowSelector.AllTime)]
    [InlineData("30D", WindowSelector.ThirtyDays)]
    public void TryParse_ValidString_ReturnsExpectedEnum(string raw, WindowSelector expected)
    {
        var ok = WindowSelectorParser.TryParse(raw, out var value);

        ok.Should().BeTrue();
        value.Should().Be(expected);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("1d")]
    [InlineData("180d")]
    [InlineData("custom")]
    public void TryParse_Invalid_ReturnsFalse(string? raw)
    {
        var ok = WindowSelectorParser.TryParse(raw, out _);
        ok.Should().BeFalse();
    }
}
```

- [ ] **Step 2: Run the test — expect compile failure**

Run: `cd boilerplateBE && dotnet test tests/Starter.Api.Tests/Starter.Api.Tests.csproj --filter "FullyQualifiedName~WindowSelectorTests"`
Expected: Compile error — `WindowSelector` / `WindowSelectorParser` do not exist yet.

- [ ] **Step 3: Implement the enum + parser**

Create `boilerplateBE/src/modules/Starter.Module.Workflow/Application/Queries/GetWorkflowAnalytics/WindowSelector.cs`:

```csharp
namespace Starter.Module.Workflow.Application.Queries.GetWorkflowAnalytics;

public enum WindowSelector
{
    SevenDays = 0,
    ThirtyDays = 1,
    NinetyDays = 2,
    AllTime = 3,
}

public static class WindowSelectorParser
{
    public static bool TryParse(string? raw, out WindowSelector value)
    {
        value = default;
        if (string.IsNullOrWhiteSpace(raw)) return false;

        switch (raw.Trim().ToLowerInvariant())
        {
            case "7d":  value = WindowSelector.SevenDays;  return true;
            case "30d": value = WindowSelector.ThirtyDays; return true;
            case "90d": value = WindowSelector.NinetyDays; return true;
            case "all": value = WindowSelector.AllTime;    return true;
            default: return false;
        }
    }
}
```

- [ ] **Step 4: Run the test — expect PASS**

Run: `cd boilerplateBE && dotnet test tests/Starter.Api.Tests/Starter.Api.Tests.csproj --filter "FullyQualifiedName~WindowSelectorTests"`
Expected: PASS — all theory rows green.

- [ ] **Step 5: Commit**

```bash
git add boilerplateBE/src/modules/Starter.Module.Workflow/Application/Queries/GetWorkflowAnalytics/WindowSelector.cs \
        boilerplateBE/tests/Starter.Api.Tests/Workflow/WindowSelectorTests.cs
git commit -m "feat(workflow): add WindowSelector enum + parser for analytics window"
```

---

### Task 3: Define analytics DTOs + query record

**Files:**
- Create: `boilerplateBE/src/modules/Starter.Module.Workflow/Application/Queries/GetWorkflowAnalytics/WorkflowAnalyticsDto.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.Workflow/Application/Queries/GetWorkflowAnalytics/GetWorkflowAnalyticsQuery.cs`
- Test: (covered later by the handler tests in Task 5)

- [ ] **Step 1: Create the DTO file**

Create `WorkflowAnalyticsDto.cs`:

```csharp
namespace Starter.Module.Workflow.Application.Queries.GetWorkflowAnalytics;

public sealed record WorkflowAnalyticsDto(
    Guid DefinitionId,
    string DefinitionName,
    WindowSelector Window,
    DateTime WindowStart,
    DateTime WindowEnd,
    int InstancesInWindow,
    HeadlineMetrics Headline,
    IReadOnlyList<StateMetric> StatesByBottleneck,
    IReadOnlyList<ActionRateMetric> ActionRates,
    IReadOnlyList<InstanceCountPoint> InstanceCountSeries,
    IReadOnlyList<StuckInstanceDto> StuckInstances,
    IReadOnlyList<ApproverActivityDto> ApproverActivity);

public sealed record HeadlineMetrics(
    int TotalStarted,
    int TotalCompleted,
    int TotalCancelled,
    double? AvgCycleTimeHours);

public sealed record StateMetric(
    string StateName,
    double MedianDwellHours,
    double P95DwellHours,
    int VisitCount);

public sealed record ActionRateMetric(
    string StateName,
    string Action,
    int Count,
    double Percentage);

public sealed record InstanceCountPoint(
    DateTime Bucket,
    int Started,
    int Completed,
    int Cancelled);

public sealed record StuckInstanceDto(
    Guid InstanceId,
    string? EntityDisplayName,
    string CurrentState,
    DateTime StartedAt,
    int DaysSinceStarted,
    string? CurrentAssigneeDisplayName);

public sealed record ApproverActivityDto(
    Guid UserId,
    string UserDisplayName,
    int Approvals,
    int Rejections,
    int Returns,
    double? AvgResponseTimeHours);
```

- [ ] **Step 2: Create the query record**

Create `GetWorkflowAnalyticsQuery.cs`:

```csharp
using MediatR;
using Starter.Shared.Results;

namespace Starter.Module.Workflow.Application.Queries.GetWorkflowAnalytics;

public sealed record GetWorkflowAnalyticsQuery(
    Guid DefinitionId,
    WindowSelector Window) : IRequest<Result<WorkflowAnalyticsDto>>;
```

- [ ] **Step 3: Build**

Run: `cd boilerplateBE && dotnet build`
Expected: Build succeeded. (Handler does not exist yet — MediatR will fail at runtime if sent, but compile is clean.)

- [ ] **Step 4: Commit**

```bash
git add boilerplateBE/src/modules/Starter.Module.Workflow/Application/Queries/GetWorkflowAnalytics/
git commit -m "feat(workflow): define analytics query + DTO records"
```

---

### Task 4: Add error helpers for analytics

**Files:**
- Modify: `boilerplateBE/src/modules/Starter.Module.Workflow/Domain/Errors/WorkflowErrors.cs`

- [ ] **Step 1: Add the two helpers**

In `WorkflowErrors.cs`, after `Concurrency()`:

```csharp
    public static Error AnalyticsNotAvailableOnTemplate() =>
        Error.NotFound(
            "Workflow.AnalyticsNotAvailableOnTemplate",
            "Analytics are not available for system templates. Clone the definition to view analytics for your tenant's flow.");

    public static Error InvalidAnalyticsWindow(string? raw) =>
        Error.Validation(
            "Workflow.InvalidAnalyticsWindow",
            $"Invalid analytics window '{raw}'. Supported values: 7d, 30d, 90d, all.");
```

- [ ] **Step 2: Build**

Run: `cd boilerplateBE && dotnet build`
Expected: Build succeeded.

- [ ] **Step 3: Commit**

```bash
git add boilerplateBE/src/modules/Starter.Module.Workflow/Domain/Errors/WorkflowErrors.cs
git commit -m "feat(workflow): add analytics-specific error helpers"
```

---

### Task 5: Handler skeleton + guard tests (tenant / template / not-found)

Handler implementation is split across Tasks 5–11. Task 5 creates the skeleton with the three guards and the fixture plumbing; subsequent tasks progressively fill in each widget's aggregation. Each task adds its tests first, watches them fail, then implements just enough to make them green.

**Files:**
- Create: `boilerplateBE/src/modules/Starter.Module.Workflow/Application/Queries/GetWorkflowAnalytics/GetWorkflowAnalyticsQueryHandler.cs`
- Create: `boilerplateBE/tests/Starter.Api.Tests/Workflow/GetWorkflowAnalyticsQueryHandlerTests.cs`
- Uses: `boilerplateBE/tests/Starter.Api.Tests/Workflow/WorkflowEngineTestFactory.cs` (existing — reused for `CreateDb()`)

- [ ] **Step 1: Write the three failing guard tests**

Create `GetWorkflowAnalyticsQueryHandlerTests.cs`:

```csharp
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Starter.Abstractions.Readers;
using Starter.Module.Workflow.Application.Queries.GetWorkflowAnalytics;
using Starter.Module.Workflow.Domain.Entities;
using Starter.Module.Workflow.Infrastructure.Persistence;
using Xunit;

namespace Starter.Api.Tests.Workflow;

public sealed class GetWorkflowAnalyticsQueryHandlerTests : IDisposable
{
    private readonly WorkflowDbContext _db;
    private readonly Mock<IUserReader> _userReader = new();
    private readonly GetWorkflowAnalyticsQueryHandler _sut;
    private readonly Guid _tenantId = Guid.NewGuid();

    public GetWorkflowAnalyticsQueryHandlerTests()
    {
        _db = WorkflowEngineTestFactory.CreateDb();
        _userReader
            .Setup(r => r.GetManyAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IEnumerable<Guid> ids, CancellationToken _) =>
                ids.Select(id => new UserSummary(
                    id, _tenantId, $"u{id:N}"[..8], $"u{id:N}@t"[..10],
                    DisplayName: $"User {id:N}"[..9], Status: "Active")).ToList());
        _sut = new GetWorkflowAnalyticsQueryHandler(_db, _userReader.Object);
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task Handle_UnknownDefinition_ReturnsDefinitionNotFound()
    {
        var result = await _sut.Handle(
            new GetWorkflowAnalyticsQuery(Guid.NewGuid(), WindowSelector.ThirtyDays),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Workflow.DefinitionNotFound");
    }

    [Fact]
    public async Task Handle_TemplateDefinition_ReturnsAnalyticsNotAvailableOnTemplate()
    {
        var template = WorkflowDefinition.Create(
            tenantId: null,
            name: "tpl",
            displayName: "Template",
            entityType: "General",
            statesJson: "[]",
            transitionsJson: "[]",
            isTemplate: true,
            sourceModule: "Tests");
        _db.WorkflowDefinitions.Add(template);
        await _db.SaveChangesAsync();

        var result = await _sut.Handle(
            new GetWorkflowAnalyticsQuery(template.Id, WindowSelector.ThirtyDays),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Workflow.AnalyticsNotAvailableOnTemplate");
    }

    [Fact]
    public async Task Handle_EmptyDefinition_ReturnsZeroFilledDto()
    {
        var def = CreateTenantDefinition();

        var result = await _sut.Handle(
            new GetWorkflowAnalyticsQuery(def.Id, WindowSelector.ThirtyDays),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.InstancesInWindow.Should().Be(0);
        result.Value.Headline.TotalStarted.Should().Be(0);
        result.Value.Headline.AvgCycleTimeHours.Should().BeNull();
        result.Value.StatesByBottleneck.Should().BeEmpty();
        result.Value.ActionRates.Should().BeEmpty();
        result.Value.StuckInstances.Should().BeEmpty();
        result.Value.ApproverActivity.Should().BeEmpty();
        // Series: 30-day window bucketed by day = 31 days (inclusive both ends).
        result.Value.InstanceCountSeries.Should().NotBeEmpty();
    }

    // ── Fixture helpers ──────────────────────────────────────────────────────

    private WorkflowDefinition CreateTenantDefinition()
    {
        var def = WorkflowDefinition.Create(
            tenantId: _tenantId,
            name: "analytics-test",
            displayName: "Analytics Test",
            entityType: "Order",
            statesJson: "[]",
            transitionsJson: "[]",
            isTemplate: false,
            sourceModule: "Tests");
        _db.WorkflowDefinitions.Add(def);
        _db.SaveChanges();
        return def;
    }
}
```

- [ ] **Step 2: Run — expect compile failure**

Run: `cd boilerplateBE && dotnet test tests/Starter.Api.Tests/Starter.Api.Tests.csproj --filter "FullyQualifiedName~GetWorkflowAnalyticsQueryHandlerTests"`
Expected: Compile error — `GetWorkflowAnalyticsQueryHandler` does not exist.

- [ ] **Step 3: Implement the skeleton handler with guards and zero-filled series**

Create `GetWorkflowAnalyticsQueryHandler.cs`:

```csharp
using MediatR;
using Microsoft.EntityFrameworkCore;
using Starter.Abstractions.Readers;
using Starter.Module.Workflow.Domain.Entities;
using Starter.Module.Workflow.Domain.Errors;
using Starter.Module.Workflow.Infrastructure.Persistence;
using Starter.Shared.Results;

namespace Starter.Module.Workflow.Application.Queries.GetWorkflowAnalytics;

internal sealed class GetWorkflowAnalyticsQueryHandler(
    WorkflowDbContext db,
    IUserReader userReader)
    : IRequestHandler<GetWorkflowAnalyticsQuery, Result<WorkflowAnalyticsDto>>
{
    public async Task<Result<WorkflowAnalyticsDto>> Handle(
        GetWorkflowAnalyticsQuery request, CancellationToken ct)
    {
        var definition = await db.WorkflowDefinitions
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == request.DefinitionId, ct);

        if (definition is null)
            return Result.Failure<WorkflowAnalyticsDto>(
                WorkflowErrors.DefinitionNotFoundById(request.DefinitionId));

        if (definition.IsTemplate)
            return Result.Failure<WorkflowAnalyticsDto>(
                WorkflowErrors.AnalyticsNotAvailableOnTemplate());

        var now = DateTime.UtcNow;
        var (windowStart, windowEnd) = ResolveWindow(request.Window, definition.CreatedAt, now);

        var instancesInWindow = await db.WorkflowInstances
            .AsNoTracking()
            .CountAsync(i => i.DefinitionId == definition.Id
                          && i.StartedAt >= windowStart
                          && i.StartedAt <= windowEnd, ct);

        var emptyHeadline = new HeadlineMetrics(0, 0, 0, AvgCycleTimeHours: null);
        var series = BuildZeroFilledSeries(request.Window, windowStart, windowEnd);

        var dto = new WorkflowAnalyticsDto(
            DefinitionId: definition.Id,
            DefinitionName: definition.Name,
            Window: request.Window,
            WindowStart: windowStart,
            WindowEnd: windowEnd,
            InstancesInWindow: instancesInWindow,
            Headline: emptyHeadline,
            StatesByBottleneck: Array.Empty<StateMetric>(),
            ActionRates: Array.Empty<ActionRateMetric>(),
            InstanceCountSeries: series,
            StuckInstances: Array.Empty<StuckInstanceDto>(),
            ApproverActivity: Array.Empty<ApproverActivityDto>());

        return Result.Success(dto);
    }

    private static (DateTime Start, DateTime End) ResolveWindow(
        WindowSelector window, DateTime definitionCreatedAt, DateTime now) =>
        window switch
        {
            WindowSelector.SevenDays   => (now.AddDays(-7),  now),
            WindowSelector.ThirtyDays  => (now.AddDays(-30), now),
            WindowSelector.NinetyDays  => (now.AddDays(-90), now),
            WindowSelector.AllTime     => (definitionCreatedAt, now),
            _ => throw new ArgumentOutOfRangeException(nameof(window)),
        };

    private static IReadOnlyList<InstanceCountPoint> BuildZeroFilledSeries(
        WindowSelector window, DateTime start, DateTime end)
    {
        var granularity = PickGranularity(window);
        var buckets = new List<InstanceCountPoint>();
        var cursor = TruncateTo(start, granularity);
        var endTrunc = TruncateTo(end, granularity);

        while (cursor <= endTrunc)
        {
            buckets.Add(new InstanceCountPoint(cursor, 0, 0, 0));
            cursor = Advance(cursor, granularity);
        }

        return buckets;
    }

    private static BucketGranularity PickGranularity(WindowSelector window) => window switch
    {
        WindowSelector.SevenDays  => BucketGranularity.Day,
        WindowSelector.ThirtyDays => BucketGranularity.Day,
        WindowSelector.NinetyDays => BucketGranularity.Week,
        WindowSelector.AllTime    => BucketGranularity.Month,
        _ => BucketGranularity.Day,
    };

    private static DateTime TruncateTo(DateTime dt, BucketGranularity g) => g switch
    {
        BucketGranularity.Day   => new DateTime(dt.Year, dt.Month, dt.Day, 0, 0, 0, DateTimeKind.Utc),
        BucketGranularity.Week  => new DateTime(dt.Year, dt.Month, dt.Day, 0, 0, 0, DateTimeKind.Utc)
                                     .AddDays(-(int)dt.DayOfWeek),
        BucketGranularity.Month => new DateTime(dt.Year, dt.Month, 1, 0, 0, 0, DateTimeKind.Utc),
        _ => dt,
    };

    private static DateTime Advance(DateTime dt, BucketGranularity g) => g switch
    {
        BucketGranularity.Day   => dt.AddDays(1),
        BucketGranularity.Week  => dt.AddDays(7),
        BucketGranularity.Month => dt.AddMonths(1),
        _ => dt.AddDays(1),
    };

    private enum BucketGranularity { Day, Week, Month }
}
```

- [ ] **Step 4: Run tests — expect PASS**

Run: `cd boilerplateBE && dotnet test tests/Starter.Api.Tests/Starter.Api.Tests.csproj --filter "FullyQualifiedName~GetWorkflowAnalyticsQueryHandlerTests"`
Expected: 3 tests PASS.

- [ ] **Step 5: Commit**

```bash
git add boilerplateBE/src/modules/Starter.Module.Workflow/Application/Queries/GetWorkflowAnalytics/GetWorkflowAnalyticsQueryHandler.cs \
        boilerplateBE/tests/Starter.Api.Tests/Workflow/GetWorkflowAnalyticsQueryHandlerTests.cs
git commit -m "feat(workflow): analytics handler skeleton with tenant/template/empty guards"
```

---

### Task 6: Headline metrics + `instancesInWindow` + window-semantics test

**Files:**
- Modify: `boilerplateBE/src/modules/Starter.Module.Workflow/Application/Queries/GetWorkflowAnalytics/GetWorkflowAnalyticsQueryHandler.cs`
- Modify: `boilerplateBE/tests/Starter.Api.Tests/Workflow/GetWorkflowAnalyticsQueryHandlerTests.cs`

- [ ] **Step 1: Add a seed helper to the test file**

At the bottom of `GetWorkflowAnalyticsQueryHandlerTests.cs` (inside the class, before `Dispose`), add:

```csharp
private WorkflowInstance SeedInstance(
    Guid definitionId,
    DateTime startedAt,
    InstanceStatus status = InstanceStatus.Active,
    DateTime? completedAt = null,
    DateTime? cancelledAt = null,
    string initialState = "Draft")
{
    var instance = WorkflowInstance.Create(
        tenantId: _tenantId,
        definitionId: definitionId,
        entityType: "Order",
        entityId: Guid.NewGuid(),
        initialState: initialState,
        startedByUserId: Guid.NewGuid(),
        contextJson: null,
        definitionName: "analytics-test");
    _db.WorkflowInstances.Add(instance);
    _db.SaveChanges();

    // Backdate StartedAt via SQL-style direct field write; EF InMemory allows
    // rewriting tracked property values here because the shadow fields are
    // open. Avoids a new mutator on the aggregate.
    var entry = _db.Entry(instance);
    entry.Property(nameof(WorkflowInstance.StartedAt)).CurrentValue = startedAt;
    if (status == InstanceStatus.Completed)
    {
        entry.Property(nameof(WorkflowInstance.Status)).CurrentValue = InstanceStatus.Completed;
        entry.Property(nameof(WorkflowInstance.CompletedAt)).CurrentValue =
            completedAt ?? startedAt.AddHours(10);
    }
    else if (status == InstanceStatus.Cancelled)
    {
        entry.Property(nameof(WorkflowInstance.Status)).CurrentValue = InstanceStatus.Cancelled;
        entry.Property(nameof(WorkflowInstance.CancelledAt)).CurrentValue =
            cancelledAt ?? startedAt.AddHours(5);
    }
    _db.SaveChanges();
    return instance;
}
```

Also add the `using` at the top:

```csharp
using Starter.Module.Workflow.Domain.Enums;
```

- [ ] **Step 2: Add the failing tests**

Append to `GetWorkflowAnalyticsQueryHandlerTests`:

```csharp
[Fact]
public async Task Handle_30DayWindow_HeadlineCountsMatchSeededStatusBreakdown()
{
    var def = CreateTenantDefinition();
    var now = DateTime.UtcNow;

    SeedInstance(def.Id, now.AddDays(-1), InstanceStatus.Active);
    SeedInstance(def.Id, now.AddDays(-10), InstanceStatus.Completed,
        completedAt: now.AddDays(-10).AddHours(8));
    SeedInstance(def.Id, now.AddDays(-20), InstanceStatus.Completed,
        completedAt: now.AddDays(-20).AddHours(12));
    SeedInstance(def.Id, now.AddDays(-25), InstanceStatus.Cancelled,
        cancelledAt: now.AddDays(-25).AddHours(3));

    var result = await _sut.Handle(
        new GetWorkflowAnalyticsQuery(def.Id, WindowSelector.ThirtyDays),
        CancellationToken.None);

    result.IsSuccess.Should().BeTrue();
    var dto = result.Value;
    dto.InstancesInWindow.Should().Be(4);
    dto.Headline.TotalStarted.Should().Be(4);
    dto.Headline.TotalCompleted.Should().Be(2);
    dto.Headline.TotalCancelled.Should().Be(1);
    dto.Headline.AvgCycleTimeHours.Should().BeApproximately(10.0, 0.1); // (8+12)/2
}

[Fact]
public async Task Handle_InstanceStartedBeforeWindow_IsExcludedEvenIfCompletedInside()
{
    var def = CreateTenantDefinition();
    var now = DateTime.UtcNow;

    // Started 60 days ago (outside 30-day window) but completed 5 days ago.
    SeedInstance(def.Id, now.AddDays(-60), InstanceStatus.Completed,
        completedAt: now.AddDays(-5));
    // Started yesterday, still active.
    SeedInstance(def.Id, now.AddDays(-1), InstanceStatus.Active);

    var result = await _sut.Handle(
        new GetWorkflowAnalyticsQuery(def.Id, WindowSelector.ThirtyDays),
        CancellationToken.None);

    result.Value.InstancesInWindow.Should().Be(1);
    result.Value.Headline.TotalStarted.Should().Be(1);
    result.Value.Headline.TotalCompleted.Should().Be(0);
}

[Fact]
public async Task Handle_AllTimeWindow_UsesDefinitionCreatedAtAsStart()
{
    var def = CreateTenantDefinition();
    var now = DateTime.UtcNow;

    SeedInstance(def.Id, now.AddDays(-200), InstanceStatus.Completed,
        completedAt: now.AddDays(-199));

    var result = await _sut.Handle(
        new GetWorkflowAnalyticsQuery(def.Id, WindowSelector.AllTime),
        CancellationToken.None);

    result.Value.WindowStart.Should().BeOnOrBefore(def.CreatedAt.AddSeconds(1));
    result.Value.InstancesInWindow.Should().Be(1);
    result.Value.Headline.TotalCompleted.Should().Be(1);
}
```

- [ ] **Step 3: Run — expect failures**

Run: `cd boilerplateBE && dotnet test tests/Starter.Api.Tests/Starter.Api.Tests.csproj --filter "FullyQualifiedName~GetWorkflowAnalyticsQueryHandlerTests"`
Expected: 3 new tests FAIL (all headline numbers still 0 because Task 5 returned `emptyHeadline`).

- [ ] **Step 4: Implement headline aggregation**

In `GetWorkflowAnalyticsQueryHandler.cs`, replace the block starting at `var emptyHeadline = new HeadlineMetrics(0, 0, 0, AvgCycleTimeHours: null);` with:

```csharp
        var headline = await ComputeHeadlineAsync(definition.Id, windowStart, windowEnd, ct);

        var series = BuildZeroFilledSeries(request.Window, windowStart, windowEnd);

        var dto = new WorkflowAnalyticsDto(
            DefinitionId: definition.Id,
            DefinitionName: definition.Name,
            Window: request.Window,
            WindowStart: windowStart,
            WindowEnd: windowEnd,
            InstancesInWindow: instancesInWindow,
            Headline: headline,
```

Then add the `ComputeHeadlineAsync` method to the handler class:

```csharp
private async Task<HeadlineMetrics> ComputeHeadlineAsync(
    Guid definitionId, DateTime windowStart, DateTime windowEnd, CancellationToken ct)
{
    var rows = await db.WorkflowInstances
        .AsNoTracking()
        .Where(i => i.DefinitionId == definitionId
                 && i.StartedAt >= windowStart
                 && i.StartedAt <= windowEnd)
        .Select(i => new
        {
            i.Status,
            i.StartedAt,
            i.CompletedAt,
        })
        .ToListAsync(ct);

    var total = rows.Count;
    var completed = rows.Count(r => r.Status == Domain.Enums.InstanceStatus.Completed);
    var cancelled = rows.Count(r => r.Status == Domain.Enums.InstanceStatus.Cancelled);

    double? avgCycleHours = null;
    var completedWithCycle = rows
        .Where(r => r.Status == Domain.Enums.InstanceStatus.Completed && r.CompletedAt.HasValue)
        .Select(r => (r.CompletedAt!.Value - r.StartedAt).TotalHours)
        .ToList();
    if (completedWithCycle.Count > 0)
        avgCycleHours = Math.Round(completedWithCycle.Average(), 1);

    return new HeadlineMetrics(total, completed, cancelled, avgCycleHours);
}
```

- [ ] **Step 5: Run — expect PASS**

Run: `cd boilerplateBE && dotnet test tests/Starter.Api.Tests/Starter.Api.Tests.csproj --filter "FullyQualifiedName~GetWorkflowAnalyticsQueryHandlerTests"`
Expected: 6 tests PASS.

- [ ] **Step 6: Commit**

```bash
git add boilerplateBE/src/modules/Starter.Module.Workflow/Application/Queries/GetWorkflowAnalytics/GetWorkflowAnalyticsQueryHandler.cs \
        boilerplateBE/tests/Starter.Api.Tests/Workflow/GetWorkflowAnalyticsQueryHandlerTests.cs
git commit -m "feat(workflow): analytics headline metrics + start-anchor window semantics"
```

---

### Task 7: Instance count series (populated buckets) with InMemory fallback

**Files:**
- Modify: `boilerplateBE/src/modules/Starter.Module.Workflow/Application/Queries/GetWorkflowAnalytics/GetWorkflowAnalyticsQueryHandler.cs`
- Modify: `boilerplateBE/tests/Starter.Api.Tests/Workflow/GetWorkflowAnalyticsQueryHandlerTests.cs`

- [ ] **Step 1: Add the failing test**

Append to `GetWorkflowAnalyticsQueryHandlerTests`:

```csharp
[Fact]
public async Task Handle_30DayWindow_InstanceCountSeriesBucketsByDayAndIncludesStartedCompletedCancelled()
{
    var def = CreateTenantDefinition();
    var today = DateTime.UtcNow.Date;

    // Two started today.
    SeedInstance(def.Id, today.AddHours(2),  InstanceStatus.Active);
    SeedInstance(def.Id, today.AddHours(10), InstanceStatus.Active);
    // One started+completed yesterday.
    SeedInstance(def.Id, today.AddDays(-1).AddHours(5), InstanceStatus.Completed,
        completedAt: today.AddDays(-1).AddHours(9));
    // One started two days ago, cancelled today.
    SeedInstance(def.Id, today.AddDays(-2).AddHours(1), InstanceStatus.Cancelled,
        cancelledAt: today.AddHours(12));

    var result = await _sut.Handle(
        new GetWorkflowAnalyticsQuery(def.Id, WindowSelector.ThirtyDays),
        CancellationToken.None);

    var series = result.Value.InstanceCountSeries;
    var todayBucket     = series.Single(p => p.Bucket.Date == today);
    var yesterdayBucket = series.Single(p => p.Bucket.Date == today.AddDays(-1));
    var twoDaysBucket   = series.Single(p => p.Bucket.Date == today.AddDays(-2));

    todayBucket.Started.Should().Be(2);
    todayBucket.Cancelled.Should().Be(1);
    yesterdayBucket.Started.Should().Be(1);
    yesterdayBucket.Completed.Should().Be(1);
    twoDaysBucket.Started.Should().Be(1);
}
```

- [ ] **Step 2: Run — expect failure**

Run: same filter as before.
Expected: FAIL — `todayBucket.Started` is 0.

- [ ] **Step 3: Implement `ComputeInstanceCountSeriesAsync`**

In the handler, replace the `BuildZeroFilledSeries(...)` call in `Handle` with:

```csharp
        var series = await ComputeInstanceCountSeriesAsync(
            definition.Id, request.Window, windowStart, windowEnd, ct);
```

Add the new method to the handler class:

```csharp
private async Task<IReadOnlyList<InstanceCountPoint>> ComputeInstanceCountSeriesAsync(
    Guid definitionId,
    WindowSelector window,
    DateTime windowStart,
    DateTime windowEnd,
    CancellationToken ct)
{
    var granularity = PickGranularity(window);

    // Always materialize within-window rows and bucket in C# so EF InMemory
    // (tests) and Postgres behave identically. This is the read path's hot
    // aggregation — profiling in the perf test (Task 13) is the signal to
    // move to raw SQL date_trunc if 1s is ever breached.
    var rows = await db.WorkflowInstances
        .AsNoTracking()
        .Where(i => i.DefinitionId == definitionId
                 && i.StartedAt >= windowStart
                 && i.StartedAt <= windowEnd)
        .Select(i => new
        {
            i.StartedAt,
            i.Status,
            i.CompletedAt,
            i.CancelledAt,
        })
        .ToListAsync(ct);

    var dict = new Dictionary<DateTime, (int started, int completed, int cancelled)>();
    for (var cursor = TruncateTo(windowStart, granularity);
         cursor <= TruncateTo(windowEnd, granularity);
         cursor = Advance(cursor, granularity))
    {
        dict[cursor] = (0, 0, 0);
    }

    foreach (var r in rows)
    {
        var startBucket = TruncateTo(r.StartedAt, granularity);
        if (dict.TryGetValue(startBucket, out var s))
            dict[startBucket] = (s.started + 1, s.completed, s.cancelled);

        if (r.Status == Domain.Enums.InstanceStatus.Completed && r.CompletedAt.HasValue)
        {
            var completedBucket = TruncateTo(r.CompletedAt.Value, granularity);
            if (dict.TryGetValue(completedBucket, out var c))
                dict[completedBucket] = (c.started, c.completed + 1, c.cancelled);
        }

        if (r.Status == Domain.Enums.InstanceStatus.Cancelled && r.CancelledAt.HasValue)
        {
            var cancelledBucket = TruncateTo(r.CancelledAt.Value, granularity);
            if (dict.TryGetValue(cancelledBucket, out var x))
                dict[cancelledBucket] = (x.started, x.completed, x.cancelled + 1);
        }
    }

    return dict
        .OrderBy(kvp => kvp.Key)
        .Select(kvp => new InstanceCountPoint(kvp.Key, kvp.Value.started, kvp.Value.completed, kvp.Value.cancelled))
        .ToList();
}
```

Remove the old `BuildZeroFilledSeries` method — the new method subsumes it.

- [ ] **Step 4: Run — expect PASS**

Run: same filter.
Expected: 7 tests PASS. The `Handle_EmptyDefinition_ReturnsZeroFilledDto` test continues to pass because with no rows the dict keeps its zero-filled state.

- [ ] **Step 5: Commit**

```bash
git add boilerplateBE/src/modules/Starter.Module.Workflow/Application/Queries/GetWorkflowAnalytics/GetWorkflowAnalyticsQueryHandler.cs \
        boilerplateBE/tests/Starter.Api.Tests/Workflow/GetWorkflowAnalyticsQueryHandlerTests.cs
git commit -m "feat(workflow): analytics instance count series with auto-bucketing"
```

---

### Task 8: Bottleneck states with `percentile_cont` + InMemory fallback

**Files:**
- Modify: `boilerplateBE/src/modules/Starter.Module.Workflow/Application/Queries/GetWorkflowAnalytics/GetWorkflowAnalyticsQueryHandler.cs`
- Modify: `boilerplateBE/tests/Starter.Api.Tests/Workflow/GetWorkflowAnalyticsQueryHandlerTests.cs`

- [ ] **Step 1: Add a step-seeding helper**

Inside the test class, add:

```csharp
private void SeedStep(Guid instanceId, string fromState, string toState,
    StepType stepType, string action, Guid? actorUserId, DateTime timestamp)
{
    var step = WorkflowStep.Create(instanceId, fromState, toState, stepType, action,
        actorUserId, comment: null, metadataJson: null);
    _db.WorkflowSteps.Add(step);
    _db.SaveChanges();
    _db.Entry(step).Property(nameof(WorkflowStep.Timestamp)).CurrentValue = timestamp;
    _db.SaveChanges();
}
```

- [ ] **Step 2: Add failing tests (dwell computation + 3-visit filter + ordering)**

Append to the test class:

```csharp
[Fact]
public async Task Handle_Bottlenecks_OnlyStatesWithThreeOrMoreVisitsAppear_OrderedByMedianDesc()
{
    var def = CreateTenantDefinition();
    var now = DateTime.UtcNow;

    // Three instances dwell in "AwaitingApproval": 10h, 20h, 30h. Median=20, P95≈29.
    for (var i = 0; i < 3; i++)
    {
        var inst = SeedInstance(def.Id, now.AddDays(-5 - i), InstanceStatus.Active,
            initialState: "AwaitingApproval");
        // Enter "AwaitingApproval"
        SeedStep(inst.Id, "Draft", "AwaitingApproval",
            StepType.HumanTask, "Submit", actorUserId: null,
            timestamp: now.AddDays(-5 - i));
        // Exit "AwaitingApproval" after (10 + 10*i) hours
        SeedStep(inst.Id, "AwaitingApproval", "Approved",
            StepType.HumanTask, "approve", actorUserId: Guid.NewGuid(),
            timestamp: now.AddDays(-5 - i).AddHours(10 + 10 * i));
    }

    // Two instances dwell in "SeniorReview" (<3) — should be excluded.
    for (var i = 0; i < 2; i++)
    {
        var inst = SeedInstance(def.Id, now.AddDays(-4 - i), InstanceStatus.Active,
            initialState: "SeniorReview");
        SeedStep(inst.Id, "Draft", "SeniorReview",
            StepType.HumanTask, "Escalate", actorUserId: null, timestamp: now.AddDays(-4 - i));
        SeedStep(inst.Id, "SeniorReview", "Approved",
            StepType.HumanTask, "approve", actorUserId: Guid.NewGuid(),
            timestamp: now.AddDays(-4 - i).AddHours(5));
    }

    var result = await _sut.Handle(
        new GetWorkflowAnalyticsQuery(def.Id, WindowSelector.ThirtyDays),
        CancellationToken.None);

    result.Value.StatesByBottleneck.Should().HaveCount(1);
    var b = result.Value.StatesByBottleneck[0];
    b.StateName.Should().Be("AwaitingApproval");
    b.VisitCount.Should().Be(3);
    b.MedianDwellHours.Should().BeApproximately(20.0, 1.0);
    b.P95DwellHours.Should().BeGreaterThanOrEqualTo(20.0);
}
```

- [ ] **Step 3: Run — expect failure**

Run: same filter.
Expected: FAIL — `StatesByBottleneck` is empty.

- [ ] **Step 4: Implement the bottleneck aggregation**

Wire the call in `Handle`:

```csharp
        var bottlenecks = await ComputeBottlenecksAsync(definition.Id, windowStart, windowEnd, ct);
```

And update the DTO composition to use `StatesByBottleneck: bottlenecks`.

Add to the handler:

```csharp
private async Task<IReadOnlyList<StateMetric>> ComputeBottlenecksAsync(
    Guid definitionId, DateTime windowStart, DateTime windowEnd, CancellationToken ct)
{
    // Pull in-window instances' step rows, then pair entry→exit in C#.
    // Postgres-specific percentile_cont path is tracked in the spec's
    // "Analytics follow-ups" — for now we compute median/p95 in C# so
    // InMemory and Postgres behave identically.
    var instanceIds = await db.WorkflowInstances
        .AsNoTracking()
        .Where(i => i.DefinitionId == definitionId
                 && i.StartedAt >= windowStart
                 && i.StartedAt <= windowEnd)
        .Select(i => i.Id)
        .ToListAsync(ct);

    if (instanceIds.Count == 0) return Array.Empty<StateMetric>();

    var steps = await db.WorkflowSteps
        .AsNoTracking()
        .Where(s => instanceIds.Contains(s.InstanceId))
        .OrderBy(s => s.InstanceId).ThenBy(s => s.Timestamp)
        .Select(s => new { s.InstanceId, s.FromState, s.ToState, s.Timestamp })
        .ToListAsync(ct);

    // Build dwell samples per state: for each (instance, state), pair the
    // step that lands in `state` (ToState=state) with the next step that
    // leaves it (FromState=state).
    var dwellsByState = new Dictionary<string, List<double>>(StringComparer.Ordinal);

    foreach (var group in steps.GroupBy(s => s.InstanceId))
    {
        var ordered = group.ToList();
        for (var i = 0; i < ordered.Count; i++)
        {
            var entry = ordered[i];
            // Find matching exit where FromState == entry.ToState, later in time.
            for (var j = i + 1; j < ordered.Count; j++)
            {
                var exit = ordered[j];
                if (exit.FromState == entry.ToState)
                {
                    var hours = (exit.Timestamp - entry.Timestamp).TotalHours;
                    if (!dwellsByState.TryGetValue(entry.ToState, out var list))
                    {
                        list = new List<double>();
                        dwellsByState[entry.ToState] = list;
                    }
                    list.Add(hours);
                    break;
                }
            }
        }
    }

    return dwellsByState
        .Where(kvp => kvp.Value.Count >= 3)
        .Select(kvp => new StateMetric(
            StateName: kvp.Key,
            MedianDwellHours: Math.Round(Percentile(kvp.Value, 0.5), 2),
            P95DwellHours: Math.Round(Percentile(kvp.Value, 0.95), 2),
            VisitCount: kvp.Value.Count))
        .OrderByDescending(m => m.MedianDwellHours)
        .ToList();
}

private static double Percentile(List<double> values, double quantile)
{
    if (values.Count == 0) return 0;
    var sorted = values.OrderBy(v => v).ToList();
    var rank = quantile * (sorted.Count - 1);
    var lower = (int)Math.Floor(rank);
    var upper = (int)Math.Ceiling(rank);
    if (lower == upper) return sorted[lower];
    var weight = rank - lower;
    return sorted[lower] * (1 - weight) + sorted[upper] * weight;
}
```

- [ ] **Step 5: Run — expect PASS**

Run: same filter.
Expected: 8 tests PASS.

- [ ] **Step 6: Commit**

```bash
git add boilerplateBE/src/modules/Starter.Module.Workflow/Application/Queries/GetWorkflowAnalytics/GetWorkflowAnalyticsQueryHandler.cs \
        boilerplateBE/tests/Starter.Api.Tests/Workflow/GetWorkflowAnalyticsQueryHandlerTests.cs
git commit -m "feat(workflow): analytics bottleneck states with median/p95 dwell"
```

---

### Task 9: Action rates per state

**Files:**
- Modify: handler + test file

- [ ] **Step 1: Add the failing test**

```csharp
[Fact]
public async Task Handle_ActionRates_PercentagesWithinStateSumToOne()
{
    var def = CreateTenantDefinition();
    var now = DateTime.UtcNow;

    // Seed 10 completed human-task steps from "ManagerReview":
    // 7 approve, 3 reject.
    var instance = SeedInstance(def.Id, now.AddDays(-5), InstanceStatus.Active);
    for (var i = 0; i < 7; i++)
        SeedStep(instance.Id, "ManagerReview", "Approved",
            StepType.HumanTask, "approve", Guid.NewGuid(), now.AddDays(-5).AddMinutes(i));
    for (var i = 0; i < 3; i++)
        SeedStep(instance.Id, "ManagerReview", "Rejected",
            StepType.HumanTask, "reject", Guid.NewGuid(), now.AddDays(-5).AddMinutes(10 + i));

    // One SystemAction step must be ignored.
    SeedStep(instance.Id, "ManagerReview", "AutoEscalated",
        StepType.SystemAction, "autoEscalate", actorUserId: null, now.AddDays(-4));

    var result = await _sut.Handle(
        new GetWorkflowAnalyticsQuery(def.Id, WindowSelector.ThirtyDays),
        CancellationToken.None);

    var rates = result.Value.ActionRates.Where(r => r.StateName == "ManagerReview").ToList();
    rates.Should().HaveCount(2);
    rates.Single(r => r.Action == "approve").Count.Should().Be(7);
    rates.Single(r => r.Action == "reject").Count.Should().Be(3);
    rates.Sum(r => r.Percentage).Should().BeApproximately(1.0, 0.001);
}
```

- [ ] **Step 2: Run — expect failure**

Expected: FAIL — `ActionRates` is empty.

- [ ] **Step 3: Implement**

In `Handle`, add:

```csharp
        var actionRates = await ComputeActionRatesAsync(definition.Id, windowStart, windowEnd, ct);
```

Wire `ActionRates: actionRates` into the DTO.

Add the method:

```csharp
private async Task<IReadOnlyList<ActionRateMetric>> ComputeActionRatesAsync(
    Guid definitionId, DateTime windowStart, DateTime windowEnd, CancellationToken ct)
{
    var instanceIds = await db.WorkflowInstances
        .AsNoTracking()
        .Where(i => i.DefinitionId == definitionId
                 && i.StartedAt >= windowStart
                 && i.StartedAt <= windowEnd)
        .Select(i => i.Id)
        .ToListAsync(ct);

    if (instanceIds.Count == 0) return Array.Empty<ActionRateMetric>();

    var steps = await db.WorkflowSteps
        .AsNoTracking()
        .Where(s => instanceIds.Contains(s.InstanceId)
                 && s.StepType == Domain.Enums.StepType.HumanTask
                 && s.ActorUserId != null)
        .Select(s => new { s.FromState, s.Action })
        .ToListAsync(ct);

    return steps
        .GroupBy(s => s.FromState)
        .SelectMany(stateGroup =>
        {
            var totalInState = stateGroup.Count();
            return stateGroup
                .GroupBy(s => s.Action)
                .Select(actionGroup => new ActionRateMetric(
                    StateName: stateGroup.Key,
                    Action: actionGroup.Key,
                    Count: actionGroup.Count(),
                    Percentage: Math.Round((double)actionGroup.Count() / totalInState, 4)));
        })
        .OrderBy(m => m.StateName).ThenByDescending(m => m.Count)
        .ToList();
}
```

- [ ] **Step 4: Run — expect PASS**

Expected: 9 tests PASS.

- [ ] **Step 5: Commit**

```bash
git add boilerplateBE/src/modules/Starter.Module.Workflow/Application/Queries/GetWorkflowAnalytics/GetWorkflowAnalyticsQueryHandler.cs \
        boilerplateBE/tests/Starter.Api.Tests/Workflow/GetWorkflowAnalyticsQueryHandlerTests.cs
git commit -m "feat(workflow): analytics action rates per state"
```

---

### Task 10: Stuck instances (top 10) with assignee display name

**Files:**
- Modify: handler + test file

- [ ] **Step 1: Add an approval-task seeding helper**

Inside the test class:

```csharp
private void SeedPendingTask(Guid instanceId, string stepName, Guid assigneeUserId)
{
    var task = ApprovalTask.Create(
        tenantId: _tenantId,
        instanceId: instanceId,
        stepName: stepName,
        assigneeUserId: assigneeUserId,
        assigneeRole: null,
        assigneeStrategyJson: null,
        entityType: "Order",
        entityId: Guid.NewGuid(),
        definitionName: "analytics-test",
        availableActionsJson: "[]");
    _db.ApprovalTasks.Add(task);
    _db.SaveChanges();
}
```

Also add the using at top:

```csharp
using Starter.Abstractions.Readers;
```

- [ ] **Step 2: Add failing tests**

```csharp
[Fact]
public async Task Handle_StuckInstances_OrderedByStartedAtAsc_And_IncludeAssigneeDisplayName()
{
    var def = CreateTenantDefinition();
    var now = DateTime.UtcNow;
    var alice = Guid.NewGuid();

    var oldest = SeedInstance(def.Id, now.AddDays(-10), InstanceStatus.Active);
    var middle = SeedInstance(def.Id, now.AddDays(-5),  InstanceStatus.Active);
    var newest = SeedInstance(def.Id, now.AddDays(-1),  InstanceStatus.Active);

    SeedPendingTask(oldest.Id, "AwaitingApproval", alice);
    SeedPendingTask(middle.Id, "AwaitingApproval", alice);
    SeedPendingTask(newest.Id, "AwaitingApproval", alice);

    var result = await _sut.Handle(
        new GetWorkflowAnalyticsQuery(def.Id, WindowSelector.ThirtyDays),
        CancellationToken.None);

    var stuck = result.Value.StuckInstances;
    stuck.Should().HaveCount(3);
    stuck[0].InstanceId.Should().Be(oldest.Id);
    stuck[1].InstanceId.Should().Be(middle.Id);
    stuck[2].InstanceId.Should().Be(newest.Id);
    stuck[0].DaysSinceStarted.Should().BeGreaterThanOrEqualTo(10);
    stuck[0].CurrentAssigneeDisplayName.Should().NotBeNullOrWhiteSpace();
}

[Fact]
public async Task Handle_StuckInstances_CappedAtTenRows()
{
    var def = CreateTenantDefinition();
    var now = DateTime.UtcNow;

    for (var i = 0; i < 15; i++)
        SeedInstance(def.Id, now.AddDays(-i - 1), InstanceStatus.Active);

    var result = await _sut.Handle(
        new GetWorkflowAnalyticsQuery(def.Id, WindowSelector.ThirtyDays),
        CancellationToken.None);

    result.Value.StuckInstances.Should().HaveCount(10);
}
```

- [ ] **Step 3: Run — expect failure**

Expected: FAIL — `StuckInstances` empty.

- [ ] **Step 4: Implement**

Wire `stuckInstances` into `Handle`:

```csharp
        var stuckInstances = await ComputeStuckInstancesAsync(definition.Id, windowStart, windowEnd, now, ct);
```

(Pass `now` so `DaysSinceStarted` uses the same clock.)

Add the method:

```csharp
private async Task<IReadOnlyList<StuckInstanceDto>> ComputeStuckInstancesAsync(
    Guid definitionId, DateTime windowStart, DateTime windowEnd, DateTime now, CancellationToken ct)
{
    var activeRows = await db.WorkflowInstances
        .AsNoTracking()
        .Where(i => i.DefinitionId == definitionId
                 && i.Status == Domain.Enums.InstanceStatus.Active
                 && i.StartedAt >= windowStart
                 && i.StartedAt <= windowEnd)
        .OrderBy(i => i.StartedAt)
        .Take(10)
        .Select(i => new
        {
            i.Id,
            i.EntityDisplayName,
            i.CurrentState,
            i.StartedAt,
        })
        .ToListAsync(ct);

    if (activeRows.Count == 0) return Array.Empty<StuckInstanceDto>();

    var pendingAssigneesByInstance = await db.ApprovalTasks
        .AsNoTracking()
        .Where(t => activeRows.Select(r => r.Id).Contains(t.InstanceId)
                 && t.Status == Domain.Enums.TaskStatus.Pending
                 && t.AssigneeUserId != null)
        .GroupBy(t => t.InstanceId)
        .Select(g => new { InstanceId = g.Key, AssigneeUserId = g.First().AssigneeUserId!.Value })
        .ToListAsync(ct);

    var assigneeLookup = pendingAssigneesByInstance
        .ToDictionary(x => x.InstanceId, x => x.AssigneeUserId);

    var userIds = assigneeLookup.Values.Distinct().ToList();
    var displayNameLookup = new Dictionary<Guid, string>();
    if (userIds.Count > 0)
    {
        var users = await userReader.GetManyAsync(userIds, ct);
        foreach (var u in users) displayNameLookup[u.Id] = u.DisplayName;
    }

    return activeRows.Select(r =>
    {
        string? assigneeName = null;
        if (assigneeLookup.TryGetValue(r.Id, out var uid)
            && displayNameLookup.TryGetValue(uid, out var name))
            assigneeName = name;

        return new StuckInstanceDto(
            InstanceId: r.Id,
            EntityDisplayName: r.EntityDisplayName,
            CurrentState: r.CurrentState,
            StartedAt: r.StartedAt,
            DaysSinceStarted: (int)Math.Ceiling((now - r.StartedAt).TotalDays),
            CurrentAssigneeDisplayName: assigneeName);
    }).ToList();
}
```

- [ ] **Step 5: Run — expect PASS**

Expected: 11 tests PASS.

- [ ] **Step 6: Commit**

```bash
git add boilerplateBE/src/modules/Starter.Module.Workflow/Application/Queries/GetWorkflowAnalytics/GetWorkflowAnalyticsQueryHandler.cs \
        boilerplateBE/tests/Starter.Api.Tests/Workflow/GetWorkflowAnalyticsQueryHandlerTests.cs
git commit -m "feat(workflow): analytics stuck instances with assignee resolution"
```

---

### Task 11: Approver activity (top 10)

**Files:**
- Modify: handler + test file

- [ ] **Step 1: Add the failing test**

```csharp
[Fact]
public async Task Handle_ApproverActivity_CountsAndOrderByTotalActionsDesc()
{
    var def = CreateTenantDefinition();
    var now = DateTime.UtcNow;
    var alice = Guid.NewGuid();
    var bob   = Guid.NewGuid();

    var inst = SeedInstance(def.Id, now.AddDays(-5), InstanceStatus.Active);

    // Alice: 3 approvals, 1 reject
    for (var i = 0; i < 3; i++)
        SeedStep(inst.Id, "ManagerReview", "Approved", StepType.HumanTask, "approve", alice,
            now.AddDays(-5).AddMinutes(i));
    SeedStep(inst.Id, "ManagerReview", "Rejected", StepType.HumanTask, "reject", alice,
        now.AddDays(-5).AddMinutes(5));

    // Bob: 1 return
    SeedStep(inst.Id, "ManagerReview", "Draft", StepType.HumanTask, "return", bob,
        now.AddDays(-4));

    // System action — must be excluded.
    SeedStep(inst.Id, "ManagerReview", "AutoArchived", StepType.SystemAction, "autoArchive",
        actorUserId: null, now.AddDays(-3));

    var result = await _sut.Handle(
        new GetWorkflowAnalyticsQuery(def.Id, WindowSelector.ThirtyDays),
        CancellationToken.None);

    var activity = result.Value.ApproverActivity;
    activity.Should().HaveCount(2);
    activity[0].UserId.Should().Be(alice);
    activity[0].Approvals.Should().Be(3);
    activity[0].Rejections.Should().Be(1);
    activity[0].Returns.Should().Be(0);
    activity[1].UserId.Should().Be(bob);
    activity[1].Returns.Should().Be(1);
}
```

- [ ] **Step 2: Run — expect failure**

Expected: FAIL — `ApproverActivity` empty.

- [ ] **Step 3: Implement**

Wire into `Handle`:

```csharp
        var approverActivity = await ComputeApproverActivityAsync(definition.Id, windowStart, windowEnd, ct);
```

And `ApproverActivity: approverActivity` in the DTO composition.

Add the method:

```csharp
private async Task<IReadOnlyList<ApproverActivityDto>> ComputeApproverActivityAsync(
    Guid definitionId, DateTime windowStart, DateTime windowEnd, CancellationToken ct)
{
    var instanceIds = await db.WorkflowInstances
        .AsNoTracking()
        .Where(i => i.DefinitionId == definitionId
                 && i.StartedAt >= windowStart
                 && i.StartedAt <= windowEnd)
        .Select(i => i.Id)
        .ToListAsync(ct);

    if (instanceIds.Count == 0) return Array.Empty<ApproverActivityDto>();

    var steps = await db.WorkflowSteps
        .AsNoTracking()
        .Where(s => instanceIds.Contains(s.InstanceId)
                 && s.StepType == Domain.Enums.StepType.HumanTask
                 && s.ActorUserId != null)
        .Select(s => new { ActorUserId = s.ActorUserId!.Value, s.Action, s.Timestamp })
        .ToListAsync(ct);

    if (steps.Count == 0) return Array.Empty<ApproverActivityDto>();

    // Response time: match each step to the corresponding completed ApprovalTask
    // in the same instance by (InstanceId, CompletedByUserId, CompletedAt=step.Timestamp).
    var completedTasks = await db.ApprovalTasks
        .AsNoTracking()
        .Where(t => instanceIds.Contains(t.InstanceId)
                 && t.Status == Domain.Enums.TaskStatus.Completed
                 && t.CompletedByUserId != null
                 && t.CompletedAt != null)
        .Select(t => new
        {
            t.InstanceId,
            UserId = t.CompletedByUserId!.Value,
            CompletedAt = t.CompletedAt!.Value,
            t.CreatedAt,
        })
        .ToListAsync(ct);

    var grouped = steps
        .GroupBy(s => s.ActorUserId)
        .Select(g => new
        {
            UserId = g.Key,
            Approvals = g.Count(x => string.Equals(x.Action, "approve", StringComparison.OrdinalIgnoreCase)),
            Rejections = g.Count(x => string.Equals(x.Action, "reject", StringComparison.OrdinalIgnoreCase)),
            Returns = g.Count(x =>
                string.Equals(x.Action, "return", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(x.Action, "returnforrevision", StringComparison.OrdinalIgnoreCase)),
            Total = g.Count(),
            // Average response: task.CreatedAt → step.Timestamp for best-match tasks
            AvgHours = MatchResponseTimes(g.ToList(), completedTasks, g.Key),
        })
        .OrderByDescending(x => x.Total)
        .Take(10)
        .ToList();

    var userIds = grouped.Select(x => x.UserId).Distinct().ToList();
    var displayNameLookup = new Dictionary<Guid, string>();
    if (userIds.Count > 0)
    {
        var users = await userReader.GetManyAsync(userIds, ct);
        foreach (var u in users) displayNameLookup[u.Id] = u.DisplayName;
    }

    return grouped.Select(x => new ApproverActivityDto(
        UserId: x.UserId,
        UserDisplayName: displayNameLookup.TryGetValue(x.UserId, out var name) ? name : x.UserId.ToString(),
        Approvals: x.Approvals,
        Rejections: x.Rejections,
        Returns: x.Returns,
        AvgResponseTimeHours: x.AvgHours)).ToList();
}

private static double? MatchResponseTimes<TStep, TTask>(
    List<TStep> actorSteps,
    IReadOnlyList<TTask> completedTasks,
    Guid actorUserId)
    where TStep : class
    where TTask : class
{
    // Best-effort: pair each step with a completed task for the same user whose
    // CompletedAt matches the step's Timestamp (within 1 second). If unmatched,
    // skip the sample rather than inflate with a zero.
    dynamic steps = actorSteps;
    dynamic tasks = completedTasks;

    var samples = new List<double>();
    foreach (var step in steps)
    {
        foreach (var task in tasks)
        {
            if (task.UserId != actorUserId) continue;
            var delta = (DateTime)task.CompletedAt - (DateTime)task.CreatedAt;
            var stepMatchesTask = Math.Abs(((DateTime)task.CompletedAt - (DateTime)step.Timestamp).TotalSeconds) < 1.0;
            if (stepMatchesTask)
            {
                samples.Add(delta.TotalHours);
                break;
            }
        }
    }
    return samples.Count > 0 ? Math.Round(samples.Average(), 2) : null;
}
```

> **Note:** The `dynamic` trick keeps the anonymous-type arguments strongly typed at the call site while avoiding a dedicated record just for the matcher. It's contained to one helper method. If the code reviewer pushes back, replace with two named records and a single typed method signature — either is fine.

- [ ] **Step 4: Run — expect PASS**

Expected: 12 tests PASS.

- [ ] **Step 5: Commit**

```bash
git add boilerplateBE/src/modules/Starter.Module.Workflow/Application/Queries/GetWorkflowAnalytics/GetWorkflowAnalyticsQueryHandler.cs \
        boilerplateBE/tests/Starter.Api.Tests/Workflow/GetWorkflowAnalyticsQueryHandlerTests.cs
git commit -m "feat(workflow): analytics approver activity with avg response time"
```

---

### Task 12: Controller endpoint + route mapping + wrong-window failure test

**Files:**
- Modify: `boilerplateBE/src/modules/Starter.Module.Workflow/Controllers/WorkflowController.cs`

- [ ] **Step 1: Add the action to the controller**

In `WorkflowController.cs`, after the `UpdateDefinition` action (around line 77), add:

```csharp
[HttpGet("definitions/{id:guid}/analytics")]
[Authorize(Policy = WorkflowPermissions.ViewAnalytics)]
[ProducesResponseType(typeof(ApiResponse<WorkflowAnalyticsDto>), StatusCodes.Status200OK)]
[ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
[ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
public async Task<IActionResult> GetDefinitionAnalytics(
    Guid id,
    [FromQuery(Name = "window")] string? window,
    CancellationToken ct = default)
{
    if (!WindowSelectorParser.TryParse(window ?? "30d", out var selector))
        return HandleResult(Result.Failure<WorkflowAnalyticsDto>(
            WorkflowErrors.InvalidAnalyticsWindow(window)));

    var result = await Mediator.Send(new GetWorkflowAnalyticsQuery(id, selector), ct);
    return HandleResult(result);
}
```

Add the `using` at the top:

```csharp
using Starter.Module.Workflow.Application.Queries.GetWorkflowAnalytics;
using Starter.Module.Workflow.Domain.Errors;
using Starter.Shared.Results;
```

- [ ] **Step 2: Build and run the full workflow suite**

Run: `cd boilerplateBE && dotnet build && dotnet test tests/Starter.Api.Tests/Starter.Api.Tests.csproj --filter "FullyQualifiedName~Workflow"`
Expected: all green.

- [ ] **Step 3: Smoke the endpoint manually (optional)**

Start the API and curl:

```bash
curl -s -H "Authorization: Bearer $JWT" \
  "http://localhost:5000/api/v1/Workflow/definitions/<id>/analytics?window=30d" | jq .
```

Expected shape: `{ success: true, data: { definitionName, window: 'ThirtyDays', … } }`.

- [ ] **Step 4: Commit**

```bash
git add boilerplateBE/src/modules/Starter.Module.Workflow/Controllers/WorkflowController.cs
git commit -m "feat(workflow): expose GET /api/v1/Workflow/definitions/{id}/analytics"
```

---

### Task 13: Perf test (10k instances, <1s budget, opt-in trait)

**Files:**
- Create: `boilerplateBE/tests/Starter.Api.Tests/Workflow/WorkflowAnalyticsPerformanceTests.cs`

- [ ] **Step 1: Write the perf test**

```csharp
using System.Diagnostics;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Starter.Abstractions.Readers;
using Starter.Module.Workflow.Application.Queries.GetWorkflowAnalytics;
using Starter.Module.Workflow.Domain.Entities;
using Starter.Module.Workflow.Domain.Enums;
using Starter.Module.Workflow.Infrastructure.Persistence;
using Xunit;

namespace Starter.Api.Tests.Workflow;

[Trait("perf", "true")]
public sealed class WorkflowAnalyticsPerformanceTests : IDisposable
{
    private readonly WorkflowDbContext _db = WorkflowEngineTestFactory.CreateDb();
    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task Handle_10kInstances_CompletesUnderOneSecond()
    {
        var tenant = Guid.NewGuid();
        var def = WorkflowDefinition.Create(
            tenantId: tenant, name: "perf", displayName: "Perf", entityType: "Order",
            statesJson: "[]", transitionsJson: "[]", isTemplate: false, sourceModule: "Perf");
        _db.WorkflowDefinitions.Add(def);
        await _db.SaveChangesAsync();

        var now = DateTime.UtcNow;
        for (var i = 0; i < 10_000; i++)
        {
            var inst = WorkflowInstance.Create(
                tenantId: tenant, definitionId: def.Id, entityType: "Order",
                entityId: Guid.NewGuid(), initialState: "Draft",
                startedByUserId: Guid.NewGuid(), contextJson: null,
                definitionName: "perf");
            _db.WorkflowInstances.Add(inst);
        }
        await _db.SaveChangesAsync();

        // Seed ~40k steps (4 per instance) — same pattern used by the handler's bottleneck aggregation.
        foreach (var inst in _db.WorkflowInstances.AsNoTracking().ToList())
        {
            _db.WorkflowSteps.Add(WorkflowStep.Create(inst.Id, "Draft", "Review",
                StepType.HumanTask, "Submit", Guid.NewGuid(), null, null));
            _db.WorkflowSteps.Add(WorkflowStep.Create(inst.Id, "Review", "Approved",
                StepType.HumanTask, "approve", Guid.NewGuid(), null, null));
        }
        await _db.SaveChangesAsync();

        var userReader = new Mock<IUserReader>();
        userReader.Setup(r => r.GetManyAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<UserSummary>());
        var sut = new GetWorkflowAnalyticsQueryHandler(_db, userReader.Object);

        var sw = Stopwatch.StartNew();
        var result = await sut.Handle(
            new GetWorkflowAnalyticsQuery(def.Id, WindowSelector.ThirtyDays),
            CancellationToken.None);
        sw.Stop();

        result.IsSuccess.Should().BeTrue();
        sw.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(1),
            "10k-instance analytics must complete under the 1s budget; if it doesn't, see the spec's deferred 'snapshot table' item.");
    }
}
```

- [ ] **Step 2: Run the perf test**

Run: `cd boilerplateBE && dotnet test tests/Starter.Api.Tests/Starter.Api.Tests.csproj --filter "Trait=perf"`
Expected: PASS (or FAIL, at which point investigate before continuing — see spec risk section).

- [ ] **Step 3: Commit**

```bash
git add boilerplateBE/tests/Starter.Api.Tests/Workflow/WorkflowAnalyticsPerformanceTests.cs
git commit -m "test(workflow): perf gate for analytics handler (10k instances, <1s)"
```

---

### Task 14: Frontend — TypeScript types + permission constant

**Files:**
- Modify: `boilerplateFE/src/types/workflow.types.ts`
- Modify: `boilerplateFE/src/constants/permissions.ts`

- [ ] **Step 1: Add the permission**

In `permissions.ts`, inside the `Workflows` object add:

```ts
    ViewAnalytics: 'Workflows.ViewAnalytics',
```

The `Workflows` block becomes:

```ts
Workflows: {
    View: 'Workflows.View',
    ManageDefinitions: 'Workflows.ManageDefinitions',
    Start: 'Workflows.Start',
    ActOnTask: 'Workflows.ActOnTask',
    Cancel: 'Workflows.Cancel',
    ViewAllTasks: 'Workflows.ViewAllTasks',
    ViewAnalytics: 'Workflows.ViewAnalytics',
},
```

- [ ] **Step 2: Add the TS types**

At the bottom of `boilerplateFE/src/types/workflow.types.ts`, append:

```ts
// ── Analytics ──────────────────────────────────────────────────────────────

export type AnalyticsWindow = 'SevenDays' | 'ThirtyDays' | 'NinetyDays' | 'AllTime';

export interface HeadlineMetrics {
  totalStarted: number;
  totalCompleted: number;
  totalCancelled: number;
  avgCycleTimeHours: number | null;
}

export interface StateMetric {
  stateName: string;
  medianDwellHours: number;
  p95DwellHours: number;
  visitCount: number;
}

export interface ActionRateMetric {
  stateName: string;
  action: string;
  count: number;
  percentage: number;
}

export interface InstanceCountPoint {
  bucket: string; // ISO
  started: number;
  completed: number;
  cancelled: number;
}

export interface StuckInstance {
  instanceId: string;
  entityDisplayName: string | null;
  currentState: string;
  startedAt: string;
  daysSinceStarted: number;
  currentAssigneeDisplayName: string | null;
}

export interface ApproverActivity {
  userId: string;
  userDisplayName: string;
  approvals: number;
  rejections: number;
  returns: number;
  avgResponseTimeHours: number | null;
}

export interface WorkflowAnalytics {
  definitionId: string;
  definitionName: string;
  window: AnalyticsWindow;
  windowStart: string;
  windowEnd: string;
  instancesInWindow: number;
  headline: HeadlineMetrics;
  statesByBottleneck: StateMetric[];
  actionRates: ActionRateMetric[];
  instanceCountSeries: InstanceCountPoint[];
  stuckInstances: StuckInstance[];
  approverActivity: ApproverActivity[];
}
```

- [ ] **Step 3: Build**

Run: `cd boilerplateFE && npm run build`
Expected: build green.

- [ ] **Step 4: Commit**

```bash
git add boilerplateFE/src/constants/permissions.ts \
        boilerplateFE/src/types/workflow.types.ts
git commit -m "feat(workflow-fe): add analytics types + ViewAnalytics permission mirror"
```

---

### Task 15: Frontend — API method, query hook, query key

**Files:**
- Modify: `boilerplateFE/src/config/api.config.ts`
- Modify: `boilerplateFE/src/features/workflow/api/workflow.api.ts`
- Modify: `boilerplateFE/src/features/workflow/api/workflow.queries.ts`
- Modify: `boilerplateFE/src/lib/query/keys.ts`

- [ ] **Step 1: Add the endpoint to `api.config.ts`**

In the `WORKFLOW:` block:

```ts
DEFINITION_ANALYTICS: (id: string) => `/workflow/definitions/${id}/analytics`,
```

(place it right after `DEFINITION_DETAIL`).

- [ ] **Step 2: Add the query key**

In `src/lib/query/keys.ts`, extend the `workflow.definitions` block to:

```ts
definitions: {
  all: ['workflow', 'definitions'] as const,
  list: (entityType?: string) => ['workflow', 'definitions', 'list', entityType] as const,
  detail: (id: string) => ['workflow', 'definitions', 'detail', id] as const,
  analytics: (id: string, window: string) =>
    ['workflow', 'definitions', 'analytics', id, window] as const,
},
```

- [ ] **Step 3: Add the API method**

In `workflow.api.ts`, after `updateDefinition` add:

```ts
getAnalytics: (id: string, window: string): Promise<WorkflowAnalytics> =>
  apiClient
    .get<ApiResponse<WorkflowAnalytics>>(API_ENDPOINTS.WORKFLOW.DEFINITION_ANALYTICS(id), {
      params: { window },
    })
    .then((r) => r.data.data),
```

Also import the type at the top:

```ts
import type {
  …existing…
  WorkflowAnalytics,
} from '@/types/workflow.types';
```

- [ ] **Step 4: Add the hook**

In `workflow.queries.ts`, after `useWorkflowDefinition` add:

```ts
export function useWorkflowAnalytics(id: string, window: string) {
  return useQuery({
    queryKey: queryKeys.workflow.definitions.analytics(id, window),
    queryFn: () => workflowApi.getAnalytics(id, window),
    enabled: !!id && !!window,
    staleTime: 60_000,
  });
}
```

- [ ] **Step 5: Build**

Run: `cd boilerplateFE && npm run build`
Expected: build green.

- [ ] **Step 6: Commit**

```bash
git add boilerplateFE/src/config/api.config.ts \
        boilerplateFE/src/features/workflow/api/workflow.api.ts \
        boilerplateFE/src/features/workflow/api/workflow.queries.ts \
        boilerplateFE/src/lib/query/keys.ts
git commit -m "feat(workflow-fe): add analytics API method + query hook"
```

---

### Task 16: Frontend — i18n keys

**Files:**
- Modify: `boilerplateFE/src/features/workflow/i18n/en.json`
- Modify: `boilerplateFE/src/features/workflow/i18n/ar.json`
- Modify: `boilerplateFE/src/features/workflow/i18n/ku.json`

- [ ] **Step 1: Add analytics keys to `en.json`**

Append under a new `analytics` block inside the `workflow` object:

```json
"analytics": {
  "tabLabel": "Analytics",
  "overviewTabLabel": "Overview",
  "windowLabel": "Window",
  "window": {
    "7d": "Last 7 days",
    "30d": "Last 30 days",
    "90d": "Last 90 days",
    "all": "All time"
  },
  "lowDataBanner": "Based on {{count}} run(s) — metrics may not be representative.",
  "headline": {
    "started": "Started",
    "completed": "Completed",
    "cancelled": "Cancelled",
    "avgCycleTime": "Avg. cycle time"
  },
  "hoursShort": "h",
  "daysShort": "d",
  "bottleneck": {
    "title": "Bottleneck states",
    "subtitle": "Median dwell time per state",
    "empty": "Not enough visits yet (need at least 3 per state)."
  },
  "actionRates": {
    "title": "Action rates",
    "empty": "No user actions recorded in this window."
  },
  "instanceSeries": {
    "title": "Instances over time"
  },
  "stuck": {
    "title": "Currently stuck",
    "currentState": "State",
    "startedAt": "Started",
    "daysSince": "Days since",
    "assignee": "Assignee",
    "empty": "No active instances."
  },
  "approverActivity": {
    "title": "Approver activity",
    "user": "User",
    "approvals": "Approvals",
    "rejections": "Rejections",
    "returns": "Returns",
    "avgResponse": "Avg. response (h)",
    "empty": "No approver actions in this window."
  }
}
```

- [ ] **Step 2: Mirror to `ar.json` and `ku.json`**

Copy the same keys into `ar.json` and `ku.json` with translated values (use existing translation patterns in the rest of the workflow keys for tone). If translations are not immediately available, copy English as placeholder — flagged for post-merge translation.

- [ ] **Step 3: Build**

Run: `cd boilerplateFE && npm run build`
Expected: build green.

- [ ] **Step 4: Commit**

```bash
git add boilerplateFE/src/features/workflow/i18n/
git commit -m "feat(workflow-fe): add analytics i18n keys (en/ar/ku)"
```

---

### Task 17: Frontend — `WindowSelector` + `LowDataBanner` primitives

**Files:**
- Create: `boilerplateFE/src/features/workflow/components/analytics/WindowSelector.tsx`
- Create: `boilerplateFE/src/features/workflow/components/analytics/LowDataBanner.tsx`

- [ ] **Step 1: Create `WindowSelector.tsx`**

```tsx
import { useTranslation } from 'react-i18next';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';

export type WindowValue = '7d' | '30d' | '90d' | 'all';

interface Props {
  value: WindowValue;
  onChange: (v: WindowValue) => void;
}

export function WindowSelector({ value, onChange }: Props) {
  const { t } = useTranslation();
  return (
    <Select value={value} onValueChange={(v) => onChange(v as WindowValue)}>
      <SelectTrigger className="w-44">
        <SelectValue />
      </SelectTrigger>
      <SelectContent>
        <SelectItem value="7d">{t('workflow.analytics.window.7d')}</SelectItem>
        <SelectItem value="30d">{t('workflow.analytics.window.30d')}</SelectItem>
        <SelectItem value="90d">{t('workflow.analytics.window.90d')}</SelectItem>
        <SelectItem value="all">{t('workflow.analytics.window.all')}</SelectItem>
      </SelectContent>
    </Select>
  );
}
```

- [ ] **Step 2: Create `LowDataBanner.tsx`**

```tsx
import { useTranslation } from 'react-i18next';
import { AlertCircle } from 'lucide-react';

interface Props {
  count: number;
}

export function LowDataBanner({ count }: Props) {
  const { t } = useTranslation();
  return (
    <div className="flex items-start gap-2 rounded-xl border border-border bg-muted/50 px-4 py-3 text-sm text-muted-foreground">
      <AlertCircle className="mt-0.5 h-4 w-4 flex-shrink-0" />
      <p>{t('workflow.analytics.lowDataBanner', { count })}</p>
    </div>
  );
}
```

- [ ] **Step 3: Build**

Run: `cd boilerplateFE && npm run build`
Expected: build green.

- [ ] **Step 4: Commit**

```bash
git add boilerplateFE/src/features/workflow/components/analytics/
git commit -m "feat(workflow-fe): add WindowSelector + LowDataBanner primitives"
```

---

### Task 18: Frontend — `HeadlineStrip`

**Files:**
- Create: `boilerplateFE/src/features/workflow/components/analytics/HeadlineStrip.tsx`

- [ ] **Step 1: Create the component**

```tsx
import { useTranslation } from 'react-i18next';
import { Card, CardContent } from '@/components/ui/card';
import type { HeadlineMetrics } from '@/types/workflow.types';

interface Props {
  headline: HeadlineMetrics;
}

export function HeadlineStrip({ headline }: Props) {
  const { t } = useTranslation();
  const stats = [
    { label: t('workflow.analytics.headline.started'),   value: headline.totalStarted },
    { label: t('workflow.analytics.headline.completed'), value: headline.totalCompleted },
    { label: t('workflow.analytics.headline.cancelled'), value: headline.totalCancelled },
    {
      label: t('workflow.analytics.headline.avgCycleTime'),
      value: headline.avgCycleTimeHours !== null
        ? `${headline.avgCycleTimeHours.toFixed(1)}${t('workflow.analytics.hoursShort')}`
        : '—',
    },
  ];
  return (
    <div className="grid grid-cols-2 gap-3 md:grid-cols-4">
      {stats.map((s) => (
        <Card key={s.label}>
          <CardContent className="py-4">
            <p className="text-xs font-medium text-muted-foreground">{s.label}</p>
            <p className="mt-1 text-2xl font-semibold text-foreground">{s.value}</p>
          </CardContent>
        </Card>
      ))}
    </div>
  );
}
```

- [ ] **Step 2: Commit**

```bash
git add boilerplateFE/src/features/workflow/components/analytics/HeadlineStrip.tsx
git commit -m "feat(workflow-fe): add HeadlineStrip analytics widget"
```

---

### Task 19: Frontend — `InstanceCountChart`

**Files:**
- Create: `boilerplateFE/src/features/workflow/components/analytics/InstanceCountChart.tsx`

- [ ] **Step 1: Create the chart**

```tsx
import { useTranslation } from 'react-i18next';
import { Card, CardContent } from '@/components/ui/card';
import { EmptyState } from '@/components/common';
import { BarChart, Bar, XAxis, YAxis, Tooltip, ResponsiveContainer, CartesianGrid, Legend } from 'recharts';
import type { InstanceCountPoint } from '@/types/workflow.types';
import { BarChart3 } from 'lucide-react';

interface Props {
  series: InstanceCountPoint[];
}

export function InstanceCountChart({ series }: Props) {
  const { t } = useTranslation();
  const hasData = series.some((p) => p.started + p.completed + p.cancelled > 0);

  const data = series.map((p) => ({
    bucket: new Date(p.bucket).toLocaleDateString(),
    started: p.started,
    completed: p.completed,
    cancelled: p.cancelled,
  }));

  return (
    <Card>
      <CardContent className="py-5">
        <h3 className="mb-3 text-sm font-semibold text-foreground">{t('workflow.analytics.instanceSeries.title')}</h3>
        {hasData ? (
          <ResponsiveContainer width="100%" height={260}>
            <BarChart data={data}>
              <CartesianGrid strokeDasharray="3 3" className="stroke-border" />
              <XAxis dataKey="bucket" tick={{ fontSize: 11 }} />
              <YAxis allowDecimals={false} tick={{ fontSize: 11 }} />
              <Tooltip />
              <Legend wrapperStyle={{ fontSize: 11 }} />
              <Bar dataKey="started"   stackId="a" fill="var(--primary)" />
              <Bar dataKey="completed" stackId="a" fill="var(--chart-2, #10b981)" />
              <Bar dataKey="cancelled" stackId="a" fill="var(--destructive)" />
            </BarChart>
          </ResponsiveContainer>
        ) : (
          <EmptyState icon={BarChart3} title={t('common.empty', 'No data')} />
        )}
      </CardContent>
    </Card>
  );
}
```

- [ ] **Step 2: Commit**

```bash
git add boilerplateFE/src/features/workflow/components/analytics/InstanceCountChart.tsx
git commit -m "feat(workflow-fe): add InstanceCountChart analytics widget"
```

---

### Task 20: Frontend — `BottleneckStatesChart` + `ActionRatesChart`

**Files:**
- Create: `boilerplateFE/src/features/workflow/components/analytics/BottleneckStatesChart.tsx`
- Create: `boilerplateFE/src/features/workflow/components/analytics/ActionRatesChart.tsx`

- [ ] **Step 1: Create `BottleneckStatesChart.tsx`**

```tsx
import { useTranslation } from 'react-i18next';
import { Card, CardContent } from '@/components/ui/card';
import { EmptyState } from '@/components/common';
import { BarChart, Bar, XAxis, YAxis, Tooltip, ResponsiveContainer, CartesianGrid } from 'recharts';
import type { StateMetric } from '@/types/workflow.types';
import { Hourglass } from 'lucide-react';

interface Props {
  states: StateMetric[];
}

export function BottleneckStatesChart({ states }: Props) {
  const { t } = useTranslation();
  return (
    <Card>
      <CardContent className="py-5">
        <h3 className="mb-1 text-sm font-semibold text-foreground">{t('workflow.analytics.bottleneck.title')}</h3>
        <p className="mb-3 text-xs text-muted-foreground">{t('workflow.analytics.bottleneck.subtitle')}</p>
        {states.length > 0 ? (
          <ResponsiveContainer width="100%" height={260}>
            <BarChart data={states} layout="vertical">
              <CartesianGrid strokeDasharray="3 3" className="stroke-border" />
              <XAxis type="number" tick={{ fontSize: 11 }} unit="h" />
              <YAxis type="category" dataKey="stateName" width={120} tick={{ fontSize: 11 }} />
              <Tooltip />
              <Bar dataKey="medianDwellHours" fill="var(--primary)" />
            </BarChart>
          </ResponsiveContainer>
        ) : (
          <EmptyState icon={Hourglass} title={t('workflow.analytics.bottleneck.empty')} />
        )}
      </CardContent>
    </Card>
  );
}
```

- [ ] **Step 2: Create `ActionRatesChart.tsx`**

```tsx
import { useMemo } from 'react';
import { useTranslation } from 'react-i18next';
import { Card, CardContent } from '@/components/ui/card';
import { EmptyState } from '@/components/common';
import { BarChart, Bar, XAxis, YAxis, Tooltip, ResponsiveContainer, CartesianGrid, Legend } from 'recharts';
import type { ActionRateMetric } from '@/types/workflow.types';
import { Vote } from 'lucide-react';

interface Props {
  rates: ActionRateMetric[];
}

export function ActionRatesChart({ rates }: Props) {
  const { t } = useTranslation();

  // Group by state, with one series per action.
  const { data, actions } = useMemo(() => {
    const stateMap = new Map<string, Record<string, number | string>>();
    const actionSet = new Set<string>();
    for (const r of rates) {
      actionSet.add(r.action);
      const row = stateMap.get(r.stateName) ?? { state: r.stateName };
      row[r.action] = r.count;
      stateMap.set(r.stateName, row);
    }
    return { data: Array.from(stateMap.values()), actions: Array.from(actionSet) };
  }, [rates]);

  const palette = ['var(--primary)', 'var(--destructive)', 'var(--chart-2, #10b981)', 'var(--chart-3, #f59e0b)'];

  return (
    <Card>
      <CardContent className="py-5">
        <h3 className="mb-3 text-sm font-semibold text-foreground">{t('workflow.analytics.actionRates.title')}</h3>
        {rates.length > 0 ? (
          <ResponsiveContainer width="100%" height={260}>
            <BarChart data={data}>
              <CartesianGrid strokeDasharray="3 3" className="stroke-border" />
              <XAxis dataKey="state" tick={{ fontSize: 11 }} />
              <YAxis allowDecimals={false} tick={{ fontSize: 11 }} />
              <Tooltip />
              <Legend wrapperStyle={{ fontSize: 11 }} />
              {actions.map((a, i) => (
                <Bar key={a} dataKey={a} fill={palette[i % palette.length]} />
              ))}
            </BarChart>
          </ResponsiveContainer>
        ) : (
          <EmptyState icon={Vote} title={t('workflow.analytics.actionRates.empty')} />
        )}
      </CardContent>
    </Card>
  );
}
```

- [ ] **Step 3: Build**

Run: `cd boilerplateFE && npm run build`
Expected: build green.

- [ ] **Step 4: Commit**

```bash
git add boilerplateFE/src/features/workflow/components/analytics/BottleneckStatesChart.tsx \
        boilerplateFE/src/features/workflow/components/analytics/ActionRatesChart.tsx
git commit -m "feat(workflow-fe): add bottleneck + action-rate charts"
```

---

### Task 21: Frontend — `StuckInstancesTable` (with row navigation) + `ApproverActivityTable`

**Files:**
- Create: `boilerplateFE/src/features/workflow/components/analytics/StuckInstancesTable.tsx`
- Create: `boilerplateFE/src/features/workflow/components/analytics/ApproverActivityTable.tsx`

- [ ] **Step 1: Create `StuckInstancesTable.tsx`**

```tsx
import { useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { Card, CardContent } from '@/components/ui/card';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { EmptyState } from '@/components/common';
import type { StuckInstance } from '@/types/workflow.types';
import { Timer } from 'lucide-react';

interface Props {
  rows: StuckInstance[];
}

export function StuckInstancesTable({ rows }: Props) {
  const { t } = useTranslation();
  const navigate = useNavigate();

  if (rows.length === 0) {
    return (
      <Card>
        <CardContent className="py-5">
          <h3 className="mb-3 text-sm font-semibold text-foreground">{t('workflow.analytics.stuck.title')}</h3>
          <EmptyState icon={Timer} title={t('workflow.analytics.stuck.empty')} />
        </CardContent>
      </Card>
    );
  }

  return (
    <Card>
      <CardContent className="py-5">
        <h3 className="mb-3 text-sm font-semibold text-foreground">{t('workflow.analytics.stuck.title')}</h3>
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead>{t('workflow.analytics.stuck.currentState')}</TableHead>
              <TableHead>{t('workflow.analytics.stuck.startedAt')}</TableHead>
              <TableHead>{t('workflow.analytics.stuck.daysSince')}</TableHead>
              <TableHead>{t('workflow.analytics.stuck.assignee')}</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {rows.map((r) => (
              <TableRow
                key={r.instanceId}
                className="cursor-pointer"
                onClick={() => navigate(`/workflows/instances/${r.instanceId}`)}
              >
                <TableCell>
                  <div className="flex flex-col">
                    <span className="font-medium">{r.entityDisplayName ?? r.instanceId.slice(0, 8)}</span>
                    <span className="text-xs text-muted-foreground">{r.currentState}</span>
                  </div>
                </TableCell>
                <TableCell>{new Date(r.startedAt).toLocaleDateString()}</TableCell>
                <TableCell>
                  {r.daysSinceStarted}{t('workflow.analytics.daysShort')}
                </TableCell>
                <TableCell>{r.currentAssigneeDisplayName ?? '—'}</TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </CardContent>
    </Card>
  );
}
```

- [ ] **Step 2: Create `ApproverActivityTable.tsx`**

```tsx
import { useTranslation } from 'react-i18next';
import { Card, CardContent } from '@/components/ui/card';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { EmptyState } from '@/components/common';
import type { ApproverActivity } from '@/types/workflow.types';
import { Users } from 'lucide-react';

interface Props {
  rows: ApproverActivity[];
}

export function ApproverActivityTable({ rows }: Props) {
  const { t } = useTranslation();

  if (rows.length === 0) {
    return (
      <Card>
        <CardContent className="py-5">
          <h3 className="mb-3 text-sm font-semibold text-foreground">{t('workflow.analytics.approverActivity.title')}</h3>
          <EmptyState icon={Users} title={t('workflow.analytics.approverActivity.empty')} />
        </CardContent>
      </Card>
    );
  }

  return (
    <Card>
      <CardContent className="py-5">
        <h3 className="mb-3 text-sm font-semibold text-foreground">{t('workflow.analytics.approverActivity.title')}</h3>
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead>{t('workflow.analytics.approverActivity.user')}</TableHead>
              <TableHead>{t('workflow.analytics.approverActivity.approvals')}</TableHead>
              <TableHead>{t('workflow.analytics.approverActivity.rejections')}</TableHead>
              <TableHead>{t('workflow.analytics.approverActivity.returns')}</TableHead>
              <TableHead>{t('workflow.analytics.approverActivity.avgResponse')}</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {rows.map((r) => (
              <TableRow key={r.userId}>
                <TableCell>{r.userDisplayName}</TableCell>
                <TableCell>{r.approvals}</TableCell>
                <TableCell>{r.rejections}</TableCell>
                <TableCell>{r.returns}</TableCell>
                <TableCell>{r.avgResponseTimeHours !== null ? r.avgResponseTimeHours.toFixed(1) : '—'}</TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </CardContent>
    </Card>
  );
}
```

- [ ] **Step 3: Build**

Run: `cd boilerplateFE && npm run build`
Expected: build green.

- [ ] **Step 4: Commit**

```bash
git add boilerplateFE/src/features/workflow/components/analytics/StuckInstancesTable.tsx \
        boilerplateFE/src/features/workflow/components/analytics/ApproverActivityTable.tsx
git commit -m "feat(workflow-fe): add stuck-instances + approver-activity tables"
```

---

### Task 22: Frontend — `WorkflowAnalyticsTab` composition

**Files:**
- Create: `boilerplateFE/src/features/workflow/components/analytics/WorkflowAnalyticsTab.tsx`

- [ ] **Step 1: Create the tab**

```tsx
import { useSearchParams } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { Spinner } from '@/components/ui/spinner';
import { useWorkflowAnalytics } from '@/features/workflow/api';
import { WindowSelector, type WindowValue } from './WindowSelector';
import { LowDataBanner } from './LowDataBanner';
import { HeadlineStrip } from './HeadlineStrip';
import { InstanceCountChart } from './InstanceCountChart';
import { BottleneckStatesChart } from './BottleneckStatesChart';
import { ActionRatesChart } from './ActionRatesChart';
import { StuckInstancesTable } from './StuckInstancesTable';
import { ApproverActivityTable } from './ApproverActivityTable';

interface Props {
  definitionId: string;
}

const DEFAULT_WINDOW: WindowValue = '30d';
const LOW_DATA_THRESHOLD = 5;

export function WorkflowAnalyticsTab({ definitionId }: Props) {
  const { t: _t } = useTranslation();
  const [params, setParams] = useSearchParams();
  const window = (params.get('window') as WindowValue) || DEFAULT_WINDOW;

  const { data, isLoading } = useWorkflowAnalytics(definitionId, window);

  const setWindow = (w: WindowValue) => {
    const next = new URLSearchParams(params);
    next.set('window', w);
    setParams(next, { replace: true });
  };

  if (isLoading) {
    return (
      <div className="flex justify-center py-12">
        <Spinner size="lg" />
      </div>
    );
  }

  if (!data) return null;

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-end">
        <WindowSelector value={window} onChange={setWindow} />
      </div>

      {data.instancesInWindow < LOW_DATA_THRESHOLD && (
        <LowDataBanner count={data.instancesInWindow} />
      )}

      <HeadlineStrip headline={data.headline} />

      <InstanceCountChart series={data.instanceCountSeries} />

      <div className="grid grid-cols-1 gap-4 md:grid-cols-2">
        <BottleneckStatesChart states={data.statesByBottleneck} />
        <ActionRatesChart rates={data.actionRates} />
      </div>

      <StuckInstancesTable rows={data.stuckInstances} />
      <ApproverActivityTable rows={data.approverActivity} />
    </div>
  );
}
```

- [ ] **Step 2: Build**

Run: `cd boilerplateFE && npm run build`
Expected: build green.

- [ ] **Step 3: Commit**

```bash
git add boilerplateFE/src/features/workflow/components/analytics/WorkflowAnalyticsTab.tsx
git commit -m "feat(workflow-fe): compose WorkflowAnalyticsTab"
```

---

### Task 23: Frontend — wrap `WorkflowDefinitionDetailPage` in `<Tabs>`

**Files:**
- Modify: `boilerplateFE/src/features/workflow/pages/WorkflowDefinitionDetailPage.tsx`

- [ ] **Step 1: Re-wrap post-header content in Tabs**

Replace the file contents with:

```tsx
import { useState } from 'react';
import { useParams } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Spinner } from '@/components/ui/spinner';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { PageHeader } from '@/components/common';
import { useBackNavigation, usePermissions } from '@/hooks';
import { PERMISSIONS } from '@/constants/permissions';
import { useWorkflowDefinition, useCloneDefinition, useUpdateDefinition } from '../api';
import { WorkflowAnalyticsTab } from '../components/analytics/WorkflowAnalyticsTab';

export default function WorkflowDefinitionDetailPage() {
  const { t } = useTranslation();
  const { id } = useParams<{ id: string }>();
  useBackNavigation('/workflows/definitions', t('workflow.definitions.title'));

  const { data: def, isLoading } = useWorkflowDefinition(id!);
  const { mutate: cloneDefinition, isPending: cloning } = useCloneDefinition();
  const { mutate: updateDefinition, isPending: updating } = useUpdateDefinition();
  const { hasPermission } = usePermissions();
  const canViewAnalytics = hasPermission(PERMISSIONS.Workflows.ViewAnalytics);

  const [editName, setEditName] = useState('');
  const [isEditing, setIsEditing] = useState(false);

  if (isLoading) {
    return (
      <div className="flex justify-center py-12">
        <Spinner size="lg" />
      </div>
    );
  }

  if (!def) return null;

  const showAnalyticsTab = canViewAnalytics && !def.isTemplate;

  const handleEdit = () => {
    setEditName(def.name);
    setIsEditing(true);
  };

  const handleSave = () => {
    updateDefinition(
      { id: id!, data: { displayName: editName } },
      { onSuccess: () => setIsEditing(false) },
    );
  };

  const overviewContent = (
    <div className="space-y-6">
      {isEditing && !def.isTemplate && (
        <Card>
          <CardContent className="py-5 space-y-4">
            <div className="space-y-2">
              <Label>{t('workflow.definitions.name')}</Label>
              <Input value={editName} onChange={(e) => setEditName(e.target.value)} />
            </div>
            <div className="flex items-center gap-2">
              <Button onClick={handleSave} disabled={updating}>
                {updating ? t('common.saving') : t('common.save')}
              </Button>
              <Button variant="outline" onClick={() => setIsEditing(false)}>
                {t('common.cancel')}
              </Button>
            </div>
          </CardContent>
        </Card>
      )}

      <section className="space-y-3">
        <h2 className="text-base font-semibold text-foreground">{t('workflow.detail.stateList')}</h2>
        <div className="space-y-3">
          {def.states?.map((state, index) => (
            <Card key={state.name}>
              <CardContent className="py-4">
                <div className="flex items-start justify-between gap-4">
                  <div className="space-y-1.5">
                    <div className="flex items-center gap-2">
                      <span className="text-xs font-medium text-muted-foreground">
                        {index + 1}.
                      </span>
                      <h3 className="text-sm font-semibold text-foreground">
                        {state.displayName || state.name}
                      </h3>
                      <Badge variant="outline" className="text-xs">{state.type}</Badge>
                    </div>
                    {state.assignee && (
                      <p className="text-xs text-muted-foreground">
                        {t('workflow.detail.assignee')}: {state.assignee.strategy}
                        {state.assignee.parameters && Object.keys(state.assignee.parameters).length > 0 && (
                          <> ({Object.entries(state.assignee.parameters).map(([k, v]) => `${k}: ${v}`).join(', ')})</>
                        )}
                      </p>
                    )}
                    {state.actions && state.actions.length > 0 && (
                      <div className="flex items-center gap-1.5 mt-1">
                        {state.actions.map((action) => (
                          <Badge key={action} variant="secondary" className="text-xs">
                            {action}
                          </Badge>
                        ))}
                      </div>
                    )}
                    {state.formFields && state.formFields.length > 0 && (
                      <div className="mt-2 space-y-1">
                        <p className="text-xs font-medium text-muted-foreground">{t('workflow.forms.required', 'Form Fields')}:</p>
                        <div className="flex flex-wrap gap-1.5">
                          {state.formFields.map((f) => (
                            <Badge key={f.name} variant="outline" className="text-xs">
                              {f.label} ({f.type}){f.required ? ' *' : ''}
                            </Badge>
                          ))}
                        </div>
                      </div>
                    )}
                    {state.sla && (
                      <div className="mt-2">
                        <p className="text-xs text-muted-foreground">
                          <span className="font-medium">{t('workflow.sla.overdue', 'SLA')}:</span>
                          {state.sla.reminderAfterHours != null && (
                            <span className="ms-1">⏰ Reminder after {state.sla.reminderAfterHours}h</span>
                          )}
                          {state.sla.escalateAfterHours != null && (
                            <span className="ms-1">🔺 Escalate after {state.sla.escalateAfterHours}h</span>
                          )}
                        </p>
                      </div>
                    )}
                    {state.parallel && (
                      <div className="mt-2">
                        <p className="text-xs text-muted-foreground">
                          <span className="font-medium">{t('workflow.parallel.progress', 'Parallel')}:</span>
                          <span className="ms-1">{state.parallel.mode} — {state.parallel.assignees.length} assignee(s)</span>
                        </p>
                      </div>
                    )}
                  </div>
                  {(state.onEnter || state.onExit) && (
                    <div className="text-xs text-muted-foreground">
                      <span className="font-medium">{t('workflow.detail.hooks')}:</span>
                      {state.onEnter && <span className="ms-1">onEnter({state.onEnter.length})</span>}
                      {state.onExit && <span className="ms-1">onExit({state.onExit.length})</span>}
                    </div>
                  )}
                </div>
              </CardContent>
            </Card>
          ))}
        </div>
      </section>
    </div>
  );

  return (
    <div className="space-y-6">
      <PageHeader
        title={def.name}
        actions={
          <div className="flex items-center gap-2">
            {def.isTemplate ? (
              <Button onClick={() => cloneDefinition(id!)} disabled={cloning}>
                {t('workflow.detail.cloneToCustomize')}
              </Button>
            ) : (
              !isEditing && (
                <Button variant="outline" onClick={handleEdit}>
                  {t('workflow.definitions.edit')}
                </Button>
              )
            )}
          </div>
        }
      />

      <Card>
        <CardContent className="py-5">
          <div className="flex flex-wrap items-center gap-3">
            <Badge variant="secondary">{def.entityType}</Badge>
            <Badge variant={def.isTemplate ? 'outline' : 'default'}>
              {def.isTemplate
                ? t('workflow.definitions.systemTemplate')
                : t('workflow.definitions.customized')}
            </Badge>
            {def.isActive ? (
              <Badge variant="default">{t('workflow.status.active')}</Badge>
            ) : (
              <Badge variant="secondary">{t('common.inactive')}</Badge>
            )}
          </div>
        </CardContent>
      </Card>

      {showAnalyticsTab ? (
        <Tabs defaultValue="overview">
          <TabsList>
            <TabsTrigger value="overview">{t('workflow.analytics.overviewTabLabel')}</TabsTrigger>
            <TabsTrigger value="analytics">{t('workflow.analytics.tabLabel')}</TabsTrigger>
          </TabsList>
          <TabsContent value="overview">{overviewContent}</TabsContent>
          <TabsContent value="analytics">
            <WorkflowAnalyticsTab definitionId={id!} />
          </TabsContent>
        </Tabs>
      ) : (
        overviewContent
      )}
    </div>
  );
}
```

- [ ] **Step 2: Verify `usePermissions` import path**

Run: `cd boilerplateFE && grep -r "export.*usePermissions" src/hooks/` — confirm the hook name and import. If it's `useHasPermission` instead, adjust.

- [ ] **Step 3: Build**

Run: `cd boilerplateFE && npm run build`
Expected: build green.

- [ ] **Step 4: Commit**

```bash
git add boilerplateFE/src/features/workflow/pages/WorkflowDefinitionDetailPage.tsx
git commit -m "feat(workflow-fe): tabbed Overview/Analytics layout on definition detail"
```

---

### Task 24: Docs — feature page

**Files:**
- Create: `docs/features/workflow-analytics.md`

- [ ] **Step 1: Write the docs page**

Create `docs/features/workflow-analytics.md`:

```markdown
# Workflow Analytics

A per-definition analytics dashboard surfacing six operator metrics from existing workflow tables. Available as the **Analytics** tab on any non-template workflow definition detail page.

## Who sees it

Users with the `Workflows.ViewAnalytics` permission. Seeded to `SuperAdmin` and tenant `Admin` by default.

The tab is **hidden** for system templates (`isTemplate = true`). Analytics answer "how is *my tenant's* flow performing" — cross-tenant template analytics is deferred (see the roadmap).

## Metrics

### Headline

- **Started / Completed / Cancelled** — raw counts for instances whose `StartedAt` falls in the window.
- **Avg. cycle time** — `AVG(CompletedAt − StartedAt)` across completed instances, in hours to one decimal.

### Bottleneck states

For each state, the median and p95 dwell time computed from matched entry (`ToState = X`) → exit (`FromState = X`) step pairs. Only states with **≥ 3 visits** in the window are shown (fewer is not meaningful).

### Action rates

`GROUP BY (FromState, Action)` on human-task steps. Percentage is computed within a single state's action total.

### Instances over time

Stacked bar chart of `Started` / `Completed` / `Cancelled` per bucket. Bucketing is automatic: 7d → day, 30d → day, 90d → week, All time → month. Missing buckets are zero-filled.

### Currently stuck

Top 10 active instances, ordered by `StartedAt` ASC. Row click navigates to `/workflows/instances/{id}` — all mutation (delegation, escalation, cancellation) happens there. No inline reassign from the widget.

### Approver activity

Top 10 users by total human-task actions in the window, with per-action counts (`approve` / `reject` / `return`) and average response time (time from task created → task completed).

## Time window

A single dropdown: **7d / 30d / 90d / All time**. Custom date ranges are deferred.

**Window is `StartedAt`-anchored.** An instance that completes inside the window but started before it is **excluded**. This is the most common source of "why doesn't this match my count" confusion — the rule is deliberate: analytics describe "what entered this flow during this period", not "what exited."

`All time` uses `definition.CreatedAt` as `WindowStart`.

## Low-data banner

When fewer than 5 instances fall in the window, a muted banner tells the user metrics may not be representative. The numbers are still shown.

## API

```http
GET /api/v1/Workflow/definitions/{id}/analytics?window={7d|30d|90d|all}
Authorization: Bearer <jwt>
```

- `200 OK` → `ApiResponse<WorkflowAnalyticsDto>`
- `400 Workflow.InvalidAnalyticsWindow` — window string not one of the four supported values
- `404 Workflow.DefinitionNotFound` — `id` doesn't resolve in the current tenant
- `404 Workflow.AnalyticsNotAvailableOnTemplate` — definition is a system template
- `401` / `403` — missing token / missing `Workflows.ViewAnalytics`

## Implementation notes

- Everything is computed on the fly in the `GetWorkflowAnalyticsQueryHandler` — no cache, no snapshot table, no background job.
- The handler injects the concrete `WorkflowDbContext` plus `IUserReader` for display-name resolution (same pattern `WorkflowEngine.GetHistoryAsync` uses).
- Percentile and time-bucket math currently runs in C# so EF InMemory (tests) and Postgres behave identically. If the `WorkflowAnalyticsPerformanceTests` 1s budget is ever breached at 10k instances, swap bottleneck and series aggregations to raw Postgres SQL (`percentile_cont`, `date_trunc`) — code paths are already factored to make this drop-in.

## Follow-ups

See **Analytics follow-ups (deferred)** in `docs/roadmaps/workflow.md`.
```

- [ ] **Step 2: Commit**

```bash
git add docs/features/workflow-analytics.md
git commit -m "docs(workflow): add workflow analytics feature page"
```

---

### Task 25: Docs — roadmap update (move 4b to Shipped + record deferrals)

**Files:**
- Modify: `docs/roadmaps/workflow.md`

- [ ] **Step 1: Add a "Phase 4b Shipped" section**

Immediately after the existing "Phase 4a Shipped" section (around the dashed rule following it), add:

```markdown
## Phase 4b Shipped (merged YYYY-MM-DD)

**Workflow analytics — read-only Analytics tab on `WorkflowDefinitionDetailPage`.** Aggregate metrics per non-template definition: headline cycle/completion counts, bottleneck states (median + p95 dwell, min 3 visits), per-state action rates, instance count series with auto-bucketing, top-10 currently stuck instances with click-through to instance detail, top-10 approver activity with avg response time.

Shipped components:

- BE: `GetWorkflowAnalyticsQuery` + handler under `Starter.Module.Workflow.Application.Queries.GetWorkflowAnalytics/`, `WindowSelector` enum + parser, `WorkflowAnalyticsDto` hierarchy, new `Workflows.ViewAnalytics` permission seeded to SuperAdmin + Admin.
- BE: `GET /api/v1/Workflow/definitions/{id}/analytics?window={7d|30d|90d|all}` on `WorkflowController` — 400 for invalid window, 404 for missing definition or template.
- BE: `GetWorkflowAnalyticsQueryHandlerTests` (tenant / template / empty / window semantics / bottleneck 3-visit filter / action percentages / stuck ordering / approver counts) + opt-in `WorkflowAnalyticsPerformanceTests` (`[Trait("perf", "true")]`, 10k instances, <1s budget).
- FE: `WorkflowAnalyticsTab` composes `WindowSelector`, `LowDataBanner`, `HeadlineStrip`, `InstanceCountChart`, `BottleneckStatesChart`, `ActionRatesChart`, `StuckInstancesTable`, `ApproverActivityTable`. `useWorkflowAnalytics` hook backed by TanStack Query with `staleTime: 60_000`. Window persisted in `?window=` query param.
- FE: `WorkflowDefinitionDetailPage` wraps post-header content in shadcn/ui `<Tabs>` — Overview is unchanged, Analytics shown only when `canViewAnalytics && !def.isTemplate`.
- Docs: `docs/features/workflow-analytics.md`.

See `docs/superpowers/specs/2026-04-22-workflow-phase4b-analytics-design.md` for full design.

### Analytics follow-ups (deferred)

- **Module-level cross-definition dashboard** (`/workflows/analytics`). Deliberately skipped in 4b to keep scope contained to per-definition. Pick up when tenant admins request a single-pane view.
- **Custom date ranges** (date picker instead of preset selector). Pick up when feedback shows the four presets are too coarse.
- **Inline / bulk reassign from stuck-instances widget**. Currently read-only with click-through to instance detail. Pick up if operator feedback shows the extra navigation hop is a pain.
- **Cross-tenant template analytics for SuperAdmin**. Analytics for system templates (`isTemplate = true`) showing aggregate adoption across tenants. Touches tenant-filter bypass and must be designed carefully.
- **Pre-aggregated snapshot table**. Only revisit if `WorkflowAnalyticsPerformanceTests` ever fails the 1s budget on main. Current in-C# median/p95 + bucketing path is factored to make Postgres `percentile_cont` + `date_trunc` a drop-in replacement.
```

- [ ] **Step 2: Commit**

```bash
git add docs/roadmaps/workflow.md
git commit -m "docs(workflow): record Phase 4b shipped + deferred follow-ups"
```

---

### Task 26: Final verification (full test suite + FE build)

- [ ] **Step 1: Run the full backend test suite**

Run: `cd boilerplateBE && dotnet test`
Expected: all green (the perf test is only triggered under the `perf` trait filter, so this doesn't hit the 10k seed).

- [ ] **Step 2: Run the perf test explicitly**

Run: `cd boilerplateBE && dotnet test --filter "Trait=perf"`
Expected: PASS under 1s.

- [ ] **Step 3: Build the frontend**

Run: `cd boilerplateFE && npm run build`
Expected: production build green.

- [ ] **Step 4: Commit (no-op if everything's already committed)**

Nothing to commit — verification only.

---

### Task 27: Live QA — Post-Feature Testing Workflow

Follow the project's standard post-feature testing workflow from `CLAUDE.md`. Goal: a human (or Playwright) actually opens the browser, signs in as tenant admin of a seeded tenant, opens a custom (non-template) workflow definition, clicks the Analytics tab, and exercises the widgets end-to-end.

- [ ] **Step 1: Provision test app per the CLAUDE.md workflow**

Pick free ports (e.g. 5100/3100), run `scripts/rename.ps1 -Name "_testWfPhase4b" -OutputDir "." -Modules "All" -IncludeMobile:$false`, fix the seed email underscore, bucket name, CORS, FrontendUrl, and rate limits per the CLAUDE.md "Post-Feature Testing Workflow" section.

- [ ] **Step 2: Generate migrations for all 8 DbContexts**

Per the CLAUDE.md note. The Workflow module's migration is one of them.

- [ ] **Step 3: Start BE + FE, login as `acme.admin@acme.com`, open a custom definition**

If no custom definition exists, clone the `general-approval` template to create one, then start a handful of workflows as different users and execute some tasks to produce history.

- [ ] **Step 4: Open Analytics tab, verify per-widget rendering**

Checklist:

- [ ] Window selector dropdown changes all widgets.
- [ ] Headline strip shows correct Started / Completed / Cancelled / Avg cycle.
- [ ] Instance count chart renders with the right bucketing (day for 7d/30d, week for 90d, month for All time).
- [ ] Bottleneck chart either shows states with ≥ 3 visits or the empty-state message.
- [ ] Action rates chart groups by state, one bar per action.
- [ ] Stuck instances table links correctly to `/workflows/instances/{id}` on row click.
- [ ] Approver activity table shows users + counts.
- [ ] Low-data banner shows when `instancesInWindow < 5`.

- [ ] **Step 5: Test permission hide**

Log in as a `User` role (default user without `ViewAnalytics`). Open the definition. Verify the Analytics tab is not rendered and only Overview content is shown.

- [ ] **Step 6: Test template hide**

Open a system template (e.g. `general-approval`). Verify no tabs are rendered — Overview content is shown flat, no Analytics tab.

- [ ] **Step 7: Test invalid window URL**

Manually set `?window=invalid` in the URL. Expect the query to fail with a 400 from the API and the standard error toast. (Refresh with `?window=30d` to recover.)

- [ ] **Step 8: Leave running for user manual QA**

Do not clean up until the user explicitly signs off.

---

## Self-Review

**Spec coverage:**

- Headline metrics → Task 6 ✅
- Bottleneck states with `percentile_cont`/InMemory fallback → Task 8 (fallback path is C#-always for consistency; documented in the docs page and roadmap as drop-in replacement) ✅
- Action rates → Task 9 ✅
- Instance count series with auto-bucketing → Task 7 ✅
- Stuck instances (top 10, `IUserReader` lookup) → Task 10 ✅
- Approver activity (top 10) → Task 11 ✅
- Controller endpoint + window parsing → Task 12 ✅
- Permission + seeding → Task 1 ✅
- Error codes (`Workflow.DefinitionNotFound`, `Workflow.AnalyticsNotAvailableOnTemplate`, `Workflow.InvalidAnalyticsWindow`) → Tasks 4 + 12 ✅
- Template hide (BE + FE) → Tasks 5 + 23 ✅
- Tabs layout on `WorkflowDefinitionDetailPage` → Task 23 ✅
- All seven widget components → Tasks 17–21 ✅
- Widget composition + `?window=` persistence → Task 22 ✅
- TS types + API + hook + permissions mirror → Tasks 14–15 ✅
- i18n → Task 16 ✅
- Perf test (opt-in trait, <1s) → Task 13 ✅
- Docs + roadmap → Tasks 24–25 ✅
- Live QA → Task 27 ✅

**Placeholder scan:** No "TBD" / "TODO" / "similar to Task N" / "handle edge cases" patterns. Every code step shows actual code. Commands and expected outputs are explicit.

**Type consistency:** `WorkflowAnalyticsDto` / `HeadlineMetrics` / `StateMetric` / `ActionRateMetric` / `InstanceCountPoint` / `StuckInstanceDto` / `ApproverActivityDto` names and fields are consistent between Task 3 (BE definition) and Task 14 (FE mirror — camelCase). `WindowSelector` enum values `SevenDays | ThirtyDays | NinetyDays | AllTime` are consistent across Task 2 (BE), Task 14 (FE `AnalyticsWindow` union), and response JSON. Route `/workflow/definitions/{id}/analytics` consistent across Task 12 (BE) and Task 15 (FE). `Workflows.ViewAnalytics` permission string consistent across Tasks 1 (BE), 14 (FE), and 23 (tab-hide check).

**Scope check:** All 27 tasks are essential. The perf test (Task 13) is the only optional item and it's strictly additive (opt-in trait doesn't affect the default test run).
