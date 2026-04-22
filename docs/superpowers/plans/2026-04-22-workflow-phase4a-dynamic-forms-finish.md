# Workflow Phase 4a — Finish Dynamic Forms Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Finish shipping dynamic forms — surface field-level validation errors to the FE, prove the form-data → conditional-branch chain with an integration test, and document the capability.

**Architecture:** The BE already has `FormDataValidator`, `FormFieldDefinition`, and ContextJson-merging in the engine. This plan:
1. Refactors `WorkflowEngine.ExecuteTaskAsync` from `Task<bool>` to `Task<Result<bool>>`, using the existing `Result.ValidationFailure(ValidationErrors)` channel for form validation errors and specific `WorkflowErrors.*` factories for other failure paths.
2. Propagates the `Result<bool>` through the command handler and the batch handler.
3. Updates `ApprovalDialog` to read `validationErrors` from the response envelope and render per-field errors via the existing `DynamicFormRenderer.errors` prop.
4. Adds a `.NET` integration test proving `formData submitted → ContextJson merged → branching transition resolves` end-to-end.
5. Updates roadmap docs and adds a feature doc.

**Tech Stack:** .NET 10 + MediatR + EF Core + xUnit + FluentAssertions + Moq (BE); React 19 + TypeScript + TanStack Query + i18next (FE).

**Spec:** [`docs/superpowers/specs/2026-04-22-workflow-phase4a-dynamic-forms-finish-design.md`](../specs/2026-04-22-workflow-phase4a-dynamic-forms-finish-design.md)

---

## File Structure

**Backend — modify:**
- `boilerplateBE/src/modules/Starter.Module.Workflow/Domain/Errors/WorkflowErrors.cs` — add 2 new factories
- `boilerplateBE/src/Starter.Abstractions/Capabilities/IWorkflowService.cs` — signature change on `ExecuteTaskAsync`
- `boilerplateBE/src/Starter.Infrastructure/Capabilities/NullObjects/NullWorkflowService.cs` — stub update
- `boilerplateBE/src/modules/Starter.Module.Workflow/Infrastructure/Services/WorkflowEngine.cs` — signature + return sites (Task 3 & 4)
- `boilerplateBE/src/modules/Starter.Module.Workflow/Application/Commands/ExecuteTask/ExecuteTaskCommandHandler.cs` — propagate `Result<bool>`
- `boilerplateBE/src/modules/Starter.Module.Workflow/Application/Commands/BatchExecuteTasks/BatchExecuteTasksCommandHandler.cs` — adapt to `Result<bool>`
- `boilerplateBE/tests/Starter.Api.Tests/Workflow/WorkflowEngineTests.cs` — update existing `ExecuteTaskAsync_*` assertions
- `boilerplateBE/tests/Starter.Api.Tests/Workflow/BatchExecuteTasksTests.cs` — update mock setups

**Backend — create:**
- `boilerplateBE/tests/Starter.Api.Tests/Workflow/ExecuteTaskResultShapeTests.cs` — asserts specific `Result` shape per failure path
- `boilerplateBE/tests/Starter.Api.Tests/Workflow/FormDataBranchingIntegrationTests.cs` — end-to-end form → branch test

**Frontend — modify:**
- `boilerplateFE/src/features/workflow/components/ApprovalDialog.tsx` — handle `validationErrors` on error

**Frontend — create:**
- `boilerplateFE/src/features/workflow/utils/extractValidationErrors.ts` — tiny helper

**Docs — modify:**
- `docs/roadmaps/workflow.md` — Phase 4a Shipped section + Phase 4+ Deferred Forms subsection

**Docs — create:**
- `docs/features/workflow-forms.md` — capability documentation

---

## Engine `return false` audit (locked)

All seven `return false` paths in `WorkflowEngine.ExecuteTaskAsync`, with target `Result<bool>`:

| Line | Condition | Target |
|---|---|---|
| 263 | `task is null` | `Result.Failure<bool>(WorkflowErrors.TaskNotFound(taskId))` (existing factory, NotFound) |
| 276 | `task.Status != Pending` | `Result.Failure<bool>(WorkflowErrors.TaskNotPending(taskId))` (new, Conflict) |
| 285 | actor != assignee | `Result.Failure<bool>(WorkflowErrors.TaskNotAssignedToUser(taskId, actorUserId))` (existing factory, Forbidden) |
| 304 | no matching transition | `Result.Failure<bool>(WorkflowErrors.InvalidTransition(currentState, action))` (existing factory, Validation) |
| 332 | form validation failed | `Result.ValidationFailure<bool>(validationErrors)` (built from `FormValidationError`) |
| 406 | concurrency (parallel branch) | `Result.Failure<bool>(WorkflowErrors.Concurrency())` (new, Conflict) |
| 466 | concurrency (main path) | `Result.Failure<bool>(WorkflowErrors.Concurrency())` (new, Conflict) |

New `WorkflowErrors` factories to add: `TaskNotPending(Guid)`, `Concurrency()`.

---

## Task 1: Add new `WorkflowErrors` factories

**Files:**
- Modify: `boilerplateBE/src/modules/Starter.Module.Workflow/Domain/Errors/WorkflowErrors.cs`

- [ ] **Step 1: Add the two new factory methods**

Open `boilerplateBE/src/modules/Starter.Module.Workflow/Domain/Errors/WorkflowErrors.cs` and append these factories inside the class body (right before the closing brace on line 33):

```csharp
    public static Error TaskNotPending(Guid id) =>
        Error.Conflict("Workflow.TaskNotPending", $"Approval task '{id}' is not in a pending state");

    public static Error Concurrency() =>
        Error.Conflict("Workflow.Concurrency", "Another user acted on this task concurrently. Please refresh and try again.");
```

- [ ] **Step 2: Build the BE**

Run: `cd boilerplateBE && dotnet build`
Expected: PASS (no compile errors introduced).

- [ ] **Step 3: Commit**

```bash
git add boilerplateBE/src/modules/Starter.Module.Workflow/Domain/Errors/WorkflowErrors.cs
git commit -m "feat(workflow): add TaskNotPending + Concurrency error factories"
```

---

## Task 2: Update `IWorkflowService` + `NullWorkflowService` signatures

