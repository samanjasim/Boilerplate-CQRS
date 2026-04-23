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

## Phase 4a Shipped (merged YYYY-MM-DD)

**Dynamic forms — step data collection.** State definitions can declare `FormFields` whose submitted values merge into `WorkflowInstance.ContextJson` and become available to conditional transitions. Supported field types: `text`, `textarea`, `number`, `date`, `select`, `checkbox`.

Shipped components:
- `FormFieldDefinition` record on `WorkflowStateConfig` in `Starter.Abstractions.Capabilities`.
- `FormFieldsJson` denormalized on `ApprovalTask` for inbox rendering without definition joins.
- `IFormDataValidator` with type-specific validation (min/max, maxLength, required, select option membership, date parse).
- `WorkflowEngine.ExecuteTaskAsync` merges submitted values into `ContextJson`; returns `Result<Result<bool>>` surfacing `ValidationErrors` via the standard API envelope.
- FE: `DynamicFormRenderer` component rendering all 6 types; `ApprovalDialog` submits `formData` and renders inline field errors; `WorkflowStepTimeline` displays submitted data in instance history.
- Documentation: `docs/features/workflow-forms.md`.

See `docs/superpowers/specs/2026-04-22-workflow-phase4a-dynamic-forms-finish-design.md` for the finish-line design.

---

## Phase 4b Shipped (merged 2026-04-22)

**Analytics dashboard — per-definition operator metrics.** Six metric panels computed from existing `WorkflowInstances`, `WorkflowSteps`, and `ApprovalTasks` tables with no new database tables.

Shipped components:
- `GetWorkflowAnalyticsQuery` + `GetWorkflowAnalyticsQueryHandler` under `Application/Queries/` — computes headline counts, bottleneck states (median + p95 dwell time, ≥ 3 visits threshold), action rates, instance count series, stuck instances, and approver activity.
- `WorkflowAnalyticsDto` in `Starter.Abstractions` — typed result envelope for all six panels.
- `GET /api/v1/workflows/definitions/{id}/analytics?window={7d|30d|90d|all}` — returns 400 for missing/invalid window; 404 for template definitions.
- `Workflows.ViewAnalytics` permission added to `Starter.Shared/Constants/Permissions.cs` and mirrored in `boilerplateFE/src/constants/permissions.ts`.
- FE: `WorkflowAnalyticsTab` component with headline strip, bottleneck table, action-rate table, instance count chart, stuck-instances table, and approver-activity table. Low-data banner when fewer than 5 instances exist in the window.
- Documentation: `docs/features/workflow-analytics.md`.

### Analytics follow-ups (deferred)

- Snapshot/materialized view for analytics at scale (>100k instances)
- Explicit `WorkflowStepId` FK on `ApprovalTask` for exact response-time join
- Post-merge translations for ar/ku analytics keys

---

## Phase 4c Shipped (merged YYYY-MM-DD)

**Visual workflow designer (MVP).** Drag-and-drop state-machine builder at `/workflows/definitions/:id/designer`. Produces the same `WorkflowStateConfig[]` + `WorkflowTransitionConfig[]` JSON the backend already accepts. First-class UI for common fields (identity, actions, assignee strategy + basic params, SLA, trigger); inline JSON blocks for advanced fields (hooks, form fields, parallel/quorum, compound conditions, fallback assignee, custom params). Node positions persist via optional `UiPosition` on `WorkflowStateConfig`. Auto-layout via dagre on first open; explicit toolbar button for reflow. Templates open in read-only mode with "Clone to edit".

Shipped components:
- `UpdateDefinitionCommandValidator` — unified FluentValidation rules for slug / uniqueness / type membership / exactly-one-Initial / at-least-one-Terminal / assignee-required-for-HumanTask / SLA ordering / transition from-to-trigger rules.
- Optional `UiPosition` record on `WorkflowStateConfig` in `Starter.Abstractions`.
- FE: `WorkflowDefinitionDesignerPage` + `DesignerCanvas` (React Flow) + `StateNode` / `TransitionEdge` custom renderers + `SidePanel` router + `StateEditor` + `TransitionEditor` + `JsonBlockField` + `DesignerToolbar` + `useDesignerStore` (zustand) + `useAutoLayout` (dagre) + `designerSchema.ts` (zod mirror).
- i18n keys in en/ar/ku under `workflow.designer.*`.
- Documentation: `docs/features/workflow-designer.md`.

See `docs/superpowers/specs/2026-04-23-workflow-phase4c-visual-designer-design.md` for the full design.

### Designer follow-ups (deferred)

- Post-merge translations for ar/ku `workflow.designer.*` keys (shipped in English across all three locales; translation pass is a short follow-up).
- Designer — Option A (full-schema visual editor, removing JSON-block escape hatches) — see deferred item below.
- Simulation / dry-run, collaborative editing, version history + diff, AI-assisted authoring — see deferred items below.

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

### Forms (deferred from Phase 4a)

