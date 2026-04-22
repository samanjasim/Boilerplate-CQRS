# Workflow Phase 3+ Roadmap — Design Spec

**Date:** 2026-04-22
**Status:** Approved scope; Phase 3 is the first implementation slice.
**Predecessors:** Phase 1 (foundation), Phase 2a (engine power — SLA + delegation + parallel approvals), Phase 2b (operational hardening — outbox + denormalized inbox).

## Goal

Convert all 11 deferred Phase 2+ workflow items from [`docs/roadmaps/workflow.md`](../../roadmaps/workflow.md) into a sequenced multi-phase roadmap. Each phase is one shipping unit (one branch → one PR → one merge), matching the Phase 2a/2b cadence. Phase 6 (AI-powered workflows) is documented but explicitly deferred until the AI module merges from `feature/ai-integration`.

## Cadence model

- **Phase = shipping unit.** One feature branch off `main`, one PR, one merge.
- **Brainstorm → plan → implement → merge** before the next phase starts.
- **Bundled phase (3, 5):** multiple features in one PR, sequenced as clean commit sets.
- **Split phase (4):** a phase whose scope is too large for one PR is split into 4a/4b/4c, each its own PR with its own brainstorm + plan.
- **Cross-phase rule:** Phase 3.1 (engine extraction) must merge before any future feature that touches the engine — Phase 4a (dynamic forms) and Phase 4c (visual designer) both depend on the cleaner engine boundaries.

---

## Phase 3 — Foundation & Quick Wins

**Bundled into one PR.** Refactor first so 3.2 and 3.3 build on a clean engine.

### 3.1 — WorkflowEngine.cs extraction

**What:** Split [`WorkflowEngine.cs`](../../../boilerplateBE/src/modules/Starter.Module.Workflow/Infrastructure/Services/WorkflowEngine.cs) (~1425 lines, grew >200 since Phase 2a/2b) into focused collaborators:
- `ParallelApprovalCoordinator` — quorum task creation, completion evaluation, and group-state advancement.
- `AutoTransitionEvaluator` — condition matching, default transitions, and Initial-state auto-advance.
- `HumanTaskFactory` — `ApprovalTask` creation including denormalization (Phase 2b columns), assignee resolution wiring, and SLA snapshot capture.

**Why now:** File trigger condition met (>200-line growth since extraction was first deferred), AND subsequent Phase 3.2 / 4.1 / 4.3 features will modify the engine. Refactoring now keeps later diffs reviewable.

**Behavior change:** None. All existing 155 tests must remain green; this is a pure restructure.

**Boundary rules:**
- `WorkflowEngine` retains its public `IWorkflowService` surface — no controller or call-site changes outside the module.
- Collaborators are package-internal classes registered as scoped services in `WorkflowModule.ConfigureServices`.
- One collaborator extracted per commit; tests run between extractions.

### 3.2 — Compound conditional expressions

**What:** Extend `ConditionEvaluator` to support `AND`, `OR`, `NOT` operators and nested condition groups, in addition to today's single-field comparisons.

**Schema change:** [`WorkflowConfigRecords.cs`](../../../boilerplateBE/src/Starter.Abstractions/Capabilities/WorkflowConfigRecords.cs) `ConditionConfig` gains an optional `Operator` field (`And` | `Or` | `Not`) and a `Conditions` array. When `Operator` is null, behavior is the existing single-comparison evaluation.

**Backward compatibility:** Existing `statesJson` / `transitionsJson` payloads continue to parse — the new fields are additive and optional.

**Testing:** New unit tests in `tests/Starter.Api.Tests/Workflow/` covering AND/OR/NOT, nested groups, short-circuit evaluation, and JSON round-trips.

### 3.3 — Bulk operations

**What:** Allow approvers to select multiple pending tasks and execute the same action (Approve / Reject / Return for revision) on all of them in one round-trip.

**Backend:**
- `BatchExecuteTasksCommand(IReadOnlyList<Guid> TaskIds, string Action, string? Comment)` returning `BatchExecuteResult(Succeeded, Failed, Skipped)` with per-task outcome detail.
- Handler loops with per-task try/catch — one task's failure does not abort the batch.
- Each task validated for tenant scope (handler is multi-tenant-safe via existing query filters).

**Frontend:**
- Inbox table gets a checkbox column and a "select all on page" header.
- A floating batch-action bar appears when 1+ tasks are selected (Approve / Reject / Return / clear selection).
- Result summary toast shows `{succeeded} succeeded, {failed} failed, {skipped} skipped` with a "View details" expansion.