**Files:**
- Modify: `boilerplateBE/src/Starter.Abstractions/Capabilities/IWorkflowService.cs`
- Modify: `boilerplateBE/src/Starter.Infrastructure/Capabilities/NullObjects/NullWorkflowService.cs`

- [ ] **Step 1: Read the current interface declaration**

Open `boilerplateBE/src/Starter.Abstractions/Capabilities/IWorkflowService.cs` and locate the `ExecuteTaskAsync` method (around line 20). The current signature is:

```csharp
Task<bool> ExecuteTaskAsync(Guid taskId, string action, string? comment,
    Guid actorUserId, Dictionary<string, object>? formData = null,
    CancellationToken ct = default);
```

- [ ] **Step 2: Change interface signature to `Task<Result<bool>>`**

Replace the declaration with:

```csharp
Task<Result<bool>> ExecuteTaskAsync(Guid taskId, string action, string? comment,
    Guid actorUserId, Dictionary<string, object>? formData = null,
    CancellationToken ct = default);
```

Add the necessary using directive at the top of the file if not already present:

```csharp
using Starter.Shared.Results;
```

- [ ] **Step 3: Update `NullWorkflowService` stub**

Open `boilerplateBE/src/Starter.Infrastructure/Capabilities/NullObjects/NullWorkflowService.cs` and locate the `ExecuteTaskAsync` method (around line 41). Replace its signature + body with:

```csharp
public Task<Result<bool>> ExecuteTaskAsync(
    Guid taskId, string action, string? comment,
    Guid actorUserId, Dictionary<string, object>? formData = null,
    CancellationToken ct = default) =>
    Task.FromResult(Result.Success(false));
```

Add the using directive if not already present:

```csharp
using Starter.Shared.Results;
```

`Success(false)` is the semantically-neutral "didn't do anything" response from a null object — callers that check only `IsSuccess` won't treat it as an error, and callers that consume `.Value` see `false` (no-op).

- [ ] **Step 4: Build the BE**

Run: `cd boilerplateBE && dotnet build`
Expected: FAIL — `WorkflowEngine` and the two handlers still return `Task<bool>`. This is the expected intermediate state; next tasks fix it.

- [ ] **Step 5: Do NOT commit yet**

Task 2 is not independently buildable — it sets up for Tasks 3/4/5 which together restore the green build. Continue to Task 3.

---

## Task 3: Refactor `WorkflowEngine.ExecuteTaskAsync` — non-validation failure paths

**Files:**
- Modify: `boilerplateBE/src/modules/Starter.Module.Workflow/Infrastructure/Services/WorkflowEngine.cs`

This task rewires the method's return type and all six non-validation `return false` sites. The validation path (line 332) is handled in Task 4.

- [ ] **Step 1: Change method signature**

Open `boilerplateBE/src/modules/Starter.Module.Workflow/Infrastructure/Services/WorkflowEngine.cs`. At line 250, change:

```csharp
public async Task<bool> ExecuteTaskAsync(
```

to:

```csharp
public async Task<Result<bool>> ExecuteTaskAsync(
```

Add the using directive at the top of the file if not already present:

```csharp
using Starter.Shared.Results;
```

Also note: `WorkflowErrors` is already imported for other methods in the same file; verify the `using Starter.Module.Workflow.Domain.Errors;` line exists, add it if not.

- [ ] **Step 2: Replace `return false` on line 263 (task null)**

Change:

```csharp
if (task is null)
{
    logger.LogWarning("Approval task {TaskId} not found.", taskId);
    return false;
}
```

to:

```csharp
if (task is null)
{
    logger.LogWarning("Approval task {TaskId} not found.", taskId);
    return Result.Failure<bool>(WorkflowErrors.TaskNotFound(taskId));
}
```

- [ ] **Step 3: Replace `return true` on line 270 (idempotent success)**

Change:

```csharp
if (task.Status == Domain.Enums.TaskStatus.Completed && task.Action == action)
{
    logger.LogDebug("Task {TaskId} already completed with action {Action} — idempotent success.", taskId, action);
    return true;
}
```

to:

```csharp
if (task.Status == Domain.Enums.TaskStatus.Completed && task.Action == action)
{
    logger.LogDebug("Task {TaskId} already completed with action {Action} — idempotent success.", taskId, action);
    return Result.Success(true);
}
```

- [ ] **Step 4: Replace `return false` on line 276 (not pending)**

Change:

```csharp
if (task.Status != Domain.Enums.TaskStatus.Pending)
{
    logger.LogWarning("Task {TaskId} is not pending (status: {Status}).", taskId, task.Status);
    return false;
}
```

to:

```csharp
if (task.Status != Domain.Enums.TaskStatus.Pending)
{
    logger.LogWarning("Task {TaskId} is not pending (status: {Status}).", taskId, task.Status);
    return Result.Failure<bool>(WorkflowErrors.TaskNotPending(taskId));
}
```

- [ ] **Step 5: Replace `return false` on line 285 (actor != assignee)**

Change:

```csharp
if (task.AssigneeUserId.HasValue && task.AssigneeUserId.Value != actorUserId)
{
    logger.LogWarning(
        "Actor {ActorId} is not assigned to task {TaskId} (assigned to {AssigneeId}).",
        actorUserId, taskId, task.AssigneeUserId);
    return false;
}
```

to:

```csharp
if (task.AssigneeUserId.HasValue && task.AssigneeUserId.Value != actorUserId)
{
    logger.LogWarning(
        "Actor {ActorId} is not assigned to task {TaskId} (assigned to {AssigneeId}).",
        actorUserId, taskId, task.AssigneeUserId);
    return Result.Failure<bool>(WorkflowErrors.TaskNotAssignedToUser(taskId, actorUserId));
}
```

- [ ] **Step 6: Replace `return false` on line 304 (no matching transition)**

Change:

```csharp
if (matchingTransitions.Count == 0)
{
    logger.LogWarning(
        "No transition from '{FromState}' with trigger '{Action}' in definition '{DefName}'.",
        instance.CurrentState, action, definition.Name);
    return false;
}
```

to:

```csharp
if (matchingTransitions.Count == 0)
{
    logger.LogWarning(
        "No transition from '{FromState}' with trigger '{Action}' in definition '{DefName}'.",
        instance.CurrentState, action, definition.Name);
    return Result.Failure<bool>(WorkflowErrors.InvalidTransition(instance.CurrentState, action));
}
```

