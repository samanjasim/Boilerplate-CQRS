# Workflow & Approvals — Roadmap

Deliberately deferred improvements for module maintainers. Each entry names the trigger that flips it from "defer" to "do now" and points at the starting files so the next developer does not have to rediscover context.

This file is maintainer-facing.

---

### Step data collection (dynamic forms)

**What:** Allow workflow state definitions to declare a form schema so the assignee submits structured data (e.g. "reason for rejection", "revised budget amount") rather than a free-text comment. The submitted values would be stored in `WorkflowStep.MetadataJson` and made available to downstream condition evaluators.

**Why deferred:** The current comment field covers the P0 use case (reviewer notes). Dynamic form schemas require a JSON-schema render pass on the frontend, a validation layer in `ExecuteTaskCommandHandler`, and a decision on whether form fields are part of the condition expression language.

**Pick this up when:** A domain module (e.g. Expenses, Purchase Orders) needs structured data at an approval step to drive a condition or populate a downstream record.

**Starting points:**
- Add `FormSchema` property to `WorkflowStateConfig` in [`Starter.Abstractions/Capabilities/WorkflowConfigRecords.cs`](../../Starter.Abstractions/Capabilities/WorkflowConfigRecords.cs).
- Validate submitted form data against the schema in [`Application/Commands/ExecuteTask/ExecuteTaskCommandHandler.cs`](./Application/Commands/ExecuteTask/ExecuteTaskCommandHandler.cs).
- Merge form data into `WorkflowInstance.ContextJson` so `IConditionEvaluator` can reference it.

---

### Visual workflow designer

**What:** A drag-and-drop state-machine builder in the frontend that produces the `statesJson` / `transitionsJson` payload consumed by `WorkflowDefinition.Create`. Today definitions are created via raw JSON in the admin panel.

**Why deferred:** The JSON editor is usable by developers bootstrapping workflows. A visual designer is a significant frontend investment (React Flow or similar) with no pressing tenant demand.

**Pick this up when:** Non-technical tenant admins need to create or modify workflow definitions without developer intervention, OR workflow count per tenant exceeds ~5.

**Starting points:**
- Frontend: create `WorkflowDesignerPage` under `boilerplateFE/src/features/workflow/pages/` using a graph library (React Flow recommended).
- The designer should serialize to the same `WorkflowStateConfig[]` + `WorkflowTransitionConfig[]` format already accepted by `POST /api/v1/workflows/definitions`.
- Wire a preview/simulation mode that walks through states without persisting.

---

### SLA tracking and auto-escalation

**What:** Each `WorkflowStateConfig` can declare a `DueDuration` (e.g. `PT8H`). When an `ApprovalTask` breaches its due date, a background job escalates — reassigning to a fallback assignee or sending a reminder notification.

**Why deferred:** `ApprovalTask.DueDate` is already persisted and surfaced in `PendingTaskSummary`. The escalation logic (job, strategy config, notification hook) is non-trivial and no tenant is time-boxing approvals yet.

**Pick this up when:** A tenant's SLA policy requires escalation within a defined window, OR HR/Leave module adoption brings headcount pressure on approvers.

**Starting points:**
- Add `DueDuration` to `WorkflowStateConfig` in [`Starter.Abstractions/Capabilities/WorkflowConfigRecords.cs`](../../Starter.Abstractions/Capabilities/WorkflowConfigRecords.cs).
- Populate `ApprovalTask.DueDate` from it in [`Infrastructure/Services/WorkflowEngine.cs`](./Infrastructure/Services/WorkflowEngine.cs) `CreateApprovalTaskAsync`.
- Add `SlaEscalationJob : BackgroundService` that queries overdue tasks and applies the configured escalation strategy.
- Define `EscalationStrategy` config (reassign to manager, notify, auto-approve/reject) on `WorkflowStateConfig`.

---

### Delegation

**What:** Allow an assignee to delegate their pending tasks to another user for a date range (e.g. during leave). The delegatee receives tasks as if they were the original assignee.

**Why deferred:** The assignee model is single-user today. Delegation requires a `DelegationRule` entity, a lookup pass in `AssigneeResolverService`, and a UI for users to manage their delegations.

**Pick this up when:** Leave/Attendance module lands and managers need cover during absences, or HR raises it as a blocker for adoption.

