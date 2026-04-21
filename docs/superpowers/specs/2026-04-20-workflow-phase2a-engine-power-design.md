# Workflow & Approvals Phase 2a: Engine Power

**Date:** 2026-04-20
**Status:** Approved design
**Depends on:** Workflow Phase 1 (merged on `feature/workflow-approvals` branch)
**Phase scope:** Step data collection (dynamic forms), compound conditional expressions, parallel approvals, SLA tracking + auto-escalation, delegation

---

## Context

Phase 1 delivered the core state-machine engine: sequential approval chains, single-assignee tasks, simple field-value conditions, free-text comments only. Phase 2a upgrades the engine's power — structured form data, multi-assignee parallel approvals, time-based escalation, delegation, and compound branching logic. These are the capabilities that every downstream feature (AI integration, visual designer, analytics) will consume.

## Cross-Module Integration

All integration via Null-Object-safe capability contracts. The workflow module ships and works without any dependent module installed.

| Module | Integration | Pattern |
|---|---|---|
| Communication | SLA reminders, escalation notifications, delegation notifications | `IMessageDispatcher` — new templates seeded |
| Comments & Activity | Form data submissions recorded as activity entries | `IActivityService` — existing capability |
| Billing | SLA feature gated by plan tier via feature flags | Feature flag `workflow.sla_enabled` |
| Webhooks | Parallel completion + escalation events published externally | `IWebhookPublisher` — existing capability |
| AI (future) | Form schema in `PendingTaskSummary` enables AI agents to read/fill forms programmatically | DTO enrichment only — no AI module dependency |

---

## Feature 1: Step Data Collection (Dynamic Forms)

### Schema Format

Each HumanTask state gains an optional `formFields` array in its definition:

```json
{
    "name": "ManagerReview",
    "type": "HumanTask",
    "formFields": [
        {
            "name": "approvedAmount",
            "label": "Approved Budget Amount",
            "type": "number",
            "required": true,
            "min": 0,
            "max": 100000
        },
        {
            "name": "reason",
            "label": "Decision Reason",
            "type": "select",
            "required": true,
            "options": [
                { "value": "justified", "label": "Budget Justified" },
                { "value": "partial", "label": "Partially Approved" },
                { "value": "other", "label": "Other (see comment)" }
            ]
        },
        {
            "name": "effectiveDate",
            "label": "Effective Date",
            "type": "date",
            "required": false
        },
        {
            "name": "confirmed",
            "label": "I confirm this has been reviewed with the department head",
            "type": "checkbox",
            "required": true
        }
    ]
}
```

### Supported Field Types

| Type | Validation Props | Rendered As |
|---|---|---|
| `text` | `maxLength`, `placeholder` | Input |
| `textarea` | `maxLength`, `placeholder` | Textarea |
| `number` | `min`, `max` | Number input |
| `date` | (none) | Date picker |
| `select` | `options: [{ value, label }]` | Select dropdown |
| `checkbox` | (none) | Checkbox with label |

### New Abstraction

In `Starter.Abstractions/Capabilities/WorkflowConfigRecords.cs`:

```csharp
public sealed record FormFieldDefinition(
    string Name,
    string Label,
    string Type,
    bool Required = false,
    List<SelectOption>? Options = null,
    double? Min = null,
    double? Max = null,
    int? MaxLength = null,
    string? Placeholder = null,
    string? Description = null);

public sealed record SelectOption(string Value, string Label);
```

Add `List<FormFieldDefinition>? FormFields` to `WorkflowStateConfig`.

### Backend Flow

1. `ExecuteTaskCommand` gains `Dictionary<string, object>? FormData`
2. Handler validates submitted data against the state's `formFields`:
   - Required fields present
   - Types match (number is numeric, date is parseable, select value is in options list)
   - Constraints met (min/max, maxLength)
   - Returns validation errors if invalid — task NOT executed
3. Engine stores raw submission in `WorkflowStep.MetadataJson` (immutable audit trail)
4. Engine merges form fields into `WorkflowInstance.ContextJson` (mutable working state for conditions)

### Frontend

- `ApprovalDialog` reads `formFields` from the task's state config
- Renders dynamic form using shadcn/ui components (Input, Select, DatePicker, Checkbox, Textarea)
- Submits `formData` alongside `action` and `comment`
- Shows validation errors inline per field
- When `formFields` is empty/null, dialog behaves as before (comment only)

### DTO Changes

- `PendingTaskSummary` gains `List<FormFieldDefinition>? FormFields` — so the frontend (and future AI agents) know what fields to render/fill
- `WorkflowStepRecord` gains `Dictionary<string, object>? FormData` — so the step history shows what was submitted