**Testing:** Handler tests cover happy path, mixed-success, all-fail, cross-tenant rejection. FE tests cover selection state, batch action dispatch, and summary rendering.

### Phase 3 sequencing

Within the bundled PR:
1. Extract `HumanTaskFactory` first (smallest, isolates denormalization).
2. Extract `AutoTransitionEvaluator` (uses condition evaluator → easier to test in isolation before 3.2 changes it).
3. Extract `ParallelApprovalCoordinator` (depends on `HumanTaskFactory`).
4. Land 3.2 — modify `ConditionEvaluator` (clean target after extraction).
5. Land 3.3 — bulk operations (BE first, then FE).

---

## Phase 4 — Authoring & Analytics

**Split into 4a / 4b / 4c.** Each is its own brainstorm → spec → plan → PR → merge cycle. Splitting is required because 4c alone (visual designer) is React-Flow-sized and would bury 4a/4b in review.

### 4a — Step data collection / dynamic forms

**What:** Workflow state definitions can declare a `FormSchema` describing structured fields the assignee submits at approval time (e.g. `revisedBudget: number`, `rejectionReason: string`). Submitted values merge into `WorkflowInstance.ContextJson` and become available to `IConditionEvaluator`.

**Why first in Phase 4:** Highest user value — enables structured data-driven branching, which is the single most-requested workflow capability after delegation.

**Boundaries:**
- `FormSchema` lives on `WorkflowStateConfig` in [`Starter.Abstractions/Capabilities/`](../../../boilerplateBE/src/Starter.Abstractions/Capabilities).
- Validation in `ExecuteTaskCommandHandler` — invalid submissions return `Result<Failure>` with field-level errors.
- Frontend: a generic `<DynamicForm schema={...}>` renderer in `src/features/workflow/components/`. Reuse existing shadcn/ui `Input`, `Select`, `Checkbox`, `Textarea`.

**Dependency on 3.1:** Form data ingestion happens in `HumanTaskFactory` → `ApprovalTask.FormFieldsJson` (already added in Phase 2b denormalization), so the cleaner factory boundary makes this safer to layer on.

### 4b — Workflow analytics

**What:** Aggregate metrics per definition: average cycle time, bottleneck states (median dwell time per step), approval rate per step, instance count over time. Surfaced via `GET /api/v1/workflows/definitions/{id}/analytics` and an "Analytics" tab on the definition detail page.

**Boundaries:**
- `GetWorkflowAnalyticsQuery` under `Application/Queries/`. Aggregates `WorkflowStep` records using EF Core grouping with raw-SQL fallbacks for percentile calculations (Postgres `percentile_cont`).
- Read-model lives in the Workflow module — no separate analytics module yet.
- Frontend: chart library = Recharts (already in `package.json` for billing usage charts).

**Multi-tenancy:** Existing global query filters apply to `WorkflowStep` — tenant admins see only their tenant's data; SuperAdmin sees all.

### 4c — Visual workflow designer

**What:** Drag-and-drop state-machine builder using React Flow that produces the same `WorkflowStateConfig[]` + `WorkflowTransitionConfig[]` JSON consumed by `WorkflowDefinition.Create`. Replaces today's raw-JSON admin editor.

**Strict YAGNI scope:**
- Render and edit the existing JSON shape — **no new state-machine constructs.**
- No simulation engine in v1 — preview is a read-only walkthrough using actual instance creation against a draft definition.
- No collaborative editing.

**Frontend-only feature:** No backend changes. Validation reuses the existing `POST /api/v1/workflows/definitions` endpoint.

**Risk note:** Highest scope-creep risk in the entire roadmap. The spec for 4c must explicitly enumerate non-goals at the start.

---

## Phase 5 — Integration & Access Control

**Bundled into one PR.** Both items are small, both touch shared infrastructure (controllers + cross-module).

### 5.1 — External webhook triggers (inbound)

**What:** `POST /api/v1/workflow/webhook/{eventName}` lets external systems advance workflows. The engine matches the event name against active instances' current-state transitions and auto-executes matching transitions. Authenticated via API key (`X-Api-Key` header) using the existing API key infrastructure.

**Schema change:** `WorkflowTransitionConfig` gains an optional `ExternalTrigger: { EventName: string, ApiKeyScope?: string }`.

