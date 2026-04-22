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

## Phase 2 Shipped

### Phase 2a — Engine power (merged)

- **SLA tracking and auto-escalation** — `DueDuration` on `WorkflowStateConfig`, `SlaEscalationJob` escalates overdue tasks via `EscalationStrategy` (Notify / Reassign / AutoApprove / AutoReject). Snapshots configuration at task creation.
- **Delegation** — `DelegationRule` entity with date-range scoping. `AssigneeResolverService` checks active rules before returning the resolved assignee. `originalAssigneeUserId` tracked on `ApprovalTask`. Frontend delegation management UI + delegated-to-me indicator.
- **Parallel approvals** — `QuorumConfig` on `WorkflowStateConfig` (AllOf | AnyOf | Threshold). Engine creates N tasks per step and advances only when the quorum condition is satisfied.

### Phase 2b — Operational hardening (merged 2026-04-21)

- **Transactional outbox on WorkflowDbContext** — `AddEntityFrameworkOutbox<WorkflowDbContext>` + `UseBusOutbox()`. `MassTransitMessagePublisher` switched from singleton `IBus` to scoped `IPublishEndpoint` so both the Application and Workflow DbContext outboxes engage for their respective events.
- **Denormalized inbox** — `DefinitionName`, `DefinitionDisplayName`, `EntityType`, `EntityDisplayName`, `StepName`, `AssigneeDisplayName`, `OriginalAssigneeDisplayName`, `FormFieldsJson` snapshotted onto `ApprovalTask` at creation. `GetPendingTasksAsync` fast-path skips the definition/instance JOIN and the per-row `IUserReader` lookup; legacy pre-migration rows fall back to the JOIN.

---

## Phase 3 Shipped (merged 2026-04-22)

- **WorkflowEngine extraction** — [`WorkflowEngine.cs`](../../boilerplateBE/src/modules/Starter.Module.Workflow/Infrastructure/Services/WorkflowEngine.cs) split into [`HumanTaskFactory`](../../boilerplateBE/src/modules/Starter.Module.Workflow/Infrastructure/Services/HumanTaskFactory.cs), [`AutoTransitionEvaluator`](../../boilerplateBE/src/modules/Starter.Module.Workflow/Infrastructure/Services/AutoTransitionEvaluator.cs), and [`ParallelApprovalCoordinator`](../../boilerplateBE/src/modules/Starter.Module.Workflow/Infrastructure/Services/ParallelApprovalCoordinator.cs). Each collaborator has its own tests (`HumanTaskFactoryTests`, `AutoTransitionEvaluatorTests`, `ParallelApprovalCoordinatorTests`). Unlocks Phase 4a/4c.
- **Compound conditional expressions** — `ConditionConfig` extended with `Operator: And|Or|Not` + nested `Conditions[]`. `ConditionEvaluator` recurses with short-circuit semantics. Covered in `ConditionEvaluatorTests` (AND/OR/NOT/empty-conditions/threshold combinations).
- **Bulk operations** — `BatchExecuteTasksCommand` + `POST /api/v1/workflow/tasks/batch-execute`. Per-task try/catch, neutralized error messages (no exception leak), 50-task cap, pre-flight "Skipped" for tasks with required form fields. Inbox UI: row checkboxes, bulk action bar, confirm dialog, result dialog with expandable per-task outcomes, retry-failed action, and stacked overdue/delegated badges. i18n in en/ar/ku.

---

## Phase 4+ Deferred Items

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

### Step data collection (dynamic forms)

**What:** Allow workflow state definitions to declare a form schema so the assignee submits structured data (e.g. "reason for rejection", "revised budget amount") rather than a free-text comment. The submitted values would be stored in `WorkflowStep.MetadataJson` and made available to downstream condition evaluators.

**Why deferred:** The current comment field covers the P0 use case (reviewer notes). Dynamic form schemas require a JSON-schema render pass on the frontend, a validation layer in `ExecuteTaskCommandHandler`, and a decision on whether form fields are part of the condition expression language.

**Pick this up when:** A domain module (e.g. Expenses, Purchase Orders) needs structured data at an approval step to drive a condition or populate a downstream record.

**Starting points:**
- Add `FormSchema` property to `WorkflowStateConfig` in [`Starter.Abstractions/Capabilities/WorkflowConfigRecords.cs`](../../boilerplateBE/src/Starter.Abstractions/Capabilities/WorkflowConfigRecords.cs).
- Validate submitted form data against the schema in [`Application/Commands/ExecuteTask/ExecuteTaskCommandHandler.cs`](../../boilerplateBE/src/modules/Starter.Module.Workflow/Application/Commands/ExecuteTask/ExecuteTaskCommandHandler.cs).
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

### AI integration

**What:** Allow a `SystemAction` step to call an AI model (e.g. "auto-classify request risk level" or "summarize supporting documents") and store the result in `WorkflowInstance.ContextJson` for downstream condition evaluation.

**Why deferred:** No tenant has requested AI-gated workflows, and plugging in an LLM at a step boundary requires a credentials model, latency budget, and fallback strategy that would dwarf the current hook system.

**Pick this up when:** A tenant wants automated risk scoring or document summarisation as part of an approval flow, AND the Claude API / Anthropic SDK integration is already established in the codebase.

**Starting points:**
- Define `HookType.AiAction` in the hook configuration model.
- Implement `AiActionHookHandler` in [`Infrastructure/Services/HookExecutor.cs`](../../boilerplateBE/src/modules/Starter.Module.Workflow/Infrastructure/Services/HookExecutor.cs) that calls an `IAiActionProvider` capability (Null Object fallback = no-op).
- Register `NullAiActionProvider` in [`Starter.Infrastructure/Capabilities/NullObjects`](../../boilerplateBE/src/Starter.Infrastructure/Capabilities/NullObjects).

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
- Add `GetWorkflowAnalyticsQuery` under [`Application/Queries/`](../../boilerplateBE/src/modules/Starter.Module.Workflow/Application/Queries/) that groups `WorkflowStep` records by definition + state and computes dwell-time percentiles.
- Expose via `GET /api/v1/workflows/definitions/{id}/analytics`.
- Frontend: add an "Analytics" tab to the workflow definition detail page.

---
