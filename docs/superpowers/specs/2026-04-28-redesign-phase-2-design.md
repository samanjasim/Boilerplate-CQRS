# Phase 2 — Platform Admin Cluster Polish

**Created:** 2026-04-28
**Branch:** `fe/redesign-phase-2-views` (off `origin/main`)
**Predecessor:** Phase 1 (sidebar IA + Identity cluster + command palette) — shipped as PRs #28 / #29 / #31.
**Successor (deferred — see §11):** Phase 3 — Data cluster; Phase 4 — Commerce; Phase 5 — Workflow & comms + Onboarding; Phase 6 — AI module UI; Phase 7 — Mobile (Flutter) port.

## 1. Why this phase exists

Phase 1 polished Identity (`users`, `roles`, `tenants`, `profile`, `access`) and proved the dashboard pattern — hero metric strip + glass content + sparkline cards — on entity-bearing data. Identity worked because users / roles / tenants *are* metric-bearing — they have counts, deltas, and population shapes worth surfacing.

Platform admin pages (`settings`, `feature-flags`, `api-keys`, `audit-logs`) are different: they are *administrative tools*, not entity dashboards. A sparkline on a settings page is decoration. A hero metric strip on API Keys is noise. Phase 2 tests whether J4 character can land on restrained admin surfaces — and where a real visual moment is justified (audit logs is a time-series; feature flags do have a meaningful health snapshot), it earns its space.

Phase 2 also lands the only new route in the J4 redesign so far — `AuditLogDetailPage` — because the existing flat list with truncated JSON expand-rows actively hurts the audit-log investigation flow.

## 2. Scope (in)

1. **`audit-logs/AuditLogsPage`** — timeline hero (events-in-window count + SVG sparkline), polished filter row + table, click-through wiring to detail page.
2. **`audit-logs/AuditLogDetailPage`** *(new route)* — event details card (syntax-highlighted JSON viewer of the `Changes` blob) + metadata card (actor, IP, trace ID, agent attribution, correlation links). Requires a small **backend addition**: `GetAuditLogByIdQuery` + handler + `GET api/v1/audit-logs/{id}` controller action (current controller exposes only the list endpoint).
3. **`feature-flags/FeatureFlagsPage`** — three sparkline stat cards (enabled / overridden / opted-out) + status pill column on the flag table; tenant-override drilldown stays inline (no new route).
4. **`api-keys/ApiKeysPage`** — restrained: header KPI badge ("N active · N expiring in 30d"), glass table, redesigned secret-reveal screen (the most-delicate UX in the cluster).
5. **`settings/SettingsPage`** — restrained: existing category tabs become a sticky sidebar (`≥lg`) / horizontal tabs (`<lg`), glass cards per setting group, sticky save bar when dirty, per-tenant override badges.

## 3. Out of scope (deferred — picked up in later phases)

Listed here so a future session can pick them up without re-reviewing this work. Each item has a clear next-phase owner.

| Deferred item | Picked up in |
|---|---|
| Per-feature polish for **Data** cluster (`files`, `reports`, `notifications`) | Phase 3 |
| Per-feature polish for **Commerce** cluster (`billing`, `products`) | Phase 4 |
| Per-feature polish for **Workflow & comms** cluster (`workflow`, `communication`, `comments-activity`, `import-export`, `webhooks`) | Phase 5 |
| Per-feature polish for **Onboarding** wizard | Phase 5 (bundled) |
| **AI module UI** (chat surfaces, persona×role admin, agent template browser, RAG ingestion, RAG eval dashboards, public widget config) | Phase 6 |
| **Mobile (Flutter) J4 port** | Phase 7 |
| **`FeatureFlagDetailPage`** (per-tenant override matrix as its own route, opt-out list, audit history) | Re-evaluate after Phase 2 ships — only if the inline drilldown proves inadequate |
| **AR + KU translations** for new keys (`auditLogs.detail.*`, `auditLogs.timeline.*`, `featureFlags.stats.*`, `apiKeys.reveal.*`) | EN ships in Phase 2; translation pass when localizer available (i18next falls back to EN — does not block merge) |
| **State-diff UX for audit logs** (paired before/after) | Requires BE schema changes — store before/after on `AuditLog`. Out of scope for Phase 2; revisit if/when audit data model evolves. |
| **Top-actors strip on audit timeline** (top 3 acting users + top 3 affected entities) | Re-evaluate if existing filter chips don't satisfy investigation flows |
| **Stacked-by-severity timeline** (Info / Warning / Error bands) | Re-evaluate post-Phase 2 — current filter chips already cover severity |
| **BE endpoint for audit timeline buckets** (replacing client-side 2000-row fetch) | Only if production usage shows the client-side approach is too slow |
| **Audit log retention / archive UI** | Out of scope — backend feature, not visual polish |
| **`/styleguide` access in production** | Open question carried from Phase 1 — not decided in Phase 2 |
| **Bottom-nav / mobile-first deep redesign** | Phase 7 (Mobile port) — web stays drawer-only |
| **`useCountUp` consolidation** (currently 3 implementations) | Extract once a 4th consumer appears |

