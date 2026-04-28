# Post Phase 2 — Status & Roadmap for Continued Visual Work

**Created:** 2026-04-28
**Status:** Phase 2 shipped on `fe/redesign-phase-2-views` (PR open).
**Audience:** A future Claude session (or human) picking up J4 redesign work after Phase 2 lands.
**Predecessor:** [`2026-04-27-post-phase-0-status-and-roadmap.md`](2026-04-27-post-phase-0-status-and-roadmap.md) — status as of Phase 0 ship.

---

## 1. Where things stand

### Phase 2 is shipped

- **Spec:** [`2026-04-28-redesign-phase-2-design.md`](2026-04-28-redesign-phase-2-design.md)
- **Plan:** [`docs/superpowers/plans/2026-04-28-redesign-phase-2-platform-admin.md`](../plans/2026-04-28-redesign-phase-2-platform-admin.md)
- **Branch:** `fe/redesign-phase-2-views` — 4 commits, +2,400 / −360 (incl. spec, plan, implementation, review fixes)

### What Phase 2 delivers

- **Audit logs list page** — timeline hero (events-in-window count + SVG sparkline, no chart library), full-row click-through to detail page, completed entity-type and action filter dropdowns (User / Role / Tenant / File / API key / Resource grant / AI assistant + Created / Updated / Deleted / EmergencyRevoked).
- **`AuditLogDetailPage`** *(new route)* — gradient-text heading, status pill derived from action category, two-column glass body (`JsonView` event payload + `AuditMetadataCard` with actor / IP / trace / agent attribution + correlation drilldown link).
- **`JsonView` component** — read-only syntax-highlighted JSON viewer, ~150 LOC, no external diff library, `dir="ltr"` + `role="region"` aria treatment, graceful fallback to raw text on parse failure.
- **`AuditMetadataCard`** — copyable fields with hover-to-reveal copy button, agent attribution block (visible only when `agentPrincipalId` set) with violet runtime-token tinting.
- **Feature flags page** — three-card stat strip (`enabled` / `tenant overrides` / `opted-out`); status pill column with correct treatment for Boolean / non-Boolean (`Configured`) / per-tenant flags via shared `getFlagStatus` util.
- **API keys page** — KPI badge in header (active / expiring); redesigned `ApiKeySecretDisplay` with amber semantic-token warning band, copy button, close-without-copy confirm dialog.
- **Settings page** — sticky `SettingsCategoryNav` sidebar (≥lg) / horizontal tabs (<lg) with arrow-key navigation; glass-card setting groups; sticky bottom-right save bar coordinated via `--settings-save-bar-h` CSS var.
- **Backend addition** — `GetAuditLogByIdQuery` + handler + `GET /api/v1/audit-logs/{id}` controller action; `AuditLogDto` extended with `OnBehalfOfUserId` / `AgentPrincipalId` / `AgentRunId`; existing list handler also projects the new fields. Multi-tenant via the existing `ApplicationDbContext` global filter (super-admins see cross-tenant, tenant-admins see own tenant only).

### Verifiable artifacts

- `_testJ4visual/` test app verified — FE + BE running on 3100 / 5100, all five pages exercised.
- `npm run build` passes; `npm run lint` clean; `dotnet build` clean.
- Identity cluster (Phase 1) pages unchanged — no regressions.
- `/styleguide` (dev-only) renders all primitives unchanged.

---

## 2. What was deferred from Phase 2

Tracked here so a future session can pick them up without re-reading the spec or the diff.

### Deferred per the Phase 2 spec (§3)

| Item | Picked up in |
|---|---|
| Per-feature polish for **Data** cluster (`files`, `reports`, `notifications`) | **Phase 3 (next)** |
| Per-feature polish for **Commerce** cluster (`billing`, `products`) | Phase 4 |
| Per-feature polish for **Workflow & comms** cluster (`workflow`, `communication`, `comments-activity`, `import-export`, `webhooks`) | Phase 5 |
| Per-feature polish for **Onboarding** wizard | Phase 5 (bundled) |
| **AI module UI** (chat surfaces, persona×role admin, agent template browser, RAG ingestion, RAG eval dashboards, public widget config) | Phase 6 |
| **Mobile (Flutter) J4 port** | Phase 7 |
| **`FeatureFlagDetailPage`** (per-tenant override matrix as own route) | Re-evaluate after Phase 2 ships — the inline drilldown is sufficient for now |
| **AR + KU translations** for Phase 2 keys (`auditLogs.detail.*`, `auditLogs.timeline.*`, `featureFlags.stats.*`, `featureFlags.status.*`, `apiKeys.reveal.*`, `common.reset`, the new audit entity-type / action labels) | Translator pass when localizer available; i18next falls back to EN |
| **State-diff UX for audit logs** (paired before/after) | Requires BE schema changes — `AuditLog.Changes` is currently event-shaped, not paired-state. Out of scope here; revisit if/when the audit data model evolves. |
| **Top-actors strip on audit timeline** | Re-evaluate if filter chips don't satisfy investigation flows |
| **Stacked-by-severity timeline** | Re-evaluate post-Phase 2 |
| **BE endpoint for audit timeline buckets** | Only if production usage shows the client-side 2,000-row fetch is too slow |
| **Audit log retention / archive UI** | Out of scope — backend feature, not visual polish |
| **Bottom-nav / mobile-first deep redesign** | Phase 7 (Mobile port) — web stays drawer-only |
| **`useCountUp` consolidation** (currently 3 implementations) | Extract once a 4th consumer appears |

### Deferred during implementation review

These came up during the review pass (after the implementation but before merge) and were intentionally not chased:

| Item | Why | When to revisit |
|---|---|---|
| **Add `tenantId` + `tenantName` to `AuditLogDto` for the detail endpoint** | Phase 2 dropped the dead `tenantName` branch from `AuditMetadataCard` rather than wire it. Spec §5.2 still calls for "tenant (super-admin only)" in the metadata block. | When a super-admin user complains about needing the tenant chip during cross-tenant investigation, add `TenantId` + a tenant-name lookup. ~30 LOC across BE DTO + FE. |
| **JsonView nested-array reorder edge case** | The current viewer is read-only — there's no diff, so reorder isn't a concern unless we ever introduce a diff UX. | Only relevant if the deferred state-diff UX above ships. |
| **`useBackNavigation` cleanup** | The hook is `@deprecated` per its docstring (floating-glass shell removed the back-button UI). It still sets `useUIStore.backNavigation` but no component reads it. | Sweep all call sites and delete the hook + the `useUIStore.backNavigation` slice in a small follow-up PR (separate from feature work). |

### Test app caveat

The `_testJ4visual` test app was rename'd from a version of the boilerplate that **predates** the agent attribution feature (Plan 5d-1, PR #26). Its `AuditLog` entity has no `OnBehalfOfUserId` / `AgentPrincipalId` / `AgentRunId` columns, so its test-app handlers project `null` for those fields. This is fine — no audit rows in test data have agent attribution anyway, so visible behavior is unchanged. If you regenerate the test app from current `main` (post Plan 5d-1 merge), the projection should be reverted to use the real fields (just copy the source-tree handler over).

---

## 3. How to start the next session

A future Claude session (or human) joining this codebase should:

1. **Read in this order (≈30 minutes total):**
   - `CLAUDE.md` (project root) — overall conventions + Frontend Rules
   - [`2026-04-26-phase-0-visual-foundation-design.md`](2026-04-26-phase-0-visual-foundation-design.md) — design vocabulary (tokens, components, layouts)
   - [`2026-04-27-redesign-phase-1-design.md`](2026-04-27-redesign-phase-1-design.md) — sidebar IA + Identity cluster patterns
   - [`2026-04-28-redesign-phase-2-design.md`](2026-04-28-redesign-phase-2-design.md) — Platform admin patterns (timeline hero, restraint vs hero metric, secret reveal)
   - This doc — current status + what's next

2. **Pick the next phase** based on the deferred table above. **Phase 3 = Data cluster** is the recommended next step.

3. **Phase 3 — Data cluster (recommended next):**
   - Pages: `files` / `reports` / `notifications` (3 features, all flat list pages with no detail routes today).
   - Approach (mirrors Phase 1 + Phase 2 cadence):
     - Run `superpowers:brainstorming` to align on per-page character. Open question: do reports get a "request status" hero (queued / processing / failed counts) or stay restrained like API keys?
     - Files page candidates: storage-used hero strip; thumbnail grid view for image files; bulk delete UX.
     - Reports page candidates: status cards (queued / processing / completed / failed); inline retry on failure; download link freshness indicator.
     - Notifications page candidates: timeline-style grouping by date; unread/read toggle; preference quick-settings entry point.
     - Write a Phase 3 spec only if new visual decisions emerge; otherwise go directly to a plan.
     - Execute via `superpowers:subagent-driven-development`, branch `fe/redesign-phase-3-views` off latest `main`.

4. **Workflow conventions (proven across Phase 0 → 2):**
   - Iterate via file-copy from source → test app (do NOT regenerate the test app per change).
   - Commits land directly on the working branch (no per-task feature branches within a plan).
   - Each plan does its own subagent-driven execution; reviews happen per-task.
   - Final `superpowers:code-reviewer` pass on the diff before push.
   - Branch from latest `origin/main` after each phase ships, not from the previous phase branch.

---

## 4. Open questions for the next session

- **`/styleguide` access in production?** Carried from Phase 0 + 1. Still dev-only. Revisit if a public design system page is ever wanted.
- **Custom tenant branding extending to spectrum?** Today only `--primary` overrides per tenant; the tri-color spectrum (copper / emerald / violet) is fixed.
- **Marketing copy localization** for the landing — still hardcoded English.
- **Audit logs detail — tenant chip for super-admins.** Phase 2 dropped the dead branch; if super-admin investigation flows need it, the BE DTO change is small.

---

## 5. Phase 0 → Phase 2 cumulative shape

| Phase | Branch | Scope | Status |
|---|---|---|---|
| Phase 0 | `fe/base` | J4 Spectrum visual foundation, tokens, primitives, layouts, landing, auth, dashboard | Shipped (PR #27) |
| Phase 1 | `fe/redesign-phase-1-views` | Sidebar IA, command palette, Identity cluster polish | Shipped (PRs #28, #29, #31) |
| Phase 2 | `fe/redesign-phase-2-views` | Platform admin cluster polish, AuditLogDetailPage, BE detail endpoint | **Shipped (this PR)** |
| Phase 3 | _next_ | Data cluster (`files`, `reports`, `notifications`) | Recommended next |
| Phase 4 | _later_ | Commerce cluster (`billing`, `products`) | |
| Phase 5 | _later_ | Workflow & comms + Onboarding wizard | |
| Phase 6 | _later_ | AI module UI | |
| Phase 7 | _later_ | Mobile (Flutter) J4 port | |

---

## 6. Acknowledgements

Phase 2 followed the cadence proven in Phases 0 and 1: brainstorm → spec → plan → subagent-driven execution → review pass → fixes → ship. The implementation was carried out by a subagent and reviewed in this session; review fixes addressed three blockers (missing i18n key, broken non-Boolean flag status pill, placeholder `tenantsOverridingCount`) and several IMPORTANT clean-code issues (helper deduplication, replacing local debounce with shared hook, semantic token migration, dropping dead props). The same cadence is the recommended approach for Phase 3+.
