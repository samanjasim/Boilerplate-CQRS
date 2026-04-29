# Phase 3 — Data cluster redesign (Files, Reports, Notifications)

**Created:** 2026-04-29
**Branch:** `fe/redesign-phase-3-views`
**Predecessors:** Phase 0 (visual foundation), Phase 1 (Identity), Phase 2 (Platform admin).
**Roadmap reference:** [`2026-04-28-post-phase-2-status-and-roadmap.md`](2026-04-28-post-phase-2-status-and-roadmap.md) §3.3.

---

## 1. Goal

Bring the three Data-cluster pages onto the J4 Spectrum visual language while giving each one a clear character that reflects how the data actually behaves. The cluster splits into two patterns:

- **Hero-strip pattern** (Files, Reports) — the page opens with a stat strip earned by the data: storage totals for Files, an in-flight queue for Reports.
- **Grouped-list pattern** (Notifications) — no hero; the page earns its identity through date grouping, a counted segmented filter, and a preferences entry point.

Why this split: a hero on Notifications would be weak (unread count is already in the sidebar badge), and a list-only treatment on Reports would hide the only async element in the cluster. The split reflects the data, not a desire for parallel structure.

## 2. Non-goals

Listed up front so they don't get relitigated:

- **Files:** thumbnail grid for image files, bulk-delete UX. Both are real product improvements but unrelated to the visual redesign and have their own design questions (signed-URL fetch + fallback for thumbnails; sticky action bar + multi-select state for bulk delete). Defer to Phase 5 or a focused follow-up.
- **Reports:** detail page, freshness indicator on download links, inline-retry beyond the existing button. Revisit only if production usage surfaces a real need.
- **Notifications:** detail page, in-page preferences drawer. The link to the existing preferences page is enough.
- **Backend:** no schema changes. If the BE already returns aggregates for Reports, we use them; otherwise we add one lightweight read endpoint (see §4).
- **Translations:** AR + KU land **inline** with the same commit, not deferred. Phase 2 deferred translations and we paid the cost during the post-merge RTL pass; we won't repeat that.

## 3. Pages

### 3.1 Files (`FilesPage.tsx`, currently 852 LOC)

**Hero strip (new).** Promote `StorageSummaryPanel` from a dialog to a persistent hero strip at the top of the page:

- Glass-card container, `surface-glass`, full nav-content width, `mb-6` spacer below.
- **Total** (left, dominant): gradient-text byte total (`24.8 GB`), eyebrow `Total storage`. The quota comes from the `files.max_storage_mb` feature flag (BE already enforces it via `UploadFileCommandHandler`); read on the FE via `useFeatureFlag('files.max_storage_mb')`. When the flag has a value, show `of 100 GB` next to the figure with a thin progress bar beneath colored against the quota; when unset, render only the total. Super-admins viewing the cross-tenant aggregate skip the progress bar entirely (no global quota).
- **By category** (middle, takes most width): up to 4 horizontal bars — one per file category, with category name + size + count to the side. Each bar uses `bg-primary` with width proportional to the largest category's size (existing logic in `StorageSummaryPanel.tsx:63`). If more than 4 categories exist, collapse the tail into "Other".
- **Super-admin toggle** (right): the existing "all tenants" checkbox repositions inline as a small ghost-styled toggle. Tenant users don't see it.
- The "Storage Summary" trigger button on the page header is **removed**. The dialog component is **deleted** (the data is now permanently visible).

**Page decomposition (carry-along refactor).** `FilesPage.tsx` is at 852 LOC; the redesign work justifies extracting:

- `components/FileUploadDialog.tsx` — upload dialog with drag-and-drop and category select (already in the page; lift as-is).
- `components/FileEditDialog.tsx` — rename / description edit dialog.
- `components/FileRowActions.tsx` — the per-row dropdown menu (download / share / transfer / edit / delete) and its dialog state.
- `components/FilesGridView.tsx` — the grid layout (extracted from the existing layout switch).
- `components/FilesTableView.tsx` — the table layout (same).
- `components/StorageHeroStrip.tsx` — the new hero (replaces `StorageSummaryPanel.tsx`, which is deleted).

Target: `FilesPage.tsx` lands under 250 LOC and reads as composition + filter/pagination state. Existing behaviour preserved 1:1 — no changed semantics, no removed features.

**Visibility filter unchanged** (`all / mine / shared / public` already in place). View toggle (grid / list) unchanged.

### 3.2 Reports (`ReportsPage.tsx`, currently 329 LOC)

**Status hero strip (new).** Three glass stat cards above the existing filter row:

| Card | Source | Treatment |
|---|---|---|
| **Active** | `pendingCount + processingCount` | Eyebrow `in flight`. Tinted with `--active-bg`. Tiny `Spinner` glyph next to the number iff `processingCount > 0`. |
| **Completed** | `completedCount` | Eyebrow `ready to download`. Default glass treatment. |
| **Failed** | `failedCount` | Eyebrow `failed`. Red-tinted (`bg-destructive/10 text-destructive`). **Only renders when `failedCount > 0`** — when zero, the slot collapses and Active + Completed share the row at 50/50. |

The hero counts come from a tenant-scoped read. **Implementation choice (decide during plan, not now):**

- **Preferred:** piggy-back the existing list query — its server response should already carry total counts per status. If yes, derive from there.
- **Fallback:** add `useReportsStatusCounts()` calling a small BE endpoint `GET /api/v1/reports/status-counts` returning `{ pending, processing, completed, failed }`. New endpoint follows the same controller / handler pattern as the existing list query.

