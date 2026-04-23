# Workflow Phase 4c — Visual Workflow Designer (MVP) — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ship a drag-and-drop visual workflow designer (MVP — option B) at `/workflows/definitions/:id/designer` that produces the same `WorkflowStateConfig[]` + `WorkflowTransitionConfig[]` JSON the backend already accepts, with JSON-block escape hatches for advanced fields.

**Architecture:** Pure frontend feature plus one additive BE change (optional `UiPosition` on `WorkflowStateConfig`) so node positions persist. Built on `@xyflow/react` (React Flow v12) with `dagre` auto-layout, `zustand` for local designer state, and `zod` mirroring a new `UpdateDefinitionCommandValidator` (FluentValidation) on the BE.

**Tech Stack:** React 19, TypeScript, `@xyflow/react`, `dagre`, `zustand`, `zod`, `react-hook-form` (existing), Tailwind + shadcn/ui. .NET 10, FluentValidation, xUnit, FluentAssertions.

**Spec:** [`docs/superpowers/specs/2026-04-23-workflow-phase4c-visual-designer-design.md`](../specs/2026-04-23-workflow-phase4c-visual-designer-design.md)

---

## Testing strategy

- **Backend:** TDD with xUnit + FluentAssertions. Existing pattern in `tests/Starter.Api.Tests/Workflow/`.
- **Frontend:** **No FE unit-test infrastructure exists in this project.** FE correctness is validated via `npm run build` (tsc + vite) and `npm run lint` (eslint), plus manual QA via Playwright MCP / Chrome DevTools MCP against a rename'd test app per `.claude/skills/post-feature-testing.md`. Do **not** introduce vitest in this plan — it's out of scope.

## File structure — what lives where

**Backend (2 files touched, 2 created):**
- `boilerplateBE/src/Starter.Abstractions/Capabilities/WorkflowConfigRecords.cs` — add `UiPosition` record + optional field on `WorkflowStateConfig`.
- `boilerplateBE/src/modules/Starter.Module.Workflow/Application/Commands/UpdateDefinition/UpdateDefinitionCommandValidator.cs` — **new**.
- `boilerplateBE/tests/Starter.Api.Tests/Workflow/UpdateDefinitionCommandValidatorTests.cs` — **new**.

**Frontend (new files under `src/features/workflow/components/designer/`):**
```
DesignerCanvas.tsx         — React Flow shell; registers custom node + edge types
StateNode.tsx              — custom node component
TransitionEdge.tsx         — custom edge component
DesignerToolbar.tsx        — Save, Auto-layout, Add State, validation summary
SidePanel.tsx              — routes between State / Transition / Empty editors based on selection
StateEditor.tsx            — first-class visual fields + JSON blocks for advanced
TransitionEditor.tsx       — visual trigger/type + JSON block for condition
JsonBlockField.tsx         — reusable textarea-with-parse-errors
hooks/useDesignerStore.ts  — zustand: nodes, edges, dirty, selection, serialization
hooks/useAutoLayout.ts     — dagre top-to-bottom layout wrapper
validation/designerSchema.ts — zod mirror of BE validator rules
```

**Frontend (touched):**
- `boilerplateFE/src/types/workflow.types.ts` — add `uiPosition?: { x: number; y: number }` to `WorkflowStateConfig`.
- `boilerplateFE/src/config/routes.config.ts` — add `DESIGNER` path + `getDesigner` helper.
- `boilerplateFE/src/routes/routes.tsx` — lazy route with `PermissionGuard`.
- `boilerplateFE/src/features/workflow/pages/WorkflowDefinitionDetailPage.tsx` — add "Open Designer" button.
- `boilerplateFE/src/features/workflow/pages/WorkflowDefinitionDesignerPage.tsx` — **new**.
- `boilerplateFE/src/i18n/locales/en/translation.json` (+ ar, ku) — `workflow.designer.*` keys.
- `boilerplateFE/package.json` — add `@xyflow/react` and `dagre`.

**Docs:**
- `docs/roadmaps/workflow.md` — move Phase 4c to "Shipped"; add 9 new deferred entries.
- `docs/features/workflow-designer.md` — **new** feature page.

---

## Task 0: Sync with `origin/main`

**Purpose:** Ensure this branch includes anything that landed on main since we branched.

- [ ] **Step 1: Fetch**

Run from the worktree root:
```bash
cd /Users/samanjasim/Projects/forme/Boilerplate-CQRS/.claude/worktrees/wf-phase4c
git fetch origin main
```

- [ ] **Step 2: Check divergence**

```bash
git log --oneline HEAD..origin/main | head -10
```

Expected: either empty (up to date) or a list of commits on main.

- [ ] **Step 3: Merge if needed**

If there are commits:
```bash
git merge origin/main --no-edit
```

Resolve conflicts if any (unlikely — Phase 4c touches mostly new files). Then:
```bash
dotnet build boilerplateBE/Starter.sln --nologo 2>&1 | tail -5
cd boilerplateFE && npm install && npm run build && npm run lint && cd ..
```

Expected: backend `0 Error(s)`, FE build succeeds, lint clean.

---

## Task 1: Backend — add `UiPosition` record + field

**Files:**
- Modify: `boilerplateBE/src/Starter.Abstractions/Capabilities/WorkflowConfigRecords.cs`

**Purpose:** Let the designer persist node positions inside `statesJson`. Additive-only — existing data deserializes unchanged because the field is nullable.

- [ ] **Step 1: Add `UiPosition` record and field**

Edit `boilerplateBE/src/Starter.Abstractions/Capabilities/WorkflowConfigRecords.cs`. Change `WorkflowStateConfig` and add `UiPosition` after `SlaConfig`:

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
    SlaConfig? Sla = null,
    UiPosition? UiPosition = null);

public sealed record SlaConfig(
    int? ReminderAfterHours = null,
    int? EscalateAfterHours = null);

public sealed record UiPosition(double X, double Y);
```

- [ ] **Step 2: Build backend**

```bash
dotnet build boilerplateBE/Starter.sln --nologo 2>&1 | tail -5
```

Expected: `0 Error(s)`.

- [ ] **Step 3: Run existing abstractions purity test**

```bash
dotnet test boilerplateBE/tests/Starter.Api.Tests/Starter.Api.Tests.csproj --no-build --filter "FullyQualifiedName~AbstractionsPurity" -l "console;verbosity=minimal" 2>&1 | tail -5
```

Expected: `Passed`. Confirms no accidental dependency added to the abstractions layer.

- [ ] **Step 4: Commit**

```bash
git add boilerplateBE/src/Starter.Abstractions/Capabilities/WorkflowConfigRecords.cs
git commit -m "feat(workflow): add optional UiPosition to WorkflowStateConfig"
```

---

## Task 2: Backend — `UpdateDefinitionCommandValidator` state rules (TDD)

**Files:**
- Create: `boilerplateBE/src/modules/Starter.Module.Workflow/Application/Commands/UpdateDefinition/UpdateDefinitionCommandValidator.cs`
- Create: `boilerplateBE/tests/Starter.Api.Tests/Workflow/UpdateDefinitionCommandValidatorTests.cs`

**Purpose:** Single-source-of-truth validation for designer saves. Surfaces slug format, uniqueness, type membership, and "exactly one Initial / at least one Terminal" errors via the existing `Result<T>` pipeline.

- [ ] **Step 1: Write failing tests for state-level rules**

Create `boilerplateBE/tests/Starter.Api.Tests/Workflow/UpdateDefinitionCommandValidatorTests.cs`:

```csharp
using System.Text.Json;
using FluentAssertions;
using Starter.Abstractions.Capabilities;
using Starter.Module.Workflow.Application.Commands.UpdateDefinition;
using Xunit;

namespace Starter.Api.Tests.Workflow;

public sealed class UpdateDefinitionCommandValidatorTests
{
    private readonly UpdateDefinitionCommandValidator _sut = new();

    private static string StatesJson(params WorkflowStateConfig[] states) =>
        JsonSerializer.Serialize(states.ToList());

    private static string TransitionsJson(params WorkflowTransitionConfig[] t) =>
        JsonSerializer.Serialize(t.ToList());

    [Fact]
    public void Passes_when_payload_has_one_initial_one_terminal_and_no_transitions()
    {
        var cmd = new UpdateDefinitionCommand(
            Guid.NewGuid(),
            DisplayName: "ok",
            Description: null,
            StatesJson: StatesJson(
                new("Start", "Start", "Initial"),
                new("Done", "Done", "Terminal")),
            TransitionsJson: TransitionsJson());

        var result = _sut.Validate(cmd);

        result.IsValid.Should().BeTrue(because: string.Join(", ", result.Errors.Select(e => e.ErrorMessage)));
    }

    [Fact]
    public void Fails_when_state_name_is_not_a_slug()
    {
        var cmd = new UpdateDefinitionCommand(Guid.NewGuid(), "ok", null,
            StatesJson(
                new("bad name", "Bad", "Initial"),
                new("Done", "Done", "Terminal")),
            TransitionsJson());

        var result = _sut.Validate(cmd);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("slug"));
    }

    [Fact]
    public void Fails_when_state_names_are_duplicated()
    {
        var cmd = new UpdateDefinitionCommand(Guid.NewGuid(), "ok", null,
            StatesJson(
                new("Start", "Start", "Initial"),
                new("Start", "Other", "Terminal")),
            TransitionsJson());

        var result = _sut.Validate(cmd);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("unique"));
    }

    [Fact]
    public void Fails_when_type_is_unknown()
    {
        var cmd = new UpdateDefinitionCommand(Guid.NewGuid(), "ok", null,
            StatesJson(
                new("Start", "Start", "Initial"),
                new("Weird", "Weird", "Bogus"),
                new("Done", "Done", "Terminal")),
            TransitionsJson());

        var result = _sut.Validate(cmd);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("Initial") && e.ErrorMessage.Contains("Terminal"));
    }

    [Fact]
    public void Fails_when_there_is_no_initial_state()
    {
        var cmd = new UpdateDefinitionCommand(Guid.NewGuid(), "ok", null,
            StatesJson(
                new("Mid", "Mid", "HumanTask"),
                new("Done", "Done", "Terminal")),
            TransitionsJson());

        var result = _sut.Validate(cmd);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("exactly one") && e.ErrorMessage.Contains("Initial"));
    }

    [Fact]
    public void Fails_when_there_are_two_initial_states()
    {
        var cmd = new UpdateDefinitionCommand(Guid.NewGuid(), "ok", null,
            StatesJson(
                new("A", "A", "Initial"),
                new("B", "B", "Initial"),
                new("Done", "Done", "Terminal")),
            TransitionsJson());

        var result = _sut.Validate(cmd);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("exactly one"));
    }

    [Fact]
    public void Fails_when_there_is_no_terminal_state()
    {
        var cmd = new UpdateDefinitionCommand(Guid.NewGuid(), "ok", null,
            StatesJson(
                new("Start", "Start", "Initial"),
                new("Middle", "Middle", "HumanTask")),
            TransitionsJson());

        var result = _sut.Validate(cmd);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("Terminal"));
    }

    [Fact]
    public void Fails_when_humantask_has_no_assignee()
    {
        var cmd = new UpdateDefinitionCommand(Guid.NewGuid(), "ok", null,
            StatesJson(
                new("Start", "Start", "Initial"),
                new("Review", "Review", "HumanTask"),
                new("Done", "Done", "Terminal")),
            TransitionsJson());

        var result = _sut.Validate(cmd);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("assignee"));
    }

    [Fact]
    public void Fails_when_sla_reminder_ge_escalate()
    {
        var cmd = new UpdateDefinitionCommand(Guid.NewGuid(), "ok", null,
            StatesJson(
                new("Start", "Start", "Initial"),
                new("Review", "Review", "HumanTask",
                    Assignee: new("Role", new() { ["roleName"] = "Admin" }),
                    Sla: new(ReminderAfterHours: 8, EscalateAfterHours: 4)),
                new("Done", "Done", "Terminal")),
            TransitionsJson());

        var result = _sut.Validate(cmd);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("reminder") && e.ErrorMessage.Contains("escalate"));
    }

    [Fact]
    public void Fails_when_displayName_is_empty()
    {
        var cmd = new UpdateDefinitionCommand(Guid.NewGuid(), "", null, null, null);

        var result = _sut.Validate(cmd);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "DisplayName");
    }
}
```

- [ ] **Step 2: Run tests — expect compile failure (validator file doesn't exist)**

```bash
dotnet test boilerplateBE/tests/Starter.Api.Tests/Starter.Api.Tests.csproj --no-build --filter "FullyQualifiedName~UpdateDefinitionCommandValidator" -l "console;verbosity=minimal" 2>&1 | tail -10
```

Expected: build error — `UpdateDefinitionCommandValidator` type does not exist.

- [ ] **Step 3: Create the validator**

Create `boilerplateBE/src/modules/Starter.Module.Workflow/Application/Commands/UpdateDefinition/UpdateDefinitionCommandValidator.cs`:

```csharp
using System.Text.Json;
using System.Text.RegularExpressions;
using FluentValidation;
using Starter.Abstractions.Capabilities;

