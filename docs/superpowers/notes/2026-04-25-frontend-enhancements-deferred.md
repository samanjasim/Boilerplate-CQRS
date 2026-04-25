# Frontend Enhancements — Deferred Items

**Source spec:** [2026-04-25-frontend-enhancements-design.md](../specs/2026-04-25-frontend-enhancements-design.md)
**Status:** Tracking the rolling list of items deferred from the `chore/frontend-enhancements*` branches. Each item is a candidate for its own brainstorm → spec → plan → branch.

**Branch history:**
- `chore/frontend-enhancements` (PR #23, merged) — list-page primitives, UX polish, onboarding, a11y, perf
- `chore/frontend-enhancements-2` (current) — `noUncheckedIndexedAccess`, audit-log date filters, webhook delivery replay, route-level ErrorBoundary, Ably push for workflow tasks

---

## Done in branch 2 (was deferred — now shipped)

- ✅ #10 Route-level ErrorBoundary — `RouteErrorBoundary` keyed on pathname wraps `<Outlet />` in `MainLayout`; layout stays intact on feature crashes.
- ✅ #11 Real-time push (workflow tasks) — `useAblyNotifications` now invalidates `workflow.tasks.all` on `WorkflowTaskAssigned`. Refactored the type→keys dispatch into a map so further wiring is one line.
- ✅ #13 Date range picker — shared `DateRangePicker` (Popover + native date inputs, zero deps). Wired into audit logs; BE already accepted `DateFrom`/`DateTo`.
- ✅ #15 Replay-failed-delivery — new `RedeliverWebhookCommand` + `POST /Webhooks/deliveries/{id}/redeliver`; retry button on failed delivery rows in `DeliveryLogModal` (gated by `Webhooks.Update`).
- ✅ #20 `noUncheckedIndexedAccess` — flag enabled; all 22 unsafe sites fixed across 11 files (no `!` assertions, only guards or stricter types).

---

## Strategic features (own plan each)

### 1. AI module frontend
The BE has 9 controllers / 20+ endpoints (assistants, chat, documents, personas, eval, search, agent templates, tools). Zero FE pages today. Likely needs:
- Assistants list + detail + create/edit
- Chat UI (with persona switcher, document attachments, streaming)
- Documents library (ingest, reprocess, delete)
- Personas CRUD
- Agent templates browser (install templates from §Plan 5c-2)
- Search UI for semantic search

**Why deferred:** AI roadmap (Plan 5c) is being driven separately on the BE. Build FE alongside Plan 5c-3 or as Plan 5d.

### 2. Comments-activity timeline on Tenant + User detail (skipped by user)
User explicitly opted out — was not deemed valuable for those entities. Slot system supports it; the work would be ~30 min if reconsidered.

### 3. Plan CRUD admin page (billing)
BE has `CreatePlan`, `UpdatePlan`, `DeactivatePlan`. FE only browses plans. Platform admins need a `/admin/plans` page with edit dialog and deactivate confirmation. Sized as ~half-day FE.

### 4. Multi-file + drag-drop upload (FilesPage)
BE supports both. UI shows single-file selector and only the existing drag-drop slot processes one file. Add file queue state, batch upload, per-file progress. ~half-day.

### 5. Onboarding deepening
Wizard currently does logo + description + invites. Could add:
- Communication channel setup (SMTP / Slack / Discord)
- Default role chooser
- Sample data import option (seed demo workflows / products)
- Billing plan picker (skip if no billing module enabled)

**Why deferred:** Each adds material complexity. Today's wizard does the 80%-case well.

### 6. Platform-admin first-time setup
Brand-new deployment: SuperAdmin currently sees a finished dashboard. Could include feature flag tour, default role config, branding, pricing setup. Each tab already exists in `TenantDetailPage` — wiring into a guided flow is the work.

### 7. OnboardingWizard → routed page
Currently `fixed inset-0` overlay. Routed `/onboarding` page would allow browser back, deep links, easier testing. Cosmetic — current UX works.

---

## Polish / low-ROI right now

### 8. Row memoization on heavy list pages
`FilesPage` (852L), `TenantDetailPage` (786L), `WorkflowInboxPage` (352L). No measured jank — defer until real perf signal.

### 9. Tighten `src/components/ui/index.ts` barrel exports
Already tight (16 explicit components, no wildcard re-exports). Nothing to do.

### 11b. Real-time push for webhook deliveries
**Re-evaluated:** webhook deliveries don't poll, and there's no BE notification type for delivery completion. Adding push would mean creating new BE infra (channel or notification type) for low UX value — users open the deliveries modal once after an event, not continuously. Skip unless tenants report needing live tail of delivery activity.

### 12. React-context replacement of `useOnboardingCheck`
Single consumer (`MainLayout`); premature abstraction. Convert only if multi-tenant or per-role onboarding emerges.

---

## Audit log enhancements

### 14. Saved filter presets
Per-user saved filters for common audit queries. UI cost is meaningful (preset CRUD modal, list, default selection); BE needs save/list/delete. Not a poll-replacement so no urgency.

---

## Webhook enhancements

### 16. Per-event subscription management
**Already done.** `EventSelector` fetches from `useWebhookEventTypes()`; not hardcoded. Audit was wrong.

---

## Files enhancements

### 17. Folder/tag organization
Currently flat list with tag strings. Folder hierarchy needs BE schema + tree nav UI + permission model. Sized as ~2 days.

### 18. Bulk delete (files)
No multi-select on FilesPage. Same shape as #19.

---

## User management

### 19. Bulk user actions (UsersListPage)
Real gap. No `BulkUpdateUserStatusCommand` on BE; no checkbox/multi-select on FE. Sized as ~1 day BE + 1 day FE because:
- BE: command + handler + validator + permission scoping + tenant isolation + tests
- FE: row checkboxes + selection state + sticky action bar + confirm dialog with count + progress feedback for partial-failure scenarios
- i18n in 3 languages

Defer until requested by a tenant with >20 users hitting the one-by-one UX wall.

---

## Process notes

- Each deferred item should pass through brainstorm → spec → plan when prioritized.
- Items 1, 3, 5–6 are sized as their own branches.
- Items 8, 9, 11b, 12, 14, 17, 18 can be bundled into a follow-up sweep if accumulated.
- Item 19 is meaningful enough for its own ~2-day branch.

---

## Audit corrections (kept for reference)

The original four-dimensional audit overstated several gaps:

**Already implemented at audit time — audit was wrong:**
- D.1 Feature flag CRUD — `EditFeatureFlagDialog` and delete already wired in `FeatureFlagsList.tsx`.
- D.3 Tenant branding / business info / custom text / default role — `TenantDetailPage` already has tabs for all four.
- D.4 Audit log row detail — already has expandable rows showing JSON diff.
- D.5 Notification preferences write — `NotificationPreferences.tsx` already calls `useUpdateNotificationPreferences`.
- D.6 Emergency API key revoke — `EmergencyRevokeDialog` exists, gated by `ApiKeys.EmergencyRevoke`.
- D.7 Webhook admin in sidebar — already linked at `Sidebar.tsx:123`.
- B detail-page back navigation — `WebhookAdminDetailPage`, `WorkflowDefinitionDetailPage`, `WorkflowInstanceDetailPage` all use `useBackNavigation`.
- #16 webhook event types come from BE, not hardcoded.
