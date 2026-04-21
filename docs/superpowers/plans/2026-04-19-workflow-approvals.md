# Workflow & Approvals — Phase 1 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a composable state-machine workflow engine that any module can use for multi-step processes with human approvals, automated actions, and configurable routing.

**Architecture:** New `Starter.Module.Workflow` with own `WorkflowDbContext`. Exposes `IWorkflowService` capability in `Starter.Abstractions`. Pluggable assignee resolution via `IAssigneeResolverProvider`. Side effects via existing capability contracts (`IMessageDispatcher`, `IActivityService`, `ICommentService`, `IWebhookPublisher`). All Null-Object safe.

**Tech Stack:** .NET 10 (MediatR, EF Core, MassTransit), React 19 (TypeScript, TanStack Query, Tailwind CSS 4)

**Spec:** `docs/superpowers/specs/2026-04-19-workflow-approvals-design.md`

---

## File Map

### Abstractions (Starter.Abstractions)
| File | Purpose |
|---|---|
| `Capabilities/IWorkflowService.cs` | Main capability contract (12 methods) |
| `Capabilities/IAssigneeResolverProvider.cs` | Pluggable assignee resolution |
| `Capabilities/WorkflowDtos.cs` | All DTOs: StatusSummary, PendingTask, StepRecord, InstanceSummary, DefinitionSummary/Detail |
| `Capabilities/WorkflowConfigRecords.cs` | Template seeding config: WorkflowTemplateConfig, WorkflowStateConfig, WorkflowTransitionConfig, AssigneeConfig, HookConfig |
| `Capabilities/WellKnownNotificationTypes.cs` | Add `WorkflowTaskAssigned` constant (existing file) |

### Infrastructure (Starter.Infrastructure)
| File | Purpose |
|---|---|
| `Capabilities/NullObjects/NullWorkflowService.cs` | Silent no-op fallback |
| `DependencyInjection.cs` | Add `TryAddScoped<IWorkflowService>` (modify) |

### Module (Starter.Module.Workflow)
| File | Purpose |
|---|---|
| `Domain/Entities/WorkflowDefinition.cs` | State machine definition entity |
| `Domain/Entities/WorkflowInstance.cs` | Running workflow entity |
| `Domain/Entities/WorkflowStep.cs` | Transition history entity |
| `Domain/Entities/ApprovalTask.cs` | Pending human task entity |
| `Domain/Enums/InstanceStatus.cs` | Active, Completed, Cancelled |
| `Domain/Enums/TaskStatus.cs` | Pending, Completed, Cancelled, Reassigned |
| `Domain/Enums/StepType.cs` | HumanTask, SystemAction, ConditionalGate |
| `Domain/Enums/StateType.cs` | Initial, HumanTask, SystemAction, ConditionalGate, Terminal |
| `Domain/Errors/WorkflowErrors.cs` | Error factory |
| `Domain/Events/WorkflowDomainEvents.cs` | All 6 domain events |
| `Infrastructure/Persistence/WorkflowDbContext.cs` | Module DbContext with tenant filters |
| `Infrastructure/Persistence/Configurations/*.cs` | EF entity configurations |
| `Infrastructure/Services/ConditionEvaluator.cs` | Field-value condition matching |
| `Infrastructure/Services/AssigneeResolverService.cs` | Resolves assignees via strategy providers |
| `Infrastructure/Services/BuiltInAssigneeProvider.cs` | SpecificUser, Role, EntityCreator strategies |
| `Infrastructure/Services/HookExecutor.cs` | Executes on-enter/on-exit hooks |
| `Infrastructure/Services/WorkflowEngine.cs` | Core engine — implements IWorkflowService |
| `Application/Commands/StartWorkflow/*.cs` | Command + Handler + Validator |
| `Application/Commands/ExecuteTask/*.cs` | Command + Handler |
| `Application/Commands/CancelWorkflow/*.cs` | Command + Handler |
| `Application/Commands/CloneDefinition/*.cs` | Command + Handler |
| `Application/Commands/UpdateDefinition/*.cs` | Command + Handler |
| `Application/Queries/GetPendingTasks/*.cs` | Query + Handler |
| `Application/Queries/GetWorkflowStatus/*.cs` | Query + Handler |
| `Application/Queries/GetWorkflowHistory/*.cs` | Query + Handler |
| `Application/Queries/GetWorkflowInstances/*.cs` | Query + Handler |
| `Application/Queries/GetWorkflowDefinitions/*.cs` | Query + Handler |
| `Application/Queries/GetWorkflowDefinitionDetail/*.cs` | Query + Handler |
| `Application/EventHandlers/*.cs` | RecordActivity, NotifyAssignee, PublishIntegrationEvents |
| `Application/DTOs/*.cs` | Controller-level DTOs |
| `Controllers/WorkflowDefinitionsController.cs` | Definition CRUD + clone |
| `Controllers/WorkflowInstancesController.cs` | Instance lifecycle + status + history |
| `Controllers/WorkflowTasksController.cs` | Task inbox + execute action |
| `Constants/WorkflowPermissions.cs` | Permission constants |
| `WorkflowModule.cs` | IModule implementation |
| `AssemblyInfo.cs` | InternalsVisibleTo for tests |
| `ROADMAP.md` | Phase 2 deferred items |

### Frontend (boilerplateFE)
| File | Purpose |
|---|---|
| `src/types/workflow.types.ts` | TypeScript types |
| `src/config/api.config.ts` | Add WORKFLOW endpoints (modify) |
| `src/features/workflow/api/workflow.api.ts` | API client methods |
| `src/features/workflow/api/workflow.queries.ts` | TanStack Query hooks |
| `src/features/workflow/api/index.ts` | Re-exports |
| `src/features/workflow/pages/WorkflowInboxPage.tsx` | Task inbox |
| `src/features/workflow/pages/WorkflowDefinitionsPage.tsx` | Admin list |
| `src/features/workflow/pages/WorkflowDefinitionDetailPage.tsx` | Admin detail/edit |
| `src/features/workflow/components/ApprovalDialog.tsx` | Approve/reject modal |
| `src/features/workflow/components/WorkflowStatusPanel.tsx` | Entity slot component |
| `src/features/workflow/components/WorkflowDashboardWidget.tsx` | Dashboard slot |
| `src/features/workflow/components/WorkflowStepTimeline.tsx` | Step history timeline |
| `src/features/workflow/index.ts` | Module registration + slots |
| `src/config/modules.config.ts` | Add workflow module (modify) |
| `src/routes/routes.tsx` | Add workflow routes (modify) |
| `src/components/layout/MainLayout/Sidebar.tsx` | Add inbox nav item (modify) |
| `src/i18n/locales/en/translation.json` | Add workflow i18n keys (modify) |
| `src/i18n/locales/ar/translation.json` | Arabic translations (modify) |
| `src/i18n/locales/ku/translation.json` | Kurdish translations (modify) |
| `src/lib/extensions/slot-map.ts` | Add workflow slot types (modify) |
| `src/constants/permissions.ts` | Add workflow permissions (modify) |

