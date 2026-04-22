# Workflow Phase 4a — Finish Dynamic Forms

**Status:** Draft — awaiting user review
**Author:** Claude (brainstorm + spec)
**Date:** 2026-04-22
**Parent roadmap:** [2026-04-22-workflow-phase3-plus-roadmap-design.md](2026-04-22-workflow-phase3-plus-roadmap-design.md)
**Depends on:** Phase 3 (merged — engine extraction, compound conditions, bulk ops)
**Ships as:** 1 PR

---

## Background

The Phase 3+ roadmap described 4a as "build dynamic forms." A code review of the post-Phase-3 tree shows ~80% of dynamic-form infrastructure was already delivered incrementally through Phase 2a (initial schema shape) and Phase 2b (`FormFieldsJson` denormalization on `ApprovalTask`). The components already in place:

| Piece | Location | Status |
|---|---|---|
| `FormFieldDefinition` record (6 field types: text, textarea, number, date, select, checkbox) | `Starter.Abstractions/Capabilities/WorkflowConfigRecords.cs` | Shipped |
| `FormFields` on `WorkflowStateConfig` | same file | Shipped |
| `FormFieldsJson` denormalized on `ApprovalTask` | `Starter.Module.Workflow/Domain/Entities/ApprovalTask.cs` | Shipped (Phase 2b) |
| `DynamicFormRenderer` (6 types) | `boilerplateFE/src/features/workflow/components/DynamicFormRenderer.tsx` | Shipped |
| `ApprovalDialog` form integration | `boilerplateFE/src/features/workflow/components/ApprovalDialog.tsx` | Shipped |
| `IFormDataValidator` with type-specific validation | `Starter.Module.Workflow/Infrastructure/Services/FormDataValidator.cs` | Shipped |
| `formData` flows `ExecuteTaskRequest` → `Command` → `Service` | Controller + handler + engine | Shipped |
| Form data merges into `WorkflowInstance.ContextJson` | `WorkflowEngine.cs:342-353` | Shipped |
| Merged data available to `AutoTransitionEvaluator` | `WorkflowEngine.cs:309-314` | Shipped |
| Form data rendered in `WorkflowStepTimeline` | `boilerplateFE/src/features/workflow/components/WorkflowStepTimeline.tsx` | Shipped |
| Definition detail page shows form fields as read-only badges | `WorkflowDefinitionDetailPage.tsx` | Shipped |
| `FormDataValidatorTests`, `HumanTaskFactoryTests` (form-fields cases) | `boilerplateBE/tests/Starter.Api.Tests/Workflow/` | Shipped |

4a is therefore scoped as a **polish-to-ship** phase, not a build-from-scratch phase.

## Problem statement

Three gaps block 4a from being a credible shipped capability:

1. **Validation errors masquerade as "TaskNotFound."** `WorkflowEngine.ExecuteTaskAsync` returns `bool`; validation failure returns `false`; `ExecuteTaskCommandHandler` maps `false` to `WorkflowErrors.TaskNotFound`. A user who submits a value over `maxLength` sees "task not found." This is a correctness bug.
2. **No field-level server errors surfaced in the FE.** The client does a simple required-field check; any server-side type / range / select-option / date-parse error returns an opaque failure with no per-field display.
3. **Zero end-to-end coverage** of the chain `formData submitted → merged into ContextJson → branching transition resolved by that field`. Unit tests on the validator and the factory exist; nothing asserts the end-to-end wiring.

Secondary gaps:

4. **Documentation drift.** The roadmap describes 4a as to-build; the code is mostly shipped. No user-facing documentation explains the schema shape, supported field types, or authoring pattern.
5. **Deferred field types undocumented.** Option C items from the brainstorm (multiselect, file upload, array-of-object) plus conditional field visibility need to be captured as deferred so they resurface in future planning.

## Goals

- Fix the validation-error surfacing bug.
- Plumb field-level errors from the `FormDataValidator` through the API envelope to the `DynamicFormRenderer`.
- Add end-to-end integration test proving the form-data → conditional-branch chain.
- Add Vitest unit tests for `DynamicFormRenderer` and the `ApprovalDialog` error path.
- Add user-facing documentation (`docs/features/workflow-forms.md`).
- Update `docs/roadmaps/workflow.md` to reflect 4a shipped + Phase 4+ deferred items.

## Non-goals (explicit)

