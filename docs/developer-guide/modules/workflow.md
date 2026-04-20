# Workflow & Approvals — Developer Guide

This module provides a composable state-machine workflow engine. Other modules register their entities as "workflowable" and attach workflows (approvals, reviews, multi-step processes) without coupling to the Workflow module directly.

**Module path:** `boilerplateBE/src/modules/Starter.Module.Workflow/`
**Capability contract:** [`IWorkflowService`](../../../boilerplateBE/src/Starter.Abstractions/Capabilities/IWorkflowService.cs)
**Null-object fallback:** [`NullWorkflowService`](../../../boilerplateBE/src/Starter.Infrastructure/Capabilities/NullObjects/NullWorkflowService.cs)

## Architecture overview

```
WorkflowDefinition (template)
        │
        │ is instantiated as
        ▼
WorkflowInstance (one per request)
        │
        │ contains many
        ▼
WorkflowStep (completed transitions, history)
        │
        │ creates at each HumanTask state
        ▼
ApprovalTask (what the assignee sees in their inbox)
```

- **WorkflowEngine** (`Infrastructure/Services/WorkflowEngine.cs`) is the core — it starts instances, creates tasks, executes user actions, evaluates conditions, advances state, raises events.
- **ApprovalTask** is the unit of assignment. One task per assignee per step (multiple per step when parallel approvals are configured).
- **WorkflowStep** is the audit/history entity — one row per completed transition.

## Registering an entity as workflowable

In the domain module's DI registration, call `AddWorkflowableEntity`:

```csharp
services.AddWorkflowableEntity("Product", async (sp, entityId, ct) => {
    var db = sp.GetRequiredService<IApplicationDbContext>();
    var product = await db.Products.FindAsync([entityId], ct);
    return product is null ? null : new WorkflowableEntityInfo(product.Id, product.Name);
});
```

This registers the entity type with the engine's auto-discovery registry. Afterwards, the Workflow UI's "Start Workflow" button on that entity's detail page works automatically.

## Authoring a workflow definition

A definition is JSON (`WorkflowTemplateConfig`) with arrays of states, transitions, and optional conditions.

Minimal example:

```json
{
  "name": "simple-approval",
  "displayName": "Simple Approval",
  "entityType": "Product",
  "states": [
    { "name": "Draft", "displayName": "Draft", "type": "Initial" },
    {
      "name": "Review",
      "displayName": "Manager Review",
      "type": "HumanTask",
      "assignee": { "strategy": "Role", "value": "Manager" }
    },
    { "name": "Approved", "displayName": "Approved", "type": "Final" }
  ],
  "transitions": [
    { "from": "Draft", "to": "Review", "action": "Submit" },
    { "from": "Review", "to": "Approved", "action": "Approve" }
  ]
}
```

State types are defined as constants in [`Domain/Constants/WorkflowStateTypes.cs`](../../../boilerplateBE/src/modules/Starter.Module.Workflow/Domain/Constants/WorkflowStateTypes.cs): `Initial`, `HumanTask`, `SystemAction`, `Final`.

## Dynamic forms

Any `HumanTask` state can collect structured data via `formFields`:

```json
{
  "name": "Review",
  "type": "HumanTask",
  "formFields": [
    { "name": "reason", "label": "Reason", "type": "text", "required": true, "maxLength": 500 }
  ]
}
```

Submitted form data is validated by `FormDataValidator`, stored on the `WorkflowStep.MetadataJson` row, and merged into `WorkflowInstance.ContextJson` for downstream condition evaluation.

Supported field types: `text`, `textarea`, `number`, `select`, `date`, `checkbox`.

## Assignee strategies

Built-in strategies (`BuiltInAssigneeProvider`):

- `User` — pick a specific user by id.
- `Role` — resolve all users holding the role.
- `Initiator` — the person who started the request.
- `InitiatorManager` — the initiator's manager (requires HR-module integration; falls back to null if not wired).

Custom strategies register `IAssigneeResolverProvider` with `SupportedStrategies => ["MyStrategy"]`. The engine finds the right provider per strategy name.

## Conditions

Condition evaluator supports simple field comparisons and compound AND/OR/NOT:

```json
{
  "operator": "And",
  "conditions": [
    { "field": "amount", "op": "gte", "value": 5000 },
    {
      "operator": "Or",
      "conditions": [
        { "field": "department", "op": "eq", "value": "Engineering" },
        { "field": "department", "op": "eq", "value": "Product" }
      ]
    }
  ]
}
```

