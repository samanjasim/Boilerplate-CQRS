# Workflow Phase 4c — Visual Workflow Designer (MVP) — Design Spec

**Date:** 2026-04-23
**Status:** Approved for implementation.
**Predecessors:** Phase 3 (engine extraction), Phase 4a (dynamic forms), Phase 4b (analytics).
**Roadmap entry:** [`docs/roadmaps/workflow.md`](../../roadmaps/workflow.md) — Visual workflow designer.

## Goal

Ship a drag-and-drop state-machine builder that produces the same `WorkflowStateConfig[]` + `WorkflowTransitionConfig[]` JSON the backend already accepts via `POST/PUT /api/v1/workflows/definitions`. Replace today's read-only state-list UX on `WorkflowDefinitionDetailPage` with a real authoring surface for the common 80% of configuration, while preserving full expressiveness through inline JSON "escape hatches" for advanced fields.

MVP stance (option B): visual UI for the common fields; embedded JSON blocks for advanced fields. A future Option A upgrade (full-schema visual editor) is documented as a roadmap item, not part of this spec's shipping scope.

## Architecture

Pure frontend feature with one additive backend change (optional `UiPosition` on `WorkflowStateConfig`) so node positions survive reload. Runs at a dedicated route `/workflows/definitions/:id/designer`, gated by `Workflows.ManageDefinitions`. Built on `@xyflow/react` (React Flow v12) with `dagre` for first-open auto-layout and zustand for local designer state.

## Tech stack

- **Graph library:** `@xyflow/react` (React Flow v12).
- **Auto-layout:** `dagre`.
- **Local state:** `zustand` (already used in the app).
- **Validation:** `zod` on FE (already used), `FluentValidation` on BE (already used).
- **Charts/other deps:** none new.
- **Backend:** existing `UpdateDefinitionCommand` unchanged in shape; gains a validator and one optional record field.

---

## Placement & routing

- **New route:** `/workflows/definitions/:id/designer`, lazy-loaded via the existing route bundle pattern.
- **Permission gate:** `Workflows.ManageDefinitions` at route level.
- **Back-nav:** returns to `/workflows/definitions/:id` (detail page).
- **Entry point:** on `WorkflowDefinitionDetailPage`, non-template definitions gain an "Open Designer" button (secondary action) next to the existing "Edit" (rename) button.
- **Templates:** the same URL resolves, but the designer renders in read-only mode with a "Clone to edit" banner that calls `useCloneDefinition` and redirects to the clone's designer URL.

## Canvas

- **Library:** `@xyflow/react` v12.
- **Layout direction:** top-to-bottom (approval chains read naturally down the page).
- **Controls:** React Flow's built-in `MiniMap` + `Controls` panels.
- **Connection model:** dragging from a node's bottom handle onto another node creates a transition with `trigger: ''` and opens the edge editor to prompt for the trigger name.
- **Deletion:** `Delete` key removes the selected node or edge. Deleting a state that has edges touching it triggers a `ConfirmDialog` listing the affected transitions.
- **Snap-to-grid:** disabled for MVP (React Flow allows free positioning).

## State node (visual)

Fixed-width card (~220px) with:
- A top color strip keyed by `type` (Initial / HumanTask / SystemAction / Terminal).
- `displayName` (or `name` as fallback) in bold.
- `type` badge.
- Assignee strategy chip when applicable (e.g. "Role: Admin").
- Action chips (compact) when `type = HumanTask` and actions are declared.
- SLA icon + hours when SLA is set.
- A warning dot in the top-right when the node has unresolved validation errors (hover for tooltip).

The node renders via a custom `StateNode` component registered with React Flow.

## Transition edge (visual)

- Arrow marker + centered trigger label.
- Color coding: manual transition (gray), conditional (orange with a small "ƒ" icon), auto-transition (dashed).
- Selected edge shows a subtle halo.

The edge renders via a custom `TransitionEdge` component.