**Starting points:**
- Add `DelegationRule` entity to `WorkflowDbContext` (from user, to user, start/end date, scope).
- Extend `AssigneeResolverService.ResolveAsync` to check active delegation rules before returning the resolved assignee.
- Expose delegation management endpoints in `WorkflowsController`.

---

### Parallel approvals

**What:** Allow a step to require approval from multiple assignees simultaneously (all-of or any-of quorum). Today each step has a single assignee and the state machine advances on the first action.

**Why deferred:** Parallel assignment is the most-requested enterprise feature after delegation, but it requires splitting `ApprovalTask` into a task-group model, a quorum evaluator, and UI changes to show multiple pending actors per step.

**Pick this up when:** A procurement or policy module requires multi-party sign-off (e.g. two-of-three department heads).

**Starting points:**
- Add `QuorumConfig` to `WorkflowStateConfig` (type: AllOf | AnyOf | Threshold, threshold count).
- Extend `WorkflowEngine.CreateApprovalTaskAsync` to create N tasks when quorum is configured.
- Add `ApprovalTaskGroup` entity or a `GroupId` column on `ApprovalTask` to track quorum state.
- Advance the instance only when the quorum condition is satisfied inside `ExecuteTaskAsync`.

---

### Compound conditional expressions

**What:** Today `ConditionEvaluator` evaluates simple single-field comparisons against `WorkflowInstance.ContextJson`. Support compound expressions with `AND`/`OR`/`NOT` operators and nested groups.

**Why deferred:** The simple evaluator handles all documented use cases (field equals value, field greater than threshold). Compound expressions require a proper expression tree parser and increase the testing surface significantly.

**Pick this up when:** A workflow template requires multi-condition branching (e.g. "amount > 5000 AND department = Engineering").

**Starting points:**
- Extend `ConditionConfig` in [`Starter.Abstractions/Capabilities/WorkflowConfigRecords.cs`](../../Starter.Abstractions/Capabilities/WorkflowConfigRecords.cs) to support `Operator: And|Or|Not` with a `Conditions[]` array.
- Update [`Infrastructure/Services/ConditionEvaluator.cs`](./Infrastructure/Services/ConditionEvaluator.cs) to recurse over compound expressions.
- Add unit tests in [`tests/Starter.Api.Tests/Workflow/`](../../../tests/Starter.Api.Tests/Workflow/) covering compound AND/OR/NOT evaluation.

---

### AI integration

**What:** Allow a `SystemAction` step to call an AI model (e.g. "auto-classify request risk level" or "summarize supporting documents") and store the result in `WorkflowInstance.ContextJson` for downstream condition evaluation.

**Why deferred:** No tenant has requested AI-gated workflows, and plugging in an LLM at a step boundary requires a credentials model, latency budget, and fallback strategy that would dwarf the current hook system.

**Pick this up when:** A tenant wants automated risk scoring or document summarisation as part of an approval flow, AND the Claude API / Anthropic SDK integration is already established in the codebase.

**Starting points:**
- Define `HookType.AiAction` in the hook configuration model.
- Implement `AiActionHookHandler` in [`Infrastructure/Services/HookExecutor.cs`](./Infrastructure/Services/HookExecutor.cs) that calls an `IAiActionProvider` capability (Null Object fallback = no-op).
- Register `NullAiActionProvider` in [`Starter.Infrastructure/Capabilities/NullObjects`](../../Starter.Infrastructure/Capabilities/NullObjects).

---

### Workflow analytics

**What:** Aggregate metrics per definition — average cycle time, bottleneck states (steps with the longest median dwell time), approval rate per step, and volume over time. Expose via a read-model query and a dashboard card.

**Why deferred:** `WorkflowStep` already captures timestamps; the raw data is there. But building a fast aggregate query layer (or a pre-computed projection) requires a design decision on whether analytics live in the module's own read-model or in a future analytics module.

**Pick this up when:** Tenant admins start asking "why does our purchase approval take 3 days?" — i.e., when the workflow catalog is large enough that visibility creates value.

**Starting points:**
- Add `GetWorkflowAnalyticsQuery` under [`Application/Queries/`](./Application/Queries/) that groups `WorkflowStep` records by definition + state and computes dwell-time percentiles.
- Expose via `GET /api/v1/workflows/definitions/{id}/analytics`.
- Frontend: add an "Analytics" tab to the workflow definition detail page.