- [ ] **Step 7: Replace `return false` on line 406 (parallel concurrency)**

Change:

```csharp
catch (DbUpdateConcurrencyException)
{
    logger.LogWarning("Concurrency conflict on task {TaskId}. Another user may have already acted.", taskId);
    return false;
}
```

(the block at line 403-407)

to:

```csharp
catch (DbUpdateConcurrencyException)
{
    logger.LogWarning("Concurrency conflict on task {TaskId}. Another user may have already acted.", taskId);
    return Result.Failure<bool>(WorkflowErrors.Concurrency());
}
```

- [ ] **Step 8: Replace `return false` on line 466 (main-path concurrency)**

Change the same pattern at line 463-467:

```csharp
catch (DbUpdateConcurrencyException)
{
    logger.LogWarning("Concurrency conflict on task {TaskId}. Another user may have already acted.", taskId);
    return false;
}
```

to:

```csharp
catch (DbUpdateConcurrencyException)
{
    logger.LogWarning("Concurrency conflict on task {TaskId}. Another user may have already acted.", taskId);
    return Result.Failure<bool>(WorkflowErrors.Concurrency());
}
```

- [ ] **Step 9: Find and replace any remaining `return true` at the end of the method**

Search the `ExecuteTaskAsync` method body for any remaining bare `return true;` (the success path(s) after the completion logic). Replace each with:

```csharp
return Result.Success(true);
```

There will typically be one final success return near the end of the method. Grep within the method body to be sure no `return true` or `return false` remains — the compiler will also catch any misses.

- [ ] **Step 10: Build (Task 4 still needed to finish)**

Run: `cd boilerplateBE && dotnet build`
Expected: Still FAIL because line 332 (form validation) still has `return false`, AND `ExecuteTaskCommandHandler` + `BatchExecuteTasksCommandHandler` still consume `bool`. Tasks 4 & 5 will fix.

- [ ] **Step 11: Do NOT commit yet**

---

## Task 4: Form validation path — wire `ValidationErrors` + `Result.ValidationFailure`

**Files:**
- Modify: `boilerplateBE/src/modules/Starter.Module.Workflow/Infrastructure/Services/WorkflowEngine.cs` (line 324-334)

- [ ] **Step 1: Replace the validation block (line 324-334)**

Open `boilerplateBE/src/modules/Starter.Module.Workflow/Infrastructure/Services/WorkflowEngine.cs`. The current block reads:

```csharp
// Validate form data if the current state has form fields defined
if (fromStateConfig?.FormFields is { Count: > 0 })
{
    var validationErrors = formDataValidator.Validate(fromStateConfig.FormFields, formData);
    if (validationErrors.Count > 0)
    {
        logger.LogWarning(
            "Form data validation failed for task {TaskId}: {Errors}",
            taskId, string.Join(", ", validationErrors.Select(e => $"{e.FieldName}: {e.Message}")));
        return false;
    }
}
```

Replace with:

```csharp
// Validate form data if the current state has form fields defined
if (fromStateConfig?.FormFields is { Count: > 0 })
{
    var fieldErrors = formDataValidator.Validate(fromStateConfig.FormFields, formData);
    if (fieldErrors.Count > 0)
    {
        logger.LogWarning(
            "Form data validation failed for task {TaskId}: {Errors}",
            taskId, string.Join(", ", fieldErrors.Select(e => $"{e.FieldName}: {e.Message}")));

        var validationErrors = new ValidationErrors();
        foreach (var err in fieldErrors)
            validationErrors.Add(err.FieldName, err.Message);
        return Result.ValidationFailure<bool>(validationErrors);
    }
}
```

The local rename `validationErrors → fieldErrors` avoids shadowing the new `ValidationErrors` instance of the same name.

- [ ] **Step 2: Build**

Run: `cd boilerplateBE && dotnet build`
Expected: Still FAIL because `ExecuteTaskCommandHandler` and `BatchExecuteTasksCommandHandler` + their tests still consume `bool`. Task 5 fixes.

- [ ] **Step 3: Do NOT commit yet**

---

## Task 5: Propagate `Result<bool>` through handlers + update existing tests

**Files:**
- Modify: `boilerplateBE/src/modules/Starter.Module.Workflow/Application/Commands/ExecuteTask/ExecuteTaskCommandHandler.cs`
- Modify: `boilerplateBE/src/modules/Starter.Module.Workflow/Application/Commands/BatchExecuteTasks/BatchExecuteTasksCommandHandler.cs`
- Modify: `boilerplateBE/tests/Starter.Api.Tests/Workflow/WorkflowEngineTests.cs`
- Modify: `boilerplateBE/tests/Starter.Api.Tests/Workflow/BatchExecuteTasksTests.cs`

- [ ] **Step 1: Update `ExecuteTaskCommandHandler`**

Open `boilerplateBE/src/modules/Starter.Module.Workflow/Application/Commands/ExecuteTask/ExecuteTaskCommandHandler.cs`. Replace the entire file body with:

```csharp
using MediatR;
using Starter.Abstractions.Capabilities;
using Starter.Application.Common.Interfaces;
using Starter.Shared.Results;

namespace Starter.Module.Workflow.Application.Commands.ExecuteTask;

internal sealed class ExecuteTaskCommandHandler(
    IWorkflowService workflowService,
    ICurrentUserService currentUser) : IRequestHandler<ExecuteTaskCommand, Result<bool>>
{
    public Task<Result<bool>> Handle(ExecuteTaskCommand request, CancellationToken cancellationToken) =>
        workflowService.ExecuteTaskAsync(
            request.TaskId,
            request.Action,
            request.Comment,
            currentUser.UserId!.Value,
            request.FormData,
            cancellationToken);
}
```

The `WorkflowErrors` reference is now removed; the service returns the correct Result shape directly.

- [ ] **Step 2: Update `BatchExecuteTasksCommandHandler` call site**

Open `boilerplateBE/src/modules/Starter.Module.Workflow/Application/Commands/BatchExecuteTasks/BatchExecuteTasksCommandHandler.cs`. Find the call around line 48:

```csharp
var ok = await workflowService.ExecuteTaskAsync(
```