namespace Starter.Module.Workflow.Application.Commands.UpdateDefinition;

public sealed partial class UpdateDefinitionCommandValidator : AbstractValidator<UpdateDefinitionCommand>
{
    private static readonly string[] KnownTypes = ["Initial", "HumanTask", "SystemAction", "Terminal"];

    public UpdateDefinitionCommandValidator()
    {
        RuleFor(x => x.DisplayName)
            .NotEmpty().WithMessage("DisplayName is required.")
            .MaximumLength(120);

        When(x => x.StatesJson is not null, () =>
        {
            RuleFor(x => x.StatesJson!)
                .Custom((statesJson, ctx) => ValidateStates(statesJson, ctx));
        });
    }

    private static void ValidateStates(string statesJson, ValidationContext<UpdateDefinitionCommand> ctx)
    {
        List<WorkflowStateConfig>? states;
        try
        {
            states = JsonSerializer.Deserialize<List<WorkflowStateConfig>>(statesJson,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (JsonException ex)
        {
            ctx.AddFailure(nameof(UpdateDefinitionCommand.StatesJson),
                $"StatesJson is not valid JSON: {ex.Message}");
            return;
        }

        if (states is null || states.Count == 0)
        {
            ctx.AddFailure(nameof(UpdateDefinitionCommand.StatesJson), "At least one state is required.");
            return;
        }

        // Per-state rules
        foreach (var (state, index) in states.Select((s, i) => (s, i)))
        {
            var prefix = $"states[{index}]";

            if (string.IsNullOrWhiteSpace(state.Name))
                ctx.AddFailure($"{prefix}.name", "State name is required.");
            else if (!SlugRegex().IsMatch(state.Name))
                ctx.AddFailure($"{prefix}.name",
                    $"State name '{state.Name}' must be a slug (letters, digits, underscore; starts with a letter).");
            else if (state.Name.Length > 80)
                ctx.AddFailure($"{prefix}.name", "State name must be 80 characters or fewer.");

            if (string.IsNullOrWhiteSpace(state.DisplayName))
                ctx.AddFailure($"{prefix}.displayName", "State displayName is required.");
            else if (state.DisplayName.Length > 120)
                ctx.AddFailure($"{prefix}.displayName", "State displayName must be 120 characters or fewer.");

            if (!KnownTypes.Contains(state.Type, StringComparer.OrdinalIgnoreCase))
                ctx.AddFailure($"{prefix}.type",
                    $"State type '{state.Type}' is not one of Initial, HumanTask, SystemAction, Terminal.");

            if (state.Type.Equals("HumanTask", StringComparison.OrdinalIgnoreCase)
                && (state.Assignee is null || string.IsNullOrWhiteSpace(state.Assignee.Strategy)))
            {
                ctx.AddFailure($"{prefix}.assignee",
                    $"State '{state.Name}' is HumanTask but has no assignee strategy.");
            }

            if (state.Sla is { ReminderAfterHours: { } reminder, EscalateAfterHours: { } escalate }
                && reminder >= escalate)
            {
                ctx.AddFailure($"{prefix}.sla",
                    $"State '{state.Name}' SLA: reminder hours ({reminder}) must be less than escalate hours ({escalate}).");
            }
        }

        // Uniqueness
        var duplicateNames = states
            .GroupBy(s => s.Name, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();
        foreach (var dup in duplicateNames)
            ctx.AddFailure(nameof(UpdateDefinitionCommand.StatesJson),
                $"State name '{dup}' must be unique within the definition.");

        // Exactly one Initial
        var initialCount = states.Count(s => s.Type.Equals("Initial", StringComparison.OrdinalIgnoreCase));
        if (initialCount != 1)
            ctx.AddFailure(nameof(UpdateDefinitionCommand.StatesJson),
                $"A definition must have exactly one Initial state (found {initialCount}).");

        // At least one Terminal
        var terminalCount = states.Count(s => s.Type.Equals("Terminal", StringComparison.OrdinalIgnoreCase));
        if (terminalCount == 0)
            ctx.AddFailure(nameof(UpdateDefinitionCommand.StatesJson),
                "A definition must have at least one Terminal state.");
    }

    [GeneratedRegex(@"^[A-Za-z][A-Za-z0-9_]*$", RegexOptions.CultureInvariant)]
    private static partial Regex SlugRegex();
}
```

- [ ] **Step 4: Run tests — expect pass**

```bash
dotnet test boilerplateBE/tests/Starter.Api.Tests/Starter.Api.Tests.csproj --filter "FullyQualifiedName~UpdateDefinitionCommandValidator" -l "console;verbosity=minimal" 2>&1 | tail -5
```

Expected: `Passed! ... Total: 10`.

- [ ] **Step 5: Commit**

```bash
git add boilerplateBE/src/modules/Starter.Module.Workflow/Application/Commands/UpdateDefinition/UpdateDefinitionCommandValidator.cs \
        boilerplateBE/tests/Starter.Api.Tests/Workflow/UpdateDefinitionCommandValidatorTests.cs
git commit -m "feat(workflow): add UpdateDefinitionCommandValidator — state rules"
```

---

## Task 3: Backend — validator transition rules (TDD)

**Files:**
- Modify: `boilerplateBE/src/modules/Starter.Module.Workflow/Application/Commands/UpdateDefinition/UpdateDefinitionCommandValidator.cs`
- Modify: `boilerplateBE/tests/Starter.Api.Tests/Workflow/UpdateDefinitionCommandValidatorTests.cs`

- [ ] **Step 1: Write failing transition tests**

Append these tests to `UpdateDefinitionCommandValidatorTests.cs`:

```csharp
[Fact]
public void Fails_when_transition_from_references_unknown_state()
{
    var cmd = new UpdateDefinitionCommand(Guid.NewGuid(), "ok", null,
        StatesJson(
            new("Start", "Start", "Initial"),
            new("Done", "Done", "Terminal")),
        TransitionsJson(new("Ghost", "Done", "Go")));

    var result = _sut.Validate(cmd);

    result.IsValid.Should().BeFalse();
    result.Errors.Should().Contain(e => e.ErrorMessage.Contains("from") && e.ErrorMessage.Contains("Ghost"));
}

[Fact]
public void Fails_when_transition_to_references_unknown_state()
{
    var cmd = new UpdateDefinitionCommand(Guid.NewGuid(), "ok", null,
        StatesJson(
            new("Start", "Start", "Initial"),
            new("Done", "Done", "Terminal")),
        TransitionsJson(new("Start", "Ghost", "Go")));

    var result = _sut.Validate(cmd);

    result.IsValid.Should().BeFalse();
    result.Errors.Should().Contain(e => e.ErrorMessage.Contains("to") && e.ErrorMessage.Contains("Ghost"));
}

[Fact]
public void Fails_when_trigger_is_empty()
{
    var cmd = new UpdateDefinitionCommand(Guid.NewGuid(), "ok", null,
        StatesJson(
            new("Start", "Start", "Initial"),
            new("Done", "Done", "Terminal")),
        TransitionsJson(new("Start", "Done", "")));

    var result = _sut.Validate(cmd);

    result.IsValid.Should().BeFalse();
    result.Errors.Should().Contain(e => e.ErrorMessage.Contains("trigger"));
}

[Fact]
public void Fails_when_transition_originates_from_a_terminal_state()
{
    var cmd = new UpdateDefinitionCommand(Guid.NewGuid(), "ok", null,
        StatesJson(
            new("Start", "Start", "Initial"),
            new("Done", "Done", "Terminal"),
            new("AlsoDone", "AlsoDone", "Terminal")),
        TransitionsJson(new("Done", "AlsoDone", "X")));

    var result = _sut.Validate(cmd);

    result.IsValid.Should().BeFalse();
    result.Errors.Should().Contain(e => e.ErrorMessage.Contains("Terminal"));
}

[Fact]
public void Fails_when_same_from_and_trigger_pair_appears_twice()
{
    var cmd = new UpdateDefinitionCommand(Guid.NewGuid(), "ok", null,
        StatesJson(
            new("Start", "Start", "Initial"),
            new("A", "A", "Terminal"),
            new("B", "B", "Terminal")),
        TransitionsJson(
            new("Start", "A", "Go"),
            new("Start", "B", "Go")));

    var result = _sut.Validate(cmd);

    result.IsValid.Should().BeFalse();
    result.Errors.Should().Contain(e => e.ErrorMessage.Contains("duplicate"));
}
```

- [ ] **Step 2: Run — expect failures**

```bash
dotnet test boilerplateBE/tests/Starter.Api.Tests/Starter.Api.Tests.csproj --filter "FullyQualifiedName~UpdateDefinitionCommandValidator" -l "console;verbosity=minimal" 2>&1 | tail -8
```

Expected: 5 of the new tests fail.

- [ ] **Step 3: Add transition validation to the validator**

Edit `UpdateDefinitionCommandValidator.cs`. Inside the constructor, after the existing `When` block for `StatesJson`, add:

```csharp
When(x => x.StatesJson is not null && x.TransitionsJson is not null, () =>
{
    RuleFor(x => x).Custom((cmd, ctx) => ValidateTransitions(cmd.StatesJson!, cmd.TransitionsJson!, ctx));
});
```

Then add the `ValidateTransitions` method to the class:

```csharp
private static void ValidateTransitions(
    string statesJson,
    string transitionsJson,
    ValidationContext<UpdateDefinitionCommand> ctx)
{
    List<WorkflowStateConfig>? states;
    List<WorkflowTransitionConfig>? transitions;

    var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
    try
    {
        states = JsonSerializer.Deserialize<List<WorkflowStateConfig>>(statesJson, opts);
        transitions = JsonSerializer.Deserialize<List<WorkflowTransitionConfig>>(transitionsJson, opts);
    }
    catch (JsonException ex)
    {
        ctx.AddFailure(nameof(UpdateDefinitionCommand.TransitionsJson),
            $"TransitionsJson is not valid JSON: {ex.Message}");
        return;
    }

    if (states is null || transitions is null) return;

    var stateByName = states.ToDictionary(s => s.Name, s => s, StringComparer.OrdinalIgnoreCase);

    foreach (var (t, index) in transitions.Select((t, i) => (t, i)))
    {
        var prefix = $"transitions[{index}]";

        if (string.IsNullOrWhiteSpace(t.Trigger))
            ctx.AddFailure($"{prefix}.trigger", "Transition trigger is required.");

        if (!stateByName.TryGetValue(t.From, out var fromState))
            ctx.AddFailure($"{prefix}.from",
                $"Transition from '{t.From}' references an unknown state.");
        else if (fromState.Type.Equals("Terminal", StringComparison.OrdinalIgnoreCase))
            ctx.AddFailure($"{prefix}.from",
                $"Transition cannot originate from Terminal state '{t.From}'.");

        if (!stateByName.ContainsKey(t.To))
            ctx.AddFailure($"{prefix}.to",
                $"Transition to '{t.To}' references an unknown state.");
    }

    // Duplicate (from, trigger) pairs
    var duplicates = transitions
        .Where(t => !string.IsNullOrWhiteSpace(t.Trigger))
        .GroupBy(t => (t.From, t.Trigger), TupleEqualityComparer)
        .Where(g => g.Count() > 1)
        .Select(g => g.Key)
        .ToList();

    foreach (var (from, trigger) in duplicates)
        ctx.AddFailure(nameof(UpdateDefinitionCommand.TransitionsJson),
            $"Transitions have duplicate (from='{from}', trigger='{trigger}') pair.");
}

private static readonly IEqualityComparer<(string From, string Trigger)> TupleEqualityComparer =
    EqualityComparer<(string From, string Trigger)>.Create(
        (a, b) => StringComparer.OrdinalIgnoreCase.Equals(a.From, b.From)
                  && StringComparer.OrdinalIgnoreCase.Equals(a.Trigger, b.Trigger),
        t => HashCode.Combine(
            StringComparer.OrdinalIgnoreCase.GetHashCode(t.From),
            StringComparer.OrdinalIgnoreCase.GetHashCode(t.Trigger)));
```

- [ ] **Step 4: Run — expect all tests pass**

```bash
dotnet test boilerplateBE/tests/Starter.Api.Tests/Starter.Api.Tests.csproj --filter "FullyQualifiedName~UpdateDefinitionCommandValidator" -l "console;verbosity=minimal" 2>&1 | tail -5
```

Expected: `Passed! ... Total: 15`.

- [ ] **Step 5: Run full BE test suite — confirm no regressions**

```bash
dotnet test boilerplateBE/tests/Starter.Api.Tests/Starter.Api.Tests.csproj --no-build -l "console;verbosity=minimal" 2>&1 | tail -3
```

Expected: all existing tests still pass; validator brings total to previous + 15.

- [ ] **Step 6: Commit**

```bash
git add boilerplateBE/src/modules/Starter.Module.Workflow/Application/Commands/UpdateDefinition/UpdateDefinitionCommandValidator.cs \
        boilerplateBE/tests/Starter.Api.Tests/Workflow/UpdateDefinitionCommandValidatorTests.cs
git commit -m "feat(workflow): validator covers transition rules (from/to/trigger/terminal/duplicates)"
```

---

## Task 4: Frontend — install deps, add type, add route const, add i18n keys

**Files:**
- Modify: `boilerplateFE/package.json` + `package-lock.json`
- Modify: `boilerplateFE/src/types/workflow.types.ts`
- Modify: `boilerplateFE/src/config/routes.config.ts`
- Modify: `boilerplateFE/src/i18n/locales/en/translation.json`
- Modify: `boilerplateFE/src/i18n/locales/ar/translation.json`
- Modify: `boilerplateFE/src/i18n/locales/ku/translation.json`

- [ ] **Step 1: Install deps**

```bash
cd boilerplateFE
npm install @xyflow/react@^12 dagre@^0.8
npm install -D @types/dagre@^0.7
```

- [ ] **Step 2: Add `uiPosition` to `WorkflowStateConfig`**

Edit `boilerplateFE/src/types/workflow.types.ts`. Add the field to the interface:

```ts
export interface WorkflowStateConfig {
  name: string;
  displayName: string;
  type: string;
  assignee?: AssigneeConfig | null;
  actions?: string[] | null;
  onEnter?: HookConfig[] | null;
  onExit?: HookConfig[] | null;
  formFields?: FormFieldDefinition[] | null;
  parallel?: ParallelConfig | null;
  sla?: SlaConfig | null;
  uiPosition?: { x: number; y: number } | null;
}
```

- [ ] **Step 3: Add designer route constants**

Edit `boilerplateFE/src/config/routes.config.ts`. Inside the `WORKFLOWS` block add:

```ts
WORKFLOWS: {
  INBOX: '/workflows/inbox',
  INSTANCES: '/workflows/instances',
  INSTANCE_DETAIL: '/workflows/instances/:id',
  getInstanceDetail: (id: string) => `/workflows/instances/${id}`,
  DEFINITIONS: '/workflows/definitions',
  DEFINITION_DETAIL: '/workflows/definitions/:id',
  getDefinitionDetail: (id: string) => `/workflows/definitions/${id}`,
  DEFINITION_DESIGNER: '/workflows/definitions/:id/designer',
  getDefinitionDesigner: (id: string) => `/workflows/definitions/${id}/designer`,
},
```

- [ ] **Step 4: Add `workflow.designer.*` i18n keys (en)**

Edit `boilerplateFE/src/i18n/locales/en/translation.json`. Locate the existing `"workflow"` object and add, nested under it:

```json
"designer": {
  "title": "Designer",
  "openDesigner": "Open Designer",
  "save": "Save",
  "saving": "Saving...",
  "addState": "Add State",
  "autoLayout": "Auto-layout",
  "undo": "Undo",
  "redo": "Redo",
  "unsavedChanges": "You have unsaved changes.",
  "unsavedWarningTitle": "Discard unsaved changes?",
  "unsavedWarningBody": "Your edits to this definition will be lost.",
  "discard": "Discard",
  "stay": "Stay",
  "emptySelection": "Click a state to edit, drag between states to connect, press Delete to remove.",
  "state": {
    "identity": "Identity",
    "name": "Name",
    "nameSlugHelp": "Slug: letters, digits, underscore; starts with a letter.",
    "displayName": "Display name",
    "type": "Type",
    "actions": "Actions",
    "actionPlaceholder": "Add action, press Enter",
    "assignee": "Assignee",
    "strategy": "Strategy",
    "strategySpecificUser": "Specific user",
    "strategyRole": "Role",
    "strategyEntityCreator": "Entity creator",
    "userId": "User ID",
    "roleName": "Role name",
    "sla": "SLA",
    "reminderAfterHours": "Remind after (hours)",
    "escalateAfterHours": "Escalate after (hours)",
    "advanced": "Advanced",
    "fallbackAssignee": "Fallback assignee",
    "customParameters": "Custom parameters",
    "onEnterHooks": "OnEnter hooks",
    "onExitHooks": "OnExit hooks",
    "formFields": "Form fields",
    "parallel": "Parallel / quorum"
  },
  "transition": {
    "trigger": "Trigger",
    "from": "From",
    "to": "To",
    "type": "Type",
    "typeManual": "Manual",
    "typeAuto": "Auto",
    "condition": "Condition"
  },
  "template": {
    "readOnlyTitle": "This is a system template",
    "readOnlyBody": "Clone this template to make edits.",
    "cloneToEdit": "Clone to edit"
  },
  "json": {
    "format": "Format",
    "reset": "Reset",
    "parseError": "Invalid JSON: {{message}}",
    "hintPlaceholder": "Paste JSON matching the shape shown above."
  },
  "errors": {
    "nameRequired": "Name is required.",
    "nameSlug": "Name must be a slug (letters, digits, underscore; starts with a letter).",
    "nameUnique": "State name must be unique.",
    "displayNameRequired": "Display name is required.",
    "typeUnknown": "Type must be Initial, HumanTask, SystemAction, or Terminal.",
    "exactlyOneInitial": "Exactly one Initial state is required (found {{count}}).",
    "atLeastOneTerminal": "At least one Terminal state is required.",
    "assigneeRequiredForHumanTask": "HumanTask requires an assignee strategy.",
    "slaOrder": "Reminder hours must be less than escalate hours.",
    "triggerRequired": "Trigger is required.",
    "fromUnknown": "Transition 'from' references an unknown state.",
    "toUnknown": "Transition 'to' references an unknown state.",
    "fromTerminal": "Transitions cannot originate from Terminal state.",
    "duplicateFromTrigger": "Another transition already has this (from, trigger) pair."
  }
}
```

- [ ] **Step 5: Mirror the same `designer` block in `ar/translation.json` and `ku/translation.json`**

Copy the entire `"designer": { ... }` block from `en/translation.json` into `boilerplateFE/src/i18n/locales/ar/translation.json` and `boilerplateFE/src/i18n/locales/ku/translation.json` at the equivalent nested location. Leave the strings in English; a translation pass is a follow-up item.

- [ ] **Step 6: Build + lint**

```bash
cd boilerplateFE
npm run build 2>&1 | tail -5
npm run lint 2>&1 | tail -5
cd ..
```

Expected: build succeeds, lint clean.

- [ ] **Step 7: Commit**

```bash
git add boilerplateFE/package.json boilerplateFE/package-lock.json \
        boilerplateFE/src/types/workflow.types.ts \
        boilerplateFE/src/config/routes.config.ts \
        boilerplateFE/src/i18n/locales/en/translation.json \
        boilerplateFE/src/i18n/locales/ar/translation.json \
        boilerplateFE/src/i18n/locales/ku/translation.json
git commit -m "feat(workflow-fe): add designer deps, types, route consts, i18n keys"
```

---

## Task 5: Frontend — `designerSchema.ts` (zod mirror of BE validator)

**Files:**
- Create: `boilerplateFE/src/features/workflow/components/designer/validation/designerSchema.ts`

**Purpose:** Client-side validation for early feedback. Source of truth is the BE validator — this mirror exists so the Save button can disable pre-emptively and show inline errors. Rules must match the BE 1:1.

- [ ] **Step 1: Create the schema file**

Create `boilerplateFE/src/features/workflow/components/designer/validation/designerSchema.ts`:

```ts
import { z } from 'zod';
import type { WorkflowStateConfig, WorkflowTransitionConfig } from '@/types/workflow.types';

const SLUG = /^[A-Za-z][A-Za-z0-9_]*$/;
const KNOWN_TYPES = ['Initial', 'HumanTask', 'SystemAction', 'Terminal'] as const;

export const stateSchema = z.object({
  name: z.string()
    .min(1, { message: 'nameRequired' })
    .max(80)
    .regex(SLUG, { message: 'nameSlug' }),
  displayName: z.string().min(1, { message: 'displayNameRequired' }).max(120),
  type: z.enum(KNOWN_TYPES, { message: 'typeUnknown' }),
  assignee: z.unknown().optional().nullable(),
  actions: z.array(z.string()).optional().nullable(),
  onEnter: z.unknown().optional().nullable(),
  onExit: z.unknown().optional().nullable(),
  formFields: z.unknown().optional().nullable(),
  parallel: z.unknown().optional().nullable(),
  sla: z.object({
    reminderAfterHours: z.number().nullable().optional(),
    escalateAfterHours: z.number().nullable().optional(),
  }).optional().nullable().refine(sla => {
    if (!sla) return true;
    const r = sla.reminderAfterHours;
    const e = sla.escalateAfterHours;
    if (r == null || e == null) return true;
    return r < e;
  }, { message: 'slaOrder' }),
  uiPosition: z.object({ x: z.number(), y: z.number() }).optional().nullable(),
});

export const transitionSchema = z.object({
  from: z.string().min(1),
  to: z.string().min(1),
  trigger: z.string().min(1, { message: 'triggerRequired' }),
  type: z.string().optional(),
  condition: z.unknown().optional().nullable(),
});

export interface ValidationIssue {
  path: string;        // e.g. "states[2].name" or "graph"
  messageKey: string;  // i18n key under workflow.designer.errors
  params?: Record<string, unknown>;
}

export function validateDefinition(
  states: WorkflowStateConfig[],
  transitions: WorkflowTransitionConfig[],
): ValidationIssue[] {
  const issues: ValidationIssue[] = [];

  // Per-state schema
  states.forEach((state, i) => {
    const result = stateSchema.safeParse(state);
    if (!result.success) {
      for (const issue of result.error.issues) {
        issues.push({
          path: `states[${i}].${issue.path.join('.')}`,
          messageKey: issue.message || 'unknown',
        });
      }
    }
    // HumanTask requires assignee strategy
    if (state.type === 'HumanTask') {
      const a = state.assignee;
      if (!a || !(a as { strategy?: string }).strategy) {
        issues.push({
          path: `states[${i}].assignee`,
          messageKey: 'assigneeRequiredForHumanTask',
        });
      }
    }
  });

  // Uniqueness
  const seenNames = new Set<string>();
  states.forEach((state, i) => {
    const key = state.name?.toLowerCase();
    if (!key) return;
    if (seenNames.has(key)) {
      issues.push({ path: `states[${i}].name`, messageKey: 'nameUnique' });
    }
    seenNames.add(key);
  });

  // Exactly one Initial
  const initialCount = states.filter(s => s.type === 'Initial').length;
  if (initialCount !== 1) {
    issues.push({ path: 'graph', messageKey: 'exactlyOneInitial', params: { count: initialCount } });
  }

  // At least one Terminal
  if (!states.some(s => s.type === 'Terminal')) {
    issues.push({ path: 'graph', messageKey: 'atLeastOneTerminal' });
  }

  // Transitions
  const stateByName = new Map(states.map(s => [s.name.toLowerCase(), s]));
  transitions.forEach((t, i) => {
    const result = transitionSchema.safeParse(t);
    if (!result.success) {
      for (const issue of result.error.issues) {
        issues.push({
          path: `transitions[${i}].${issue.path.join('.')}`,
          messageKey: issue.message || 'unknown',
        });
      }
    }

    const from = stateByName.get(t.from?.toLowerCase());
    if (!from) issues.push({ path: `transitions[${i}].from`, messageKey: 'fromUnknown' });
    else if (from.type === 'Terminal') issues.push({ path: `transitions[${i}].from`, messageKey: 'fromTerminal' });

    if (!stateByName.get(t.to?.toLowerCase())) {
      issues.push({ path: `transitions[${i}].to`, messageKey: 'toUnknown' });
    }
  });

  // Duplicate (from, trigger)
  const seenPairs = new Set<string>();
  transitions.forEach((t, i) => {
    if (!t.trigger) return;
    const key = `${t.from.toLowerCase()}::${t.trigger.toLowerCase()}`;
    if (seenPairs.has(key)) {
      issues.push({ path: `transitions[${i}]`, messageKey: 'duplicateFromTrigger' });
    }
    seenPairs.add(key);
  });

  return issues;
}
```

- [ ] **Step 2: Build + lint**

```bash
cd boilerplateFE && npm run build 2>&1 | tail -3 && npm run lint 2>&1 | tail -3 && cd ..
```

Expected: clean.

- [ ] **Step 3: Commit**

```bash
git add boilerplateFE/src/features/workflow/components/designer/validation/designerSchema.ts
git commit -m "feat(workflow-fe): add designer validation schema (zod mirror of BE validator)"
```

---

## Task 6: Frontend — `useDesignerStore` (zustand)

**Files:**
- Create: `boilerplateFE/src/features/workflow/components/designer/hooks/useDesignerStore.ts`

**Purpose:** Single source of truth for designer state. Holds nodes, edges, dirty flag, selection, and serialization helpers.

- [ ] **Step 1: Create the store**

Create `boilerplateFE/src/features/workflow/components/designer/hooks/useDesignerStore.ts`:

```ts
import { create } from 'zustand';
import { applyNodeChanges, applyEdgeChanges, type Node, type Edge, type NodeChange, type EdgeChange, type Connection } from '@xyflow/react';
import type { WorkflowStateConfig, WorkflowTransitionConfig } from '@/types/workflow.types';
import { validateDefinition, type ValidationIssue } from '../validation/designerSchema';

export type StateNodeData = WorkflowStateConfig;
export type StateNode = Node<StateNodeData, 'state'>;

export type TransitionEdgeData = Omit<WorkflowTransitionConfig, 'from' | 'to'>;
export type TransitionEdge = Edge<TransitionEdgeData, 'transition'>;

type Selection =
  | { kind: 'state'; name: string }
  | { kind: 'transition'; id: string }
  | { kind: 'empty' };

interface DesignerState {
  nodes: StateNode[];
  edges: TransitionEdge[];
  isDirty: boolean;
  selection: Selection;
  issues: ValidationIssue[];
}

interface DesignerActions {
  load: (states: WorkflowStateConfig[], transitions: WorkflowTransitionConfig[]) => void;
  onNodesChange: (changes: NodeChange[]) => void;
  onEdgesChange: (changes: EdgeChange[]) => void;
  onConnect: (connection: Connection) => void;
  addState: (state: WorkflowStateConfig, position: { x: number; y: number }) => void;
  updateStateByName: (name: string, patch: Partial<WorkflowStateConfig>) => void;
  updateTransitionById: (id: string, patch: Partial<WorkflowTransitionConfig>) => void;
  select: (selection: Selection) => void;
  markClean: () => void;
  setNodesFromLayout: (positioned: StateNode[]) => void;
  toDefinition: () => { states: WorkflowStateConfig[]; transitions: WorkflowTransitionConfig[] };
}

export type DesignerStore = DesignerState & DesignerActions;

const edgeIdFor = (from: string, trigger: string) => `${from}__${trigger}`;

function recompute(state: DesignerState): Pick<DesignerState, 'issues'> {
  const { states, transitions } = toDefinitionFrom(state.nodes, state.edges);
  return { issues: validateDefinition(states, transitions) };
}

function toDefinitionFrom(nodes: StateNode[], edges: TransitionEdge[]) {
  const states: WorkflowStateConfig[] = nodes.map(n => ({
    ...n.data,
    uiPosition: { x: n.position.x, y: n.position.y },
  }));
  const transitions: WorkflowTransitionConfig[] = edges.map(e => ({
    from: e.source,
    to: e.target,
    trigger: e.data?.trigger ?? '',
    type: e.data?.type ?? 'Manual',
    condition: e.data?.condition ?? null,
  }));
  return { states, transitions };
}

export const useDesignerStore = create<DesignerStore>((set, get) => ({
  nodes: [],
  edges: [],
  isDirty: false,
  selection: { kind: 'empty' },
  issues: [],

  load: (states, transitions) => {
    const nodes: StateNode[] = states.map((s, i) => ({
      id: s.name,
      type: 'state',
      position: s.uiPosition ?? { x: 0, y: i * 140 },
      data: s,
    }));
    const edges: TransitionEdge[] = transitions.map(t => ({
      id: edgeIdFor(t.from, t.trigger),
      source: t.from,
      target: t.to,
      type: 'transition',
      data: { trigger: t.trigger, type: t.type ?? 'Manual', condition: t.condition ?? null },
    }));
    set({
      nodes,
      edges,
      isDirty: false,
      selection: { kind: 'empty' },
      ...recompute({ nodes, edges, isDirty: false, selection: { kind: 'empty' }, issues: [] }),
    });
  },

  onNodesChange: (changes) => {
    const next = applyNodeChanges(changes, get().nodes) as StateNode[];
    // Mark dirty only on user-initiated drag/remove; ignore pure selection events.
    const mutated = changes.some(c => c.type === 'position' || c.type === 'remove' || c.type === 'add');
    set({
      nodes: next,
      isDirty: mutated ? true : get().isDirty,
      ...recompute({ ...get(), nodes: next }),
    });
  },

  onEdgesChange: (changes) => {
    const next = applyEdgeChanges(changes, get().edges) as TransitionEdge[];
    const mutated = changes.some(c => c.type === 'remove' || c.type === 'add');
    set({
      edges: next,
      isDirty: mutated ? true : get().isDirty,
      ...recompute({ ...get(), edges: next }),
    });
  },

  onConnect: (connection) => {
    if (!connection.source || !connection.target) return;
    const trigger = ''; // user will name it in the side panel
    const id = edgeIdFor(connection.source, trigger || `__tmp_${Date.now()}`);
    const newEdge: TransitionEdge = {
      id,
      source: connection.source,
      target: connection.target,
      type: 'transition',
      data: { trigger, type: 'Manual', condition: null },
    };
    const edges = [...get().edges, newEdge];
    set({
      edges,
      isDirty: true,
      selection: { kind: 'transition', id },
      ...recompute({ ...get(), edges }),
    });
  },

  addState: (state, position) => {
    const node: StateNode = {
      id: state.name,
      type: 'state',
      position,
      data: state,
    };
    const nodes = [...get().nodes, node];
    set({
      nodes,
      isDirty: true,
      selection: { kind: 'state', name: state.name },
      ...recompute({ ...get(), nodes }),
    });
  },

  updateStateByName: (name, patch) => {
    const nodes = get().nodes.map(n =>
      n.id === name ? { ...n, id: patch.name ?? n.id, data: { ...n.data, ...patch } } : n);
    // If the name changed, rewrite edge endpoints too.
    let edges = get().edges;
    if (patch.name && patch.name !== name) {
      edges = edges.map(e => ({
        ...e,
        source: e.source === name ? patch.name! : e.source,
        target: e.target === name ? patch.name! : e.target,
      }));
    }
    set({
      nodes,
      edges,
      isDirty: true,
      selection: patch.name ? { kind: 'state', name: patch.name } : get().selection,
      ...recompute({ ...get(), nodes, edges }),
    });
  },

  updateTransitionById: (id, patch) => {
    const edges = get().edges.map(e => e.id === id
      ? { ...e, data: { ...(e.data ?? { trigger: '', type: 'Manual', condition: null }), ...patch } as TransitionEdgeData }
      : e);
    set({
      edges,
      isDirty: true,
      ...recompute({ ...get(), edges }),
    });
  },

  select: (selection) => set({ selection }),

  markClean: () => set({ isDirty: false }),

  setNodesFromLayout: (positioned) => set({
    nodes: positioned,
    ...recompute({ ...get(), nodes: positioned }),
  }),

  toDefinition: () => toDefinitionFrom(get().nodes, get().edges),
}));
```

- [ ] **Step 2: Build + lint**

```bash
cd boilerplateFE && npm run build 2>&1 | tail -3 && npm run lint 2>&1 | tail -3 && cd ..
```

Expected: clean.

- [ ] **Step 3: Commit**

```bash
git add boilerplateFE/src/features/workflow/components/designer/hooks/useDesignerStore.ts
git commit -m "feat(workflow-fe): add useDesignerStore (zustand) with serialization + validation recompute"
```

---

## Task 7: Frontend — `useAutoLayout` (dagre wrapper)

**Files:**
- Create: `boilerplateFE/src/features/workflow/components/designer/hooks/useAutoLayout.ts`

- [ ] **Step 1: Create hook**

Create `boilerplateFE/src/features/workflow/components/designer/hooks/useAutoLayout.ts`:

```ts
import { useCallback } from 'react';
import dagre from 'dagre';
import type { StateNode, TransitionEdge } from './useDesignerStore';

const NODE_WIDTH = 220;
const NODE_HEIGHT = 80;

export function useAutoLayout() {
  return useCallback((nodes: StateNode[], edges: TransitionEdge[]): StateNode[] => {
    const g = new dagre.graphlib.Graph();
    g.setDefaultEdgeLabel(() => ({}));
    g.setGraph({ rankdir: 'TB', nodesep: 48, ranksep: 80 });

    for (const n of nodes) g.setNode(n.id, { width: NODE_WIDTH, height: NODE_HEIGHT });
    for (const e of edges) g.setEdge(e.source, e.target);

    dagre.layout(g);

    return nodes.map(n => {
      const laid = g.node(n.id);
      return {
        ...n,
        position: { x: laid.x - NODE_WIDTH / 2, y: laid.y - NODE_HEIGHT / 2 },
      };
    });
  }, []);
}
```

- [ ] **Step 2: Build + lint**

```bash
cd boilerplateFE && npm run build 2>&1 | tail -3 && npm run lint 2>&1 | tail -3 && cd ..
```

- [ ] **Step 3: Commit**

```bash
git add boilerplateFE/src/features/workflow/components/designer/hooks/useAutoLayout.ts
git commit -m "feat(workflow-fe): add useAutoLayout hook (dagre top-to-bottom)"
```

---

## Task 8: Frontend — `StateNode` component

**Files:**
- Create: `boilerplateFE/src/features/workflow/components/designer/StateNode.tsx`

- [ ] **Step 1: Create the component**

```tsx
import { memo } from 'react';
import { Handle, Position, type NodeProps } from '@xyflow/react';
import { AlertTriangle, Clock, Users } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import type { StateNode as StateNodeType } from './hooks/useDesignerStore';
import { useDesignerStore } from './hooks/useDesignerStore';

const TYPE_COLOR: Record<string, string> = {
  Initial: 'bg-blue-500',
  HumanTask: 'bg-amber-500',
  SystemAction: 'bg-purple-500',
  Terminal: 'bg-emerald-500',
};

function StateNodeInner({ data, id, selected }: NodeProps<StateNodeType>) {
  const hasError = useDesignerStore(s => s.issues.some(i => i.path.startsWith(`states[`) && i.path.includes(id)));
  const color = TYPE_COLOR[data.type] ?? 'bg-muted';

  return (
    <div
      className={[
        'rounded-xl border bg-card shadow-card w-[220px] text-sm',
        selected ? 'ring-2 ring-primary' : 'border-border',
      ].join(' ')}
      data-state-type={data.type}
    >
      <div className={['h-1.5 rounded-t-xl', color].join(' ')} />
      <div className="p-3 space-y-1.5">
        <div className="flex items-start justify-between gap-1.5">
          <div className="min-w-0">
            <div className="font-semibold text-foreground truncate">
              {data.displayName || data.name}
            </div>
            <div className="text-[11px] text-muted-foreground truncate">{data.name}</div>
          </div>
          {hasError && (
            <AlertTriangle className="h-3.5 w-3.5 text-destructive shrink-0" aria-label="validation errors" />
          )}
        </div>

        <div className="flex items-center gap-1">
          <Badge variant="outline" className="text-[10px]">{data.type}</Badge>
          {data.assignee?.strategy && (
            <Badge variant="secondary" className="text-[10px] gap-1">
              <Users className="h-3 w-3" />
              {data.assignee.strategy}
              {data.assignee.parameters?.roleName ? `: ${String(data.assignee.parameters.roleName)}` : ''}
            </Badge>
          )}
          {data.sla && (data.sla.reminderAfterHours != null || data.sla.escalateAfterHours != null) && (
            <Badge variant="secondary" className="text-[10px] gap-1">
              <Clock className="h-3 w-3" />
              SLA
            </Badge>
          )}
        </div>

        {data.actions && data.actions.length > 0 && (
          <div className="flex flex-wrap gap-1">
            {data.actions.map(a => (
              <span key={a} className="text-[10px] rounded bg-muted px-1.5 py-0.5">{a}</span>
            ))}
          </div>
        )}
      </div>

      <Handle type="target" position={Position.Top} className="!bg-primary" />
      <Handle type="source" position={Position.Bottom} className="!bg-primary" />
    </div>
  );
}

export const StateNode = memo(StateNodeInner);
```

- [ ] **Step 2: Build + lint**

```bash
cd boilerplateFE && npm run build 2>&1 | tail -3 && npm run lint 2>&1 | tail -3 && cd ..
```

- [ ] **Step 3: Commit**

```bash
git add boilerplateFE/src/features/workflow/components/designer/StateNode.tsx
git commit -m "feat(workflow-fe): add StateNode React Flow custom node"
```

---

## Task 9: Frontend — `TransitionEdge` component

**Files:**
- Create: `boilerplateFE/src/features/workflow/components/designer/TransitionEdge.tsx`

- [ ] **Step 1: Create the component**

```tsx
import { memo } from 'react';
import { BaseEdge, EdgeLabelRenderer, getSmoothStepPath, type EdgeProps } from '@xyflow/react';
import type { TransitionEdge as TransitionEdgeType } from './hooks/useDesignerStore';

function TransitionEdgeInner({
  id, sourceX, sourceY, targetX, targetY, sourcePosition, targetPosition, data, selected,
}: EdgeProps<TransitionEdgeType>) {
  const [path, labelX, labelY] = getSmoothStepPath({
    sourceX, sourceY, sourcePosition, targetX, targetY, targetPosition,
  });

  const conditional = data?.condition != null;
  const auto = data?.type?.toLowerCase() === 'auto';

  const stroke = conditional ? 'hsl(var(--accent-foreground))' : 'hsl(var(--muted-foreground))';
  const dash = auto ? '6 3' : undefined;

  return (
    <>
      <BaseEdge
        id={id}
        path={path}
        style={{ stroke, strokeWidth: selected ? 2.5 : 1.5, strokeDasharray: dash }}
        markerEnd="url(#react-flow__arrowclosed)"
      />
      <EdgeLabelRenderer>
        <div
          style={{
            position: 'absolute',
            transform: `translate(-50%, -50%) translate(${labelX}px, ${labelY}px)`,
            pointerEvents: 'all',
          }}
          className={[
            'px-1.5 py-0.5 rounded text-[11px] font-medium bg-background border',
            selected ? 'border-primary' : 'border-border',
            !data?.trigger ? 'text-destructive italic' : 'text-foreground',
          ].join(' ')}
        >
          {conditional ? 'ƒ ' : ''}{data?.trigger || '(set trigger)'}
        </div>
      </EdgeLabelRenderer>
    </>
  );
}

export const TransitionEdge = memo(TransitionEdgeInner);
```

- [ ] **Step 2: Build + lint**

```bash
cd boilerplateFE && npm run build 2>&1 | tail -3 && npm run lint 2>&1 | tail -3 && cd ..
```

- [ ] **Step 3: Commit**

```bash
git add boilerplateFE/src/features/workflow/components/designer/TransitionEdge.tsx
git commit -m "feat(workflow-fe): add TransitionEdge custom edge"
```

---

## Task 10: Frontend — `DesignerCanvas` shell

**Files:**
- Create: `boilerplateFE/src/features/workflow/components/designer/DesignerCanvas.tsx`

- [ ] **Step 1: Create the canvas**

```tsx
import { useCallback } from 'react';
import {
  ReactFlow, Background, Controls, MiniMap, ReactFlowProvider,
  type NodeTypes, type EdgeTypes, type OnSelectionChangeParams,
} from '@xyflow/react';
import '@xyflow/react/dist/style.css';

import { useDesignerStore } from './hooks/useDesignerStore';
import { StateNode } from './StateNode';
import { TransitionEdge } from './TransitionEdge';

const nodeTypes: NodeTypes = { state: StateNode };
const edgeTypes: EdgeTypes = { transition: TransitionEdge };

interface Props {
  readOnly?: boolean;
}

function DesignerCanvasInner({ readOnly = false }: Props) {
  const nodes = useDesignerStore(s => s.nodes);
  const edges = useDesignerStore(s => s.edges);
  const onNodesChange = useDesignerStore(s => s.onNodesChange);
  const onEdgesChange = useDesignerStore(s => s.onEdgesChange);
  const onConnect = useDesignerStore(s => s.onConnect);
  const select = useDesignerStore(s => s.select);

  const onSelectionChange = useCallback((p: OnSelectionChangeParams) => {
    if (p.nodes[0]) select({ kind: 'state', name: p.nodes[0].id });
    else if (p.edges[0]) select({ kind: 'transition', id: p.edges[0].id });
    else select({ kind: 'empty' });
  }, [select]);

  return (
    <div className="h-full w-full">
      <ReactFlow
        nodes={nodes}
        edges={edges}
        onNodesChange={readOnly ? undefined : onNodesChange}
        onEdgesChange={readOnly ? undefined : onEdgesChange}
        onConnect={readOnly ? undefined : onConnect}
        onSelectionChange={onSelectionChange}
        nodeTypes={nodeTypes}
        edgeTypes={edgeTypes}
        nodesDraggable={!readOnly}
        nodesConnectable={!readOnly}
        elementsSelectable
        fitView
        fitViewOptions={{ padding: 0.2 }}
        proOptions={{ hideAttribution: true }}
      >
        <Background />
        <MiniMap pannable zoomable />
        <Controls showInteractive={false} />
      </ReactFlow>
    </div>
  );
}

export function DesignerCanvas(props: Props) {
  return (
    <ReactFlowProvider>
      <DesignerCanvasInner {...props} />
    </ReactFlowProvider>
  );
}
```

- [ ] **Step 2: Build + lint**

```bash
cd boilerplateFE && npm run build 2>&1 | tail -3 && npm run lint 2>&1 | tail -3 && cd ..
```

- [ ] **Step 3: Commit**

```bash
git add boilerplateFE/src/features/workflow/components/designer/DesignerCanvas.tsx
git commit -m "feat(workflow-fe): add DesignerCanvas shell (React Flow + MiniMap + Controls)"
```

---

## Task 11: Frontend — `JsonBlockField` reusable component

**Files:**
- Create: `boilerplateFE/src/features/workflow/components/designer/JsonBlockField.tsx`

- [ ] **Step 1: Create the component**

```tsx
import { useState, useEffect } from 'react';
import { useTranslation } from 'react-i18next';
import { Button } from '@/components/ui/button';
import { Label } from '@/components/ui/label';

interface Props {
  label: string;
  placeholder?: string;
  value: unknown;
  onChange: (parsed: unknown) => void;
  disabled?: boolean;
}

export function JsonBlockField({ label, placeholder, value, onChange, disabled }: Props) {
  const { t } = useTranslation();
  const [text, setText] = useState(() => (value == null ? '' : JSON.stringify(value, null, 2)));
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    setText(value == null ? '' : JSON.stringify(value, null, 2));
  }, [value]);

  const commit = () => {
    if (text.trim() === '') { setError(null); onChange(null); return; }
    try {
      const parsed = JSON.parse(text);
      setError(null);
      onChange(parsed);
    } catch (e) {
      setError(e instanceof Error ? e.message : String(e));
    }
  };

  const format = () => {
    if (text.trim() === '') return;
    try {
      const parsed = JSON.parse(text);
      setText(JSON.stringify(parsed, null, 2));
      setError(null);
    } catch (e) {
      setError(e instanceof Error ? e.message : String(e));
    }
  };

  return (
    <div className="space-y-1.5">
      <div className="flex items-center justify-between">
        <Label className="text-xs font-medium">{label}</Label>
        {!disabled && (
          <div className="flex items-center gap-1">
            <Button type="button" size="sm" variant="ghost" onClick={format} disabled={!text.trim()}>
              {t('workflow.designer.json.format')}
            </Button>
            <Button type="button" size="sm" variant="ghost" onClick={() => { setText(''); onChange(null); setError(null); }}>
              {t('workflow.designer.json.reset')}
            </Button>
          </div>
        )}
      </div>
      <textarea
        className="w-full rounded-xl border border-border bg-background p-2 font-mono text-xs leading-relaxed min-h-[140px]"
        value={text}
        onChange={e => setText(e.target.value)}
        onBlur={commit}
        placeholder={placeholder ?? t('workflow.designer.json.hintPlaceholder')}
        disabled={disabled}
        spellCheck={false}
      />
      {error && (
        <p className="text-[11px] text-destructive">
          {t('workflow.designer.json.parseError', { message: error })}
        </p>
      )}
    </div>
  );
}
```

- [ ] **Step 2: Build + lint + commit**

```bash
cd boilerplateFE && npm run build 2>&1 | tail -3 && npm run lint 2>&1 | tail -3 && cd ..
git add boilerplateFE/src/features/workflow/components/designer/JsonBlockField.tsx
git commit -m "feat(workflow-fe): add JsonBlockField (textarea + parse-on-blur + errors)"
```

---

## Task 12: Frontend — `StateEditor` (first-class visual fields)

**Files:**
- Create: `boilerplateFE/src/features/workflow/components/designer/StateEditor.tsx`

- [ ] **Step 1: Create the component (first-class sections only — JSON blocks come in Task 13)**

```tsx
import { useTranslation } from 'react-i18next';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Button } from '@/components/ui/button';
import { X } from 'lucide-react';
import { useDesignerStore } from './hooks/useDesignerStore';
import type { WorkflowStateConfig } from '@/types/workflow.types';

