# Workflow Phase 2a: Engine Power — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Upgrade the workflow engine with step data collection (dynamic forms), compound conditions, parallel approvals (AllOf/AnyOf), SLA tracking + auto-escalation, and delegation.

**Architecture:** Extend existing `WorkflowStateConfig` with `FormFields`, `Parallel`, `Sla` configs. Add `FormDataValidator` service for schema validation. Extend `ConditionEvaluator` with recursive AND/OR. Add `GroupId` to `ApprovalTask` for parallel task grouping. New `SlaEscalationJob` background service for time-based processing. New `DelegationRule` entity + resolver integration. All cross-module integration via Null-Object-safe capabilities.

**Tech Stack:** .NET 10 (MediatR, EF Core, BackgroundService), React 19 (TypeScript, TanStack Query, Tailwind CSS 4, shadcn/ui)

**Spec:** `docs/superpowers/specs/2026-04-20-workflow-phase2a-engine-power-design.md`

---

## Task 1: Extend Abstractions — Config Records + DTOs

**Files:**
- Modify: `boilerplateBE/src/Starter.Abstractions/Capabilities/WorkflowConfigRecords.cs`
- Modify: `boilerplateBE/src/Starter.Abstractions/Capabilities/WorkflowDtos.cs`
- Modify: `boilerplateBE/src/Starter.Abstractions/Capabilities/IWorkflowService.cs`

- [ ] **Step 1: Add new config records to WorkflowConfigRecords.cs**

Add after existing records:

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

public sealed record ParallelConfig(
    string Mode,
    List<AssigneeConfig> Assignees);

public sealed record SlaConfig(
    int? ReminderAfterHours = null,
    int? EscalateAfterHours = null);
```

Add `FormFields`, `Parallel`, `Sla` to `WorkflowStateConfig`:

```csharp
public sealed record WorkflowStateConfig(
    string Name,
    string DisplayName,
    string Type,
    AssigneeConfig? Assignee = null,
    List<string>? Actions = null,
    List<HookConfig>? OnEnter = null,
    List<HookConfig>? OnExit = null,
    List<FormFieldDefinition>? FormFields = null,
    ParallelConfig? Parallel = null,
    SlaConfig? Sla = null);
```

Extend `ConditionConfig` for compound expressions (backward compatible — existing leaf fields become nullable):

```csharp
public sealed record ConditionConfig(
    string? Field = null,
    string? Operator = null,
    object? Value = null,
    string? Logic = null,
    List<ConditionConfig>? Conditions = null);
```

- [ ] **Step 2: Extend DTOs in WorkflowDtos.cs**

Update `PendingTaskSummary`:
```csharp
public sealed record PendingTaskSummary(
    Guid TaskId, Guid InstanceId, string DefinitionName,
    string EntityType, Guid EntityId, string StepName,
    string? AssigneeRole, DateTime CreatedAt, DateTime? DueDate,
    List<string>? AvailableActions = null,
    string? EntityDisplayName = null,
    List<FormFieldDefinition>? FormFields = null,
    Guid? GroupId = null,
    int? ParallelTotal = null,
    int? ParallelCompleted = null,
    bool IsOverdue = false,
    int? HoursOverdue = null,
    bool IsDelegated = false,
    string? DelegatedFromDisplayName = null);
```

Update `WorkflowStepRecord`:
```csharp
public sealed record WorkflowStepRecord(
    string FromState, string ToState, string StepType, string Action,
    Guid? ActorUserId, string? ActorDisplayName, string? Comment,
    DateTime Timestamp, Dictionary<string, object>? Metadata,
    Dictionary<string, object>? FormData = null);
```

- [ ] **Step 3: Update IWorkflowService.ExecuteTaskAsync signature**

```csharp
Task<bool> ExecuteTaskAsync(Guid taskId, string action, string? comment,
    Guid actorUserId, Dictionary<string, object>? formData = null,
    CancellationToken ct = default);
