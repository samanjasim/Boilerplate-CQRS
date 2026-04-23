# Workflow & Approvals Module — Phase 1 Design

**Date:** 2026-04-19
**Status:** Approved design
**Wave:** 1 (Cross-Domain Engines)
**Depends on:** None (consumes Comments, Communication, Webhooks via capabilities — all optional)
**Phase scope:** Engine + human tasks + system actions + conditional gates + pluggable assignee resolution + side-effect hooks + task inbox + admin template management + entity status panel

---

## Problem

Process-driven applications need configurable workflows: leave approval chains, enrollment processing, order lifecycle management. Without a workflow engine, each domain module reinvents status fields, approval logic, and notification routing. The boilerplate needs a single composable engine that any module can use to define multi-step processes with human approvals, automated actions, and configurable routing.

## Solution

A composable state-machine engine in `Starter.Module.Workflow`. Modules seed workflow templates during `SeedDataAsync`; tenant admins clone and customize templates (change approvers, adjust step order, toggle hooks). The engine handles transition logic, assignee resolution, side effects, and task tracking. Other modules consume it through `IWorkflowService` in `Starter.Abstractions` — when the Workflow module is absent, `NullWorkflowService` silently no-ops and consuming modules fall back to simple status fields.

---

## Architecture

### Module Structure

```
Starter.Module.Workflow/
├── Domain/
│   ├── Entities/       — WorkflowDefinition, WorkflowInstance, WorkflowStep, ApprovalTask
│   ├── Enums/          — StepType, TaskStatus, InstanceStatus, TransitionAction
│   ├── Events/         — WorkflowTransitionEvent, ApprovalTaskAssignedEvent, etc.
│   └── Errors/         — WorkflowErrors (static error factory)
├── Application/
│   ├── Commands/       — StartWorkflow, ExecuteTask, CancelWorkflow, CloneDefinition, UpdateDefinition
│   ├── Queries/        — GetPendingTasks, GetHistory, GetDefinitions, GetStatus, GetInstanceList
│   ├── EventHandlers/  — RecordActivity, NotifyAssignee, PublishWebhook, SendEmail
│   └── DTOs/           — WorkflowDefinitionDto, PendingTaskDto, WorkflowStepDto, etc.
├── Infrastructure/
│   ├── Persistence/    — WorkflowDbContext, entity configurations
│   └── Services/       — WorkflowEngine, AssigneeResolverService, HookExecutor, ConditionEvaluator
├── Controllers/        — WorkflowDefinitionsController, WorkflowInstancesController, WorkflowTasksController
├── Constants/          — WorkflowPermissions
├── WorkflowModule.cs   — IModule implementation
└── ROADMAP.md          — Phase 2 deferred items
```

### Capability Contracts (`Starter.Abstractions/Capabilities/`)

```csharp
public interface IWorkflowService : ICapability
{
    // ── Lifecycle ──
    Task<Guid> StartAsync(string entityType, Guid entityId, string definitionName,
        Guid initiatorUserId, Guid? tenantId, CancellationToken ct = default);
    Task CancelAsync(Guid instanceId, string? reason, Guid actorUserId,
        CancellationToken ct = default);

    // ── Task Actions ──
    Task<bool> ExecuteTaskAsync(Guid taskId, string action, string? comment,
        Guid actorUserId, CancellationToken ct = default);

    // ── Query: Status ──
    Task<WorkflowStatusSummary?> GetStatusAsync(string entityType, Guid entityId,
        CancellationToken ct = default);
    Task<bool> IsInStateAsync(string entityType, Guid entityId, string stateName,
        CancellationToken ct = default);

    // ── Query: Inbox ──
    Task<IReadOnlyList<PendingTaskSummary>> GetPendingTasksAsync(Guid userId,
        CancellationToken ct = default);
    Task<int> GetPendingTaskCountAsync(Guid userId, CancellationToken ct = default);

    // ── Query: History (AI, Reporting, Audit) ──
    Task<IReadOnlyList<WorkflowStepRecord>> GetHistoryAsync(Guid instanceId,
        CancellationToken ct = default);
    Task<IReadOnlyList<WorkflowInstanceSummary>> GetInstancesAsync(string entityType,
        string? state = null, int page = 1, int pageSize = 20,
        CancellationToken ct = default);

    // ── Query: Definitions (AI tool discovery, admin UI) ──
    Task<IReadOnlyList<WorkflowDefinitionSummary>> GetDefinitionsAsync(
        string? entityType = null, Guid? tenantId = null,
        CancellationToken ct = default);
    Task<WorkflowDefinitionDetail?> GetDefinitionAsync(Guid definitionId,
        CancellationToken ct = default);

    // ── Template Seeding (used by consuming modules in SeedDataAsync) ──
    Task SeedTemplateAsync(string name, string entityType,
        WorkflowTemplateConfig config, CancellationToken ct = default);
}
```