const STATE_TYPES = ['Initial', 'HumanTask', 'SystemAction', 'Terminal'] as const;
const BUILTIN_STRATEGIES = ['SpecificUser', 'Role', 'EntityCreator'] as const;

interface Props {
  stateName: string;
  readOnly?: boolean;
}

export function StateEditor({ stateName, readOnly = false }: Props) {
  const { t } = useTranslation();
  const node = useDesignerStore(s => s.nodes.find(n => n.id === stateName));
  const update = useDesignerStore(s => s.updateStateByName);

  if (!node) return null;
  const state = node.data;

  const patch = (p: Partial<WorkflowStateConfig>) => update(state.name, p);

  return (
    <div className="space-y-4 p-4">
      {/* Identity */}
      <section className="space-y-2">
        <h4 className="text-xs font-semibold uppercase tracking-wide text-muted-foreground">
          {t('workflow.designer.state.identity')}
        </h4>
        <div className="space-y-1.5">
          <Label>{t('workflow.designer.state.name')}</Label>
          <Input
            value={state.name}
            onChange={e => patch({ name: e.target.value })}
            disabled={readOnly}
          />
          <p className="text-[11px] text-muted-foreground">{t('workflow.designer.state.nameSlugHelp')}</p>
        </div>
        <div className="space-y-1.5">
          <Label>{t('workflow.designer.state.displayName')}</Label>
          <Input
            value={state.displayName}
            onChange={e => patch({ displayName: e.target.value })}
            disabled={readOnly}
          />
        </div>
        <div className="space-y-1.5">
          <Label>{t('workflow.designer.state.type')}</Label>
          <select
            className="w-full rounded-xl border border-border bg-background px-3 py-2 text-sm"
            value={state.type}
            onChange={e => patch({ type: e.target.value })}
            disabled={readOnly}
          >
            {STATE_TYPES.map(tt => <option key={tt} value={tt}>{tt}</option>)}
          </select>
        </div>
      </section>

      {/* Actions (HumanTask only) */}
      {state.type === 'HumanTask' && (
        <section className="space-y-2">
          <h4 className="text-xs font-semibold uppercase tracking-wide text-muted-foreground">
            {t('workflow.designer.state.actions')}
          </h4>
          <ActionChipInput
            value={state.actions ?? []}
            onChange={actions => patch({ actions })}
            disabled={readOnly}
          />
        </section>
      )}

      {/* Assignee (HumanTask only) */}
      {state.type === 'HumanTask' && (
        <section className="space-y-2">
          <h4 className="text-xs font-semibold uppercase tracking-wide text-muted-foreground">
            {t('workflow.designer.state.assignee')}
          </h4>
          <div className="space-y-1.5">
            <Label>{t('workflow.designer.state.strategy')}</Label>
            <select
              className="w-full rounded-xl border border-border bg-background px-3 py-2 text-sm"
              value={state.assignee?.strategy ?? ''}
              onChange={e => patch({ assignee: { ...(state.assignee ?? { parameters: {} }), strategy: e.target.value } })}
              disabled={readOnly}
            >
              <option value="">—</option>
              {BUILTIN_STRATEGIES.map(s => <option key={s} value={s}>{s}</option>)}
              {state.assignee?.strategy && !BUILTIN_STRATEGIES.includes(state.assignee.strategy as typeof BUILTIN_STRATEGIES[number]) && (
                <option value={state.assignee.strategy}>{state.assignee.strategy}</option>
              )}
            </select>
          </div>
          {state.assignee?.strategy === 'Role' && (
            <div className="space-y-1.5">
              <Label>{t('workflow.designer.state.roleName')}</Label>
              <Input
                value={String(state.assignee.parameters?.roleName ?? '')}
                onChange={e => patch({ assignee: { ...state.assignee!, parameters: { ...(state.assignee!.parameters ?? {}), roleName: e.target.value } } })}
                disabled={readOnly}
              />
            </div>
          )}
          {state.assignee?.strategy === 'SpecificUser' && (
            <div className="space-y-1.5">
              <Label>{t('workflow.designer.state.userId')}</Label>
              <Input
                value={String(state.assignee.parameters?.userId ?? '')}
                onChange={e => patch({ assignee: { ...state.assignee!, parameters: { ...(state.assignee!.parameters ?? {}), userId: e.target.value } } })}
                disabled={readOnly}
              />
            </div>
          )}
        </section>
      )}

      {/* SLA */}
      {state.type === 'HumanTask' && (
        <section className="space-y-2">
          <h4 className="text-xs font-semibold uppercase tracking-wide text-muted-foreground">
            {t('workflow.designer.state.sla')}
          </h4>
          <div className="grid grid-cols-2 gap-2">
            <div className="space-y-1.5">
              <Label className="text-xs">{t('workflow.designer.state.reminderAfterHours')}</Label>
              <Input
                type="number"
                min={0}
                value={state.sla?.reminderAfterHours ?? ''}
                onChange={e => patch({ sla: { ...(state.sla ?? {}), reminderAfterHours: e.target.value === '' ? null : Number(e.target.value) } })}
                disabled={readOnly}
              />
            </div>
            <div className="space-y-1.5">
              <Label className="text-xs">{t('workflow.designer.state.escalateAfterHours')}</Label>
              <Input
                type="number"
                min={0}
                value={state.sla?.escalateAfterHours ?? ''}
                onChange={e => patch({ sla: { ...(state.sla ?? {}), escalateAfterHours: e.target.value === '' ? null : Number(e.target.value) } })}
                disabled={readOnly}
              />
            </div>
          </div>
        </section>
      )}

      {/* Advanced (JSON blocks) wired in Task 13 */}
    </div>
  );
}