```

Update `NullWorkflowService` to match.

- [ ] **Step 4: Build to verify backward compatibility**

Run: `dotnet build boilerplateBE --nologo`
Expected: 0 errors. The default parameter values maintain backward compatibility.

- [ ] **Step 5: Fix any compile errors from ConditionConfig change**

The `ConditionConfig` fields changed from required to optional. Find all callers (ConditionEvaluator, tests, seed data) and verify they still work. The evaluator's null checks need adjustment.

- [ ] **Step 6: Run tests**

Run: `dotnet test boilerplateBE/tests/Starter.Api.Tests/Starter.Api.Tests.csproj --nologo`
Fix any failures from the signature changes.

- [ ] **Step 7: Commit**

```bash
git add boilerplateBE/src/Starter.Abstractions/ boilerplateBE/src/Starter.Infrastructure/
git commit -m "feat(workflow): extend abstractions for Phase 2a — forms, parallel, SLA, delegation"
```

---

## Task 2: Compound Conditions (TDD)

**Files:**
- Modify: `boilerplateBE/src/modules/Starter.Module.Workflow/Infrastructure/Services/ConditionEvaluator.cs`
- Modify: `boilerplateBE/tests/Starter.Api.Tests/Workflow/ConditionEvaluatorTests.cs`

- [ ] **Step 1: Write 7 new tests**

Add to `ConditionEvaluatorTests.cs`:

1. `Evaluate_AndGroup_AllTrue_ReturnsTrue` — `{ logic: "and", conditions: [equals-true, equals-true] }` → true
2. `Evaluate_AndGroup_OneFalse_ReturnsFalse` — `{ logic: "and", conditions: [equals-true, equals-false] }` → false
3. `Evaluate_OrGroup_OneTrueRestFalse_ReturnsTrue` — `{ logic: "or", conditions: [equals-false, equals-true] }` → true
4. `Evaluate_OrGroup_AllFalse_ReturnsFalse` — `{ logic: "or", conditions: [equals-false, equals-false] }` → false
5. `Evaluate_NestedAndOr_EvaluatesCorrectly` — `{ logic: "and", conditions: [leaf, { logic: "or", conditions: [leaf, leaf] }] }` → test nested evaluation
6. `Evaluate_EmptyConditionsList_ReturnsFalse` — `{ logic: "and", conditions: [] }` → false (or true for AND? Go with: AND of empty = true, OR of empty = false — mathematical convention)
7. `Evaluate_LeafCondition_StillWorksUnchanged` — existing leaf format still works (backward compat)

- [ ] **Step 2: Run tests — should fail on new tests**

- [ ] **Step 3: Update ConditionEvaluator.Evaluate**

Make the method recursive:

```csharp
public bool Evaluate(ConditionConfig condition, Dictionary<string, object>? context)
{
    // Group evaluation (AND/OR)
    if (condition.Logic is not null && condition.Conditions is not null)
    {
        return condition.Logic.ToLowerInvariant() switch
        {
            "and" => condition.Conditions.Count == 0 || condition.Conditions.All(c => Evaluate(c, context)),
            "or" => condition.Conditions.Any(c => Evaluate(c, context)),
            _ => false,
        };
    }

    // Leaf evaluation (existing logic)
    if (condition.Field is null) return false;
    // ... existing EvaluateLeaf logic
}
```

Refactor existing leaf logic into a private `EvaluateLeaf` method.

- [ ] **Step 4: Run tests — all should pass (old + new)**
- [ ] **Step 5: Commit**

```bash
git add boilerplateBE/src/modules/Starter.Module.Workflow/Infrastructure/Services/ConditionEvaluator.cs \
       boilerplateBE/tests/Starter.Api.Tests/Workflow/ConditionEvaluatorTests.cs
git commit -m "feat(workflow): compound conditions with recursive AND/OR evaluation (TDD)"
```

---

## Task 3: FormDataValidator (TDD)

**Files:**
- Create: `boilerplateBE/src/modules/Starter.Module.Workflow/Infrastructure/Services/FormDataValidator.cs`
- Create: `boilerplateBE/tests/Starter.Api.Tests/Workflow/FormDataValidatorTests.cs`

- [ ] **Step 1: Write tests**

Test cases:
1. `Validate_AllRequiredFieldsPresent_ReturnsNoErrors`
2. `Validate_MissingRequiredField_ReturnsError`
3. `Validate_NumberBelowMin_ReturnsError`
4. `Validate_NumberAboveMax_ReturnsError`
5. `Validate_TextExceedsMaxLength_ReturnsError`
6. `Validate_SelectValueNotInOptions_ReturnsError`
7. `Validate_CheckboxRequired_FalseValue_ReturnsError`
8. `Validate_OptionalFieldMissing_NoError`
9. `Validate_NoFormFields_ReturnsNoErrors` (null/empty schema = no validation)
10. `Validate_DateField_InvalidFormat_ReturnsError`

Interface:
```csharp
public interface IFormDataValidator
{
    List<FormValidationError> Validate(
        List<FormFieldDefinition>? formFields,
        Dictionary<string, object>? formData);
}