Change the surrounding block so `ok` becomes a `Result<bool>` and the success check uses `.IsSuccess && .Value`. The typical shape is:

```csharp
var result = await workflowService.ExecuteTaskAsync(
    taskId, action, comment, actorUserId, formData: null, ct);
var ok = result.IsSuccess && result.Value;
```

Keep all the rest of the loop body (the `ok` boolean is still used to bucket success/fail outcomes in the batch result). If the surrounding code uses the return value differently, adapt minimally — the semantics are: treat `Result.Success(true)` as success, everything else as "failed" for batch aggregation purposes. Per-task error detail is stored via whatever mechanism the handler already uses; don't expand it in this task — batch error surfacing is a separate concern.

- [ ] **Step 3: Update `WorkflowEngineTests` assertions**

Open `boilerplateBE/tests/Starter.Api.Tests/Workflow/WorkflowEngineTests.cs`. The existing tests assert on `bool` return values. Every `await _sut.ExecuteTaskAsync(...)` call needs its assertion updated from `bool` to `Result<bool>` semantics.

For each of the following call sites (from earlier grep: lines 255, 297, 340, 379, 420, 705, 745, 774, 810, 846), the pattern is:

**Before:**
```csharp
var result = await _sut.ExecuteTaskAsync(/* args */);
result.Should().BeTrue();  // or BeFalse()
```

**After (success expectations):**
```csharp
var result = await _sut.ExecuteTaskAsync(/* args */);
result.IsSuccess.Should().BeTrue();
result.Value.Should().BeTrue();
```

**After (failure expectations — e.g. line 340, `NotAssigned_ReturnsFalse`):**
```csharp
var result = await _sut.ExecuteTaskAsync(/* args */);
result.IsFailure.Should().BeTrue();
result.Error.Code.Should().Be("Workflow.TaskNotAssignedToUser");  // or whatever code matches the scenario
```