**DTOs in `Starter.Abstractions`** (no domain entity leakage):

```csharp
public sealed record WorkflowStatusSummary(
    Guid InstanceId, Guid DefinitionId, string DefinitionName,
    string CurrentState, string Status, DateTime StartedAt, Guid StartedByUserId);

public sealed record PendingTaskSummary(
    Guid TaskId, Guid InstanceId, string DefinitionName,
    string EntityType, Guid EntityId, string StepName,
    string? AssigneeRole, DateTime CreatedAt, DateTime? DueDate);

public sealed record WorkflowStepRecord(
    string FromState, string ToState, string StepType, string Action,
    Guid? ActorUserId, string? ActorDisplayName, string? Comment,
    DateTime Timestamp, Dictionary<string, object>? Metadata);

public sealed record WorkflowInstanceSummary(
    Guid InstanceId, Guid DefinitionId, string DefinitionName,
    string EntityType, Guid EntityId, string CurrentState,
    string Status, DateTime StartedAt, DateTime? CompletedAt);

public sealed record WorkflowDefinitionSummary(
    Guid Id, string Name, string EntityType, int StepCount,
    bool IsTemplate, bool IsActive, string? SourceModule);

public sealed record WorkflowDefinitionDetail(
    Guid Id, string Name, string EntityType, bool IsTemplate,
    bool IsActive, string? SourceModule,
    List<WorkflowStateConfig> States,
    List<WorkflowTransitionConfig> Transitions);
```

### Assignee Resolution

Pluggable strategy pattern via `IAssigneeResolverProvider` in `Starter.Abstractions`:

```csharp
public interface IAssigneeResolverProvider : ICapability
{
    /// <summary>
    /// Returns the strategy names this provider handles (e.g., "OrgManager", "DepartmentHead").
    /// </summary>
    IReadOnlyList<string> SupportedStrategies { get; }

    /// <summary>
    /// Resolves assignee user IDs for the given strategy and context.
    /// </summary>
    Task<IReadOnlyList<Guid>> ResolveAsync(
        string strategy, Dictionary<string, object> parameters,
        WorkflowAssigneeContext context, CancellationToken ct = default);
}

public sealed record WorkflowAssigneeContext(
    string EntityType, Guid EntityId, Guid? TenantId,
    Guid InitiatorUserId, string CurrentState);
```

**Built-in strategies** (in the Workflow module itself):

| Strategy | Resolution | Parameters |
|---|---|---|
| `SpecificUser` | Hardcoded user ID | `{ "userId": "guid" }` |
| `Role` | Any user with the specified role | `{ "roleName": "Admin" }` |
| `EntityCreator` | The user who started the workflow | None |

**Module-provided strategies** (registered by other modules):

| Strategy | Provider Module | Resolution |
|---|---|---|
| `OrgManager` | Employees (future) | The initiator's direct manager |
| `DepartmentHead` | Employees (future) | Head of the specified department |

Modules register providers via `services.AddScoped<IAssigneeResolverProvider, OrgAssigneeProvider>()` in their `ConfigureServices`. The Workflow module collects all registered providers via `IEnumerable<IAssigneeResolverProvider>` and routes by strategy name.