## Side panel (per-selection editor)

Right-docked ~360px panel that collapses to an icon when nothing is selected.

### When a state is selected — first-class visual sections

1. **Identity**
   - `name` — slug, required, unique within the definition.
   - `displayName` — required.
   - `type` — dropdown: Initial / HumanTask / SystemAction / Terminal.
2. **Actions** — chip input (only when `type = HumanTask`).
3. **Assignee** — strategy dropdown (`SpecificUser` / `Role` / `EntityCreator`) + context-aware parameter input (`userId` for SpecificUser, `roleName` for Role, no params for EntityCreator). Only when `type = HumanTask`.
4. **SLA** — two optional number inputs: "Remind after N hours" and "Escalate after N hours".

### When a state is selected — advanced sections (JSON blocks)

Rendered collapsed by default. Each block shows a schema-hint placeholder, live parse errors, "Reset" and "Format" buttons:
- **Fallback assignee** — `AssigneeConfig`.
- **Custom assignee parameters** — used when strategy is a non-built-in string.
- **OnEnter hooks** — `HookConfig[]`.
- **OnExit hooks** — `HookConfig[]`.
- **Form fields** — `FormFieldDefinition[]`.
- **Parallel / quorum** — `ParallelConfig`.

### When an edge is selected

Stacked form:
1. **Trigger** — text input, required, non-empty.
2. **From / To** — read-only (edit by re-dragging the connection).
3. **Type** — dropdown: Manual / Auto.
4. **Condition** — collapsed JSON block (`ConditionConfig`, supports compound And/Or/Not).

### Empty selection

Quick-help panel: "Click a state to edit, drag between states to connect, press Delete to remove".

## JSON block field — reusable component

`JsonBlockField`:
- Plain `<textarea>` (monospace, 8-row default, grows with content). No Monaco editor dependency for MVP — bundle cost isn't justified.
- On blur: `JSON.parse` the value; errors render inline with line pointer.
- Schema-hint placeholder text showing the expected shape.
- "Reset to empty" and "Format" (pretty-print) buttons.
- On save: parsed value is attached to the selected state/edge object under the correct key.

## Position persistence