function ActionChipInput({ value, onChange, disabled }: { value: string[]; onChange: (v: string[]) => void; disabled?: boolean }) {
  return (
    <div className="flex flex-wrap gap-1">
      {value.map((a, i) => (
        <span key={a} className="inline-flex items-center gap-1 rounded bg-muted px-2 py-0.5 text-xs">
          {a}
          {!disabled && (
            <button
              type="button"
              onClick={() => onChange(value.filter((_, j) => j !== i))}
              className="opacity-60 hover:opacity-100"
              aria-label={`Remove ${a}`}
            >
              <X className="h-3 w-3" />
            </button>
          )}
        </span>
      ))}
      {!disabled && (
        <input
          type="text"
          className="rounded border border-border bg-background px-2 py-0.5 text-xs min-w-[120px]"
          placeholder="Add action, press Enter"
          onKeyDown={e => {
            if (e.key === 'Enter') {
              e.preventDefault();
              const v = (e.target as HTMLInputElement).value.trim();
              if (v && !value.includes(v)) onChange([...value, v]);
              (e.target as HTMLInputElement).value = '';
            }
          }}
        />
      )}
    </div>
  );
}
```

- [ ] **Step 2: Build + lint + commit**

```bash
cd boilerplateFE && npm run build 2>&1 | tail -3 && npm run lint 2>&1 | tail -3 && cd ..
git add boilerplateFE/src/features/workflow/components/designer/StateEditor.tsx
git commit -m "feat(workflow-fe): add StateEditor first-class visual fields (identity/actions/assignee/sla)"
```

---

## Task 13: Frontend — `StateEditor` JSON blocks (advanced fields)

**Files:**
- Modify: `boilerplateFE/src/features/workflow/components/designer/StateEditor.tsx`

- [ ] **Step 1: Add advanced-section JSON blocks to `StateEditor`**

At the bottom of the `StateEditor` component's returned JSX (replacing the placeholder comment "Advanced (JSON blocks) wired in Task 13"), insert:

```tsx
{/* Advanced (JSON blocks) */}
<section className="space-y-2">
  <h4 className="text-xs font-semibold uppercase tracking-wide text-muted-foreground">
    {t('workflow.designer.state.advanced')}
  </h4>
  <JsonBlockField
    label={t('workflow.designer.state.fallbackAssignee')}
    value={state.assignee?.fallback ?? null}
    onChange={v => patch({ assignee: { ...(state.assignee ?? { strategy: '' }), fallback: v as WorkflowStateConfig['assignee'] } })}
    placeholder='{ "strategy": "Role", "parameters": { "roleName": "SuperAdmin" } }'
    disabled={readOnly}
  />
  <JsonBlockField
    label={t('workflow.designer.state.customParameters')}
    value={state.assignee?.parameters ?? null}
    onChange={v => patch({ assignee: { ...(state.assignee ?? { strategy: '' }), parameters: v as Record<string, unknown> | null } })}
    placeholder='{ "custom": "value" }'
    disabled={readOnly}
  />
  <JsonBlockField
    label={t('workflow.designer.state.onEnterHooks')}
    value={state.onEnter ?? null}
    onChange={v => patch({ onEnter: v as WorkflowStateConfig['onEnter'] })}
    placeholder='[ { "type": "notify", "template": "workflow.task-assigned", "to": "assignee" } ]'
    disabled={readOnly}
  />
  <JsonBlockField
    label={t('workflow.designer.state.onExitHooks')}
    value={state.onExit ?? null}
    onChange={v => patch({ onExit: v as WorkflowStateConfig['onExit'] })}
    placeholder='[ { "type": "activity", "action": "workflow_transition" } ]'
    disabled={readOnly}
  />
  <JsonBlockField
    label={t('workflow.designer.state.formFields')}
    value={state.formFields ?? null}
    onChange={v => patch({ formFields: v as WorkflowStateConfig['formFields'] })}
    placeholder='[ { "name": "amount", "label": "Amount", "type": "number", "required": true } ]'
    disabled={readOnly}
  />
  <JsonBlockField
    label={t('workflow.designer.state.parallel')}
    value={state.parallel ?? null}
    onChange={v => patch({ parallel: v as WorkflowStateConfig['parallel'] })}
    placeholder='{ "mode": "AllOf", "assignees": [ { "strategy": "Role", "parameters": { "roleName": "Admin" } } ] }'
    disabled={readOnly}
  />