Each step definition includes a primary assignee strategy and an optional fallback:

```json
{
    "stepName": "ManagerReview",
    "type": "HumanTask",
    "assignee": {
        "strategy": "OrgManager",
        "parameters": {},
        "fallback": {
            "strategy": "Role",
            "parameters": { "roleName": "Admin" }
        }
    }
}
```

When the primary strategy's provider isn't installed (e.g., Employees module absent), the engine uses the fallback. If both fail, the task is assigned to tenant admins.

---

## Entities

### `WorkflowDefinition`

| Field | Type | Description |
|---|---|---|
| Id | Guid | PK |
| TenantId | Guid? | Null = system template (visible to all tenants) |
| Name | string | Unique per tenant (e.g., "leave-approval") |
| DisplayName | string | Human-readable (e.g., "Leave Approval") |
| Description | string? | Admin-facing description |
| EntityType | string | Which entity type this workflow applies to (e.g., "LeaveRequest") |
| IsTemplate | bool | True = module-seeded template, cannot be edited directly |
| IsActive | bool | Can be deactivated without deletion |
| SourceDefinitionId | Guid? | If cloned from a template, points to the original |
| SourceModule | string? | Which module seeded this template (e.g., "Leave") |
| Version | int | Incremented on each edit |
| StatesJson | string | JSON array of state configurations |
| TransitionsJson | string | JSON array of transition rules |
| CreatedAt | DateTime | |
| ModifiedAt | DateTime? | |

**StatesJson structure:**

```json
[
    {
        "name": "Draft",
        "displayName": "Draft",
        "type": "Initial",
        "onEnter": [],
        "onExit": []
    },
    {
        "name": "PendingManager",
        "displayName": "Pending Manager Review",
        "type": "HumanTask",
        "assignee": {
            "strategy": "Role",
            "parameters": { "roleName": "Admin" },
            "fallback": null
        },
        "actions": ["Approve", "Reject", "ReturnForRevision"],
        "onEnter": [
            { "type": "notify", "template": "workflow.task-assigned", "to": "assignee" },
            { "type": "activity", "action": "workflow_transition" }
        ],
        "onExit": []
    },
    {
        "name": "Approved",
        "displayName": "Approved",
        "type": "Terminal",
        "onEnter": [
            { "type": "notify", "template": "workflow.request-approved", "to": "initiator" },
            { "type": "activity", "action": "workflow_transition" },
            { "type": "webhook", "event": "workflow.completed" }
        ],
        "onExit": []
    }
]
```

**TransitionsJson structure:**

```json
[
    {
        "from": "Draft",
        "to": "PendingManager",
        "trigger": "Submit",
        "type": "Manual"
    },
    {
        "from": "PendingManager",
        "to": "Approved",
        "trigger": "Approve",
        "type": "Manual"
    },
    {
        "from": "PendingManager",
        "to": "Rejected",
        "trigger": "Reject",
        "type": "Manual"
    },
    {
        "from": "PendingManager",
        "to": "Draft",
        "trigger": "ReturnForRevision",
        "type": "Manual"
    },
    {
        "from": "PendingManager",
        "to": "PendingDirector",
        "trigger": "Approve",
        "type": "Conditional",
        "condition": { "field": "amount", "operator": "greaterThan", "value": 1000 }
    }
]
```

### `WorkflowInstance`

| Field | Type | Description |
|---|---|---|
| Id | Guid | PK |
| TenantId | Guid? | Multi-tenant isolation |
| DefinitionId | Guid | FK to WorkflowDefinition |
| EntityType | string | Denormalized for querying |
| EntityId | Guid | The entity this workflow runs on |
| CurrentState | string | Current state name |
| Status | InstanceStatus | Active, Completed, Cancelled |
| StartedByUserId | Guid | Who initiated |
| StartedAt | DateTime | |
| CompletedAt | DateTime? | |
| CancelledAt | DateTime? | |
| CancelledByUserId | Guid? | |
| CancellationReason | string? | |
| ContextJson | string? | Entity-specific data passed at start (used by conditional gates) |