---

## Feature 2: Compound Conditional Expressions

### Extended ConditionConfig

The existing `ConditionConfig` record gains group fields:

```csharp
public sealed record ConditionConfig(
    string? Field = null,
    string? Operator = null,
    object? Value = null,
    string? Logic = null,
    List<ConditionConfig>? Conditions = null);
```

**Leaf condition:** `{ field: "amount", operator: "greaterThan", value: 5000 }`
**Group:** `{ logic: "and", conditions: [leaf1, leaf2] }`
**Nested:** `{ logic: "and", conditions: [leaf1, { logic: "or", conditions: [leaf2, leaf3] }] }`

### Engine Change

`ConditionEvaluator.Evaluate` becomes recursive:

```csharp
public bool Evaluate(ConditionConfig condition, Dictionary<string, object>? context)
{
    if (condition.Logic is not null && condition.Conditions is not null)
    {
        return condition.Logic.ToLowerInvariant() switch
        {
            "and" => condition.Conditions.All(c => Evaluate(c, context)),
            "or" => condition.Conditions.Any(c => Evaluate(c, context)),
            _ => false,
        };
    }

    // Existing leaf evaluation logic
    return EvaluateLeaf(condition, context);
}
```

### Integration with Step Data Collection

Form data merged into `ContextJson` becomes available to conditional gates. Example: approver enters `approvedAmount: 4500` → conditional gate branches on `{ logic: "and", conditions: [{ field: "approvedAmount", operator: "greaterThan", value: 5000 }, { field: "reason", operator: "equals", value: "other" }] }`.

### Tests

Extend `ConditionEvaluatorTests` with:
- `Evaluate_AndGroup_AllTrue_ReturnsTrue`
- `Evaluate_AndGroup_OneFalse_ReturnsFalse`
- `Evaluate_OrGroup_OneTrueRestFalse_ReturnsTrue`
- `Evaluate_OrGroup_AllFalse_ReturnsFalse`
- `Evaluate_NestedAndOr_EvaluatesCorrectly`
- `Evaluate_EmptyConditionsList_ReturnsFalse`
- Backward compatibility: existing leaf conditions still work unchanged

---

## Feature 3: Parallel Approvals

### State Config

HumanTask states gain an optional `parallel` config:

```json
{
    "name": "BoardReview",
    "type": "HumanTask",
    "parallel": {
        "mode": "AllOf",
        "assignees": [
            { "strategy": "SpecificUser", "parameters": { "userId": "..." } },
            { "strategy": "Role", "parameters": { "roleName": "Finance" } }
        ]
    },
    "actions": ["Approve", "Reject"]
}
```

When `parallel` is absent: single assignee from the state's `assignee` field (unchanged).
When `parallel` is present: `assignee` field is ignored; `parallel.assignees` array is used.

### New Abstractions

In `WorkflowConfigRecords.cs`:

```csharp
public sealed record ParallelConfig(
    string Mode,  // "AllOf" | "AnyOf"
    List<AssigneeConfig> Assignees);
```

Add `ParallelConfig? Parallel` to `WorkflowStateConfig`.

### Data Model

`ApprovalTask` gains:
- `GroupId` (Guid?) — all tasks in the same parallel group share a GroupId. Null for single-assignee tasks (backward compatible).

### Engine Logic

**Task creation:** When entering a state with `parallel`, the engine creates one `ApprovalTask` per resolved assignee, all sharing the same `GroupId`.

**Task completion in AllOf mode:**
1. Task is completed (approve/reject)
2. Query other tasks with the same `GroupId`
3. If action is Reject: immediately cancel all remaining pending tasks, transition to rejection state
4. If action is Approve: check if ALL tasks in the group are completed with Approve. If yes, transition. If no, wait.

**Task completion in AnyOf mode:**
1. Task is completed (approve/reject)
2. Cancel all remaining pending tasks in the group
3. Transition based on this task's action

### UI Changes

- Task inbox: unchanged — each assignee sees their own task
- Workflow detail page: step timeline shows parallel step status: "2 of 3 approved" or "Waiting for 1 more approval"
- `WorkflowStepTimeline` component gains parallel awareness

### DTO Changes

- `PendingTaskSummary` gains `Guid? GroupId` and `int? ParallelTotal` and `int? ParallelCompleted`

---

## Feature 4: SLA Tracking + Auto-Escalation

### State Config

```json
{
    "name": "PendingManager",
    "type": "HumanTask",
    "sla": {
        "reminderAfterHours": 24,
        "escalateAfterHours": 48
    }
}
```

### New Abstraction

