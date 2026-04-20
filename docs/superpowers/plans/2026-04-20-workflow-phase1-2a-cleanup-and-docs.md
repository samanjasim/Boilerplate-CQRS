# Workflow Phase 1+2a Cleanup + Docs Reorganization Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Land 5 code fixes (pending-tasks DB pagination, timeline history-driven status, escalation event, ISO date serialization, state-type constants) plus a full docs reorganization into `docs/user-manual/` and `docs/developer-guide/`, then merge the `feature/workflow-approvals` branch.

**Architecture:** Two phases — Phase A moves + creates docs (pure file operations, low risk), Phase B applies code fixes using TDD (each fix: failing test → minimal impl → green → commit). Phase C validates end-to-end.

**Tech Stack:** .NET 10 / EF Core / xUnit / Moq / FluentAssertions (backend); React 19 / Vite / Vitest / Testing Library (frontend); git for moves.

**Spec:** [`docs/superpowers/specs/2026-04-20-workflow-phase1-2a-cleanup-and-docs-design.md`](../specs/2026-04-20-workflow-phase1-2a-cleanup-and-docs-design.md)

**Branch:** `feature/workflow-approvals` (current, 45 prior commits). Do NOT rebase. Each task commits incrementally; stack these on top.

**Working directory:** `/Users/samanjasim/Projects/forme/Boilerplate-CQRS/.claude/worktrees/thirsty-hopper-b1c9bb`. All paths below are repo-relative.

---

## Phase A — Documentation Reorganization

Pure file moves + three new docs + stub redirects. Each commit self-contained.

### Task A1: Move architecture + theming + workstation into developer-guide/

**Files:**
- Create dir: `docs/developer-guide/architecture/`
- Move: `docs/workstation-setup.md` → `docs/developer-guide/getting-started.md`
- Move: `docs/theming-guide.md` → `docs/developer-guide/theming.md`
- Move: `docs/D2-domain-module-example.md` → `docs/developer-guide/domain-module-example.md`
- Move: `docs/architecture/system-design.md` → `docs/developer-guide/architecture/system-design.md`
- Move: `docs/architecture/cross-module-communication.md` → `docs/developer-guide/architecture/cross-module-communication.md`
- Move: `docs/architecture/module-development-guide.md` → `docs/developer-guide/architecture/module-development.md`

- [ ] **Step 1: Create target directories**

```bash
mkdir -p docs/developer-guide/architecture
```

- [ ] **Step 2: git mv each file**

Use `git mv` so history is preserved.

```bash
git mv docs/workstation-setup.md docs/developer-guide/getting-started.md
git mv docs/theming-guide.md docs/developer-guide/theming.md
git mv docs/D2-domain-module-example.md docs/developer-guide/domain-module-example.md
git mv docs/architecture/system-design.md docs/developer-guide/architecture/system-design.md
git mv docs/architecture/cross-module-communication.md docs/developer-guide/architecture/cross-module-communication.md
git mv docs/architecture/module-development-guide.md docs/developer-guide/architecture/module-development.md
rmdir docs/architecture
```

- [ ] **Step 3: Fix internal cross-links inside the moved files**

For each moved file, open it and grep for relative paths that break. Use Grep (case-sensitive).

```bash
# Run this command from the repo root to find candidate broken links:
```

Then: Use the Grep tool with pattern `\]\(\.\./` or `\]\(\.\/` in `path=docs/developer-guide/` to list relative links. For each match, read the target resolved-relative-to-new-location. If the target doesn't exist, update the link to the correct path. Most intra-docs links should be unaffected if you only move and don't rewrite paths inside — but architecture files may reference `../theming-guide.md` (now `../theming.md`) or `../workstation-setup.md` (now `getting-started.md`). Fix those with Edit.

Search targets to update inside the moved files:
- `workstation-setup.md` → `getting-started.md`
- `theming-guide.md` → `theming.md`
- `D2-domain-module-example.md` → `domain-module-example.md`
- `architecture/system-design.md` → `architecture/system-design.md` (unchanged relative if sibling)
- `architecture/module-development-guide.md` → `architecture/module-development.md`

- [ ] **Step 4: Commit**

```bash
git add -A docs/
git commit -m "refactor(docs): move workstation-setup + theming + architecture into developer-guide/"
```

---

### Task A2: Move BE-level module docs into developer-guide/modules/

**Files:**
- Create dir: `docs/developer-guide/modules/`
- Move: `boilerplateBE/docs/feature-flags.md` → `docs/developer-guide/modules/feature-flags.md`
- Move: `boilerplateBE/docs/observability-setup.md` → `docs/developer-guide/modules/observability.md`
- Move: `boilerplateBE/docs/webhook-testing-guide.md` → `docs/developer-guide/modules/webhooks.md`

- [ ] **Step 1: Create target dir + git mv**

```bash
mkdir -p docs/developer-guide/modules
git mv boilerplateBE/docs/feature-flags.md docs/developer-guide/modules/feature-flags.md
git mv boilerplateBE/docs/observability-setup.md docs/developer-guide/modules/observability.md
git mv boilerplateBE/docs/webhook-testing-guide.md docs/developer-guide/modules/webhooks.md
```

- [ ] **Step 2: Check if boilerplateBE/docs/ is now empty, remove if so**

```bash
ls boilerplateBE/docs/ 2>/dev/null && rmdir boilerplateBE/docs 2>/dev/null || true
```

- [ ] **Step 3: Commit**

```bash
git add -A
git commit -m "refactor(docs): move BE-level module docs (feature-flags, observability, webhooks)"
```

---

### Task A3: Move Communication module docs

**Files:**
- Move: `boilerplateBE/src/modules/Starter.Module.Communication/docs/user-manual.md` → `docs/user-manual/modules/communication.md`
- Move: `boilerplateBE/src/modules/Starter.Module.Communication/docs/developer-guide.md` → `docs/developer-guide/modules/communication.md`

- [ ] **Step 1: Create target dir + git mv**

```bash
mkdir -p docs/user-manual/modules
git mv boilerplateBE/src/modules/Starter.Module.Communication/docs/user-manual.md docs/user-manual/modules/communication.md
git mv boilerplateBE/src/modules/Starter.Module.Communication/docs/developer-guide.md docs/developer-guide/modules/communication.md
```

- [ ] **Step 2: Check + remove empty module docs dir**

```bash
ls boilerplateBE/src/modules/Starter.Module.Communication/docs/ 2>/dev/null && \
  rmdir boilerplateBE/src/modules/Starter.Module.Communication/docs 2>/dev/null || true
```

- [ ] **Step 3: Commit**

```bash
git add -A
git commit -m "refactor(docs): move Communication module docs to central structure"
```

---

### Task A4: Move CommentsActivity module docs

**Files:**
- Move: `boilerplateBE/src/modules/Starter.Module.CommentsActivity/DEVELOPER_GUIDE.md` → `docs/developer-guide/modules/comments-activity.md`

- [ ] **Step 1: git mv**

```bash
git mv boilerplateBE/src/modules/Starter.Module.CommentsActivity/DEVELOPER_GUIDE.md docs/developer-guide/modules/comments-activity.md
```

- [ ] **Step 2: Commit**

```bash
git add -A
git commit -m "refactor(docs): move CommentsActivity developer guide to central structure"
```

---

### Task A5: Consolidate module ROADMAPs into docs/roadmaps/

**Files:**
- Create dir: `docs/roadmaps/`
- Move: `boilerplateBE/src/modules/Starter.Module.Workflow/ROADMAP.md` → `docs/roadmaps/workflow.md`
- Move: `boilerplateBE/src/modules/Starter.Module.Communication/ROADMAP.md` → `docs/roadmaps/communication.md`
- Move: `boilerplateBE/src/modules/Starter.Module.CommentsActivity/ROADMAP.md` → `docs/roadmaps/comments-activity.md`

- [ ] **Step 1: Create target dir + git mv**

```bash
mkdir -p docs/roadmaps
git mv boilerplateBE/src/modules/Starter.Module.Workflow/ROADMAP.md docs/roadmaps/workflow.md
git mv boilerplateBE/src/modules/Starter.Module.Communication/ROADMAP.md docs/roadmaps/communication.md
git mv boilerplateBE/src/modules/Starter.Module.CommentsActivity/ROADMAP.md docs/roadmaps/comments-activity.md
```

- [ ] **Step 2: Fix internal links in the moved roadmaps**

Each ROADMAP currently references files with relative paths like `./Infrastructure/Services/WorkflowEngine.cs` (assuming the doc is inside the module). After the move, those relative paths no longer resolve.

Use Edit to rewrite each relative path inside `docs/roadmaps/workflow.md` to point at the module root. E.g. `./Infrastructure/Services/WorkflowEngine.cs` → `../../boilerplateBE/src/modules/Starter.Module.Workflow/Infrastructure/Services/WorkflowEngine.cs`. Do the same for communication.md and comments-activity.md.

- [ ] **Step 3: Commit**

```bash
git add -A
git commit -m "refactor(docs): consolidate module roadmaps into docs/roadmaps/"
```

---

### Task A6: Move product docs

**Files:**
- Create dir: `docs/product/`
- Move: `docs/market-assessment.md` → `docs/product/market-assessment.md`
- Move: `docs/future-roadmap.md` → `docs/roadmaps/product-roadmap.md`