### `WorkflowStep`

| Field | Type | Description |
|---|---|---|
| Id | Guid | PK |
| InstanceId | Guid | FK to WorkflowInstance |
| FromState | string | |
| ToState | string | |
| StepType | StepType | HumanTask, SystemAction, ConditionalGate |
| Action | string | "Approve", "Reject", "Auto", "BranchTrue", etc. |
| ActorUserId | Guid? | Null for system actions |
| Comment | string? | Approval comment |
| MetadataJson | string? | Additional step data |
| Timestamp | DateTime | |

### `ApprovalTask`

| Field | Type | Description |
|---|---|---|
| Id | Guid | PK |
| TenantId | Guid? | Multi-tenant isolation |
| InstanceId | Guid | FK to WorkflowInstance |
| StepName | string | Which state this task is for |
| AssigneeUserId | Guid? | Resolved specific user |
| AssigneeRole | string? | Role-based assignment |
| AssigneeStrategyJson | string? | Full strategy config (for re-resolution if needed) |
| Status | TaskStatus | Pending, Completed, Cancelled, Reassigned |
| Action | string? | What action was taken (Approve/Reject/Return) |
| Comment | string? | Actor's comment |
| DueDate | DateTime? | Optional deadline |
| CreatedAt | DateTime | |
| CompletedAt | DateTime? | |
| CompletedByUserId | Guid? | |

---

## Step Types

### HumanTask

Waits for a person to act. Creates an `ApprovalTask` record. The assigned user sees it in their inbox and on the entity detail page. Available actions are defined per state in the definition (typically Approve, Reject, ReturnForRevision). On action:

1. `ApprovalTask` marked completed with action + comment
2. Transition to the target state per `TransitionsJson`
3. `WorkflowStep` record created
4. On-exit hooks of current state fire
5. On-enter hooks of target state fire
6. If the actor wrote a comment: `ICommentService.AddCommentAsync()` saves it to the entity timeline
7. `IActivityService.RecordAsync()` records the transition as an activity entry
8. Integration event `WorkflowTransitionEvent` published

### SystemAction

Executes automatically when the state is entered. No human involvement. Examples: send notification, update entity field, publish webhook. On enter:

1. Execute the configured action (call capability contract)
2. Immediately transition to the next state
3. Log as `WorkflowStep` with `StepType.SystemAction`

### ConditionalGate

Evaluates a condition and branches. On enter:

1. Evaluate condition against `WorkflowInstance.ContextJson`
2. If true: follow the conditional transition
3. If false: follow the default transition
4. Log as `WorkflowStep` with `StepType.ConditionalGate`

**Condition format (Phase 1 — simple field-value matching):**

```json
{ "field": "amount", "operator": "greaterThan", "value": 1000 }
```

Supported operators: `equals`, `notEquals`, `greaterThan`, `lessThan`, `greaterThanOrEqual`, `lessThanOrEqual`, `contains`, `in`.

---

## Side-Effect Hooks

Each state can have `onEnter` and `onExit` hook arrays. Each hook has a `type` that maps to a capability:

| Hook Type | Capability Used | What It Does |
|---|---|---|
| `notify` | `IMessageDispatcher` | Send email/notification to assignee, initiator, or specific role |
| `activity` | `IActivityService` | Record workflow transition as activity entry |
| `webhook` | `IWebhookPublisher` | Publish event to configured webhook endpoints |
| `inAppNotify` | `INotificationServiceCapability` | Create in-app notification (bell icon) |

All routed through Null-Object-safe capability contracts. When Communication is absent, `notify` hooks silently skip. When Webhooks is absent, `webhook` hooks skip. The workflow engine never breaks.

---

## Integration with Comments & Activity

### Activity Recording

On every transition, the engine calls `IActivityService.RecordAsync()` with action `"workflow_transition"` and metadata containing `fromState`, `toState`, `action`, `actorDisplayName`. The Comments module's timeline component renders these with a distinct visual treatment (milestone card style vs regular comment bubble) — unified chronological timeline with visual differentiation (Option C from brainstorming).