</section>
```

Also add `import { JsonBlockField } from './JsonBlockField';` at the top.

- [ ] **Step 2: Build + lint + commit**

```bash
cd boilerplateFE && npm run build 2>&1 | tail -3 && npm run lint 2>&1 | tail -3 && cd ..
git add boilerplateFE/src/features/workflow/components/designer/StateEditor.tsx
git commit -m "feat(workflow-fe): add advanced JSON blocks to StateEditor"
```

---

## Task 14: Frontend — `TransitionEditor` + `SidePanel` router

**Files:**
- Create: `boilerplateFE/src/features/workflow/components/designer/TransitionEditor.tsx`
- Create: `boilerplateFE/src/features/workflow/components/designer/SidePanel.tsx`

- [ ] **Step 1: Create `TransitionEditor.tsx`**

```tsx
import { useTranslation } from 'react-i18next';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { JsonBlockField } from './JsonBlockField';
import { useDesignerStore } from './hooks/useDesignerStore';

interface Props {
  edgeId: string;
  readOnly?: boolean;
}

export function TransitionEditor({ edgeId, readOnly = false }: Props) {
  const { t } = useTranslation();
  const edge = useDesignerStore(s => s.edges.find(e => e.id === edgeId));
  const update = useDesignerStore(s => s.updateTransitionById);

  if (!edge) return null;

  return (
    <div className="space-y-4 p-4">
      <div className="space-y-1.5">
        <Label>{t('workflow.designer.transition.trigger')}</Label>
        <Input
          value={edge.data?.trigger ?? ''}
          onChange={e => update(edge.id, { trigger: e.target.value })}
          disabled={readOnly}
        />
      </div>
      <div className="grid grid-cols-2 gap-2">
        <div className="space-y-1.5">
          <Label className="text-xs">{t('workflow.designer.transition.from')}</Label>
          <Input value={edge.source} readOnly />
        </div>
        <div className="space-y-1.5">
          <Label className="text-xs">{t('workflow.designer.transition.to')}</Label>
          <Input value={edge.target} readOnly />
        </div>
      </div>
      <div className="space-y-1.5">
        <Label>{t('workflow.designer.transition.type')}</Label>
        <select
          className="w-full rounded-xl border border-border bg-background px-3 py-2 text-sm"
          value={edge.data?.type ?? 'Manual'}
          onChange={e => update(edge.id, { type: e.target.value })}
          disabled={readOnly}
        >
          <option value="Manual">{t('workflow.designer.transition.typeManual')}</option>
          <option value="Auto">{t('workflow.designer.transition.typeAuto')}</option>
        </select>
      </div>
      <JsonBlockField
        label={t('workflow.designer.transition.condition')}
        value={edge.data?.condition ?? null}
        onChange={v => update(edge.id, { condition: v as object | null })}
        placeholder='{ "field": "amount", "operator": ">", "value": 1000 }'
        disabled={readOnly}
      />
    </div>
  );
}
```

- [ ] **Step 2: Create `SidePanel.tsx`**

```tsx
import { useTranslation } from 'react-i18next';
import { StateEditor } from './StateEditor';
import { TransitionEditor } from './TransitionEditor';
import { useDesignerStore } from './hooks/useDesignerStore';

