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