- Add optional `UiPosition? UiPosition` to `WorkflowStateConfig` (C# record in `Starter.Abstractions/Capabilities/WorkflowConfigRecords.cs`).
- Mirror as optional `uiPosition?: { x: number; y: number }` in the FE type.
- `UiPosition` is purely cosmetic — the engine and analytics ignore it.
- On designer save, each node's live React Flow position is written into its state's `uiPosition`.
- `[JsonIgnoreCondition.WhenWritingNull]` so existing templates don't gain an empty field.

```csharp
public sealed record UiPosition(double X, double Y);
```

## Auto-layout

- `useAutoLayout` hook wraps `dagre` with a top-to-bottom configuration.
- Toolbar has an explicit "Auto-layout" button. Clicking it reflows the graph and marks the designer dirty so the new positions persist on Save.
- On first open: if **no** state in the definition has `uiPosition`, auto-layout runs automatically for display purposes only. This does **not** mark the designer dirty — a user who navigates away without touching anything sees no "unsaved changes" prompt. Positions become persistent only after the user intentionally edits something (or clicks the Auto-layout button) and Saves.

## Validation — unified FE/BE rules

Single source of truth: `UpdateDefinitionCommandValidator` on the backend. The FE `designerSchema.ts` (zod) mirrors the rules 1:1. A table-driven parity test in the BE test suite enforces that every named FE rule has a matching BE rule.

### State rules

- `name`: required; slug matches `^[A-Za-z][A-Za-z0-9_]*$`; unique within the definition; max 80 chars.
- `displayName`: required; max 120 chars.
- `type`: one of `Initial | HumanTask | SystemAction | Terminal`.
- Exactly one state with `type = Initial`.
- At least one state with `type = Terminal`.
- HumanTask with no `action` entries: warning, not error.
- `assignee.strategy` required when `type = HumanTask`.
- `sla.reminderAfterHours < sla.escalateAfterHours` when both are set.

### Transition rules

- `from`, `to`, `trigger`: all required.
- `from` / `to` must reference existing state names.
- No transition originating from a `Terminal` state.
- No duplicate `(from, trigger)` pairs.
- Every non-Initial state reachable from the Initial state: warning, not error (lets users work on sub-graphs mid-edit).

### Cross-cutting

- Each unknown field in a JSON block is rejected with a BE message that echoes into the FE's inline JSON error surface.
- **When validation runs on the FE:**
  - Side-panel fields: on blur (inline errors), so the user sees issues as they edit.
  - JSON blocks: on blur.
  - Graph-level rules (exactly one Initial, reachability, duplicate triggers): recomputed on every store mutation; surfaced as warning dots on affected nodes and as a summary count in the toolbar.
- On save: FE runs the full zod schema; if it passes, the mutation is fired. Any BE rule that fires anyway is surfaced via the existing axios error interceptor and mapped back to the offending field where possible.

## Templates (read-only mode)

- Designer detects `def.isTemplate` and renders: canvas locked (no drag, no connect, no edit), side-panel fields `readOnly`, toolbar shows only "Clone to edit".
- "Clone to edit" triggers `useCloneDefinition`, then navigates to `/workflows/definitions/{cloneId}/designer`.

## Save / dirty flow

- Zustand store tracks `isDirty` on any mutation (node add / move / delete, edge add / delete, side-panel save).
- Toolbar `Save` button: disabled when `!isDirty || hasValidationErrors`.
- Save serializes `{ states, transitions }` into `statesJson` / `transitionsJson` and calls `useUpdateDefinition` — no new mutation required.
- `window.beforeunload` + react-router `useBlocker` warn on navigate-away with unsaved changes.
- On success: `isDirty = false`, success toast, invalidate the definition detail query so detail page reflects changes on return.

## i18n

All labels, validation messages, and JSON-block placeholders live under `workflow.designer.*` in `en/ar/ku/translation.json`.

## Testing

The project has no FE unit-test infrastructure today (no vitest, no existing test files in `boilerplateFE`). Introducing it is out of scope for 4c. FE correctness is validated as it has been for every prior FE phase of this project:

- **TypeScript build** (`npm run build`) — catches type drift in the designer schema and types.
- **ESLint** (`npm run lint`) — catches react-refresh / hooks-rules violations.
- **Manual QA via Playwright MCP / Chrome DevTools MCP** inside a rename'd test app per the project's post-feature-testing workflow (`.claude/skills/post-feature-testing.md`). Covers: opening the designer, creating and editing a definition, template read-only mode, auto-layout, save/dirty, JSON-block edit + error, navigate-away warning.

Backend:
- **`UpdateDefinitionCommandValidatorTests`** — one xUnit test per validation rule in the spec (required fields, slug pattern, uniqueness, type membership, exactly-one-initial, at-least-one-terminal, assignee-required-for-humantask, sla ordering, transition from/to existence, no-from-terminal, no-duplicate-from-trigger).

FE/BE validation parity is a **best-effort mirror**: the BE validator is the source of truth; FE zod mirrors the rules 1:1 for better UX. If they drift, the BE's returned `ValidationErrors` still surface through the existing axios interceptor. Enforced by code review, not by a cross-process test.

## File structure

**Frontend (new):**

```
src/features/workflow/pages/WorkflowDefinitionDesignerPage.tsx
src/features/workflow/components/designer/DesignerCanvas.tsx
src/features/workflow/components/designer/StateNode.tsx
src/features/workflow/components/designer/TransitionEdge.tsx
src/features/workflow/components/designer/DesignerToolbar.tsx
src/features/workflow/components/designer/SidePanel.tsx
src/features/workflow/components/designer/StateEditor.tsx
src/features/workflow/components/designer/TransitionEditor.tsx
src/features/workflow/components/designer/JsonBlockField.tsx
src/features/workflow/components/designer/hooks/useDesignerStore.ts
src/features/workflow/components/designer/hooks/useAutoLayout.ts
src/features/workflow/components/designer/validation/designerSchema.ts
```

**Frontend (touched):**

- `src/types/workflow.types.ts` — add optional `uiPosition` to `WorkflowStateConfig`.
- `src/config/routes.config.ts` — new `DESIGNER` path.
- `src/routes/routes.tsx` — lazy route with permission guard.
- `src/features/workflow/pages/WorkflowDefinitionDetailPage.tsx` — add "Open Designer" button for non-templates.
- `src/i18n/locales/{en,ar,ku}/translation.json` — `workflow.designer.*` keys.
- `package.json` — add `@xyflow/react`, `dagre`.

**Backend (touched):**

- `Starter.Abstractions/Capabilities/WorkflowConfigRecords.cs` — add `UiPosition` record; add optional `UiPosition` to `WorkflowStateConfig`.
- `Starter.Module.Workflow/Application/Commands/UpdateDefinition/UpdateDefinitionCommandValidator.cs` — new file.
- `tests/Starter.Api.Tests/Workflow/UpdateDefinitionCommandValidatorTests.cs` — new file.

**Docs (touched):**

- `docs/roadmaps/workflow.md` — move Phase 4c into "Shipped"; add the deferred items listed in the "Future roadmap entries" section below.
- `docs/features/workflow-designer.md` — new feature page with screenshots + the visual/JSON split table.

---

## Future roadmap entries (to be added to `docs/roadmaps/workflow.md`)

Each item below becomes a new entry in the roadmap's deferred section as part of the 4c PR's documentation task. The format mirrors the existing workflow roadmap entries (what / why deferred / pick this up when / starting points).

### Option A — Full-schema visual editor (flagship follow-up)

Upgrade path that removes the inline JSON blocks by shipping a dedicated visual editor for each advanced field. Each block removal is a ship-on-its-own PR, so Option A is a **sequence** of improvements, not a single cutover.

**Trigger:** first tenant complaint that editing hooks / form fields / conditions in JSON is a barrier, OR workflow authoring crosses ~20 custom definitions across all tenants.

**Sub-items:**
- **Hooks editor** — visual builder for OnEnter / OnExit with per-hook-type forms (Notify template picker, Activity action picker, Webhook event picker, InApp notify target picker). Removes the `onEnter` / `onExit` JSON blocks.
- **Form fields editor** — visual row-based editor with per-type controls (add / reorder / delete fields with inline type dropdown, options list for select). Removes the `formFields` JSON block.
- **Parallel / quorum editor** — mode dropdown + assignees list builder. Removes the `parallel` JSON block.
- **Compound condition editor** — tree-style AND/OR/NOT nested builder. Removes the `condition` JSON block. Requires updating FE `ConditionConfig` type to match the BE's compound shape (currently single-field only in FE).
- **Assignee fallback** — dedicated sub-form (not JSON). Removes the `fallback` JSON block.

### Simulation / dry-run

**What:** a "Run" mode that walks a mock instance through the designed workflow without persistence, so authors can verify that triggers and conditions fire as expected.

**Why deferred:** meaningful simulation needs a light stub of assignee resolution, condition evaluation, and hook execution; that's a chunk of code parallel to the real engine. Ship the authoring surface first, then see whether users actually need a simulator vs just starting a real test instance against a draft.

**Pick this up when:** authors repeatedly ship broken definitions because they can't preview them.

### Collaborative editing / presence

**What:** cursors + live avatars showing who else is in the designer on the same definition.

**Why deferred:** needs a persistent collaboration channel (WebSocket or Yjs/CRDT), server-side conflict resolution, and UI affordances. Not justified until multiple authors regularly edit the same definition at once.

**Pick this up when:** two or more tenant users report conflicting edits on the same definition, OR a tenant explicitly asks for team-editing.

### Definition version history + diff

**What:** store a history of saved definitions and let users compare two versions, restore an old one, and annotate changes.

**Why deferred:** needs schema (history table, audit trail), UX (diff renderer), and storage-growth considerations. Today, audit logs already capture `WorkflowDefinition.Updated` events — users can reconstruct recent history if they really need to.

**Pick this up when:** tenant compliance or change-management requirements force it, OR authors lose work due to unintended saves.

### Designer import / export beyond existing JSON

**What:** export a definition as YAML or a visual image (PNG); import a definition from a different tenant's export bundle.

**Why deferred:** YAML is just a serialization format bike-shed; image export is `reactflow-to-image` (3rd-party). Neither moves the needle vs "copy the JSON body".

**Pick this up when:** a specific integration (marketplace, template sharing) makes a non-JSON format useful.

### Accessibility-first keyboard authoring

**What:** full screen-reader + keyboard-only authoring mode with spoken state/edge descriptions and a linear-list navigator fallback.

**Why deferred:** React Flow ships basic keyboard nav (arrow-select, delete, enter-to-edit). MVP meets that bar. A full a11y authoring experience requires custom navigators and labels beyond the designer surface.

**Pick this up when:** an enterprise tenant has a compliance requirement (Section 508, WCAG 2.2 AA for authoring tools) or reports accessibility as a blocker.

### AI-assisted definition authoring

**What:** natural-language prompt to scaffold a workflow ("build me an expense-approval with first-line then director then finance"), AI-generated state suggestions, AI-generated condition expressions from English.

**Why deferred:** wait for the workflow-AI integration already outlined in Phase 6 of the workflow roadmap (`IWorkflowAiService`, AI assignee provider). Authoring-AI is a natural extension of that work, not an MVP-designer feature.

**Pick this up when:** Phase 6 ships.

### Per-state-type richer node skinning

**What:** distinct visual node shapes / iconography per `type` beyond today's color strip + badge (e.g. diamond for conditional routes, double-ring for parallel, pill for SystemAction).

**Why deferred:** diminishing returns vs Option A scope. Nice-to-have, no tenant demand.

**Pick this up when:** UX feedback consistently flags that state types are hard to tell apart at a glance.

### New state-machine constructs

**What:** compensating transitions, sub-workflows / state inlining, timeout-based auto-transitions with visual designer affordances.

**Why deferred:** these are engine-level capabilities, not designer features. The roadmap treats them as engine work that the designer will pick up once the engine supports them. No engine work in Phase 4c.

**Pick this up when:** engine gains the construct AND a tenant needs it in the authoring UX.

---

## Out of scope for this phase

- No new engine capabilities.
- No changes to `WorkflowDefinition.Create` shape.
- No migrations.
- No new permissions beyond reusing `Workflows.ManageDefinitions`.
- No changes to how the engine deserializes `statesJson` / `transitionsJson` — `UiPosition` is additive.

## Cross-cutting concerns

### Multi-tenancy

Unchanged. All reads/writes already tenant-scoped via existing query filters on `WorkflowDefinition`. Cloning a template produces a tenant-scoped copy (existing behavior).

### Backward compatibility

Adding `UiPosition` to `WorkflowStateConfig` is additive and nullable; existing templates and stored definitions continue to deserialize without change.

### Performance

- Expected node counts: 3–15 per definition (based on shipped templates). React Flow handles hundreds comfortably. No virtualization needed.
- Dagre layout runs in O(nodes + edges); negligible for any realistic definition.
- No server round-trips while editing (pure client state until Save).

### Documentation upkeep

After merge, move Phase 4c into "Phase X Shipped" in `docs/roadmaps/workflow.md` and add the deferred items from the "Future roadmap entries" section above.