### Tests
| File | Purpose |
|---|---|
| `tests/Starter.Api.Tests/Workflow/ConditionEvaluatorTests.cs` | Condition matching |
| `tests/Starter.Api.Tests/Workflow/AssigneeResolverTests.cs` | Strategy resolution |
| `tests/Starter.Api.Tests/Workflow/WorkflowEngineTests.cs` | Core engine logic |
| `tests/Starter.Api.Tests/Workflow/HookExecutorTests.cs` | Side effect hooks |
| `tests/Starter.Api.Tests/Workflow/WorkflowModulePermissionsTests.cs` | Permission invariants |

### Scripts
| File | Purpose |
|---|---|
| `scripts/modules.json` | Add workflow entry (modify) |

---

## Task 1: Abstractions — IWorkflowService + DTOs + IAssigneeResolverProvider

**Files:**
- Create: `boilerplateBE/src/Starter.Abstractions/Capabilities/IWorkflowService.cs`
- Create: `boilerplateBE/src/Starter.Abstractions/Capabilities/WorkflowDtos.cs`
- Create: `boilerplateBE/src/Starter.Abstractions/Capabilities/WorkflowConfigRecords.cs`
- Create: `boilerplateBE/src/Starter.Abstractions/Capabilities/IAssigneeResolverProvider.cs`
- Modify: `boilerplateBE/src/Starter.Abstractions/Capabilities/WellKnownNotificationTypes.cs`

- [ ] **Step 1: Create IWorkflowService**

Create `boilerplateBE/src/Starter.Abstractions/Capabilities/IWorkflowService.cs` with the full interface from the spec — 12 methods across lifecycle, task actions, query (status, inbox, history, definitions), and template seeding.

- [ ] **Step 2: Create WorkflowDtos**

Create `boilerplateBE/src/Starter.Abstractions/Capabilities/WorkflowDtos.cs` with all 6 DTO records: `WorkflowStatusSummary`, `PendingTaskSummary`, `WorkflowStepRecord`, `WorkflowInstanceSummary`, `WorkflowDefinitionSummary`, `WorkflowDefinitionDetail`.

- [ ] **Step 3: Create WorkflowConfigRecords**

Create `boilerplateBE/src/Starter.Abstractions/Capabilities/WorkflowConfigRecords.cs` with config records used for template seeding and JSON deserialization:

```csharp
namespace Starter.Abstractions.Capabilities;

/// <summary>Config record for seeding workflow templates via IWorkflowService.SeedTemplateAsync.</summary>
public sealed record WorkflowTemplateConfig(
    string DisplayName,
    string? Description,
    List<WorkflowStateConfig> States,
    List<WorkflowTransitionConfig> Transitions);

public sealed record WorkflowStateConfig(
    string Name,
    string DisplayName,
    string Type, // "Initial", "HumanTask", "SystemAction", "ConditionalGate", "Terminal"
    AssigneeConfig? Assignee = null,
    List<string>? Actions = null,
    List<HookConfig>? OnEnter = null,
    List<HookConfig>? OnExit = null);

public sealed record WorkflowTransitionConfig(
    string From,
    string To,
    string Trigger,
    string Type = "Manual", // "Manual" or "Conditional"
    ConditionConfig? Condition = null);

public sealed record AssigneeConfig(
    string Strategy,
    Dictionary<string, object>? Parameters = null,
    AssigneeConfig? Fallback = null);

public sealed record HookConfig(
    string Type, // "notify", "activity", "webhook", "inAppNotify"
    string? Template = null,
    string? To = null,
    string? Event = null,
    string? Action = null);

public sealed record ConditionConfig(
    string Field,
    string Operator, // "equals", "notEquals", "greaterThan", "lessThan", etc.
    object Value);
```

- [ ] **Step 4: Create IAssigneeResolverProvider**

Create `boilerplateBE/src/Starter.Abstractions/Capabilities/IAssigneeResolverProvider.cs`:

```csharp
namespace Starter.Abstractions.Capabilities;

/// <summary>
/// Pluggable assignee resolution for workflow steps. Modules register providers
/// to support org-structure-aware strategies (e.g., "OrgManager", "DepartmentHead").
/// The Workflow module collects all providers via IEnumerable and routes by strategy name.
/// </summary>
public interface IAssigneeResolverProvider : ICapability
{
    IReadOnlyList<string> SupportedStrategies { get; }

    Task<IReadOnlyList<Guid>> ResolveAsync(
        string strategy,
        Dictionary<string, object> parameters,
        WorkflowAssigneeContext context,
        CancellationToken ct = default);
}

public sealed record WorkflowAssigneeContext(
    string EntityType,
    Guid EntityId,
    Guid? TenantId,
    Guid InitiatorUserId,
    string CurrentState);
```

- [ ] **Step 5: Add WorkflowTaskAssigned to WellKnownNotificationTypes**

In `WellKnownNotificationTypes.cs`, add:

```csharp
/// <summary>A workflow approval task was assigned to the user.</summary>
public const string WorkflowTaskAssigned = "WorkflowTaskAssigned";
```

- [ ] **Step 6: Build to verify**

Run: `dotnet build boilerplateBE/src/Starter.Abstractions/Starter.Abstractions.csproj --nologo`
Expected: 0 errors

- [ ] **Step 7: Commit**

```bash
git add boilerplateBE/src/Starter.Abstractions/Capabilities/
git commit -m "feat(workflow): add IWorkflowService + IAssigneeResolverProvider capability contracts"
```

---

## Task 2: NullWorkflowService + DI Registration

**Files:**
- Create: `boilerplateBE/src/Starter.Infrastructure/Capabilities/NullObjects/NullWorkflowService.cs`
- Modify: `boilerplateBE/src/Starter.Infrastructure/DependencyInjection.cs`

- [ ] **Step 1: Create NullWorkflowService**

Implement every `IWorkflowService` method as a silent no-op. Lists return empty, nullable returns null, booleans return false, Guids return `Guid.Empty`. All log at Debug level. Follow exact pattern of `NullMessageDispatcher` — primary constructor with `ILogger<NullWorkflowService>`.