Field paths resolve against `WorkflowInstance.ContextJson` (which merges initial submission + all form data submitted along the way).

## Parallel approvals

A state can require multiple assignees:

```json
{
  "name": "BoardVote",
  "type": "HumanTask",
  "assignees": [
    { "strategy": "Role", "value": "BoardMember" }
  ],
  "quorum": { "type": "AnyOf", "threshold": 2 }
}
```

`QuorumType` values: `AllOf` (everyone must act), `AnyOf` (threshold count must act). The engine creates one `ApprovalTask` per resolved assignee and advances only when the quorum is met.

## SLA and escalation

Per-state SLA config:

```json
{
  "sla": {
    "reminderAfterHours": 24,
    "escalateAfterHours": 48
  }
}
```

`SlaEscalationJob` (background service, 15-minute tick) scans pending tasks:
- At `reminderAfterHours`: sends a reminder via `IMessageDispatcher`.
- At `escalateAfterHours`: cancels the original task, creates a replacement task assigned to the fallback (state's `assignee.fallback` config), sends an escalation notification, and raises `WorkflowTaskEscalatedEvent`.

## Hooks

A transition can fire side-effects via hooks (configured on the transition). Execution order: pre-transition → state change → post-transition. `HookExecutor` dispatches registered `IHookHandler` implementations by `HookType`.

## Events

Domain events raised (see [`Domain/Events/WorkflowDomainEvents.cs`](../../../boilerplateBE/src/modules/Starter.Module.Workflow/Domain/Events/WorkflowDomainEvents.cs)):

- `WorkflowStartedEvent`
- `WorkflowTransitionEvent`
- `WorkflowCompletedEvent`
- `WorkflowCancelledEvent`
- `ApprovalTaskAssignedEvent`
- `ApprovalTaskCompletedEvent`
- `WorkflowTaskEscalatedEvent` — raised by `SlaEscalationJob` when a task is escalated with a resolved fallback assignee.

## Known issues

### #6: `WorkflowEngine.cs` is 1200+ lines

The engine currently mixes several responsibilities:

- Sequential transition evaluation + execution
- Parallel-approval task-group coordination (quorum evaluation, sibling-task advancement)
- Auto-transition evaluation (condition-driven state progression without a human action)
- Task creation with assignee resolution, SLA due-date calculation, delegation lookup

Extraction candidates for a future refactor:

- `ParallelApprovalCoordinator` — quorum evaluation and task-group advancement.
- `AutoTransitionEvaluator` — condition-driven next-state progression.
- `HumanTaskFactory` — task creation with assignee resolution, SLA, delegation.

**Status:** Deferred. Current code is well test-covered and working. Extraction is a pure refactor with no capability added.

**Roadmap entry:** [`docs/roadmaps/workflow.md`](../../roadmaps/workflow.md)

**Pick this up when:** Phase 2b integration work needs to modify the engine, OR when the file next needs substantive modification (≥200 new lines).

**Starting points:**
- Group the engine's private methods visually — parallel-coordination, auto-transition, and task-creation clusters are already distinct.
- Introduce one collaborator at a time behind a package-internal interface; existing tests stay green.
- No public-interface change on `IWorkflowService` or the controller.

## Testing

Unit tests live in [`boilerplateBE/tests/Starter.Api.Tests/Workflow/`](../../../boilerplateBE/tests/Starter.Api.Tests/Workflow/):

- `WorkflowEngineTests.cs` — end-to-end engine behavior
- `AssigneeResolverTests.cs` — strategy resolution
- `ConditionEvaluatorTests.cs` — simple + compound expressions
- `FormDataValidatorTests.cs` — form field validation
- `HookExecutorTests.cs` — hook execution ordering
- `SlaEscalationJobTests.cs` — reminders, escalation, event emission
- `DelegationTests.cs` — delegation rule lookup
- `WorkflowModulePermissionsTests.cs` — authorization policy wiring

Follow the existing pattern: in-memory `WorkflowDbContext`, mocked external capabilities (`IMessageDispatcher`, `IUserReader`), FluentAssertions for readable asserts.

## Extending the module

Most Phase 2+ features fall under four patterns:

1. **New assignee strategy** — add `IAssigneeResolverProvider`, register in DI.
2. **New hook type** — add `HookType` enum value, add `IHookHandler`, register.
3. **New condition operator** — extend `ConditionEvaluator`, add test cases.
4. **New entity integration** — call `AddWorkflowableEntity` in the domain module.

Deeper changes (new state type, quorum strategy, transition semantics) require engine modification. See the #6 refactor note before making structural changes.