In `WorkflowConfigRecords.cs`:

```csharp
public sealed record SlaConfig(
    int? ReminderAfterHours = null,
    int? EscalateAfterHours = null);
```

Add `SlaConfig? Sla` to `WorkflowStateConfig`.

### ApprovalTask Entity Additions

- `ReminderSentAt` (DateTime?) — when the reminder was sent
- `EscalatedAt` (DateTime?) — when escalation happened
- `OriginalAssigneeUserId` (Guid?) — populated when created via escalation or delegation

### Background Job

`SlaEscalationJob : BackgroundService` runs every 15 minutes:

1. **Reminders:** Find pending tasks where `CreatedAt + reminderHours < now` AND `ReminderSentAt IS NULL`. For each: send reminder via `IMessageDispatcher` with template `workflow.sla-reminder`, set `ReminderSentAt`.

2. **Escalations:** Find pending tasks where `CreatedAt + escalateHours < now` AND `EscalatedAt IS NULL`. For each:
   - Cancel the original task
   - Resolve fallback assignee from the step's assignee config (or tenant admins if no fallback)
   - Create a new task for the fallback with `OriginalAssigneeUserId` set
   - Send `workflow.sla-escalated` notification via `IMessageDispatcher`
   - Set `EscalatedAt` on the original task
   - Publish `WorkflowTaskEscalatedEvent` integration event

### Notification Templates (seeded via ITemplateRegistrar)

| Template | Variables |
|---|---|
| `workflow.sla-reminder` | `assigneeName`, `entityDisplayName`, `definitionName`, `stepName`, `hoursWaiting`, `appUrl` |
| `workflow.sla-escalated` | `newAssigneeName`, `originalAssigneeName`, `entityDisplayName`, `definitionName`, `stepName`, `hoursOverdue`, `appUrl` |

### Feature Gating

SLA can be gated by feature flag `workflow.sla_enabled` tied to plan tier. When the flag is off, the background job skips processing. Seed the flag in `WorkflowModule.SeedDataAsync`.

### UI Changes

- Task inbox: overdue tasks show a red badge/indicator with "Overdue by X hours"
- Workflow detail page: step timeline shows SLA status per step (on time / reminder sent / escalated)
- `PendingTaskSummary` gains `bool IsOverdue` and `int? HoursOverdue`

---

## Feature 5: Delegation

### New Entity

`DelegationRule` in `WorkflowDbContext`:

| Field | Type | Description |
|---|---|---|
| Id | Guid | PK |
| TenantId | Guid? | Multi-tenant |
| FromUserId | Guid | Who is delegating |
| ToUserId | Guid | Who receives the tasks |
| StartDate | DateTime | Delegation starts (UTC) |
| EndDate | DateTime | Delegation ends (UTC) |
| IsActive | bool | Can be manually deactivated |
| CreatedAt | DateTime | |

EF configuration: table `workflow_delegation_rules`, unique index on `(FromUserId, StartDate, EndDate)` to prevent overlapping delegations, tenant query filter.

### Engine Integration

`AssigneeResolverService.ResolveAsync` gains a post-resolution step:

```csharp
// After resolving the primary assignee:
foreach (var userId in resolvedUserIds.ToList())
{
    var delegation = await FindActiveDelegation(userId, ct);
    if (delegation is not null)
    {
        resolvedUserIds.Remove(userId);
        resolvedUserIds.Add(delegation.ToUserId);
        // Store original for audit
        originalAssigneeMap[delegation.ToUserId] = userId;
    }
}
```

`ApprovalTask` uses `OriginalAssigneeUserId` (added in SLA feature) to track the original assignee.

### Visibility

Both the delegate and the original assignee can see the task in their inbox:
- Delegate: sees the task with a "Delegated from [Name]" badge, can act on it
- Original: sees the task as read-only with "Delegated to [Name]" badge

The engine query for `GetPendingTasksAsync` expands to include tasks where `AssigneeUserId == userId OR OriginalAssigneeUserId == userId`.

### API Endpoints

| Method | Path | Description | Permission |
|---|---|---|---|
| POST | `/workflow/delegations` | Create delegation rule | `Workflows.View` (any user for their own) |
| GET | `/workflow/delegations` | List my delegations | `Workflows.View` |
| DELETE | `/workflow/delegations/{id}` | Cancel a delegation | `Workflows.View` (own only) |
| GET | `/workflow/delegations/active` | Check my active delegation | `Workflows.View` |

### Notification Templates

