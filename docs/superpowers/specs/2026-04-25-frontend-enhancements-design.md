# Frontend Enhancements — Design Spec

**Date:** 2026-04-25
**Branch:** `chore/frontend-enhancements`
**Scope:** Full-pass quality lift on the React frontend — modularity reuse, UX polish, flow completeness, onboarding hardening, targeted perf wins.

---

## 1. Context

The CQRS boilerplate frontend has matured rapidly through ~22 feature modules. A four-dimensional audit (modularity, UI/UX, flow completeness, perf/code-quality) confirmed the foundation is solid — slot extension system, TanStack Query layer, axios refresh, lazy routes, theme tokens, Zustand slicing, error boundary, react-hooks/exhaustive-deps. Concrete gaps cluster in five buckets:

1. **Reusability** — list-page boilerplate (pagination, loading/error/empty ladder, search+filter toolbar, avatar-name cells) and status badge mappings repeat across 19+ pages.
2. **UI/UX polish** — wide tables clip on mobile, ~20 icon-only buttons miss `aria-label`, auth forms use logical-direction-blind padding (`pl-` instead of `ps-`), three detail pages lack `useBackNavigation`, two pages render raw "Loading…" text, toast feedback is uneven.
3. **Flow completeness** — backend capabilities exist with no FE surface: tenant branding/business-info/custom-text/default-role, bulk user actions, feature flag edit/delete, audit log row detail, notification preferences write, emergency API key revoke, webhook admin nav.
4. **Onboarding** — wizard exists (logo + invites) but trigger is a fragile heuristic (`tenant ≤1 user AND no logo AND not localStorage-dismissed`), translations exist only in EN, no remind-me-later, no re-entry, no progress persistence.
5. **Perf / hygiene** — notifications still poll on a 30s interval despite Ably being wired; one hardcoded `['files']` key bypasses `queryKeys`; `noUncheckedIndexedAccess` is off.

The branch `chore/frontend-enhancements` is dedicated to closing these gaps. AI module FE is **out of scope** — the AI roadmap (Plan 5c) is being driven separately. The slot system already correctly surfaces comments-activity on `ProductDetailPage` and `WorkflowInstanceDetailPage` — extending it to `TenantDetailPage` and `UserDetailPage` is **deferred** unless time allows.

---

## 2. Goals

- **Reuse over repetition.** Every list page should compose two hooks and one toolbar component instead of re-implementing the same 40 lines.
- **UX consistency on a single read-through.** No raw loading text, no cropped tables on mobile, every icon-only button labeled, every detail page back-navigable, RTL-correct from the auth screens onward.
- **No dead-end buttons.** If the BE supports an action, the FE either surfaces it or we record why it doesn't.
- **Onboarding survives a device switch and a language switch.** State lives on the BE, copy lives in all three locales.
- **Don't regress what's solid.** Module boundaries, slot system, query layer, axios interceptor — leave alone.

## 3. Non-goals (deferred — see §10)

- AI module FE (assistants, chat, documents, personas, agents).
- Building a comments-activity timeline on `TenantDetailPage` or `UserDetailPage`.
- Plan CRUD admin UI for billing.
- Multi-file + drag-drop upload on `FilesPage`.
- Deepening onboarding scope (channel setup, sample data, default role chooser, billing plan picker).
- Platform-admin-specific onboarding flow.
- Converting `OnboardingWizard` from full-screen overlay to a routed page.
- Row memoization on large list pages (no jank reported yet).
- Bundle barrel-export tightening on `src/components/ui/index.ts`.
- Real-time push for workflow / webhook deliveries / reports.

---

## 4. Architecture & Approach

The branch executes five workstreams. Each ships its own commit (or commit cluster) so reviewers can read the diff in coherent chunks. **Order matters** — A is a dependency of B and D; C and E are independent and slot in last.

```
A (foundation) → B (polish, uses A) ─┐
                                     ├→ Sequential commits → Post-feature test → PR
                 D (flows, uses A) ──┤
                 C (onboarding) ─────┤
                 E (perf tweaks) ────┘
```

