# Workflow Phase 1+2a Cleanup + Docs Reorganization

**Date:** 2026-04-20
**Status:** Approved design
**Branch target:** `feature/workflow-approvals` (current, 45 commits)
**Depends on:** Phase 1 and Phase 2a (both already committed on this branch)
**Next phase:** After this merges, `feature/workflow-phase2b` branches from `main`

---

## Context

The `feature/workflow-approvals` branch has 45 commits covering Phase 1 (core state-machine engine) and Phase 2a (dynamic forms, parallel approvals, SLA escalation, delegation, compound conditions). All 144 unit tests pass; both backend and frontend builds are clean.

Seven known issues remain from Phase 2a. Five are merge-worthy fixes that fit in a small scope; two (full `WorkflowEngine.cs` extraction; live SLA badge QA) are deliberately deferred.

Alongside the code fixes, module documentation is inconsistent across the three module directories, and there is no canonical end-user or developer documentation home at the repo root. This PR fixes both in one pass because:

1. The branch is already at a natural "module works end-to-end" plateau — a clean merge point.
2. Docs reorganization should land with the feature it documents, not in a follow-up PR that goes stale.
3. Phase 2b branch off `main` stays unblocked by conflicts if this branch keeps accumulating.

## Goals

- Fix 5 merge-worthy known issues.
- Document the deferred `WorkflowEngine.cs` refactor as a first-class known issue, not a handoff-note TODO.
- Create a canonical `docs/user-manual/` and `docs/developer-guide/` at repo root.
- Consolidate per-module docs (currently scattered across `boilerplateBE/src/modules/*/docs/`, `*/DEVELOPER_GUIDE.md`, `*/ROADMAP.md`, and `boilerplateBE/docs/`) into the repo-level structure.
- Leave 1-line redirect stubs in module folders so developers browsing a module still find their way to the canonical docs.

## Non-goals

- Refactoring `WorkflowEngine.cs` (deferred — own PR).
- Rewriting existing doc content (moves + mild header cleanup only, plus 3 new files).
- Screenshots or GIFs in the user manual (text-only this pass).
- Touching `docs/superpowers/` (session artifacts, keep in place).
- Touching `docs/testing/` (regression logs, keep in place).
- Auto-generated TOCs — hand-written `README.md` index files.

---

## Part 1: Code Fixes

### Fix #3: Raise `WorkflowTaskEscalatedEvent`

**Problem.** The event is defined in `Domain/Events/WorkflowDomainEvents.cs` but `SlaEscalationJob.EscalateTaskAsync` never publishes it. Consumers (audit trail, analytics, cross-module webhooks) cannot react to escalations. This is dead code in the public API surface.

**Design.** After `dbContext.ApprovalTasks.Add(escalatedTask)`, raise the event on the `WorkflowInstance` aggregate. Skip the event when either original or new assignee is null — the event's signature requires both (`Guid`, not `Guid?`), and an unassigned→unassigned handoff carries no meaningful semantic. Notifications still fire on escalation regardless, since those are a separate concern.

```csharp
if (originalAssigneeId.HasValue && newAssigneeId.HasValue)
{
    task.Instance.AddDomainEvent(new WorkflowTaskEscalatedEvent(
        escalatedTask.Id,
        task.InstanceId,
        originalAssigneeId.Value,
        newAssigneeId.Value,
        task.StepName,
        task.Instance.EntityType,
        task.Instance.EntityId,
        task.TenantId));
}
```

**Test.** Unit test: seed an overdue task with escalation config pointing at a fallback user, run `ProcessOverdueTasksAsync`, assert the event is in the instance's domain events collection. Verify it's NOT raised when no fallback assignee resolves.

**Files.** `Infrastructure/Services/SlaEscalationJob.cs`; new test in `tests/Starter.Api.Tests/Workflow/`.

---

### Fix #5: Timeline uses history records, not array index

**Problem.** `boilerplateFE/src/features/workflow/components/WorkflowStepTimeline.tsx::getStepStatus` computes step completion by comparing `findIndex` positions in the `states` array. This assumes a linear progression. For any branching workflow (e.g., `Draft → ReviewA → Rejected` where `ReviewB` and `Approved` come later in `states[]`), the unvisited later states render as "completed" — misleading the user.