For the existing `NotAssigned_ReturnsFalse` test specifically, assert the error is `TaskNotAssignedToUser` (the error factory used at line 285's replacement). Rename the test to `NotAssigned_ReturnsForbiddenError` to match the new semantics.

Use Grep to find all `ExecuteTaskAsync` call sites in the file and update each; the compiler will flag any you miss.

- [ ] **Step 4: Update `BatchExecuteTasksTests` mock setups**

Open `boilerplateBE/tests/Starter.Api.Tests/Workflow/BatchExecuteTasksTests.cs`. The mocks currently use `ReturnsAsync(true)` / `ReturnsAsync(false)`. Update every `_workflow.Setup(w => w.ExecuteTaskAsync(...)).ReturnsAsync(true|false)` to return a `Result<bool>`:

**Before:**
```csharp
_workflow.Setup(w => w.ExecuteTaskAsync(success, "approve", null, _userId, null, It.IsAny<CancellationToken>())).ReturnsAsync(true);
_workflow.Setup(w => w.ExecuteTaskAsync(fail, "approve", null, _userId, null, It.IsAny<CancellationToken>())).ReturnsAsync(false);
```

**After:**
```csharp
_workflow.Setup(w => w.ExecuteTaskAsync(success, "approve", null, _userId, null, It.IsAny<CancellationToken>()))
    .ReturnsAsync(Result.Success(true));
_workflow.Setup(w => w.ExecuteTaskAsync(fail, "approve", null, _userId, null, It.IsAny<CancellationToken>()))
    .ReturnsAsync(Result.Failure<bool>(WorkflowErrors.TaskNotFound(fail)));
```

Add the necessary using directives to the test file:

```csharp
using Starter.Shared.Results;
using Starter.Module.Workflow.Domain.Errors;
```

Leave the `.ThrowsAsync(new InvalidOperationException("nope"))` mock unchanged — exception propagation semantics are unrelated to the Result refactor.

- [ ] **Step 5: Build + test**

Run:
```bash
cd boilerplateBE && dotnet build && dotnet test --filter "FullyQualifiedName~Workflow"
```
Expected: PASS — all existing Workflow tests pass under the new Result<bool> contract.

- [ ] **Step 6: Commit (Tasks 2-5 together)**

```bash
git add boilerplateBE/src/Starter.Abstractions/Capabilities/IWorkflowService.cs \
    boilerplateBE/src/Starter.Infrastructure/Capabilities/NullObjects/NullWorkflowService.cs \
    boilerplateBE/src/modules/Starter.Module.Workflow/Infrastructure/Services/WorkflowEngine.cs \
    boilerplateBE/src/modules/Starter.Module.Workflow/Application/Commands/ExecuteTask/ExecuteTaskCommandHandler.cs \
    boilerplateBE/src/modules/Starter.Module.Workflow/Application/Commands/BatchExecuteTasks/BatchExecuteTasksCommandHandler.cs \
    boilerplateBE/tests/Starter.Api.Tests/Workflow/WorkflowEngineTests.cs \
    boilerplateBE/tests/Starter.Api.Tests/Workflow/BatchExecuteTasksTests.cs
git commit -m "refactor(workflow): ExecuteTaskAsync returns Result<bool>, surfaces typed errors"
```

---

## Task 6: New test — Result shape per failure path

**Files:**
- Create: `boilerplateBE/tests/Starter.Api.Tests/Workflow/ExecuteTaskResultShapeTests.cs`

- [ ] **Step 1: Write the failing test file**

Create `boilerplateBE/tests/Starter.Api.Tests/Workflow/ExecuteTaskResultShapeTests.cs`. This test class reuses the existing `WorkflowEngineTests` fixture pattern — pick a small scenario (single task, simple definition) and assert the precise error code per failure path. Base the fixture helpers (`CreateDefinitionAsync`, `CreateInstanceAsync`, `CreateTaskAsync`) on the patterns from `WorkflowEngineTests.cs`; if those helpers are private, either make them `internal`, extract them to a shared test helper, or inline-build the scenarios.

```csharp
using FluentAssertions;
using Starter.Module.Workflow.Domain.Enums;
using Starter.Module.Workflow.Domain.Errors;
using Xunit;

namespace Starter.Api.Tests.Workflow;

public class ExecuteTaskResultShapeTests : WorkflowEngineTestsBase
{
    [Fact]
    public async Task ExecuteTaskAsync_TaskNotFound_ReturnsTaskNotFoundError()
    {
        var result = await _sut.ExecuteTaskAsync(
            Guid.NewGuid(), "approve", null, Guid.NewGuid(), null, default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Workflow.TaskNotFound");
    }

    [Fact]
    public async Task ExecuteTaskAsync_TaskAlreadyCompleted_ReturnsTaskNotPendingError()
    {
        var (taskId, userId) = await CreateCompletedTaskAsync();

        var result = await _sut.ExecuteTaskAsync(taskId, "reject", null, userId, null, default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Workflow.TaskNotPending");
    }

    [Fact]
    public async Task ExecuteTaskAsync_WrongActor_ReturnsTaskNotAssignedToUserError()
    {
        var (taskId, _) = await CreateSimpleApprovalTaskAsync();
        var otherUser = Guid.NewGuid();

        var result = await _sut.ExecuteTaskAsync(taskId, "approve", null, otherUser, null, default);

        result.IsFailure.Should().BeTrue();
        result.Error.Description.Should().Contain("not assigned to user");
    }

    [Fact]
    public async Task ExecuteTaskAsync_InvalidTrigger_ReturnsInvalidTransitionError()
    {
        var (taskId, userId) = await CreateSimpleApprovalTaskAsync();

        var result = await _sut.ExecuteTaskAsync(taskId, "bogusTrigger", null, userId, null, default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Workflow.InvalidTransition");
    }

    [Fact]
    public async Task ExecuteTaskAsync_FormValidationFails_ReturnsValidationErrors()
    {
        var (taskId, userId) = await CreateTaskWithFormFieldAsync(
            fieldName: "amount", fieldType: "number", required: true, min: 0);

        var result = await _sut.ExecuteTaskAsync(
            taskId, "approve", null, userId, new() { ["amount"] = -5.0 }, default);

        result.IsFailure.Should().BeTrue();
        result.ValidationErrors.Should().NotBeNull();
        result.ValidationErrors!.Errors.Should().Contain(e => e.PropertyName == "amount");
    }

    [Fact]
    public async Task ExecuteTaskAsync_IdempotentResubmit_ReturnsSuccessTrue()
    {
        var (taskId, userId) = await CreateCompletedTaskAsync(action: "approve");

        var result = await _sut.ExecuteTaskAsync(taskId, "approve", null, userId, null, default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeTrue();
    }
}
```

The helpers `WorkflowEngineTestsBase`, `CreateSimpleApprovalTaskAsync`, `CreateCompletedTaskAsync`, and `CreateTaskWithFormFieldAsync` come from whatever shared fixture the existing `WorkflowEngineTests.cs` uses. Before writing this file, open `WorkflowEngineTests.cs` and identify how it arranges tasks (likely a private helper method or inline arrangement). Two paths:

1. **If `WorkflowEngineTests` has reusable fixture/helpers as internal or base class** — reuse them.
2. **If arrangements are inline** — copy the minimal arrangement pattern into a new `WorkflowEngineTestsBase` abstract base class in the same test project (new file `WorkflowEngineTestsBase.cs`), then have both `WorkflowEngineTests` and `ExecuteTaskResultShapeTests` inherit from it. The refactor is mechanical: extract common `_sut`, `_context`, `_factory`, and the arrangement helpers.

Defer the choice to implementation time based on what you find; the guiding principle is DRY — don't copy-paste the arrangement logic.

- [ ] **Step 2: Run the new tests to verify they fail appropriately**

Run: `cd boilerplateBE && dotnet test --filter "FullyQualifiedName~ExecuteTaskResultShapeTests"`
Expected: All tests PASS because the engine changes from Tasks 3 & 4 already produce these Result shapes. If any test fails, check the error code matches what's actually returned — correct the test assertion if the factory used a different code than expected.

- [ ] **Step 3: Commit**

```bash
git add boilerplateBE/tests/Starter.Api.Tests/Workflow/ExecuteTaskResultShapeTests.cs
# plus WorkflowEngineTestsBase.cs if you extracted one
git commit -m "test(workflow): assert Result shape per ExecuteTaskAsync failure path"
```

---

## Task 7: Integration test — form data → context merge → branching transition

**Files:**
- Create: `boilerplateBE/tests/Starter.Api.Tests/Workflow/FormDataBranchingIntegrationTests.cs`

- [ ] **Step 1: Write the failing test**

Create `boilerplateBE/tests/Starter.Api.Tests/Workflow/FormDataBranchingIntegrationTests.cs`:

```csharp
using System.Text.Json;
using FluentAssertions;
using Starter.Abstractions.Capabilities;
using Starter.Module.Workflow.Domain.Enums;
using Xunit;

namespace Starter.Api.Tests.Workflow;

public class FormDataBranchingIntegrationTests : WorkflowEngineTestsBase
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    [Fact]
    public async Task ExecuteTask_FormDataSatisfiesHighAmountCondition_BranchesToSeniorReview()
    {
        // Arrange — definition with a form field and two conditional outgoing transitions
        var states = new List<WorkflowStateConfig>
        {
            new("AwaitingApproval", "Awaiting Approval", "HumanTask",
                Actions: new() { "approve" },
                FormFields: new() {
                    new FormFieldDefinition("amount", "Amount", "number", Required: true, Min: 0)
                }),
            new("SeniorReview", "Senior Review", "HumanTask", Actions: new() { "approve" }),
            new("Approved", "Approved", "Terminal"),
        };
        var transitions = new List<WorkflowTransitionConfig>
        {
            new("AwaitingApproval", "SeniorReview", "approve",
                Condition: new ConditionConfig(Field: "amount", Operator: ">", Value: 10000)),
            new("AwaitingApproval", "Approved", "approve"),
        };
        var (_, instanceId, taskId, userId) = await SeedScenarioAsync(states, transitions);

        // Act
        var result = await _sut.ExecuteTaskAsync(
            taskId, "approve", null, userId,
            formData: new Dictionary<string, object> { ["amount"] = 15000.0 },
            ct: default);

        // Assert — result + state + context merge
        result.IsSuccess.Should().BeTrue();
        var instance = await _context.WorkflowInstances.FindAsync(instanceId);
        instance!.CurrentState.Should().Be("SeniorReview");

        var ctx = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(instance.ContextJson!, JsonOpts);
        ctx!.Should().ContainKey("amount");
        ctx["amount"].GetDouble().Should().Be(15000.0);
    }

    [Fact]
    public async Task ExecuteTask_FormDataBelowThreshold_BranchesToApproved()
    {
        var states = new List<WorkflowStateConfig>
        {
            new("AwaitingApproval", "Awaiting Approval", "HumanTask",
                Actions: new() { "approve" },
                FormFields: new() {
                    new FormFieldDefinition("amount", "Amount", "number", Required: true, Min: 0)
                }),
            new("SeniorReview", "Senior Review", "HumanTask", Actions: new() { "approve" }),
            new("Approved", "Approved", "Terminal"),
        };
        var transitions = new List<WorkflowTransitionConfig>
        {
            new("AwaitingApproval", "SeniorReview", "approve",
                Condition: new ConditionConfig(Field: "amount", Operator: ">", Value: 10000)),
            new("AwaitingApproval", "Approved", "approve"),
        };
        var (_, instanceId, taskId, userId) = await SeedScenarioAsync(states, transitions);

        var result = await _sut.ExecuteTaskAsync(
            taskId, "approve", null, userId,
            formData: new Dictionary<string, object> { ["amount"] = 500.0 },
            ct: default);

        result.IsSuccess.Should().BeTrue();
        var instance = await _context.WorkflowInstances.FindAsync(instanceId);
        instance!.CurrentState.Should().Be("Approved");
    }

    [Fact]
    public async Task ExecuteTask_InvalidFormData_NoStateChangeAndNoContextMerge()
    {
        var states = new List<WorkflowStateConfig>
        {
            new("AwaitingApproval", "Awaiting Approval", "HumanTask",
                Actions: new() { "approve" },
                FormFields: new() {
                    new FormFieldDefinition("amount", "Amount", "number", Required: true, Min: 0)
                }),
            new("Approved", "Approved", "Terminal"),
        };
        var transitions = new List<WorkflowTransitionConfig>
        {
            new("AwaitingApproval", "Approved", "approve"),
        };
        var (_, instanceId, taskId, userId) = await SeedScenarioAsync(states, transitions);

        var result = await _sut.ExecuteTaskAsync(
            taskId, "approve", null, userId,
            formData: new Dictionary<string, object> { ["amount"] = -5.0 },
            ct: default);

        // Result surfaces validation errors
        result.IsFailure.Should().BeTrue();
        result.ValidationErrors.Should().NotBeNull();
        result.ValidationErrors!.Errors.Should().Contain(e => e.PropertyName == "amount");

        // Instance state and context unchanged
        var instance = await _context.WorkflowInstances.FindAsync(instanceId);
        instance!.CurrentState.Should().Be("AwaitingApproval");
        instance.ContextJson.Should().BeNull();
    }
}
```

The test class relies on `WorkflowEngineTestsBase` (from Task 6) to provide `_sut`, `_context`, and a new helper `SeedScenarioAsync(states, transitions)` that:
1. Creates a `WorkflowDefinition` with the given states/transitions.
2. Creates a single `WorkflowInstance` in `AwaitingApproval`.
3. Creates a single pending `ApprovalTask` assigned to a synthesized user, returning `(definitionId, instanceId, taskId, userId)`.

If this helper doesn't already exist, extract it into `WorkflowEngineTestsBase`. Model it after whatever setup the existing `ExecuteTaskAsync_Approve_TransitionsToNextState` test uses in `WorkflowEngineTests.cs` (around line 213).

- [ ] **Step 2: Run the test**

Run: `cd boilerplateBE && dotnet test --filter "FullyQualifiedName~FormDataBranchingIntegrationTests"`
Expected: All three tests PASS. The engine already has the ContextJson merge + conditional branching logic; this test verifies the end-to-end chain.

- [ ] **Step 3: Commit**

```bash
git add boilerplateBE/tests/Starter.Api.Tests/Workflow/FormDataBranchingIntegrationTests.cs
# plus WorkflowEngineTestsBase.cs changes if helper extraction happened here
git commit -m "test(workflow): integration test for form data → context merge → branching"
```

---

## Task 8: FE — `extractValidationErrors` helper + `ApprovalDialog` wiring

**Files:**
- Create: `boilerplateFE/src/features/workflow/utils/extractValidationErrors.ts`
- Modify: `boilerplateFE/src/features/workflow/components/ApprovalDialog.tsx`

- [ ] **Step 1: Create the helper**

Create `boilerplateFE/src/features/workflow/utils/extractValidationErrors.ts`:

```ts
import type { AxiosError } from 'axios';

/**
 * Extracts field-level validation errors from an API error response.
 * The BE envelope (ApiResponse) serializes FluentValidation/Result.ValidationFailure output
 * to `validationErrors: { fieldName: ["message", ...] }`. This helper flattens the first
 * message per field into the shape the DynamicFormRenderer expects.
 */
export function extractValidationErrors(err: unknown): Record<string, string> | null {
  const axiosErr = err as AxiosError<{
    validationErrors?: Record<string, string[]>;
  }>;
  const ve = axiosErr.response?.data?.validationErrors;
  if (!ve || typeof ve !== 'object') return null;

  const flat: Record<string, string> = {};
  for (const [field, messages] of Object.entries(ve)) {
    if (Array.isArray(messages) && messages.length > 0) {
      flat[field] = messages[0];
    }
  }
  return Object.keys(flat).length > 0 ? flat : null;
}
```

- [ ] **Step 2: Wire into `ApprovalDialog`**

Open `boilerplateFE/src/features/workflow/components/ApprovalDialog.tsx`. Add the import near the top:

```tsx
import { extractValidationErrors } from '../utils/extractValidationErrors';
```

Locate the `handleAction` function (starts around line 81). Replace it with:

```tsx
const handleAction = (action: string) => {
  if (!validateForm()) return;

  executeTask(
    {
      taskId,
      data: {
        action,
        comment: comment || undefined,
        ...(hasFormFields ? { formData } : {}),
      },
    },
    {
      onSuccess: () => {
        setComment('');
        setFormData({});
        setFormErrors({});
        onOpenChange(false);
      },
      onError: (err) => {
        const fieldErrors = extractValidationErrors(err);
        if (fieldErrors) {
          setFormErrors(fieldErrors);
        }
        // Non-field errors (e.g. 403, 404, 409) surface via the existing toast path in useExecuteTask.
      },
    },
  );
};
```

- [ ] **Step 3: Build the FE**

Run: `cd boilerplateFE && npm run build`
Expected: PASS — TypeScript compiles and Vite builds without errors.

- [ ] **Step 4: Commit**

```bash
git add boilerplateFE/src/features/workflow/utils/extractValidationErrors.ts \
    boilerplateFE/src/features/workflow/components/ApprovalDialog.tsx
git commit -m "feat(workflow-fe): surface server-side form validation errors inline"
```

---

## Task 9: Live QA — standard post-feature testing workflow

**Files:** (test-app; nothing committed in this repo)

This task follows the project's standard post-feature testing workflow (see `CLAUDE.md`'s "Post-Feature Testing Workflow" section).