- [ ] **Step 2: Register in DI**

In `DependencyInjection.cs`, add `services.TryAddScoped<IWorkflowService, NullWorkflowService>()` in the Null Object fallbacks section (alongside `IMessageDispatcher`, `ITemplateRegistrar`, etc.).

- [ ] **Step 3: Build + Commit**

```bash
dotnet build boilerplateBE/src/Starter.Infrastructure/Starter.Infrastructure.csproj --nologo
git add boilerplateBE/src/Starter.Infrastructure/
git commit -m "feat(workflow): add NullWorkflowService fallback + DI registration"
```

---

## Task 3: Domain Entities + Enums + Errors + Events

**Files:**
- Create: `boilerplateBE/src/modules/Starter.Module.Workflow/Domain/Enums/InstanceStatus.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.Workflow/Domain/Enums/TaskStatus.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.Workflow/Domain/Enums/StepType.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.Workflow/Domain/Enums/StateType.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.Workflow/Domain/Errors/WorkflowErrors.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.Workflow/Domain/Entities/WorkflowDefinition.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.Workflow/Domain/Entities/WorkflowInstance.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.Workflow/Domain/Entities/WorkflowStep.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.Workflow/Domain/Entities/ApprovalTask.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.Workflow/Domain/Events/WorkflowDomainEvents.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.Workflow/Starter.Module.Workflow.csproj`

- [ ] **Step 1: Create .csproj**

Create the module project file. Reference only `Starter.Abstractions.Web` (same as CommentsActivity). Include packages: `Microsoft.EntityFrameworkCore`, `MediatR`, `MassTransit.Abstractions`.

- [ ] **Step 2: Create Enums**

4 enum files:
- `InstanceStatus`: `Active = 0, Completed = 1, Cancelled = 2`
- `TaskStatus`: `Pending = 0, Completed = 1, Cancelled = 2, Reassigned = 3`
- `StepType`: `HumanTask = 0, SystemAction = 1, ConditionalGate = 2`
- `StateType`: `Initial = 0, HumanTask = 1, SystemAction = 2, ConditionalGate = 3, Terminal = 4`

- [ ] **Step 3: Create WorkflowErrors**

Static error factory following the pattern in `CommentErrors`:

```csharp
public static class WorkflowErrors
{
    public static Error DefinitionNotFound(string name) =>
        Error.NotFound("Workflow.DefinitionNotFound", $"Workflow definition '{name}' not found");
    public static Error InstanceNotFound(Guid id) =>
        Error.NotFound("Workflow.InstanceNotFound", $"Workflow instance '{id}' not found");
    public static Error TaskNotFound(Guid id) =>
        Error.NotFound("Workflow.TaskNotFound", $"Approval task '{id}' not found");
    public static Error InvalidTransition(string currentState, string action) =>
        Error.Validation("Workflow.InvalidTransition", $"Action '{action}' is not valid from state '{currentState}'");
    public static Error TaskNotAssignedToUser(Guid taskId, Guid userId) =>
        Error.Forbidden("Workflow.TaskNotAssigned", $"Task '{taskId}' is not assigned to user '{userId}'");
    public static Error InstanceNotActive(Guid id) =>
        Error.Validation("Workflow.InstanceNotActive", $"Workflow instance '{id}' is not active");
    public static Error DefinitionNotActive(string name) =>
        Error.Validation("Workflow.DefinitionNotActive", $"Workflow definition '{name}' is not active");
    public static Error CannotEditTemplate() =>
        Error.Validation("Workflow.CannotEditTemplate", "System templates cannot be edited directly. Clone it first.");
}
```

- [ ] **Step 4: Create WorkflowDefinition entity**

Follow the `Comment` entity pattern: `AggregateRoot`, `ITenantEntity`, private setters, factory method. Fields match the spec entity table. JSON state/transition data stored as `string` properties (`StatesJson`, `TransitionsJson`).

- [ ] **Step 5: Create WorkflowInstance entity**

Same pattern. FK to `WorkflowDefinition`. Include `ContextJson` for conditional gate data. Status management methods: `Complete()`, `Cancel(reason, userId)`, `TransitionTo(newState)`.

- [ ] **Step 6: Create WorkflowStep entity**

Value entity (inherits `BaseEntity`, not `AggregateRoot`). Records each transition in the workflow history.

- [ ] **Step 7: Create ApprovalTask entity**

Inherits `BaseEntity`, implements `ITenantEntity`. Methods: `Complete(action, comment, userId)`, `Cancel()`.

- [ ] **Step 8: Create Domain Events**

All 6 events as sealed records extending `DomainEventBase`:
- `WorkflowStartedEvent(Guid InstanceId, string EntityType, Guid EntityId, string DefinitionName, Guid InitiatorUserId, Guid? TenantId)`
- `WorkflowTransitionEvent(Guid InstanceId, string FromState, string ToState, string Action, Guid? ActorUserId, string EntityType, Guid EntityId, Guid? TenantId)`
- `WorkflowCompletedEvent(Guid InstanceId, string EntityType, Guid EntityId, string FinalState, Guid? TenantId)`
- `WorkflowCancelledEvent(Guid InstanceId, string Reason, Guid CancelledByUserId, Guid? TenantId)`
- `ApprovalTaskAssignedEvent(Guid TaskId, Guid InstanceId, Guid? AssigneeUserId, string? AssigneeRole, string StepName, string EntityType, Guid EntityId, Guid? TenantId)`
- `ApprovalTaskCompletedEvent(Guid TaskId, string Action, Guid ActorUserId, string? Comment, Guid? TenantId)`

- [ ] **Step 9: Build + Commit**

Add the new project to the solution file and to `Starter.Api.csproj` ProjectReferences.

```bash
dotnet sln boilerplateBE/Starter.sln add boilerplateBE/src/modules/Starter.Module.Workflow/Starter.Module.Workflow.csproj
# Add ProjectReference in Starter.Api.csproj
dotnet build boilerplateBE --nologo
git add boilerplateBE/
git commit -m "feat(workflow): domain entities, enums, errors, domain events"
```

---

## Task 4: WorkflowDbContext + Entity Configurations

**Files:**
- Create: `boilerplateBE/src/modules/Starter.Module.Workflow/Infrastructure/Persistence/WorkflowDbContext.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.Workflow/Infrastructure/Persistence/Configurations/WorkflowDefinitionConfiguration.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.Workflow/Infrastructure/Persistence/Configurations/WorkflowInstanceConfiguration.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.Workflow/Infrastructure/Persistence/Configurations/WorkflowStepConfiguration.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.Workflow/Infrastructure/Persistence/Configurations/ApprovalTaskConfiguration.cs`