**Design.** A state is `current` iff its name matches `currentState`. A state is `completed` iff the instance's step history contains a record with `toState === state.name` AND the state is not the current state. All other states are `future`. This makes the timeline history-driven, which is also what users naturally want to see.

```tsx
function getStepStatus(
  stateName: string,
  currentState: string,
  history: WorkflowStepRecord[],
): StepStatus {
  if (stateName === currentState) return 'current';
  return history.some(r => r.toState === stateName) ? 'completed' : 'future';
}
```

The array layout (ordering of rendered rows) stays as-is — this fix only changes the status badge logic. States that the instance never visited render in the `future` style.

**Test.** Vitest unit test in `boilerplateFE/src/features/workflow/components/__tests__/` covering three cases: linear completion, branching rejection (visit A, skip B, jump to Rejected final), and loop-back (return-to-draft → resubmit).

**Files.** `boilerplateFE/src/features/workflow/components/WorkflowStepTimeline.tsx`; new test file co-located.

---

### Fix #4: DB-level pagination for pending tasks

**Problem.** `GetPendingTasksQueryHandler` materializes the full tenant's task list via `.ToListAsync()`, then slices with `Skip`/`Take` in memory. Degrades at roughly 500 pending tasks per tenant.

**Design.** Push `Skip(pageSize * (page - 1)).Take(pageSize)` into the `IQueryable` before materialization. Compute `totalCount` via a second query on the same `IQueryable` *before* applying `Skip`/`Take`. Return `PagedResult<PendingTaskSummary>` (already the standard shape — matches other paginated queries in the codebase).

`IUserReader` display-name lookups stay post-materialization — they only run for the current page's rows now. If lookups become a bottleneck later, that's the "denormalized inbox" roadmap item (own PR).

**API signature change.** `IWorkflowService.GetPendingTasksAsync` currently returns `Task<IReadOnlyList<PendingTaskSummary>>`. Update to accept `(Guid userId, int page, int pageSize, CancellationToken ct)` and return `Task<PagedResult<PendingTaskSummary>>`. Update:
- the query + handler
- `IWorkflowService` contract in `Starter.Abstractions/Capabilities/`
- `NullWorkflowService` null-object implementation
- the controller endpoint + response DTO
- the frontend API hook in `boilerplateFE/src/features/workflow/api/` and any component consuming the list shape (inbox page)

Controller DTO and frontend query hook: align to the paginated shape across the board.

**Test.** Unit test that seeds 25 tasks, calls the handler with `page: 2, pageSize: 10`, and asserts:
- returned count is 10
- `Total` is 25
- returned items are ordered correctly and match tasks 11-20

**Files.** `Application/Queries/GetPendingTasks/GetPendingTasksQueryHandler.cs`; `Application/Queries/GetPendingTasks/GetPendingTasksQuery.cs`; `Starter.Abstractions/Capabilities/IWorkflowService.cs` (if exposed); frontend query hook if interface shape changes.

---

### Fix #7: State-type magic strings → constants

**Problem.** String literals `"Initial"`, `"Approval"`, `"Action"`, `"Final"` (and any HumanTask/SystemAction variants in use) appear in `WorkflowEngine`, condition evaluators, validators, and handlers. Typos fail silently (treated as unknown state type); refactoring is grep-dependent.

**Design.** New file `Domain/Constants/WorkflowStateTypes.cs`:

```csharp
namespace Starter.Module.Workflow.Domain.Constants;

public static class WorkflowStateTypes
{
    public const string Initial = "Initial";
    public const string HumanTask = "HumanTask";
    public const string SystemAction = "SystemAction";
    public const string Final = "Final";
    // ... confirm full set during implementation — no additions beyond what exists
}
```

Pure grep-replace — no behavior change. JSON definition strings stay identical (seed definitions and persisted instances must continue to work).

**Test.** Existing tests re-run green after the replacement is sufficient evidence. No new tests needed.