public sealed record FormValidationError(string FieldName, string Message);
```

- [ ] **Step 2: Run tests — should fail**
- [ ] **Step 3: Implement FormDataValidator**

Validate each field definition against the submitted data. Handle `JsonElement` values (same as ConditionEvaluator). Return a list of errors — empty list means valid.

- [ ] **Step 4: Run tests — should pass**
- [ ] **Step 5: Commit**

```bash
git add boilerplateBE/src/modules/Starter.Module.Workflow/Infrastructure/Services/FormDataValidator.cs \
       boilerplateBE/tests/Starter.Api.Tests/Workflow/FormDataValidatorTests.cs
git commit -m "feat(workflow): FormDataValidator for dynamic form schema validation (TDD)"
```

---

## Task 4: Step Data Collection in Engine

**Files:**
- Modify: `boilerplateBE/src/modules/Starter.Module.Workflow/Infrastructure/Services/WorkflowEngine.cs`
- Modify: `boilerplateBE/src/modules/Starter.Module.Workflow/Application/Commands/ExecuteTask/ExecuteTaskCommand.cs`
- Modify: `boilerplateBE/src/modules/Starter.Module.Workflow/Application/Commands/ExecuteTask/ExecuteTaskCommandHandler.cs`
- Modify: `boilerplateBE/src/modules/Starter.Module.Workflow/Controllers/WorkflowController.cs`
- Modify: `boilerplateBE/src/modules/Starter.Module.Workflow/WorkflowModule.cs`

- [ ] **Step 1: Update ExecuteTaskCommand**

```csharp
public sealed record ExecuteTaskCommand(
    Guid TaskId,
    string Action,
    string? Comment = null,
    Dictionary<string, object>? FormData = null) : IRequest<Result<bool>>;
```

- [ ] **Step 2: Update ExecuteTaskCommandHandler**

Pass `FormData` through to `workflowService.ExecuteTaskAsync`.

- [ ] **Step 3: Update WorkflowEngine.ExecuteTaskAsync**

Add `Dictionary<string, object>? formData = null` parameter. Before executing the task:
1. Get the current state's `FormFields` from the definition
2. If `FormFields` is not null/empty, validate `formData` via `IFormDataValidator`
3. If validation fails, log warning and return false
4. Store raw `formData` in `WorkflowStep.MetadataJson` (serialize as JSON)
5. Merge each form field into `WorkflowInstance.ContextJson` for downstream conditions

Register `IFormDataValidator` in `WorkflowModule.ConfigureServices`.

- [ ] **Step 4: Update controller**

The `ExecuteTask` endpoint body already accepts the full command — `FormData` is automatically deserialized from JSON.

- [ ] **Step 5: Populate FormFields in PendingTaskSummary**

In `WorkflowEngine.GetPendingTasksAsync`, read the definition's state config for each task's `StepName` and populate `FormFields` on the DTO.

- [ ] **Step 6: Populate FormData in WorkflowStepRecord**

In `WorkflowEngine.GetHistoryAsync`, deserialize `MetadataJson` and populate `FormData` on the step record DTO.

- [ ] **Step 7: Build + test + commit**

```bash
dotnet build boilerplateBE --nologo
dotnet test boilerplateBE/tests/Starter.Api.Tests/Starter.Api.Tests.csproj --nologo
git add boilerplateBE/
git commit -m "feat(workflow): step data collection — validate, store, merge form data into context"
```

---

## Task 5: ApprovalTask Entity + DelegationRule Entity + DB Changes

**Files:**
- Modify: `boilerplateBE/src/modules/Starter.Module.Workflow/Domain/Entities/ApprovalTask.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.Workflow/Domain/Entities/DelegationRule.cs`
- Modify: `boilerplateBE/src/modules/Starter.Module.Workflow/Domain/Events/WorkflowDomainEvents.cs`
- Modify: `boilerplateBE/src/modules/Starter.Module.Workflow/Infrastructure/Persistence/WorkflowDbContext.cs`
- Modify: `boilerplateBE/src/modules/Starter.Module.Workflow/Infrastructure/Persistence/Configurations/ApprovalTaskConfiguration.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.Workflow/Infrastructure/Persistence/Configurations/DelegationRuleConfiguration.cs`

- [ ] **Step 1: Add fields to ApprovalTask entity**

Add properties:
- `Guid? GroupId` — for parallel task grouping
- `DateTime? ReminderSentAt` — SLA reminder tracking
- `DateTime? EscalatedAt` — SLA escalation tracking
- `Guid? OriginalAssigneeUserId` — tracks original when delegated or escalated

Update the `Create` factory to accept optional `Guid? groupId = null`.

- [ ] **Step 2: Create DelegationRule entity**

```csharp
public sealed class DelegationRule : BaseEntity, ITenantEntity
{
    public Guid? TenantId { get; private set; }
    public Guid FromUserId { get; private set; }
    public Guid ToUserId { get; private set; }
    public DateTime StartDate { get; private set; }
    public DateTime EndDate { get; private set; }
    public bool IsActive { get; private set; } = true;