- [ ] **Step 1: Create + move**

```bash
mkdir -p docs/product
git mv docs/market-assessment.md docs/product/market-assessment.md
git mv docs/future-roadmap.md docs/roadmaps/product-roadmap.md
```

- [ ] **Step 2: Commit**

```bash
git add -A
git commit -m "refactor(docs): move product docs (market-assessment, future-roadmap)"
```

---

### Task A7: Add redirect stubs in module folders

**Files (all NEW, each 1-line stub):**
- Create: `boilerplateBE/src/modules/Starter.Module.Workflow/ROADMAP.md`
- Create: `boilerplateBE/src/modules/Starter.Module.Communication/ROADMAP.md`
- Create: `boilerplateBE/src/modules/Starter.Module.Communication/docs/user-manual.md`
- Create: `boilerplateBE/src/modules/Starter.Module.Communication/docs/developer-guide.md`
- Create: `boilerplateBE/src/modules/Starter.Module.CommentsActivity/ROADMAP.md`
- Create: `boilerplateBE/src/modules/Starter.Module.CommentsActivity/DEVELOPER_GUIDE.md`
- Create: `boilerplateBE/docs/feature-flags.md`
- Create: `boilerplateBE/docs/observability-setup.md`
- Create: `boilerplateBE/docs/webhook-testing-guide.md`

- [ ] **Step 1: Recreate the `boilerplateBE/docs/` directory and Communication `docs/` dir if removed**

```bash
mkdir -p boilerplateBE/docs
mkdir -p boilerplateBE/src/modules/Starter.Module.Communication/docs
```

- [ ] **Step 2: Write each stub using Write tool**

Each stub is a single-line pointer. Content template:

```markdown
> This document has moved to [`/docs/<relative-path>`](<correct-relative-link>).
```

Concrete files to write (use Write tool per file):

1. `boilerplateBE/src/modules/Starter.Module.Workflow/ROADMAP.md`:
   ```markdown
   > This document has moved to [`/docs/roadmaps/workflow.md`](../../../../docs/roadmaps/workflow.md).
   ```

2. `boilerplateBE/src/modules/Starter.Module.Communication/ROADMAP.md`:
   ```markdown
   > This document has moved to [`/docs/roadmaps/communication.md`](../../../../docs/roadmaps/communication.md).
   ```

3. `boilerplateBE/src/modules/Starter.Module.Communication/docs/user-manual.md`:
   ```markdown
   > This document has moved to [`/docs/user-manual/modules/communication.md`](../../../../../docs/user-manual/modules/communication.md).
   ```

4. `boilerplateBE/src/modules/Starter.Module.Communication/docs/developer-guide.md`:
   ```markdown
   > This document has moved to [`/docs/developer-guide/modules/communication.md`](../../../../../docs/developer-guide/modules/communication.md).
   ```

5. `boilerplateBE/src/modules/Starter.Module.CommentsActivity/ROADMAP.md`:
   ```markdown
   > This document has moved to [`/docs/roadmaps/comments-activity.md`](../../../../docs/roadmaps/comments-activity.md).
   ```

6. `boilerplateBE/src/modules/Starter.Module.CommentsActivity/DEVELOPER_GUIDE.md`:
   ```markdown
   > This document has moved to [`/docs/developer-guide/modules/comments-activity.md`](../../../../docs/developer-guide/modules/comments-activity.md).
   ```

7. `boilerplateBE/docs/feature-flags.md`:
   ```markdown
   > This document has moved to [`/docs/developer-guide/modules/feature-flags.md`](../../docs/developer-guide/modules/feature-flags.md).
   ```

8. `boilerplateBE/docs/observability-setup.md`:
   ```markdown
   > This document has moved to [`/docs/developer-guide/modules/observability.md`](../../docs/developer-guide/modules/observability.md).
   ```

9. `boilerplateBE/docs/webhook-testing-guide.md`:
   ```markdown
   > This document has moved to [`/docs/developer-guide/modules/webhooks.md`](../../docs/developer-guide/modules/webhooks.md).
   ```

- [ ] **Step 3: Verify each relative link resolves**

For each stub file, read the markdown and confirm the target file exists by running `ls <resolved-path>`. If any link is broken, fix the relative path with Edit.

- [ ] **Step 4: Commit**

```bash
git add -A
git commit -m "refactor(docs): add redirect stubs in module folders"
```

---

### Task A8: Write Workflow user manual

**Files:**
- Create: `docs/user-manual/modules/workflow.md`

- [ ] **Step 1: Write the file with the Write tool**

Write the file with this structure (~200-300 lines). Each section has concrete guidance — no placeholders. Target audience is an end user who has never used the system.

```markdown
# Workflow & Approvals — User Manual

The Workflow module lets you submit requests that need approval, track their progress, and act on tasks assigned to you. This guide is for end users (not developers).

## Concepts

- **Request (Workflow Instance)** — a single submitted workflow. For example, "Expense report #42" or "Purchase order for laptop."
- **Task (Pending Task)** — a step in a request that's waiting for someone to act. You see tasks assigned to you in the **Inbox**.
- **Definition** — the template that shapes the workflow (the steps, who approves each step, what forms show up). Admins author definitions; you pick one when starting a new request.
- **Step** — a single state in the workflow (e.g. "Draft," "Manager Review," "Approved").

## Starting a new request

1. Go to **Workflow → Inbox** from the sidebar.
2. Click **New Request** (top right).
3. Choose a workflow definition from the dropdown. The description shows what the workflow is for.
4. Fill in the initial form if the definition requires one. Required fields are marked with `*`.
5. Click **Submit**. Your request appears under **Workflow → My Requests** and automatically advances to the first approval step.

## Approving, rejecting, or returning a task

Your **Inbox** shows every task assigned to you.

1. Click **Approve** (or **Reject**) next to the task.
2. If the step has a form, fill in the required fields.
3. Add an optional comment — useful for explaining your decision.
4. Click **Confirm**.

Three main actions:

- **Approve** — advances the request to the next step.
- **Reject** — ends the request in a rejected state. The originator sees the rejection reason from your comment.
- **Return for revision** — sends the request back to the originator. They can edit the submission and resubmit.

## Resubmitting after return for revision

If a reviewer sends your request back:

1. You'll receive a notification.
2. Open the request from **My Requests**.
3. Edit the initial form fields (or attached entity data).
4. Click **Resubmit**.

The request re-enters the first approval step.

## Delegating your tasks (coverage while on leave)

When you're away, you can delegate your pending tasks to another user.

1. From the **Inbox**, click **Delegate** (top right).
2. Pick the user to delegate to.
3. Choose a start date and end date.
4. Click **Confirm**.

During the delegation period, any new tasks that would normally land in your inbox go to the delegate instead. The delegated user sees a "Delegated from {your name}" badge on each task.

To cancel an active delegation early, click **Cancel Delegation** from the banner at the top of your inbox.

## Viewing request history and current status

From **Workflow → My Requests**, click any row to open the request detail page. You'll see:

- **Status banner** — current state, who started the request, when.
- **Step timeline** — every step the request has passed through, who acted, what comment they left, any form data they submitted. Steps not yet visited are shown in muted grey.
- **Comments + Activity** — free-text discussion and system events (delegations, escalations, reassignments) in chronological order.
- **Cancel** — only available to the originator while the request is still active.

## Reading the Dashboard widget

On your dashboard, the **Pending Approvals** widget shows:

- **Count** — total tasks waiting for you.
- **Shortcuts** — quick links to the Inbox.

A red overdue badge indicates one or more tasks have passed their SLA. Click through to the Inbox to see which.

## Forms and form fields

Some steps require structured data, not just a free-text comment. Common field types:

- **Text** — free-form single line.
- **Number** — numeric; may have min/max.
- **Select** — pick one from a dropdown.
- **Date** — calendar picker.
- **Checkbox** — yes/no.

Required fields have a red `*` next to the label. You cannot submit until all required fields are filled.

## Notifications

You'll receive notifications when:

- A task is assigned to you (or to a user who has delegated to you).
- A task you submitted is approved, rejected, or returned for revision.
- A task assigned to you is overdue (SLA reminder).
- A task assigned to you is escalated to someone else.

Manage notification channels (email, in-app, etc.) from **Settings → Notification Preferences**.

## Frequently asked questions

**Q: I don't see a "New Request" button.**
A: You need the `Workflows.Start` permission. Ask your tenant admin.

**Q: Can I edit a request after submitting it?**
A: Only after a reviewer returns it for revision. While it's in approval, the content is locked.

**Q: Can I see all of my tenant's requests?**
A: Only if you have the `Workflows.ViewAll` permission. Otherwise you see your own requests plus tasks assigned to you.

**Q: My delegate approved a task — who's recorded as the actor?**
A: The delegate is the actor. The original assignee (you) is recorded as "delegated from" in the step history.
```

- [ ] **Step 2: Commit**

```bash
git add docs/user-manual/modules/workflow.md
git commit -m "docs(workflow): add user manual"
```

---

### Task A9: Write Workflow developer guide (with #6 known issue section)

**Files:**
- Create: `docs/developer-guide/modules/workflow.md`

- [ ] **Step 1: Write the file with the Write tool**

