# Workflow & Approvals Module

The Workflow module is a composable state-machine engine that turns any business process into a configurable, multi-tenant workflow. Other modules register their entities as "workflowable"; tenant admins author workflows visually without code; the engine handles multi-step approvals, conditional routing, SLA tracking, and side effects.

## Vision

**One composable workflow engine that any module can use to define multi-step processes** with human approvals, automated actions, and configurable routing. When the Workflow module is absent, consuming modules fall back to simple status fields via `NullWorkflowService`.

Key properties:
- **Tenant-authorable** — admins clone templates and customize via the visual designer; no developer involvement.
- **Multi-step with conditional routing** — branch on submitted form data, entity context, or AI classification.
- **Human + system actions** — approval tasks assigned to users/roles/hierarchies, plus automated processing steps.
- **SLA-aware** — reminders, escalations, reassignments when tasks age.
- **Integrated** — comments, activity timeline, email, webhooks, AI agents all pluggable.

## What's shipped

### Phase 1 — Foundation (✅ merged 2026-04-22)
- **State-machine engine** with Initial / HumanTask / SystemAction / Terminal states
- **Approval tasks** and multi-tenant inbox
- **Template seeding** — modules define workflow templates; admins clone them
- **Entity integration** — any module registers an entity as workflowable
- **Comments & activity** wired to workflow instances

See [Phase 1 Design](../../superpowers/specs/2026-04-19-workflow-approvals-design.md).

### Phase 2a — Engine power (✅ merged 2026-04-22)
- **SLA tracking & auto-escalation** — reminders, reassign, auto-approve/reject when overdue
- **Delegation** — users delegate to colleagues during leave; tasks route to the delegate
- **Parallel approvals** — multiple assignees per step with quorum (AllOf / AnyOf / Threshold)

See [Phase 2a Design](../../superpowers/specs/2026-04-20-workflow-phase2a-engine-power-design.md).

### Phase 2b — Operational hardening (✅ merged 2026-04-21)
- **Transactional outbox** on WorkflowDbContext — event consistency at scale
- **Denormalized inbox** — task display skips definition JOINs; snapshots definition/entity names and form fields on task creation

See [Phase 2b Design](../../superpowers/specs/2026-04-21-workflow-phase2b-operational-hardening-design.md).

### Phase 3 — Foundation & quick wins (✅ merged 2026-04-22)
- **WorkflowEngine extraction** — split 1400-line engine into `HumanTaskFactory`, `AutoTransitionEvaluator`, `ParallelApprovalCoordinator`
- **Compound conditions** — AND/OR/NOT operators with nested groups
- **Bulk operations** — select multiple tasks, execute same action (Approve/Reject/Return) in one round-trip

See [Phase 3 Design](../../superpowers/specs/2026-04-22-workflow-phase3-plus-roadmap-design.md).

### Phase 4a — Step data collection (✅ merged 2026-04-22)
- **Dynamic forms** — states declare `FormFields`; submitted values merge into `WorkflowInstance.ContextJson`
- **Branching on form data** — conditions evaluate submitted values and route accordingly
- Supported types: `text`, `textarea`, `number`, `date`, `select`, `checkbox`

See [Workflow Forms](features/forms.md) and [Phase 4a Design](../../superpowers/specs/2026-04-22-workflow-phase4a-dynamic-forms-finish-design.md).

### Phase 4b — Analytics (✅ merged 2026-04-22)
- **Per-definition metrics** — cycle time, bottleneck states (median dwell time), approval rates, instance count series, stuck instances, approver activity
- **`GET /api/v1/workflows/definitions/{id}/analytics?window={7d|30d|90d|all}`** — returns aggregated metrics
- **Analytics tab** on definition detail page with charts and tables

See [Workflow Analytics](features/analytics.md) and [Phase 4b Design](../../superpowers/specs/2026-04-22-workflow-phase4b-analytics-design.md).

