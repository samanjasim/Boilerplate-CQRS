# Shell Visual Refresh — Floating-glass (Balanced)

**Created:** 2026-04-27
**Branch:** `fe/redesign-phase-1` (continues from Plan A)
**Predecessor:** Plan A (sidebar grouping + mobile drawer) shipped on this branch.
**Companion spec:** `docs/superpowers/specs/2026-04-27-redesign-phase-1-design.md` — this design **supersedes §4.3 (sidebar visual), §5 (header visual), §6 (PageHeader visual)** of that doc but keeps §4.1 (group structure) and §4.2 (data shape) intact (those shipped in Plan A).

## 1. Why this spec exists

Plan A delivered the right *information architecture* — modules grouped, drawer on mobile, accessible — but the user feedback after seeing the live result was: *"the layout of the sidebar and the whole shell is still ugly, not inviting, not futuristic, feels like an old design."*

The brainstorm settled on the **floating-glass / Balanced** direction. This spec captures the visual moves needed to make the shell feel premium without restructuring the IA.

## 2. The mood

> Same dashboard. Same nav structure. New chrome.

- **Floating chrome** — sidebar and header detach from the viewport edge. They sit as glass cards with margin around them. The aurora bleeds through the gaps, so the chrome reads as floating *over* a colored canvas rather than framing it.
- **Pill active state** — the active nav item becomes a copper-gradient pill with a soft halo. The current flat tinted rectangle goes away.
- **Gradient where it earns its keep** — page titles, hero stat numbers, the primary CTA, the active item. Everywhere else stays calm typography. Restraint is the discipline; gradient is the accent.
- **Search-first header** — the header centers a search bar with a `⌘K` keycap, not a bare Menu icon. Other controls (lang, theme, notifications, avatar) cluster to the right as compact pills.
- **Breadcrumbs above page title** — every page gets a tiny eyebrow trail. The current page is colored copper.
- **Stat tiles as glass** — KPI cards on dashboards are translucent glass with a gradient hero number and a delta indicator.

Reference mood: Vercel dashboard, newer Linear screens, Apple Vision Pro UI. Familiar pattern, but with depth and breathing room.

## 3. Scope

**In scope** (replaces prior visual treatment):

1. **Sidebar visual** — `Sidebar.tsx` chrome (the data shape and grouping from Plan A is unchanged).
2. **Header visual** — `Header.tsx` chrome including a search bar trigger that doubles as the `⌘K` palette opener.
3. **MainLayout content** — `<main>` padding adjusts for the floating chrome. Aurora visibility increases.
4. **PageHeader visual** — title typography, breadcrumbs, action row layout. (`tabs` prop still planned; visual matches the new system.)
5. **New tokens & utilities** in `src/styles/index.css`:
   - `.surface-floating` — extends `.surface-glass` with floating-card shadow + inset top highlight.
   - `.pill-active` — gradient + halo + inset border for the active nav item.
   - `--floating-shadow`, `--floating-highlight` CSS vars.
6. **Stat-card shared component** — extract the dashboard's stat tile into `@/components/common/StatCard.tsx` so the same look reuses on Identity cluster pages.

**Out of scope** (keep as-is or defer):

- Sidebar grouping, mobile drawer behavior, icon set — Plan A locked these.
- Aurora keyframe animations — Phase 0 already ships them; we're using the existing tokens.
- Public landing page (`/`) — keep its existing aurora-heavy treatment unchanged.
- Auth pages — Phase 0 already polished these.
- Identity cluster page polish — separate plan, but uses these new tokens / `StatCard`.
- AI module UI, Mobile (Flutter) port — later phases.

## 4. Sidebar — floating-glass treatment