interface Props {
  readOnly?: boolean;
}

export function SidePanel({ readOnly = false }: Props) {
  const { t } = useTranslation();
  const selection = useDesignerStore(s => s.selection);

  return (
    <aside className="w-[360px] shrink-0 border-l border-border bg-card overflow-auto">
      {selection.kind === 'state' && <StateEditor stateName={selection.name} readOnly={readOnly} />}
      {selection.kind === 'transition' && <TransitionEditor edgeId={selection.id} readOnly={readOnly} />}
      {selection.kind === 'empty' && (
        <div className="p-6 text-sm text-muted-foreground">
          {t('workflow.designer.emptySelection')}
        </div>
      )}
    </aside>
  );
}
```

- [ ] **Step 3: Build + lint + commit**

```bash
cd boilerplateFE && npm run build 2>&1 | tail -3 && npm run lint 2>&1 | tail -3 && cd ..
git add boilerplateFE/src/features/workflow/components/designer/TransitionEditor.tsx \
        boilerplateFE/src/features/workflow/components/designer/SidePanel.tsx
git commit -m "feat(workflow-fe): add TransitionEditor + SidePanel router"
```

---

## Task 15: Frontend — `DesignerToolbar`

**Files:**
- Create: `boilerplateFE/src/features/workflow/components/designer/DesignerToolbar.tsx`

- [ ] **Step 1: Create the toolbar**

```tsx
import { useTranslation } from 'react-i18next';
import { Save, LayoutGrid, Plus, AlertTriangle } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { useDesignerStore } from './hooks/useDesignerStore';