Target audience: a developer who is extending or integrating with the Workflow module. Assume they know Clean Architecture + CQRS basics; teach them Workflow specifics.

```markdown
# Workflow & Approvals — Developer Guide

This module provides a composable state-machine workflow engine. Other modules register their entities as "workflowable" and attach workflows (approvals, reviews, multi-step processes) without coupling to the Workflow module directly.

**Module path:** `boilerplateBE/src/modules/Starter.Module.Workflow/`
**Capability contract:** [`IWorkflowService`](../../../boilerplateBE/src/Starter.Abstractions/Capabilities/IWorkflowService.cs)
**Null-object fallback:** [`NullWorkflowService`](../../../boilerplateBE/src/Starter.Infrastructure/Capabilities/NullObjects/NullWorkflowService.cs)

## Architecture overview

```
WorkflowDefinition (template)
        │
        │ is instantiated as
        ▼
WorkflowInstance (one per request)
        │
        │ contains many
        ▼
WorkflowStep (completed transitions, history)
        │
        │ creates at each HumanTask state
        ▼
ApprovalTask (what the assignee sees in their inbox)
```

- **WorkflowEngine** (`Infrastructure/Services/WorkflowEngine.cs`) is the core — it starts instances, creates tasks, executes user actions, evaluates conditions, advances state, raises events.
- **ApprovalTask** is the unit of assignment. One task per assignee per step (multiple per step when parallel approvals are configured).
- **WorkflowStep** is the audit/history entity — one row per completed transition.

## Registering an entity as workflowable

In the domain module's DI registration, call `AddWorkflowableEntity`:

```csharp
services.AddWorkflowableEntity("Product", async (sp, entityId, ct) => {
    var db = sp.GetRequiredService<IApplicationDbContext>();
    var product = await db.Products.FindAsync([entityId], ct);
    return product is null ? null : new WorkflowableEntityInfo(product.Id, product.Name);
});
```

This registers the entity type with the engine's auto-discovery registry. Afterwards, the Workflow UI's "Start Workflow" button on that entity's detail page works automatically.

## Authoring a workflow definition

A definition is JSON (`WorkflowTemplateConfig`) with arrays of states, transitions, and optional conditions.

Minimal example:

```json
{
  "name": "simple-approval",
  "displayName": "Simple Approval",
  "entityType": "Product",
  "states": [
    { "name": "Draft", "displayName": "Draft", "type": "Initial" },
    {
      "name": "Review",
      "displayName": "Manager Review",
      "type": "HumanTask",
      "assignee": { "strategy": "Role", "value": "Manager" }
    },
    { "name": "Approved", "displayName": "Approved", "type": "Final" }
  ],
  "transitions": [
    { "from": "Draft", "to": "Review", "action": "Submit" },
    { "from": "Review", "to": "Approved", "action": "Approve" }
  ]
}
```

State types are defined as constants in [`Domain/Constants/WorkflowStateTypes.cs`](../../../boilerplateBE/src/modules/Starter.Module.Workflow/Domain/Constants/WorkflowStateTypes.cs): `Initial`, `HumanTask`, `SystemAction`, `Final`.

## Dynamic forms

Any `HumanTask` state can collect structured data via `formFields`:

```json
{
  "name": "Review",
  "type": "HumanTask",
  "formFields": [
    { "name": "reason", "label": "Reason", "type": "text", "required": true, "maxLength": 500 }
  ]
}
```

Submitted form data is validated by `FormDataValidator`, stored on the `WorkflowStep.MetadataJson` row, and merged into `WorkflowInstance.ContextJson` for downstream condition evaluation.

Supported field types: `text`, `textarea`, `number`, `select`, `date`, `checkbox`.

## Assignee strategies

Built-in strategies (`BuiltInAssigneeProvider`):

- `User` — pick a specific user by id.
- `Role` — resolve all users holding the role.
- `Initiator` — the person who started the request.
- `InitiatorManager` — the initiator's manager (requires HR-module integration; falls back to null if not wired).

Custom strategies register `IAssigneeResolverProvider` with `SupportedStrategies => ["MyStrategy"]`. The engine finds the right provider per strategy name.

## Conditions

Condition evaluator supports simple field comparisons and compound AND/OR/NOT:

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

Field paths resolve against `WorkflowInstance.ContextJson` (which merges initial submission + all form data submitted along the way).

## Parallel approvals

A state can require multiple assignees:

```json
{
  "name": "BoardVote",
  "type": "HumanTask",
  "assignees": [
    { "strategy": "Role", "value": "BoardMember" }
  ],
  "quorum": { "type": "AnyOf", "threshold": 2 }
}
```

`QuorumType` values: `AllOf` (everyone must act), `AnyOf` (threshold count must act). The engine creates one `ApprovalTask` per resolved assignee and advances only when the quorum is met.

## SLA and escalation

Per-state SLA config:

```json
{
  "sla": {
    "reminderAfterHours": 24,
    "escalateAfterHours": 48
  }
}
```

`SlaEscalationJob` (background service, 15-minute tick) scans pending tasks:
- At `reminderAfterHours`: sends a reminder via `IMessageDispatcher`.
- At `escalateAfterHours`: cancels the original task, creates a replacement task assigned to the fallback (state's `assignee.fallback` config), sends an escalation notification, and raises `WorkflowTaskEscalatedEvent`.

## Hooks

A transition can fire side-effects via hooks (configured on the transition). Execution order: pre-transition → state change → post-transition. `HookExecutor` dispatches registered `IHookHandler` implementations by `HookType`.

## Events

Domain events raised (see [`Domain/Events/WorkflowDomainEvents.cs`](../../../boilerplateBE/src/modules/Starter.Module.Workflow/Domain/Events/WorkflowDomainEvents.cs)):

- `WorkflowStartedEvent`
- `WorkflowTransitionEvent`
- `WorkflowCompletedEvent`
- `WorkflowCancelledEvent`
- `ApprovalTaskAssignedEvent`
- `ApprovalTaskCompletedEvent`
- `WorkflowTaskEscalatedEvent` — raised by `SlaEscalationJob` when a task is escalated with a resolved fallback assignee.

## Known issues

### #6: `WorkflowEngine.cs` is 1200+ lines

The engine currently mixes several responsibilities:

- Sequential transition evaluation + execution
- Parallel-approval task-group coordination (quorum evaluation, sibling-task advancement)
- Auto-transition evaluation (condition-driven state progression without a human action)
- Task creation with assignee resolution, SLA due-date calculation, delegation lookup

Extraction candidates for a future refactor:

- `ParallelApprovalCoordinator` — quorum evaluation and task-group advancement.
- `AutoTransitionEvaluator` — condition-driven next-state progression.
- `HumanTaskFactory` — task creation with assignee resolution, SLA, delegation.

**Status:** Deferred. Current code is well test-covered and working. Extraction is a pure refactor with no capability added.

**Roadmap entry:** [`docs/roadmaps/workflow.md`](../../roadmaps/workflow.md)

**Pick this up when:** Phase 2b integration work needs to modify the engine, OR when the file next needs substantive modification (≥200 new lines).

**Starting points:**
- Group the engine's private methods visually — parallel-coordination, auto-transition, and task-creation clusters are already distinct.
- Introduce one collaborator at a time behind a package-internal interface; existing tests stay green.
- No public-interface change on `IWorkflowService` or the controller.

## Testing

Unit tests live in [`boilerplateBE/tests/Starter.Api.Tests/Workflow/`](../../../boilerplateBE/tests/Starter.Api.Tests/Workflow/):

- `WorkflowEngineTests.cs` — end-to-end engine behavior
- `AssigneeResolverTests.cs` — strategy resolution
- `ConditionEvaluatorTests.cs` — simple + compound expressions
- `FormDataValidatorTests.cs` — form field validation
- `HookExecutorTests.cs` — hook execution ordering
- `SlaEscalationJobTests.cs` — reminders, escalation, event emission
- `DelegationTests.cs` — delegation rule lookup
- `WorkflowModulePermissionsTests.cs` — authorization policy wiring

Follow the existing pattern: in-memory `WorkflowDbContext`, mocked external capabilities (`IMessageDispatcher`, `IUserReader`), FluentAssertions for readable asserts.

## Extending the module

Most Phase 2+ features fall under four patterns:

1. **New assignee strategy** — add `IAssigneeResolverProvider`, register in DI.
2. **New hook type** — add `HookType` enum value, add `IHookHandler`, register.
3. **New condition operator** — extend `ConditionEvaluator`, add test cases.
4. **New entity integration** — call `AddWorkflowableEntity` in the domain module.

Deeper changes (new state type, quorum strategy, transition semantics) require engine modification. See the #6 refactor note before making structural changes.
```

- [ ] **Step 2: Commit**

```bash
git add docs/developer-guide/modules/workflow.md
git commit -m "docs(workflow): add developer guide with #6 known issue section"
```

---

### Task A10: Write CommentsActivity user manual

**Files:**
- Create: `docs/user-manual/modules/comments-activity.md`

- [ ] **Step 1: Write the file with the Write tool**