### 4.1 Workstream A — Reusability foundation

**New module: `src/hooks/useListPage.ts`**
Composite hook returning everything a list page needs:

```ts
const list = useListPage({
  query: useUsers,                     // any TanStack hook
  defaultSort: { sortBy: 'createdAt', sortDir: 'desc' },
  initialFilters: { search: '', status: '' },
});
// → { data, pagination, isLoading, isError, isEmpty, params, setSearch, setFilter, setPage, setPageSize, refetch }
```

Internally combines:
- `pageNumber` / `pageSize` state (with `getPersistedPageSize` initial)
- `search` (debounced ~300ms)
- arbitrary `filters` map
- derived booleans `isEmpty` / `isErroring` / `isInitialLoading`
- stable `params` object passed to the query hook

This eliminates the boilerplate that currently appears in 19 list pages.

**New component: `src/components/common/ListPageState.tsx`**
Renders the standard error/loading/empty ladder so pages don't re-implement it:

```tsx
<ListPageState state={list} emptyState={{ icon, title, description, action }}>
  {(rows) => <Table>...</Table>}
</ListPageState>
```

**New component: `src/components/common/ListToolbar.tsx`**
Composable toolbar: search input, filter slot, action slot. Replaces the duplicated header rows in `ProductsListPage`, `WorkflowInstancesPage`, `AuditLogsPage`, `UsersListPage`, etc.

**New component: `src/components/common/EntityLinkCell.tsx`**
Avatar + name + email + link table cell. Used in users / tenants tables.