- [ ] **Step 1: Check free ports**

Run: `lsof -iTCP -sTCP:LISTEN -nP | awk '{print $9}' | grep -oE '[0-9]+$' | sort -un`
Pick an unused pair: 5100/3100 or 5200/3200 or 5300/3300.

- [ ] **Step 2: Create rename'd test app**

Run (from worktree root): `scripts/rename.ps1 -Name "_testWfPhase4a" -OutputDir "." -Modules "All" -IncludeMobile:$false`

- [ ] **Step 3: Reconfigure**

Follow the standard CLAUDE.md rename post-config: fix seed email (strip `_` prefix), fix bucket name, update ports in launchSettings.json, update CORS + FrontendUrl + BaseUrl, create `.env` for FE, 10x rate limits for testing.

- [ ] **Step 4: Generate migrations for all 8 DbContexts**

Per CLAUDE.md post-feature workflow: Core + 7 modules, each requires `dotnet ef migrations add Init --project <project> --startup-project <api> --context <context> --no-build`.

- [ ] **Step 5: Build + start**

```bash
cd _testWfPhase4a/_testWfPhase4a-BE && dotnet build && dotnet run
# separate terminal
cd _testWfPhase4a/_testWfPhase4a-FE && npm install && npx vite --port <FE_PORT>
```

