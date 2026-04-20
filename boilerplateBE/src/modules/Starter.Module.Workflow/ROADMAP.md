# Workflow & Approvals — Roadmap

Deliberately deferred improvements for module maintainers. Each entry names the trigger that flips it from "defer" to "do now" and points at the starting files so the next developer does not have to rediscover context.

This file is maintainer-facing.

---

## Phase 1 Enhancements Applied

The following improvements were made after the initial Phase 1 implementation. They affect Phase 2 scope:

- **Return-to-Draft holds for resubmit** — Initial states only auto-transition on `StartAsync`, not after `ReturnForRevision`. Phase 2's step data collection should allow the returned-for-revision step to pre-fill the form with the original submission data.
- **Instance list with user/status scoping** — `GetInstancesAsync` now accepts `startedByUserId` and `status` filters. Regular users are auto-scoped to their own workflows. Phase 2's analytics should leverage this scoping for per-user metrics.
- **Instance Detail page** — renders step timeline, comments slot, cancel action, and pending task actions. Phase 2's step data collection forms should render inline on this page, and SLA indicators (overdue badges) should attach to the step timeline.
- **Workflow slot rendered on Products detail page** — proves the entity integration pattern. Phase 2's visual designer should generate slot-rendering code automatically for new entity types.
- **`startedByDisplayName` in InstanceSummary** — enriches the API response. Phase 2's delegation feature should add `delegatedToDisplayName` similarly.
- **Concurrency protection** — `RowVersion` (xmin) on `WorkflowInstance` and `ApprovalTask`. `DbUpdateConcurrencyException` caught in ExecuteTask/Transition/Cancel. Prevents dual-approval data corruption.
- **Idempotent task execution** — retried ExecuteTask calls return success if the task is already completed with the same action. TransitionAsync is idempotent when the instance already passed the Initial state.
- **`AddWorkflowableEntity` helper** — one-liner registration for modules (`services.AddWorkflowableEntity("Product", ...)`). Products module registered as proof of concept. Registry enables auto-discovery for Phase 2's "Start Workflow" button on entity detail pages.
- **TransitionAsync** — manual trigger for transitions on Initial-type states. Only the initiator can call it. Used for resubmitting after Return for Revision.
- **Participant-based access control** — `GetWorkflowHistoryQueryHandler` checks user is initiator, assignee, or has `ViewAllTasks`. Non-participants get 404 (not 403, to avoid info leakage).
- **Comments & Activity wired to WorkflowInstance** — engine saves approval comments and activity entries against `entityType: "WorkflowInstance"` + `entityId: instanceId`. Detail page renders the unified timeline.

---

## Phase 2 Deferred Items

### AI agent as workflow participant

**What:** Allow an AI agent to be an assignee on a workflow step. When a step with `AssigneeStrategy: "AiAgent"` is entered, the AI module auto-processes it — reviewing documents, classifying risk, summarizing content — and either auto-advances the workflow or flags it for human review.

**Why deferred:** The AI module is still in development. The assignee resolution infrastructure (`IAssigneeResolverProvider`) supports this — the AI module would register a provider with strategy `"AiAgent"` that resolves to a system actor ID.

**Pick this up when:** The AI module is merged and a domain module needs automated processing steps (e.g., "AI reviews leave request justification before manager sees it").

**Starting points:**
- AI module registers `AiAssigneeProvider : IAssigneeResolverProvider` with `SupportedStrategies => ["AiAgent"]`
- `ResolveAsync` returns a system-level AI actor user ID
- The engine creates an `ApprovalTask` assigned to the AI actor
- A MassTransit consumer (`ProcessAiWorkflowTaskConsumer`) picks up `ApprovalTaskAssignedEvent` when assignee is the AI actor, processes the step, and calls `ExecuteTaskAsync`

---

### IWorkflowAiService — AI-friendly workflow API

**What:** A higher-level capability interface that the AI module's function-calling system can consume. Methods like `GetAvailableActionsAsync(entityType, entityId)` → returns what the AI can do next on this entity's workflow, `DescribeWorkflowAsync(definitionId)` → returns a natural-language description of the workflow steps for LLM context.

**Why deferred:** The AI module's tool registry (`IAiToolRegistry`) needs to be merged first. Once it is, workflow tools can be registered automatically.

**Pick this up when:** AI module is merged and AI-powered workflow actions are a product requirement.

**Starting points:**
- Define `IWorkflowAiService` in `Starter.Abstractions/Capabilities/` extending `ICapability`
- Implement in the Workflow module's engine
- Register as an AI tool via `IAiToolRegistry` in `WorkflowModule.ConfigureServices`

---

### External webhook triggers (inbound)

**What:** A `POST /workflow/webhook/{eventName}` endpoint that external systems (payment gateways, CI/CD, third-party APIs) can call to trigger workflow transitions. Authenticated via API key. The engine matches the event name to the current state's transitions and auto-advances.

**Why deferred:** Outbound webhooks (via `IWebhookPublisher`) are working. Inbound requires an event-to-transition mapping config, API key scoping per workflow definition, and payload validation.

**Pick this up when:** An integration requires external systems to advance workflows (e.g., Stripe payment confirmed → order workflow advances from "PendingPayment" to "Paid").

**Starting points:**
- Add `ExternalTriggerConfig` to `WorkflowTransitionConfig` (event name, expected payload schema)
- New controller endpoint `POST /workflow/webhook/{eventName}` with API key auth
- Engine matches event name to transitions on active instances and auto-executes