    // Factory + Deactivate method
}
```

- [ ] **Step 3: Add WorkflowTaskEscalatedEvent**

```csharp
public sealed record WorkflowTaskEscalatedEvent(
    Guid TaskId, Guid InstanceId, Guid OriginalAssigneeUserId,
    Guid NewAssigneeUserId, string StepName, string EntityType,
    Guid EntityId, Guid? TenantId) : DomainEventBase;
```

- [ ] **Step 4: Update WorkflowDbContext**

Add `DbSet<DelegationRule> DelegationRules`. Add tenant filter for DelegationRule.

- [ ] **Step 5: Update ApprovalTaskConfiguration**

Map new columns: `GroupId`, `ReminderSentAt`, `EscalatedAt`, `OriginalAssigneeUserId`. Add index on `(GroupId)` for parallel query.

- [ ] **Step 6: Create DelegationRuleConfiguration**

Table `workflow_delegation_rules`. Unique index on `(FromUserId, StartDate, EndDate)`. Tenant filter.

- [ ] **Step 7: Build + commit**

```bash
dotnet build boilerplateBE --nologo
git add boilerplateBE/src/modules/Starter.Module.Workflow/
git commit -m "feat(workflow): ApprovalTask + DelegationRule entity changes for Phase 2a"
```

---

## Task 6: Parallel Approvals in Engine (TDD)

**Files:**
- Modify: `boilerplateBE/src/modules/Starter.Module.Workflow/Infrastructure/Services/WorkflowEngine.cs`
- Modify: `boilerplateBE/tests/Starter.Api.Tests/Workflow/WorkflowEngineTests.cs`

- [ ] **Step 1: Write parallel approval tests**

1. `StartAsync_ParallelAllOf_CreatesMultipleTasks` — state with `parallel: { mode: "AllOf", assignees: [...] }` creates N tasks with same GroupId
2. `ExecuteTaskAsync_ParallelAllOf_LastApproval_Transitions` — approve all tasks → workflow advances
3. `ExecuteTaskAsync_ParallelAllOf_PartialApproval_Waits` — approve 2 of 3 → workflow stays
4. `ExecuteTaskAsync_ParallelAllOf_AnyReject_CancelsAllAndTransitions` — one rejects → all cancelled, workflow rejects
5. `ExecuteTaskAsync_ParallelAnyOf_FirstApproval_TransitionsAndCancelsRest` — first approval → advance, cancel remaining
6. `SingleAssignee_NoGroupId_WorksUnchanged` — backward compatibility

- [ ] **Step 2: Implement parallel task creation**

In `CreateApprovalTaskAsync` (or a new method): when the state has `Parallel`, iterate over `Parallel.Assignees`, resolve each, create one task per assignee with shared `GroupId = Guid.NewGuid()`.

- [ ] **Step 3: Implement parallel completion logic**

In `ExecuteTaskAsync`, after completing a task that has a `GroupId`:
- If AllOf + action is Reject: cancel all sibling pending tasks, transition to rejection state
- If AllOf + action is Approve: count completed approvals vs total in group. If all complete, transition.
- If AnyOf: cancel all sibling pending tasks, transition based on this action.

- [ ] **Step 4: Run tests — should pass**
- [ ] **Step 5: Commit**

```bash
git add boilerplateBE/src/modules/Starter.Module.Workflow/Infrastructure/Services/WorkflowEngine.cs \
       boilerplateBE/tests/Starter.Api.Tests/Workflow/WorkflowEngineTests.cs
