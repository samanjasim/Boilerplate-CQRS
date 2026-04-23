# Workflow Phase 4b — Workflow Analytics

**Status:** Draft — awaiting user review
**Date:** 2026-04-22
**Parent roadmap:** [2026-04-22-workflow-phase3-plus-roadmap-design.md](2026-04-22-workflow-phase3-plus-roadmap-design.md)
**Depends on:** Phase 3 (engine extraction, compound conditions, bulk ops), Phase 4a (dynamic forms shipped)
**Ships as:** 1 PR

---

## Background

The Phase 3+ roadmap identifies Phase 4b as "Workflow analytics" — aggregate metrics per workflow definition, surfaced via `GET /api/v1/Workflow/definitions/{id}/analytics` (matching the existing `WorkflowController` route convention) and an "Analytics" tab on the definition detail page. Chart library is Recharts (already in `package.json` for billing usage charts). Read-model lives in the Workflow module; no separate analytics module.

All the raw data needed already exists in the schema:

| Data source | Fields used |
|---|---|
| `WorkflowInstance` | `StartedAt`, `CompletedAt`, `CancelledAt`, `Status`, `DefinitionId`, `TenantId`, `CurrentState` |
| `WorkflowStep` | `InstanceId`, `FromState`, `ToState`, `StepType`, `Action`, `ActorUserId`, `Timestamp` |
| `ApprovalTask` | `InstanceId`, `AssigneeUserId`, `CompletedAt` (resolve display name via `IUserReader`, same pattern `WorkflowEngine` uses for history) |

Phase 2b already added `(TenantId, DefinitionId)` on `WorkflowInstance` and `(InstanceId, Timestamp)` on `WorkflowStep`. No new indexes or tables are required for this phase.

## Problem statement

A tenant admin looking at a workflow definition today has no way to answer any operator question:

1. **"How long does this flow actually take?"** — no cycle-time metric.
2. **"Where are flows getting stuck?"** — no per-state dwell metric.
3. **"Are approvers approving or rejecting?"** — no action-rate breakdown.
4. **"Is throughput trending up or down?"** — no time-series.
5. **"Which instances are currently stuck, and on whom?"** — no stuck-instances list.
6. **"Who's carrying the approval load?"** — no per-approver activity.

These questions are the most commonly raised operator concerns after delegation, which Phase 2a shipped. Phase 4b's purpose is to answer them with a single read-only tab backed by a single endpoint.

## Goals

- Ship a read-only Analytics tab on `WorkflowDefinitionDetailPage` for custom (non-template) definitions.
- Expose a single `GET /api/v1/Workflow/definitions/{id}/analytics?window={7d|30d|90d|all}` endpoint returning everything the tab renders.
- Compute every metric on-the-fly from existing tables — no new tables, no caching, no background jobs.
- Gate behind a new `Workflows.ViewAnalytics` permission.
- Cover the handler with unit tests (EF InMemory) and an optional perf test (10k-instance seed, < 1s budget).
- Document the endpoint + each metric + the start-anchor window rule in `docs/features/workflow-analytics.md`.
- Capture deferred analytics ideas in the roadmap so they resurface in future planning.

## Non-goals (explicit)

- **No module-level cross-definition dashboard.** A tenant-wide `/workflows/analytics` page showing all definitions rolled up is intentionally deferred to a later phase (tracked under "Analytics follow-ups" in the roadmap).
- **No custom date ranges.** Window selector is a fixed preset: `7d` / `30d` / `90d` / `All time`. Date-range picker is deferred.
- **No mutation from the tab.** Clicking a stuck-instance row navigates to the existing `WorkflowInstanceDetailPage`; delegation / escalation / cancellation happen there as they already do.
- **No inline or bulk reassign** from the stuck-instances widget.
- **No analytics on system templates.** Tab hidden when `definition.isTemplate = true`. Cross-tenant template analytics for SuperAdmin is deferred.
- **No Redis cache layer** on the handler.
- **No pre-aggregated snapshot table.** If the handler ever exceeds its 1s budget, that decision gets revisited.
- **No new sidebar navigation entry.** Analytics is strictly a sub-tab of the definition detail page this phase.
- **No new database tables, migrations, or indexes.**

## Scope decisions

Captured from brainstorm; included so future readers understand the rationale:

| # | Question | Pick | Rationale |
|---|---|---|---|
| 1 | Scope | Roadmap four + rejection/cancellation rate + top N stuck instances + per-approver activity | Stops short of a module-level dashboard; answers "where is work stuck and on whom" in addition to the headline numbers. |
| 2 | Time window | Preset selector: `7d` / `30d` / `90d` / `All time` | One dropdown; no URL/date-picker complexity. Custom ranges deferred. |
| 3 | Storage | Compute on-the-fly per request | No new tables, no staleness semantics. Cache/snapshot only if measured pain. |
| 4 | Permissions | New `Workflows.ViewAnalytics` | Lets us grant analytics without granting definition-edit later. Seeded to the same roles as `ManageDefinitions` by default. |
| 5 | System templates | Tab hidden | Honest semantics — analytics = "how is *my tenant's* flow performing." |
| 6 | Interaction model | Read-only; navigate to instance detail for actions | Analytics is a diagnostic surface; one source of truth for mutation. |

## Architecture

```
GET /api/v1/Workflow/definitions/{id}/analytics?window=30d
         ↓
  [Authorize(Policy = WorkflowPermissions.ViewAnalytics)]
         ↓
  GetWorkflowAnalyticsQuery(definitionId, window)
         ↓
  GetWorkflowAnalyticsQueryHandler
    ├─ Tenant-filtered definition load → guard (IsTemplate, NotFound)
    ├─ Instance aggregate (headline + count series)
    ├─ Step aggregate (bottleneck + action rates)
    ├─ Stuck instances (Active, ordered by StartedAt)
    └─ Approver activity (ActorUserId grouping)
         ↓
  Returns Result<WorkflowAnalyticsDto>
         ↓
  Controller maps via HandleResult() → ApiResponse<WorkflowAnalyticsDto>
```

**Boundary rules:**

- Query lives in `boilerplateBE/src/modules/Starter.Module.Workflow/Application/Queries/GetWorkflowAnalytics/` following the existing `GetWorkflowDefinitionByIdQuery` pattern (one folder, query record, handler, DTO file).
- Handler injects the concrete `WorkflowDbContext` directly (the module-owned context; no `IWorkflowDbContext` interface exists and none is added). It also injects `IUserReader` for resolving assignee/approver display names — the same pattern `WorkflowEngine.GetHistoryAsync` uses.
- Reads go against `WorkflowInstances`, `WorkflowSteps`, `ApprovalTasks`. All joins are tenant-filtered by the existing global query filter on `WorkflowInstance`.
- **Percentile calculations** (median + P95 dwell per state) use one raw-SQL call via `FromSqlInterpolated` that invokes Postgres `percentile_cont(0.5) WITHIN GROUP (ORDER BY duration_seconds)`. The handler detects the provider via `db.Database.ProviderName` and falls back to `AVG(...)` on non-Postgres providers so the EF InMemory test provider still returns a plausible number.
- **Window validation** — query-string `window` parsed to a `WindowSelector` sealed enum (`SevenDays | ThirtyDays | NinetyDays | AllTime`). Invalid strings return `400 Workflow.InvalidAnalyticsWindow`.
- **Template guard** — handler rejects with `Workflow.AnalyticsNotAvailableOnTemplate` (kind `NotFound`). Defense-in-depth; the frontend also hides the tab.
- **Tenant guard** — missing/wrong-tenant definition returns `Workflow.DefinitionNotFound` (no info leak; same message regardless of cause).
- **Window semantics** — `WorkflowInstance.StartedAt ∈ [WindowStart, WindowEnd]` anchors every metric. An instance that completes inside the window but started before it is **excluded**. This is documented prominently because it's the #1 source of "why doesn't this match my count" confusion.

**No new indexes in this phase.** If the perf test (below) fails at 10k instances, we revisit then.

## API surface

```http
GET /api/v1/Workflow/definitions/{id}/analytics?window={7d|30d|90d|all}
Authorization: Bearer {jwt}
```

**Success — `200 OK`:**

