# Phase 1 — Navigation, Layout Shell & Identity Cluster Polish

**Created:** 2026-04-27
**Branch:** `fe/redesign-phase-1` (off `origin/main`)
**Predecessor:** Phase 0 (J4 Spectrum visual foundation) — shipped as PR #27 (`74c9a6ad`).
**Successor (deferred — see §11):** Phase 2 — feature page polish for the remaining clusters; Phase 3 — AI module UI; Phase 4 — Mobile (Flutter) port.

## 1. Why this phase exists

Phase 0 delivered tokens, primitives, layouts, the landing page, the auth flow, and a redesigned dashboard. Every page that already used `Card`, `Button`, `Table`, `PageHeader`, or `EmptyState` inherited the J4 look automatically — but the **information architecture** (the sidebar) and the **page-level patterns** (Header chrome, PageHeader API, page container, mobile behavior) are still where Phase 0 left them: a flat 25-item nav list and an unopinionated content shell.

Phase 1 fixes the IA and the layout shell, then proves the new patterns by polishing the highest-traffic feature cluster (Identity: users / roles / tenants / profile / access). Future clusters reuse the same patterns without further IA churn.

## 2. Scope (in)

1. **Sidebar redesign** — module-first grouping with explicit group labels; updated visual treatment; updated collapsed-state behavior; new mobile drawer.
2. **Header evolution** — add a command palette (`⌘K` / `Ctrl+K`) for nav search; keep existing right-side chrome.
3. **PageHeader API additions** — optional `breadcrumbs` and `tabs` props (backwards-compatible).
4. **MainLayout responsive behavior** — sidebar becomes a slide-out drawer on `<lg`.
5. **Identity cluster polish** — apply the dashboard pattern (hero metric strip + glass content + sparkline cards) to:
   - `users` (UsersListPage, UserDetailPage)
   - `roles` (RolesListPage, RoleDetailPage, RoleCreatePage, RoleEditPage)
   - `tenants` (TenantsListPage super-admin view, Organization tenant view, TenantDetailPage)
   - `profile` (ProfilePage)
   - `access` (light polish only — confirm tokens, no major restructure)

## 3. Out of scope (deferred — picked up in later phases)

Listed here so a future session can pick them up without re-reviewing this work. Each item has a clear next-phase owner.

| Deferred item | Picked up in |
|---|---|
| Per-feature polish for **Platform admin** cluster (`settings`, `feature-flags`, `api-keys`, `audit-logs`) | Phase 2 |
| Per-feature polish for **Data** cluster (`files`, `reports`, `notifications`) | Phase 2 |
| Per-feature polish for **Commerce** cluster (`billing`, `products`) | Phase 2 |
| Per-feature polish for **Workflow & comms** cluster (`workflow`, `communication`, `comments-activity`, `import-export`, `webhooks`) | Phase 2 |
| Per-feature polish for **Onboarding** wizard | Phase 2 |
| **AI module UI** (chat surfaces, persona×role admin, agent template browser, RAG ingestion, RAG eval dashboards, public widget config) | Phase 3 |
| **Mobile (Flutter) J4 port** | Phase 4 |
| Marketing site / public docs portal | Optional Phase X |
| **AR + KU translations** for Phase 0 keys (`dashboard.*`, `auth_chrome.*`) and the new `nav.groups.*` keys | English keys land in Phase 1; AR + KU translations follow when a localizer is available (i18next falls back to EN — does not block merge) |
| `--violet-tinted-fg` and `--accent-tinted-fg` semantic tokens (drop `dark:` overrides on `AiSection` / `PersonasPreview`) | Phase 3 (AI module work — those are AI-section components) |
| Consolidate `useCountUp` (currently three implementations) | Extract once a fourth consumer appears in this phase or later; not a blocker |
| `HeroSection` `setTimeout` choreography → `useReducer` | Cosmetic refactor; not a blocker |
| `AiSection` capability list i18n migration | Phase 3 |
| Bottom-nav / mobile-first deep redesign | Phase 4 (Mobile port) — web stays drawer-only |
| Command palette **content beyond nav routes** (recent records, semantic search, assistant queries) | Phase 3 (AI module deep integration) |
| `/styleguide` access in production | Open question — not decided in Phase 1 |

## 4. Sidebar redesign

### 4.1 Group structure

Strict modules-first reading: every optional module gets its own labeled group, and core features split into "People", "Content", and "Platform" relations. Single-item groups are intentional — they preserve the mental model that each module owns a top-level section, and they age well as modules grow.