**Files.** New `Domain/Constants/WorkflowStateTypes.cs`; every file that currently compares against the literal values.

---

### Fix #1: Delegation dialog date serialization (ISO 8601)

**Problem.** The delegation dialog submit path may send date inputs as locale-formatted strings. Backend expects ISO 8601. Failure is locale-conditional.

**Design.** In the delegation dialog submit handler, convert both `startDate` and `endDate` via `new Date(value).toISOString()` before including them in the request body. Same treatment anywhere else the dialog serializes dates. If the underlying input is a native `<input type="date">`, its `value` is already `YYYY-MM-DD` — we still wrap in `toISOString()` to get full timestamp with Z suffix that the backend `DateTime` parser accepts unambiguously.

**Test.** Live: set system locale to `tr-TR` (Turkish — known to cause issues), submit a delegation, verify backend receives ISO dates. Unit: parse the submit payload, assert `startDate.endsWith('Z')` and matches `YYYY-MM-DDTHH:MM:SS.sssZ` regex.

**Files.** `boilerplateFE/src/features/workflow/components/DelegationDialog.tsx` (or wherever the submit handler lives).

---

## Part 2: Docs Reorganization

### Final structure (repo root `docs/`)

```
docs/
├── README.md                           # NEW — index + navigation
├── user-manual/
│   ├── README.md                       # NEW — TOC
│   ├── getting-started.md              # NEW — tenant onboarding basics
│   └── modules/
│       ├── workflow.md                 # NEW
│       ├── communication.md            # MOVE from boilerplateBE/src/modules/Starter.Module.Communication/docs/user-manual.md
│       └── comments-activity.md        # NEW
├── developer-guide/
│   ├── README.md                       # NEW — TOC
│   ├── getting-started.md              # MOVE from docs/workstation-setup.md
│   ├── architecture/
│   │   ├── system-design.md            # MOVE from docs/architecture/system-design.md
│   │   ├── cross-module-communication.md  # MOVE from docs/architecture/
│   │   └── module-development.md       # MOVE from docs/architecture/module-development-guide.md
│   ├── modules/
│   │   ├── workflow.md                 # NEW (includes #6 known issue section)
│   │   ├── communication.md            # MOVE from Communication module
│   │   ├── comments-activity.md        # MOVE from CommentsActivity/DEVELOPER_GUIDE.md
│   │   ├── feature-flags.md            # MOVE from boilerplateBE/docs/
│   │   ├── observability.md            # MOVE from boilerplateBE/docs/observability-setup.md
│   │   └── webhooks.md                 # MOVE from boilerplateBE/docs/webhook-testing-guide.md
│   ├── theming.md                      # MOVE from docs/theming-guide.md
│   └── domain-module-example.md        # MOVE from docs/D2-domain-module-example.md
├── roadmaps/
│   ├── workflow.md                     # MOVE from module ROADMAP.md + append #6
│   ├── communication.md                # MOVE from module ROADMAP.md
│   ├── comments-activity.md            # MOVE from module ROADMAP.md
│   └── product-roadmap.md              # MOVE from docs/future-roadmap.md
├── product/
│   └── market-assessment.md            # MOVE from docs/
├── testing/                            # UNCHANGED
├── session-handoff.md                  # UNCHANGED (transient)
└── superpowers/                        # UNCHANGED
```

### Redirect stubs in module folders

Each file being moved out of a module folder is replaced with a 1-line redirect so a developer browsing the module still finds the canonical doc:

```markdown
> This document has moved to [`/docs/developer-guide/modules/workflow.md`](../../../../docs/developer-guide/modules/workflow.md).
```

Stub files:
- `boilerplateBE/src/modules/Starter.Module.Workflow/ROADMAP.md`
- `boilerplateBE/src/modules/Starter.Module.Communication/ROADMAP.md`
- `boilerplateBE/src/modules/Starter.Module.Communication/docs/user-manual.md`
- `boilerplateBE/src/modules/Starter.Module.Communication/docs/developer-guide.md`
- `boilerplateBE/src/modules/Starter.Module.CommentsActivity/ROADMAP.md`
- `boilerplateBE/src/modules/Starter.Module.CommentsActivity/DEVELOPER_GUIDE.md`
- `boilerplateBE/docs/feature-flags.md`
- `boilerplateBE/docs/observability-setup.md`
- `boilerplateBE/docs/webhook-testing-guide.md`

