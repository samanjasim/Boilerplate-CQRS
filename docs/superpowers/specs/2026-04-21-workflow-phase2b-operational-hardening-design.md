# Workflow Phase 2b — Operational Hardening (Design)

## Context

Phase 2a (`docs/superpowers/specs/2026-04-20-workflow-phase2a-engine-power-design.md`, merged in PR #5) shipped five user-visible engine features: dynamic forms, compound conditions, parallel approvals, SLA escalation, delegation. The roadmap (`docs/roadmaps/workflow.md`) lists ten further deferred items grouped into operational maturity, integration surface, authoring UX, and AI-dependent features.

Phase 2b takes the **operational maturity** subset minus bulk operations. The remaining two items — transactional outbox and denormalized inbox — share a single property: they are pure backend hardening with no FE surface, so they bundle into one focused PR.

Bulk operations was scoped out because it needs FE work (checkbox column, batch action bar, optimistic UI) which makes the PR a different shape. It will land in its own follow-up.

## Cross-Module Integration

- **No new capability contracts.** This phase touches only the Workflow module's own infrastructure.
- **MassTransit + EF outbox** is already wired against `ApplicationDbContext` in `Starter.Infrastructure/DependencyInjection.cs` (line 195). This phase mirrors that wiring against `WorkflowDbContext`, so the existing outbox dispatcher hosted service handles delivery without further changes.
- **Comments & Activity, Communication, Webhooks** modules are unaffected — they continue to publish via `IMessagePublisher` against the bus directly.

## Feature 1 — Transactional outbox on `WorkflowDbContext`

### What

Bind MassTransit's EF outbox to `WorkflowDbContext` so every `IBus.Publish` call originating inside a workflow scope queues the message into the workflow's outbox table within the same DB transaction as the state change. Delivery becomes asynchronous and crash-safe.

### Why

Today the publish path is:

1. Engine commits state change via `WorkflowDbContext.SaveChangesAsync`
2. MediatR notification fires `PublishWorkflowTransitionIntegrationHandler`
3. Handler calls `IMessagePublisher.PublishAsync`, which calls `IBus.Publish`
4. Message goes directly to RabbitMQ

If the process crashes between step 1 and step 4, the state change is durable but the integration event is lost. The boilerplate roadmap calls this out (`docs/roadmaps/workflow.md:75-86`) as the trigger for at-least-once consumers.

### Schema additions to `WorkflowDbContext`

MassTransit's EF outbox requires three tables:

| Table | Purpose |
|---|---|
| `OutboxMessage` | Queue of unpublished messages |
| `OutboxState` | Per-context delivery position |
| `InboxState` | Idempotency tracking for inbound consumers (kept even if no consumers run on this context, since MassTransit configures all three together) |

The `WorkflowDbContext` adds three `DbSet<>` properties for these and registers their EF configurations in `OnModelCreating`. MassTransit ships configuration helpers — call `modelBuilder.AddTransactionalOutboxEntities()` in `OnModelCreating`.

### DI registration

In the workflow module's DI registration (search `boilerplateBE/src/modules/Starter.Module.Workflow` for the `IServiceCollection` extension that registers `WorkflowDbContext`), add:

```csharp
busConfigurator.AddEntityFrameworkOutbox<WorkflowDbContext>(o =>
{
    o.UsePostgres();
    o.UseBusOutbox();
});
```

The `busConfigurator` reference reuses the existing MassTransit configurator from `Starter.Infrastructure/DependencyInjection.cs`. The cleanest path is to expose a public hook on the existing MassTransit setup that modules can extend, OR have the Workflow module's DI add a second `AddEntityFrameworkOutbox` to the same `IBusRegistrationConfigurator`. Implementation plan will pick one — both are valid.

### Migration

A new migration on `WorkflowDbContext` adds the three outbox tables with the conventional MassTransit schema. The migration history table is `__EFMigrationsHistory_Workflow`.

### Tests

`WorkflowOutboxTests.cs` in `tests/Starter.Api.Tests/Workflow/`:

- `Publish_InsideWorkflowSaveChanges_WritesOutboxRow` — publish happens inside `SaveChangesAsync`; assert a row appears in `OutboxMessage` with the same transaction commit timestamp.
- `Publish_OutsideWorkflowSaveChanges_StillSendsDirectlyToBus` — publishes from a non-workflow scope still go straight to the bus (sanity check that the outbox is scoped to `WorkflowDbContext` only).
- `BrokerOutage_DoesNotLoseEvent` — start with broker stopped, perform a workflow transition, observe outbox row written, start broker, observe row drained and message delivered. Implementation will fake broker outage by restarting the MassTransit bus harness.

### Non-changes

- `IMessagePublisher` interface unchanged.
- `MassTransitMessagePublisher` implementation unchanged — it still calls `_bus.Publish`.
- `PublishWorkflowIntegrationEventsHandler` unchanged — same MediatR notification handler, same call into `IMessagePublisher`.

The outbox is invisible to all callers. It only intercepts at the MassTransit transport layer when it sees a scoped `WorkflowDbContext` in the active scope.

## Feature 2 — Denormalized inbox

### What

Copy state-specific data from `WorkflowInstance` + `WorkflowDefinition` + the active `WorkflowStateConfig` onto `ApprovalTask` at task-creation time so `GetPendingTasksAsync` becomes a single-table query.

### Why

`GetPendingTasksAsync` (`Infrastructure/Services/WorkflowEngine.cs:574`) currently:

- Joins three tables (`ApprovalTask` → `WorkflowInstance` → `WorkflowDefinition`)
- Pulls full rows from all three (no projection)
- Deserializes `StatesJson` and `TransitionsJson` per row to derive `availableActions`, `formFields`, and SLA overdue status

At a tenant with 1000+ pending tasks and 20 active definitions this scales poorly: page query payload includes the full definition's JSON for every task row.

### Columns added to `ApprovalTask`

All populated by `CreateApprovalTaskAsync` (`Infrastructure/Services/WorkflowEngine.cs` — search for the method) from the Instance + Definition + the StateConfig matching the task's `StepName`.

| Column | Type | Source | Why denormalize |
|---|---|---|---|
| `DefinitionName` | `string` (max 200) | `Definition.Name` | Inbox shows definition name in each row |
| `DefinitionDisplayName` | `string?` (max 200) | `Definition.DisplayName` | Display label, falls back to `DefinitionName` |
| `EntityType` | `string` (max 100) | `Instance.EntityType` | Used to construct entity detail link |
| `EntityId` | `Guid` | `Instance.EntityId` | Link target |
| `EntityDisplayName` | `string?` (max 200) | `Instance.EntityDisplayName` | "Approve {label}" — already on Instance, copy at task creation |
| `FormFieldsJson` | `string?` (text) | JSON-serialized `StateConfig.FormFields` (null if empty) | ApprovalDialog reads this without deserializing the whole `StatesJson` |
| `AvailableActionsJson` | `string` (text) | JSON-serialized list of trigger names from `TransitionsJson` filtered to `Type == "Manual"` and `From == StepName` | Inbox shows action buttons without parsing transitions |
| `SlaReminderAfterHours` | `int?` | `StateConfig.Sla.ReminderAfterHours` | Overdue badge derives from `(now - CreatedAt).TotalHours >= SlaReminderAfterHours` — no state lookup |

### Rewritten `GetPendingTasksAsync`

```csharp
var baseQuery = context.ApprovalTasks
    .Where(t => t.Status == TaskStatus.Pending
        && (t.AssigneeUserId == userId || t.OriginalAssigneeUserId == userId))
    .OrderByDescending(t => t.CreatedAt);

var totalCount = await baseQuery.CountAsync(ct);
var tasks = await baseQuery
    .Skip((pageNumber - 1) * pageSize)
    .Take(pageSize)
    .ToListAsync(ct);

// (Existing delegation user-name lookup stays — already batched.)
// (Existing parallel-group sibling-count query stays — already batched.)

var items = tasks.Select(t => new PendingTaskSummary(
    t.Id,
    t.InstanceId,
    t.DefinitionName,                     // was: t.Instance.Definition.Name
    t.EntityType,                         // was: t.Instance.EntityType
    t.EntityId,                           // was: t.Instance.EntityId
    t.StepName,
    t.AssigneeRole,
    t.CreatedAt,
    t.DueDate,
    DeserializeActions(t.AvailableActionsJson),
    t.EntityDisplayName,                  // was: t.Instance.EntityDisplayName
    FormFields: DeserializeFormFields(t.FormFieldsJson),
    GroupId: t.GroupId,
    ParallelTotal: parallelTotal,
    ParallelCompleted: parallelCompleted,
    IsOverdue: ComputeOverdue(t.CreatedAt, t.SlaReminderAfterHours, out var hoursOverdue),
    HoursOverdue: hoursOverdue,
    IsDelegated: isDelegated,
    DelegatedFromDisplayName: delegatedFromDisplayName)).ToList();
```

### Migration

EF migration adds the columns and backfills existing rows via a single SQL `UPDATE … FROM` statement that joins `ApprovalTasks` to `WorkflowInstances` to `WorkflowDefinitions`. The `FormFieldsJson` / `AvailableActionsJson` / `SlaReminderAfterHours` backfill is more complex (requires deserializing `StatesJson`); for backfill it is acceptable to leave these `NULL` on existing rows and let the inbox fall back to the legacy join only when the new columns are null. Alternative: write a one-off backfill data migration that runs the same logic as `CreateApprovalTaskAsync` over each existing pending task. The implementation plan will pick the simpler of the two — likely the fallback approach since pending tasks typically clear within days.

### Stale data risk

If a `WorkflowDefinition` is edited while open tasks exist, denormalized fields drift. Mitigation:

- The existing UI encourages clone-before-edit (the definitions list page shows a Clone button).
- The denormalized fields that depend on a state config (`FormFieldsJson`, `AvailableActionsJson`, `SlaReminderAfterHours`) are static for the lifetime of a single state instance — once a task is created for state X, it stays in state X until executed.
- `Definition.Name` / `DisplayName` rename is the only real drift case. Cosmetic only — the link still resolves via `EntityType`/`EntityId`.

Acceptable tradeoff. If clone-before-edit becomes a problem, a follow-up can add a `definitions:edit` confirmation that warns about open tasks.

### Tests

`PendingTasksDenormalizationTests.cs` in `tests/Starter.Api.Tests/Workflow/`:

- `CreateTask_PopulatesDenormalizedFields` — start a workflow, assert the resulting `ApprovalTask` has all denormalized fields populated correctly from Instance + Definition + StateConfig.
- `GetPendingTasks_DoesNotJoinDefinitionOrInstance` — capture EF SQL via `EnableSensitiveDataLogging`, assert the generated query references only `ApprovalTasks` (no `WorkflowDefinitions` / `WorkflowInstances` JOIN).
- `LegacyTasks_FallbackToJoin` — pre-create an `ApprovalTask` with NULL denormalized columns; assert `GetPendingTasksAsync` still returns correct data (validates the migration fallback behavior if we go that route).

## Files Changed (overview)

### Workflow Module — Domain

- `Domain/Entities/ApprovalTask.cs` — add 8 properties, parameterize `Create` factory.

### Workflow Module — Infrastructure

- `Infrastructure/Persistence/WorkflowDbContext.cs` — add 3 outbox `DbSet`s, register configurations in `OnModelCreating`.
- `Infrastructure/Persistence/Configurations/ApprovalTaskConfiguration.cs` — add EF config for the 8 new columns.
- `Infrastructure/Services/WorkflowEngine.cs`:
  - `CreateApprovalTaskAsync` — populate denormalized fields from Instance + Definition + StateConfig.
  - `GetPendingTasksAsync` — single-table query, fall back to existing logic when denormalized fields are null.
- `Infrastructure/DependencyInjection.cs` (or wherever `WorkflowDbContext` is registered) — add `AddEntityFrameworkOutbox<WorkflowDbContext>`.
- New EF migration in `Infrastructure/Persistence/Migrations/` — adds 8 columns to `ApprovalTasks` + 3 outbox tables.

### Tests

- `tests/Starter.Api.Tests/Workflow/WorkflowOutboxTests.cs` (new)
- `tests/Starter.Api.Tests/Workflow/PendingTasksDenormalizationTests.cs` (new)

### Frontend

None. The DTO `PendingTaskSummary` is unchanged; the FE continues to receive identical JSON.

## Non-Goals (Phase 2b)

- Bulk approve/reject UI (deferred — different shape, FE-heavy).
- External webhook triggers (`POST /workflow/webhook/{eventName}`) — deferred to Phase 2c or beyond.
- Visual workflow designer — deferred.
- Entity-level comment access control — deferred (cross-module concern, brainstorm separately).
- Outbox on `ApplicationDbContext` consumer changes — out of scope, only the Workflow module's publish path changes.
- Definition edit guardrails — accept stale denormalized data risk for this PR.