- [ ] **Step 1: Create WorkflowDbContext**

Follow `CommentsActivityDbContext` pattern exactly:
- Inherit `DbContext, IModuleDbContext`
- Inject optional `ICurrentUserService` for tenant ID
- Declare `DbSet` properties for all 4 entities
- Apply configurations from assembly in `OnModelCreating`
- Add tenant query filters for `WorkflowDefinition` (null = system template, visible to all), `WorkflowInstance`, and `ApprovalTask`

- [ ] **Step 2: Create entity configurations**

4 configuration files following the existing EF convention. Key points:
- `WorkflowDefinition`: table `workflow_definitions`, unique index on `(TenantId, Name)`, `StatesJson` and `TransitionsJson` as text columns
- `WorkflowInstance`: table `workflow_instances`, FK to `WorkflowDefinition`, index on `(EntityType, EntityId)`, index on `(TenantId, Status)`
- `WorkflowStep`: table `workflow_steps`, FK to `WorkflowInstance`, index on `InstanceId`
- `ApprovalTask`: table `workflow_approval_tasks`, FK to `WorkflowInstance`, index on `(AssigneeUserId, Status)`, index on `(TenantId, Status)`

- [ ] **Step 3: Build + Commit**

```bash
dotnet build boilerplateBE --nologo
git add boilerplateBE/src/modules/Starter.Module.Workflow/Infrastructure/
git commit -m "feat(workflow): WorkflowDbContext with tenant filters + entity configurations"
```

---

## Task 5: ConditionEvaluator (TDD)

**Files:**
- Create: `boilerplateBE/src/modules/Starter.Module.Workflow/Infrastructure/Services/ConditionEvaluator.cs`
- Create: `boilerplateBE/tests/Starter.Api.Tests/Workflow/ConditionEvaluatorTests.cs`

- [ ] **Step 1: Write tests**

Test cases:
1. `Evaluate_Equals_MatchingValue_ReturnsTrue`
2. `Evaluate_Equals_NonMatchingValue_ReturnsFalse`
3. `Evaluate_GreaterThan_LargerValue_ReturnsTrue`
4. `Evaluate_GreaterThan_SmallerValue_ReturnsFalse`
5. `Evaluate_LessThan_Works`
6. `Evaluate_Contains_SubstringMatch_ReturnsTrue`
7. `Evaluate_In_ValueInList_ReturnsTrue`
8. `Evaluate_MissingField_ReturnsFalse`
9. `Evaluate_NullContext_ReturnsFalse`
10. `Evaluate_NumericStringComparison_WorksCorrectly` (JSON deserializes numbers as JsonElement)

The evaluator takes a `ConditionConfig` and a `Dictionary<string, object>?` context (from `WorkflowInstance.ContextJson` deserialized).

```csharp
public interface IConditionEvaluator
{
    bool Evaluate(ConditionConfig condition, Dictionary<string, object>? context);
}
```

- [ ] **Step 2: Run tests — should fail**

- [ ] **Step 3: Implement ConditionEvaluator**

```csharp
internal sealed class ConditionEvaluator : IConditionEvaluator
{
    public bool Evaluate(ConditionConfig condition, Dictionary<string, object>? context)
    {
        if (context is null || !context.TryGetValue(condition.Field, out var rawValue))
            return false;

        return condition.Operator.ToLowerInvariant() switch
        {
            "equals" => CompareEquals(rawValue, condition.Value),
            "notequals" => !CompareEquals(rawValue, condition.Value),
            "greaterthan" => CompareNumeric(rawValue, condition.Value) > 0,
            "lessthan" => CompareNumeric(rawValue, condition.Value) < 0,
            "greaterthanorequal" => CompareNumeric(rawValue, condition.Value) >= 0,
            "lessthanorequal" => CompareNumeric(rawValue, condition.Value) <= 0,
            "contains" => ToString(rawValue).Contains(ToString(condition.Value), StringComparison.OrdinalIgnoreCase),
            "in" => EvaluateIn(rawValue, condition.Value),
            _ => false,
        };
    }
    // Helper methods for type coercion (JsonElement → double/string)
}
```

Handle `JsonElement` values (since `Dictionary<string, object>` deserialized from JSON stores `JsonElement` not primitive types).

- [ ] **Step 4: Run tests — should pass**

- [ ] **Step 5: Commit**

```bash
git add boilerplateBE/src/modules/Starter.Module.Workflow/Infrastructure/Services/ConditionEvaluator.cs \
       boilerplateBE/tests/Starter.Api.Tests/Workflow/ConditionEvaluatorTests.cs
git commit -m "feat(workflow): ConditionEvaluator with field-value matching (TDD, 10 tests)"
```

---

## Task 6: AssigneeResolverService + Built-in Strategies (TDD)

**Files:**
- Create: `boilerplateBE/src/modules/Starter.Module.Workflow/Infrastructure/Services/AssigneeResolverService.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.Workflow/Infrastructure/Services/BuiltInAssigneeProvider.cs`
- Create: `boilerplateBE/tests/Starter.Api.Tests/Workflow/AssigneeResolverTests.cs`

- [ ] **Step 1: Write tests**

Test cases:
1. `Resolve_SpecificUser_ReturnsExactUserId`
2. `Resolve_Role_QueriesUsersByRole_ReturnsUserIds` (mock `IRoleReader` or `IUserReader`)
3. `Resolve_EntityCreator_ReturnsInitiatorUserId`
4. `Resolve_UnknownStrategy_FallsBackToFallbackStrategy`
5. `Resolve_BothStrategiesFail_ReturnsTenantAdmins`
6. `Resolve_StrategyFromExternalProvider_RoutesCorrectly`

- [ ] **Step 2: Implement BuiltInAssigneeProvider**

```csharp
internal sealed class BuiltInAssigneeProvider(IRoleReader roleReader) : IAssigneeResolverProvider
{
    public IReadOnlyList<string> SupportedStrategies => ["SpecificUser", "Role", "EntityCreator"];

    public async Task<IReadOnlyList<Guid>> ResolveAsync(
        string strategy, Dictionary<string, object> parameters,
        WorkflowAssigneeContext context, CancellationToken ct = default)
    {
        return strategy switch
        {
            "SpecificUser" => [Guid.Parse(parameters["userId"].ToString()!)],
            "Role" => await ResolveByRole(parameters["roleName"].ToString()!, context.TenantId, ct),
            "EntityCreator" => [context.InitiatorUserId],
            _ => [],
        };
    }
}
```