### Content of new Workflow docs

**`docs/user-manual/modules/workflow.md` — end-user facing.** Sections:

1. Concepts — what a workflow is, what a request is, what the inbox is
2. Submitting a request — using "New Request" dialog, picking a definition, filling a form
3. Approving or rejecting — opening a task, actions, form fields, comments
4. Returning for revision — what happens, how the originator resubmits
5. Delegation — setting up coverage while on leave, how delegated tasks appear
6. Viewing history and status — instance detail page walkthrough
7. Reading the dashboard widget — pending tasks count, shortcuts

**`docs/developer-guide/modules/workflow.md` — developer facing.** Sections:

1. Architecture — state machine, engine, tasks vs. steps vs. instances, events
2. Registering an entity as workflowable — `AddWorkflowableEntity` walkthrough
3. Authoring a workflow definition — JSON schema, states, transitions, conditions, hooks
4. Dynamic forms — `formFields` schema, validation rules, accessing submitted data
5. Assignee strategies — built-in strategies, registering custom `IAssigneeResolverProvider`
6. Conditions — simple + compound (AND/OR/NOT), reference syntax for `ContextJson`
7. Parallel approvals — `QuorumConfig` AllOf/AnyOf semantics, task-group model
8. SLA and escalation — `Sla` config, reminder vs. escalate semantics
9. Hooks — types, execution order, `HookExecutor` contract
10. Known issues:
    - **#6 `WorkflowEngine.cs` is 1200+ lines.** The engine mixes sequential transition, parallel-approval coordination, SLA task creation, and auto-transition evaluation. Extract candidates: `ParallelApprovalCoordinator` (quorum evaluation + task-group advancement), `AutoTransitionEvaluator` (condition-driven state progression), `HumanTaskFactory` (task creation with assignee resolution, SLA due date, delegation lookup). Deferred because the current code is test-covered and working; extraction is pure refactor with no new capability. Pick this up when adding Phase 2b integration hooks or when the file next needs substantive modification.

**`docs/user-manual/modules/comments-activity.md` — end-user facing.** Sections:

1. What are comments vs. activity
2. Adding a comment — Markdown support, @mentions
3. @mention notifications — when they fire, how preferences control them
4. Reading the activity timeline on any entity

### Updates to `roadmaps/workflow.md`

Append a new entry in the existing Phase 2 Deferred Items format:

```markdown
### WorkflowEngine.cs extraction

**What:** Split `Infrastructure/Services/WorkflowEngine.cs` (~1200 lines) into focused
collaborators. Candidates: `ParallelApprovalCoordinator`, `AutoTransitionEvaluator`,
`HumanTaskFactory`.

**Why deferred:** Pure refactor — no capability added, no bug fixed. Existing tests cover
the behavior well. Doing it inline would balloon the cleanup PR.

**Pick this up when:** Phase 2b integration work needs to modify the engine, OR when the
file next grows by more than ~200 lines.

**Starting points:**
- Identify coordinator boundaries by grouping private methods in `WorkflowEngine.cs` —
  parallel approval, auto-transition, and task creation clusters are already visually
  distinct.
- Introduce one collaborator at a time behind a package-internal interface; existing
  tests should stay green through each extraction.
- No public-interface change on `IWorkflowService` / the controller.
```

### Updates to top-level files

- **`README.md`** — add a "Documentation" section linking to `docs/user-manual/README.md` and `docs/developer-guide/README.md`.
- **`CLAUDE.md`** — grep for any reference to old doc paths (`workstation-setup.md`, module `ROADMAP.md`, `DEVELOPER_GUIDE.md`, `docs/architecture/`, etc.) and update. Do NOT rewrite `CLAUDE.md` content, only fix dead links.

---

## Testing Strategy

### Code fixes