interface Props {
  onSave: () => void;
  onAutoLayout: () => void;
  onAddState: () => void;
  saving: boolean;
  readOnly?: boolean;
}

export function DesignerToolbar({ onSave, onAutoLayout, onAddState, saving, readOnly = false }: Props) {
  const { t } = useTranslation();
  const isDirty = useDesignerStore(s => s.isDirty);
  const errors = useDesignerStore(s => s.issues.length);

  const saveDisabled = readOnly || !isDirty || errors > 0 || saving;

  return (
    <div className="flex items-center justify-between gap-2 border-b border-border bg-card px-4 py-2">
      <div className="flex items-center gap-2">
        {!readOnly && (
          <Button size="sm" variant="outline" onClick={onAddState}>
            <Plus className="h-4 w-4 ltr:mr-1.5 rtl:ml-1.5" />
            {t('workflow.designer.state.addState', 'Add State')}
          </Button>
        )}
        {!readOnly && (
          <Button size="sm" variant="outline" onClick={onAutoLayout}>
            <LayoutGrid className="h-4 w-4 ltr:mr-1.5 rtl:ml-1.5" />
            {t('workflow.designer.autoLayout')}
          </Button>
        )}
      </div>
      <div className="flex items-center gap-2">
        {errors > 0 && (
          <span className="inline-flex items-center gap-1 text-xs text-destructive">
            <AlertTriangle className="h-3.5 w-3.5" />
            {errors}
          </span>
        )}
        {!readOnly && (
          <Button size="sm" onClick={onSave} disabled={saveDisabled}>
            <Save className="h-4 w-4 ltr:mr-1.5 rtl:ml-1.5" />
            {saving ? t('workflow.designer.saving') : t('workflow.designer.save')}
          </Button>
        )}
      </div>
    </div>
  );
}
```

- [ ] **Step 2: Build + lint + commit**

```bash
cd boilerplateFE && npm run build 2>&1 | tail -3 && npm run lint 2>&1 | tail -3 && cd ..
git add boilerplateFE/src/features/workflow/components/designer/DesignerToolbar.tsx
git commit -m "feat(workflow-fe): add DesignerToolbar (save / auto-layout / add state / errors)"
```

---

## Task 16: Frontend — `WorkflowDefinitionDesignerPage` (load, save, dirty, template read-only)

**Files:**
- Create: `boilerplateFE/src/features/workflow/pages/WorkflowDefinitionDesignerPage.tsx`

- [ ] **Step 1: Create the page**

```tsx
import { useEffect, useRef, useCallback } from 'react';
import { useParams, useNavigate, useBlocker } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { Spinner } from '@/components/ui/spinner';
import { Card, CardContent } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { PageHeader, ConfirmDialog } from '@/components/common';
import { useBackNavigation } from '@/hooks';
import { ROUTES } from '@/config';
import { useWorkflowDefinition, useUpdateDefinition, useCloneDefinition } from '../api';
import { DesignerCanvas } from '../components/designer/DesignerCanvas';
import { SidePanel } from '../components/designer/SidePanel';
import { DesignerToolbar } from '../components/designer/DesignerToolbar';
import { useDesignerStore } from '../components/designer/hooks/useDesignerStore';
import { useAutoLayout } from '../components/designer/hooks/useAutoLayout';