- [ ] **Step 6: Seed a form-bearing definition**

Via the API (or DB insert) create a tenant-scoped workflow definition whose `AwaitingApproval` state declares a `FormFields: [{ name: 'amount', type: 'number', required: true, min: 0 }]` and has two conditional transitions (mirror the integration test). Start one instance assigned to `acme.admin@acme.com`.

- [ ] **Step 7: Live verification via chrome-devtools-mcp**

1. `new_page` → navigate to `http://localhost:<FE_PORT>/login`
2. Log in as `acme.admin@acme.com` / `Admin@123456`
3. Navigate to the workflow inbox
4. Click the pending task → opens `ApprovalDialog` showing `DynamicFormRenderer` for the `amount` field
5. Submit `amount = -5` → click Approve
6. **Assert:** dialog stays open; red error text "'Amount' must be at least 0." appears under the `amount` input
7. Clear the error by typing `amount = 15000` → click Approve
8. **Assert:** dialog closes; inbox refreshes; task is gone. Open instance detail.
9. **Assert:** instance `CurrentState == SeniorReview` (the high-amount branch taken). The `WorkflowStepTimeline` shows the submitted `amount: 15000` under the Awaiting Approval step's `FormDataDisplay`.

- [ ] **Step 8: Take screenshots for PR**

Capture: (a) inline field error on invalid submit, (b) successful submit + branch routing, (c) form data shown in timeline.

- [ ] **Step 9: Leave test app running** (do NOT tear down until user confirms)

Report URLs to the user for manual QA.

- [ ] **Step 10: No commits in this task** — live QA produces no source changes.

---

## Task 10: Feature documentation — `docs/features/workflow-forms.md`

**Files:**
- Create: `docs/features/workflow-forms.md`

- [ ] **Step 1: Create the doc**

Create `docs/features/workflow-forms.md`:

```markdown
# Workflow Dynamic Forms

State definitions can declare form fields that the assignee must submit when executing a task. Submitted values are merged into the instance's `ContextJson` and become available for conditional transitions in the same step or any subsequent step.

## When to use

Reach for dynamic forms when a workflow decision depends on structured data the assignee provides at approval time. Typical examples:

- **Amount-based routing** — collect `amount: number` and branch to a senior reviewer when amount exceeds a threshold.
- **Rejection reasons** — collect `rejectionReason: textarea` so history records why the request was declined.
- **Category classification** — collect `category: select(A|B|C)` and route to different downstream states.

## Supported field types (v1)

| Type | Renders as | Validation |
|---|---|---|
| `text` | single-line `<Input>` | `maxLength` |
| `textarea` | multi-line `<Textarea>` | `maxLength` |
| `number` | `<Input type="number">` | `min` / `max` |
| `date` | `<Input type="date">` | must parse as date |
| `select` | `<Select>` with `options[]` | value must be one of `options` |
| `checkbox` | native checkbox | required means must be `true` |

All field types support: `required` (boolean), `label` (display label), `description` (help text), `placeholder` (input hint).

## Schema

A state declares fields via `WorkflowStateConfig.FormFields`:

```csharp
new WorkflowStateConfig(
    Name: "AwaitingApproval",
    DisplayName: "Awaiting Approval",
    Type: "HumanTask",
    Actions: new() { "approve", "reject" },
    FormFields: new() {
        new FormFieldDefinition("amount", "Amount", "number",
            Required: true, Min: 0, Max: 1_000_000),
        new FormFieldDefinition("category", "Category", "select",
            Required: true,
            Options: new() {
                new("travel", "Travel"),
                new("equipment", "Equipment"),
                new("software", "Software"),
            }),
        new FormFieldDefinition("justification", "Justification", "textarea",
            MaxLength: 500),
    })
```

## Conditional branching

After a task executes successfully, the submitted form data merges into `WorkflowInstance.ContextJson`:

```json
{ "amount": 15000, "category": "equipment", "justification": "Replacement laptop" }
```

Subsequent transitions (or the transition just evaluated for multi-match selection) can reference any merged field:

```csharp
new WorkflowTransitionConfig(
    From: "AwaitingApproval",
    To: "SeniorReview",
    Trigger: "approve",
    Condition: new ConditionConfig(Field: "amount", Operator: ">", Value: 10000))