## 4. Audit logs — list page (`AuditLogsPage`)

### 4.1 Timeline hero

Full-width hero block above the existing filter row. Surface: `surface-glass` with copper-tinted top edge. ~120px tall.

**Left third:**
- Big number — total events matching the active filter window. Gradient-text via `.gradient-text`.
- Secondary line — window label ("last 24 hours", "last 7 days", or filter-range string).

**Center two-thirds:**
- Inline-SVG sparkline, ~80px tall, full bar width.
- Bucket sizing by window: `≤1h` → per-minute, `≤24h` → per-hour, `≤7d` → per-day, `>7d` → per-day with a banner. (See §4.4 for capacity limits.)
- Hover shows bucket count + timestamp.
- Empty window → flat baseline + caption "No events in this window".

**Pattern:** mirrors the dashboard's existing inline-SVG sparkline approach — no chart library is added.

### 4.2 Table polish

- Existing inline expand-row removed. Whole table row becomes a clickable affordance routing to `/admin/audit-logs/:id`.
- Status pill column uses J4 `Badge` variants (`info`, `pending`, `failed`, `healthy`).
- `surface-glass` table container per CLAUDE.md (Table component already includes it; verify no extra `Card` wrapper).
- Filter row: existing FilterPanel polished — copper-tinted active filters, keep behavior.

### 4.3 Filter behavior

Filter changes refetch the timeline rows. Debounce: 500ms before re-fetch fires. TanStack Query keyed on the full filter object so cache survives back-navigation.

### 4.4 Capacity & data shape

- Timeline buckets are derived **client-side** from existing paged audit list. For the active filter window, fetch up to **N=2000** most-recent matching rows and bucket in JS.
- `>2000` rows in window → show banner above sparkline ("Showing last 2,000 events for the timeline. Refine the filter for accurate counts.") and continue rendering. Table itself remains paged independently.
- No new BE endpoint for the timeline itself. (The detail page does add `GET api/v1/audit-logs/{id}` — see §5.4.)

## 5. Audit logs — detail page (`AuditLogDetailPage`) *(new route)*

### 5.1 Route registration

- Path: `/admin/audit-logs/:id` (mirrors existing admin path convention).
- Lazy component in `routes.tsx`.
- Permission: `System.ViewAuditLogs` via `PermissionGuard`.
- 404 page when `useAuditLog(id)` returns NotFound (handles both wrong-tenant and deleted/expired cases).

### 5.2 Layout

- `useBackNavigation('/admin/audit-logs', t('auditLogs.title'))` for the header back arrow.
- **Header band:** action verb as `.gradient-text` (derived from the `action` field — e.g., `Updated` → "User updated"), entity display name, timestamp. Status pill derived from action category: destructive actions (`Deleted`, `Revoked`, `Suspended`) → `failed` (red); auth-related (`Login`, `Logout`, `LoginFailed`) → `info`; everything else → `info`. Pill uses J4 status `Badge` variants.
- **Body:** two columns on `≥lg`, single column below.
  - **Left column — Event card** (`Card variant="glass"`): syntax-highlighted JSON viewer for the `changes` blob (see §5.3).
  - **Right column — Metadata card** (`Card variant="solid"`): actor row (`UserAvatar` + name + email), IP, trace/conversation ID with copy button, agent attribution block (visible only when `agentPrincipalId` is set: "Acted on behalf of {user}" + agent run link), tenant (super-admin only — never shown to tenant users), correlation links ("Same conversation: 3 events" → filtered list using `correlationId`).