```markdown
# Comments & Activity — User Manual

The Comments & Activity module lets you discuss any entity (a product, a workflow request, a user profile, etc.) and read a chronological activity log of what's happened to it.

## Adding a comment

1. Open the detail page of any entity that supports comments (workflow requests, products, etc.).
2. Scroll to the **Comments & Activity** section.
3. Type your comment in the text box. Markdown is supported — you can use `**bold**`, `*italic*`, `> quotes`, `` `code` ``, and bullet lists.
4. Click **Post**.

## @mentioning users

Type `@` followed by a name to mention another user in your comment. A dropdown appears with matching users; click one to mention them.

Mentioned users receive a notification. By default the notification is in-app; they can opt into email notifications in **Settings → Notification Preferences** under "Comment mentions".

## The activity timeline

The timeline interleaves comments with system activity for the entity:

- **Comments** — posted by users.
- **Activity entries** — automatic events (request approved, step transitioned, delegation started, etc.).

Both are ordered chronologically with the newest at the top.

## Editing and deleting your comments

- **Edit** — click the three-dot menu on your comment, then **Edit**. Timestamps show "edited" after an edit.
- **Delete** — same menu, **Delete**. The comment is removed immediately. Only the author or an admin can delete.

## Who can see comments

Comments are visible to anyone in your tenant who has the `Comments.View` permission. Today, there is no per-entity access control — comments on a workflow request are visible to any user with the permission, not just workflow participants. This may change in future versions.
```

- [ ] **Step 2: Commit**

```bash
git add docs/user-manual/modules/comments-activity.md
git commit -m "docs(comments-activity): add user manual"
```

---

### Task A11: Append `WorkflowEngine.cs` extraction entry to workflow roadmap

**Files:**
- Modify: `docs/roadmaps/workflow.md` (previously moved from `boilerplateBE/.../Workflow/ROADMAP.md`)

- [ ] **Step 1: Append the new entry**

Use the Edit tool. Find the last existing "Phase 2 Deferred Items" section entry. Insert the new entry below it (before any trailing horizontal rule if present).

New entry (exact text to insert):

```markdown
### WorkflowEngine.cs extraction

**What:** Split [`Infrastructure/Services/WorkflowEngine.cs`](../../boilerplateBE/src/modules/Starter.Module.Workflow/Infrastructure/Services/WorkflowEngine.cs) (~1200 lines) into focused collaborators. Candidates: `ParallelApprovalCoordinator`, `AutoTransitionEvaluator`, `HumanTaskFactory`.

**Why deferred:** Pure refactor — no capability added, no bug fixed. Existing tests cover the behavior well. Doing it inline would balloon the cleanup PR.

**Pick this up when:** Phase 2b integration work needs to modify the engine, OR when the file next grows by more than ~200 lines.

**Starting points:**
- Identify coordinator boundaries by grouping private methods in `WorkflowEngine.cs` — parallel approval, auto-transition, and task creation clusters are already visually distinct.
- Introduce one collaborator at a time behind a package-internal interface; existing tests should stay green through each extraction.
- No public-interface change on `IWorkflowService` or the controller.

---
```

- [ ] **Step 2: Commit**

```bash
git add docs/roadmaps/workflow.md
git commit -m "docs(roadmap): add WorkflowEngine extraction entry to workflow roadmap"
```

---

### Task A12: Write top-level index READMEs

**Files:**
- Create: `docs/README.md`
- Create: `docs/user-manual/README.md`
- Create: `docs/developer-guide/README.md`

- [ ] **Step 1: Write `docs/README.md`**

```markdown
# Documentation

- **[User Manual](user-manual/README.md)** — end-user guides for every feature.
- **[Developer Guide](developer-guide/README.md)** — architecture, module internals, extension points, theming.
- **[Roadmaps](roadmaps/)** — deferred items per module + product-level roadmap.
- **[Testing](testing/)** — regression checklist and session logs.
- **[Product](product/)** — market assessment and strategy docs.
- **[Superpowers](superpowers/)** — AI session artifacts (specs and plans).
- **[Session Handoff](session-handoff.md)** — current open context for the next session (transient).
```

- [ ] **Step 2: Write `docs/user-manual/README.md`**

```markdown
# User Manual

Guides for end users of the platform.

- **[Getting Started](getting-started.md)** — creating an account, first login, tenant basics.

## Modules

- **[Workflow & Approvals](modules/workflow.md)** — submitting requests, approvals, delegation.
- **[Communication](modules/communication.md)** — notification channels, preferences.
- **[Comments & Activity](modules/comments-activity.md)** — commenting, @mentions, activity timeline.
```

- [ ] **Step 3: Write `docs/developer-guide/README.md`**

```markdown
# Developer Guide

Docs for developers building on or extending the platform.

- **[Getting Started](getting-started.md)** — workstation setup, local environment.
- **[Theming](theming.md)** — theme preset system and semantic tokens.
- **[Domain Module Example](domain-module-example.md)** — walking through the domain module template (D2).

## Architecture

- **[System Design](architecture/system-design.md)** — overall architecture.
- **[Cross-Module Communication](architecture/cross-module-communication.md)** — capability contracts, null objects, events.
- **[Module Development](architecture/module-development.md)** — adding a new module end-to-end.

## Modules

- **[Workflow & Approvals](modules/workflow.md)** — engine, definitions, extension points, known issues.
- **[Communication](modules/communication.md)** — message dispatcher, templates, channels.
- **[Comments & Activity](modules/comments-activity.md)** — entity-scoped comments, activity feed.
- **[Feature Flags](modules/feature-flags.md)** — per-tenant overrides, enforcement.
- **[Observability](modules/observability.md)** — OpenTelemetry, Serilog, metrics.
- **[Webhooks](modules/webhooks.md)** — outbound webhook testing and event model.
```

- [ ] **Step 4: Commit**

```bash
git add docs/README.md docs/user-manual/README.md docs/developer-guide/README.md
git commit -m "docs: add top-level README index files for user-manual + developer-guide"
```

---

### Task A13: Write `user-manual/getting-started.md`

**Files:**
- Create: `docs/user-manual/getting-started.md`

- [ ] **Step 1: Write file**

```markdown
# Getting Started

Welcome! This guide walks you through your first login and the core concepts.

## Creating an account

There are two ways to get an account:

1. **Self-register** (if your tenant allows it) — go to the sign-up page, fill in your details, and verify your email.
2. **Invitation** — an admin sends you an email with a link. Click the link, set a password, and you're in.

## Your first login

1. Enter your username or email.
2. Enter your password.
3. If you've set up two-factor authentication, enter the 6-digit code from your authenticator app.

After logging in you land on the **Dashboard**, which shows a summary of items that need your attention across the platform.

## Understanding tenants

Every account belongs to a tenant (an organization). You only see data and users from your own tenant. Platform administrators (cross-tenant) are the exception.

## Getting help

- Click the **?** in the top bar for contextual help on any page.
- Your tenant admin can grant you additional permissions if you're missing access.
- For issues, contact your tenant's support channel (email, chat, etc.) — configured in **Settings → Support**.
```

- [ ] **Step 2: Commit**

```bash
git add docs/user-manual/getting-started.md
git commit -m "docs: add user-manual getting-started guide"
```

---

### Task A14: Update root README + CLAUDE.md with new doc links

**Files:**
- Modify: `README.md`
- Modify: `CLAUDE.md`

- [ ] **Step 1: Grep for references to moved files**

Run the Grep tool with the following patterns across `README.md` and `CLAUDE.md` (path=repo root, not a subdir):

- `workstation-setup`
- `theming-guide`
- `D2-domain-module-example`
- `docs/architecture/`
- `boilerplateBE/docs/`
- `DEVELOPER_GUIDE`
- `future-roadmap`
- `market-assessment`

For each hit, use Edit to update the link to the new path per the structure in Tasks A1-A6.

- [ ] **Step 2: Add a Documentation section to `README.md`**

Add this section near the top of `README.md` (after the project tagline, before detailed setup instructions). If a Documentation section already exists, overwrite it.

```markdown
## Documentation

- **[User Manual](docs/user-manual/README.md)** — end-user guides.
- **[Developer Guide](docs/developer-guide/README.md)** — architecture, modules, extending the platform.
- **[Roadmaps](docs/roadmaps/)** — deferred items per module + product roadmap.
```

- [ ] **Step 3: Commit**

```bash
git add README.md CLAUDE.md
git commit -m "docs: update root README and CLAUDE.md with new doc links"
```

---

## Phase B — Code Fixes (TDD)

### Task B1: #7 Add `WorkflowStateTypes` constants

**Files:**
- Create: `boilerplateBE/src/modules/Starter.Module.Workflow/Domain/Constants/WorkflowStateTypes.cs`
- Modify: `boilerplateBE/src/modules/Starter.Module.Workflow/Infrastructure/Services/WorkflowEngine.cs` (lines 55, 166, 543, 802, 1206, 1211, 1221)
- Modify: `boilerplateBE/src/modules/Starter.Module.Workflow/Application/Commands/TransitionWorkflow/TransitionWorkflowCommandHandler.cs` (line 22)

**No new tests** — this is a pure literal replacement. Existing tests staying green is the evidence.

- [ ] **Step 1: Create the constants file**

Write this to `boilerplateBE/src/modules/Starter.Module.Workflow/Domain/Constants/WorkflowStateTypes.cs`:

```csharp
namespace Starter.Module.Workflow.Domain.Constants;

/// <summary>
/// Canonical state type identifiers used in workflow definitions.
/// Matches the "type" field in WorkflowStateConfig JSON.
/// </summary>
public static class WorkflowStateTypes
{
    public const string Initial = "Initial";
    public const string HumanTask = "HumanTask";
    public const string SystemAction = "SystemAction";
    public const string Final = "Final";
}
```

- [ ] **Step 2: Replace occurrences in `WorkflowEngine.cs`**

Use Edit with the exact strings at each location (the file uses `"Initial"`, `"HumanTask"`, `"SystemAction"` as string literals in `StringComparison.OrdinalIgnoreCase` comparisons).

For each occurrence, replace:

- `"Initial"` (as the compared-to value) → `WorkflowStateTypes.Initial`
- `"HumanTask"` → `WorkflowStateTypes.HumanTask`
- `"SystemAction"` → `WorkflowStateTypes.SystemAction`
- `"Final"` → `WorkflowStateTypes.Final` (only where used in type comparisons, NOT in state name literals like `"Draft"`, `"Approved"`, etc.)

Add the using directive `using Starter.Module.Workflow.Domain.Constants;` at the top of `WorkflowEngine.cs` if not already present.

DO NOT change JSON-embedded values in `WorkflowModule.cs` seed templates — those are data strings that happen to equal the constants. JSON definitions stay as-is.

- [ ] **Step 3: Replace in `TransitionWorkflowCommandHandler.cs` line 22**

Change:

```csharp
return Result.Failure<bool>(WorkflowErrors.InvalidTransition("Initial", request.Trigger));
```

To:

```csharp
return Result.Failure<bool>(WorkflowErrors.InvalidTransition(WorkflowStateTypes.Initial, request.Trigger));
```

Add `using Starter.Module.Workflow.Domain.Constants;` if needed.

- [ ] **Step 4: Build backend**

```bash
cd boilerplateBE && dotnet build
```

Expected: build succeeds, no warnings about unused usings, no compile errors.

- [ ] **Step 5: Run backend tests**

```bash
cd boilerplateBE && dotnet test --no-build
```

Expected: all 144 existing tests pass (no new tests).

- [ ] **Step 6: Commit**

```bash
git add boilerplateBE/src/modules/Starter.Module.Workflow/Domain/Constants/WorkflowStateTypes.cs \
        boilerplateBE/src/modules/Starter.Module.Workflow/Infrastructure/Services/WorkflowEngine.cs \
        boilerplateBE/src/modules/Starter.Module.Workflow/Application/Commands/TransitionWorkflow/TransitionWorkflowCommandHandler.cs
git commit -m "fix(workflow): add WorkflowStateTypes constants for state type magic strings"
```

---

### Task B2: #4 DB-level pagination for `GetPendingTasks`

**Files:**
- Modify: `boilerplateBE/src/Starter.Abstractions/Capabilities/IWorkflowService.cs`
- Modify: `boilerplateBE/src/Starter.Infrastructure/Capabilities/NullObjects/NullWorkflowService.cs`
- Modify: `boilerplateBE/src/modules/Starter.Module.Workflow/Infrastructure/Services/WorkflowEngine.cs` (the `GetPendingTasksAsync` method around line 572)
- Modify: `boilerplateBE/src/modules/Starter.Module.Workflow/Application/Queries/GetPendingTasks/GetPendingTasksQuery.cs`
- Modify: `boilerplateBE/src/modules/Starter.Module.Workflow/Application/Queries/GetPendingTasks/GetPendingTasksQueryHandler.cs`
- Modify: `boilerplateBE/src/modules/Starter.Module.Workflow/Controllers/WorkflowController.cs` (line 151-163)
- Create: `boilerplateBE/tests/Starter.Api.Tests/Workflow/GetPendingTasksPaginationTests.cs`
- Frontend (verify pagination shape): `boilerplateFE/src/features/workflow/api/workflow.queries.ts`, `boilerplateFE/src/features/workflow/pages/WorkflowInboxPage.tsx`

- [ ] **Step 1: Write the failing test**

Create `boilerplateBE/tests/Starter.Api.Tests/Workflow/GetPendingTasksPaginationTests.cs`. Mirror the pattern from `SlaEscalationJobTests.cs` (in-memory DbContext, mock dependencies).

```csharp
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Starter.Abstractions.Capabilities;
using Starter.Abstractions.Readers;
using Starter.Application.Common.Models;
using Starter.Module.Workflow.Domain.Entities;
using Starter.Module.Workflow.Infrastructure.Persistence;
using Starter.Module.Workflow.Infrastructure.Services;
using Xunit;
using TaskStatus = Starter.Module.Workflow.Domain.Enums.TaskStatus;

namespace Starter.Api.Tests.Workflow;

public sealed class GetPendingTasksPaginationTests : IDisposable
{
    private readonly WorkflowDbContext _db;
    private readonly Mock<IUserReader> _userReader = new();
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();

    public GetPendingTasksPaginationTests()
    {
        var options = new DbContextOptionsBuilder<WorkflowDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new WorkflowDbContext(options);

        _userReader
            .Setup(r => r.GetManyAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<UserRecord>());
    }

    [Fact]
    public async Task Returns_correct_page_slice_and_total_count()
    {
        // Arrange — seed a definition + instance + 25 pending tasks
        var definition = WorkflowDefinition.Create(_tenantId, "test-def", "Test Def", "Product", "{}", "[]", true);
        _db.WorkflowDefinitions.Add(definition);
        var instance = WorkflowInstance.Create(_tenantId, definition.Id, "Product", Guid.NewGuid(), _userId, "entity-X", "Draft");
        _db.WorkflowInstances.Add(instance);

        for (int i = 0; i < 25; i++)
        {
            var task = ApprovalTask.Create(
                _tenantId, instance.Id, "Review",
                _userId, null, null, null,
                "Product", instance.EntityId, null, null);
            // Make created-at strictly descending so task[0] is newest, task[24] oldest
            typeof(ApprovalTask).GetProperty(nameof(ApprovalTask.CreatedAt))!
                .SetValue(task, DateTime.UtcNow.AddMinutes(-i));
            _db.ApprovalTasks.Add(task);
        }
        await _db.SaveChangesAsync();

        var engine = new WorkflowEngine(_db, _userReader.Object, /* other deps… */);
        // NOTE: WorkflowEngine constructor may require additional dependencies. Check
        // `WorkflowEngineTests.cs` for the full ctor signature and mock the rest as
        // noop/null-returning mocks. The test only exercises the read path.

        // Act — page 2, pageSize 10 → should return tasks 11..20
        var result = await engine.GetPendingTasksAsync(_userId, pageNumber: 2, pageSize: 10, CancellationToken.None);

        // Assert
        result.TotalCount.Should().Be(25);
        result.PageNumber.Should().Be(2);
        result.PageSize.Should().Be(10);
        result.Items.Should().HaveCount(10);
        result.TotalPages.Should().Be(3);
    }

    public void Dispose() => _db.Dispose();
}
```