**Cleanup tasks:**
- Replace local `STATUS_VARIANTS` in 4 files with import from `@/constants/status.ts`.
- Add missing `index.ts` to `feature-flags/`, `notifications/`. (`onboarding/` is intentionally components-only — no `api/` needed since it composes other features' APIs.)
- Move `access/types.ts` → `src/types/access.types.ts` for consistency.

### 4.2 Workstream B — UX polish campaign

**Table primitive — `src/components/ui/table.tsx`**
Add wrapper `<div className="overflow-x-auto">` so wide tables scroll horizontally on mobile instead of clipping. Single change applies everywhere.

**Accessibility — `aria-label` campaign**
Walk the ~20 icon-only buttons identified by the audit (FeatureFlagsList, ApiKeysPage, FilesPage, WebhooksPage, comments-activity CommentItem, import-export ImportsTab, etc.) and add localized `aria-label`s. Translations go to `common.actions.*` shared keys.

**RTL fixes**
- Auth forms: `pl-10` / `pr-10` → `ps-10` / `pe-10` across LoginForm, RegisterPage, RegisterTenantPage, VerifyEmailPage, AcceptInvitePage, ForgotPasswordPage.
- ChevronRight icons: add `rtl:rotate-180` in AuditLogsPage, tenants ActivityTab, workflow BulkResultDialog.
- AuditLogsPage `ml-1` → `ms-1`.

**Back navigation**
Add `useBackNavigation` to:
- `WebhookAdminDetailPage`
- `WorkflowDefinitionDetailPage`
- `WorkflowInstanceDetailPage`

**Loading state cleanup**
Replace raw `"Loading..."` strings in `TenantProductsTab` and `ProductDetailPage` with the existing `<Spinner>` primitive (or skeleton if appropriate).

**Toast consistency**
Audit mutation hooks for missing success/error toasts. Extract a `useMutationToast({ successKey, errorKey })` helper that wraps `useMutation` to standardize the pattern.

### 4.3 Workstream C — Onboarding hardening

**Backend (small touch):**
Add `OnboardedAt` (nullable `DateTimeOffset`) to the `Tenant` entity. New endpoint: `POST /api/v1/tenants/{id}/mark-onboarded`. Existing `UpdateTenantBranding` already covers logo + description, so no other BE changes needed for the wizard's actions.

**Frontend:**
- `useOnboardingCheck` reads `tenant.onboardedAt` from the user object (extend `UserDto.tenantOnboardedAt`) instead of guessing from user count + logo.
- `OnboardingWizard.onComplete` and `Skip` both call `markOnboarded`. localStorage flag remains as instant-feedback cache, but truth lives on the BE.
- "Remind me later" button — sets a 24h localStorage cookie; doesn't call BE.
- "Run setup again" — exposed in Profile page as an action; clears `OnboardedAt` (only callable by tenant admins) and refreshes.
- AR + KU translations for all `onboarding.*` keys — straight translation of existing EN strings.
- `useUsers({ enabled })` call is removed from `useOnboardingCheck` since we no longer need user count.
- Wizard step state survives refresh via `sessionStorage`.

### 4.4 Workstream D — Flow completeness

Each is a self-contained slice. Picked because each closes a gap a user would hit on the happy path.

**D1. Tenant branding settings**
Add a `Branding` tab to `TenantDetailPage` with three sections:
- Branding (logo, description) — `UpdateTenantBranding`
- Business Info (legal name, address, phone, tax id) — `UpdateTenantBusinessInfo`
- Default Role — `SetTenantDefaultRole`
- Custom Text — already a tab; verify it works end-to-end and polish.

**D2. Bulk user actions on UsersListPage**
Add row checkboxes + selection state + sticky action bar with: Activate, Suspend, Deactivate.
**Backend:** add `BulkUpdateUserStatusCommand` (accepts `userIds[]` + `status`) — N+1 round-trips for typical use is unacceptable UX.

**D3. Feature flag edit/delete**
Surface edit and delete actions on `FeatureFlagsPage` for platform admins. Use existing `UpdateFeatureFlag` and `DeleteFeatureFlag` endpoints. Wire to `ConfirmDialog`.

**D4. Audit log row detail drawer**
Click row → side drawer with full event payload (request body, response, IP, user agent, timestamps). No new BE endpoint — list endpoint already returns full record; we just need the UI.

**D5. Notification preferences write**
The component already renders preferences read-only. Wire it to the existing `UpdateNotificationPreferences` mutation. Add per-channel toggles (email / in-app / push) per category.

**D6. Emergency API key revoke**
Add a "Revoke immediately (with reason)" action gated by `ApiKeys.EmergencyRevoke` permission. Reason dialog → `EmergencyRevokeApiKey` endpoint.

**D7. Webhook admin sidebar entry**
Add a sidebar nav link for SuperAdmin → `WebhookAdminPage`. Already routed, just orphaned from nav.

### 4.5 Workstream E — Perf tweaks

- Notifications: replace `refetchInterval: 30000` with reliance on `useAblyNotifications` invalidation. Remove polling.
- Replace `['files']` hardcoded key in [access.queries.ts:94](../../boilerplateFE/src/features/access/api/access.queries.ts) with `queryKeys.files.all`.
- Add `noUncheckedIndexedAccess: true` to `tsconfig.app.json`. Fix any new errors.

---

## 5. New / Modified Files Summary

| Workstream | Files added | Files modified |
|---|---|---|
| A | 4: `useListPage.ts`, `ListPageState.tsx`, `ListToolbar.tsx`, `EntityLinkCell.tsx` + missing `index.ts` × 2 | ~19 list pages adopt new primitives; 4 status badge cleanups |
| B | 0 | `table.tsx` (overflow), 6 auth pages (RTL), 3 detail pages (back nav), 2 pages (loading), ~20 icon buttons, 1 mutation toast helper |
| C | 1 BE endpoint, 1 BE entity field + migration, 0 FE files | `useOnboardingCheck.ts`, `OnboardingWizard.tsx`, `Profile` page, EN/AR/KU translation files, `User` types |
| D | D2 BE bulk command + handler + validator | `TenantDetailPage`, `UsersListPage`, `FeatureFlagsPage`, `AuditLogsPage`, `NotificationPreferences`, `ApiKeysPage`, `Sidebar.tsx` |
| E | 0 | `notifications.queries.ts`, `access.queries.ts`, `tsconfig.app.json` |

## 6. Risks & Mitigations

| Risk | Mitigation |
|---|---|
| Refactoring 19 list pages onto new primitives introduces regressions | Adopt incrementally per page; keep old patterns valid; full QA pass at end via post-feature-testing skill |
| BE `OnboardedAt` migration touches the boilerplate's migration policy (memory says no migrations checked in) | Add field, but do **not** commit the EF migration — projects generate their own per CLAUDE.md / memory rule |
| `BulkUpdateUserStatusCommand` could be misused | Tenant-scope guard + `Users.Manage` permission + max 100 IDs |
| New `noUncheckedIndexedAccess` may surface ~20+ TS errors | Done last in workstream E so it doesn't block earlier work; fix inline |
| Toast helper adoption is incomplete | Acceptable — gradual migration is fine; existing toasts continue to work |
| Comments timeline on tenant/user detail not in scope but tempting to add | Resist scope creep; `TODO_DEFERRED.md` records it |

## 7. Testing Strategy

- **TypeScript:** `npm run build` must pass after each workstream.
- **ESLint:** `npm run lint` must pass.
- **Manual:** post-feature-testing skill at the end:
  1. Spin up a renamed test app on free ports.
  2. Generate migrations including the new `OnboardedAt` field.
  3. Verify each workstream's surface area in browser via Playwright MCP.
  4. Test as SuperAdmin, tenant admin, and tenant user — confirm permission gating works.
  5. Switch language to AR, verify RTL fixes and onboarding translations.
  6. Resize to mobile, verify table horizontal scroll.
  7. Leave running, hand to user for QA.

## 8. Permissions impact

- New: `ApiKeys.EmergencyRevoke` (already exists on BE — verify mirrored on FE constants).
- Existing: `Tenants.Manage`, `Users.Manage`, `FeatureFlags.Manage`, `AuditLogs.View`, `Notifications.ManagePreferences`.
- No new role assignments needed.

## 9. Definition of Done

- All in-scope items in §4 implemented.
- `npm run build` and `npm run lint` clean.
- `dotnet build` clean for BE touches (D2 bulk endpoint, C onboarding endpoint).
- Post-feature-testing dry-run completes: app boots, every workstream's surface verifies in browser as SuperAdmin + tenant admin + tenant user in EN + AR.
- `TODO_DEFERRED.md` (or equivalent) committed with the deferred-items checklist.
- Single PR opened with workstream commits squash-mergeable or kept as logical chunks per reviewer preference.

## 10. Deferred Items (record so we come back)

These were identified by the audit but excluded from this branch. Capture in `docs/superpowers/notes/2026-04-25-frontend-enhancements-deferred.md` so they don't get lost.

**Strategic features (own plan each):**
1. AI module FE — assistants list, chat UI, documents ingestion, personas, agent templates browser, search.
2. Comments-activity timeline on `TenantDetailPage` + `UserDetailPage` (slot extension).
3. Plan CRUD admin page for platform admins (billing).
4. Multi-file + drag-drop upload on `FilesPage`.
5. Onboarding deepening: communication channel setup, sample data import, default role chooser, billing plan picker.
6. Platform admin first-time setup wizard.
7. Convert `OnboardingWizard` from full-screen overlay to a routed `/onboarding` page.

**Nice-to-have polish (low ROI right now):**
8. Row memoization on `FilesPage`, `TenantDetailPage`, `WorkflowInboxPage` — defer until jank reported.
9. Tighten `src/components/ui/index.ts` barrel exports — mostly cosmetic, low impact.
10. Add route-level `ErrorBoundary` for feature-specific fallbacks.
11. Real-time push (Ably) for workflow tasks, webhook deliveries, report job completion.
12. Surface `showOnboarding` heuristics replacement uniformly with React context if more triggers emerge.

**Audit log enhancements:**
13. Date range picker for filters.
14. Saved filter presets.

**Webhook enhancements:**
15. Replay-failed-delivery action.
16. Per-event subscription management UI (currently event types are hardcoded in CreateWebhookDialog).

**Files enhancements:**
17. Folder/tag organization.
18. Bulk delete.

Each deferred item should become its own brainstorm → spec → plan when prioritized.

---

## 11. Open Questions

None blocking. User authorized "go with my instinct" for scope; deferred items are captured.
