# Phase 0 — Visual Foundation Design (J4 Spectrum)

**Status:** Draft
**Owner:** Saman Jasim
**Date:** 2026-04-26
**Phase position:** Phase 0 of a multi-phase frontend redesign. Locks the visual language and the design-system token surface that Phase 1 (Shell + Landing), Phase 2 (Shared components), and Phase 3 (Feature pages) will inherit.

---

## 1. Goal & non-goals

### Goal

Lock a single, coherent visual language — **J4 Spectrum** — and the token system that drives it, then build a **Style Reference page** that renders every primitive, every shared component, and every functional UI element (language switcher, theme toggle, notification bell, command-K, avatar menu, breadcrumbs, pagination, etc.) in that language. After Phase 0 ships, every later page redesign is *applying* this system, not inventing it.

### In scope

- Token system (colors, gradients, radii, shadows, typography) — light and dark, both modes shipped together.
- Restated rules for every shadcn/ui primitive (`button`, `input`, `card`, `dialog`, `table`, `badge`, `select`, `dropdown-menu`, `popover`, `tabs`, `checkbox`, `textarea`, `avatar`, `separator`, `spinner`, `sonner`).
- Restated rules for every component in `@/components/common` (22 components).
- Functional UI patterns: header, sidebar, command-K search, language switcher, theme toggle, notification bell, breadcrumbs, back navigation.
- Landing page section blueprint (hero, tech strip, feature grid, code preview, architecture, stats, footer CTA).
- AuthLayout, MainLayout, PublicLayout shells.
- A live, navigable Style Reference page at `/styleguide` — **development builds only** (`import.meta.env.DEV`). Not bundled into production builds.
- RTL behavior (Arabic) and accessibility rules (contrast, focus, reduced motion).

### Out of scope

- Per-feature page redesigns (those are Phase 3).
- Mobile (Flutter) — separate spec when we get there. Phase 0 only locks **frontend** visual language.
- Backend changes (none required).
- New copy/content — using existing strings; copy revision is a separate task.
- Marketing site beyond the landing route.
- Brand mark / logo redesign — keeping the existing `S` mark.

### Non-goal: density vs density

The earlier brief was "dense + technical + trustworthy." The chosen direction (J4 Spectrum) **trades raw information density for emotional density** — gradients, glass surfaces, gradient-text moments. Density returns through the *table layer* (compact rows, mono numerics, hairline separators) — but the chrome is generous. If we later want a "compact mode" toggle, that's a future variation; this spec ships the airy default only.

---

## 2. Visual language — J4 Spectrum DNA

What makes the language recognizable from across the room:

1. **Three-axis aurora backdrop.** Three large, blurred, elliptical radial gradients on the canvas — emerald top-left, copper top-right, violet bottom — bleeding under content. Visible on hero/landing/marketing surfaces; reduced to a single corner bloom on dense list pages.
2. **Spectrum gradient on key text.** Headlines have **one** gradient-text accent per page (verb or proper noun). The gradient runs `copper → amber → indigo` (or `94522e → c67a52 → 4338ca` light / `f0ae81 → c67a52 → a5b4fc` dark). Used sparingly — never on body text.
3. **Glassmorphic surfaces.** Cards, popovers, dropdowns, and the header are translucent (4–8% white in dark, 60–70% white in light) with `backdrop-filter: blur(16-24px)` and a hairline border. They visually float above the aurora.
4. **Pulsing live signal.** A 6×6 dot with a soft outer glow indicates "live"/"healthy"/"online" status, animated via `@keyframes pulse` (slow 2.4s opacity wave). Reused in: nav active markers, status pills, online presence, real-time indicators.
5. **Gradient logo & primary CTAs.** The `S` brand mark and primary buttons use a `135deg` copper gradient with a soft outer halo (4-12% copper at 12-24px blur). One gradient button per page, max.
6. **Mono numerics.** All numbers in tables, IDs, deltas, and metric subheads are `IBM Plex Mono`. Hero metrics stay `Inter` ultra-light for the size-to-weight ratio.
7. **Hairline separators over shadows on data.** Tables, lists, and dense surfaces use 1px borders at 4-8% opacity. Shadows are reserved for *floating* elements (cards on aurora, popovers, modals).
8. **Subtle 32-40px grid texture.** A faint dotted/lined grid behind hero surfaces only, masked into a center vignette so it never reaches the data.

If a future variation drops one of these, it stops being J4 Spectrum.

---

## 3. Color tokens

We extend the existing `index.css` `@layer base` and `@theme` blocks, keeping the warm-copper palette the user already approved. **The HSL primitives stay**; the additions are **gradients, glow tokens, and accent companions** (emerald, indigo) that J4 needs.

### 3.1 Primary, accent, status

Stay as-is in `theme.config.ts` warm-copper preset:

| Token | Light | Dark |
|---|---|---|
| `--primary` | `22 51% 55%` (#c67a52) | `22 56% 60%` |
| `--primary-foreground` | `#fff` | `#fff` |
| `--destructive` | `#c4574c` | `#c4574c` (slightly darker) |
| `--accent-500` (emerald — already present) | `#10b981` | `#34d399` |
| **NEW** `--accent-violet-500` | `#6366f1` | `#a5b4fc` |
| **NEW** `--accent-violet-700` | `#4338ca` | `#6366f1` |
| **NEW** `--accent-amber-500` | `#d97706` | `#fbbf24` |

Why violet/amber: the J4 Spectrum gradient needs a tri-color span. Emerald + copper + violet gives us trustworthy spectrum without losing the warm-copper anchor. Amber appears only in feature-card icons (4-color icon set: copper, emerald, violet, amber).

### 3.2 Surfaces

| Token | Light | Dark | Use |
|---|---|---|---|
| `--background` | `#fbf8f1` | `#060410` | App canvas |
| `--card` | `#ffffff` | `rgba(255,255,255,0.04)` | Solid surface |
| **NEW** `--card-glass` | `rgba(255,255,255,0.7)` | `rgba(255,255,255,0.04)` | Glass surface (popovers, header, floating cards) |
| **NEW** `--card-glass-strong` | `rgba(255,255,255,0.85)` | `rgba(255,255,255,0.06)` | Modals on aurora |
| `--border` | `#e5e2dc` | `rgba(255,255,255,0.06)` | Hairline |
| **NEW** `--border-strong` | `rgba(120,69,40,0.12)` | `rgba(255,255,255,0.10)` | Card outlines on aurora |
| `--foreground` | `#2c2c2c` | `#e8e2d5` | Body text |
| `--muted-foreground` | `#6b6b6b` | `#9b8978` | Secondary text |

### 3.3 Aurora & glow tokens (NEW)

These define the J4 backdrop. Set as CSS variables on `<body>` so they flow into Tailwind via the `@theme` block.

```css
/* Light */
--aurora-1: radial-gradient(ellipse 700px 300px at 30% 0%, rgba(167,243,208,0.30), transparent 60%);
--aurora-2: radial-gradient(ellipse 600px 400px at 80% 30%, rgba(212,136,95,0.25), transparent 55%);
--aurora-3: radial-gradient(ellipse 550px 500px at 50% 100%, rgba(165,180,252,0.22), transparent 60%);

/* Dark */
--aurora-1-dark: radial-gradient(ellipse 800px 400px at 30% 0%, rgba(167,243,208,0.18), transparent 60%);
--aurora-2-dark: radial-gradient(ellipse 700px 500px at 80% 30%, rgba(198,122,82,0.32), transparent 55%);
--aurora-3-dark: radial-gradient(ellipse 600px 600px at 50% 100%, rgba(165,180,252,0.22), transparent 60%);

/* Spectrum gradient (text) */
--spectrum-text-light: linear-gradient(135deg, #94522e 0%, #c67a52 50%, #4338ca 100%);
--spectrum-text-dark:  linear-gradient(135deg, #f0ae81 0%, #c67a52 50%, #a5b4fc 100%);

/* Copper button gradient */
--btn-primary-light: linear-gradient(135deg, #d4885f, #c67a52);
--btn-primary-dark:  linear-gradient(135deg, #d4885f, #b56a42);

/* Glow halos (used on logo dot, primary CTAs, pulse dots) */
--glow-copper-sm: 0 0 12px rgba(198,122,82,0.4);
--glow-copper-md: 0 4px 16px rgba(198,122,82,0.30);
--glow-copper-lg: 0 0 0 1px rgba(198,122,82,0.4), 0 8px 24px rgba(198,122,82,0.35);
--glow-emerald: 0 0 8px #6ee7b7, 0 0 0 3px rgba(16,185,129,0.18);
```

Single source of truth: gradient hex values live here, not duplicated in components.

### 3.4 Status pill colors

| State | Light bg / fg / border | Dark bg / fg / border |
|---|---|---|
| Healthy | `rgba(16,185,129,0.10)` / `#047857` / `rgba(16,185,129,0.20)` | `rgba(110,231,183,0.12)` / `#6ee7b7` / `rgba(110,231,183,0.20)` |
| Pending | `rgba(245,158,11,0.12)` / `#b45309` / `rgba(245,158,11,0.25)` | `rgba(245,158,11,0.12)` / `#fbbf24` / `rgba(245,158,11,0.20)` |
| Failed | `rgba(196,87,76,0.10)` / `#9b3d33` / `rgba(196,87,76,0.20)` | `rgba(196,87,76,0.15)` / `#f6a097` / `rgba(196,87,76,0.30)` |
| Inactive | `rgba(0,0,0,0.04)` / `#6b6b6b` / `rgba(0,0,0,0.06)` | `rgba(255,255,255,0.04)` / `#9b8978` / `rgba(255,255,255,0.06)` |

`STATUS_BADGE_VARIANT` in `@/constants/status.ts` updates to map to these. Existing call sites stay.

---

## 4. Typography

We're already on **IBM Plex Sans** + **IBM Plex Sans Arabic** (RTL) + **IBM Plex Mono** (already loaded). No new font.

### 4.1 Type ramp

| Role | Font | Size | Weight | Letter-spacing | Line-height |
|---|---|---|---|---|---|
| Hero metric (landing) | Inter | 84-96px | 200 | -0.05em | 0.95 |
| Hero headline (landing) | Inter | 44px | 200 | -0.04em | 1.05 |
| Section title | Inter | 26px | 300 | -0.025em | 1.15 |
| Page title (in-app) | Inter | 22px | 600 | -0.02em | 1.2 |
| H1 (existing override stays) | IBM Plex | 1.75rem | 700 | -0.02em | 1.2 |
| H2 / Section heading | IBM Plex | 1.25rem | 600 | -0.01em | 1.3 |
| H3 | IBM Plex | 1.0625rem | 600 | 0 | 1.4 |
| H4 | IBM Plex | 0.9375rem | 600 | 0 | 1.5 |
| Body | IBM Plex | 0.8125rem | 400 | 0 | 1.6 |
| Small | IBM Plex | 0.75rem | 400 | 0 | 1.5 |
| Eyebrow | IBM Plex | 10px | 700 | 0.18em (uppercase) | 1.5 |
| Section label | IBM Plex | 9-10px | 700 | 0.15-0.20em | 1.5 |
| Mono — numbers | IBM Plex Mono | 11-12px | 500 | -0.01em | 1.2 |
| Mono — IDs | IBM Plex Mono | 10-11px | 400 | 0 | 1.2 |
| Code preview | IBM Plex Mono | 11px | 400 | 0 | 1.65 |

**Inter is added** alongside IBM Plex for hero/section titles only — it nails the ultra-light + tight tracking look at large sizes that the J4 hero needs. Loaded weights: 200, 300, 500, 600. Body and UI continue on IBM Plex.

Add `Inter` to `index.html` Google Fonts import alongside the existing IBM Plex.

### 4.2 Spectrum gradient text

Applied via:

```css
.gradient-text {
  background: var(--spectrum-text-light);
  -webkit-background-clip: text;
  -webkit-text-fill-color: transparent;
  background-clip: text;
}
.dark .gradient-text { background: var(--spectrum-text-dark); -webkit-background-clip: text; -webkit-text-fill-color: transparent; background-clip: text; }
```

Rules:
- One gradient-text moment per visible viewport. If a hero has it, the section below must not.
- Never on body text or anything < 18px.
- Never on numbers (use mono + foreground color).
- Always on a single span, ideally a verb or named noun.

---

## 5. Radii, spacing, density

### 5.1 Radii

Existing tokens stay (`sm=8`, `md=12`, `lg=16`, `xl=20`, `2xl=24`). Restated usage:

| Surface | Radius |
|---|---|
| Page background / sections | none (full bleed) |
| Cards (data, feature) | `lg` (16px) |
| Cards (landing feature blocks) | `md` (12px) on glass, `lg` (16px) on solid |
| Inputs, buttons (md) | `md` (12px) |
| Buttons (sm) | `sm` (8px) |
| Pills / badges | full (999px) |
| Logo mark | 6-7px (sub-radius, intentional) |
| Modal | `xl` (20px) |
| Popover / dropdown | `lg` (16px) |
| Avatar / icon tile | `sm` (8px) — square-ish, matches logo mark |

### 5.2 Surface density spec

| Surface | Padding | Gap | Border | Shadow | Backdrop |
|---|---|---|---|---|---|
| Page content | `24px` | — | — | — | — |
| Card (data) | `16px 20px` | `16px` between | hairline | none on aurora | — |
| Card (glass, floating) | `16-22px` | `16px` | `border-strong` | `glow-copper-md` | `blur(20px)` |
| Table row | `9-11px 14px` | — | hairline between rows | — | — |
| Table header | `9px 14px` | — | hairline below | — | — |
| Stat card | `12-14px` | — | hairline | — | `blur(20px)` |
| Sidebar item | `7-9px 10-12px` | `1-2px` between | none | none | — |
| Sidebar item (active) | same | same | none | inner `bg-active` | — |
| Header | `12-14px 20-24px` | — | hairline below | — | `blur(12px)` |

Density is **tighter than the current shadcn defaults** — most components ship with ~16px padding; J4 Spectrum lands at ~12-14px on dense surfaces. This is the only place we depart from existing component defaults.

---

## 6. Shadow & glow rules

| Effect | Token | When |
|---|---|---|
| Hairline border | `border-border` | Default separator |
| Card lift | `shadow-card` (existing) | Solid cards on flat bg |
| Card on aurora | none, use `border-strong` | Card sitting over aurora |
| Floating popover | `shadow-float` (existing) | Popover, dropdown |
| Modal | `shadow-soft-lg` + `border-strong` | Dialog |
| Button primary halo | `glow-copper-md` (light) / `glow-copper-lg` (dark) | Primary CTA only |
| Live dot | `glow-emerald` (light) / `0 0 8px #6ee7b7` (dark) | Pulse signals |
| Logo mark | `glow-copper-sm` (light/dark) | The `S` brand mark |

Rule: a single page surface uses **at most one** shadow style for the same element class. Mixing card-shadow and float-shadow on the same view feels chaotic.

---

## 7. Component spec — shadcn primitives

For each, what changes from default. Anything not mentioned is unchanged.

### `button`
- 4 variants: `default` (gradient copper + halo), `secondary` (glass), `ghost` (transparent + hover bg), `outline` (border + foreground).
- 3 sizes: `sm` (h-7, 11px text, 7px radius), `md` (h-9, 13px, 10px radius — default), `lg` (h-11, 14px, 12px radius).
- `default` always uses `--btn-primary-{mode}` gradient, never solid copper.
- `default` always carries `--glow-copper-{md|lg}` shadow.
- One `default` per page surface (more than one = visual noise).
- Loading state: spinner inside, label dimmed.

### `input` / `textarea`
- Glass background in light (70% white), 4% white in dark.
- 1px hairline border, transitions to `border-strong` on focus + 2px ring at `--ring`.
- Search variants: leading icon at 12px from start, RTL mirrors via `ltr:pl-/rtl:pr-`.
- Mono fallback for fields holding pure numeric/ID inputs.

### `card`
- Three variants:
  - `card` (solid, default) — `bg-card` + `border-border` + `shadow-card`.
  - `card-glass` — `bg-card-glass` + `backdrop-blur-xl` + `border-strong`. For floating cards on aurora.
  - `card-elevated` — `card` + `shadow-soft-lg` + hover lift. For interactive list cards.
- All `rounded-lg` (16px).

### `dialog`
- Overlay: `bg-black/40` (light) / `bg-black/60` (dark) + `backdrop-blur-sm`.
- Content: `card-glass-strong` + `shadow-soft-lg` + `rounded-xl` (20px).
- Header has eyebrow + title; footer has actions right-aligned (RTL: start-aligned).

### `dropdown-menu` / `popover`
- Both render as `card-glass` with `shadow-float`.
- Items: `8-9px 12px` padding, hover bg `hover-bg`, active bg `active-bg`.
- Separator: 1px hairline.

### `select`
- Trigger styled like input.
- Content uses popover styling.
- Selected item gets `active-bg` + check icon (existing).

### `table`
- Container: `rounded-lg` + glass surface + hairline border + no extra Card wrap (existing rule).
- Header row: `bg-card-glass-strong` (subtle), 9-10px uppercase tracked label.
- Body row: hairline between rows, hover `hover-bg`, mono cells for IDs/numbers.
- Row padding: `9-11px 14px` (denser than shadcn default).
- Sticky header support when scroll container is in play.

### `badge`
- Variants matching status colors (§3.4): `healthy`, `pending`, `failed`, `inactive`, plus `info` (violet) and `default` (foreground/muted).
- Two sizes: `sm` (9px text, 1-2px padding) for tables, `md` (11px text, 3px padding) elsewhere.
- Always `rounded-full`.
- Existing `STATUS_BADGE_VARIANT` constant updated; call sites unchanged.

### `tabs`
- Underline-style tabs (not pill style).
- Active: 1.5px copper underline, foreground text.
- Inactive: muted-foreground, no underline.
- Hover: foreground + faint underline.

### `avatar`
- 6-8px radius (square-ish, matches logo mark style).
- Initials: copper gradient bg + white text + `glow-copper-sm`.
- Image variant: 1px hairline ring.

### `checkbox` / `switch` / `radio`
- Checkbox checked: copper gradient bg + white check.
- Switch on: copper gradient track + white knob + `glow-copper-sm`.
- Radio: copper inner dot, hairline outer ring.

### `separator`
- 1px hairline at `--border`. Vertical or horizontal.

### `spinner`
- 2px stroke, copper, conic gradient option for primary CTAs.

### `sonner` (toasts)
- Glass card style, `shadow-float`.
- Variants colored on the left edge with a 3px accent strip (success=emerald, error=destructive, info=copper, warning=amber).

---

## 8. Component spec — `@/components/common`

Every component restated in J4 Spectrum. **No file is deleted; behavior preserved.** Style-only.

### `PageHeader`
- 24px section padding, page title + meta + actions row.
- Title uses page-title scale (22px / 600 / -0.02em).
- Meta row underneath: muted-foreground 11px.
- Actions right-aligned (RTL: start-aligned).
- Optional `BackNavigation` slot above title (renders as `← Tenants` in muted with hover foreground).

### `Pagination`
- Full bar: row count left, page-size select + page nav right.
- Page buttons: 8px radius, hairline border, ghost hover.
- Active page: `active-bg` + foreground.
- Page-size select: standard `select` component.
- Persists to localStorage (existing `getPersistedPageSize`).

### `EmptyState`
- Glass card on dense surfaces, flat on landing pages.
- Icon (32-40px) in copper-tinted glass tile + glow.
- Title (h3) + description (body muted) + optional primary action button.
- Centered, 320-440px max-width.

### `ConfirmDialog`
- Wrapper around `dialog`. Title in foreground. Description in muted.
- Destructive action: ghost + destructive text. Primary action: gradient copper.

### `DateRangePicker`
- Glass popover with two-month calendar.
- Selected range shown with copper gradient backdrop on cells.
- Preset buttons: ghost, copper hover.

### `LanguageSwitcher` ⚡ functional concern
- Header dropdown trigger: globe icon + current locale code (`EN` / `AR`).
- Popover (glass) lists locales with current marked by check.
- On select: switches `i18n` + sets `dir` on `<html>`.
- RTL: dropdown opens left-aligned in RTL, mirrors all icons.

### `ThemeToggle` ⚡ functional concern
- Icon button in header. Sun/moon glyph that morphs on toggle.
- Three states cycle: `light` → `dark` → `system`. Tooltip shows current.
- Persists to localStorage; honors `prefers-color-scheme` for `system`.

### `NotificationBell` ⚡ functional concern
- Icon + count pill (mono numerics, copper gradient bg) when count > 0.
- Click opens glass popover with notification list.
- Each row: avatar/icon + title + time + unread dot (copper pulse).
- Footer: "View all" link to `/notifications`.

### `UserAvatar` (header)
- Initials avatar (see §7).
- Click opens glass dropdown: name + email at top, role badge, then `Profile` / `Sessions` / `Settings` / separator / `Sign out`.

### `LoadingScreen`
- Centered logo with subtle pulse.
- Below: copper progress bar at 60% opacity, indeterminate gradient sweep.
- Used during initial app boot only.

### `ErrorBoundary` / `RouteErrorBoundary`
- Glass card, centered, max-width 480px.
- Error icon (destructive tile) + title + collapsible details.
- Two actions: `Reload` (primary) + `Go home` (ghost).

### `ListPageState` / `ListToolbar`
- Top bar: title, search input (glass), filter dropdowns (ghost buttons), trailing primary action.
- States passed in: loading (skeleton rows), empty (EmptyState), error (small inline error).

### `ExportButton`
- Ghost button with `download` icon, opens dropdown for format (CSV / XLSX / PDF).

### `FileUpload`
- Drop zone: dashed border at `border-strong`, glass bg, copper hover state.
- Active: copper gradient border, slight scale.
- File list below with thumbnail/icon + name + size (mono) + remove.

### `InfoField`
- Label (eyebrow style) + value (foreground).
- Used in detail/profile views.
- Optional inline copy-button trailing.

### `SubjectPicker` / `SubjectStack`
- Picker: command-palette style search popover with avatar + name rows.
- Stack: overlapping avatar circles, +N on overflow.

### `ResourceShareDialog` / `OwnershipTransferDialog`
- Both built on `ConfirmDialog` + `SubjectPicker` + permission radios.

### `VisibilityBadge`
- One of: `Public` (emerald), `Private` (muted), `Restricted` (amber). Pill style.

---

## 9. Header pattern (MainLayout)

Layout from start to end (LTR):

```
[Logo dot + name] [Breadcrumbs]                [Search ⌘K] [Notifications] [Language] [Theme] [Avatar]
```

Spec:
- Height: 56px.
- Background: `bg-card-glass` + `backdrop-blur-md` + hairline bottom border. Sits on aurora when present.
- Logo + breadcrumb: left cluster.
- Search: 240px min-width, glass input, `⌘K` kbd hint trailing. Opens command palette on focus.
- Right cluster: 4 icon-buttons (Notifications, Language, Theme, Avatar). Each 32×32, ghost variant.

RTL: cluster mirrors. Logo right, controls left. Icons mirror via `rtl:rotate-180` on directional ones (search has no directional content; avatar dropdown opens to the start side).

---

## 10. Sidebar pattern (MainLayout)

```
[Logo + name]
[Workspace section]
  - Dashboard
  - Tenants    ← active
  - Users
  - Roles
[Platform section]
  - Audit logs
  - API keys
  - Webhooks
  - Settings
[+] Collapsible footer with "Help" + status indicator
```

Spec:
- Width: 220px expanded, 56px collapsed.
- Background: `bg-card-glass` (semi-transparent) + hairline right border. Aurora reads through faintly.
- Section labels (eyebrow style) above each group.
- Items: 7-9px padding, 14px icon, 12px label.
- Active item: `active-bg` + foreground + 12px copper-glow icon (the only icon in the sidebar that glows).
- Hover: `hover-bg` + foreground.
- Collapsed: icons only, tooltip on hover with label.
- Bottom: tenant switcher (if `TenantId` set) + help link.

RTL: mirrors fully (right side, content opens to start).

---

## 11. Layout patterns

### `MainLayout`
Sidebar + Header + content area. Existing structure preserved.
- Background: `--background` + aurora layers as `<body>::before` and `::after` (positioned/blurred via tokens in §3.3). The aurora is a **page-level decoration**, not per-component, so navigation feels continuous.
- Aurora is **reduced** on dense list pages (single corner bloom) — controlled via a `data-page-style="dense"` attribute on the main container that switches the aurora token set. Hero/dashboard pages stay full Spectrum.

### `AuthLayout`
Centered card on full-bleed aurora. Card is `card-glass-strong` + `shadow-soft-lg` + 32px padding. Logo at top, form below. Small footer with "Back to home" link.

### `PublicLayout`
Used for landing + marketing surfaces. Top nav (logo + 4-5 links + Sign in + Get started CTA). Full aurora. No sidebar.

---

## 12. Landing page section blueprint

A single landing route at `/` (public). Sections render top-to-bottom; each is a reusable block exported from `@/features/landing/components/`. Final copy locked below — no more placeholder pass.

### 12.1 `<LandingNav />`
- **Logo** + name "Starter"
- **Links:** `Product` · `Architecture` · `Pricing` · `Docs` · `GitHub`
- **Right cluster:** `Sign in` (ghost) + `Get started` (gradient CTA → `/auth/register`)
- Sticks to top, glass background appears on scroll past hero

### 12.2 `<HeroSection />`
- **Eyebrow:** `OPEN SOURCE · MULTI-TENANT · PRODUCTION-GRADE`
- **Headline (gradient-text accent on the second line):**
  > Stop rebuilding the foundation.
  > **Build what's actually yours.**
- **Deck:**
  > A full-stack CQRS starter spanning .NET 10, React 19, and Flutter 3 — with the fifteen things every SaaS quietly needs (auth, RBAC, multi-tenancy, billing, audit, webhooks, feature flags, observability) already wired together. Skip a quarter of foundation work and ship the part only your team can build.
- **CTAs:** `Clone on GitHub →` (gradient primary, opens repo URL) + `Read the architecture` (ghost, scrolls to §12.6)
- **Meta line:** `Three clients · TypeScript-strict · CQRS via MediatR · Apache-2.0` *(license placeholder — confirm before launch)*
- No fake social proof. No fabricated star counts. Real claims only.

### 12.3 `<TechStrip />`
- **Label:** `BUILT ON`
- **Tags (mono, in order):** `.NET 10`, `React 19`, `Tailwind 4`, `Flutter 3`, `PostgreSQL`, `Redis`, `RabbitMQ`, `MediatR`, `EF Core`, `MassTransit`, `OpenTelemetry`
- Hairline borders top + bottom, no aurora behind this strip

### 12.4 `<FeatureGrid />`
- **Eyebrow:** `WHAT'S ALREADY IN`
- **Title (gradient-text on second line):**
  > Fifteen capabilities.
  > **Eight you'd dread building from scratch.**
- **Deck:**
  > Real implementations of the boring-but-load-bearing pieces — JWT rotation, RBAC matrices, transactional outbox, billing proration, audit diffs. The ones that take weeks to get right and months to clean up.
- **8 cards** (icon color cycles copper / emerald / violet / amber):

| Title | Body | Tags |
|---|---|---|
| Auth & Sessions | JWT + refresh-token rotation, TOTP/2FA, invitations, password reset, session listing, login history. Mailpit-wired for dev SMTP. | `JWT` `2FA` `API keys` |
| Multi-tenancy | Global EF query filters keep tenant data isolated automatically. Platform admins still see everything; tenant users see only theirs. Onboarding, branding, status, business info. | `RLS-style` `Branding` |
| RBAC & Permissions | Role/permission matrix with policy-based authorization. Permissions mirrored across BE/FE/Mobile so adding one is a single source-of-truth edit. | `Roles` `Policies` |
| Billing & Plans | Subscription plans CRUD, plan changes with proration, usage tracking, payment records. Stripe-adapter-ready, no provider lock-in. | `Plans` `Usage` |
| Audit Trail | Every state-changing action logged with actor, tenant, before/after diff. Filterable, immutable, exportable to CSV or PDF. | `Immutable` `CSV/PDF` |
| Webhooks | Endpoint CRUD, signed deliveries, retry with exponential backoff, delivery log, secret rotation, manual test-fire. Outbox pattern under the hood. | `Outbox` `HMAC` |
| Feature flags | Tenant-scoped overrides, opt-out, enforcement modes. Ship behind a flag, ramp without redeploys. | `Tenant override` |
| Observability | OpenTelemetry → Jaeger, Prometheus metrics, Serilog structured logs with conversation-id correlation across every consumer. | `OTel` `Jaeger` `Prom` |

### 12.5 `<CodeSection />`
- **Eyebrow:** `SHOW, DON'T TELL`
- **Title (gradient-text on second line):**
  > Real handlers.
  > **Transactional outbox, by default.**
- **Deck:**
  > Every command handler is a sealed primary-constructor record. Events are scheduled through a collector — never published mid-handler — so the row commits atomically with the business write. An architecture test fails the build if anyone bypasses it.
- **Code preview:** the `RegisterTenantCommandHandler.cs` snippet from the mockup (BE Application layer). Static highlight only — no live syntax-highlighter dependency.
- **Talking points** (numbered 01-04 right of the code):
  1. **Sealed primary constructors.** Less ceremony, smaller files, no DI mistakes.
  2. **Result&lt;T&gt; everywhere.** No exceptions for control flow. Controllers map to HTTP via `HandleResult()`.
  3. **Outbox over `IPublishEndpoint`.** Events commit with the business row. Architecture test fails the build if you regress.
  4. **Validators auto-discovered.** FluentValidation + a MediatR pipeline behavior — drop a class in, it runs.

### 12.6 `<ArchitectureSection />`
- **Eyebrow:** `ARCHITECTURE AT A GLANCE`
- **Title (gradient-text on second line):**
  > Three clients.
  > **One source of truth.**
- **Deck:**
  > Permission strings, theme tokens, and API response envelopes mirror across the .NET backend, React frontend, and Flutter mobile client. Define a permission once — it's enforced everywhere.
- **Three cells:**
  - **Backend** — `.NET 10` — `CQRS · MediatR · EF Core`
  - **Frontend** — `React 19` — `Tailwind 4 · TanStack · shadcn/ui`
  - **Mobile** — `Flutter 3` — `flutter_bloc · Clean Arch · Hive`
- **Flow line below:** `API → Application (MediatR CQRS) → Domain ← Infrastructure (EF Core, Outbox, Services)`

### 12.7 `<StatsStrip />`
4 mono-numeric columns with gradient-text accents:
- `15` — backend features
- `22` — frontend modules
- `3` — production clients
- `0` — hello-worlds

### 12.8 `<FooterCta />`
- **Headline (gradient-text on second line):**
  > Ship the boring parts
  > **before lunch.**
- **Body:**
  > Clone, run `docker compose up`, log in as `superadmin@starter.com` in 60 seconds.
- **Mono surface:** `git clone https://github.com/<org>/boilerplate-cqrs` *(replace `<org>` placeholder before launch — see §19.5)*
- **Footer nav:** `Product` · `Docs` · `GitHub` · `License` · `© 2026`

### Section ordering rule
Hero → Tech → Features → Code → Architecture → Stats → Footer CTA. The progression goes: **claim → proof of stack → proof of breadth → proof of code quality → proof of cross-stack discipline → numeric punch line → action**. Sections may be reordered by content team but the spec rule is: code & architecture sections never come before the feature grid (the user must believe what's claimed before they're asked to evaluate the implementation).

---

## 13. RTL & i18n

Existing rules (CLAUDE.md) re-stated in J4 context:

- `text-start` / `text-end` (logical) over `text-left` / `text-right`.
- `ltr:pl-3 rtl:pr-3` for directional padding.
- Arrows: `rtl:rotate-180` on `→` chevrons. Aurora gradients mirror via x-axis flip (the spectrum reads LTR→ in LTR, RTL→ in RTL — implemented by mirroring the gradient stop positions).
- Mono spans (IDs, code) **do not** flip — they're explicitly `dir="ltr"`.
- Code preview block always `dir="ltr"`.
- Font swap (already in place): RTL pages use `IBM Plex Sans Arabic`. The Inter additions for hero/section titles are **English-only**; in RTL they fall back to `IBM Plex Sans Arabic` for those headings.

---

## 14. Accessibility

- **Contrast.** All foreground/background pairs hit WCAG AA (4.5:1 body, 3:1 large). Verified with the chosen tokens — gradient-text moments tested against the *darkest* gradient stop's contrast, not the lightest, to avoid fail-on-edge.
- **Focus.** Existing `.focus-ring` utility stays. 2px ring at `--active-border` with 30% opacity, 2px offset. Inset variant for inputs.
- **Reduced motion.** `@media (prefers-reduced-motion: reduce)` disables: aurora gradient animations (if any are added later — Phase 0 ships static), pulse keyframes, button hover lift, gradient sweeps.
- **Screen readers.** All icon-only buttons (theme toggle, language, notifications, avatar) carry `aria-label` and a visually-hidden text alternative. Spectrum gradient text uses `<span>` with the same color contrast on its primary stop, and is not the only signal for any meaning.
- **Keyboard.** Sidebar fully tab-navigable. Search opens with `⌘K` / `Ctrl+K`. Esc closes overlays. Tab order respects logical reading order (LTR or RTL).

---

## 15. Style Reference page (`/styleguide`)

A single live page that renders **everything** specced in §3-12. **Development-only** — the `/styleguide` route is registered conditionally on `import.meta.env.DEV` and is not present in production bundles. Lazy-loaded so it doesn't bloat dev HMR either.

Sections:

1. **Tokens** — color swatches (with hex + role), gradients (with usage), shadows (rendered).
2. **Typography** — every row of the type ramp rendered with sample text (LTR + RTL).
3. **Buttons** — every variant × size combo, plus `disabled` and `loading` states.
4. **Inputs** — text, search, textarea, select, checkbox, switch, radio, date, file (each in default, focused, error, disabled).
5. **Cards** — solid, glass, elevated, with example content.
6. **Tables** — full table with stat strip, header, hover, sticky header demo.
7. **Badges** — every status × size.
8. **Dialogs** — confirm, full content, destructive.
9. **Popovers/Dropdowns** — including dropdown-menu variants.
10. **Tabs / Toasts / Avatars / Separators / Spinners**.
11. **Header & Sidebar** — rendered as a non-functional preview within the page.
12. **Common components** — every common component live (LanguageSwitcher, ThemeToggle, NotificationBell, UserAvatar dropdown, EmptyState, Pagination, BackNavigation, etc.).
13. **Landing blocks** — each landing-section block rendered standalone with sample data.
14. **RTL / i18n** — toggle to flip the entire page to RTL Arabic and verify each block.
15. **Dark/Light** — toggle at top to flip both modes; visual diff is intentional verification.

This page is the **acceptance test** for Phase 0. If something can't be rendered correctly here, it's not done.

---

## 16. Implementation strategy (high level)

Phase 0 lands in three commits, each independently reviewable:

1. **Tokens & utilities.** Update `index.css` (new tokens), `theme.config.ts` (no preset change but adds violet/amber accent companions), `tailwind.config.ts` (register Inter, new color-name aliases). Add aurora utility classes. Add `Inter` to `index.html` font import.
2. **shadcn/ui restyle + common components.** File-by-file pass updating styling per §7 and §8. No file deletions, no API changes — pure visual-token swaps. Each component gets a matching story rendered on `/styleguide`.
3. **Layouts & landing.** Update `MainLayout` (header + sidebar), `AuthLayout`, `PublicLayout`. Build landing components per §12 and wire them at `/`. Apply aurora on `<body>` via the layout components.

After all three land, Phase 0 is complete. A frontend-design pass over the existing 22 feature pages becomes Phase 3 — but those pages will already inherit most of the system through PageHeader / Pagination / EmptyState / Table.

---

## 17. Risks & mitigations

| Risk | Mitigation |
|---|---|
| Aurora gradients hurt readability on data-heavy pages | `data-page-style="dense"` attribute reduces aurora to a single corner bloom on list pages. Style Reference verifies contrast on dense surfaces. |
| Backdrop-filter performance on lower-end devices | Glass surfaces use `backdrop-filter` only for *floating* elements (header, popovers, modals, hero card). Body content stays solid. Acceptable performance budget. |
| Gradient-text accessibility | Tested against AA on the darkest gradient stop. The gradient is **not** the only signal for any UI meaning — it's a typographic flourish. |
| Inter font adds payload | Add only weights 200/300/500/600 (~60KB total). Body type stays IBM Plex (already loaded). |
| Tenant branding override (existing feature) | Existing tenant-branding hooks override `--primary` only. J4 gradients derive from `--primary` via `color-mix()` in the same fashion as existing semantic tokens — branding flows through. |
| RTL gradient mirroring | Tested on Style Reference RTL toggle. If the `135deg` gradient feels wrong in RTL, swap to `225deg` per `dir` (rule encoded in tokens). |
| Existing pages will look different the moment shadcn/common get restyled in step 2 (since they're used everywhere) | This is acceptable — step 2 lands as a single PR with the full token + component pass, so the visual shift happens atomically. Step 3 adds aurora layouts on top. There's no "half-redesigned app" intermediate state. |

---

## 18. Acceptance criteria

Phase 0 is done when:

- [ ] All tokens listed in §3 exist in `index.css` for both modes and are referenced by their semantic names (no hex literals in components).
- [ ] Inter is loaded; type ramp in §4.1 renders.
- [ ] Every shadcn/ui primitive in §7 matches its rule when rendered on `/styleguide`.
- [ ] Every common component in §8 matches its rule on `/styleguide`.
- [ ] `/styleguide` renders correctly in both light/dark and LTR/RTL.
- [ ] Header (§9) and Sidebar (§10) render in `MainLayout` with all 4 right-cluster controls (notifications / language / theme / avatar) functional.
- [ ] Landing route (`/`) renders sections 1-8 from §12.
- [ ] No `as unknown as` casts introduced; types extended properly.
- [ ] `npm run build` passes.
- [ ] Lighthouse run on `/` and `/styleguide`: a11y ≥ 95, performance ≥ 80 (mid-tier hardware target).
- [ ] AA contrast verified on `/styleguide` for every token pair.

---

## 19. Resolved decisions

All Phase 0 open questions resolved 2026-04-26:

1. **Inter font — accepted.** Loaded weights 200/300/500/600 (~60KB total). Used for hero/section titles only; IBM Plex remains the body and UI face.
2. **Landing copy — refined inline in §12** (this revision). Final wording locked; further edits are content tweaks not design.
3. **`/styleguide` gating — development builds only.** Route registered conditionally on `import.meta.env.DEV`; never bundled into production. No tenant exposure concern; no platform-admin role check needed.
4. **Tenant branding — default spectrum only for now.** Tenants override `--primary` (existing capability), which flows through to all copper-derived gradients via `color-mix()`. The tri-color spectrum (copper/emerald/violet) stays fixed across tenants. If/when the backend gains a `tenant.brandSecondary` and `tenant.brandTertiary` field, a follow-up can extend it.
5. **"Clone the boilerplate" CTA — GitHub repo URL.** Set as `import.meta.env.VITE_GITHUB_URL` (already in env config pattern), defaulting to `https://github.com/<org>/boilerplate-cqrs` until the org/repo name is locked.

---

## 20. After Phase 0

This spec's deliverable feeds into:

- **Phase 1 — Shell + Landing.** Already pulled in here (header, sidebar, landing). Phase 1 may end up trivial after Phase 0 since most of it lands here; it'll mostly be polish + animation passes.
- **Phase 2 — Shared components.** Already pulled in (the 22 components in `@/components/common`). Phase 2 likely becomes "expand any specific shared component that needs more states/variants found during page work."
- **Phase 3 — Feature pages.** 22 features, each apply the system. Each takes 1-3 PRs depending on complexity.

If Phase 0 lands cleanly, Phases 1-2 collapse to small follow-ups and the bulk of remaining work is Phase 3.