git commit -m "feat(workflow): parallel approvals — AllOf + AnyOf modes (TDD)"
```

---

## Task 7: SLA Escalation Background Job (TDD)

**Files:**
- Create: `boilerplateBE/src/modules/Starter.Module.Workflow/Infrastructure/Services/SlaEscalationJob.cs`
- Create: `boilerplateBE/tests/Starter.Api.Tests/Workflow/SlaEscalationJobTests.cs`
- Modify: `boilerplateBE/src/modules/Starter.Module.Workflow/WorkflowModule.cs`

- [ ] **Step 1: Write tests**

Test the core escalation logic (not the BackgroundService timer — extract the processing method):

1. `ProcessOverdueTasks_TaskWithSlaReminder_SendsReminderAndMarks`
2. `ProcessOverdueTasks_TaskAlreadyReminded_DoesNotSendAgain`
3. `ProcessOverdueTasks_TaskPastEscalation_EscalatesAndCreatesNewTask`
4. `ProcessOverdueTasks_TaskAlreadyEscalated_DoesNotEscalateAgain`
5. `ProcessOverdueTasks_TaskWithNoSla_Skipped`
6. `ProcessOverdueTasks_CompletedTask_Skipped`

- [ ] **Step 2: Implement SlaEscalationJob**

```csharp
public sealed class SlaEscalationJob : BackgroundService
{
    // Timer runs every 15 minutes
    // Calls ProcessOverdueTasksAsync which:
    // 1. Loads all pending tasks with their instance + definition
    // 2. For each task, get the state config's SLA settings
    // 3. If reminder threshold breached + not already reminded: send reminder, mark
    // 4. If escalation threshold breached + not already escalated: cancel, create new task for fallback, notify
}
```

Inject `IServiceScopeFactory` (standard BackgroundService pattern for scoped services).
Use `IMessageDispatcher` for notifications, `AssigneeResolverService` for fallback resolution.

- [ ] **Step 3: Register in WorkflowModule**

```csharp
services.AddHostedService<SlaEscalationJob>();
```

- [ ] **Step 4: Seed SLA notification templates**

Add `workflow.sla-reminder` and `workflow.sla-escalated` templates in `SeedDataAsync`.

- [ ] **Step 5: Run tests + commit**

```bash
git add boilerplateBE/src/modules/Starter.Module.Workflow/Infrastructure/Services/SlaEscalationJob.cs \
       boilerplateBE/src/modules/Starter.Module.Workflow/WorkflowModule.cs \
       boilerplateBE/tests/Starter.Api.Tests/Workflow/SlaEscalationJobTests.cs