Top-level (no label, never empty):

- Dashboard
- Notifications

Then labeled groups in order:

| Group key | Label | Items | Permission / module gate |
|---|---|---|---|
| `workflow` | `nav.groups.workflow` → "Workflow" | Task Inbox · History · Definitions | `activeModules.workflow` + `Workflows.View` (Definitions also requires `Workflows.ManageDefinitions`) |
| `communication` | `nav.groups.communication` → "Communication" | Channels · Templates · Trigger Rules · Integrations · Delivery Log | `activeModules.communication` + `Communication.View` (Delivery Log also requires `Communication.ViewDeliveryLog`); requires `user.tenantId` |
| `products` | `nav.groups.products` → "Products" | Products | `activeModules.products` + `Products.View` |
| `billing` | `nav.groups.billing` → "Billing" | Billing · Plans · Subscriptions | `activeModules.billing` + `Billing.View` (Plans needs `Billing.ViewPlans`, Subscriptions needs `Billing.ManageTenantSubscriptions`); Billing requires `user.tenantId` |
| `webhooks` | `nav.groups.webhooks` → "Webhooks" | Webhooks (tenant) | `activeModules.webhooks` + `Webhooks.View` + `user.tenantId` + `webhooksFlag.isEnabled` |
| `importExport` | `nav.groups.importExport` → "Import / Export" | Import / Export | `activeModules.importExport` + (`System.ExportData` & `exportsFlag.isEnabled`) OR (`System.ImportData` & `importsFlag.isEnabled`) |
| `people` | `nav.groups.people` → "People" | Users · Roles · Organization (or Tenants for super-admin) | Per-item: `Users.View` · `Roles.View` · `Tenants.View` |
| `content` | `nav.groups.content` → "Content" | Files · Reports | Per-item: `Files.View` · `System.ExportData` |
| `platform` | `nav.groups.platform` → "Platform" | Audit Logs · API Keys · Feature Flags · Webhooks Admin · Settings | Per-item: `System.ViewAuditLogs` · `ApiKeys.View` · `FeatureFlags.View` · `Webhooks.ViewPlatform` · `System.ManageSettings` |

**Group hide rule:** if all of a group's items are filtered out by permissions / module gates / feature flags, the group label and divider also hide. Groups never render with zero visible items.

**Group order:** modules → core relations → platform admin. Inside a module group, command-frequency-first (Task Inbox before History before Definitions). Inside core relations, the order users naturally expect (Users → Roles → Organization).

### 4.2 Data shape

The current flat `navItems` array becomes a typed group-of-items structure, defined inline in `Sidebar.tsx`:

```ts
interface SidebarNavItem {
  label: string;            // already i18n'd
  icon: LucideIcon;
  path: string;
  end?: boolean;            // exact-match for NavLink (replaces hand-rolled `end={...}` switch)
  badge?: number;           // e.g., Task Inbox pending count
}

interface SidebarNavGroup {
  id: string;               // 'workflow', 'communication', etc. — used for keys + collapsed-state separator
  label?: string;           // i18n'd group label; absent for the top "Dashboard / Notifications" block
  items: SidebarNavItem[];
}
```

Building the array is one function (`buildNavGroups(...)`) inside the component, executed on every render so permission / flag / module gates evaluate fresh. The function returns `SidebarNavGroup[]`, with empty groups stripped before render. Keep this in `Sidebar.tsx` — extracting to a separate file adds an indirection without payoff.

### 4.3 Visual treatment — expanded (`w-60`)

- **Group label** — `<div>` rendered above each group's `<ul>`:
  - Classes: `px-3 pt-4 pb-1.5 text-[10px] font-medium uppercase tracking-[0.08em] text-muted-foreground`
  - First labeled group keeps `pt-2` (no double top padding above the divider).
- **Group divider** — `border-t border-border/40` on the `<div>` wrapping each labeled group except the first labeled group. The top "Dashboard / Notifications" block has no top divider.
- **Item rows** — unchanged from Phase 0 (`state-active` / `state-hover`, copper drop-shadow on the active icon, font-mono badge pill).

### 4.4 Visual treatment — collapsed (`w-16`)

- Group labels **hide entirely**.
- **Thin separator** between groups: `<div className="my-2 mx-3 border-t border-border/40" />`. Same separator stays between the top block and the first labeled group.
- Items render icon-only, centered (existing behavior).
- Active glow + badge dot still render on icons.