export default function WorkflowDefinitionDesignerPage() {
  const { t } = useTranslation();
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();

  useBackNavigation(ROUTES.WORKFLOWS.getDefinitionDetail(id!), t('workflow.definitions.title'));

  const { data: def, isLoading } = useWorkflowDefinition(id!);
  const { mutate: updateDefinition, isPending: saving } = useUpdateDefinition();
  const { mutate: cloneDefinition, isPending: cloning } = useCloneDefinition();

  const autoLayout = useAutoLayout();
  const load = useDesignerStore(s => s.load);
  const toDefinition = useDesignerStore(s => s.toDefinition);
  const markClean = useDesignerStore(s => s.markClean);
  const isDirty = useDesignerStore(s => s.isDirty);
  const setNodesFromLayout = useDesignerStore(s => s.setNodesFromLayout);
  const addState = useDesignerStore(s => s.addState);

  const loaded = useRef(false);

  // Load definition into the store once
  useEffect(() => {
    if (!def || loaded.current) return;
    load(def.states ?? [], def.transitions ?? []);

    // Auto-layout if no positions exist (display-only; does NOT mark dirty)
    const hasPositions = (def.states ?? []).some(s => s.uiPosition);
    if (!hasPositions) {
      const positioned = autoLayout(useDesignerStore.getState().nodes, useDesignerStore.getState().edges);
      setNodesFromLayout(positioned);
      markClean(); // first-open auto-layout is not a user edit
    }

    loaded.current = true;
  }, [def, load, autoLayout, setNodesFromLayout, markClean]);

  // Navigate-away guard
  const blocker = useBlocker(({ currentLocation, nextLocation }) =>
    isDirty && currentLocation.pathname !== nextLocation.pathname,
  );

  // Before unload (browser close/refresh)
  useEffect(() => {
    const handler = (e: BeforeUnloadEvent) => {
      if (isDirty) { e.preventDefault(); e.returnValue = ''; }
    };
    window.addEventListener('beforeunload', handler);
    return () => window.removeEventListener('beforeunload', handler);
  }, [isDirty]);

  const handleSave = useCallback(() => {
    if (!id) return;
    const { states, transitions } = toDefinition();
    updateDefinition(
      {
        id,
        data: {
          statesJson: JSON.stringify(states),
          transitionsJson: JSON.stringify(transitions),
        },
      },
      { onSuccess: () => markClean() },
    );
  }, [id, updateDefinition, toDefinition, markClean]);

  const handleAutoLayout = useCallback(() => {
    const positioned = autoLayout(useDesignerStore.getState().nodes, useDesignerStore.getState().edges);
    setNodesFromLayout(positioned);
    // Explicit user action — mark dirty via the store's own mechanism by simulating a position change
    useDesignerStore.setState({ isDirty: true });
  }, [autoLayout, setNodesFromLayout]);

  const handleAddState = useCallback(() => {
    const existing = useDesignerStore.getState().nodes;
    let i = existing.length + 1;
    let name = `State${i}`;
    while (existing.some(n => n.id === name)) { i += 1; name = `State${i}`; }
    addState(
      { name, displayName: `State ${i}`, type: 'HumanTask' },
      { x: 80 + (i % 5) * 50, y: 80 + i * 30 },
    );
  }, [addState]);

  const handleClone = useCallback(() => {
    if (!id) return;
    cloneDefinition(id, {
      onSuccess: (cloneId) => navigate(ROUTES.WORKFLOWS.getDefinitionDesigner(String(cloneId))),
    });
  }, [id, cloneDefinition, navigate]);

  if (isLoading) {
    return <div className="flex justify-center py-12"><Spinner size="lg" /></div>;
  }
  if (!def) return null;

  const readOnly = def.isTemplate;

  return (
    <div className="flex flex-col h-[calc(100vh-5rem)]">
      <PageHeader
        title={`${def.name} — ${t('workflow.designer.title')}`}
      />
      {readOnly && (
        <Card className="m-4">
          <CardContent className="py-4 flex items-center justify-between gap-4">
            <div>
              <h3 className="text-sm font-semibold">{t('workflow.designer.template.readOnlyTitle')}</h3>
              <p className="text-xs text-muted-foreground">{t('workflow.designer.template.readOnlyBody')}</p>
            </div>
            <Button onClick={handleClone} disabled={cloning}>
              {t('workflow.designer.template.cloneToEdit')}
            </Button>
          </CardContent>
        </Card>
      )}
      <DesignerToolbar
        onSave={handleSave}
        onAutoLayout={handleAutoLayout}
        onAddState={handleAddState}
        saving={saving}
        readOnly={readOnly}
      />
      <div className="flex flex-1 min-h-0">
        <div className="flex-1 min-w-0">
          <DesignerCanvas readOnly={readOnly} />
        </div>
        <SidePanel readOnly={readOnly} />
      </div>

      <ConfirmDialog
        isOpen={blocker.state === 'blocked'}
        onClose={() => blocker.state === 'blocked' && blocker.reset()}
        title={t('workflow.designer.unsavedWarningTitle')}
        description={t('workflow.designer.unsavedWarningBody')}
        onConfirm={() => blocker.state === 'blocked' && blocker.proceed()}
        confirmLabel={t('workflow.designer.discard')}
        variant="danger"
      />
    </div>
  );
}
```

- [ ] **Step 2: Build + lint**

```bash
cd boilerplateFE && npm run build 2>&1 | tail -5 && npm run lint 2>&1 | tail -3 && cd ..
```

Expected: clean. React Router 7.11 (in use) fully supports `useBlocker`.

- [ ] **Step 3: Commit**

```bash
git add boilerplateFE/src/features/workflow/pages/WorkflowDefinitionDesignerPage.tsx
git commit -m "feat(workflow-fe): add WorkflowDefinitionDesignerPage (load/save/dirty/template read-only)"
```

---

## Task 17: Frontend — wire route + "Open Designer" button on detail page

**Files:**
- Modify: `boilerplateFE/src/routes/routes.tsx`
- Modify: `boilerplateFE/src/features/workflow/pages/WorkflowDefinitionDetailPage.tsx`

- [ ] **Step 1: Register the lazy route**

In `boilerplateFE/src/routes/routes.tsx`, add a lazy import near the other workflow imports:

```ts
const WorkflowDefinitionDesignerPage = activeModules.workflow ? lazy(() => import('@/features/workflow/pages/WorkflowDefinitionDesignerPage')) : NullPage;
```

Then, in the existing `WORKFLOWS.DEFINITIONS` permission-guarded children block (the one that already contains `DEFINITION_DETAIL`), add:

```tsx
{ path: ROUTES.WORKFLOWS.DEFINITION_DESIGNER, element: <WorkflowDefinitionDesignerPage /> },
```

- [ ] **Step 2: Add "Open Designer" button on detail page**

In `boilerplateFE/src/features/workflow/pages/WorkflowDefinitionDetailPage.tsx`:

1. Add imports:
```tsx
import { useNavigate } from 'react-router-dom';
import { Workflow as WorkflowIcon } from 'lucide-react';
```

2. Inside the component, add:
```tsx
const navigate = useNavigate();
```

3. In the `PageHeader` `actions` (near the existing `handleEdit` button), add **before** the Edit button, rendering only for non-template + `ManageDefinitions`:
```tsx
{!def.isTemplate && hasPermission(PERMISSIONS.Workflows.ManageDefinitions) && (
  <Button
    variant="default"
    onClick={() => navigate(ROUTES.WORKFLOWS.getDefinitionDesigner(id!))}
  >
    <WorkflowIcon className="h-4 w-4 ltr:mr-2 rtl:ml-2" />
    {t('workflow.designer.openDesigner')}
  </Button>
)}
```

Also add for templates (read-only view):
```tsx
{def.isTemplate && (
  <Button
    variant="outline"
    onClick={() => navigate(ROUTES.WORKFLOWS.getDefinitionDesigner(id!))}
  >
    <WorkflowIcon className="h-4 w-4 ltr:mr-2 rtl:ml-2" />
    {t('workflow.designer.openDesigner')}
  </Button>
)}
```

- [ ] **Step 3: Build + lint**

```bash
cd boilerplateFE && npm run build 2>&1 | tail -5 && npm run lint 2>&1 | tail -3 && cd ..
```

- [ ] **Step 4: Commit**

```bash
git add boilerplateFE/src/routes/routes.tsx \
        boilerplateFE/src/features/workflow/pages/WorkflowDefinitionDetailPage.tsx
git commit -m "feat(workflow-fe): wire designer route + Open Designer button on detail page"
```

---

## Task 18: Docs — roadmap + feature page

**Files:**
- Modify: `docs/roadmaps/workflow.md`
- Create: `docs/features/workflow-designer.md`

- [ ] **Step 1: Move Phase 4c entry into "Shipped" in `docs/roadmaps/workflow.md`**

Replace the existing "Visual workflow designer" section under the "Phase 4+ Deferred Items" area with a new "Phase 4c Shipped" section, inserted immediately after the existing "Phase 4b Shipped" section:

```markdown
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
```

- [ ] **Step 2: Remove the old "Visual workflow designer" section under "Phase 4+ Deferred Items"** — it has shipped.

In the same file, delete the section starting at `### Visual workflow designer` (under "Phase 4+ Deferred Items") down to the closing `---` that precedes the next deferred item.

- [ ] **Step 3: Add 9 new deferred entries to "Phase 4+ Deferred Items"**

Append these under the existing items. Each follows the established `### Title` / `**What:**` / `**Why deferred:**` / `**Pick this up when:**` pattern.

```markdown
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
```

- [ ] **Step 4: Create `docs/features/workflow-designer.md`**

```markdown
# Workflow Designer

Drag-and-drop visual editor for workflow definitions at `/workflows/definitions/:id/designer`. Requires `Workflows.ManageDefinitions` permission.

## What it does

- Loads the selected definition's states and transitions into a React Flow canvas.
- Lets admins drag nodes around, connect them with typed transitions, and edit state/transition properties in a right-docked side panel.
- Writes edits back to the existing `PUT /api/v1/workflow/definitions/{id}` endpoint as `statesJson` / `transitionsJson`.
- Persists node positions inside the JSON payload (optional `uiPosition` field on each state).

## What can be edited visually

- **State:** name (slug), displayName, type (Initial / HumanTask / SystemAction / Terminal), actions, assignee strategy + basic parameters (roleName, userId), SLA (reminder / escalate hours).
- **Transition:** trigger, type (Manual / Auto).

## What requires JSON-block editing

The following advanced fields are authored via inline JSON textareas in the side panel (schema hints included):

- **State:** fallback assignee, custom assignee parameters, OnEnter hooks, OnExit hooks, form fields, parallel/quorum.
- **Transition:** condition (single or compound And/Or/Not).

## Templates

Templates render in **read-only mode**. A "Clone to edit" button on the read-only banner creates a customised copy and redirects to the clone's designer URL.

## Validation

Client-side (zod) and server-side (FluentValidation) validators mirror each other:

- State name must be a slug (`^[A-Za-z][A-Za-z0-9_]*$`) and unique.
- Exactly one Initial state; at least one Terminal state.
- HumanTask states require an assignee strategy.
- SLA reminder hours < SLA escalate hours when both set.
- Transition `from`/`to` reference known states; no transitions from Terminal states; no duplicate (`from`, `trigger`) pairs.

Graph-level validation issues are surfaced in the toolbar; field-level issues appear inline. The Save button is disabled while any validation error is present.

## Auto-layout

On first open, if no state has a saved position, the designer auto-layouts via dagre top-to-bottom. Clicking the "Auto-layout" toolbar button re-runs the layout on demand.

## Save / dirty

- The toolbar's Save button is disabled unless `isDirty && !hasValidationErrors`.
- Navigating away or reloading while dirty prompts a confirmation.
- Successful save invalidates the definition detail query.
```

- [ ] **Step 5: Commit**

```bash
git add docs/roadmaps/workflow.md docs/features/workflow-designer.md
git commit -m "docs(workflow): move Phase 4c to shipped and add deferred roadmap entries"
```

---

## Task 19: Verification — full test suites + post-feature testing

**Purpose:** Confirm no regressions and exercise the designer against a real backend per the project's post-feature testing workflow.

- [ ] **Step 1: Run full backend tests**

```bash
dotnet test boilerplateBE/tests/Starter.Api.Tests/Starter.Api.Tests.csproj -l "console;verbosity=minimal" 2>&1 | tail -5
```

Expected: all green. Total should be `previous + 15` (15 new validator tests).

- [ ] **Step 2: Run FE build + lint**

```bash
cd boilerplateFE && npm run build 2>&1 | tail -5 && npm run lint 2>&1 | tail -3 && cd ..
```

Expected: both clean.

- [ ] **Step 3: Run post-feature testing workflow**

Follow `.claude/skills/post-feature-testing.md` steps 1–10:

1. Check free ports.
2. Rename a test app named `_testWfPhase4c` (BE+FE only; skip mobile).
3. Reconfigure: fix seed email (strip `_` from domain), fix bucket name, update ports, CORS, FrontendUrl, BaseUrl, create `.env`, bump rate limits 10x.
4. Generate all 8 DbContext migrations.
5. Build + start BE + FE.
6. Setup Communication (SMTP channel → Mailpit).
7. Seed test data (a couple of custom definitions).
8. Playwright MCP QA — at minimum:
   - Open designer on a custom definition; verify canvas + MiniMap + Controls render.
   - Drag a state to a new position; observe dirty flag; click Save; reload; positions persist.
   - Click "Add State"; edit name + displayName; see it in the list.
   - Drag from a state's bottom handle to another state; type a trigger in the side panel; Save; reload.
   - Make `name` = `invalid name` (with space); Save button disables; fix → enables.
   - Open a template definition (e.g. `general-approval`); confirm read-only banner + canvas locked + "Clone to edit".
   - Click "Clone to edit"; URL changes to new clone's designer; canvas is editable.
   - Edit OnEnter hooks JSON block with invalid JSON; see inline parse error.
9. Test as Admin AND as User — User role should get 403 at the route (no `ManageDefinitions`).
10. Fix any findings in the worktree source; copy FE files to test app for hot reload.

- [ ] **Step 4: Confirm clean state and stop (do not clean test app until user approves)**

Leave the test app running. Report URLs to the user for manual QA.

No commit for this task — it's verification only.

---

## Done. Next step

At this point the feature is implemented, documented, and QA'd. Invoke the `superpowers:finishing-a-development-branch` skill to decide on merge vs PR.