git commit -m "feat(workflow): SLA escalation background job — reminders + auto-reassignment (TDD)"
```

---

## Task 8: Delegation — Entity + Resolver + Commands

**Files:**
- Modify: `boilerplateBE/src/modules/Starter.Module.Workflow/Infrastructure/Services/AssigneeResolverService.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.Workflow/Application/Commands/CreateDelegation/CreateDelegationCommand.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.Workflow/Application/Commands/CreateDelegation/CreateDelegationCommandHandler.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.Workflow/Application/Commands/CancelDelegation/CancelDelegationCommand.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.Workflow/Application/Commands/CancelDelegation/CancelDelegationCommandHandler.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.Workflow/Application/Queries/GetDelegations/GetDelegationsQuery.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.Workflow/Application/Queries/GetDelegations/GetDelegationsQueryHandler.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.Workflow/Application/Queries/GetActiveDelegation/GetActiveDelegationQuery.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.Workflow/Application/Queries/GetActiveDelegation/GetActiveDelegationQueryHandler.cs`
- Modify: `boilerplateBE/src/modules/Starter.Module.Workflow/Controllers/WorkflowController.cs`
- Create: `boilerplateBE/tests/Starter.Api.Tests/Workflow/DelegationTests.cs`
- Modify: `boilerplateBE/src/modules/Starter.Module.Workflow/WorkflowModule.cs`

- [ ] **Step 1: Write delegation resolver tests**

1. `ResolveAsync_ActiveDelegation_SwapsAssignee`
2. `ResolveAsync_NoDelegation_KeepsOriginal`
3. `ResolveAsync_ExpiredDelegation_KeepsOriginal`
4. `ResolveAsync_FutureDelegation_KeepsOriginal`
5. `ResolveAsync_DeactivatedDelegation_KeepsOriginal`

- [ ] **Step 2: Update AssigneeResolverService**

Add delegation post-resolution step. The service needs `WorkflowDbContext` injected to query `DelegationRules`.

After resolving primary assignees:
```csharp
foreach (var userId in resolvedUserIds.ToList())
{
    var now = DateTime.UtcNow;
    var delegation = await workflowDbContext.DelegationRules
        .FirstOrDefaultAsync(d => d.FromUserId == userId
            && d.IsActive
            && d.StartDate <= now
            && d.EndDate >= now, ct);

    if (delegation is not null)
    {
        resolvedUserIds.Remove(userId);
        resolvedUserIds.Add(delegation.ToUserId);
        // Track for OriginalAssigneeUserId on ApprovalTask
    }
}
```

Return both resolved IDs and the original-to-delegate mapping.

- [ ] **Step 3: Create delegation commands + queries**

`CreateDelegationCommand(Guid ToUserId, DateTime StartDate, DateTime EndDate)`
`CancelDelegationCommand(Guid DelegationId)`
`GetDelegationsQuery` — returns current user's delegations
`GetActiveDelegationQuery` — returns current user's active delegation (if any)

- [ ] **Step 4: Add controller endpoints**

```csharp
[HttpPost("delegations")]
[HttpGet("delegations")]
[HttpDelete("delegations/{id}")]
[HttpGet("delegations/active")]
```

- [ ] **Step 5: Seed delegation notification templates**

Add `workflow.delegation-created` and `workflow.delegation-ended` in `SeedDataAsync`.

- [ ] **Step 6: Update GetPendingTasksAsync for delegation visibility**

Both delegate AND original assignee should see the task:
```csharp
.Where(t => t.AssigneeUserId == userId || t.OriginalAssigneeUserId == userId)
```

- [ ] **Step 7: Run tests + commit**

```bash
git add boilerplateBE/
git commit -m "feat(workflow): delegation — entity, resolver integration, CQRS commands, visibility"
```

---

## Task 9: Module Registration Updates

**Files:**
- Modify: `boilerplateBE/src/modules/Starter.Module.Workflow/WorkflowModule.cs`

- [ ] **Step 1: Register new services**

In `ConfigureServices`:
```csharp
services.AddScoped<IFormDataValidator, FormDataValidator>();
services.AddHostedService<SlaEscalationJob>();
```

- [ ] **Step 2: Seed new notification templates**

In `SeedDataAsync`, add 4 new templates:
- `workflow.sla-reminder`
- `workflow.sla-escalated`
- `workflow.delegation-created`
- `workflow.delegation-ended`

Each with appropriate variables, subject, body following the existing template pattern.

- [ ] **Step 3: Build full solution + run all tests**

```bash
dotnet build boilerplateBE --nologo
dotnet test boilerplateBE/tests/Starter.Api.Tests/Starter.Api.Tests.csproj --nologo
```

- [ ] **Step 4: Commit**

```bash
git add boilerplateBE/src/modules/Starter.Module.Workflow/WorkflowModule.cs
git commit -m "feat(workflow): register FormDataValidator + SlaEscalationJob + seed new templates"
```

---

## Task 10: Frontend — Types + API + Hooks

**Files:**
- Modify: `boilerplateFE/src/types/workflow.types.ts`
- Modify: `boilerplateFE/src/config/api.config.ts`
- Modify: `boilerplateFE/src/features/workflow/api/workflow.api.ts`
- Modify: `boilerplateFE/src/features/workflow/api/workflow.queries.ts`

- [ ] **Step 1: Update TypeScript types**

Add to `workflow.types.ts`:

```typescript
export interface FormFieldDefinition {
  name: string;
  label: string;
  type: 'text' | 'textarea' | 'number' | 'date' | 'select' | 'checkbox';
  required?: boolean;
  options?: SelectOption[];
  min?: number;
  max?: number;
  maxLength?: number;
  placeholder?: string;
  description?: string;
}

export interface SelectOption {
  value: string;
  label: string;
}