- [ ] **Step 3: Implement AssigneeResolverService**

Collects all `IEnumerable<IAssigneeResolverProvider>`, routes by strategy name, handles fallback chain:

```csharp
internal sealed class AssigneeResolverService(
    IEnumerable<IAssigneeResolverProvider> providers,
    ILogger<AssigneeResolverService> logger)
{
    public async Task<IReadOnlyList<Guid>> ResolveAsync(
        AssigneeConfig config, WorkflowAssigneeContext context, CancellationToken ct)
    {
        var result = await TryResolveStrategy(config.Strategy, config.Parameters ?? [], context, ct);
        if (result.Count > 0) return result;

        if (config.Fallback is not null)
        {
            result = await TryResolveStrategy(config.Fallback.Strategy, config.Fallback.Parameters ?? [], context, ct);
            if (result.Count > 0) return result;
        }

        // Last resort: tenant admins
        logger.LogWarning("All assignee strategies failed for step in entity {EntityType}/{EntityId}, assigning to tenant admins",
            context.EntityType, context.EntityId);
        return await ResolveTenantAdmins(context.TenantId, ct);
    }
}
```

- [ ] **Step 4: Run tests — should pass**
- [ ] **Step 5: Commit**

---

## Task 7: HookExecutor (TDD)

**Files:**
- Create: `boilerplateBE/src/modules/Starter.Module.Workflow/Infrastructure/Services/HookExecutor.cs`
- Create: `boilerplateBE/tests/Starter.Api.Tests/Workflow/HookExecutorTests.cs`

- [ ] **Step 1: Write tests**

Test cases:
1. `Execute_NotifyHook_CallsMessageDispatcher`
2. `Execute_ActivityHook_CallsActivityService`
3. `Execute_WebhookHook_CallsWebhookPublisher`
4. `Execute_InAppNotifyHook_CallsNotificationService`
5. `Execute_UnknownHookType_LogsWarningAndSkips`
6. `Execute_DispatcherThrows_ContinuesToNextHook` (error isolation)
7. `Execute_NullObjectCapabilities_CompletesWithoutError`

- [ ] **Step 2: Implement HookExecutor**

```csharp
internal sealed class HookExecutor(
    IMessageDispatcher messageDispatcher,
    IActivityService activityService,
    IWebhookPublisher webhookPublisher,
    INotificationServiceCapability notificationService,
    IUserReader userReader,
    ILogger<HookExecutor> logger)
{
    public async Task ExecuteHooksAsync(
        List<HookConfig>? hooks,
        WorkflowInstance instance,
        WorkflowDefinition definition,
        string state,
        Guid? actorUserId,
        CancellationToken ct)
    {
        if (hooks is null || hooks.Count == 0) return;

        foreach (var hook in hooks)
        {
            try
            {
                await ExecuteHookAsync(hook, instance, definition, state, actorUserId, ct);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Hook {HookType} failed for workflow {InstanceId} state {State}",
                    hook.Type, instance.Id, state);
            }
        }
    }
}
```

Each hook type routes to its capability contract. The `To` field in notify hooks resolves to: `"assignee"` → current task assignee, `"initiator"` → workflow starter, or a specific role.

- [ ] **Step 3: Run tests — should pass**
- [ ] **Step 4: Commit**

---

## Task 8: WorkflowEngine — Core Service (TDD)

**Files:**
- Create: `boilerplateBE/src/modules/Starter.Module.Workflow/Infrastructure/Services/WorkflowEngine.cs`
- Create: `boilerplateBE/tests/Starter.Api.Tests/Workflow/WorkflowEngineTests.cs`

This is the largest and most critical file. It implements `IWorkflowService`.

- [ ] **Step 1: Write engine tests**

Test cases for lifecycle:
1. `StartAsync_ValidDefinition_CreatesInstanceAndFirstTask`
2. `StartAsync_InitialStateIsHumanTask_CreatesApprovalTask`
3. `StartAsync_InitialStateIsSystemAction_AutoTransitions`
4. `StartAsync_InactiveDefinition_ReturnsError`
5. `ExecuteTaskAsync_ValidApproval_TransitionsState`
6. `ExecuteTaskAsync_Reject_TransitionsToRejectedState`
7. `ExecuteTaskAsync_ReturnForRevision_TransitionsBackToDraft`
8. `ExecuteTaskAsync_InvalidAction_ReturnsError`
9. `ExecuteTaskAsync_NotAssignedToUser_ReturnsError`
10. `ExecuteTaskAsync_ConditionalGateAfterApproval_EvaluatesAndBranches`
11. `ExecuteTaskAsync_WithComment_SavesComment`
12. `CancelAsync_ActiveInstance_CancelsAndCancelsTasks`

Test cases for queries:
13. `GetStatusAsync_ExistingEntity_ReturnsStatus`
14. `GetStatusAsync_NoWorkflow_ReturnsNull`
15. `IsInStateAsync_CorrectState_ReturnsTrue`
16. `GetPendingTasksAsync_ReturnsOnlyUserTasks`
17. `GetPendingTaskCountAsync_ReturnsCorrectCount`
18. `GetHistoryAsync_ReturnsOrderedSteps`
19. `SeedTemplateAsync_NewTemplate_CreatesDefinition`
20. `SeedTemplateAsync_ExistingTemplate_Skips`

- [ ] **Step 2: Implement WorkflowEngine**

The engine is the core — it coordinates:
- Definition lookup and validation
- Instance creation with initial state
- State transition logic (find valid transition for action from current state)
- Conditional gate evaluation (via `IConditionEvaluator`)
- Assignee resolution (via `AssigneeResolverService`)
- Approval task creation/completion
- Hook execution (via `HookExecutor`)
- Comment saving (via `ICommentService`)
- Activity recording (via `IActivityService`)
- Domain event raising
- All query methods (status, inbox, history, definitions)

Key design: the engine uses `WorkflowDbContext` directly (not through MediatR) for the `IWorkflowService` implementation. CQRS commands/queries (Task 9-10) wrap these for the API layer.

- [ ] **Step 3: Run tests — should pass**
- [ ] **Step 4: Commit**

---

## Task 9: CQRS Commands (StartWorkflow, ExecuteTask, CancelWorkflow)