| Fix | Test |
|---|---|
| #3 | New unit test: event raised on escalation with both assignees; not raised when fallback resolves to none |
| #5 | New Vitest test: three cases (linear, branching rejection, loop-back) |
| #4 | New unit test: 25 tasks, page 2 of size 10 returns rows 11-20 with `Total=25` |
| #7 | Existing tests green after literal replacement |
| #1 | Unit test on serialization; live test in `tr-TR` locale |

### End-to-end pass before PR

Spin up the existing test app (ports 5300/3300 if free — falls back to 5200/3200) per the "Post-Feature Testing Workflow" in `CLAUDE.md`. Cover:

1. Dashboard widget renders pending tasks count correctly.
2. Submit a workflow request via "New Request" dialog.
3. Approve one step, reject one step, return-for-revision one step, resubmit.
4. Open delegation dialog, set a date range, submit — verify backend receives ISO dates.
5. Visit a rejected instance's detail page — confirm timeline shows rejected state as "current" and un-visited later states as "future" (not "completed").
6. With 25+ seeded pending tasks, navigate through pages — verify pagination fetches a fresh page on each click (network tab check).

Only block the PR if a regression surfaces. Known-prior issues (e.g., SLA overdue badges untested live — that's issue #2, deliberately deferred) don't block.

### Docs

Manual review of the new/moved files:
- Every internal link resolves
- Every stub redirects to a file that actually exists
- `README.md` and `CLAUDE.md` have no dead links (grep for the moved filenames)
- `docs/README.md`, `docs/user-manual/README.md`, `docs/developer-guide/README.md` render as usable tables of contents

---

## Commit Plan

Each commit self-contained, builds + tests green, reviewable in isolation.

### Docs commits (do these first — pure moves, low risk)

1. `refactor(docs): move workstation-setup + theming + architecture into developer-guide/`
2. `refactor(docs): consolidate module roadmaps into docs/roadmaps/`
3. `refactor(docs): move BE-level module docs (feature-flags, observability, webhooks)`
4. `refactor(docs): move Communication module docs to central structure`
5. `refactor(docs): move CommentsActivity module docs to central structure`
6. `refactor(docs): add redirect stubs in module folders`
7. `refactor(docs): move product docs (market-assessment, future-roadmap)`
8. `docs(workflow): add user manual`
9. `docs(workflow): add developer guide with #6 known issue section`
10. `docs(comments-activity): add user manual`
11. `docs(roadmap): add WorkflowEngine extraction entry to workflow roadmap`
12. `docs: add top-level README index files for user-manual + developer-guide`
13. `docs: update root README and CLAUDE.md with new doc links`

### Code-fix commits (do these second)

14. `fix(workflow): add WorkflowStateTypes constants for state type magic strings`
15. `fix(workflow): DB-level pagination for GetPendingTasks`
16. `fix(workflow): step timeline uses history records instead of array index`
17. `fix(workflow): raise WorkflowTaskEscalatedEvent on SLA escalation`
18. `fix(workflow): ISO 8601 date serialization in delegation dialog`

### PR commit

19. PR description authored manually when opening — summarizes all fixes, links to this spec and the prior Phase 2a spec.

---

## PR + Merge Strategy

1. Open PR from `feature/workflow-approvals` → `main`.
2. PR description lists every commit and links to both Phase 2a and this spec.
3. Self-review the diff before requesting review (the PR will be large — 45 prior commits + ~18 new).
4. After merge, delete `feature/workflow-approvals`.
5. Next session: `git checkout main && git pull && git checkout -b feature/workflow-phase2b` and brainstorm Phase 2b (Integration & Scale).

---

## Out of Scope (deferred explicitly)

- **#6 `WorkflowEngine.cs` extraction** — own PR, see roadmap entry.
- **#2 SLA overdue badges live-tested** — QA task, not a code fix.
- Screenshots / GIFs in user manual — possible follow-up PR.
- Auto-generated TOCs — possible follow-up.
- Docs for modules that don't yet have any (Billing, Webhooks as features, Feature Flags already moved but content unchanged) — possible follow-up.
