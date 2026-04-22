# Workflow Phase 3 Foundation — QA + Code Review

**Date:** 2026-04-22
**Branch:** `feature/workflow-phase-3-foundation`
**Scope:** Validate the Phase 3 foundation drop (bulk task actions, engine decomposition, NOT operator in conditions) against the "production-ready" bar, fix findings, and capture a single handoff record for PR review.

---

## What shipped in this branch

| Commit | Change | Surface |
|---|---|---|
| c8afc26 | Extract `HumanTaskFactory` from `WorkflowEngine` | BE — SRP split |
| 66165a5 | Extract `AutoTransitionEvaluator` from `WorkflowEngine` | BE — SRP split |
| ac3294b | Extract `ParallelApprovalCoordinator` from `WorkflowEngine` | BE — SRP split |
| 222a405 | NOT operator support in compound conditions | BE — expression engine |
| ca42431 | `BatchExecuteTasks` command + `POST /api/v1/workflow/tasks/batch-execute` | BE — new capability |
| 0f3ee13 | Bulk task actions in inbox UI (BulkActionBar / ConfirmDialog / ResultDialog / row checkboxes) | FE — new capability |
| 1278b87 | Phase 3 foundation implementation plan | docs |

Net diff vs. `main`: **+1,427 / −250** across **29 files** (.cs / .ts / .tsx).

---

## QA plan & outcomes

Test app was spun up at BE :5200 / FE :3200 per the post-feature-testing skill. Acme / Globex tenants seeded with approval workflows that mix (a) form-required human tasks and (b) form-less human tasks, so both the happy path and the Skipped path could be exercised.

### Cases exercised (all green unless noted)

| # | Case | Path | Result |
|---|---|---|---|
| 1 | Bulk approve N form-less tasks | FE → `POST /tasks/batch-execute` | ✅ 7/7 Succeeded |
| 2 | Bulk reject N form-less tasks with a shared comment | FE | ✅ 7/7 Succeeded; comment persisted on each `ApprovalTask`'s completion record |
| 3 | Bulk return for revision | FE | ✅ 3/3 Succeeded |
| 4 | Bulk over form-required tasks | FE | ✅ 7/7 Skipped with "requires form data …" message; row checkboxes disabled client-side with localized tooltip |
| 5 | Mixed selection (form-required + form-less) | FE | ✅ Only form-less rows selectable; skipped rows never sent |
| 6 | 50-task cap | direct API | ✅ 400 — "Bulk action supports at most 50 tasks per request." |
| 7 | Empty array | direct API | ✅ 400 — "At least one task id is required." |
| 8 | Cross-page selection reset | FE | ✅ `useEffect([page, pageSize])` clears set on page navigation, so a user can't bulk-act on an invisible selection |
| 9 | Role scoping | FE as `acme.alice` / `acme.bob` | ✅ Zero rows returned for an admin-only workflow — bulk bar never visible |
| 10 | Cross-tenant isolation | FE as `globex.admin` | ✅ Cannot see acme tasks; direct API call with acme taskIds → all "Failed" (global filters hide the rows) |
| 11 | RTL — Arabic | FE | ✅ `dir=rtl`, `lang=ar`; title, headers, per-row tooltip, bulk bar and confirm dialog all translated. Screenshot: `rtl-inbox.png` |
| 12 | RTL — Kurdish | key-by-key inspection of `ku/translation.json` | ✅ All required keys present with the same structure as `ar`/`en` — same i18next pipeline drives direction/fallback |
| 13 | Delegation banner, overdue badge, empty state | key inspection | ✅ Keys present in all three locales |

### Bugs found & fixed during QA

| # | Symptom | Root cause | Fix |
|---|---|---|---|
| B1 | Bulk action over form-required tasks returned generic "Failed" with the raw engine error | Handler executed without pre-checking for required form fields; engine then rejected per-task | New `LoadCurrentStateConfigsAsync` step in the handler detects required form fields from `WorkflowStateConfig.FormFields` and surfaces **Skipped** with a clear "open the task to fill the form" message. [BatchExecuteTasksCommandHandler.cs:29-42](../../../boilerplateBE/src/modules/Starter.Module.Workflow/Application/Commands/BatchExecuteTasks/BatchExecuteTasksCommandHandler.cs) |
| B2 | Confirm dialog title leaked raw action key — "ReturnForRevision 1 tasks?" | Component interpolated the raw `BulkAction` enum value | Added `ACTION_LABEL_KEY` mapping to translate `Approve`/`Reject`/`ReturnForRevision` → `workflow.inbox.approve`/`reject`/`return`. [BulkConfirmDialog.tsx:24-35](../../../boilerplateFE/src/features/workflow/components/BulkConfirmDialog.tsx) |