> **Data shape note:** the existing `AuditLog` entity has no `severity`, no `userAgent`, and no paired `before`/`after` fields. `changes` is a single JSON blob describing the event (e.g., `{ Event: "ResourceGrantCreated", ResourceType: "...", ... }`). The original spec assumed a diff UX; reality is event-shaped logs. The detail page therefore displays the event as syntax-highlighted JSON rather than a side-by-side diff. This matches the data the BE actually emits and avoids over-engineering for a paired-state shape that doesn't exist.

### 5.3 `JsonView` component

A read-only JSON viewer (not a diff). Pure presentational, walks the parsed `changes` value recursively.

- **Syntax highlighting:**
  - Object/array braces — muted foreground.
  - Keys — copper tint.
  - String values — emerald tint.
  - Number / boolean / null values — accent (violet) tint.
  - All tints come from existing semantic CSS vars (`--tinted-fg`, `--color-violet-600`, etc.); no new tokens.
- **Layout:** monospace font (`font-mono`), 2-space indent, line-numbers in copper, hover-row highlight via `--hover-bg`.
- **Container:** `dir="ltr"` override so JSON keys render left-aligned even in RTL apps (consistent with code blocks).
- **Robustness:** `JSON.parse` wrapped in try/catch — on parse failure, render the raw string as preformatted text + a small "Raw event payload" caption.
- **A11y:** `role="region"` + `aria-label="Event payload"`; container is keyboard-scrollable.
- Size budget: ~120 LOC. No external library.

### 5.4 Data shape