**Files:**
- Create: `boilerplateBE/src/modules/Starter.Module.Workflow/Application/Commands/StartWorkflow/StartWorkflowCommand.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.Workflow/Application/Commands/StartWorkflow/StartWorkflowCommandHandler.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.Workflow/Application/Commands/StartWorkflow/StartWorkflowCommandValidator.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.Workflow/Application/Commands/ExecuteTask/ExecuteTaskCommand.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.Workflow/Application/Commands/ExecuteTask/ExecuteTaskCommandHandler.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.Workflow/Application/Commands/CancelWorkflow/CancelWorkflowCommand.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.Workflow/Application/Commands/CancelWorkflow/CancelWorkflowCommandHandler.cs`

Follow the existing `AddCommentCommand` / `AddCommentCommandHandler` pattern exactly:
- Command is `sealed record : IRequest<Result<T>>`
- Handler is `internal sealed class` with primary constructor injecting `IWorkflowService` + `ICurrentUserService`
- Handler delegates to `IWorkflowService` methods, wraps results in `Result<T>`
- Validator uses FluentValidation `AbstractValidator<T>`

- [ ] **Step 1: Create StartWorkflowCommand + Handler + Validator**

```csharp
public sealed record StartWorkflowCommand(
    string EntityType, Guid EntityId, string DefinitionName,
    Dictionary<string, object>? Context = null) : IRequest<Result<Guid>>;
```

Handler resolves `currentUser.UserId` and `currentUser.TenantId`, calls `workflowService.StartAsync(...)`.

- [ ] **Step 2: Create ExecuteTaskCommand + Handler**

```csharp
public sealed record ExecuteTaskCommand(
    Guid TaskId, string Action, string? Comment = null) : IRequest<Result<bool>>;
```

- [ ] **Step 3: Create CancelWorkflowCommand + Handler**

```csharp
public sealed record CancelWorkflowCommand(
    Guid InstanceId, string? Reason = null) : IRequest<Result>;
```

- [ ] **Step 4: Build + Commit**

---

## Task 10: CQRS Commands (CloneDefinition, UpdateDefinition) + Queries

**Files:**
- Create: `boilerplateBE/src/modules/Starter.Module.Workflow/Application/Commands/CloneDefinition/*.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.Workflow/Application/Commands/UpdateDefinition/*.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.Workflow/Application/Queries/GetPendingTasks/*.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.Workflow/Application/Queries/GetWorkflowStatus/*.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.Workflow/Application/Queries/GetWorkflowHistory/*.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.Workflow/Application/Queries/GetWorkflowInstances/*.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.Workflow/Application/Queries/GetWorkflowDefinitions/*.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.Workflow/Application/Queries/GetWorkflowDefinitionDetail/*.cs`

- [ ] **Step 1: CloneDefinition command**

Clones a template definition for the tenant. Sets `IsTemplate = false`, `SourceDefinitionId = original.Id`, `TenantId = currentUser.TenantId`.

- [ ] **Step 2: UpdateDefinition command**

Updates a non-template definition's assignees, hooks, display name, description. Increments `Version`. Rejects if `IsTemplate = true` (must clone first).

- [ ] **Step 3: All 6 queries**

Each query follows the `GetNotificationPreferencesQuery` pattern: `sealed record : IRequest<Result<T>>`, handler queries `WorkflowDbContext` or delegates to `IWorkflowService`.

- [ ] **Step 4: Build + Commit**

---

## Task 11: Event Handlers + Integration Events

**Files:**
- Create: `boilerplateBE/src/modules/Starter.Module.Workflow/Application/EventHandlers/RecordWorkflowActivityHandler.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.Workflow/Application/EventHandlers/NotifyTaskAssigneeHandler.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.Workflow/Application/EventHandlers/PublishWorkflowIntegrationEvents.cs`

- [ ] **Step 1: RecordWorkflowActivityHandler**

Handles `WorkflowTransitionEvent` → calls `IActivityService.RecordAsync()` with action `"workflow_transition"` and metadata `{ fromState, toState, action, actorDisplayName }`.

- [ ] **Step 2: NotifyTaskAssigneeHandler**

Handles `ApprovalTaskAssignedEvent` → calls `INotificationServiceCapability.CreateAsync()` for in-app notification AND `IMessageDispatcher.SendAsync("workflow.task-assigned", ...)` for email/channel notification. Checks `INotificationPreferenceReader.IsEmailEnabledAsync()` before dispatching.

- [ ] **Step 3: PublishWorkflowIntegrationEvents**

Handles domain events → publishes corresponding MassTransit integration events (for external consumers, AI module, etc.).

- [ ] **Step 4: Build + Commit**

---

## Task 12: Controllers

**Files:**
- Create: `boilerplateBE/src/modules/Starter.Module.Workflow/Controllers/WorkflowDefinitionsController.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.Workflow/Controllers/WorkflowInstancesController.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.Workflow/Controllers/WorkflowTasksController.cs`

Follow the `CommentsActivityController` pattern exactly: inherit `BaseApiController(mediator)`, use `[Authorize(Policy = WorkflowPermissions.X)]`, send MediatR commands/queries, return via `HandleResult()`.

- [ ] **Step 1: WorkflowDefinitionsController**

Endpoints:
- `GET /api/v1/workflow/definitions` — list definitions (GetWorkflowDefinitions query)
- `GET /api/v1/workflow/definitions/{id}` — get definition detail
- `POST /api/v1/workflow/definitions/{id}/clone` — clone a template
- `PUT /api/v1/workflow/definitions/{id}` — update a custom definition
- `PATCH /api/v1/workflow/definitions/{id}/toggle` — activate/deactivate

- [ ] **Step 2: WorkflowInstancesController**

Endpoints:
- `POST /api/v1/workflow/instances` — start a workflow
- `GET /api/v1/workflow/instances` — list instances (filterable)
- `GET /api/v1/workflow/instances/status` — get status for entity (query params: entityType, entityId)
- `GET /api/v1/workflow/instances/{id}/history` — get step history
- `POST /api/v1/workflow/instances/{id}/cancel` — cancel

- [ ] **Step 3: WorkflowTasksController**

Endpoints:
- `GET /api/v1/workflow/tasks` — pending tasks for current user (inbox)
- `GET /api/v1/workflow/tasks/count` — pending count (for badge)
- `POST /api/v1/workflow/tasks/{id}/execute` — execute action (approve/reject/return)

- [ ] **Step 4: Build + Commit**

---

## Task 13: WorkflowModule + Permissions + modules.json + Template Seeding

**Files:**
- Create: `boilerplateBE/src/modules/Starter.Module.Workflow/Constants/WorkflowPermissions.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.Workflow/WorkflowModule.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.Workflow/AssemblyInfo.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.Workflow/ROADMAP.md`
- Modify: `scripts/modules.json`