| Template | When | Variables |
|---|---|---|
| `workflow.delegation-created` | Delegate receives notification | `delegatorName`, `delegateName`, `startDate`, `endDate`, `appUrl` |
| `workflow.delegation-ended` | Original user notified delegation expired | `delegatorName`, `delegateName`, `endDate`, `appUrl` |

### Frontend

- "Delegate My Tasks" button on Task Inbox page header
- Dialog: user picker (search), date range picker (start/end), confirm
- When active delegation: amber banner on inbox "Your tasks are delegated to [Name] until [Date] — [Cancel]"
- Delegated tasks show badges: "Delegated from [Name]" for the delegate, "Delegated to [Name]" for the original
- Profile page: "My Delegations" section showing active/upcoming/past delegations

---

## Files Changed (Overview)

### Abstractions
| File | Change |
|---|---|
| `WorkflowConfigRecords.cs` | Add `FormFieldDefinition`, `SelectOption`, `ParallelConfig`, `SlaConfig`. Add `FormFields`, `Parallel`, `Sla` to `WorkflowStateConfig`. Extend `ConditionConfig` with `Logic` + `Conditions`. |
| `WorkflowDtos.cs` | Add `FormFields` + `FormData` to `PendingTaskSummary`/`WorkflowStepRecord`. Add `GroupId`/`ParallelTotal`/`ParallelCompleted`/`IsOverdue`/`HoursOverdue` to `PendingTaskSummary`. |
| `IWorkflowService.cs` | `ExecuteTaskAsync` gains `Dictionary<string, object>? formData` parameter |

### Workflow Module — Domain
| File | Change |
|---|---|
| `ApprovalTask.cs` | Add `GroupId`, `ReminderSentAt`, `EscalatedAt`, `OriginalAssigneeUserId` |
| `DelegationRule.cs` | New entity |
| `WorkflowDomainEvents.cs` | Add `WorkflowTaskEscalatedEvent` |

### Workflow Module — Infrastructure
| File | Change |
|---|---|
| `WorkflowDbContext.cs` | Add `DelegationRules` DbSet + tenant filter |
| `DelegationRuleConfiguration.cs` | New EF configuration |
| `ApprovalTaskConfiguration.cs` | Map new columns + indexes |
| `ConditionEvaluator.cs` | Recursive AND/OR evaluation |
| `WorkflowEngine.cs` | Form data validation + storage, parallel task creation + completion logic, form data merge into context |
| `AssigneeResolverService.cs` | Delegation post-resolution step |
| `SlaEscalationJob.cs` | New background job |
| `FormDataValidator.cs` | New service — validates form data against schema |

### Workflow Module — Application
| File | Change |
|---|---|
| `ExecuteTaskCommand.cs` | Add `FormData` parameter |
| `CreateDelegation/*.cs` | New command |
| `GetDelegations/*.cs` | New query |
| `CancelDelegation/*.cs` | New command |
| `WorkflowController.cs` | Add delegation endpoints |

### Frontend
| File | Change |
|---|---|
| `workflow.types.ts` | Add form field types, parallel fields, SLA fields, delegation types |
| `ApprovalDialog.tsx` | Dynamic form renderer based on `formFields` |
| `DynamicFormRenderer.tsx` | New component — renders form fields by type |
| `WorkflowStepTimeline.tsx` | Parallel step status, SLA indicators, form data display |
| `WorkflowInboxPage.tsx` | Overdue badges, delegation banner, "Delegate" button |
| `DelegationDialog.tsx` | New component — user picker + date range |
| `WorkflowInstanceDetailPage.tsx` | Parallel status, SLA info, submitted form data in step history |

### Tests
| File | What |
|---|---|
| `ConditionEvaluatorTests.cs` | Add 7 compound condition tests |
| `WorkflowEngineTests.cs` | Add parallel approval tests (AllOf/AnyOf), form validation tests |
| `FormDataValidatorTests.cs` | New — field type validation, required fields, constraints |
| `SlaEscalationJobTests.cs` | New — reminder timing, escalation timing, fallback resolution |
| `DelegationTests.cs` | New — delegation resolution, overlap prevention, visibility |

---

## Non-Goals (Phase 2a)

- No visual workflow designer (Phase 2d)
- No AI agent assignee or IWorkflowAiService (Phase 2c)
- No external webhook triggers inbound (Phase 2b)
- No transactional outbox (Phase 2b)
- No bulk operations (Phase 2b)
- No performance optimization / denormalization (Phase 2b)
- No workflow analytics / reporting (Phase 2c)
- No Threshold parallel mode (future — AllOf + AnyOf cover current needs)
- No NOT operator in conditions (use `notEquals` operator on leaf conditions)
- No per-workflow-type delegation scoping (future — global delegation covers current need)