### Approval Comments

When a user approves/rejects with a comment, the engine calls `ICommentService.AddCommentAsync()` to save the comment as a regular entity comment. This means:
- The comment appears in the unified timeline alongside other discussion
- Other users can reply to it
- Mentions in approval comments trigger mention notifications
- The comment is also stored in `WorkflowStep.Comment` for the workflow's own audit trail

### When Comments Module Is Absent

`NullActivityService` and `NullCommentService` silently no-op. The workflow still works — transitions are tracked in `WorkflowStep` records. The entity detail page won't show the unified timeline, but the workflow status panel still shows step history from its own data.

---

## Integration Events

Published via MassTransit on every significant workflow action:

| Event | When | Key Data |
|---|---|---|
| `WorkflowStartedEvent` | Instance created | instanceId, entityType, entityId, definitionName, initiatorUserId |
| `WorkflowTransitionEvent` | State changed | instanceId, fromState, toState, action, actorUserId, entityType, entityId |
| `WorkflowCompletedEvent` | Reached terminal state | instanceId, entityType, entityId, finalState, duration |
| `WorkflowCancelledEvent` | Cancelled | instanceId, reason, cancelledByUserId |
| `ApprovalTaskAssignedEvent` | New task created | taskId, instanceId, assigneeUserId, stepName, entityType, entityId |
| `ApprovalTaskCompletedEvent` | Task acted on | taskId, action, actorUserId, comment |

Consumers can subscribe to these for analytics, AI pattern detection, external system sync, etc.

---

## Template Seeding

Consuming modules seed workflow templates in their `SeedDataAsync`:

```csharp
public async Task SeedDataAsync(IServiceProvider services, CancellationToken ct)
{
    var workflowService = scope.ServiceProvider.GetRequiredService<IWorkflowService>();

    await workflowService.SeedTemplateAsync(
        name: "leave-approval",
        entityType: "LeaveRequest",
        config: new WorkflowTemplateConfig
        {
            DisplayName = "Leave Approval",
            Description = "Standard leave request approval flow",
            States = [
                new("Draft", "Draft", StateType.Initial),
                new("PendingManager", "Pending Manager", StateType.HumanTask,
                    Assignee: new("Role", new() { ["roleName"] = "Admin" }),
                    Actions: ["Approve", "Reject", "ReturnForRevision"],
                    OnEnter: [new("notify", "workflow.task-assigned", "assignee"),
                              new("activity", "workflow_transition")]),
                new("Approved", "Approved", StateType.Terminal,
                    OnEnter: [new("notify", "workflow.request-approved", "initiator"),
                              new("activity", "workflow_transition")]),
                new("Rejected", "Rejected", StateType.Terminal,
                    OnEnter: [new("notify", "workflow.request-rejected", "initiator"),
                              new("activity", "workflow_transition")]),
            ],
            Transitions = [
                new("Draft", "PendingManager", "Submit"),
                new("PendingManager", "Approved", "Approve"),
                new("PendingManager", "Rejected", "Reject"),
                new("PendingManager", "Draft", "ReturnForRevision"),
            ],
        },
        ct);
}
```

When the Workflow module is absent, `NullWorkflowService.SeedTemplateAsync` silently no-ops.

---

## Frontend

### 1. Task Inbox (`/workflows/inbox`)

Dedicated page with sidebar nav entry showing badge count. Filterable table:

| Column | Description |
|---|---|
| Entity | Name/reference of the entity + link |
| Workflow | Definition display name |
| Step | Current step name |
| Assigned | Date assigned |
| Due | Due date (if set) |
| Actions | Approve/Reject buttons |

**Dashboard widget slot:** Top 5 pending tasks with "View All →" link.

**Bell integration:** New approval tasks create in-app notifications via `INotificationServiceCapability`. The notification deep-links to the entity detail page where the approval dialog is accessible.