```json
{
  "success": true,
  "data": {
    "definitionId": "…",
    "definitionName": "Expense Approval",
    "window": "ThirtyDays",
    "windowStart": "2026-03-23T00:00:00Z",
    "windowEnd":   "2026-04-22T00:00:00Z",
    "instancesInWindow": 42,
    "headline": {
      "totalStarted": 42,
      "totalCompleted": 37,
      "totalCancelled": 2,
      "avgCycleTimeHours": 103.2
    },
    "statesByBottleneck": [
      { "stateName": "AwaitingManagerApproval",
        "medianDwellHours": 28.5, "p95DwellHours": 72.0, "visitCount": 37 }
    ],
    "actionRates": [
      { "stateName": "AwaitingManagerApproval",
        "action": "approve", "count": 32, "percentage": 0.864 },
      { "stateName": "AwaitingManagerApproval",
        "action": "reject",  "count":  5, "percentage": 0.135 }
    ],
    "instanceCountSeries": [
      { "bucket": "2026-04-15T00:00:00Z",
        "started": 6, "completed": 4, "cancelled": 0 }
    ],
    "stuckInstances": [
      { "instanceId": "…", "entityDisplayName": "Q2 travel budget",
        "currentState": "AwaitingManagerApproval",
        "startedAt": "2026-04-01T10:00:00Z", "daysSinceStarted": 21,
        "currentAssigneeDisplayName": "alice@acme.com" }
    ],
    "approverActivity": [
      { "userId": "…", "userDisplayName": "Alice A.",
        "approvals": 14, "rejections": 2, "returns": 0,
        "avgResponseTimeHours": 6.8 }
    ]
  },
  "errors": null
}
```

**Failures:**

| Code | HTTP | When |
|---|---|---|
| `Workflow.DefinitionNotFound` | 404 | `id` doesn't resolve in the current tenant |
| `Workflow.AnalyticsNotAvailableOnTemplate` | 404 | definition is a system template |
| `Workflow.InvalidAnalyticsWindow` | 400 | `window` not one of `7d` / `30d` / `90d` / `all` |
| (standard auth) | 401 / 403 | missing token / missing `Workflows.ViewAnalytics` |

All failures flow through the existing `HandleResult()` envelope.

## Metrics — calculation detail

| Widget | Source | Calculation |
|---|---|---|
| Headline: `totalStarted`, `totalCompleted`, `totalCancelled` | `WorkflowInstance` | `COUNT(*)` grouped by `Status`, filtered by `DefinitionId`, `StartedAt ∈ window`. |
| Headline: `avgCycleTimeHours` | `WorkflowInstance` | `AVG(CompletedAt - StartedAt)` where `Status = Completed` and `StartedAt ∈ window`. Expressed in hours to one decimal. |
| Bottleneck states (per state) | `WorkflowStep` | For each step with `FromState = X`, find the *preceding* step landing in `X` (i.e. `ToState = X`). Dwell = `exit.Timestamp − entry.Timestamp`. Compute `median` + `p95` (Postgres `percentile_cont`) + `count`. **Only states with `visitCount ≥ 3` are returned** — fewer runs aren't meaningful. Sorted descending by `medianDwellHours`. |
| Action rates (per state + action) | `WorkflowStep` where `StepType = HumanTask` AND `ActorUserId IS NOT NULL` | `GROUP BY (FromState, Action)`; `percentage` is within the `FromState` group. (`HumanTask` is the step type the engine emits for user-executed task actions — see `WorkflowEngine.ExecuteTaskAsync`.) |
| Instance count series | `WorkflowInstance` | Auto-bucket: `7d→day`, `30d→day`, `90d→week`, `AllTime→month`. On Postgres, `date_trunc` in raw SQL. On EF InMemory (tests), the handler materializes rows in-window and buckets in C# using the same provider-detection switch used for percentiles. Missing buckets zero-filled in the handler (not the DB) so the series is always dense. Each point has `started`, `completed`, `cancelled`. |
| Stuck instances (top 10) | `WorkflowInstance` + `ApprovalTask` | `Status = Active`, `ORDER BY StartedAt ASC`, `LIMIT 10`. Left-join the pending `ApprovalTask` by `InstanceId` + `Status = Pending` to get `AssigneeUserId`; the handler then calls `IUserReader.GetManyAsync` (same two-phase lookup pattern `WorkflowEngine.GetHistoryAsync` uses) to resolve `currentAssigneeDisplayName`. `daysSinceStarted = CEIL((now − StartedAt).TotalDays)`. |
| Approver activity (top 10) | `WorkflowStep` + `ApprovalTask` | `WHERE ActorUserId IS NOT NULL AND StepType = HumanTask`, `GROUP BY ActorUserId`. Counts of `approve` / `reject` / `return`. `avgResponseTimeHours = AVG(step.Timestamp − task.CreatedAt)` for the matching completed task. Top 10 by total-action count. `userDisplayName` resolved via `IUserReader.GetManyAsync`. |