- **Multiselect field type** — `type: "multiselect"` with `options[]` and multi-value persistence.
- **File upload field type** — requires signed-URL plumbing into `ContextJson`, retention policy, quota enforcement, and virus-scan hook design before it can ship.
- **Array-of-object field type** — repeating fieldsets (e.g. "list of line items"), each rendered as a nested `FormFieldDefinition[]`.
- **Conditional field visibility** — show/hide a field based on another field's value (e.g. show `rejectionReason` only when `approved == false`).
- **Default values / server-computed placeholders** — e.g. pre-populate `reviewerName` with the current user's display name.
- **Multi-step forms within a single task** — today one form per task; step-by-step wizards are an authoring concern.
- **Admin authoring UX** — planned to ship with the Phase 4c visual workflow designer.

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

### Designer — Option A (full-schema visual editor)

**What:** Upgrade the Phase 4c designer to cover every `WorkflowStateConfig` / `WorkflowTransitionConfig` field with dedicated visual editors, removing all JSON-block escape hatches. Ships as a **sequence** of independent PRs, one per removed JSON block:
- Hooks editor (OnEnter / OnExit)
- Form fields editor (visual row-based builder)
- Parallel / quorum editor (mode + assignees builder)
- Compound condition editor (tree-style AND/OR/NOT builder; requires updating FE `ConditionConfig` type to match BE's compound shape)
- Assignee fallback sub-form
- Custom assignee parameters editor

**Why deferred:** the MVP covers the 80% case. Full visual parity is valuable but larger than a single PR, and some blocks (compound conditions, form fields) deserve their own UX iterations.

**Pick this up when:** first tenant complaint that JSON editing is a barrier, OR workflow authoring crosses ~20 custom definitions across all tenants.

**Starting points:**
- Each block lives in `boilerplateFE/src/features/workflow/components/designer/StateEditor.tsx` — find the `<JsonBlockField ... >` calls and replace one at a time with a dedicated form.

---

### Designer — simulation / dry-run

**What:** a "Run" mode in the designer that walks a mock instance through the graph without persistence, letting authors verify triggers and conditions fire as expected.

**Why deferred:** meaningful simulation needs stubs of assignee resolution, condition evaluation, and hook execution in parallel to the real engine. Ship authoring first; measure whether users actually need a simulator vs. starting real test instances against a draft.

**Pick this up when:** authors repeatedly ship broken definitions because they can't preview them.

---

### Designer — collaborative editing

**What:** live cursors + presence on the same definition so multiple authors can edit together.

**Why deferred:** needs a persistent collaboration channel (WebSocket or Yjs/CRDT) and server-side conflict resolution. Not justified until two or more authors regularly touch the same definition at once.

**Pick this up when:** two or more tenant users report conflicting edits, OR a tenant explicitly asks for team-editing.

---

### Definition version history + diff

**What:** store a history of saved definitions and let users compare two versions, restore an old one, and annotate changes.

**Why deferred:** needs schema (history table, audit trail), UX (diff renderer), and storage-growth considerations. Today, `AuditLog` captures `WorkflowDefinition.Updated` events; users can reconstruct recent history via that if needed.

**Pick this up when:** tenant compliance or change-management requirements force it, OR authors lose work due to unintended saves.

---

### Designer — non-JSON import/export

**What:** export a definition as YAML or PNG; import a definition from another tenant's export bundle.

**Why deferred:** YAML is a serialization bike-shed; image export is `reactflow-to-image`. Neither moves the needle vs "copy the JSON body".

**Pick this up when:** a specific integration (marketplace, template sharing) makes a non-JSON format useful.

---

### Designer — accessibility-first keyboard authoring

**What:** full screen-reader + keyboard-only authoring mode with spoken state/edge descriptions and a linear-list navigator fallback.

**Why deferred:** React Flow ships basic keyboard nav (arrow-select, delete, enter-to-edit). MVP meets that bar. Full a11y authoring requires custom navigators and labels beyond the designer surface.

**Pick this up when:** an enterprise tenant has a compliance requirement (Section 508, WCAG 2.2 AA for authoring tools), OR reports accessibility as a blocker.

---

### Designer — AI-assisted authoring

**What:** natural-language prompt to scaffold a workflow ("build me an expense approval with first-line then director then finance"), AI-generated state suggestions, AI-generated condition expressions from English.

**Why deferred:** depends on the Phase 6 `IWorkflowAiService` work. AI authoring is a natural extension of that, not an MVP-designer feature.

**Pick this up when:** Phase 6 ships.

---

### Designer — richer per-type node skinning

**What:** distinct visual node shapes/iconography per `type` (e.g. diamond for conditional routes, double-ring for parallel states, pill for SystemAction).

**Why deferred:** diminishing returns vs Option A. Nice-to-have, no tenant demand.

**Pick this up when:** UX feedback consistently flags that state types are hard to tell apart at a glance.

---

### Designer — new state-machine constructs

**What:** compensating transitions, sub-workflows / state inlining, timeout-based auto-transitions with designer affordances.

**Why deferred:** these are engine-level capabilities, not designer features. The roadmap treats them as engine work that the designer picks up once the engine supports them.

**Pick this up when:** engine gains the construct AND a tenant needs it authored visually.