**Email/channel notification:** `IMessageDispatcher.SendAsync("workflow.task-assigned", ...)` sends via the tenant's configured channels. Users control this via notification preferences (`WorkflowTaskAssigned` preference type in `WellKnownNotificationTypes`).

### 2. Workflow Admin (`/workflows/definitions`)

List page showing all workflow definitions for the tenant:

| Column | Description |
|---|---|
| Name | Definition display name |
| Entity Type | Which entity type |
| Steps | Step count |
| Source | "System Template" badge or "Customized" badge |
| Status | Active/Inactive |
| Actions | Clone (templates), Edit/View/Deactivate (custom) |

**Detail/Edit page:** Form-based editing of step assignees and hooks. Shows a read-only ordered step list with per-step configuration. System templates are read-only (must clone first). Phase 2 adds visual drag-and-drop designer.

### 3. Entity Workflow Status Panel (Slot)

Registered via `registerSlot('entity-detail-workflow', ...)`. Shows on any entity detail page that has an active workflow:

- Current state badge
- Approve/Reject/Return buttons (if current user has a pending task)
- Step timeline integrated with the Comments & Activity unified timeline (Option C: visually differentiated workflow milestones in chronological feed)

### Approval Dialog

Modal opened from the inbox or entity panel. Contains:
- Entity reference (type + name/ID)
- Action buttons (Approve, Reject, Return for Revision — as defined per state)
- Optional comment textarea
- Note: "Your comment will appear in the entity's activity timeline"

---

## Permissions

| Permission | Description | SuperAdmin | Admin | User |
|---|---|---|---|---|
| Workflows.View | View workflow definitions and instances | ✅ | ✅ | ✅ |
| Workflows.ManageDefinitions | Clone, edit, activate/deactivate definitions | ✅ | ✅ | ❌ |
| Workflows.Start | Start a workflow on an entity | ✅ | ✅ | ✅ |
| Workflows.ActOnTask | Approve/reject/return assigned tasks | ✅ | ✅ | ✅ |
| Workflows.Cancel | Cancel an active workflow instance | ✅ | ✅ | ❌ |
| Workflows.ViewAllTasks | See all pending tasks (not just own) | ✅ | ✅ | ❌ |

---

## Notification Templates (seeded via `ITemplateRegistrar`)

| Template Name | When | Variables |
|---|---|---|
| `workflow.task-assigned` | New approval task created | `assigneeName`, `entityType`, `entityId`, `stepName`, `definitionName`, `initiatorName`, `appUrl` |
| `workflow.request-approved` | Workflow reached "Approved" terminal state | `initiatorName`, `entityType`, `entityId`, `definitionName`, `approverName`, `comment`, `appUrl` |
| `workflow.request-rejected` | Workflow reached "Rejected" terminal state | `initiatorName`, `entityType`, `entityId`, `definitionName`, `rejectorName`, `comment`, `appUrl` |
| `workflow.task-returned` | Task returned for revision | `initiatorName`, `entityType`, `entityId`, `stepName`, `returnerName`, `comment`, `appUrl` |

---

## DI Registration

### In `Starter.Abstractions`

- `IWorkflowService` interface + DTOs (records)
- `IAssigneeResolverProvider` interface + `WorkflowAssigneeContext` record
- `WellKnownNotificationTypes.WorkflowTaskAssigned` constant

### In `Starter.Infrastructure`

- `NullWorkflowService` — all methods return empty results / no-op
- `services.TryAddScoped<IWorkflowService, NullWorkflowService>()`

### In `Starter.Module.Workflow` (`WorkflowModule.ConfigureServices`)

- `WorkflowDbContext` with isolated migration history table
- `services.AddScoped<IWorkflowService, WorkflowEngine>()` — replaces Null Object
- Built-in `IAssigneeResolverProvider` for SpecificUser, Role, EntityCreator strategies
- `HookExecutor`, `ConditionEvaluator` services
- Health check for WorkflowDbContext

---

## `scripts/modules.json` Registration