export interface DelegationRule {
  id: string;
  fromUserId: string;
  toUserId: string;
  toDisplayName?: string;
  startDate: string;
  endDate: string;
  isActive: boolean;
}

export interface CreateDelegationRequest {
  toUserId: string;
  startDate: string;
  endDate: string;
}
```

Update `PendingTaskSummary` with: `formFields`, `groupId`, `parallelTotal`, `parallelCompleted`, `isOverdue`, `hoursOverdue`, `isDelegated`, `delegatedFromDisplayName`.

Update `WorkflowStepRecord` with: `formData`.

Update `ExecuteTaskRequest` with: `formData?: Record<string, unknown>`.

- [ ] **Step 2: Add API endpoints + methods**

Add delegation endpoints to `api.config.ts`:
```typescript
DELEGATIONS: '/workflow/delegations',
DELEGATION_ACTIVE: '/workflow/delegations/active',
DELEGATION_DETAIL: (id: string) => `/workflow/delegations/${id}`,
```

Add API methods for delegations in `workflow.api.ts`.

- [ ] **Step 3: Add query hooks**

Add to `workflow.queries.ts`:
- `useDelegations()`
- `useActiveDelegation()`
- `useCreateDelegation()` mutation
- `useCancelDelegation()` mutation

- [ ] **Step 4: Build + commit**

```bash
cd boilerplateFE && npm run build
git add boilerplateFE/
git commit -m "feat(workflow): frontend types, API, hooks for forms, parallel, SLA, delegation"
```

---

## Task 11: Frontend — Dynamic Form Renderer + ApprovalDialog

**Files:**
- Create: `boilerplateFE/src/features/workflow/components/DynamicFormRenderer.tsx`
- Modify: `boilerplateFE/src/features/workflow/components/ApprovalDialog.tsx`

- [ ] **Step 1: Create DynamicFormRenderer**

Component that renders form fields based on `FormFieldDefinition[]`:

```tsx
interface DynamicFormRendererProps {
  fields: FormFieldDefinition[];
  values: Record<string, unknown>;
  onChange: (name: string, value: unknown) => void;
  errors?: Record<string, string>;
}
```

For each field type, render the appropriate shadcn/ui component:
- `text` → `<Input>`
- `textarea` → `<Textarea>`
- `number` → `<Input type="number">`
- `date` → `<Input type="date">` (or DatePicker if available)
- `select` → `<Select>` with `<SelectItem>` per option
- `checkbox` → `<Checkbox>` with label

Show validation errors per field. Mark required fields with asterisk.

- [ ] **Step 2: Update ApprovalDialog**

When `formFields` is present on the task:
1. Render `DynamicFormRenderer` above the comment field
2. Manage form state: `const [formData, setFormData] = useState<Record<string, unknown>>({});`
3. On action click: include `formData` in the `useExecuteTask` mutation call
4. When `formFields` is null/empty: behave as before (comment only)

- [ ] **Step 3: Build + commit**

```bash
cd boilerplateFE && npm run build
git add boilerplateFE/src/features/workflow/components/
git commit -m "feat(workflow): DynamicFormRenderer + ApprovalDialog form integration"
```

---

## Task 12: Frontend — Parallel + SLA + Delegation UI

**Files:**
- Modify: `boilerplateFE/src/features/workflow/components/WorkflowStepTimeline.tsx`
- Modify: `boilerplateFE/src/features/workflow/pages/WorkflowInboxPage.tsx`
- Modify: `boilerplateFE/src/features/workflow/pages/WorkflowInstanceDetailPage.tsx`
- Create: `boilerplateFE/src/features/workflow/components/DelegationDialog.tsx`
- Modify: `boilerplateFE/src/i18n/locales/en/translation.json`
- Modify: `boilerplateFE/src/i18n/locales/ar/translation.json`
- Modify: `boilerplateFE/src/i18n/locales/ku/translation.json`

- [ ] **Step 1: Inbox — overdue badges + delegation banner + delegate button**

In `WorkflowInboxPage.tsx`:
- Show red "Overdue" badge on tasks where `isOverdue === true` with `hoursOverdue`
- Show "Delegated from [Name]" badge on tasks where `isDelegated === true`
- Add "Delegate My Tasks" button in the page header
- When `useActiveDelegation()` returns a delegation: show amber banner "Your tasks are delegated to [Name] until [Date]" with Cancel button

- [ ] **Step 2: DelegationDialog**

New component:
- User picker (search by name — use existing user search API if available)
- Date range picker (start date, end date)
- Confirm button
- Calls `useCreateDelegation()` mutation

- [ ] **Step 3: Step timeline — parallel + SLA indicators**

In `WorkflowStepTimeline.tsx`:
- For parallel steps: show "2 of 3 approved" progress text
- For SLA: show overdue indicator (red dot) or "Reminder sent" text on the step
- For submitted form data: show form data values below the step comment

- [ ] **Step 4: Detail page — form data display + parallel status**

In `WorkflowInstanceDetailPage.tsx`:
- Show submitted form data in step history (key: value pairs under each step)
- Show parallel approval status on active parallel steps

- [ ] **Step 5: i18n — all 3 locales**

Add keys for:
- `workflow.delegation.*` (dialog, banner, badges)
- `workflow.sla.*` (overdue, reminder, escalated)
- `workflow.parallel.*` (progress text)
- `workflow.forms.*` (field labels, validation messages)

- [ ] **Step 6: Build + commit**

```bash
cd boilerplateFE && npm run build
git add boilerplateFE/
git commit -m "feat(workflow): frontend — parallel indicators, SLA badges, delegation UI, form data display"
```

---

## Task 13: Full Build + Test Verification

- [ ] **Step 1: Full backend build**

```bash
dotnet build boilerplateBE --nologo
```

- [ ] **Step 2: Full test suite**

```bash
dotnet test boilerplateBE/tests/Starter.Api.Tests/Starter.Api.Tests.csproj --nologo
```

- [ ] **Step 3: Frontend build**

```bash
cd boilerplateFE && npm run build
```

- [ ] **Step 4: Isolation test**

```bash
pwsh scripts/rename.ps1 -Name "_testNoWorkflow" -OutputDir "." -Modules "commentsActivity,communication" -IncludeMobile:$false
dotnet build _testNoWorkflow/_testNoWorkflow-BE --nologo
rm -rf _testNoWorkflow
```

- [ ] **Step 5: Commit if fixups needed**

---

## Spec Coverage Checklist

| Spec Section | Task(s) |
|---|---|
| FormFieldDefinition + SelectOption records | Task 1 |
| FormFields on WorkflowStateConfig | Task 1 |
| ConditionConfig extended with Logic + Conditions | Task 1 |
| Compound condition evaluation (recursive AND/OR) | Task 2 |
| FormDataValidator service | Task 3 |
| ExecuteTaskAsync with formData parameter | Task 1 (interface), Task 4 (implementation) |
| Form data validation in engine | Task 4 |
| Form data stored in Step.MetadataJson + merged into Context | Task 4 |
| PendingTaskSummary FormFields + FormData enrichment | Task 4 |
| ParallelConfig on WorkflowStateConfig | Task 1 |
| ApprovalTask.GroupId | Task 5 |
| Parallel task creation (multiple tasks per step) | Task 6 |
| AllOf completion logic | Task 6 |
| AnyOf completion logic | Task 6 |
| SlaConfig on WorkflowStateConfig | Task 1 |
| ApprovalTask SLA fields (ReminderSentAt, EscalatedAt, OriginalAssigneeUserId) | Task 5 |
| SlaEscalationJob background service | Task 7 |
| SLA notification templates | Task 7 + 9 |
| DelegationRule entity | Task 5 |
| DelegationRuleConfiguration | Task 5 |
| WorkflowTaskEscalatedEvent | Task 5 |
| AssigneeResolverService delegation swap | Task 8 |
| Delegation CQRS commands + queries | Task 8 |
| Delegation controller endpoints | Task 8 |
| Delegation notification templates | Task 8 + 9 |
| GetPendingTasksAsync delegation visibility | Task 8 |
| Frontend TypeScript types | Task 10 |
| Frontend API methods + hooks | Task 10 |
| DynamicFormRenderer component | Task 11 |
| ApprovalDialog form integration | Task 11 |
| Inbox overdue badges | Task 12 |
| Inbox delegation banner + button | Task 12 |
| DelegationDialog component | Task 12 |
| Step timeline parallel + SLA indicators | Task 12 |
| Detail page form data display | Task 12 |
| i18n (en, ar, ku) | Task 12 |
| Isolation test | Task 13 |