### Phase 4c — Visual designer (✅ merged 2026-04-23)
- **Drag-and-drop state-machine builder** at `/workflows/definitions/:id/designer`
- **Visual editors** for states (type, assignee, actions, SLA) and transitions (trigger, type)
- **JSON blocks** for advanced fields (hooks, form fields, parallel/quorum, compound conditions, fallback assignee, custom params)
- **Read-only for templates** with "Clone to edit" button
- **Auto-layout** via dagre on first open
- **Position persistence** via optional `UiPosition` on each state

See [Workflow Designer](features/designer.md) and [Phase 4c Design](../../superpowers/specs/2026-04-23-workflow-phase4c-visual-designer-design.md).

## Core concepts

### Workflow instance
A single submitted workflow — e.g., "Expense report #42" or "Leave request from Alice." Created by calling `IWorkflowService.StartAsync(entityType, entityId, definitionName, initiatorUserId)`. Tracks:
- Current state
- Context (initial submission + form data merged from each step)
- History (all transitions completed)
- Status (active / completed / cancelled)

### Approval task
A step in a workflow waiting for someone to act. Lives in the user's inbox. One task per assignee per step; multiple tasks per step if parallel approvals configured. Assignees execute via:
- **Approve** — advance to next state
- **Reject** — end workflow in rejected state
- **Return for revision** — send back to initiator; they can resubmit

### Definition
The template that shapes the workflow. Seeded by modules or authored by tenant admins via the visual designer. Contains:
- **States** — Initial / HumanTask / SystemAction / Terminal
- **Transitions** — routing rules (trigger, conditions, auto-advance)
- **Assignee strategies** — role, user, initiator, initiator's manager, custom
- **SLA config** — reminder & escalation hours
- **Form fields** — structured data collection
- **Hooks** — side effects (email, webhook, custom)
- **Parallel/quorum config** — multiple assignees per step

### Condition
Branches workflows on context. Evaluates against `WorkflowInstance.ContextJson`:
- **Simple** — `{ field: "amount", op: "gte", value: 5000 }`
- **Compound** — `{ operator: "And", conditions: [...] }` with AND/OR/NOT nesting

### SLA & escalation
Per-state time budget. When a task exceeds `reminderAfterHours`, a reminder is sent. When it exceeds `escalateAfterHours`, the engine escalates (notify / reassign / auto-approve / auto-reject).

## Key docs

- **[Workflow Engine](features/engine.md)** — detailed architecture, entity relationships, assignee resolution, conditions, SLA, hooks
- **[Workflow Forms](features/forms.md)** — dynamic form fields, validation, conditional branching, authoring
- **[Workflow Designer](features/designer.md)** — visual authoring, templates, validation, auto-layout
- **[Workflow Analytics](features/analytics.md)** — definition metrics, dashboard, data model
- **[Developer Guide](developer-guide.md)** — integration patterns, extending the engine, testing
- **[User Manual](user-guide.md)** — end-user guide to tasks, requests, delegation, notifications
- **[Roadmap](roadmap.md)** — what's left (Phase 4d+), deferred items, design notes

## Next phases (deferred)

See [Roadmap → Phase 4+ Deferred](roadmap.md#phase-4-deferred-items) for:
- **Phase 4d** — Designer Option A (full visual parity, removing JSON blocks)
- **Phase 5** — Inbound webhook triggers + entity-level comment ACL
- **Phase 6** — AI-powered workflows (unblocked by Plan 5a merge)

## Quick start

### For end users
→ [User Manual](user-guide.md)

### For developers integrating a module
1. Register your entity: `services.AddWorkflowableEntity("MyEntity", ...)`
2. Seed a template: `IWorkflowService.SeedTemplateAsync(...)`
3. Admins clone + customize via the designer

### For developers extending the engine
→ [Developer Guide](developer-guide.md)

### For tenant admins authoring workflows
→ [Workflow Designer](features/designer.md)