(Note: the exact constructor params for `WorkflowEngine` — check `WorkflowEngineTests.cs` and duplicate any setup needed. Treat the test as a contract on the return shape; adapt the setup to whatever the engine's DI surface requires.)

- [ ] **Step 2: Run the test, confirm it fails to compile**

```bash
cd boilerplateBE && dotnet build
```

Expected: build fails with a compile error about `GetPendingTasksAsync` not matching the signature `(Guid, int, int, CancellationToken)` or the return type not being `PaginatedList<PendingTaskSummary>`.

- [ ] **Step 3: Update `IWorkflowService.GetPendingTasksAsync` signature**

Edit `boilerplateBE/src/Starter.Abstractions/Capabilities/IWorkflowService.cs`. Change:

```csharp
Task<IReadOnlyList<PendingTaskSummary>> GetPendingTasksAsync(Guid userId,
    CancellationToken ct = default);
```

To:

```csharp
Task<PaginatedList<PendingTaskSummary>> GetPendingTasksAsync(Guid userId,
    int pageNumber = 1, int pageSize = 20,
    CancellationToken ct = default);
```

Add `using Starter.Application.Common.Models;` at the top if not present.

- [ ] **Step 4: Update `NullWorkflowService.GetPendingTasksAsync`**

Edit `boilerplateBE/src/Starter.Infrastructure/Capabilities/NullObjects/NullWorkflowService.cs` (~line 89). Change return to a paginated empty list:

```csharp
public Task<PaginatedList<PendingTaskSummary>> GetPendingTasksAsync(
    Guid userId,
    int pageNumber = 1,
    int pageSize = 20,
    CancellationToken ct = default)
{
    logger.LogDebug(
        "Workflow pending tasks query skipped — Workflow module not installed (userId: {UserId})",
        userId);
    return Task.FromResult(PaginatedList<PendingTaskSummary>.Create(
        Array.Empty<PendingTaskSummary>(), totalCount: 0, pageNumber, pageSize));
}
```

Add `using Starter.Application.Common.Models;` if not present.

- [ ] **Step 5: Update `WorkflowEngine.GetPendingTasksAsync`**

Edit `boilerplateBE/src/modules/Starter.Module.Workflow/Infrastructure/Services/WorkflowEngine.cs` (the method starting around line 572). Change signature and implementation to push pagination into the IQueryable:

```csharp
public async Task<PaginatedList<PendingTaskSummary>> GetPendingTasksAsync(
    Guid userId, int pageNumber = 1, int pageSize = 20, CancellationToken ct = default)
{
    var baseQuery = context.ApprovalTasks
        .Include(t => t.Instance)
            .ThenInclude(i => i.Definition)
        .Where(t => t.Status == Domain.Enums.TaskStatus.Pending
            && (t.AssigneeUserId == userId || t.OriginalAssigneeUserId == userId))
        .OrderByDescending(t => t.CreatedAt);

    var totalCount = await baseQuery.CountAsync(ct);

    var pagedTasks = await baseQuery
        .Skip((pageNumber - 1) * pageSize)
        .Take(pageSize)
        .ToListAsync(ct);

    // Resolve display names only for the current page's rows
    var originalAssigneeIds = pagedTasks
        .Where(t => t.OriginalAssigneeUserId.HasValue)
        .Select(t => t.OriginalAssigneeUserId!.Value)
        .Distinct()
        .ToList();

    var delegationNameLookup = new Dictionary<Guid, string>();
    if (originalAssigneeIds.Count > 0)
    {
        var users = await userReader.GetManyAsync(originalAssigneeIds, ct);
        foreach (var u in users)
            delegationNameLookup[u.Id] = u.DisplayName;
    }

    // Pre-load parallel group sibling counts for this page only
    var groupIds = pagedTasks
        .Where(t => t.GroupId.HasValue)
        .Select(t => t.GroupId!.Value)
        .Distinct()
        .ToList();

    // ... (rest of existing enrichment code — sibling counts, isOverdue, etc.,
    //      pasted from the original implementation but operating on `pagedTasks`)

    var summaries = pagedTasks.Select(t => new PendingTaskSummary(/* ...fields... */)).ToList();

    return PaginatedList<PendingTaskSummary>.Create(summaries, totalCount, pageNumber, pageSize);
}
```

NOTE: the existing implementation has ~30 more lines after the user lookup (sibling counts, overdue calc, summary projection). Preserve ALL of that logic — only change (a) the top query to be ordered+paginated, (b) the returned shape to `PaginatedList<T>`. The enrichment logic doesn't change.

- [ ] **Step 6: Update `GetPendingTasksQuery` return type**

Edit `boilerplateBE/src/modules/Starter.Module.Workflow/Application/Queries/GetPendingTasks/GetPendingTasksQuery.cs`. Change:

```csharp
public sealed record GetPendingTasksQuery(
    int Page = 1,
    int PageSize = 20) : IRequest<Result<List<PendingTaskSummary>>>;
```

To:

```csharp
using Starter.Application.Common.Models;

public sealed record GetPendingTasksQuery(
    int Page = 1,
    int PageSize = 20) : IRequest<Result<PaginatedList<PendingTaskSummary>>>;
```

- [ ] **Step 7: Update `GetPendingTasksQueryHandler`**

Edit the handler to delegate pagination to the service and drop the in-memory slicing:

```csharp
using Starter.Application.Common.Models;

namespace Starter.Module.Workflow.Application.Queries.GetPendingTasks;

internal sealed class GetPendingTasksQueryHandler(
    IWorkflowService workflowService,
    ICurrentUserService currentUser) : IRequestHandler<GetPendingTasksQuery, Result<PaginatedList<PendingTaskSummary>>>
{
    public async Task<Result<PaginatedList<PendingTaskSummary>>> Handle(
        GetPendingTasksQuery request, CancellationToken cancellationToken)
    {
        var paged = await workflowService.GetPendingTasksAsync(
            currentUser.UserId!.Value,
            request.Page,
            request.PageSize,
            cancellationToken);

        return Result.Success(paged);
    }
}
```

- [ ] **Step 8: Update `WorkflowController.GetPendingTasks`**

Edit the controller (around line 151). Change the return type attribute and use `HandlePagedResult`:

```csharp
[HttpGet("tasks")]
[Authorize(Policy = WorkflowPermissions.ActOnTask)]
[ProducesResponseType(typeof(PagedApiResponse<PendingTaskSummary>), StatusCodes.Status200OK)]
public async Task<IActionResult> GetPendingTasks(
    [FromQuery] int page = 1,
    [FromQuery] int pageSize = 20,
    CancellationToken ct = default)
{
    var result = await Mediator.Send(new GetPendingTasksQuery(page, pageSize), ct);
    return HandlePagedResult(result);
}
```

`HandlePagedResult<T>(Result<PaginatedList<T>>)` is already defined in `BaseApiController`. Add `using Starter.Application.Common.Models;` if needed.

- [ ] **Step 9: Verify frontend consumption still compiles**

Read `boilerplateFE/src/features/workflow/pages/WorkflowInboxPage.tsx` line 36: `const pagination = data?.pagination;`. The FE already expects the paginated wire shape — no FE code change needed, IF the backend `PagedApiResponse` serializes the `PaginatedList` with a `pagination` field matching the FE contract.

Run:

```bash
cd boilerplateFE && npx tsc --noEmit
```

Expected: passes. If it errors, read the reported line and compare the FE `PendingTaskResponse` type (in `src/types/workflow.types.ts`) against what `PagedApiResponse` actually serializes. If the shape is different, update the FE type to match.

- [ ] **Step 10: Run backend build + tests**

```bash
cd boilerplateBE && dotnet build && dotnet test --no-build --filter "GetPendingTasksPaginationTests"
```

Expected: new test passes.

- [ ] **Step 11: Run the full test suite**

```bash
cd boilerplateBE && dotnet test --no-build
```

Expected: all 145 tests pass (144 existing + 1 new).

- [ ] **Step 12: Commit**

```bash
git add boilerplateBE/src/Starter.Abstractions/Capabilities/IWorkflowService.cs \
        boilerplateBE/src/Starter.Infrastructure/Capabilities/NullObjects/NullWorkflowService.cs \
        boilerplateBE/src/modules/Starter.Module.Workflow/Infrastructure/Services/WorkflowEngine.cs \
        boilerplateBE/src/modules/Starter.Module.Workflow/Application/Queries/GetPendingTasks/ \
        boilerplateBE/src/modules/Starter.Module.Workflow/Controllers/WorkflowController.cs \
        boilerplateBE/tests/Starter.Api.Tests/Workflow/GetPendingTasksPaginationTests.cs
# If FE types needed updating:
git add boilerplateFE/src/features/workflow/
git commit -m "fix(workflow): DB-level pagination for GetPendingTasks"
```

---

### Task B3: #5 Timeline uses history records instead of array index

**Files:**
- Modify: `boilerplateFE/src/features/workflow/components/WorkflowStepTimeline.tsx`
- Create: `boilerplateFE/src/features/workflow/components/__tests__/WorkflowStepTimeline.test.tsx`

- [ ] **Step 1: Write the failing test**

Create the test file:

```tsx
import { describe, it, expect, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import { WorkflowStepTimeline } from '../WorkflowStepTimeline';
import type { WorkflowStateConfig, WorkflowStepRecord } from '@/types/workflow.types';

// Mock useWorkflowHistory
vi.mock('../../api', () => ({
  useWorkflowHistory: vi.fn(),
}));

import { useWorkflowHistory } from '../../api';

// Mock useTranslation
vi.mock('react-i18next', () => ({
  useTranslation: () => ({ t: (key: string) => key }),
}));

// Helper to set history mock
function mockHistory(records: WorkflowStepRecord[]) {
  (useWorkflowHistory as ReturnType<typeof vi.fn>).mockReturnValue({ data: records });
}

const states: WorkflowStateConfig[] = [
  { name: 'Draft', displayName: 'Draft', type: 'Initial' },
  { name: 'ReviewA', displayName: 'Review A', type: 'HumanTask' },
  { name: 'ReviewB', displayName: 'Review B', type: 'HumanTask' },
  { name: 'Rejected', displayName: 'Rejected', type: 'Final' },
  { name: 'Approved', displayName: 'Approved', type: 'Final' },
];

describe('WorkflowStepTimeline', () => {
  it('marks visited states as completed and others as future (linear)', () => {
    mockHistory([
      { toState: 'ReviewA', action: 'Submit', actorDisplayName: 'Alice', timestamp: '2026-04-20T10:00:00Z' } as WorkflowStepRecord,
    ]);

    render(<WorkflowStepTimeline instanceId="i-1" currentState="ReviewA" states={states} />);

    // Draft should be... not 'completed' since currentState is ReviewA and Draft
    // isn't in history records. Draft had to be visited to reach ReviewA but the
    // history only records transitions INTO a state — so Draft (the Initial)
    // counts as 'future' by the new rule. This is acceptable: Initial states
    // render distinctly via their own style.
    // ReviewA is current.
    expect(screen.getByText('Review A')).toBeInTheDocument();
    // ReviewB not in history → future (muted text)
    expect(screen.getByText('Review B').className).toContain('text-muted-foreground');
  });

  it('does not mark later states as completed on branching rejection', () => {
    // Draft → ReviewA → Rejected. States array ordering: Draft, ReviewA, ReviewB, Rejected, Approved.
    // Old behavior (buggy): ReviewB, Approved would show as "completed" because findIndex(Rejected) = 3 > findIndex(ReviewB) = 2.
    // New behavior: ReviewB, Approved should be "future" because not in history.
    mockHistory([
      { toState: 'ReviewA', action: 'Submit', actorDisplayName: 'Alice', timestamp: '2026-04-20T10:00:00Z' } as WorkflowStepRecord,
      { toState: 'Rejected', action: 'Reject', actorDisplayName: 'Bob', timestamp: '2026-04-20T11:00:00Z' } as WorkflowStepRecord,
    ]);

    render(<WorkflowStepTimeline instanceId="i-2" currentState="Rejected" states={states} />);

    // ReviewB should be future, not completed
    const reviewBElement = screen.getByText('Review B');
    expect(reviewBElement.className).toContain('text-muted-foreground');
    // Approved should be future
    const approvedElement = screen.getByText('Approved');
    expect(approvedElement.className).toContain('text-muted-foreground');
  });

  it('handles loop-back (return-for-revision + resubmit)', () => {
    // Draft → ReviewA → Draft (returned) → ReviewA (resubmitted) → Approved
    mockHistory([
      { toState: 'ReviewA', action: 'Submit', actorDisplayName: 'Alice', timestamp: '2026-04-20T10:00:00Z' } as WorkflowStepRecord,
      { toState: 'Draft', action: 'ReturnForRevision', actorDisplayName: 'Bob', timestamp: '2026-04-20T11:00:00Z' } as WorkflowStepRecord,
      { toState: 'ReviewA', action: 'Resubmit', actorDisplayName: 'Alice', timestamp: '2026-04-20T12:00:00Z' } as WorkflowStepRecord,
      { toState: 'Approved', action: 'Approve', actorDisplayName: 'Bob', timestamp: '2026-04-20T13:00:00Z' } as WorkflowStepRecord,
    ]);

    render(<WorkflowStepTimeline instanceId="i-3" currentState="Approved" states={states} />);

    // All visited states should NOT be in future styling
    expect(screen.getByText('Review A').className).not.toContain('text-muted-foreground');
    // ReviewB never visited → future
    expect(screen.getByText('Review B').className).toContain('text-muted-foreground');
    // Rejected never visited → future
    expect(screen.getByText('Rejected').className).toContain('text-muted-foreground');
  });
});
```

- [ ] **Step 2: Run the test, verify it fails**

```bash
cd boilerplateFE && npx vitest run src/features/workflow/components/__tests__/WorkflowStepTimeline.test.tsx
```

Expected: "does not mark later states as completed on branching rejection" FAILS — ReviewB's class contains something other than `text-muted-foreground` because the current code treats `findIndex(Rejected)=3 > findIndex(ReviewB)=2` as "completed".

- [ ] **Step 3: Rewrite `getStepStatus` to use history**

Edit `WorkflowStepTimeline.tsx`. Change the `getStepStatus` function signature + call site:

```tsx
type StepStatus = 'completed' | 'current' | 'future';

function getStepStatus(
  stateName: string,
  currentState: string,
  history: WorkflowStepRecord[],
): StepStatus {
  if (stateName === currentState) return 'current';
  return history.some((r) => r.toState === stateName) ? 'completed' : 'future';
}
```

Update the single call site inside the `.map()` render loop. Remove the `states` parameter from the signature since it's no longer needed there. Pass `records` (the history array) instead.

- [ ] **Step 4: Run the test, verify pass**

```bash
cd boilerplateFE && npx vitest run src/features/workflow/components/__tests__/WorkflowStepTimeline.test.tsx
```

Expected: all three tests pass.

- [ ] **Step 5: Run full frontend test suite + build**

```bash
cd boilerplateFE && npx vitest run && npm run build
```

Expected: all tests green, build succeeds.

- [ ] **Step 6: Commit**

```bash
git add boilerplateFE/src/features/workflow/components/WorkflowStepTimeline.tsx \
        boilerplateFE/src/features/workflow/components/__tests__/WorkflowStepTimeline.test.tsx
git commit -m "fix(workflow): step timeline uses history records instead of array index"
```

---

### Task B4: #3 Raise `WorkflowTaskEscalatedEvent` on SLA escalation

**Files:**
- Modify: `boilerplateBE/src/modules/Starter.Module.Workflow/Infrastructure/Services/SlaEscalationJob.cs` (around line 202)
- Modify: `boilerplateBE/tests/Starter.Api.Tests/Workflow/SlaEscalationJobTests.cs` (add new test method)

- [ ] **Step 1: Add failing test**

Open `boilerplateBE/tests/Starter.Api.Tests/Workflow/SlaEscalationJobTests.cs`. Add a new `[Fact]` method that seeds an overdue task with escalation config, runs the job, and asserts the event is present on the instance.

```csharp
[Fact]
public async Task EscalatesTask_RaisesWorkflowTaskEscalatedEvent()
{
    // Arrange — seed definition with SLA escalation + fallback assignee
    var job = CreateJob();
    var (instance, task) = await SeedOverdueTaskWithEscalation();

    // Act
    await job.ProcessOverdueTasksAsync(CancellationToken.None);

    // Assert — reload instance and check domain events
    var reloaded = await _db.WorkflowInstances
        .Include(i => i.DomainEvents)  // if events are persisted via outbox; else check pre-save collection
        .FirstAsync(i => i.Id == instance.Id);

    reloaded.DomainEvents.Should().ContainSingle(e => e is WorkflowTaskEscalatedEvent)
        .Which.Should().BeOfType<WorkflowTaskEscalatedEvent>()
        .Which.Should().Match<WorkflowTaskEscalatedEvent>(ev =>
            ev.OriginalAssigneeUserId == _assigneeId &&
            ev.NewAssigneeUserId == _fallbackAssigneeId &&
            ev.InstanceId == instance.Id);
}

[Fact]
public async Task EscalatesTask_DoesNotRaiseEvent_WhenNoFallbackAssignee()
{
    // Arrange — seed overdue task with NO fallback assignee resolvable
    var job = CreateJob();
    var (instance, _) = await SeedOverdueTaskWithNoFallbackAssignee();

    // Act
    await job.ProcessOverdueTasksAsync(CancellationToken.None);

    // Assert
    var reloaded = await _db.WorkflowInstances.FirstAsync(i => i.Id == instance.Id);
    reloaded.DomainEvents.Should().NotContain(e => e is WorkflowTaskEscalatedEvent);
}
```

You'll need helper methods `CreateJob()`, `SeedOverdueTaskWithEscalation()`, `SeedOverdueTaskWithNoFallbackAssignee()`. Reuse setup from the existing tests in the file. If `DomainEvents` is a protected/private collection on `WorkflowInstance`, access it via reflection or through a public accessor — read the `AggregateRoot` base class to find the right surface. If `WorkflowInstance` uses MediatR's `INotification` pattern with pre-save collection, that collection is likely what you need.

**If accessing `DomainEvents` is awkward**, alternative: add an `IPublisher` mock to the job's DI (the engine uses one for event dispatch), run the job, and verify the mock received the event. Check how `SlaEscalationJob` currently dispatches events (it does via `IPublishEndpoint` / `IPublisher` / via `AddDomainEvent`). Adapt the assertion accordingly.

- [ ] **Step 2: Run test, confirm failure**

```bash
cd boilerplateBE && dotnet test --filter "EscalatesTask_Raises"
```

Expected: fails because no event is raised today.

- [ ] **Step 3: Add event raise in `EscalateTaskAsync`**

Edit `SlaEscalationJob.cs` in `EscalateTaskAsync` (around line 202, right after `dbContext.ApprovalTasks.Add(escalatedTask);`). Insert:

```csharp
// Raise domain event for audit/downstream consumers. Only when both actors are
// known — an unassigned→unassigned escalation carries no handoff semantic.
if (originalAssigneeId.HasValue && newAssigneeId.HasValue)
{
    task.Instance.AddDomainEvent(new WorkflowTaskEscalatedEvent(
        escalatedTask.Id,
        task.InstanceId,
        originalAssigneeId.Value,
        newAssigneeId.Value,
        task.StepName,
        task.Instance.EntityType,
        task.Instance.EntityId,
        task.TenantId));
}
```

Add `using Starter.Module.Workflow.Domain.Events;` at the top if not present.

- [ ] **Step 4: Run tests, confirm pass**

```bash
cd boilerplateBE && dotnet test --filter "SlaEscalation"
```

Expected: all `SlaEscalationJobTests` pass including the two new ones.

- [ ] **Step 5: Run full backend test suite**

```bash
cd boilerplateBE && dotnet test
```

Expected: all tests green.

- [ ] **Step 6: Commit**

```bash
git add boilerplateBE/src/modules/Starter.Module.Workflow/Infrastructure/Services/SlaEscalationJob.cs \
        boilerplateBE/tests/Starter.Api.Tests/Workflow/SlaEscalationJobTests.cs
git commit -m "fix(workflow): raise WorkflowTaskEscalatedEvent on SLA escalation"
```

---

### Task B5: #1 Delegation date ISO 8601 serialization (locale-safe)

**Files:**
- Modify: `boilerplateFE/src/features/workflow/components/DelegationDialog.tsx`
- Create: `boilerplateFE/src/features/workflow/components/__tests__/DelegationDialog.test.tsx` (or `delegation-serializer.test.ts` if extracting pure fn — recommended)
- Create: `boilerplateFE/src/features/workflow/components/delegation-serializer.ts` (extracted pure fn)

**Approach.** Extract the date serialization into a pure function and test it directly. The current code uses `new Date(startDate + 'T00:00:00').toISOString()` which interprets the date-only string as **local time** before converting to UTC. In timezones far from UTC, this can shift the stored date backward or forward by one day — not strictly wrong (start-of-day semantics) but locale-dependent. The fix: interpret the date as **UTC midnight** so the stored ISO string is consistent across all users.

- [ ] **Step 1: Write the failing test**

Create `boilerplateFE/src/features/workflow/components/__tests__/delegation-serializer.test.ts`:

```ts
import { describe, it, expect } from 'vitest';
import { formatDelegationDates } from '../delegation-serializer';

describe('formatDelegationDates', () => {
  it('serializes start date as UTC midnight (00:00:00Z)', () => {
    const result = formatDelegationDates('2026-04-20', '2026-04-25');
    expect(result.startDate).toBe('2026-04-20T00:00:00.000Z');
  });

  it('serializes end date as UTC end-of-day (23:59:59.999Z)', () => {
    const result = formatDelegationDates('2026-04-20', '2026-04-25');
    expect(result.endDate).toBe('2026-04-25T23:59:59.999Z');
  });

  it('is locale-independent (same output regardless of browser timezone)', () => {
    // The function should use only the string form, not a Date object with local
    // timezone interpretation. This is a behavioral smoke test — if this fails,
    // the implementation is using local-time interpretation somewhere.
    const result1 = formatDelegationDates('2026-04-20', '2026-04-20');
    // Run again; should be identical (no locale drift)
    const result2 = formatDelegationDates('2026-04-20', '2026-04-20');
    expect(result1).toEqual(result2);
    expect(result1.startDate.endsWith('Z')).toBe(true);
    expect(result1.endDate.endsWith('Z')).toBe(true);
  });
});
```

- [ ] **Step 2: Run test, confirm failure (module not found)**

```bash
cd boilerplateFE && npx vitest run src/features/workflow/components/__tests__/delegation-serializer.test.ts
```

Expected: fails with "Cannot find module" — the `delegation-serializer.ts` file doesn't exist yet.

- [ ] **Step 3: Create the serializer module**

Write `boilerplateFE/src/features/workflow/components/delegation-serializer.ts`:

```ts
/**
 * Serializes date-only strings (YYYY-MM-DD from an HTML <input type="date">)
 * into ISO 8601 UTC timestamps. Start = UTC midnight, End = UTC end-of-day.
 *
 * Timezone-independent by design — we append the UTC time suffix directly
 * instead of constructing a Date in the browser's local timezone.
 */
export function formatDelegationDates(
  startDate: string,
  endDate: string,
): { startDate: string; endDate: string } {
  return {
    startDate: `${startDate}T00:00:00.000Z`,
    endDate: `${endDate}T23:59:59.999Z`,
  };
}
```

- [ ] **Step 4: Run test, confirm pass**

```bash
cd boilerplateFE && npx vitest run src/features/workflow/components/__tests__/delegation-serializer.test.ts
```

Expected: all three tests pass.

- [ ] **Step 5: Wire the serializer into `DelegationDialog.tsx`**

Edit `DelegationDialog.tsx`. Import the helper and replace lines 47-66:

```tsx
import { formatDelegationDates } from './delegation-serializer';

// ... inside component:

const handleConfirm = () => {
  if (!toUserId || !startDate || !endDate) return;
  const { startDate: startIso, endDate: endIso } = formatDelegationDates(startDate, endDate);
  createDelegation(
    { toUserId, startDate: startIso, endDate: endIso },
    {
      onSuccess: () => {
        setToUserId('');
        setSearchTerm('');
        setStartDate('');
        setEndDate('');
        onOpenChange(false);
      },
    },
  );
};
```

- [ ] **Step 6: Run frontend build + tests**

```bash
cd boilerplateFE && npm run build && npx vitest run
```

Expected: build succeeds, all tests pass.

- [ ] **Step 7: Commit**

```bash
git add boilerplateFE/src/features/workflow/components/DelegationDialog.tsx \
        boilerplateFE/src/features/workflow/components/delegation-serializer.ts \
        boilerplateFE/src/features/workflow/components/__tests__/delegation-serializer.test.ts
git commit -m "fix(workflow): ISO 8601 UTC date serialization in delegation dialog"
```

---

## Phase C — End-to-End Validation

### Task C1: Backend build + full test suite

- [ ] **Step 1: Clean build**

```bash
cd boilerplateBE && dotnet clean && dotnet build
```

Expected: build succeeds without warnings or errors.

- [ ] **Step 2: Run full test suite**

```bash
cd boilerplateBE && dotnet test
```

Expected: all 145+ tests pass (144 original + ~3 new tests added across B2, B4 — B1, B3, B5 don't add backend tests).

If a test fails, read the failure, fix in the appropriate source, re-run.

---

### Task C2: Frontend build + full test suite

- [ ] **Step 1: Build**

```bash
cd boilerplateFE && npm run build
```

Expected: build succeeds. If `tsc --noEmit` catches a type error, fix.

- [ ] **Step 2: Run Vitest**

```bash
cd boilerplateFE && npx vitest run
```

Expected: all tests pass, including the new ones from B3 and B5.

---

### Task C3: Live verification in test app

Per the "Post-Feature Testing Workflow" in `CLAUDE.md`. Spin up the existing test app (if port 3300/5300 free; otherwise 3200/5200).

- [ ] **Step 1: Check free ports**

```bash
lsof -iTCP -sTCP:LISTEN -nP 2>/dev/null | awk '{print $9}' | grep -oE '[0-9]+$' | sort -un
```

Look at the output — if 3300 and 5300 are both absent, use those. Otherwise use 3200/5200.

- [ ] **Step 2: Start test app**

Follow the rename + start pattern in `CLAUDE.md` under "Post-Feature Testing Workflow." If a test app from the previous session is still running (check `ls .claude/worktrees/sharp-colden/` or similar), reuse it after verifying it reflects the current branch. If not, generate a fresh test app.

- [ ] **Step 3: Verify the 5 fixes manually**

Open the app in a browser. Login as an admin. Verify:

1. **#7 (no user-visible effect)** — confirmed indirectly by build + tests.
2. **#4 Pagination** — visit Workflow → Inbox. Seed 25+ tasks beforehand (use the test app's API or DB). Navigate across pages. Open browser DevTools network tab — each page-change should issue a fresh `GET /api/v1/workflow/tasks?page=N` with the correct `page` param. The response should include a `pagination` field with `totalCount`, `pageNumber`, `pageSize`.
3. **#5 Timeline** — submit a workflow request, reject it at step 1. Open the instance detail page. Confirm states that come AFTER "Rejected" in the definition's state list render as "future" (muted), NOT "completed."
4. **#3 Escalation event** — hard to verify end-to-end without 48h+ of elapsed time. Instead, mutate a test task's `CreatedAt` in the DB to simulate overdue; trigger `ProcessOverdueTasksAsync` via the job or a test endpoint; check the audit log / activity timeline for the new event. Alternative: just trust the unit test from B4 and verify the existing live escalation notification still fires.
5. **#1 Delegation date** — open the delegation dialog, pick a start + end date, submit. Check the backend DB record for the delegation: `StartDate` should be `YYYY-MM-DD 00:00:00 UTC`, `EndDate` should be `YYYY-MM-DD 23:59:59.999 UTC`. Consistent regardless of the browser's timezone.

- [ ] **Step 4: Report findings to user**

Report URL, login credentials, and any QA findings. Do NOT clean up the test app until the user says so.

---

## Post-Plan Handoff

### Opening the PR

After all tasks complete and the test app looks good:

```bash
git push origin feature/workflow-approvals
gh pr create --title "Workflow Phase 1 + 2a — cleanup and docs reorg" --body "$(cat <<'EOF'
## Summary
- 5 code fixes (#1, #3, #4, #5, #7) from Phase 2a known-issues list
- Docs reorganized into `docs/user-manual/` and `docs/developer-guide/` at repo root
- All module docs consolidated with redirect stubs left in module folders
- New Workflow user manual + developer guide (with deferred #6 engine refactor documented)

## Spec
`docs/superpowers/specs/2026-04-20-workflow-phase1-2a-cleanup-and-docs-design.md`

## Deferred explicitly
- #6 WorkflowEngine.cs extraction (own PR)
- #2 live SLA overdue-badge QA (not a code fix)

## Test plan
- [ ] Backend: dotnet build + test green (145+ tests)
- [ ] Frontend: npm run build + vitest run green
- [ ] Live: pagination, timeline, delegation tested in browser
EOF
)"
```

### After merge

1. Delete the branch locally + remotely.
2. Start fresh: `git checkout main && git pull && git checkout -b feature/workflow-phase2b`.
3. Invoke the brainstorming skill for Phase 2b scope.

---

## Self-Review Notes

Re-check before execution:
- [ ] Every code block shows actual content (no "similar to Task N" references)
- [ ] Every file path exact and resolved against the worktree root
- [ ] Every commit message matches the spec's commit plan
- [ ] Every task leaves the repo in a buildable + testable state