**Authorization:** API key must have `Workflow.ExternalTrigger` permission AND match the optional `ApiKeyScope` if set on the transition.

**Idempotency:** Transitions already idempotent post-Phase-1; this just exercises that property over HTTP.

### 5.2 — Entity-level comment access control

**What:** Add `IEntityAccessChecker : ICapability` in `Starter.Abstractions/Capabilities/`. The Comments module's `GetCommentsQueryHandler` and `AddCommentCommandHandler` call `IEntityAccessChecker.CanAccessAsync(entityType, entityId, userId, ct)` before proceeding. The Workflow module registers a checker that validates the user is a workflow participant (initiator or assignee).

**Cross-module change:** This is a Comments module API surface change (handlers gain a new dependency). Default registration in `Starter.Infrastructure` is `NullEntityAccessChecker` which always returns `true` — preserves current behavior for any module not registering a checker.

**Composition rule:** Multiple checkers AND together — all registered checkers must approve. Documented in the capability's XML doc.

**Why bundled with 5.1:** Both items are smaller than Phase 3 features individually and share a "boundary" theme (external systems / cross-module access). Bundling avoids a thin PR.

---

## Phase 6 — AI-powered workflows (deferred placeholder)

**Status:** Not planned. Documented here for future ordering only. Implementation is gated on the AI module merging from `feature/ai-integration` to `main`.

When AI module ships:

- **6.1** AI agent as workflow participant — `AiAssigneeProvider : IAssigneeResolverProvider` with `SupportedStrategies => ["AiAgent"]`. MassTransit `ProcessAiWorkflowTaskConsumer` picks up `ApprovalTaskAssignedEvent` for the AI actor.
- **6.2** `IWorkflowAiService` capability — `GetAvailableActionsAsync(entityType, entityId)`, `DescribeWorkflowAsync(definitionId)`. Registered as an AI tool via the AI module's `IAiToolRegistry`.
- **6.3** AI integration in SystemAction steps — `HookType.AiAction` calling `IAiActionProvider`. Result stored in `WorkflowInstance.ContextJson` for downstream condition evaluation. `NullAiActionProvider` (no-op) is the default.

**Re-brainstorm trigger:** When the AI module's tool registry and capability infrastructure land on main, re-brainstorm 6.1–6.3 as a new top-level phase.

---

## Cross-cutting concerns

### Testing & quality gates

- Full test suite green before merge of every phase.
- xUnit + FluentAssertions + Moq for unit tests; EF in-memory for engine tests.
- `AbstractionsPurityTests` must pass — no MassTransit / EF dependencies in `Starter.Abstractions`.
- Frontend: Vitest unit tests + Playwright for any FE-only feature (4a, 4c).

### Multi-tenancy

- All new entities use global query filters per the existing `ApplicationDbContext.OnModelCreating` pattern.
- `BatchExecuteTasksCommand` validates per-task tenant scope before executing — a single batch cannot span tenants for non-platform-admins.
- Webhook inbound requests scope writes to the API key's owning tenant.

### Migrations

- Per the user's standing rule (`feedback_no_migrations` in memory), no EF migrations are committed in the boilerplate. Schema changes are documented in the spec; rename'd test apps generate their own migrations.

### Documentation upkeep

- After each phase merges, update `docs/roadmaps/workflow.md` to move shipped items into the "Phase X Shipped" section (mirrors the cleanup applied for Phase 2a/2b on 2026-04-22).

---

## Phase summary table

| Phase | Features | Shipping | Depends on |
|---|---|---|---|
| 3 | 3.1 engine extraction + 3.2 compound conditions + 3.3 bulk ops | 1 PR (bundled) | Phase 2b |
| 4a | Step data collection / dynamic forms | 1 PR | Phase 3 (3.1) |
| 4b | Workflow analytics | 1 PR | Phase 3 |
| 4c | Visual workflow designer | 1 PR | Phase 3 (3.1) |
| 5 | 5.1 inbound webhooks + 5.2 entity-level comment ACL | 1 PR (bundled) | Phase 3 |
| 6 | 6.1 AI assignee + 6.2 IWorkflowAiService + 6.3 AI hook | Deferred | AI module on main |

## Out of scope

- AI-powered features (Phase 6) — explicitly deferred.
- Cross-tenant batch operations.
- New state-machine constructs in the visual designer (4c).
- Real-time collaborative editing in the visual designer.
- A separate analytics module — analytics live in the Workflow module's read-model for now.
