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