- [ ] **Step 1: WorkflowPermissions**

```csharp
public static class WorkflowPermissions
{
    public const string View = "Workflows.View";
    public const string ManageDefinitions = "Workflows.ManageDefinitions";
    public const string Start = "Workflows.Start";
    public const string ActOnTask = "Workflows.ActOnTask";
    public const string Cancel = "Workflows.Cancel";
    public const string ViewAllTasks = "Workflows.ViewAllTasks";
}
```

- [ ] **Step 2: WorkflowModule**

Full `IModule` implementation:
- `ConfigureServices`: Register `WorkflowDbContext`, `WorkflowEngine` as `IWorkflowService`, `ConditionEvaluator`, `AssigneeResolverService`, `BuiltInAssigneeProvider` as `IAssigneeResolverProvider`, `HookExecutor`, health check
- `GetPermissions()`: 6 permissions
- `GetDefaultRolePermissions()`: SuperAdmin (all 6), Admin (all 6), User (View + Start + ActOnTask)
- `MigrateAsync`: migrate `WorkflowDbContext`
- `SeedDataAsync`: register 4 notification templates via `ITemplateRegistrar` + register 4 events

- [ ] **Step 3: AssemblyInfo + ROADMAP**

`AssemblyInfo.cs`: `[InternalsVisibleTo("Starter.Api.Tests")]`

`ROADMAP.md`: Document Phase 2 deferred items (step data collection, visual designer, SLA tracking, delegation, parallel approvals, AI integration, analytics).

- [ ] **Step 4: modules.json**

Add the `workflow` entry per the spec.

- [ ] **Step 5: Build full solution + run all tests**

```bash
dotnet build boilerplateBE --nologo
dotnet test boilerplateBE/tests/Starter.Api.Tests/Starter.Api.Tests.csproj --nologo
```

- [ ] **Step 6: Commit**

---

## Task 14: Frontend — Types + API Config + API Client + Query Hooks

**Files:**
- Create: `boilerplateFE/src/types/workflow.types.ts`
- Modify: `boilerplateFE/src/config/api.config.ts`
- Create: `boilerplateFE/src/features/workflow/api/workflow.api.ts`
- Create: `boilerplateFE/src/features/workflow/api/workflow.queries.ts`
- Create: `boilerplateFE/src/features/workflow/api/index.ts`

- [ ] **Step 1: TypeScript types**

Mirror the backend DTOs: `WorkflowDefinitionSummary`, `WorkflowDefinitionDetail`, `WorkflowStatusSummary`, `PendingTaskSummary`, `WorkflowStepRecord`, `WorkflowInstanceSummary`. Plus request types for commands.

- [ ] **Step 2: API endpoints config**

Add `WORKFLOW` section to `API_ENDPOINTS` in `api.config.ts`:

```typescript
WORKFLOW: {
    DEFINITIONS: '/workflow/definitions',
    DEFINITION_DETAIL: (id: string) => `/workflow/definitions/${id}`,
    DEFINITION_CLONE: (id: string) => `/workflow/definitions/${id}/clone`,
    DEFINITION_TOGGLE: (id: string) => `/workflow/definitions/${id}/toggle`,
    INSTANCES: '/workflow/instances',
    INSTANCE_STATUS: '/workflow/instances/status',
    INSTANCE_HISTORY: (id: string) => `/workflow/instances/${id}/history`,
    INSTANCE_CANCEL: (id: string) => `/workflow/instances/${id}/cancel`,
    TASKS: '/workflow/tasks',
    TASKS_COUNT: '/workflow/tasks/count',
    TASK_EXECUTE: (id: string) => `/workflow/tasks/${id}/execute`,
},
```

- [ ] **Step 3: API client**

`workflow.api.ts` with methods for all endpoints. Follow `commentsActivityApi` pattern.

- [ ] **Step 4: Query hooks**

`workflow.queries.ts` with:
- `useWorkflowDefinitions()`, `useWorkflowDefinition(id)`
- `usePendingTasks()`, `usePendingTaskCount()`
- `useWorkflowStatus(entityType, entityId)`
- `useWorkflowHistory(instanceId)`
- `useWorkflowInstances(params)`
- `useStartWorkflow()`, `useExecuteTask()`, `useCancelWorkflow()`
- `useCloneDefinition()`, `useUpdateDefinition()`

- [ ] **Step 5: Build + Commit**

---

## Task 15: Frontend — Task Inbox Page + Dashboard Widget

**Files:**
- Create: `boilerplateFE/src/features/workflow/pages/WorkflowInboxPage.tsx`
- Create: `boilerplateFE/src/features/workflow/components/WorkflowDashboardWidget.tsx`

- [ ] **Step 1: WorkflowInboxPage**

Filterable table with columns: Entity, Workflow, Step, Assigned, Due, Actions. Uses `usePendingTasks()` hook. Each row has Approve/Reject quick-action buttons that open `ApprovalDialog`. Uses `PageHeader`, `EmptyState`, `Pagination` from common components. Permission-gated with `Workflows.View`.

- [ ] **Step 2: WorkflowDashboardWidget**

Dashboard slot component showing top 5 pending tasks with "View All →" link to inbox. Uses `usePendingTasks({ pageSize: 5 })` and `usePendingTaskCount()`. Shows badge with count.

- [ ] **Step 3: Build + Commit**

---

## Task 16: Frontend — Workflow Admin Pages

**Files:**
- Create: `boilerplateFE/src/features/workflow/pages/WorkflowDefinitionsPage.tsx`
- Create: `boilerplateFE/src/features/workflow/pages/WorkflowDefinitionDetailPage.tsx`

- [ ] **Step 1: WorkflowDefinitionsPage**

List page with table: Name, Entity Type, Steps, Source (badge), Status, Actions (Clone/Edit/View/Deactivate). Permission-gated with `Workflows.ManageDefinitions`. Clone action opens a confirm dialog, then calls `useCloneDefinition()`.

- [ ] **Step 2: WorkflowDefinitionDetailPage**

Detail page showing:
- Definition header (name, entity type, version, source badge)
- Ordered step list with per-step assignee config display
- Per-step hook list
- For custom definitions: inline editing of assignees and hooks via form fields
- System templates: read-only with "Clone to customize" CTA

Uses `useWorkflowDefinition(id)`, `useUpdateDefinition()`, `useBackNavigation()`.

- [ ] **Step 3: Build + Commit**

---

## Task 17: Frontend — ApprovalDialog + WorkflowStatusPanel + Slot Registration