- New BE endpoint: `GET api/v1/audit-logs/{id}` (see Task 1 of §11). Returns `AuditLogDto`. Permission: `System.ViewAuditLogs`. Multi-tenant filter applied automatically via the existing `ApplicationDbContext` global filter (super-admins see cross-tenant; tenant users see their tenant's rows; mismatched id returns 404).
- New FE pieces: `auditLogsApi.getAuditLog(id)` in `audit-logs.api.ts`; `useAuditLog(id)` hook in `audit-logs.queries.ts`.
- The frontend `AuditLog` type gains `onBehalfOfUserId`, `agentPrincipalId`, `agentRunId` (already present on the BE entity, surface them in the DTO + TS type).

## 6. Feature flags (`FeatureFlagsPage`)

### 6.1 Hero metric strip

Three sparkline stat cards in a row, matching the dashboard pattern (`Card variant="elevated"` + inline-SVG sparkline):

1. **Enabled flags** — count of flags with `Default=ON`. Sparkline of "enabled count over last 30d" derived from existing audit history if available client-side; otherwise flat baseline. (Decision punted to execution — design tolerates either.)
2. **Tenant overrides** — count of `TenantFeatureFlag` rows. Secondary chip ("3 tenants override").
3. **Opted-out tenants** — count of opt-out rows. Gradient-text count.

### 6.2 Table polish

- Status pill column added: `On` (healthy) / `Off` (failed) / `Per-tenant` (info).
- Remove any stray `gradient-hero` (the old solid copper) — replace with J4 surfaces.
- Tenant override drilldown stays inline (existing `Sheet` or row expansion). No new route in Phase 2.

## 7. API keys (`ApiKeysPage`)

### 7.1 Restrained header

- No hero metric strip.
- Inline KPI badge next to the page title: "{N} active · {N} expiring in 30d". Small, copper text. No chart.
- Compute "expiring in 30d" client-side from the existing list.

### 7.2 Table polish

- Glass table surface (no extra `Card` wrapper).
- Status pill (Active / Expiring / Revoked).
- Action menu unchanged.

### 7.3 Secret reveal redesign (`ApiKeySecretReveal`)

The most-delicate UX in the cluster. Replaces the inline secret display in the existing creation modal.

- **Layout:** large warning band ("Shown once. Save it now."), gradient-text secret value, copy button with success animation, secondary "Done" button.
- **Memory safety:** hold the secret in both a `useRef` AND state, so a parent re-render can't drop it before the user copies. No prop-flow back upward.
- **Close confirmation:** if the user attempts to close the modal *before* the copy event fires, prompt: "You haven't copied the key yet. Close anyway?" Single-shot — once they confirm, no second prompt.
- **A11y:** `aria-live="assertive"` announcement on render ("API key created. Copy it now.").
- **No download.** Copy-only. (Downloading creates persistence-on-disk risk — out of scope.)

## 8. Settings (`SettingsPage`)

### 8.1 Layout restructure

Existing category tabs become a sticky sidebar on `≥lg`, top tabs on `<lg`. Extracted to `SettingsCategoryNav.tsx`.

- Sticky sidebar respects bottom space when the dirty-save bar is present (CSS var `--settings-save-bar-h`, set on the page container, consumed in the sidebar's `bottom`).
- `aria-current="true"` on the active category. Arrow-key navigation between category items.

### 8.2 Group rendering

Each settings group inside `Card variant="glass"`. Per-tenant override badges next to overridden settings (copper tint, small).

### 8.3 Sticky save bar

Bottom-right floating bar appears when any setting is dirty. Contains: dirty-count chip ("3 unsaved changes"), Reset button (`variant="ghost"`), Save button (`variant="default"`). Hides when clean.

## 9. Components, data, and code budget

**No new shared components in `@/components/common/`.** All new code is page-scoped.

| File | Purpose | LOC budget |
|---|---|---|
| `features/audit-logs/components/AuditTimelineHero.tsx` | Hero block (count + SVG sparkline + bucket logic) | ~120 |
| `features/audit-logs/components/JsonView.tsx` | Read-only syntax-highlighted JSON viewer | ~120 |
| `features/audit-logs/components/AuditMetadataCard.tsx` | Actor / IP / trace / agent attribution block | ~90 |
| `features/audit-logs/pages/AuditLogDetailPage.tsx` | New route, assembles above | ~140 |
| `boilerplateBE/.../Features/AuditLogs/Queries/GetAuditLogById/...` | New BE query + handler | ~80 |
| `boilerplateBE/.../Controllers/AuditLogsController.cs` | Add `GET {id}` action | +15 |
| `features/audit-logs/pages/AuditLogsPage.tsx` | Refactored — adds hero, removes expand row, wires click-through | net +30 |
| `features/audit-logs/api/audit-logs.queries.ts` | Add `useAuditLog(id)` if absent | +20 |
| `features/feature-flags/components/FeatureFlagStatStrip.tsx` | Three sparkline stat cards | ~100 |
| `features/feature-flags/pages/FeatureFlagsPage.tsx` | Refactored — adds strip, status pill column | net +40 |
| `features/api-keys/components/ApiKeySecretReveal.tsx` | Replaces inline secret display in creation modal | ~80 |
| `features/api-keys/pages/ApiKeysPage.tsx` | Refactored — header KPI, glass table | net +30 |
| `features/settings/components/SettingsCategoryNav.tsx` | Sticky sidebar / horizontal tabs | ~90 |
| `features/settings/pages/SettingsPage.tsx` | Refactored — extracts nav, glass groups, sticky save | net +20 |
| `routes/routes.tsx` + `config/routes.config.ts` | Register `/admin/audit-logs/:id` | +10 |
| `i18n/locales/en/translation.json` | New keys (`auditLogs.detail.*`, `auditLogs.timeline.*`, `featureFlags.stats.*`, `apiKeys.reveal.*`) | +30 |

**Total budget:** ~900 LOC net new.

## 10. Risks, edge cases, accessibility, RTL

### 10.1 Risks & mitigations

- **Audit timeline performance** — fetching 2000 rows on every filter change is the risk. TanStack Query cache keyed on the filter object + 500ms debounce on filter change before re-fetch.
- **`JsonView` malformed payloads** — older audit rows or third-party events may store non-JSON strings in `changes`. Wrap `JSON.parse` in try/catch; on failure render the raw string as preformatted text with a "Raw event payload" caption.
- **API key secret reveal** — re-render that drops the secret from memory before copy is a real UX loss. `useRef` + state. Close-confirmation if copy hasn't fired.
- **Sticky settings save bar layout shift** — sidebar's `bottom` reads `--settings-save-bar-h` CSS var, set on the page container when dirty.
- **Sparkline empty state** — flat baseline + caption "No events in this window" instead of a degenerate single-point line.
- **Detail-route permission edge case** — must not 404 silently if list permission is held but the row belongs to a different tenant (super-admin only sees cross-tenant). Standard `PermissionGuard` + 404 page when `useAuditLog` returns NotFound.

### 10.2 Accessibility

- Timeline sparkline gets `role="img"` + `aria-label="Events over the last 24 hours: 1,234 events"`. Bucket-list `<ul>` (visually hidden) exposes per-bucket counts to screen readers.
- `JsonView` container gets `role="region"` + `aria-label="Event payload"`; keyboard-scrollable.
- Secret reveal — `aria-live="assertive"` announcement on render.
- Settings sticky sidebar — `aria-current="true"` on active category; arrow-key navigation between categories.

### 10.3 RTL

- Timeline reads left-to-right always (time is universal). Sparkline does **not** mirror.
- `JsonView` container gets `dir="ltr"` override (JSON is always LTR, same convention as code blocks).
- All other surfaces follow standard `text-start` / `ltr:` / `rtl:` patterns.

## 11. Execution plan

Single plan executed via `superpowers:subagent-driven-development`. Each task is its own commit on `fe/redesign-phase-2-views`. Visual verification per task using the Phase 0 test-app harness (`_testJ4visual` if still present, otherwise re-spun); Playwright MCP for the audit click-through and detail-page diff.

| Task | Scope | Estimate |
|---|---|---|
| 1 | Audit logs list polish (timeline hero, click-through wiring, table polish) | ~1 day |
| 2 | BE `GetAuditLogByIdQuery` + handler + controller action + FE `useAuditLog`; `AuditLogDetailPage` + `JsonView` + `AuditMetadataCard` (new route, route registration, EN translations) | ~2 days |
| 3 | Feature flags hero metric strip + status pill column + table polish | ~1 day |
| 4 | API keys polish + secret reveal redesign | ~0.5 day |
| 5 | Settings polish (sticky sidebar / horizontal tabs, glass groups, sticky save bar) | ~1 day |
| 6 | Code review pass via `superpowers:code-reviewer`, final polish, EN translation key sweep | ~0.5 day |

**Total: ~6 days.**

PR opens after task 6. AR + KU translations ship as a separate localizer pass (i18next falls back to EN — does not block merge).

## 12. Verification checklist

- [ ] `npm run build` passes; production bundle size delta < +30KB gzipped (no chart library added; new code is page-scoped).
- [ ] `/styleguide` still renders; no primitive regressions (J4 utilities + `Card` / `Badge` / `Table` variants unchanged).
- [ ] Identity cluster pages (Phase 1) unchanged — visual regression check on `/users`, `/roles`, `/dashboard`.
- [ ] Audit logs detail route 404s correctly for wrong-tenant access (super-admin vs tenant admin).
- [ ] API key secret reveal cannot lose secret on parent re-render (test by triggering a dummy parent state change while modal open).
- [ ] Settings save bar layout: sticky sidebar bottom respects bar height when dirty.
- [ ] RTL pass: every Phase 2 page rendered with `dir="rtl"` — timeline sparkline does not mirror, JsonDiff stays LTR, all directional borders/margins respect `ltr:`/`rtl:` prefixes.
- [ ] Permissions: every permission gate verified for super-admin, tenant admin, and regular user role.

## 13. Open questions

- **Feature flag "enabled count over 30d" sparkline data source** — derive client-side from existing audit history, or render a flat baseline placeholder? Decided during execution. Design tolerates either.
- **Audit timeline 2000-row capacity** — sufficient for realistic filter windows in production? If post-ship telemetry shows frequent banner-triggered truncation, a BE endpoint becomes the Phase 3 follow-up (deferred above).
- **Settings page route organization** — currently a single page with category tabs. If the category list grows beyond ~8, the sticky sidebar becomes scrollable; revisit if the catalog grows substantially.

## 14. Acknowledgements

Phase 1 sidebar IA + Identity cluster + command palette established the cadence used here: brainstorm → spec → plan → subagent-driven execution → code review pass → PR. Phase 2 reuses that cadence verbatim. The `_testJ4visual` test-app harness from Phase 0 remains the verification surface; no new harness is introduced.