### 4.5 Mobile drawer (`<lg`, < 1024 px)

At viewports `<lg`, the sidebar leaves the layout flow and becomes a slide-out drawer:

- Default state: closed, no DOM weight on `<main>` left padding.
- Trigger: existing `Menu` button in the Header (`lg:hidden`). Toggling sets `useUIStore.isSidebarOpen` (new piece of state — replaces nothing; the existing `sidebarCollapsed` only governs desktop width).
- Open state: sidebar slides in from `start` edge (`ltr:left-0` / `rtl:right-0`), same `w-60`. A backdrop `<div className="fixed inset-0 z-30 bg-background/60 backdrop-blur-sm" onClick={close}>` covers the rest of the viewport.
- Close: backdrop click, route change (auto-close on `<NavLink>` activate), or Esc.
- Group labels stay visible inside the drawer. The drawer is always "expanded" — no collapsed mode on mobile.
- The desktop collapse toggle (`ChevronsLeft`) hides on `<lg`.
- The Header's left-side back-link area collapses on `<lg` (already does — `hidden lg:flex`).

### 4.6 i18n keys to add

Under `nav.groups` namespace in `en/translation.json` (and AR / KU best-effort):

```json
"nav": {
  "groups": {
    "workflow": "Workflow",
    "communication": "Communication",
    "products": "Products",
    "billing": "Billing",
    "webhooks": "Webhooks",
    "importExport": "Import / Export",
    "people": "People",
    "content": "Content",
    "platform": "Platform"
  }
}
```

No existing nav item keys change. The Sidebar's `webhooksAdmin` link reuses the existing `nav.webhooksAdmin` key.

### 4.7 What does NOT change

- Logo block (top of sidebar) — unchanged.
- Collapse / expand toggle button at the bottom on collapsed state — unchanged.
- Tenant logo + name fallback logic — unchanged.
- Permission / flag / module gating semantics for individual items — unchanged.

## 5. Header evolution

### 5.1 Right-side chrome — unchanged

Lang switcher → Theme toggle → NotificationBell → Avatar dropdown (Profile / Logout). Phase 0 already polished these.

### 5.2 Command palette (`⌘K` / `Ctrl+K`)

A new control sits to the **left** of the LanguageSwitcher (or wraps after the back-link area on small tablet widths). It's a 36 × 36 button with a `Search` icon and a faint kbd hint (`⌘K`) in expanded variant:

- Click or hotkey opens a command palette built on Radix `@radix-ui/react-dialog` (already present) + a controlled `Input` + filtered list. No new heavyweight dependency. Plan B may swap to `cmdk` if a fuzzy-search benefit emerges, but the default is to use what's already shipping.
- **Hotkey:** `mod+K` registered in `MainLayout` via a single `useEffect` listener on `document` (cleaned up on unmount). Skipped when focus is inside an input (matches VS Code / Linear behavior).
- **Content (Phase 1):** filtered list of all visible nav routes (built from the same `buildNavGroups()` call the sidebar uses). Each row shows: icon · label · group label (right-aligned, muted). Click → `navigate(path)` + close.
- **Empty state:** "Recent" header + last 5 routes from a tiny new `recentRoutes` slice on `useUIStore` (FIFO, deduped, persisted to localStorage). Capped at 5; no UI to manage them.
- **No content beyond nav routes** in Phase 1. Recent records, semantic search, assistant queries — Phase 3 (AI module).

Single new file: `src/components/layout/MainLayout/CommandPalette.tsx`. Wired into `Header.tsx` via a controlled state on `useUIStore` so the hotkey listener and the trigger button share state.

### 5.3 Back-link area — unchanged

Existing `useBackNavigation(path, label)` hook continues to render in the header on `lg+`.

## 6. PageHeader API additions

Backwards-compatible. Existing call sites continue to work.

```ts
interface PageHeaderProps {
  title: string;
  subtitle?: string;
  actions?: ReactNode;
  // NEW:
  breadcrumbs?: { label: string; to?: string }[];   // last entry usually has no `to` (current page)
  tabs?: { label: string; to: string; count?: number }[];
}
```

### 6.1 Breadcrumbs

Rendered above the title row. Only when `breadcrumbs?.length > 0`:

- Layout: horizontal flex, `gap-1.5`, `text-sm text-muted-foreground`.
- Separator: `ChevronRight` icon (`h-3 w-3 opacity-50`, `rtl:rotate-180`).
- Linked entries: `<Link>` with `hover:text-foreground transition-colors`.
- Final entry: plain text, `text-foreground`.
- Mobile: hides on `<sm` (drawer-style nav covers the back-stack).

### 6.2 Tabs

Rendered below the subtitle row, above the actions. Only when `tabs?.length > 0`:

- Layout: horizontal flex, `gap-1`, `border-b border-border/40 -mx-4 px-4 mt-4` (full-bleed within the PageHeader card).
- Each tab: `NavLink` with `end={false}`, padding `py-2.5 px-4`, active state has `border-b-2 border-primary text-foreground` and inactive has `text-muted-foreground hover:text-foreground`.
- Optional `count`: small bubble after the label (`ml-2 rounded-full bg-secondary px-2 py-0.5 text-xs`).
- The tabs row is sticky-friendly (no transforms), so a parent `sticky top-14` works without surprises.

Backwards-compat: when neither prop is set, the rendered HTML is identical to today's PageHeader (no extra empty divs).

## 7. MainLayout — content container

**No change** to padding or max-width. `pt-16 pl-60 p-8` stays. Tables and dashboards already self-constrain when needed; pages that want narrower content should wrap their own content in a `max-w-*` container, not the shell. Adding a global max-width now would force a redesign of every wide page.

The mobile drawer state lives at `useUIStore.isSidebarOpen` (new), distinct from the existing `sidebarCollapsed`. `MainLayout` renders the existing `<Sidebar />` component — the responsive class swap (`fixed lg:static`, etc.) lives inside `Sidebar.tsx`.

## 8. Identity cluster polish

The Phase 0 dashboard pattern provides the vocabulary:

- **Hero metric strip** — gradient-text headline number + 3-4 sparkline stat cards
- **Glass content surface** — `<Card variant="glass">` wrapping the main work area
- **Activity feed / hover lifts** — applied where signal warrants

Per page, what changes:

### 8.1 `UsersListPage`

Hero strip (above the table):

- **Total users** (hero metric, gradient-text, count-up animation on mount)
- **Active** (sparkline of last-30-day login activity if cheap; otherwise just count)
- **Pending invitations** (count + warn-tinted card if > 0)
- **New this month** (delta indicator + sparkline)

Below the strip:

- Existing pending-invitations table — wrapped in `Card variant="glass"` if that's not already the case after Phase 0; otherwise unchanged.
- Existing user table — already glass after Phase 0; confirm.
- Row avatar uses `UserAvatar` + status pill (J4 Badge variant: `healthy` / `pending` / `failed`).

### 8.2 `UserDetailPage`

- New **header card** — large `UserAvatar` (`size="xl"`), name + email, role pills, status pill, last-login timestamp, "Member since" date.
- The existing tabs (Profile, Sessions, Roles, Audit) become `PageHeader.tabs`.
- Each tab's content sits in `Card variant="glass"`.

### 8.3 `RolesListPage`

Hero strip:

- **Total roles**
- **Permissions enabled** (count across all roles + percentage)
- **System roles** vs **Custom roles** split

Below:

- Grid of role **cards** (`Card variant="elevated"`) instead of table rows. Each card shows: role name (gradient-text accent for Admin / SuperAdmin), assigned permission count, member count, "Edit" CTA.
- Empty state already uses `EmptyState`; confirm icon + copy.

### 8.4 `RoleDetailPage` / `RoleCreatePage` / `RoleEditPage`

- **Permission matrix** is the hero. Wrap in `Card variant="glass"`.
- Module headers (e.g., `Users`, `Roles`, `Tenants`) get the small `nav.groups.*` styling — eyebrow uppercase label.
- Permissions inside each module: existing checkbox grid; J4 padding/spacing pass.
- Header for Detail page: breadcrumbs `Roles → {role name}`.

### 8.5 `TenantsListPage` (super-admin) / `Organization` (tenant view) / `TenantDetailPage`

`TenantsListPage`:

- Hero strip — Total tenants · Active · Suspended · New this month.
- Tenant **cards** (`Card variant="elevated"`) with logo thumbnail, name, slug, status pill, member count.

`Organization` (single-tenant admin view at `ROUTES.ORGANIZATION`):

- Single hero card — large logo (with spectrum gradient frame), tenant name (gradient-text), member count, status pill.
- Tabs (via `PageHeader.tabs`): **People** · **Settings** · **Branding**.
  - **People** — embedded user list (or link to `UsersListPage` filtered to current tenant — pick one in plan).
  - **Settings** — existing tenant settings form, glass-wrapped.
  - **Branding** — existing branding form, glass-wrapped.

