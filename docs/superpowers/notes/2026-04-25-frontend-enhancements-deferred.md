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
