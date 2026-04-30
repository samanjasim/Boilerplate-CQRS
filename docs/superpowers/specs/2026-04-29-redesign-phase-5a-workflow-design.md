# Phase 5a Workflow — Workflow & comms cluster, workflow half (6 pages + 1 read-only embed)

**Created:** 2026-04-29
**Branch:** `fe/phase-5-design` (off latest `origin/main` — Phase 4 Products PR #41 already merged)
**Predecessors:** Phase 0 (foundation), Phase 1 (Identity), Phase 2 (Platform admin), Phase 3 (Data, PR #35), Phase 4 Billing (PR #37), Phase 4 Products (PR #41).
**Roadmap reference:** [`2026-04-28-post-phase-2-status-and-roadmap.md`](2026-04-28-post-phase-2-status-and-roadmap.md) §5 — "Phase 5: Workflow & comms cluster + Onboarding wizard." Decomposed during this brainstorm into three sub-phases (5a, 5b, 5c) for reviewable PR sizes.

This is the first of three Phase 5 PRs. After 5a ships, the workflow cluster is on J4 Spectrum; communication, webhooks, import-export, comments-activity, and onboarding remain in 5b/5c.

---

## 1. Goal

Bring all 6 workflow pages onto J4 Spectrum tokens, with **two structural changes** earned by data shape:

1. **Command-center inbox** — `WorkflowInboxPage` gains a 3-card status hero (`Overdue` / `Due today` / `Upcoming`) and **per-row SLA pressure signaling** (progress bar, relative-time chip, priority dot, tinted definition chip). The page communicates urgency at-a-glance instead of being a flat checkbox table.
2. **Sticky right-rail instance detail** — `WorkflowInstanceDetailPage` moves from single-column 5-stacked-cards to asymmetric 2-col on `lg+`: main column scrolls (timeline → form data → comments), right rail stays sticky with status header + pending action + metadata. Approve/reject buttons are always in view as approvers scroll form data. Mirrors Linear / GitHub issue convention; earned by the action-driven nature of the page.

The remaining 4 pages get hero strips + token sweep on Phase 3 / Phase 4 precedent. The Designer surface gets chrome polish + state-type tinting with **zero ergonomic regression** — drag/drop, hit targets, edges, and edit semantics are unchanged.

## 2. Non-goals

Locked in at brainstorm; listed up front so they don't get relitigated:

- **No new functionality.** No bulk-cancel on the Instances list, no force-advance admin action on Instance detail, no SLA-policy editor, no per-version definition diff, no inbox quick-filter chips beyond what exists, no duplicate-instance action, no inline edit on Definitions list.
- **No animated active-state pulse on the Designer.** Q3 ruled this out (option C). Read-only previews show current state via a static badge overlay on the running state, not a per-node animated glow.
- **No `<Tabs>` on Instance Detail.** The 2-col + sticky right rail is the structural change. Splitting timeline/form/comments across tabs is a separate IA decision.
- **No 2-col on Definition Detail.** It's a read-only narrative page; the editing surface is the Designer route. A second column would be empty for ~90% of definitions.
- **No mobile-specific reflow** beyond standard `lg+` breakpoint stacking. Right rail collapses to a top section on `<lg`; designer canvas already requires `lg+` ergonomically (out of scope to fix here).
- **No translation deferral.** EN + AR + KU land inline with each component change. Phase 3 / 4 confirmed inline works; Phase 2's deferred-translation cost is not repeated.
- **No backend changes beyond the two new status-counts endpoints.** No new entities, no schema migrations, no new permissions, no DTO field additions on the existing list/detail endpoints.
- **No onboarding wizard, sidebar IA, permission, or routing changes.** Onboarding is Phase 5c.
- **No `useCountUp` consolidation.** Tracked from post-phase-2 deferred list; revisit when a 4th consumer appears.
- **No state-diff UX on the timeline** (paired before/after). Same rationale as Phase 2: requires BE schema changes; out of scope.

## 3. Pages

### 3.1 WorkflowInboxPage *(authenticated end users — `/workflows/inbox`, perm `Workflows.View`)*

Today: `<PageHeader>` + delegation banner (conditional) + checkbox-table of pending tasks (Definition Name / Request / Step / Assigned / Actions) + bulk action bar + pagination + `<EmptyState>`. ~352 LOC. No SLA signaling, no priority signaling, no metric strip — the page treats every task identically. The daily-driver page for end users.

Redesign:

- **3-card status hero** above the delegation banner, using the shared `<MetricCard>` (Phase 3 + 4 pattern). Cards:
  - `Overdue` — `tone="destructive"` (red treatment; tasks past their SLA).
  - `Due today` — `tone="active"` (copper; the hero metric — the user should triage these first).
  - `Upcoming` — `tone="default"` (neutral; everything beyond today).
- **Collapse-when-zero rule.** Cards with a zero count collapse out of the row; survivors re-flow. When all three are zero (no pending tasks), the hero hides entirely and the page falls through to the existing "Inbox empty" `<EmptyState>`.
- **Counts come from a new BE endpoint** (see §4) — `GET /api/v1/workflow/inbox/status-counts`. The current user's pending tasks bucketed by SLA. Tenant-scoped via existing module DbContext filter.
- **Per-row SLA pressure signaling — the structural change for the page.** Each table row gets a redesigned `<InboxTaskRow>` cell stack (replaces the inline `<TableRow>`):
  - **SLA progress bar** — a thin horizontal bar inside the row (col 1 alongside the checkbox or stacked under the request name). Width derived from `(now − raisedAt) / (slaDueAt − raisedAt)`; color tone progresses green → amber → red. Bar collapses (renders as null) if the task has no `slaDueAt`.
  - **Pressure chip** — relative-time text: `"due in 2h"` (green/amber/red bg per pressure), `"3 days overdue"` (red, with `AlertCircle` icon), `"on track"` (muted). Uses `formatDistanceToNow` from `date-fns` (already a dep).
  - **Priority dot** — small colored dot LTR-leading the request name. Mapped from `task.priority` if the BE field exists; otherwise derived from SLA pressure (overdue → red, due-today → amber, else → muted). Verify field availability during implementation; if not present on `PendingTaskSummary`, derive purely from SLA.
  - **Definition-name chip** — replaces the plain text cell. Uses `text-[var(--tinted-fg)]` and a 1px tinted border (`var(--active-border)` at low opacity). Matches Phase 2 platform-admin tenant chip treatment.
- **Bulk action bar / delegation banner / new-request button** — unchanged behavior; tokens swept. Bulk-eligibility logic (`requiresForm` filter) stays as-is.
- **Pagination** — uses shared `<Pagination>` with persisted page size — already on-pattern.
- **Empty state** — uses shared `<EmptyState>` — already on-pattern.

### 3.2 WorkflowInstanceDetailPage *(authenticated end users + admins — `/workflows/instances/:id`, perm `Workflows.View`)*

Today: `<PageHeader>` + 5 stacked cards in a single column up to `max-w-2xl`-ish — Status info card, Resubmit notice (conditional), Pending action (conditional, with approve/reject), Step timeline (full `<WorkflowStepTimeline>`), Comments & activity slot. Two `<Slot>` extensions below. ~375 LOC. Approve/reject buttons scroll out of view as the user reads form data — the operational pain point.

Redesign — **asymmetric 2-column layout on `lg+`, single-column stack on `<lg`.**

#### Layout invariants

These are explicit design constraints the implementation must honor — calling them out so the code review pass has unambiguous criteria:

- **Two columns on `lg+`** — ratio approximately 2:1 (main : right-rail). Use `grid-cols-1 lg:grid-cols-[minmax(0,1fr)_320px]` or equivalent. Right rail width fixed at ~320px to prevent layout shift; main column flexes.
- **Right rail is `sticky top-{n}` on `lg+`** — `top-` value matches the floating-glass shell's header offset (already exposed as `--shell-header-h` CSS var; reuse it). Falls back to `position: static` on `<lg` (Tailwind's `lg:sticky` does this automatically).
- **Right rail order**: status header → pending action (if present) → metadata. The pending action sits **between** status and metadata, not at the bottom — so it's the second thing the user sees in the rail.
- **On `<lg`**, right rail collapses to a top section above main column (status → pending action → metadata stacked first, then timeline → form data → comments below). Same DOM order on both breakpoints; only the grid direction changes.
- **Main column scrolls independently** of the right rail. Sticky positioning relies on the page-level scroll container; do not introduce a nested overflow scroll on either column.

#### Component shape

- **`<WorkflowStatusHeader>`** *(new, shared with Definition Detail §3.5)* — gradient-text title (instance display name) + status pill (via `STATUS_BADGE_VARIANT`) + chips (definition name as tinted chip, raised-by, started-at). Used as the right-rail header on Instance Detail and as the top-of-page header on Definition Detail. One component, two surfaces.
- **Pending action card** *(restyled)* — keeps the existing approve/reject/return-for-revision buttons; gains `tone="active"` glass treatment (copper-tinted background) so it stands out as the action area. If `myTask` is null (user is not the assignee), the card is omitted entirely.
- **Metadata block** *(restyled, new container `<InstanceMetadataRail>`)* — copyable fields with hover-reveal copy buttons (matches Phase 2 `<AuditMetadataCard>` treatment): instance ID, definition name (linked to definition detail), entity type, entity ID, started-at, completed-at (if present), started-by, current state. SuperAdmin sees an additional `tenantId` row.
- **Step timeline (`<WorkflowStepTimeline>`)** — main column, top section. Component itself is unchanged — token sweep only on its internal styling. Replace any hardcoded primary shades and bare `bg-muted/30` form-data sub-cards with semantic tokens.
- **Form data submitted** — main column, second section. Today inline within the timeline component's per-record render. Stays inline; tokens swept.
- **Comments & activity slot** — main column, bottom section. The existing `<Slot id="entity-detail-timeline" props={{ entityType: 'WorkflowInstance', entityId }} />` invocation is unchanged. Inherits whatever Phase 5b polishes for the comments-activity slot.
- **Resubmit notice** — when present, becomes a full-width banner above the 2-col grid (not in the main column). It's a state-of-the-page signal, not part of the narrative or the action.
- **Cancel dialog** — unchanged.

#### Mobile (`<lg`)

Single-column stack: resubmit notice → status header → pending action → metadata → timeline → form data → comments. The right rail's content is just the first three sections on small screens.

### 3.3 WorkflowInstancesPage *(admins + observers — `/workflows/instances`, perm `Workflows.View` plus `Workflows.ViewAll` for cross-user)*

Today: `<PageHeader>` (with NewRequest button) + filter row (definition / entity-type selects + my-requests-only toggle for super-admins) + table + pagination. ~234 LOC. Status pill column already uses `STATUS_BADGE_VARIANT` correctly.

Redesign:

- **4-card status hero** above the filter row, using shared `<MetricCard>`:
  - `Active` — `tone="active"` (running instances; the hero metric).
  - `Awaiting action` — `tone="default"` (instances with at least one pending task).
  - `Completed` — `tone="default"` (terminal success states).
  - `Failed-or-cancelled` — `tone="destructive"` (terminal failure states + user-cancelled).
- **Collapse-when-zero** — same rule as everywhere else; if a tenant has zero instances total, hero hides and existing `<EmptyState>` shows.
- **Counts come from a new BE endpoint** (see §4) — `GET /api/v1/workflow/instances/status-counts`. Honors the existing `myRequestsOnly` and tenant filters. SuperAdmin's tenant-filter selection updates the hero.
- **Filter row** — keep current shape. Ensure all `<Select>` triggers use the standard token sweep; no structural change.
- **Table** — unchanged columns. Status pill column already correct. Definition-name cell becomes a `text-[var(--tinted-fg)]` chip (matches Phase 2 tenant chip treatment).
- **Pagination + EmptyState** — already on-pattern.

### 3.4 WorkflowDefinitionsPage *(admins — `/workflows/definitions`, perm `Workflows.Manage`)*

Today: `<PageHeader>` + table (Name / EntityType / Steps / Source / Status / Actions) + pagination + `<EmptyState>`. ~128 LOC. Uses `Badge variant={def.isActive ? 'default' : 'secondary'}` inline mapping; abuses `workflow.definitions.activate`/`deactivate` translation keys as labels for active/inactive status.

Redesign:

- **Token sweep only.** No hero (a list of ~5–20 definitions doesn't earn a metric strip).
- **Status badge cleanup** — extend `STATUS_BADGE_VARIANT` in `@/constants/status.ts` with `Active` / `Inactive` keys mapped to `default` / `secondary`. Replace inline mapping. Add proper translation keys `workflow.definitions.status.{active,inactive}` (EN + AR + KU). Remove the abused `activate`/`deactivate` label usage from the row (those keys remain only for the actual button labels they were originally for).
- **Source column** — `Badge variant={def.isTemplate ? 'outline' : 'default'}` mapping is fine; tokens sweep handles it.
- **Action buttons** — Clone / Edit unchanged. Tokens swept.
- **Table** — uses shared `<Table>` (no extra `<Card>` wrapper).

### 3.5 WorkflowDefinitionDetailPage *(admins — `/workflows/definitions/:id`, perm `Workflows.Manage`)*

Today: `<PageHeader>` + Tabs (Overview, Analytics) + Overview tab content (definition info + states list + transitions list) + Analytics tab (`<WorkflowAnalyticsTab>` with `<HeadlineStrip>`). ~248 LOC. Read-only narrative — the editing surface is the Designer route.

Redesign:

- **Glass header card** — replaces the plain `<PageHeader>` block at the top. Uses the shared `<WorkflowStatusHeader>` (introduced for Instance Detail §3.2): gradient-text title + status pill (Active/Inactive) + chips (entity type, source, step count). Edit/Clone/Designer action buttons remain in the header — token-swept.
- **Read-only mini-canvas embed on Overview tab** — replaces the bare states/transitions text lists with a read-only `<DesignerCanvas readOnly>` instance. Uses the §3.6 designer treatment (state-type tinting, glass chrome, dot grid). Sized at e.g. `min-h-[420px] max-h-[60vh]`. Pulls states/transitions via the existing `useWorkflowDefinition` query — no extra fetch.
  - Below the canvas, keep a collapsible "JSON view" section for the raw states/transitions JSON (read-only, syntax-highlighted via the existing `<JsonView>` component from Phase 2). Default collapsed.
- **Analytics tab** — existing `<WorkflowAnalyticsTab>` and `<HeadlineStrip>` are unchanged structurally; token sweep only. Replace any hardcoded shades.
- **Tabs primitive** — already uses the shared `<Tabs>` component; tokens swept.

### 3.6 WorkflowDefinitionDesignerPage *(admins — `/workflows/definitions/:id/designer`, perm `Workflows.Manage`)*

Today: full-canvas designer page (`h-[calc(100vh-5rem)]`). `<PageHeader>` + read-only banner (when template) + `<DesignerToolbar>` + flex split (`<DesignerCanvas>` + `<SidePanel>`). ReactFlow-based; ~160 LOC for the page, 7 designer components totaling ~720 LOC.

Redesign — **chrome polish + state-type tinting; zero ergonomic regression.**

- **`<DesignerToolbar>`** — gains `surface-glass` treatment (translucent over canvas). Save / Auto-layout / +State buttons retain shapes; primary action ("Save") uses gradient-button treatment. Saving spinner uses standard `<Spinner>`.
- **`<SidePanel>`** — gains `surface-glass` treatment. Section dividers use semantic tokens.
- **`<DesignerCanvas>`** background — adds a subtle dot grid via CSS background-image, color derived from `var(--border)` at low opacity. ReactFlow's existing pan/zoom/select behavior is unchanged.
- **`<StateNode>`** — gains type-aware tinting via the J4 spectrum companion scales already registered in `index.css`:
  - **Initial states** — emerald-tinted border + tinted dot indicator. Uses `--color-emerald-{100,500,700}`.
  - **HumanTask states** — copper-tinted border + tinted dot. Uses `--active-border` and primary tokens.
  - **Final states** — neutral muted border + neutral indicator. Uses `--border-strong`.
  - **Other state types** — fall back to neutral muted (so adding new state types in the future doesn't visually break).
  - **Selected state** (designer-mode focus ring) — keeps the existing copper outline; intensity bumped via `--glow-primary-sm` halo.
  - Tooltip text added on each node showing the state type name (`workflow.designer.stateType.{initial,humanTask,final}`).
- **`<TransitionEdge>`** — unchanged behavior; tokens swept (replace any hardcoded grays with `var(--border)` / `var(--muted-foreground)`).
- **Read-only banner (template)** — restyled as a tone="default" glass strip with the existing Clone-to-edit button. Tokens swept.
- **Drag/drop, hit targets, ReactFlow node config, edit semantics, dirty tracking, navigate-away guard, before-unload handler — all unchanged.**
- **Read-only mode** — when invoked from Definition Detail (§3.5) as a mini-preview, the canvas hides toolbar + side panel; the tinting still applies. The component already supports a `readOnly` prop; verify it suppresses pan/zoom controls (or accept that they remain — pan/zoom are non-destructive).
- **Running-state badge overlay** *(when read-only and at least one running instance is in a given state)* — small numeric badge in the top-right of the state node (e.g. `3` running). Pulls from a new query (out of scope for this PR — defer to Phase 4b analytics work that already has instance-by-state counts; if not already in the analytics endpoint, defer to Phase 5b/c). Mark as **deferred** if data is not already available; the static tinting is the floor we ship.

## 4. Backend additions

Two new query handlers in `Starter.Module.Workflow`. Both reuse the existing `WorkflowInstance` and `WorkflowTask` entities; **no schema changes, no migrations.**

### 4.1 `GetInboxStatusCountsQuery`

```csharp
public sealed record GetInboxStatusCountsQuery() : IRequest<Result<InboxStatusCountsDto>>;

public sealed record InboxStatusCountsDto(int Overdue, int DueToday, int Upcoming);
```

- Handler: `GetInboxStatusCountsQueryHandler` in `Application/Features/Workflow/Inbox/Queries/`.
- Scoping: filter `WorkflowTasks` by `currentUser.UserId` (assignee), `Status == Pending`. Tenant filter applied by the existing `WorkflowDbContext` global query filter.
- Buckets:
  - `Overdue` — `task.SlaDueAt < DateTimeOffset.UtcNow && task.SlaDueAt.HasValue`
  - `DueToday` — `task.SlaDueAt >= now && task.SlaDueAt < now.Date.AddDays(1)`
  - `Upcoming` — everything else (including no-SLA tasks, which always count as upcoming).
- Endpoint: `GET /api/v1/workflow/inbox/status-counts` on `WorkflowController`. Auth: `[Authorize(Policy = Permissions.Workflows.View)]`.
- No `[AiTool]` decoration — this is a UI-only query.

### 4.2 `GetInstanceStatusCountsQuery`

```csharp
public sealed record GetInstanceStatusCountsQuery(
    bool MyRequestsOnly,
    Guid? TenantId,
    Guid? DefinitionId,
    string? EntityType
) : IRequest<Result<InstanceStatusCountsDto>>;

public sealed record InstanceStatusCountsDto(int Active, int Awaiting, int Completed, int Failed);
```

- Handler: `GetInstanceStatusCountsQueryHandler` in `Application/Features/Workflow/Instances/Queries/`.
- Scoping: honors `MyRequestsOnly` (filter to `instance.StartedByUserId == currentUser.UserId`), the existing tenant filter (super-admin can pass `TenantId`; tenant users ignore the param), and the optional `DefinitionId` / `EntityType` filters that the list page already passes.
- Buckets (mirror `WorkflowInstance.Status` enum):
  - `Active` — `Status == Running` (no awaiting task) or any non-terminal that isn't Awaiting.
  - `Awaiting` — `Status == Running` AND has at least one pending `WorkflowTask`. (Implementation: groupwise check via `WorkflowTasks.Any(t => t.Status == Pending)`.)
  - `Completed` — `Status == Completed`.
  - `Failed` — `Status In (Failed, Cancelled)`.
- Endpoint: `GET /api/v1/workflow/instances/status-counts?myRequestsOnly=&tenantId=&definitionId=&entityType=` on `WorkflowController`. Auth: `[Authorize(Policy = Permissions.Workflows.View)]`.
- Tenant-trust boundary: `TenantId` is server-validated — if the caller is not a super-admin, the param is ignored and the filter falls through to the global tenant filter. Same pattern as the existing `GetWorkflowInstancesQuery`.

### 4.3 No DTO field additions

`PendingTaskSummary` already carries `slaDueAt`, `raisedAt`, `definitionName`. No need to extend. If `priority` is not present today and not derivable from existing fields, the FE derives priority purely from SLA pressure (overdue → high, due-today → medium, else → low) — verify during implementation.

## 5. Frontend components

### 5.1 New components (3)

**`<InboxTaskRow>`** *(new — `src/features/workflow/components/InboxTaskRow.tsx`)*
- Replaces the inline `<TableRow>` rendering in `WorkflowInboxPage`.
- Encapsulates: checkbox + priority dot + tinted definition chip + request name + SLA progress bar + pressure chip + assigned-step + action button.
- Props: `task: PendingTaskSummary`, `selected: boolean`, `onToggle: () => void`, `onAct: () => void`, `bulkEligible: boolean`.
- ~80 LOC.

**`<WorkflowStatusHeader>`** *(new — `src/features/workflow/components/WorkflowStatusHeader.tsx`)*
- Gradient-text title + status pill + chips. Used on Instance Detail right rail and Definition Detail top header. One component, two surfaces.
- Props: `title: string`, `status: string`, `statusVariant: BadgeVariant`, `chips: Array<{ icon?: ReactNode; label: string; tinted?: boolean }>`, `actions?: ReactNode`.
- ~60 LOC.

**`<InstanceMetadataRail>`** *(new — `src/features/workflow/components/InstanceMetadataRail.tsx`)*
- The right-rail container for Instance Detail. Sticky on `lg+`, becomes a top section on `<lg`.
- Renders: `<WorkflowStatusHeader>` + pending action card (if `myTask`) + metadata field list with hover-copy.
- Props: `instance: WorkflowInstanceDto`, `myTask: PendingTaskSummary | null`, `onAct: (task) => void`, `isSuperAdmin: boolean`.
- ~120 LOC.

### 5.2 Reused components

- `<MetricCard>` (Phase 4 Billing) — for both hero strips on Inbox and Instances list.
- `<DesignerCanvas readOnly>` (existing, ~65 LOC) — for the Definition Detail mini-preview embed.
- `<WorkflowStepTimeline>` — unchanged structurally; tokens swept.
- `<Slot id="entity-detail-timeline" />` — unchanged.
- `<ApprovalDialog>`, `<DelegationDialog>`, `<NewRequestDialog>`, `<BulkActionBar>`, `<BulkConfirmDialog>`, `<BulkResultDialog>`, `<ConfirmDialog>` — all unchanged, tokens swept.
- `<JsonView>` (Phase 2) — for the optional collapsible JSON section on Definition Detail.

### 5.3 New hooks

- `useInboxStatusCounts()` — wraps `workflowApi.getInboxStatusCounts`. TanStack Query, no params.
- `useInstanceStatusCounts(filters)` — wraps `workflowApi.getInstanceStatusCounts`. Keyed on the same filters as `useWorkflowInstances`.

Pattern matches Phase 4 Billing's `useSubscriptionStatusCounts` exactly.

## 6. Tokens, styling, J4 utilities

- All hardcoded primary shades swept (audit `bg-primary-{50..950}`, `text-primary-{50..950}`, `border-primary-{50..950}` across the 6 pages + 8 designer components + dialogs + the timeline component).
- Designer chrome uses existing `surface-glass` utility from `src/styles/index.css` — no new utility classes.
- Designer canvas dot grid: pure CSS, e.g. `background-image: radial-gradient(var(--border) 1px, transparent 1px); background-size: 14px 14px;`.
- State-node tinting uses existing J4 spectrum companion scales (`--color-emerald-*`, primary tokens, `--border-strong`). No new tokens.
- Pressure-chip background colors derive from semantic state tokens: green = `state-active` background variant, amber = a new `state-warn` if not already present (likely needed — verify; if not, use a warm token like `bg-amber-50 dark:bg-amber-950/30` strictly inside the row component, with comment justifying the exception).
- SLA progress bar: gradient from `var(--color-emerald-500)` → `var(--active-bg)` (copper amber) → `var(--destructive)` based on percentage. Uses CSS `linear-gradient` with `width: {pct}%`.

## 7. Translation scope

EN + AR + KU inline. Estimated ~28 new keys:

- `workflow.inbox.statusCounts.{overdue,dueToday,upcoming}` (3)
- `workflow.inbox.sla.{dueIn,overdue,onTrack,noSla}` (4)
- `workflow.inbox.priority.{high,medium,low}` (3)
- `workflow.instances.statusCounts.{active,awaiting,completed,failed}` (4)
- `workflow.definitions.status.{active,inactive}` (2)
- `workflow.detail.metadata.{copy,copied,startedBy,raisedAt,instanceId,entityId,definitionLink}` (7)
- `workflow.detail.pendingActionTitle` (1)
- `workflow.designer.stateType.{initial,humanTask,final,other}` (4)

Removed/reassigned: `workflow.definitions.activate` and `workflow.definitions.deactivate` retain their original button-label usage but are no longer abused as status labels.

## 8. Backend permissions

No new permissions. Both new endpoints reuse `Permissions.Workflows.View`, identical to the existing list/inbox endpoints.

## 9. Testing & verification

- **Unit tests** — handler tests for both new query handlers (`GetInboxStatusCountsQueryHandlerTests`, `GetInstanceStatusCountsQueryHandlerTests`) covering tenant scoping, super-admin cross-tenant via `TenantId` param, `myRequestsOnly` toggle, and the bucket boundaries (overdue / due-today / upcoming on the inbox; active / awaiting / completed / failed on instances).
- **Architecture tests** — none new.
- **Frontend lint + typecheck + build** — must pass before commit.
- **Live test in test app** — `_testJ4visual` test app gets the FE diff copy-pasted (per established cadence) and is exercised via Chrome DevTools MCP / Playwright at:
  - **Inbox** — login as a tenant user with at least 5 pending tasks spanning overdue/today/upcoming; verify hero counts match table reality, SLA bars render, pressure chips show correct color, definition chips are tinted.
  - **Instance Detail** — login as an approver with a pending task; verify right rail is sticky on `lg+`, approve buttons stay in view while scrolling form data, layout collapses correctly on `<lg`. Verify both the resubmit notice (returned-for-revision instance) and the no-pending-action variant.
  - **Instances list** — login as super-admin with multi-tenant data; verify hero counts respect the tenant filter and the my-requests-only toggle.
  - **Definitions list** — verify status badges show the new active/inactive treatment and translations.
  - **Definition Detail** — verify the read-only mini-canvas renders with state-type tinting; verify the analytics tab is unchanged structurally.
  - **Designer** — verify drag/drop, edit, save, navigate-away guard all work identically; verify state-type tinting renders for Initial/HumanTask/Final on a real definition; verify the read-only template banner clones correctly.
- **No regression to existing features** — Phases 0–4 surfaces unchanged.

## 10. Branch, PR shape, commit cadence

- **Branch:** `fe/phase-5-design` (already created off `origin/main` before brainstorm).
- **Commits:** land directly on the working branch (no per-task feature branches within the plan, matching Phase 0–4 cadence).
- **Final review pass:** `superpowers:code-reviewer` before push.
- **PR title:** `feat(fe): Phase 5a Workflow — workflow cluster (6 pages + read-only canvas embed)`.
- **PR body:** spec + plan links, deferred-list, BE additions flagged (two new query handlers + endpoints), test app verification screenshots, and a callout that this is the first of three Phase 5 PRs (workflow / communication / odds-and-ends).

## 11. Open questions for the plan stage

- **Designer running-state badge overlay (§3.6).** Whether the analytics module already exposes "instances currently in state X" counts. If yes, wire it; if no, defer the badge to a follow-up. Static tinting is the floor we ship in either case.
- **Priority field on `PendingTaskSummary`.** Whether it exists today. If yes, prefer it over SLA-derived priority. If no, the FE derives priority from SLA pressure (no BE change).
- **Pressure-chip amber token.** Whether a `state-warn` semantic token already exists. If not, decide during the plan stage between adding one (preferred — recurring need) vs an inline `amber-50/950` exception with a justification comment.
- **Right-rail sticky offset.** The exact value to use for `top-`. The floating-glass shell's header offset should be exposed as `--shell-header-h`; if it isn't, expose it during this plan and consume it.

## 12. After 5a ships

The remaining Phase 5 work, decomposed during this brainstorm:

- **Phase 5b** — Communication cluster: `TemplatesPage`, `TriggerRulesPage`, `ChannelsPage`, `IntegrationsPage`, `DeliveryLogPage` + 5+ dialogs.
- **Phase 5c** — Bundled odds-and-ends: Webhooks (3 pages), Import/Export (1 page + `ImportWizard` dialog), Comments-Activity slots (used inline across the app), Onboarding wizard.

Each gets its own brainstorm → spec → plan → execution cycle, branched off `origin/main` after the predecessor merges (Phase 0–4 cadence).