**Low-data caveat.** When `instancesInWindow < 5` the response still includes all numbers; the frontend shows a muted banner ("Based on N runs — metrics may not be representative"). No backend flag.

**`AllTime` window.** `WindowStart = definition.CreatedAt`. Everything else is identical.

## Frontend

**Tab introduction on `WorkflowDefinitionDetailPage`.** The page currently renders header → editable fields → states list in a flat column. Phase 4b wraps the post-header content in `shadcn/ui Tabs`:

```tsx
<PageHeader … />
<HeaderInfoCard />                               {/* badges, always visible */}
<Tabs defaultValue="overview">
  <TabsList>
    <TabsTrigger value="overview">Overview</TabsTrigger>
    {canViewAnalytics && !def.isTemplate && (
      <TabsTrigger value="analytics">Analytics</TabsTrigger>
    )}
  </TabsList>
  <TabsContent value="overview">{/* existing content verbatim */}</TabsContent>
  <TabsContent value="analytics">
    <WorkflowAnalyticsTab definitionId={id!} />
  </TabsContent>
</Tabs>
```

- `canViewAnalytics = hasPermission(PERMISSIONS.Workflows.ViewAnalytics)`.
- Selected window is kept in `?window=` query param; reloads preserve it.
- `shadcn/ui Tabs` is already in the component kit — no new dependency.

**Component layout — `WorkflowAnalyticsTab`.**

```
┌─ WindowSelector (dropdown 7d|30d|90d|All time) ── RefreshButton ─┐
├─ LowDataBanner (only when instancesInWindow < 5) ─────────────────┤
├─ HeadlineStrip ───────────────────────────────────────────────────┤
│   [Started: 42]  [Completed: 37]  [Cancelled: 2]  [Avg: 4.3d]     │
├─ InstanceCountChart (full width, stacked BarChart) ───────────────┤
├─ Two-column row ──────────────────────────────────────────────────┤
│  ┌─ BottleneckStatesChart ─┐  ┌─ ActionRatesChart ─────────────┐  │
│  │ horizontal BarChart     │  │ grouped BarChart per state     │  │
│  │ median dwell per state  │  │ approve / reject / return bars │  │
│  └─────────────────────────┘  └────────────────────────────────┘  │
├─ StuckInstancesTable (row click → /workflows/instances/{id}) ─────┤
├─ ApproverActivityTable (user | approved | rejected | returned) ───┤
└───────────────────────────────────────────────────────────────────┘
```

- All widget components live in `boilerplateFE/src/features/workflow/components/analytics/`. One file per widget.
- Single data fetch via `useWorkflowAnalytics(definitionId, window)` in `boilerplateFE/src/features/workflow/api/workflow.queries.ts`. `queryKey: ['workflow', 'analytics', definitionId, window]`, `staleTime: 60_000` so repeat window-switches feel instant.
- Empty-state per widget uses the shared `<EmptyState>` component — no pie-chart placeholders.
- Chart colors come from theme tokens (`--primary`, `--chart-2`, …) matching the existing billing usage chart. No hardcoded hex.
- RTL-safe: two-column row uses `flex` (flips naturally); Recharts works LTR on numeric axes.

**Permissions mirror.** `boilerplateFE/src/constants/permissions.ts` gains `Workflows.ViewAnalytics`. The role-matrix page inherits it from the `GET /permissions` list without code change.

**No new sidebar entry.** Analytics is strictly a sub-tab of an existing page this phase.

## Permissions + seeding

- Backend constant — `boilerplateBE/src/modules/Starter.Module.Workflow/Constants/WorkflowPermissions.cs` gets `public const string ViewAnalytics = "Workflows.ViewAnalytics";`.
- Seed role grants — `ViewAnalytics` is granted to the same roles that currently receive `ManageDefinitions` (tenant Admin, platform SuperAdmin). `Workflows.View` users do **not** get it by default.
- Frontend mirror — `boilerplateFE/src/constants/permissions.ts` gets the matching entry.

## Testing

### Unit tests — `GetWorkflowAnalyticsQueryHandlerTests`

Driven by `WorkflowEngineTestFactory.CreateDb()` (EF InMemory) with per-test seeding:

- Happy path: 30-day window, seeded instances across all statuses, asserts headline numbers, bucket count, bottleneck ordering.
- Window semantics: an instance started 60 days ago but completed inside a 30-day window is **excluded** (start-anchor rule).
- Empty definition: 0 instances → zero-filled DTO (not null), `InstancesInWindow = 0`.
- `AllTime` window uses `definition.CreatedAt` as `WindowStart`.
- Template guard: `IsTemplate = true` → `Workflow.AnalyticsNotAvailableOnTemplate`.
- Wrong tenant: returns `Workflow.DefinitionNotFound` (not `Forbidden`, matching the existing no-info-leak convention).
- Low-data: `InstancesInWindow = 3` still returns numbers (banner is an FE concern).
- Bottleneck filter: state with `VisitCount = 2` is omitted; with `VisitCount = 3` it's present.
- Percentile fallback: in-memory provider returns averages without throwing.
- Action-rate percentages within a state sum to `1.0` (within float tolerance).
- Stuck instances ordered ASC by `StartedAt`; cap at 10 rows enforced.

### Integration test — Postgres (optional)

If `Testcontainers.PostgreSql` is already a test dependency, add one test that pins `percentile_cont` output against a hand-calculated dataset. Otherwise defer; the unit-test percentile fallback plus a pinned SQL snapshot test gives acceptable confidence.

### Perf test — `WorkflowAnalyticsPerformanceTests`

Opt-in via `[Trait("perf", "true")]`; runs on CI main, doesn't block PRs. Seeds 10k instances + 40k steps for one definition and asserts the handler completes in < 1s. If it fails, that's the signal to add an index or switch to a snapshot table.

### Frontend tests

Under `boilerplateFE/src/features/workflow/components/analytics/__tests__/`, one file per widget:

- `useWorkflowAnalytics` hook: query-key shape, error propagation.
- Widget renders: happy-path data, empty state, low-data banner visible below threshold.
- `StuckInstancesTable`: row click navigates to `/workflows/instances/{id}`.
- Permission hide: `WorkflowDefinitionDetailPage` does not render the tab trigger when `Workflows.ViewAnalytics` is missing.
- Template hide: tab trigger absent when `def.isTemplate = true`.

## Documentation

- **New:** `docs/features/workflow-analytics.md` — one page explaining what each metric means, the start-anchor window rule, the low-data caveat, the template exclusion, the click-through navigation pattern.
- **Updated:** `docs/roadmaps/workflow.md` — Phase 4b moves from "Next" to "Shipped." New sub-section "Analytics follow-ups (deferred)":
  - Module-level cross-definition dashboard (`/workflows/analytics`).
  - Custom date ranges.
  - Inline / bulk reassign from stuck-instances widget.
  - Cross-tenant template analytics for SuperAdmin.
  - Pre-aggregated snapshot table (revisit if handler exceeds 1s budget).

## Risk

**Low-to-moderate.** Everything reads from existing tables; no schema change. The one correctness-sensitive piece is the raw-SQL percentile query — mitigated by the provider-fallback switch, the unit tests covering both paths, and (when available) a Testcontainers snapshot. The perf test is the canary if any tenant grows past 10k instances on a single definition.

## Shipping checklist

- [ ] BE: new `WorkflowPermissions.ViewAnalytics` constant + role seeding.
- [ ] BE: `GetWorkflowAnalyticsQuery` + handler + DTO.
- [ ] BE: controller endpoint `GET /api/v1/Workflow/definitions/{id}/analytics`.
- [ ] BE: handler unit tests (all cases above) + optional Postgres integration.
- [ ] BE: perf test gated on `[Trait("perf", "true")]`.
- [ ] FE: `PERMISSIONS.Workflows.ViewAnalytics` constant.
- [ ] FE: `useWorkflowAnalytics` hook.
- [ ] FE: `WorkflowAnalyticsTab` + widget components.
- [ ] FE: `WorkflowDefinitionDetailPage` tabbed layout.
- [ ] FE: widget unit tests.
- [ ] Docs: `workflow-analytics.md`.
- [ ] Docs: roadmap updated (Shipped + deferred follow-ups).
- [ ] `dotnet test` green, `npm run build` green.
- [ ] Live QA per Post-Feature Testing Workflow in `CLAUDE.md`.