- **Admin authoring UX for form fields.** Deferred to 4c (visual workflow designer). Admins continue authoring via template seeding or raw JSON in the interim.
- **New field types:** `multiselect`, `file upload`, `array-of-object` (rich repeating fieldsets). Tracked as Phase 4+ deferred.
- **Conditional field visibility** (e.g., show field B only if field A = X). Tracked as Phase 4+ deferred.
- **Multi-step forms.** Out of scope — one form per task; workflow itself models step-by-step.
- **Default values / server-computed placeholders.** Out of scope.
- **No EF migrations are committed** (project rule — rename'd test apps generate their own).

## Design

### BE — validation error contract

**Change 1: `WorkflowEngine.ExecuteTaskAsync` return type** — from `Task<bool>` to `Task<Result<bool>>`.

- Success path: `Result.Success(true)`.
- Task not found / not-in-pending-state / non-matching transition: `Result.Failure<bool>(WorkflowErrors.TaskNotFound(taskId))` (preserves existing semantics).
- Validation failure: `Result.Failure<bool>(WorkflowErrors.FormValidation(errors))` — the new error carries per-field messages.
- **Plan task: audit all existing `return false` paths in `WorkflowEngine.ExecuteTaskAsync`** and categorize each into either (a) a specific `Result.Failure` with an appropriate existing or new `WorkflowErrors.*` factory, or (b) `Result.Success(false)` for legitimate "not advancing yet" no-ops (e.g., parallel-group not yet complete). The plan must enumerate each path before the refactor lands.

**Change 2: New `WorkflowErrors.FormValidation`** factory:

```csharp
public static Error FormValidation(IReadOnlyList<FormValidationError> errors) =>
    Error.Validation(
        "Workflow.FormValidation",
        "Form data validation failed",
        metadata: new Dictionary<string, object?> {
            ["fieldErrors"] = errors.ToDictionary(e => e.FieldName, e => e.Message)
        });
```

`Error.Validation` is the existing factory on `Starter.Shared.Results.Error`. The `metadata` bag flows through the `ApiResponse<T>` envelope (via the existing error serialization path — no envelope changes needed if the serializer already includes error metadata; if not, add it as a minimal extension).

**Change 3: `ExecuteTaskCommandHandler`** — no longer manually maps `false` to `TaskNotFound`; just propagates the `Result<bool>` from the service:

```csharp
public async Task<Result<bool>> Handle(ExecuteTaskCommand request, CancellationToken cancellationToken) =>
    await workflowService.ExecuteTaskAsync(
        request.TaskId, request.Action, request.Comment,
        currentUser.UserId!.Value, request.FormData, cancellationToken);
```

**Change 4: `IWorkflowService.ExecuteTaskAsync` signature** in the capability interface mirrors the engine change (returns `Result<bool>`). No other capability consumers of this method exist today.

### FE — field-level error surfacing

**Change 1: `ApprovalDialog`** — extend the `onError` path of `useExecuteTask`:

```tsx
onError: (err) => {
  const fieldErrors = extractFieldErrors(err);
  if (fieldErrors) setFormErrors(fieldErrors);
}
```

`extractFieldErrors` is a small helper that reads field-keyed errors out of the API error envelope.

**Plan task: verify envelope shape first.** Before implementing the FE extractor, the plan must confirm how `Error.metadata` is serialized into `ApiResponse<T>.errors` today. If the serializer already flattens metadata into the `errors` body, the extractor reads `err.response?.data?.errors?.fieldErrors`. If it doesn't, the plan extends the serializer minimally (one test proving the `fieldErrors` key round-trips). No other callers depend on `Error.metadata` today — this is a net-new error-body shape convention.

**Change 2: `DynamicFormRenderer`** — no component changes. It already accepts and renders an `errors` prop per-field.

**Change 3: i18n** — the plan must audit en/ar/ku translation files and add any missing `workflow.forms.*` keys referenced by `DynamicFormRenderer`, `ApprovalDialog`, and `WorkflowStepTimeline`. Known references to verify at minimum: `workflow.forms.submittedData`, `workflow.forms.required`, `workflow.forms.selectPlaceholder`. The plan enumerates the full list after a grep of the three components.

### Documentation

**`docs/features/workflow-forms.md`** — new file:
- What dynamic forms are; when to use them.
- `FormFieldDefinition` schema reference (all 6 types) with examples.
- Validation semantics (required, min/max, maxLength, select option validation, date parse).
- How form data merges into `ContextJson` and becomes available to conditional transitions.
- Worked example: an expense-approval workflow that branches on `amount`.
- Authoring pattern — since 4c is not yet shipped, admins define form fields via `WorkflowTemplateConfig` in code / template seeding; reference the existing template seeding entry points.
- Link to `WorkflowStepTimeline` rendering of submitted form data in instance history.

**`docs/roadmaps/workflow.md`** — updates:
- New section **"Phase 4a Shipped"** listing everything enumerated in the Background table.
- Section **"Phase 4+ Deferred — Forms"** capturing:
  - Multiselect field type.
  - File upload field type (requires signed-URL plumbing into `ContextJson` + retention / quota / virus-scan design).
  - Array-of-object field type (repeating fieldsets).
  - Conditional field visibility (show/hide fields based on other field values).
  - Default values / server-computed placeholders.
  - Multi-step forms within a single task.
- Preserve the existing "Phase 4+ Deferred Items" list; this adds a Forms subsection to it.

## Testing

### BE tests

1. **`FormDataValidatorTests`** (existing) — verify no regression from the consuming signature change. No test changes needed; keep as-is.

2. **`WorkflowEngineFormValidationTests`** (new — `boilerplateBE/tests/Starter.Api.Tests/Workflow/`):
   - Given a state with `FormFields: [{ name: 'amount', type: 'number', required: true, min: 0 }]`, `ExecuteTaskAsync` with `formData: { amount: -5 }` returns `Result.Failure` with `WorkflowErrors.FormValidation` whose metadata carries `fieldErrors.amount`.
   - Instance `CurrentState` unchanged on validation failure.
   - Instance `ContextJson` unchanged on validation failure.
   - Multiple simultaneous field errors all surface in `fieldErrors`.

3. **`FormDataBranchingIntegrationTests`** (new):
   - Seeds a definition with two outgoing conditional transitions from `AwaitingApproval`: `{ Condition: { Field: 'amount', Operator: '>', Value: 10000 } } → SeniorReview` and fallback `→ Approved`.
   - Starts instance; executes task with `formData: { amount: 15000 }`.
   - Asserts `instance.CurrentState == 'SeniorReview'`.
   - Asserts `instance.ContextJson` contains `amount: 15000` (merged, not replaced).
   - Complementary case: `formData: { amount: 500 }` → `instance.CurrentState == 'Approved'`.
   - Uses the existing EF-in-memory test harness (same pattern as `HumanTaskFactoryTests`).

### FE tests

4. **`DynamicFormRenderer.test.tsx`** (new — Vitest):
   - Renders each of the 6 field types when present in the schema.
   - Required field shows asterisk.
   - `errors` prop renders the error message under the corresponding field.
   - `onChange` fires with correct value type per field (number → `number`; checkbox → `boolean`; etc.).

5. **`ApprovalDialog.test.tsx`** (new — Vitest):
   - On `useExecuteTask` error with `fieldErrors` payload, `formErrors` state is populated; the renderer receives it via prop.
   - Clearing a field value clears its error (existing behavior — regression guard).

### Live verification

Per the project's post-feature testing workflow (`scripts/rename.ps1` + chrome-devtools-mcp):
- Rename'd test app seeded with a definition that has a form-bearing state and a conditional outgoing transition.
- Login as tenant user; open the task; submit an invalid value; assert red error renders inline under the field.
- Submit a valid value that triggers the non-default branch; assert instance advanced to the branched state.

### Acceptance criteria

- Submitting invalid form data returns 400 with field-keyed errors; FE shows them inline under each field (no generic "task not found" leak).
- Submitting valid form data merges into `ContextJson`; conditional transitions referencing those fields resolve correctly.
- `dotnet test` green; `npm run build` green; Vitest suite green.
- `docs/features/workflow-forms.md` exists and documents the 6 supported types + authoring-via-template pattern.
- `docs/roadmaps/workflow.md` has a "Phase 4a Shipped" section and a "Phase 4+ Deferred — Forms" subsection capturing multiselect, file upload, array-of-object, conditional visibility.

## Migrations

None. No schema changes (the form-fields columns were added in Phase 2b). Per project rule `feedback_no_migrations`, no EF migrations are committed in the boilerplate.

## Risk & rollback

**Risk:** The signature change on `WorkflowEngine.ExecuteTaskAsync` ripples to `IWorkflowService.ExecuteTaskAsync` and every consumer. Mitigation: audit consumers during implementation (controller, batch-execute handler, any tests); `Result<bool>` wrapping is mechanical.

**Risk:** `ApiResponse<T>` envelope may not currently serialize `Error.metadata`. Mitigation: confirm during implementation; if missing, extend the serializer minimally (metadata dict → `errors` body key).

**Rollback:** Revert the single PR. No data migrations; no schema changes; no config flags.

## Out of scope (summary)

- Admin authoring UX (→ 4c).
- New field types: multiselect, file upload, array-of-object (→ Phase 4+ deferred).
- Conditional field visibility (→ Phase 4+ deferred).
- Default values / server-computed placeholders (→ Phase 4+ deferred).
- Multi-step forms (→ Phase 4+ deferred).
- EF migrations (standing project rule).