`TenantDetailPage` (super-admin viewing a specific tenant):

- Same hero card pattern + `breadcrumbs: Tenants → {name}`.
- Same three tabs.

### 8.6 `ProfilePage`

- Hero card — large avatar, full name (gradient-text), email, status, "Member since".
- Below: 4 grid cards (`Card variant="elevated"` or default — pick one consistently in plan):
  - **Account** — first/last name, email, phone, language, theme.
  - **Security** — password change, 2FA toggle.
  - **Sessions** — list of active sessions with revoke action.
  - **Login history** — last N entries.
- No tabs — sections sit on the page in a 2-column grid (`md:grid-cols-2`), single column on mobile.

### 8.7 `access`

Light polish only:

- Confirm `access`'s overview page uses `PageHeader` + `Card variant="glass"`.
- Confirm any tag/pill UI uses J4 Badge variants.
- No restructure.

## 9. Implementation order

This spec is for a single branch (`fe/redesign-phase-1`). The branch will execute as five plans, in order. Each plan ends with a working test app and a small visual validation pass; Identity cluster pages can land incrementally without reverting the layout shell.

1. **Plan A — Sidebar redesign** (§4). Standalone, testable end-to-end. Mobile drawer is part of this plan.
2. **Plan B — Header command palette** (§5.2). Independent of Plan A.
3. **Plan C — PageHeader props + i18n keys** (§6, §4.6). Breadcrumbs and tabs land first as primitives; consumers in later plans.
4. **Plan D — Identity cluster, list pages** (§8.1, §8.3, §8.5). Uses Plans A–C.
5. **Plan E — Identity cluster, detail / create / edit pages** (§8.2, §8.4, §8.5 detail, §8.6, §8.7). Uses Plan C tabs + breadcrumbs.

Plans A, B, C may execute in parallel via `superpowers:dispatching-parallel-agents` if branch ergonomics allow. Plans D and E are sequential.

Each plan ends with:

- `npm run build` passes
- Test app at `_testJ4visual` (or its successor) restarted and visually validated via Chrome DevTools / Playwright MCP
- Code-review pass via `superpowers:requesting-code-review`

## 10. Verification

The Phase 0 test-app workflow (`.claude/skills/post-feature-testing.md`) is the verification harness. The test app at `_testJ4visual/` from Phase 0 may still exist; otherwise re-spin it with the same name and BE 5100 / FE 3100 ports. Source-to-test-app file copy is the iteration loop — do **not** regenerate the test app per change.

Visual targets:

- `/` — landing (no regression expected; not in scope)
- `/dashboard` — no regression
- `/users` — Identity polish visible
- `/roles` — Identity polish visible
- `/tenants` or `/organization` — Identity polish visible
- `/profile` — Identity polish visible
- Any page when `⌘K` is pressed — palette opens
- Any page on a `<lg` viewport — sidebar drawer opens via Menu button, closes on backdrop click and on route change
- Any page with `breadcrumbs` or `tabs` props — render is correct, RTL mirrors

## 11. Open questions

These are intentionally not answered in this spec; they need a decision before Plan E ships:

- **Profile page card variant** — `elevated` or default? (`elevated` adds hover lift; user profile sections aren't typically clicked on hover. Default may be calmer.)
- **`Organization` "People" tab** — embed `UsersListPage` table inline, or link out to `/users` filtered by current tenant? (Inline is heavier; link-out is two clicks but cleaner separation.)
- **Tenant logo "spectrum gradient frame"** — exact treatment (border, halo, both?). To be sketched in Plan E.
- **`/styleguide` access in production** — still open from Phase 0. Not blocking Phase 1.

## 12. Appendix — references

- Phase 0 design spec: `docs/superpowers/specs/2026-04-26-phase-0-visual-foundation-design.md`
- Phase 0 status & roadmap: `docs/superpowers/specs/2026-04-27-post-phase-0-status-and-roadmap.md`
- Phase 0 plans: `docs/superpowers/plans/2026-04-27-{visual-foundation-tokens,component-restyle-foundation,component-restyle-composite,layouts-and-landing}.md`
- Project conventions: `CLAUDE.md` § "Frontend Rules — Must Always Follow"
- J4 utilities reference: `/styleguide` (dev-only)