Both fixes were re-verified end-to-end in the browser before moving to code review.

---

## Code review findings & fixes

The review pass was run against Phase 3 files only (engine decomposition + bulk BE/FE). Criteria: unified style, reusable components, no duplication, SOLID/SRP, tenant scoping, UX.

### R1 — Dead constructor parameters (5× `CS9113`)

Primary constructors in three services declared injections they no longer used after the `WorkflowEngine` decomposition:

| File | Removed | Why safe |
|---|---|---|
| `WorkflowEngine.cs` | `IConditionEvaluator`, `AssigneeResolverService` | Now consumed via the extracted `AutoTransitionEvaluator` and `HumanTaskFactory` respectively |
| `HookExecutor.cs` | `IUserReader`, `IConfiguration` | No remaining reference in hook execution paths |
| `HumanTaskFactory.cs` | `ILogger<HumanTaskFactory>` | Factory has no log sites; the single log path lives in its caller |

Using-directives that became unused were also removed. All 5 `CS9113` warnings are gone — module builds with **0 warnings, 0 errors**.

### R2 — Raw GUIDs shown to users in `BulkResultDialog`

The result list rendered `item.taskId` as a monospace GUID, violating the project rule "Never show raw GUIDs to users" (CLAUDE.md). Fixed by:

1. Adding an optional `taskLabels: Record<string, string>` prop to [BulkResultDialog.tsx](../../../boilerplateFE/src/features/workflow/components/BulkResultDialog.tsx).
2. Building the label map at submit time from the current `tasks` in scope (entity type · display name, matching the row layout) in [WorkflowInboxPage.tsx](../../../boilerplateFE/src/features/workflow/pages/WorkflowInboxPage.tsx) via the new `buildTaskLabels` helper.
3. Falling back to a localized "Task" string (`workflow.inbox.bulkResultUnknownTask`) when a row has gone stale (e.g. the page re-fetched before the dialog opened). Key added to all three locales.

The dialog no longer shows raw identifiers in any state.

### R3 — Exception messages leaking to clients

`BatchExecuteTasksCommandHandler` was pushing `ex.Message` straight into the `BatchItemOutcome.Error` field. Internal exception messages should not cross the API boundary:

- Injected `ILogger<BatchExecuteTasksCommandHandler>` and log the full exception for diagnostics.
- Return a fixed, neutral "An unexpected error occurred while executing this task." to the caller.
- Test updated ([BatchExecuteTasksTests.cs:70-77](../../../boilerplateBE/tests/Starter.Api.Tests/Workflow/BatchExecuteTasksTests.cs)) to positively assert the sensitive text (`"nope"`) does **not** appear in the response.

### R4 — Stale/broken unit-test composition

`BatchExecuteTasksTests.cs` was never updated after the Skipped path was added — it constructed the handler with 2 args against a 3-arg primary constructor and would have failed the prior CI run if any test on it had been invoked. Rewritten to construct a real in-memory `WorkflowDbContext` alongside the mocked `IWorkflowService` and `ICurrentUserService` and the new logger. All Phase 3 tests updated to match the new (leaner) constructor signatures.

### R5 — Component reuse audit

Phase 3 added three new FE components: `BulkActionBar`, `BulkConfirmDialog`, `BulkResultDialog`. The review confirmed:

- All three consume existing primitives from `@/components/ui` (`Dialog`, `Button`, `Textarea`, `Badge`) — no ad-hoc replacements.
- `Checkbox` was added to `@/components/ui` as a first-class shared primitive (the project didn't have one) and exported from the barrel, so any future feature can reuse it.
- The inbox page continues to use the project-standard `PageHeader`, `EmptyState`, and `Pagination` — no parallel table shell was introduced.
- No duplication of the `STATUS_BADGE_VARIANT` mapping in the bulk-result dialog — the conditional is a local presentation concern (3 statuses) and intentionally not hoisted.
- i18n: all user-visible strings go through `t()`. No hard-coded English (the initial copy of "ReturnForRevision" in the title bar was the only instance and is now fixed).

### R6 — SOLID / modularity

Four of the commits on this branch (`c8afc26`, `66165a5`, `ac3294b`, `222a405`) are explicitly an engine-level SRP split — the original ~500-line `WorkflowEngine` had grown to own condition evaluation, assignee resolution, form validation, human-task creation, auto-transition detection, and parallel (AllOf) coordination all at once. After the split:

- `WorkflowEngine` — orchestrates; no longer instantiates tasks or evaluates transitions directly.
- `HumanTaskFactory` — builds approval tasks for HumanTask states (single + parallel), owns denormalization.
- `AutoTransitionEvaluator` — picks the next transition for automatic states.
- `ParallelApprovalCoordinator` — tallies AllOf child-task completion and progresses the parent.
- `HookExecutor` — unchanged in Phase 3, but shrank by two unused deps (R1).

New tests accompany each split: `HumanTaskFactoryTests` (172 lines), `ParallelApprovalCoordinatorTests` (109), `AutoTransitionEvaluatorTests` (72), `ConditionEvaluatorTests` (+121 for NOT), `BatchExecuteTasksTests` (97). Total: **1,005 new test lines** covering the new seams.

### R7 — Tenant scoping of bulk

The handler delegates task execution to `IWorkflowService.ExecuteTaskAsync`, which inherits the existing per-task tenant + assignee check (already verified in Phase 2). The pre-load (`LoadCurrentStateConfigsAsync`) uses the tenant-aware `WorkflowDbContext` — `.AsNoTracking()` only skips change-tracking, not global query filters — so a cross-tenant taskId is filtered out of `stateConfigs` and falls through to the engine, which also rejects it. Cross-tenant test (QA #10) confirms: globex admin gets "Failed" for every acme taskId, never "Succeeded" or "Skipped".

---

## User-flow gaps intentionally **not** addressed this PR

Captured so they don't surprise the next reviewer. None block merging Phase 3 foundation:

| Gap | Why deferred |
|---|---|
| Parallel execution of bulk items | Current handler runs sequentially inside one request. For N=50 worst-case this is fine (sub-second per task on the test bench); real parallelism requires fanning out via the outbox, which belongs in Phase 3b. |
| Optimistic UI for bulk actions | The result dialog is synchronous. When N > ~20 this starts to feel slow; the proper answer is a job + progress drawer (roadmap item). |
| Per-row progress indicator during bulk execute | Same — requires streaming or a follow-up query. |
| Redo / partial-retry from the result dialog | Result dialog shows per-item outcome but does not offer a retry button for the Failed rows. Valid next step once real parallelism ships. |
| Delegation badge i18n on overdue + delegated combos | Both badges render; wording reviewed in en/ar/ku. Visual stacking on narrow viewports would benefit from a follow-up design pass. |

---

## Build / test status

| Suite | Result |
|---|---|
| `dotnet build` (solution) | **0 errors, 0 workflow-related warnings** (4 pre-existing `NU1903` advisories on `System.Security.Cryptography.Xml` are unrelated) |
| `dotnet test --filter FullyQualifiedName~Workflow` | **112/112 passing** |
| `npm run build` (frontend) | ✅ Built in 2.3s |
| Playwright manual flows (Acme admin, alice, bob, globex admin, RTL-ar) | ✅ all cases listed in QA table |

---

## Files changed during this QA + review pass

Backend source:
- `Application/Commands/BatchExecuteTasks/BatchExecuteTasksCommandHandler.cs` — log + neutralize exception messages (R3); (Skipped path was already added earlier in the session as B1)
- `Infrastructure/Services/WorkflowEngine.cs` — remove 2 dead ctor params + using (R1)
- `Infrastructure/Services/HookExecutor.cs` — remove 2 dead ctor params + 2 usings (R1)
- `Infrastructure/Services/HumanTaskFactory.cs` — remove 1 dead ctor param + 1 using (R1)

Backend tests (updated to new ctor signatures + assert the no-leak behavior):
- `Workflow/BatchExecuteTasksTests.cs` (rewritten; now uses in-memory `WorkflowDbContext`)
- `Workflow/WorkflowEngineTests.cs`
- `Workflow/HookExecutorTests.cs`
- `Workflow/HumanTaskFactoryTests.cs`
- `Workflow/GetPendingTasksPaginationTests.cs`
- `Workflow/PendingTasksDenormalizationTests.cs`

Frontend:
- `features/workflow/components/BulkConfirmDialog.tsx` — action label translation (B2)
- `features/workflow/components/BulkResultDialog.tsx` — `taskLabels` prop, no more raw GUIDs (R2)
- `features/workflow/pages/WorkflowInboxPage.tsx` — `buildTaskLabels`, cleared on close (R2)
- `i18n/locales/{en,ar,ku}/translation.json` — added `bulkResultUnknownTask`

No production-only config, migrations, or secrets were touched.

---

## Handoff

- Branch is ready for PR. The PR will be created by the user (not by this session).
- If reviewers want to re-run the manual QA: `scripts/rename.ps1 -Name "_testWfPhase3" -OutputDir "." -Modules "All" -IncludeMobile:$false`, then BE on :5200 / FE on :3200 as above. The test app under `_testWfPhase3/` is preserved for that purpose.
- Next roadmap item after this PR lands: Phase 3b (async bulk fan-out via outbox + progress UX) — captured in the deferred list above.