**Files:**
- Create: `boilerplateFE/src/features/workflow/components/ApprovalDialog.tsx`
- Create: `boilerplateFE/src/features/workflow/components/WorkflowStatusPanel.tsx`
- Create: `boilerplateFE/src/features/workflow/components/WorkflowStepTimeline.tsx`
- Create: `boilerplateFE/src/features/workflow/index.ts`
- Modify: `boilerplateFE/src/config/modules.config.ts`
- Modify: `boilerplateFE/src/routes/routes.tsx`
- Modify: `boilerplateFE/src/components/layout/MainLayout/Sidebar.tsx`
- Modify: `boilerplateFE/src/lib/extensions/slot-map.ts`
- Modify: `boilerplateFE/src/constants/permissions.ts`
- Modify: `boilerplateFE/src/i18n/locales/en/translation.json`
- Modify: `boilerplateFE/src/i18n/locales/ar/translation.json`
- Modify: `boilerplateFE/src/i18n/locales/ku/translation.json`

- [ ] **Step 1: ApprovalDialog**

Modal component receiving `taskId`, `definitionName`, `entityRef`, and available `actions` array. Shows action buttons (Approve/Reject/Return) + optional comment textarea + note about comment appearing in timeline. Calls `useExecuteTask()` on action.

- [ ] **Step 2: WorkflowStepTimeline**

Renders workflow step history as a vertical timeline with milestone dots. Completed steps: green dot. Current step: orange dot with "waiting" indicator. Future steps: grey dot. Each step shows actor, action, timestamp, comment (if any). Visually distinct from comment cards (per brainstorming Option C).

- [ ] **Step 3: WorkflowStatusPanel**

Entity slot component. Uses `useWorkflowStatus(entityType, entityId)`. Shows:
- Current state badge
- Approve/Reject buttons (if user has a pending task for this entity)
- Step timeline via `WorkflowStepTimeline`
- Opens `ApprovalDialog` on button click

- [ ] **Step 4: Module registration (index.ts)**

```typescript
export const workflowModule = {
  name: 'workflow',
  register(): void {
    registerSlot('entity-detail-workflow', {
      id: 'workflow.entity-status',
      module: 'workflow',
      order: 5,
      permission: 'Workflows.View',
      component: WorkflowStatusPanel,
    });
    registerSlot('dashboard-cards', {
      id: 'workflow.pending-tasks',
      module: 'workflow',
      order: 15,
      permission: 'Workflows.View',
      component: WorkflowDashboardWidget,
    });
  },
};
```

- [ ] **Step 5: Wire up modules.config.ts, routes, sidebar, slot-map, permissions, i18n**

- `modules.config.ts`: Add `workflow: true` to `activeModules`, import + add to `enabledModules`
- `routes.tsx`: Lazy-load `WorkflowInboxPage`, `WorkflowDefinitionsPage`, `WorkflowDefinitionDetailPage` with `activeModules.workflow` guard
- `Sidebar.tsx`: Add "Task Inbox" nav item with badge count (uses `usePendingTaskCount()`) under a "Workflows" section
- `slot-map.ts`: Add `'entity-detail-workflow'` slot type with props `{ entityType: string; entityId: string }`
- `permissions.ts`: Add workflow permission constants mirroring backend
- All 3 locale files: Add `workflow.*` i18n keys for inbox, admin, dialog, status panel

- [ ] **Step 6: FE build**

```bash
cd boilerplateFE && npm run build
```

- [ ] **Step 7: Commit**

---

## Task 18: Module Permissions Test + Full Build + Isolation Test

**Files:**
- Create: `boilerplateBE/tests/Starter.Api.Tests/Workflow/WorkflowModulePermissionsTests.cs`

- [ ] **Step 1: Permission invariant tests**

Same pattern as `CommunicationModulePermissionsTests`:
- `GetPermissions_ReturnsAllSixPermissions`
- `DefaultRolePermissions_SuperAdmin_GetsAllSix`
- `DefaultRolePermissions_Admin_GetsAllSix`
- `DefaultRolePermissions_User_GetsViewStartActOnTask`
- `Module_DeclaresNoHardDependencies`

- [ ] **Step 2: Full backend build + test**

```bash
dotnet build boilerplateBE --nologo
dotnet test boilerplateBE/tests/Starter.Api.Tests/Starter.Api.Tests.csproj --nologo
```

- [ ] **Step 3: Full frontend build**

```bash
cd boilerplateFE && npm run build
```

- [ ] **Step 4: Isolation test — without Workflow module**

```bash
pwsh scripts/rename.ps1 -Name "_testNoWorkflow" -OutputDir "." -Modules "commentsActivity,communication" -IncludeMobile:$false
dotnet build _testNoWorkflow/_testNoWorkflow-BE --nologo
# Should succeed — NullWorkflowService handles absence
rm -rf _testNoWorkflow
```

- [ ] **Step 5: Commit test file**

```bash
git add boilerplateBE/tests/Starter.Api.Tests/Workflow/
git commit -m "test(workflow): permission invariant tests + build verification"
```

---

## Spec Coverage Checklist

| Spec Section | Task(s) |
|---|---|
| IWorkflowService capability contract | Task 1 |
| IAssigneeResolverProvider | Task 1, 6 |
| NullWorkflowService + DI | Task 2 |
| Domain entities (4) + enums + errors + events | Task 3 |
| WorkflowDbContext + tenant filters | Task 4 |
| Condition evaluator | Task 5 |
| Assignee resolution (built-in + pluggable) | Task 6 |
| Hook executor (notify, activity, webhook, inApp) | Task 7 |
| WorkflowEngine (core logic) | Task 8 |
| CQRS Commands (5) | Tasks 9-10 |
| CQRS Queries (6) | Task 10 |
| Event handlers (activity, notifications, integration) | Task 11 |
| Controllers (3) | Task 12 |
| WorkflowModule + permissions + template seeding | Task 13 |
| modules.json registration | Task 13 |
| Frontend types + API + hooks | Task 14 |
| Task Inbox page + dashboard widget | Task 15 |
| Workflow Admin pages (list + detail) | Task 16 |
| ApprovalDialog + WorkflowStatusPanel + slot registration | Task 17 |
| i18n (en, ar, ku) + routes + sidebar + permissions | Task 17 |
| Permission invariant tests + isolation test | Task 18 |
| ROADMAP.md (Phase 2 documentation) | Task 13 |
| Multi-tenancy (query filters) | Task 4 |
| Integration events (6) | Task 3 (events), 11 (publishing) |
| Template seeding pattern | Task 13 |
| WellKnownNotificationTypes.WorkflowTaskAssigned | Task 1 |
