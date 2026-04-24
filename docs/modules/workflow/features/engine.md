# Workflow Engine — Core Concepts & Architecture

## Entity model

The workflow engine is built on four core entities:

```
WorkflowDefinition (template)
        │
        │ is instantiated as
        ▼
WorkflowInstance (one per submitted request)
        │
        │ contains many
        ▼
WorkflowStep (completed transitions, history record)
        │
        │ creates at each HumanTask state
        ▼
ApprovalTask (what assignees see in their inbox)
```

### WorkflowDefinition
The template. Contains:
- **States** — Initial, HumanTask, SystemAction, Terminal
- **Transitions** — routing rules with optional conditions and hooks
- **Assignee strategies** — how to resolve who gets a task
- **Form fields** — structured data collection on HumanTask steps
- **SLA config** — reminder and escalation hours
- **Parallel/quorum** — multiple assignees per step, quorum to advance

Seeded by modules or authored by tenant admins. Read-only for templates; customizable via clone.

### WorkflowInstance
A single submitted workflow. Tracks:
- **Current state** — where in the flow it is
- **Status** — Active / Completed / Cancelled / Rejected
- **ContextJson** — submission data + form data merged from each step (mutable)
- **StartedByUserId** — the originator
- **CreatedAt / UpdatedAt** — timeline

Multiple instances can run the same definition concurrently. Scoped to a tenant via global query filters.

### WorkflowStep
Audit trail. One row per completed transition. Immutable. Records:
- **Completed state** — which state was just exited
- **Transition action** — what action triggered the advance (e.g., "Approve", "Reject")
- **ActedByUserId** — who executed it; or the system actor for auto-transitions
- **MetadataJson** — form data submitted at this step
- **Comment** — optional explanation from the actor
- **CreatedAt** — when it happened

Visible in the request detail page's step timeline.

### ApprovalTask
The unit of assignment. What an assignee sees in their inbox. One task per assignee per step (multiple if parallel approvals). Immutable once created; marked as completed when the assignee acts. Contains:
- **AssigneeUserId** — who it's assigned to
- **State name** — which state this task is for
- **Action** — what action the assignee can take (Approve, Reject, Return, etc.)
- **DueAt** — when it should be done (SLA escalation time)
- **OriginalAssigneeUserId** — if delegated, the original owner (for history)
- **DelegatedFromUserId** — if delegated, the delegator

Also denormalized (Phase 2b):
- **DefinitionName, DefinitionDisplayName** — template name
- **EntityType, EntityDisplayName** — what entity this is about
- **StepName** — human-readable state label
- **AssigneeDisplayName, OriginalAssigneeDisplayName** — denormalized names for inbox fast-path
- **FormFieldsJson** — form fields on this step (so inbox renders forms without definition lookups)

## Workflow execution

### Starting a workflow

```csharp
var instanceId = await IWorkflowService.StartAsync(
    entityType: "LeaveRequest",
    entityId: leaveRequestId,
    definitionName: "standard-leave-approval",
    initiatorUserId: userId,
    ct: cancellationToken
);
```

1. **Engine loads definition** — validates it exists
2. **Creates instance** — `WorkflowInstance` with status = Active, context = initial submission
3. **Finds Initial state** — there is exactly one
4. **Evaluates auto-transitions** — if Initial state has no HumanTask, engine auto-advances to next state (via `IAutoTransitionEvaluator`)
5. **Creates tasks** if next state is HumanTask — calls `IAssigneeResolverService` to resolve assignees, creates `ApprovalTask` for each
6. **Raises event** — `WorkflowStartedEvent` + `ApprovalTaskAssignedEvent` for each new task
7. **Publishes outbox** — events published asynchronously; handlers (email, notifications, webhooks) picked up by consumers

### Executing a task

Assignee acts (Approve, Reject, Return):

```csharp
await IWorkflowService.ExecuteTaskAsync(
    taskId: taskId,
    action: "Approve",
    comment: "Looks good",
    actorUserId: userId,
    formData: new { rejectionReason = "..." } // if step has form fields
);
```

1. **Loads task + instance** — validates task exists, not yet completed, and actor is assignee (or platform admin)
2. **Validates form data** — if step declares FormFields, runs `IFormDataValidator` (required, min/max, option membership, date parse)
3. **Returns 400** if validation fails; task stays pending, instance state unchanged
4. **Merges form data** into `WorkflowInstance.ContextJson` if valid
5. **Finds matching transition** — looks for transition from current state with matching trigger (action)
6. **Evaluates condition** if set — if condition fails, error (no valid transition)
7. **Executes pre-transition hooks** — OnExit hooks run before state change
8. **Changes state** — `WorkflowInstance.CurrentState` updates
9. **Executes post-transition hooks** — OnEnter hooks run after state change
10. **Creates WorkflowStep** — audit record
11. **Marks task completed** — `ApprovalTask.CompletedAt = now`
12. **Resolves next state**
    - If HumanTask: creates new tasks for all resolved assignees
    - If SystemAction: immediately executes side effects, evaluates auto-transition
    - If Final: marks instance as Completed
13. **Raises events** — `WorkflowTransitionEvent`, `ApprovalTaskCompletedEvent`, `ApprovalTaskAssignedEvent` for new tasks
14. **Publishes outbox**

Idempotent: if the same action is executed twice on an already-completed task, returns success (no error).

## Assignee resolution

### Built-in strategies

`IAssigneeResolverProvider` implementations resolve strategy strings to user IDs. Built-in:

- **`User`** — pick a specific user by id
  ```json
  { "strategy": "User", "value": "550e8400-e29b-41d4-a716-446655440000" }
  ```

- **`Role`** — resolve all users holding the role
  ```json
  { "strategy": "Role", "value": "Manager" }
  ```
  Returns multiple users → parallel approvals if quorum is configured.

- **`Initiator`** — the person who started the request
  ```json
  { "strategy": "Initiator" }
  ```

- **`InitiatorManager`** — initiator's manager (requires HR/org-chart integration; falls back to null if not wired)
  ```json
  { "strategy": "InitiatorManager" }
  ```

### Custom strategies

Register in DI:

```csharp
services.AddScoped<IAssigneeResolverProvider>(sp => new MyCustomProvider());
```

### Delegation

When resolving an assignee, `IAssigneeResolverService` checks for active `DelegationRule` (date-range scoped). If found, returns delegate instead. The engine records the original assignee on `ApprovalTask.OriginalAssigneeUserId` for history.

### Fallback assignee

When a task escalates due to SLA breach, the engine reassigns to the state's `assignee.fallback` configuration. Common pattern: Task assigned to "Manager" (role), fallback to "Director" (role).

## Conditional routing

Transitions can have conditions. The engine evaluates conditions against `WorkflowInstance.ContextJson` (which merges initial submission + all form data from previous steps).

### Simple conditions

```json
{
  "field": "amount",
  "op": "gte",
  "value": 5000
}
```

Supported operators: `eq`, `neq`, `gt`, `gte`, `lt`, `lte`, `contains`, `notContains`, `in`, `notIn`.

### Compound conditions

Nested AND/OR/NOT:

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

Short-circuit evaluation: AND fails fast on first false; OR succeeds fast on first true.

### Multi-match resolution

When a trigger has multiple matching transitions, the engine:
1. Evaluates conditional transitions first (in order)
2. Takes the first match
3. Falls back to unconditional transition if no condition matched
4. Errors if no match found

## Parallel approvals & quorum

A state can require multiple assignees:

```json
{
  "name": "BoardVote",
  "type": "HumanTask",
  "assignees": [
    { "strategy": "Role", "value": "BoardMember" }
  ],
  "quorum": {
    "type": "AnyOf",
    "threshold": 2
  }
}
```

Engine creates one `ApprovalTask` per resolved assignee. When an assignee acts:
- `ParallelApprovalCoordinator` checks if quorum is met
- Advances state only when quorum satisfied (e.g., 2 out of 5 approve)
- Each task can have its own action/comment

Quorum types: `AllOf` (all must act), `AnyOf` (threshold count must act).

## SLA & escalation

Per-state time budget:

```json
{
  "sla": {
    "reminderAfterHours": 24,
    "escalateAfterHours": 48
  }
}
```

The engine snapshots SLA config on task creation (`ApprovalTask.DueAt`). Background job `SlaEscalationJob` (15-minute tick):

1. **At `reminderAfterHours`** — sends reminder via `IMessageDispatcher`
2. **At `escalateAfterHours`** — escalates:
   - If `fallback` configured on assignee: creates replacement task assigned to fallback, marks original completed with escalation action, deletes original
   - If no fallback: raises `WorkflowTaskEscalatedEvent` for custom handling
   - Escalation is an action (like Approve) — can have its own form fields

Escalation strategy defined per-state: Notify / Reassign / AutoApprove / AutoReject.

## Hooks

Transitions can fire side effects via hooks. Execution order:

1. **Pre-transition** — OnExit hook runs before state change
2. **State change** — instance updated
3. **Post-transition** — OnEnter hook runs after state change

`HookExecutor` dispatches by `HookType` to registered `IHookHandler` implementations. Built-in:
- Email notification
- Webhook publication (outbound)

Custom hooks register `IHookHandler` in DI.

## Events

Domain events raised during workflow execution (see `Domain/Events/WorkflowDomainEvents.cs`):

- **`WorkflowStartedEvent`** — instance created
- **`WorkflowTransitionEvent`** — instance changed state
- **`WorkflowCompletedEvent`** — instance reached Terminal state
- **`WorkflowCancelledEvent`** — instance manually cancelled
- **`ApprovalTaskAssignedEvent`** — task created
- **`ApprovalTaskCompletedEvent`** — task executed
- **`WorkflowTaskEscalatedEvent`** — SLA escalation triggered

Events published via transactional outbox; consumers (email, webhooks, notifications, activity logging, audit) pick up asynchronously.

## Multi-tenancy

All workflow entities use global query filters. Platform admins (`TenantId=null`) see all tenants' workflows; tenant users see only their tenant's. Scoping is automatic via `ApplicationDbContext.OnModelCreating()`.

Batch operations validate per-task tenant scope — a user cannot execute tasks from multiple tenants in one batch (except platform admin).

## Testing patterns

Unit tests in `boilerplateBE/tests/Starter.Api.Tests/Workflow/`:

- **Engine tests** — end-to-end instance lifecycle (start → execute → complete)
- **Assignee resolver tests** — strategy resolution, delegation, fallback
- **Condition evaluator tests** — simple + compound expressions, short-circuit
- **Form data validator tests** — field-level validation
- **Hook executor tests** — hook execution ordering
- **SLA escalation tests** — reminders, escalations, event emission

Test pattern: in-memory `WorkflowDbContext`, mocked capabilities (`IMessageDispatcher`, `IUserReader`, `IAssigneeResolverProvider`), FluentAssertions.

See [Developer Guide → Testing](../developer-guide.md#testing) for setup.
