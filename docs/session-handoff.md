# Session Handoff — Workflow Module + Email-on-Mention + Module Hardening

**Last updated:** 2026-04-20
**Branch:** `feature/workflow-approvals` (43+ commits, NOT merged — no PR yet)
**Worktree:** `.claude/worktrees/sharp-colden`

---

## What Was Built

### Merged PRs
- **PR #3** — Module hardening: registered Comments & Communication in `modules.json`, fixed `rename.ps1` orphan test cleanup, added Communication tests (22), ROADMAP.md
- **PR #4** — Email-on-mention: `EmailMentionedUsersOnCommentCreatedHandler`, `INotificationPreferenceReader`, `WellKnownNotificationTypes`, frontend preference toggle with Communication awareness

### On Branch (Not Merged)

**Workflow Phase 1:** Core state-machine engine, CQRS (5 commands, 7 queries), WorkflowController, frontend (Task Inbox, Workflow History, Instance Detail, Definitions, Dashboard widget, Approval Dialog), 3 seed templates, Comments & Activity integration, production hardening (concurrency, idempotency, `AddWorkflowableEntity`)

**Workflow Phase 1 Enhancements:** `TransitionAsync` (resubmit from Draft), Return-to-Draft hold, Instances + Detail pages, user scoping (server-side enforcement), entity display names, participant access control, New Request dialog (users can start workflows from UI)

**Workflow Phase 2a (Engine Power):** Dynamic forms (`FormDataValidator` + 6 field types), compound conditions (recursive AND/OR), parallel approvals (AllOf + AnyOf), SLA tracking (`SlaEscalationJob` background service), delegation (`DelegationRule` + assignee swap + autocomplete dialog), 5 seed templates (including expense-approval with forms+SLA, board-resolution with parallel)

---

## What's Next

### Phase 2b: Integration & Scale (spec needed)
- External webhook triggers inbound
- Transactional outbox on `WorkflowDbContext`
- Performance optimization (denormalized inbox, DB-level pagination)
- Bulk operations (batch approve/reject)
- Entity-level comment ACL (`IEntityAccessChecker`)

### Phase 2c: AI & Intelligence (spec needed)
- AI agent as workflow participant
- `IWorkflowAiService` for function calling
- AI-powered routing + approval suggestions
- Workflow analytics + bottleneck reporting

### Phase 2d: Visual Designer (spec needed)
- Drag-and-drop state/transition builder
- Template creation from scratch
- Simulation mode

---

## Known Issues

1. Delegation dialog date serialization may fail in some locale formats
2. SLA overdue badges untested live (need 4h+ old tasks)
3. `WorkflowTaskEscalatedEvent` defined but never raised in SLA job
4. In-memory pagination on `GetPendingTasksAsync` (should be DB-level)
5. `WorkflowStepTimeline` uses array index for status (breaks with non-linear workflows)
6. `WorkflowEngine.cs` is 1200+ lines (should extract parallel/auto-transition logic)
7. Magic strings for state types (should be constants)

---

## Test Results
- **144 unit tests** — all pass
- **BE + FE builds** — clean
- **Isolation test** — builds without Workflow module (NullWorkflowService)
- **Live tested:** dynamic forms, delegation (search + create + delegate acts on task), definition detail (forms + SLA config visible), New Request dialog, full submit→return→resubmit→approve cycle, user scoping, comments/activity

---

## Key Files

| What | Path |
|---|---|
| Workflow module | `boilerplateBE/src/modules/Starter.Module.Workflow/` |
| Workflow engine | `boilerplateBE/src/modules/Starter.Module.Workflow/Infrastructure/Services/WorkflowEngine.cs` |
| Workflow tests | `boilerplateBE/tests/Starter.Api.Tests/Workflow/` |
| Workflow frontend | `boilerplateFE/src/features/workflow/` |
| Phase 2a spec | `docs/superpowers/specs/2026-04-20-workflow-phase2a-engine-power-design.md` |
| ROADMAP | `boilerplateBE/src/modules/Starter.Module.Workflow/ROADMAP.md` |
| Module catalog | `docs/superpowers/specs/2026-04-09-composable-module-catalog-design.md` |
| modules.json | `scripts/modules.json` |
