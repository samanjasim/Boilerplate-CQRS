# Frontend Enhancements — Deferred Items

**Source spec:** [2026-04-25-frontend-enhancements-design.md](../specs/2026-04-25-frontend-enhancements-design.md)
**Status:** Captured but **not** implemented in `chore/frontend-enhancements`. Each item below is a candidate for its own brainstorm → spec → plan → branch.

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

### 2. Comments-activity timeline on Tenant + User detail
Slot system already supports it ([entity-detail-timeline](../../boilerplateFE/src/lib/extensions/slot-map.ts)). Currently rendered on `ProductDetailPage` and `WorkflowInstanceDetailPage`. Extending to `TenantDetailPage` and `UserDetailPage` is mostly:
- Add `<Slot id="entity-detail-timeline" props={{ entityType: 'Tenant', entityId, tenantId }} />` in tenant detail
- Add same for User detail with `entityType: 'User'`
- Verify BE permissions allow viewing across these entity types
- Decide UX: dedicated tab vs always-visible side panel

**Why deferred:** Low effort, but comments scope on tenant/user pages needs UX decision (which events emit a timeline entry there?).

### 3. Plan CRUD admin page (billing)
BE has `CreatePlan`, `UpdatePlan`, `DeactivatePlan`. FE only browses plans. Platform admins need a `/admin/plans` page.

### 4. Multi-file + drag-drop upload (FilesPage)
BE supports both. UI shows single-file selector. Add drag-drop dropzone, queue display, per-file progress.

### 5. Onboarding deepening
Wizard currently does logo + description + invites. Could add:
- Communication channel setup (SMTP / Slack / Discord)
- Default role chooser
- Sample data import option (seed demo workflows / products)
- Billing plan picker (skip if no billing module enabled)

**Why deferred:** Each adds material complexity. Today's wizard does the 80%-case well.

### 6. Platform-admin first-time setup
Brand-new deployment: SuperAdmin currently sees a finished dashboard. Could include feature flag tour, default role config, branding, pricing setup.

### 7. OnboardingWizard → routed page
Currently `fixed inset-0` overlay. Routed `/onboarding` page would allow browser back, deep links, easier testing.

---

## Polish / low-ROI right now

### 8. Row memoization on heavy list pages
`FilesPage` (847L), `TenantDetailPage` (786L), `WorkflowInboxPage`. No jank reported — defer until measured.

### 9. Tighten `src/components/ui/index.ts` barrel exports
Cosmetic. Current barrel doesn't undermine tree-shaking measurably.

### 10. Route-level ErrorBoundary
App-level boundary handles all errors today. Feature-specific fallbacks are nice-to-have.

### 11. Real-time push (Ably) for more features
Workflow tasks, webhook deliveries, report job completion currently poll or notify via toast. Push would be cleaner.

### 12. React-context replacement of `useOnboardingCheck`
Only needed if more triggers emerge (multi-tenant onboarding, onboarding per role, etc).

---

## Audit log enhancements

### 13. Date range picker for filters
Current filter is basic. Add Radix-popover-based date range.

### 14. Saved filter presets
Per-user saved filters for common audit queries.

---

## Webhook enhancements

### 15. Replay-failed-delivery action
BE supports re-delivery; UI doesn't surface it.

### 16. Per-event subscription management
Event types are hardcoded in `CreateWebhookDialog`. Should derive from BE event registry.

---

## Files enhancements

### 17. Folder/tag organization
Currently flat list.

### 18. Bulk delete
No multi-select on FilesPage (parallel to bulk users in scope).

---

## Process notes

- Each deferred item should pass through brainstorm → spec → plan when prioritized.
- Items 1–6 are sized as their own branches.
- Items 8–18 can be bundled into a follow-up "frontend-enhancements-2" sweep if accumulated.

---

## Audit corrections (recorded after implementation pass)

The original four-dimensional audit overstated several gaps. Reality check from this branch's implementation:

**Already implemented — audit was wrong:**
- D.1 Feature flag CRUD — `EditFeatureFlagDialog` and delete are already wired in `FeatureFlagsList.tsx`.
- D.3 Tenant branding / business info / custom text / default role — `TenantDetailPage` already has tabs for all four with full mutations wired.
- D.4 Audit log row detail — already has expandable rows showing JSON diff (arguably better UX than a side drawer).
- D.5 Notification preferences write — `NotificationPreferences.tsx` already calls `useUpdateNotificationPreferences`.
- D.6 Emergency API key revoke — `EmergencyRevokeDialog` already exists, gated by `ApiKeys.EmergencyRevoke`.
- D.7 Webhook admin in sidebar — already linked at `Sidebar.tsx:123`.
- B detail-page back navigation — `WebhookAdminDetailPage`, `WorkflowDefinitionDetailPage`, `WorkflowInstanceDetailPage` all already use `useBackNavigation`.

**Genuinely missing — deferred for capacity:**

### 19. Bulk user actions (UsersListPage)
Real gap. No `BulkUpdateUserStatusCommand` on the BE; no checkbox/multi-select toolbar on the FE. Sized as ~1 day BE + 1 day FE because:
- BE: command + handler + validator + permission scoping + tenant isolation + tests
- FE: row checkboxes + selection state + sticky action bar + confirm dialog with count + progress feedback for partial-failure scenarios
- i18n in 3 languages

Defer until requested by a tenant with >20 users hitting the one-by-one UX wall.

### 20. Enable `noUncheckedIndexedAccess` in tsconfig
Attempted in this branch — surfaces ~18 errors across 11 files. Each fix is safe but careful (regex `match[1]`, `arr.split(...)[0]`, `MAP[key]` patterns). Worth doing as a focused half-day pass with proper verification at each site rather than rushing `!` non-null assertions that mask real bugs. Files needing fixes:
- `src/components/common/NotificationBell.tsx:23` — `NOTIFICATION_ICONS[type]` lookup
- `src/features/comments-activity/components/EntityTimeline.tsx:113-115` — `next` array access
- `src/features/communication/pages/TemplatesPage.tsx:41` — possibly-undefined object
- `src/features/notifications/pages/NotificationsPage.tsx:96` — `NOTIFICATION_ICONS` lookup
- `src/features/onboarding/components/OnboardingWizard.tsx:115` — invite element shape
- `src/features/settings/pages/SettingsPage.tsx:26,32,198,234,266` — multiple
- `src/features/webhooks/components/EventSelector.tsx:14` — possibly-undefined
- `src/hooks/useTenantBranding.ts:8-10` — three string lookups
- `src/hooks/useThemePreset.ts:22,24` — string lookups
- `src/hooks/useTimeAgo.ts:14` — `lng.split('-')[0]`
- `src/utils/storage.ts:12` — regex `match[1]`