---

### Transactional outbox on WorkflowDbContext

**What:** Bind MassTransit's EF outbox to `WorkflowDbContext` so domain events and integration events publish atomically with state changes. Currently events publish via in-memory MediatR dispatch — a crash between SaveChanges and event publishing can lose events.

**Why deferred:** Same reasoning as the Comments and Communication modules' outbox deferral. No at-least-once consumer is demanding guaranteed delivery yet.

**Pick this up when:** A downstream consumer requires at-least-once delivery (e.g., billing module tracking workflow completions for invoicing, or compliance audit requiring every transition to be durably recorded externally).

**Starting points:**
- Mirror `AddEntityFrameworkOutbox<ApplicationDbContext>` from `Starter.Infrastructure/DependencyInjection.cs` against `WorkflowDbContext`
- Swap `IPublishEndpoint.Publish` in event handlers for `IBus.Publish` inside the same `SaveChangesAsync` transaction

---

### Bulk operations

**What:** Allow admins to select multiple pending tasks and approve/reject them in batch. Useful when a manager has 20+ pending leave requests or expense approvals.

**Why deferred:** Single-task approval covers the v1 use case. Bulk requires a `BatchExecuteTasksCommand`, UI changes (checkbox selection, batch action bar), and careful concurrency handling (what if some tasks fail and others succeed?).

**Pick this up when:** Admin feedback indicates inbox volume makes individual approval impractical (typically > 10 tasks/day for a single approver).

**Starting points:**
- Add `BatchExecuteTasksCommand(List<(Guid TaskId, string Action)> Tasks, string? Comment)` 
- Handler loops with per-task try/catch, returns a result summary (succeeded/failed/skipped counts)
- Frontend: add checkbox column to inbox table, batch action bar at top

---

### Performance optimization — denormalized inbox

**What:** `GetPendingTasksAsync` currently joins `ApprovalTask → WorkflowInstance → WorkflowDefinition` and resolves display names via `IUserReader`. At scale (1000+ tasks), this is slow. Denormalize `DefinitionDisplayName` and `EntityDisplayName` onto `ApprovalTask` at creation time, and add DB-level pagination.

**Why deferred:** Current query performance is acceptable for typical task volumes (< 100 per user). Denormalization adds write-path complexity.

**Pick this up when:** Inbox query latency exceeds 500ms (monitor via OpenTelemetry traces) or a tenant has > 500 pending tasks.

**Starting points:**
- Add `DefinitionDisplayName` and `EntityDisplayName` columns to `ApprovalTask`
- Populate in `CreateApprovalTaskAsync` from the instance
- Remove the join + IUserReader call from `GetPendingTasksAsync`
- Add server-side pagination parameters to `IWorkflowService.GetPendingTasksAsync`

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

### Entity-level comment access control

**What:** Add an `IEntityAccessChecker` capability that the Comments & Activity module calls before displaying comments or allowing new comments on an entity. The Workflow module would register a checker that validates the user is a workflow participant (initiator or assignee) before showing workflow-related comments. Currently, comments are visible to anyone in the tenant with `Comments.View` permission.

**Why deferred:** The Comments module currently has no per-entity access control — it uses tenant-wide query filters only. Adding entity-level permissions requires a cross-module architectural change: the Comments module must support pluggable access checkers, and each consuming module must register one. The current access model (tenant-scoped + UI gates access via workflow detail page) is acceptable for teams where transparency is valued.

**Pick this up when:** A tenant has compliance requirements that restrict who can see specific workflow discussions (e.g., HR disciplinary workflows, salary-related approvals), OR when the boilerplate targets regulated industries (healthcare, finance) where audit-trail visibility must be role-restricted.

**Starting points:**
- Define `IEntityAccessChecker : ICapability` in `Starter.Abstractions/Capabilities/` with method `Task<bool> CanAccessAsync(string entityType, Guid entityId, Guid userId, CancellationToken ct)`.
- Register `NullEntityAccessChecker` (returns true) in `Starter.Infrastructure`.
- The Comments module's `GetCommentsQueryHandler` and `AddCommentCommandHandler` call `IEntityAccessChecker.CanAccessAsync()` before proceeding.
- The Workflow module registers a checker that validates the user is a workflow participant.
- Consider: should the checker be AND-ed (all checkers must approve) or OR-ed (any checker can approve)?

---

### Workflow analytics

**What:** Aggregate metrics per definition — average cycle time, bottleneck states (steps with the longest median dwell time), approval rate per step, and volume over time. Expose via a read-model query and a dashboard card.

**Why deferred:** `WorkflowStep` already captures timestamps; the raw data is there. But building a fast aggregate query layer (or a pre-computed projection) requires a design decision on whether analytics live in the module's own read-model or in a future analytics module.

**Pick this up when:** Tenant admins start asking "why does our purchase approval take 3 days?" — i.e., when the workflow catalog is large enough that visibility creates value.

**Starting points:**
- Add `GetWorkflowAnalyticsQuery` under [`Application/Queries/`](./Application/Queries/) that groups `WorkflowStep` records by definition + state and computes dwell-time percentiles.
- Expose via `GET /api/v1/workflows/definitions/{id}/analytics`.
- Frontend: add an "Analytics" tab to the workflow definition detail page.