**Filter row, table, action buttons, confirm dialogs — unchanged.** No detail page. Existing `StatusBadge` component stays as-is.

### 3.3 Notifications (`NotificationsPage.tsx`, currently 140 LOC)

**Date grouping (client-side).** Replace the flat list with grouped sections:

- Group keys: `Today`, `Yesterday`, `Earlier this week`, `Earlier this month`, `Older`. Strict windows (today = same calendar day in user's timezone; "earlier this week" = same ISO week minus today/yesterday; etc.). A group with zero rows on the current page is skipped.
- Group header: `text-xs uppercase tracking-[0.12em] text-muted-foreground`, copper bullet dot prefix matching the sidebar group label treatment.
- Grouping is **per-page only** — pagination is unchanged. If page 2 has rows from a different week, it gets its own headers. We don't reach across pages.
- Existing row treatment unchanged (icon, title, message, time-ago, unread tint, dot).

**Segmented filter (replaces All / Unread buttons).** A pill-style segmented control:

- Layout: `All (n) · Unread (n)` — both segments show counts.
- Active segment uses `pill-active` (the same treatment as sidebar nav items).
- Counts: `All` = total notifications across all pages (from the existing list-query pagination response). `Unread` = total unread, queried separately via the existing `?isRead=false` filter — we already have the data. If the BE doesn't return both totals in a single response, we accept one extra request when this page first renders (cheap, cached, query-keyed).
- The current "Mark all as read" button stays, repositioned to the right of the segmented control.

**Preferences entry point (new).** A small ghost button in the page header (`<PageHeader>` slot or to the right of the title): `Notification preferences →`, links to the existing notification preferences page (route lookup during plan; the page already exists per the project inventory). Visible only to users with permission to manage their own preferences (likely all logged-in users — confirm).

**No hero strip** — the page's identity comes from the grouping + counted toggle.

## 4. Backend

The redesign is FE-only with one possible exception:

- **Reports status counts.** If the existing list query already returns per-status totals in its envelope, no BE work needed. If not, add one query handler:
  - Query: `GetReportStatusCountsQuery` returning `Result<ReportStatusCountsDto>`.
  - DTO: `ReportStatusCountsDto(int Pending, int Processing, int Completed, int Failed)`.
  - Controller: `GET /api/v1/reports/status-counts`, authorized via the existing reports view permission.
  - Tenant-scoped via the existing `ApplicationDbContext` global filter — no special handling.
  - Cached only if measurement shows it's worth it (tiny queries; not a priority).

The plan task that touches Reports decides which path based on a quick read of the current list-query response shape.

## 5. Translations

All new keys land in **all three locales (EN, AR, KU)** in the same commit. No deferral.

Anticipated new keys (canonical EN forms; AR + KU translated alongside):

```
files:
  storageHero:
    total: "Total storage"
    ofQuota: "of {{quota}}"
    byCategory: "By category"
    allTenants: "All tenants"
    other: "Other"
reports:
  hero:
    active: "Active"
    activeEyebrow: "in flight"
    completed: "Completed"
    completedEyebrow: "ready to download"
    failed: "Failed"
    failedEyebrow: "failed"
notifications:
  groups:
    today: "Today"
    yesterday: "Yesterday"
    earlierThisWeek: "Earlier this week"
    earlierThisMonth: "Earlier this month"
    older: "Older"
  filter:
    all: "All ({{count}})"
    unread: "Unread ({{count}})"
  preferencesLink: "Notification preferences"
```

Final key paths and copy may shift slightly during implementation; the rule is "EN + AR + KU together, in the same commit as the component change".

## 6. Verification

The Phase 2 testing routine still applies. For each page:

- `npm run build` clean.
- `npm run lint` clean.
- Live test in `_testJ4visual` (FE on 3100 / BE on 5100). Source-edit + file-copy for FE-only changes; regenerate test app only if the BE endpoint is added.
- **RTL pass (Arabic)** — every Phase 3 page exercised in AR. Check: hero strip mirrors correctly; group headers render right-aligned; segmented filter works; gradient-text figures don't break on Arabic numerals (use Latin digits — i18next default).
- **Permission matrix:**
  - Super-admin: Files (cross-tenant via toggle), Reports (cross-tenant), Notifications (own).
  - Tenant admin: Files (own tenant; toggle hidden), Reports (own tenant), Notifications (own).
  - Regular user: Files (own tenant, scoped to visibility filter), Notifications (own). Reports hidden — `System.ExportData` permission required.
- **Phase 1 + Phase 2 visual regression** — no changes expected, but spot-check Identity (Users, Roles, Tenants, Profile) and Platform admin (Audit Logs, Feature Flags, API Keys, Settings) to confirm nothing broke.

## 7. Rollout

One branch, one PR. Per-page commits within the branch are fine, but the PR ships all three pages together — they share the cluster's design vocabulary and reviewers benefit from seeing them as a set. Subagent-driven execution with review checkpoints between pages, mirroring the Phase 2 cadence.

## 8. Open question for the plan stage

- **Reports status counts source:** existing list-query envelope vs new endpoint. Decide by reading `boilerplateBE/src/Starter.Api/Controllers/ReportsController.cs` + the corresponding query handler at the start of the Reports task. Both paths are spec-compatible; the plan doesn't need to commit until then.