**Geometry:**
- Margin around the sidebar card: `14px` on top / bottom / start. (End edge sits flush; the radius does the visual separation.)
- Border-radius: `18px` (vs. the current edge-to-edge `border-r`).
- Width: unchanged — `w-60` expanded, `lg:w-16` collapsed.
- The `<aside>` switches from `fixed top-0` to `fixed top-3.5 bottom-3.5 ltr:left-3.5 rtl:right-3.5` (3.5 ≈ 14 px in Tailwind's spacing scale).

**Surface:**
- New utility class `.surface-floating` — extends `surface-glass` with:
  ```css
  box-shadow: var(--floating-shadow);  /* 0 10px 36px rgba(0,0,0,0.45) */
  background-color: rgb(from var(--surface-glass) r g b / 0.65);  /* slightly more opaque than landing's surface-glass */
  ```
- Inset top highlight: `box-shadow: inset 0 1px 0 rgb(255 255 255 / 0.06)` — gives the chrome a "pane of glass" feel.

**Active item — the pill:**
- New utility class `.pill-active`:
  ```css
  background: linear-gradient(135deg, rgb(from var(--primary) r g b / 0.22), rgb(from var(--primary) r g b / 0.10));
  color: var(--active-text);
  box-shadow:
    0 0 22px rgb(from var(--primary) r g b / 0.18),  /* halo */
    inset 0 0 0 1px rgb(from var(--primary) r g b / 0.30);  /* gradient border */
  ```
- Replaces the current flat `state-active` for nav items only. (`state-active` stays as-is for buttons / chips that already use it.)
- `border-radius` on nav items goes from `rounded-lg` (8 px) to `rounded-[10px]` to match the pill aesthetic without restyling the existing button radius.

**Active icon:**
- Keep the existing `drop-shadow-[0_0_6px_color-mix(...)]` glow on the icon when active. The pill background reinforces it.

**Group eyebrow labels:**
- Add a 4 × 4 px copper dot prefix:
  ```html
  <span className="text-[10px] uppercase tracking-[0.08em] text-muted-foreground font-semibold">
    <span className="inline-block w-1 h-1 rounded-full bg-primary/70 me-1.5 align-middle" />
    {label}
  </span>
  ```
- Padding tightens slightly: `px-3 pt-3 pb-1.5` → `px-3 pt-3 pb-1`.

**Group dividers:**
- Removed in expanded mode. The eyebrow + spacing alone marks the boundary now (the dot prefix is the visual cue). Less visual noise.
- Collapsed mode keeps its `mx-3 my-2 border-t border-border/40` separator (still needed when labels are hidden).

**Nav item count badge** (e.g., Task Inbox):
- Already uses `btn-primary-gradient glow-primary-sm`. Keep as-is — already on-brand.

**Logo block:**
- Tenant logo / app name unchanged.
- The brand dot gets a slightly larger size (`h-9 w-9` from `h-8 w-8`) and a subtle `glow-primary-md` (currently `glow-primary-sm`).
- Add a `text-[11px] text-muted-foreground` line below the app name showing the tenant slug *or* "Workspace" if super-admin. (Optional polish — see Open Questions.)

## 5. Header — search-first floating chrome

**Geometry:**
- Floating glass card matching the sidebar: `fixed top-3.5 ltr:right-3.5 rtl:left-3.5`.
- Left edge offsets to match the sidebar's right edge + 14 px gap: `ltr:left-[calc(15rem+1.75rem)]` expanded, `lg:ltr:left-[calc(4rem+1.75rem)]` collapsed (matching the `w-60` / `lg:w-16` widths plus the 14px margin), and `<lg` it goes flush start (`max-lg:ltr:left-3.5`).
- Height: `48px` (currently `56px` — a touch tighter, headers don't need that much vertical space when they're free-floating).
- Border-radius: `16px`.
- Surface: `.surface-floating`, same as sidebar.

**Content layout (LTR; mirrors for RTL):**

```
[ search-bar  ⌘K  ] [ flex-1 ]  [ EN ] [ ☾ ] [ 🔔 ] [ avatar pill ]
```

- **Mobile menu button** disappears entirely. The `<lg` drawer is now triggered by **clicking the search bar** (search bar at `<lg` shows a hamburger icon prefix and the `⌘K` chip is hidden). Single button doing two jobs: open palette on desktop, open drawer on mobile.

  *Reasoning:* the user feedback called out the bare Menu icon as a "old admin shell" pattern. Folding it into the search bar makes the header chrome feel intentional. The desktop search and mobile menu have the same affordance — a glass pill with a leading icon.

- **Back link** (when present via `useBackNavigation`) — moves to a separate row above the search bar OR collapses into the breadcrumbs trail. **Decision:** breadcrumbs trail wins; `useBackNavigation` becomes deprecated. We use the new `PageHeader.breadcrumbs` prop instead. (Migration: existing call sites that use `useBackNavigation` get audited and converted to `breadcrumbs`. See §11.)

- **Search bar** — `350px` max-width, fills `flex-1` on narrower viewports.
  ```
  ┌─────────────────────────────────────────┐
  │  ⌕  Search anything…           ⌘K       │
  └─────────────────────────────────────────┘
  ```
  - Background: `bg-white/4` (`rgba(255,255,255,0.04)` in dark, with a light-mode inverse).
  - Border: `border border-white/8`.
  - Radius: `9px`.
  - The `⌘K` keycap is a `text-[10px] font-mono` chip in `bg-white/6 border border-white/12 rounded-[5px]`.
  - Click → opens the command palette (Plan B's deliverable, now visually unified with the trigger).

- **Right-cluster controls** — `LanguageSwitcher`, `ThemeToggle`, `NotificationBell` each become `28×28` glass pills (`rounded-lg bg-white/4 border border-white/6`).

- **Avatar pill** — `padding: 4px 10px 4px 4px`, `rounded-full`, `bg-white/5 border border-white/8`. Avatar circle on the start, name on the end.
  - Click → existing dropdown (Profile, Logout) — unchanged.

## 6. MainLayout — content sits on the aurora

**Outer wrapper:**
- Stays `aurora-canvas overflow-x-clip`.
- Aurora intensity bumps slightly — change `[data-page-style="dense"]` to use the *full* aurora (`var(--aurora-corner)` already shipped) but increase its opacity by ~30 % so it actually shows through the gaps.

**`<main>` padding:**
- `<lg`: `pt-[64px] px-3.5` (header height 48 + top margin 14 = 62 → 64 px; horizontal margin matches drawer gap).
- `lg+`: `pt-[64px] ltr:pl-[calc(15rem+1.75rem)] lg:rtl:pr-[calc(15rem+1.75rem)] pe-3.5` expanded; collapse swaps to `calc(4rem+1.75rem)`.
- Inner content padding: `p-6` (was `p-8`) — content sits closer to the chrome since the chrome already has its own breathing room.

**Backdrop (mobile drawer):**
- Same as Plan A — no change needed.

## 7. PageHeader — typography first, no card

**The card wrapper goes away.** PageHeader becomes pure typography on the aurora. The floating sidebar + header are the chrome; PageHeader is content metadata.

**API additions** (still backwards-compat):

```ts
interface PageHeaderProps {
  title: string;
  subtitle?: string;
  actions?: ReactNode;
  breadcrumbs?: { label: string; to?: string }[];   // NEW
  tabs?: { label: string; to: string; count?: number }[]; // NEW
}
```

**Layout (LTR; mirrors RTL):**

```
{breadcrumbs (eyebrow row)}
{title (gradient, large, thin)}                         {actions (right-aligned)}
{subtitle (muted, small)}
{tabs (border-b row, full-bleed)}
```

**Visual:**

- **Breadcrumbs row:**
  - `text-sm text-muted-foreground` for non-last entries.
  - Last entry: `text-primary font-medium` (current page in copper).
  - Separator: `›` glyph at `text-xs opacity-50`.
  - When `breadcrumbs` is omitted, the row doesn't render — no empty space.

- **Title:**
  - Font size: `text-3xl lg:text-[32px]` (currently `text-2xl`).
  - Weight: `font-extralight` (`200`).
  - Tracking: `tracking-tight` (currently default).
  - **Gradient:** `bg-clip-text text-transparent bg-[linear-gradient(95deg,_var(--foreground)_30%,_var(--primary)_100%)]`. Falls back to plain `text-foreground` when `prefers-reduced-motion` (since the gradient doesn't animate, this is a polish-only fallback — drop if it complicates the implementation).

- **Subtitle:** unchanged — `text-sm text-muted-foreground`.

- **Actions row:** unchanged behavior. On `lg+` actions sit on the same row as the title (right-aligned). On `<lg` they wrap to a new row below the title.

- **Tabs row:**
  - Renders below subtitle when `tabs?.length > 0`.
  - Layout: horizontal flex with `border-b border-border/40 -mx-6 px-6 mt-5` (full-bleed inside the standard `p-6` content padding).
  - Each tab: `NavLink` with `py-2.5 px-4`. Active: `border-b-2 border-primary text-foreground`; inactive: `text-muted-foreground hover:text-foreground`.
  - Optional `count`: bubble after label — `ml-2 rounded-full bg-secondary px-2 py-0.5 text-xs`.

**Backwards compat:** when neither `breadcrumbs` nor `tabs` is provided, the rendered HTML is identical to today's PageHeader minus the card wrapper.

## 8. Stat cards — extract to `@/components/common/StatCard.tsx`

The dashboard's hero metric strip uses bespoke markup today. We extract it so the same component renders on `UsersListPage`, `RolesListPage`, `TenantsListPage`, etc. (Identity cluster polish in later plans).

**API:**

```ts
interface StatCardProps {
  label: string;                    // ALL CAPS rendered automatically
  value: string | number;
  hero?: boolean;                   // applies the gradient on the value
  delta?: { value: string; trend?: 'up' | 'down' | 'flat' };
  status?: 'healthy' | 'warning' | 'error' | 'info';  // optional pulse-dot accent
  icon?: LucideIcon;                // optional small icon top-left
}
```

**Visual:**
- Container: `rounded-2xl border border-border/40 bg-[linear-gradient(150deg,_var(--surface-glass)_50%,_transparent)] backdrop-blur-md p-6`.
- Label: `text-[10px] uppercase tracking-[0.12em] font-semibold text-muted-foreground`.
- Value (default): `text-3xl font-extralight tracking-tight text-foreground`.
- Value (hero): same + `bg-clip-text text-transparent bg-[linear-gradient(135deg,_var(--foreground),_var(--primary))]`.
- Delta: `text-xs mt-1` with `text-success` (up) / `text-destructive` (down) / `text-muted-foreground` (flat) and a chevron glyph prefix.
- Status: tiny `pulse-dot` (Phase 0 utility) at the right edge.

**Migration:** `DashboardPage` is the first consumer; it stops using its bespoke markup and starts using `StatCard`. Identity cluster pages will adopt it in their own plan.

## 8.5 Light mode

Every visual decision in this spec works in both light and dark — only the surface tones flip via the existing J4 token system. Concretely:

| Aspect | Dark | Light |
|---|---|---|
| Glass card surface | `rgba(20,20,24,0.55)` | `rgba(255,255,255,0.72)` |
| Card border | `rgba(255,255,255,0.09)` | `rgba(0,0,0,0.07)` |
| Floating shadow | `0 10px 36px rgba(0,0,0,0.45)` | `0 8px 28px rgba(0,0,0,0.10)` |
| Inset highlight | `inset 0 1px 0 rgba(255,255,255,0.06)` | `inset 0 1px 0 rgba(255,255,255,0.6)` |
| Title gradient | `linear-gradient(95deg, #fff 30%, var(--primary) 100%)` | `linear-gradient(95deg, var(--foreground) 30%, var(--primary) 100%)` |
| Aurora intensity | copper at 20% + violet at 13% | copper at 16% + violet at 10% |
| Pill active border | `rgba(primary, 0.30)` | `rgba(primary, 0.32)` (slightly higher to compensate for lighter background) |

The brand copper stays vivid in both modes — the gradient on the active pill, the page title, the primary CTA, and the hero stat all use `var(--primary)` directly, which doesn't wash out in light mode.

`--surface-glass`, `--border-strong`, `--primary`, `--foreground`, `--aurora-corner` already flip via Phase 0's `useThemePreset` runtime + `@media (prefers-color-scheme)` blocks. The new `--floating-shadow` and `--floating-highlight` vars defined in §9 follow the same pattern.

**Verification:** Plan B-v2's visual tasks must pass in both modes (toggle via the existing `ThemeToggle`).

## 9. New tokens & utilities (`src/styles/index.css`)

Additions, not replacements:

```css
:root {
  --floating-shadow: 0 10px 36px rgb(0 0 0 / 0.45);
  --floating-highlight: inset 0 1px 0 rgb(255 255 255 / 0.06);
}

@media (prefers-color-scheme: light) {
  :root {
    --floating-shadow: 0 8px 28px rgb(0 0 0 / 0.10);
    --floating-highlight: inset 0 1px 0 rgb(255 255 255 / 0.6);
  }
}
```

```css
.surface-floating {
  background-color: var(--surface-glass);
  border: 1px solid var(--border-strong);
  backdrop-filter: blur(20px);
  -webkit-backdrop-filter: blur(20px);
  box-shadow: var(--floating-shadow), var(--floating-highlight);
}

.pill-active {
  background: linear-gradient(135deg,
    color-mix(in srgb, var(--color-primary) 22%, transparent),
    color-mix(in srgb, var(--color-primary) 10%, transparent));
  color: var(--active-text);
  box-shadow:
    0 0 22px color-mix(in srgb, var(--color-primary) 18%, transparent),
    inset 0 0 0 1px color-mix(in srgb, var(--color-primary) 30%, transparent);
}
```

The existing `state-active` keeps its current behavior (used by buttons, badges, etc.). The new `pill-active` is sidebar-nav-only.

## 10. Aurora intensity bump

In `src/styles/index.css`, find `[data-page-style="dense"].aurora-canvas::before` (currently uses `var(--aurora-corner)` with `filter: none`). Adjust:

- Change `background` to layer two blooms — copper + violet:
  ```css
  background:
    radial-gradient(ellipse 60% 80% at 80% 10%, color-mix(in srgb, var(--color-primary) 18%, transparent), transparent 65%),
    radial-gradient(ellipse 70% 55% at 12% 90%, color-mix(in srgb, var(--color-violet-500) 13%, transparent), transparent 70%);
  ```
- Drop `filter: none` (let the parent's blur apply).

This makes the aurora visible *through the gap between sidebar and header* without overwhelming the data on dense pages.

## 11. Migration — `useBackNavigation` deprecation

The current `useBackNavigation(path, label)` hook renders an `ArrowLeft + label` link in the Header. With breadcrumbs landing in PageHeader, the header back-link becomes redundant.

- **Mark `useBackNavigation` as deprecated** in `src/hooks/index.ts` (JSDoc `@deprecated`, point to PageHeader.breadcrumbs).
- **Header stops rendering it** — the back-link element is removed in this refresh.
- **Audit + migrate call sites** — every page that currently calls `useBackNavigation(path, label)` now passes a matching `breadcrumbs={[{ to: path, label }, { label: currentPageLabel }]}` to PageHeader.
- **Remove the hook** in a follow-up plan once all call sites migrate. Don't delete in this spec to avoid blocking.

This migration affects ~6 pages (typically detail / edit pages). Each is a 1-line swap.

## 12. Implementation order

This spec executes as a focused 5-task plan on the same `fe/redesign-phase-1` branch:

1. **Plan B-v2 — Tokens & utilities + StatCard** — adds `surface-floating`, `pill-active`, the floating-shadow vars, and extracts `StatCard`. Sidebar / Header / MainLayout / PageHeader still use their old visuals; only `DashboardPage` migrates to `StatCard` to validate the component.
2. **Plan B-v2 — Sidebar visual** — apply `.surface-floating`, pill active state, floating geometry. Mobile drawer translate logic adapts to the new `top: 14px` offset.
3. **Plan B-v2 — Header & MainLayout** — floating header, search bar trigger, content padding, aurora bump. Removes the back-link rendering. (Command palette behavior still deferred to its own plan — this just lands the *trigger* visual.)
4. **Plan B-v2 — PageHeader & breadcrumbs migration** — drops the card wrapper, applies gradient title, adds `breadcrumbs` and `tabs` props, migrates all `useBackNavigation` call sites.
5. **Plan B-v2 — Code-review pass.**

(Plan B's ⌘K palette and Plans D/E Identity polish stay deferred — they reuse these new tokens but are separate execution units.)

## 13. Verification

Same harness as Plan A — `_testJ4visual` test app at `localhost:3100`. Each task ends with:
- `npm run build` + `npm run lint` clean.
- Visual pass on the dashboard, on a list page (`/users` or `/files`), on a detail page (`/users/<id>`), at desktop expanded, desktop collapsed, and `<lg` mobile drawer.
- RTL pass on at least one page.
- `prefers-reduced-motion: reduce` pass.

Visual targets:
- The chrome reads as floating, with aurora visible in the gaps.
- The active nav item is a copper-gradient pill, not a flat rectangle.
- The page title is a gradient (white → copper).
- Stat cards are translucent glass with delta indicators.
- The `⌘K` chip is visible in the search bar and the keystroke opens the palette (or, for now, focuses the search bar — palette content is its own plan).

## 14. Open questions

These don't block writing the plan; resolve before Plan B-v2 task 4 (PageHeader migration):

- **Tenant slug under brand name in sidebar?** Adds personality and tenant context; might be redundant if the active tenant is already shown in the avatar. Pick one location.
- **Avatar pill on `<lg`** — keep the name visible or shrink to icon-only? Probably icon-only on small viewports (already the existing pattern).
- **Search bar at `<lg`** — does it expand to fill the full header, or stay 280 px and let the right-side controls compress? Reasonable to expand it to `flex-1` on `<lg`.
- **`StatCard` skeleton state** — the J4 system has no `Skeleton` primitive in `@/components/ui` yet. For now `StatCard` accepts `loading?: boolean` and renders a pulsing placeholder; promote to a shared Skeleton primitive in Phase 2 if more consumers want it.

## 15. Out of scope (deferred)

- ⌘K palette content (the actual command list / keyboard handling) — its own plan, reuses the search bar trigger from this spec.
- Identity cluster page polish (Users / Roles / Tenants / Profile) — separate plan, will use `StatCard`, the new PageHeader, and breadcrumbs.
- AI module UI, mobile (Flutter) port, marketing site — later phases.
- AR + KU translations of any new strings introduced by this spec — English-only ships, fallback applies.