```

When multiple transitions match a trigger, the engine evaluates conditional ones first via `IAutoTransitionEvaluator` and falls back to an unconditional match.

## Validation

The engine runs `IFormDataValidator.Validate` before completing the task. Failures produce a `Result.ValidationFailure<bool>` carrying `ValidationErrors` (field → message[]). The controller returns HTTP 400 with body:

```json
{
  "success": false,
  "validationErrors": {
    "amount": ["'Amount' must be at least 0."]
  }
}
```

The FE `ApprovalDialog` extracts this into the `DynamicFormRenderer.errors` prop and renders the message inline under the field. The task is not completed; the instance state is unchanged.

## Authoring workflow definitions (interim)

Until the visual workflow designer ships in Phase 4c, form fields are authored via:

1. **Template seeding** — define a `WorkflowTemplateConfig` in module code and call `IWorkflowService.SeedTemplateAsync` during DataSeeder execution. See existing template seeds for patterns.
2. **Clone + JSON edit** — clone a system template via `POST /api/v1/workflows/definitions/{id}/clone`, then patch the `statesJson` directly. The definition detail page shows field definitions as read-only badges for review.

## Rendering in history

The `WorkflowStepTimeline` component renders submitted form data via `FormDataDisplay` — key-value pairs appear under the step where the data was captured. Null / empty values are skipped.

## Limitations (tracked in roadmap)

See `docs/roadmaps/workflow.md` → "Phase 4+ Deferred — Forms" for scheduled additions:

- Multiselect, file upload, array-of-object (repeating fieldsets).
- Conditional field visibility (show/hide based on other field values).
- Default values and server-computed placeholders.
- Multi-step forms within a single task.
- Admin authoring UX (ships with 4c visual designer).
```

- [ ] **Step 2: Commit**

```bash
git add docs/features/workflow-forms.md
git commit -m "docs(workflow): add dynamic-forms feature documentation"
```

---

## Task 11: Roadmap — Phase 4a Shipped + Phase 4+ Deferred Forms

**Files:**
- Modify: `docs/roadmaps/workflow.md`

- [ ] **Step 1: Read current roadmap structure**

Open `docs/roadmaps/workflow.md` and identify:
- The existing "Phase 3 Shipped (merged 2026-04-22)" section (added during Phase 3 polish).
- The existing "Phase 4+ Deferred Items" section.

- [ ] **Step 2: Add the "Phase 4a Shipped" section**

Immediately after the "Phase 3 Shipped" section, add:

```markdown
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
```

Replace `YYYY-MM-DD` with the actual merge date during the final-polish pass of the PR.

- [ ] **Step 3: Add the "Phase 4+ Deferred — Forms" subsection**

Under the existing "Phase 4+ Deferred Items" heading, add a Forms subsection:

```markdown
### Forms (deferred from Phase 4a)

- **Multiselect field type** — `type: "multiselect"` with `options[]` and multi-value persistence.
- **File upload field type** — requires signed-URL plumbing into `ContextJson`, retention policy, quota enforcement, and virus-scan hook design before it can ship.
- **Array-of-object field type** — repeating fieldsets (e.g. "list of line items"), each rendered as a nested `FormFieldDefinition[]`.
- **Conditional field visibility** — show/hide a field based on another field's value (e.g. show `rejectionReason` only when `approved == false`).
- **Default values / server-computed placeholders** — e.g. pre-populate `reviewerName` with the current user's display name.
- **Multi-step forms within a single task** — today one form per task; step-by-step wizards are an authoring concern.
- **Admin authoring UX** — planned to ship with the Phase 4c visual workflow designer.
```

- [ ] **Step 4: Commit**

```bash
git add docs/roadmaps/workflow.md
git commit -m "docs(workflow): move 4a into Shipped; record deferred form-field types"
```

---

## Task 12: Final suite + build verification

**Files:** (verification only)

- [ ] **Step 1: Run full BE test suite**

Run: `cd boilerplateBE && dotnet test`
Expected: PASS — entire .NET test suite including Workflow tests, ExecuteTaskResultShapeTests, FormDataBranchingIntegrationTests.

- [ ] **Step 2: Run FE build**

Run: `cd boilerplateFE && npm run build`
Expected: PASS — TypeScript compiles and Vite builds without errors or warnings.

- [ ] **Step 3: Run FE lint**

Run: `cd boilerplateFE && npm run lint`
Expected: PASS — zero ESLint errors.

- [ ] **Step 4: Spot-check git log**

Run: `git log --oneline -15`
Expected: Commits for Tasks 1, 5, 6, 7, 8, 10, 11 (≈7 commits). Tasks 2-4 rolled into Task 5's combined commit per its Step 6.

- [ ] **Step 5: No commit — this is a gate, not a change**

---

## Self-Review Notes

**Spec coverage check:**
- ✅ Validation error surfacing bug fix — Tasks 1, 3, 4, 5
- ✅ Field-level server errors to FE — Tasks 4, 8
- ✅ E2E integration test — Task 7
- ✅ Result shape tests — Task 6
- ✅ Documentation (`docs/features/workflow-forms.md`) — Task 10
- ✅ Roadmap updates (4a Shipped + deferred forms) — Task 11
- ✅ i18n — pre-audited; keys already present across en/ar/ku, no task needed
- ✅ Engine `return false` audit — locked above tasks, implemented in Task 3
- ✅ Out-of-scope non-goals — respected (no admin authoring UX, no new field types, no Vitest setup)

**Type consistency check:**
- `Result<bool>` shape used consistently across interface, engine, handlers, tests
- `ValidationErrors` (shared Results type) used — not a new custom type
- `extractValidationErrors` naming consistent between creation (Task 8 Step 1) and import (Task 8 Step 2)
- `WorkflowErrors.TaskNotPending`, `WorkflowErrors.Concurrency` factory names consistent between Task 1 and Task 3's replacement blocks

**Placeholder scan:** No TBD / TODO / "implement later" / "similar to" patterns. Every step has concrete code or commands.