```json
"workflow": {
    "displayName": "Workflow & Approvals",
    "backendModule": "Starter.Module.Workflow",
    "frontendFeature": "workflow",
    "testsFolder": "Workflow",
    "configKey": "workflow",
    "required": false,
    "description": "Configurable state-machine workflows with approval chains, task inbox, and process automation. Implements IWorkflowService; null fallback returns empty results. Frontend: inbox page, admin definitions, entity status panel slot."
}
```

---

## Error Handling

| Scenario | Behavior |
|---|---|
| Workflow module absent | `NullWorkflowService` returns empty lists / `null` / no-ops. Consuming modules fall back to simple status fields. |
| Comments module absent | Transitions still tracked in `WorkflowStep`. No activity entries or approval comments in timeline. |
| Communication module absent | No email/channel notifications on task assignment. In-app notifications still work via `INotificationServiceCapability`. |
| Invalid transition attempted | Returns error result — the requested action is not valid from the current state. |
| Assignee strategy provider not installed | Falls back to the step's fallback strategy. If all fail, assigns to tenant admins. |
| Conditional gate field missing from context | Follows the default (non-conditional) transition. Logs warning. |
| Multiple active workflows on same entity | Allowed — each has its own instance. The status panel shows all active workflows. |

---

## Multi-Tenancy

- `WorkflowDefinition`, `WorkflowInstance`, `ApprovalTask` have `TenantId` with global query filters
- System templates (`IsTemplate = true`) have `TenantId = null` — visible to all tenants
- Cloned definitions get the cloning tenant's `TenantId`
- Platform admins (`TenantId = null`) see all data
- Entity-level tenant scoping: the engine passes `tenantId` from the initiator to the instance

---

## Phase 2 — Committed, Documented

These features are not in Phase 1 scope but are committed to and will be built:

### Step Data Collection (Dynamic Forms)
Each HumanTask step can define a `formSchema` (JSON) specifying fields the actor must fill in (dropdown, text, number, date). The frontend renders a dynamic form in the approval dialog. Captured data stored in `WorkflowStep.MetadataJson` and queryable via `IWorkflowService.GetHistoryAsync()`.

### Visual Workflow Designer
Drag-and-drop state/transition builder replacing the form-based editor. Renders states as nodes, transitions as arrows. Admins can create workflows from scratch (not just clone templates).

### SLA Tracking & Auto-Escalation
Per-step time limits configured in the definition. A scheduled background job detects overdue tasks and either escalates (reassign to fallback) or sends reminder notifications. `ApprovalTask.DueDate` already exists in Phase 1 entities.

### Delegation
"I'm out of office — delegate all my pending tasks to User X." Creates `Reassigned` entries in `ApprovalTask` and re-notifies the delegate.

### Parallel Approval
"Both Manager AND HR must approve" (parallel) vs "Manager THEN HR" (sequential). Phase 1 supports sequential only. Parallel requires `ApprovalTask` grouping and a completion condition (all / majority / any).

### Compound Conditional Expressions
`{ "and": [{ "field": "amount", "gt": 1000 }, { "field": "dept", "eq": "Finance" }] }`. Replaces the simple single-condition format from Phase 1.

### AI Integration
- AI-powered assignee suggestions based on historical approval patterns
- Auto-approval recommendations for low-risk requests
- Bottleneck detection and workflow optimization insights
- Natural language workflow creation ("create a 3-step approval for expenses over $500")

### Workflow Analytics & Reporting
- Average time per step / per workflow
- Bottleneck identification (which steps take longest)
- Approval/rejection ratios
- SLA compliance rates
- Data source registration for the Reporting module

---

## Non-Goals (Phase 1)

- No visual drag-and-drop workflow designer (Phase 2)
- No step data collection / dynamic forms (Phase 2)
- No SLA tracking or auto-escalation (Phase 2)
- No parallel approvals (Phase 2)
- No delegation (Phase 2)
- No compound conditional expressions (Phase 2)
- No AI routing or suggestions (Phase 2)
- No workflow versioning with migration of in-flight instances (design later)
- No sub-workflows or workflow composition
- No mobile-specific UI (Flutter app — future phase)
