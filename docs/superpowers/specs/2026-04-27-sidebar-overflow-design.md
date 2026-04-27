# Sidebar Overflow — "More" Secondary Panel

**Created:** 2026-04-27
**Branch:** `fe/redesign-phase-1` (continues from the shell visual refresh)
**Predecessor:** Shell visual refresh — `docs/superpowers/specs/2026-04-27-shell-visual-refresh-design.md` shipped on this branch (the floating-glass sidebar / header / page-header).
**Companion:** Plan A — `docs/superpowers/specs/2026-04-27-redesign-phase-1-design.md` (the original group-IA spec) — only §4.1 / §4.2 still apply (group structure & data shape).

## 1. Why this spec exists

After the floating-glass shell shipped, the sidebar still solves the *grouping* problem with a single fall-back: vertical scroll. On low-height viewports (laptop screens, small windows, browser zoom > 100 %) the sidebar overflows and the user has to scroll a fixed-position chrome element to reach groups at the bottom — which feels old and undermines the floating aesthetic.

This spec adds **group-level overflow handling**: when not all groups fit, the lowest-priority groups move into a "More" secondary panel that slides in beside the main sidebar on demand. Vertical scrolling on the main sidebar is eliminated.

## 2. Decisions locked in brainstorm

- **Overflow target: secondary slide-out panel** (Option B) — not a popover, not an inline accordion. Anchored, dismissible, sized to match the main sidebar (~ 220 px).
- **"You are here" strategy: Y+ — active "More" button + auto-open on deep-link.** When the current page is in an overflowed group, the "More" button takes pill-active styling and swaps its caption to a two-line label: leaf name + "in More · {group}". On first mount (or page refresh), if the active page is in overflow, the secondary panel auto-opens once. Any subsequent navigation respects user control (panel stays where the user left it).
- **Priority order: static** — derived from the existing `useNavGroups()` order. No drag-to-reorder, no user pinning.
- **Mobile (`<lg`):** unchanged — the drawer shows everything with scroll. Drawer is opened intentionally; scrolling inside it is acceptable. The "More" button and overflow logic are desktop-only.
- **Detection: runtime `ResizeObserver`** on the nav container, recomputing the visible/overflow split on viewport changes, sidebar-collapse changes, and font-size changes. No hardcoded breakpoints.

## 3. Scope

**In scope:**

1. New `useNavOverflow` hook — wraps `useNavGroups()` and partitions groups into `visibleGroups` / `overflowGroups` based on measured space.
2. New `MorePanel` component — secondary floating-glass panel that renders the overflow groups.
3. New `MoreButton` component (or inline JSX in Sidebar) — pinned at the bottom of the main sidebar, opens the panel; renders an active state when the current page is in overflow. Cross-fades between default and active captions.
4. `useUIStore` additions — `morePanelOpen: boolean` + `setMorePanelOpen`.
5. Sidebar render changes — render visible groups, then the More button (only when there's overflow), then the existing collapse-chevron block.
6. Auto-open behavior on first mount when active is in overflow.
7. Auto-close behavior on Esc, click-outside, route change *into* a visible group's item (not when navigating *between* overflow items — the panel stays open in that case).
8. Body scroll lock and backdrop **only on mobile drawer** (existing behavior). The desktop secondary panel does NOT lock body scroll — it sits beside the main sidebar without a backdrop.
9. **AR + KU translations** of the new `nav.more.*` keys — included in this spec, not deferred.
10. **Scroll-to-top on navigation** — small `<ScrollToTop />` route-tree component fixing the existing bug where scrolling down a long page and clicking a NavLink keeps the next page scrolled. See §13.

**Out of scope (deferred or skipped):**

- Auto-promote / reorganize on navigation (Option Z) — explicitly rejected.
- User-pinnable groups (drag to pin, persist).
- Per-group enable/disable toggles.
- Mobile drawer overflow handling (drawer keeps scrolling).

## 4. UX behavior in detail

### 4.1 What the user sees (active page in a *visible* group)

- Main sidebar shows the visible groups + a passive "More" button at the bottom.
- "More" button renders `··· · More · [N]` where N = count of overflowed groups.
- `pill-active` styling sits on the actual nav item the user is on.
- Click "More" → `MorePanel` slides in beside the main sidebar. Click "More" again → closes. Click ×, Esc, or click outside the panel → closes.

### 4.2 What the user sees (active page in an *overflowed* group)

- Main sidebar's "More" button takes `pill-active` styling.
- Caption: two-line layout — leaf name (e.g. "Channels") on top, eyebrow `IN MORE · COMMUNICATION` below.
- Optional leading icon: the active item's `LucideIcon`. Reuses the existing nav-item icon to make "you are here" precise.
- On first mount (page refresh, deep link), `MorePanel` auto-opens once. Once the user navigates to a visible group's item OR explicitly closes the panel, the panel stays in user-controlled state.
- Inside `MorePanel`, the active item still gets normal `pill-active` styling — so once the panel is open, the user sees the same "you are here" anywhere they look.

### 4.3 Open / close interactions

| Trigger | Behavior |
|---|---|
| Click "More" button (closed → open) | Open `MorePanel` |
| Click "More" button (open → closed) | Close panel |
| Click any nav item inside `MorePanel` | Navigate. Panel stays open; if next page is in a visible group, panel auto-closes (so user gets back the screen real estate). If next page is still in overflow, panel stays. |
| Click any nav item in main sidebar | Navigate. If panel was open, panel auto-closes. |
| Click outside both sidebar and panel (i.e. on main content) | Close panel |
| Press Esc | Close panel |
| Resize viewport | Re-measure; if overflow no longer happens (groups now all fit), close panel and stop rendering "More" button |
| Toggle desktop sidebar collapse (chevron) | Sidebar becomes `lg:w-16` icon-only — overflow logic still runs but each group's height is smaller; "More" button collapses to icon-only |

### 4.4 The "More" button render shape

**Default (no overflow currently active):**

```
┌─────────────────────────────┐
│ ···  More              [3]  │
└─────────────────────────────┘
```

**Active (current page is in overflow):**

```
┌─────────────────────────────┐
│ [icon]  Channels       [3]  │
│         IN MORE · COMM      │
└─────────────────────────────┘
```

The two-line layout uses the same total height as the default (~ 40 px) — bigger top text, smaller eyebrow below. No layout jump.

**Collapsed sidebar (`lg:w-16`):**

- Default: just the `···` icon, centered, with the count badge as a small dot if any overflow exists.
- Active: shows the active item's icon, with `pill-active` styling. Tooltip on hover (Radix Tooltip) reads the leaf name + group context.

### 4.5 The secondary panel render shape

```
┌────────────────────────┐
│ More groups        [×] │   <- header with title + close button
├────────────────────────┤
│ • COMMUNICATION        │   <- group eyebrow with copper dot prefix
│   Channels             │
│   Templates            │
│   Trigger Rules        │
│ • BILLING              │
│   Plans                │
│   Subscriptions        │
│ • PLATFORM             │
│   Audit Logs           │
│   Settings             │
└────────────────────────┘
```

- Same `surface-floating` styling as the main sidebar — visually a sibling card, not a popover.
- Group eyebrows match the main sidebar's eyebrow style (copper dot prefix, uppercase, tracked).
- Items use the same `pill-active` / `state-hover` pattern.
- Width: 220 px (vs main sidebar 240 px expanded — the secondary is intentionally a touch narrower so it reads as "supplementary").
- Position: `fixed top-3.5 bottom-3.5 ltr:left-[calc(15rem+1.75rem+0.875rem)]` (sidebar width + sidebar margin + 14 px gap) when main is expanded. `lg:ltr:left-[calc(4rem+1.75rem+0.875rem)]` when main is collapsed. RTL flips.
- z-index: 35 (above content `z-30`, below the mobile drawer backdrop `z-30`'s lifted state — but the secondary panel never coexists with the mobile drawer since it's lg+ only).
- Transition: `motion-safe:transition-all motion-safe:duration-300` translate-x slide-in from the start edge of its position.
- Header: small "More groups" title + a close `×` button on the end. Close icon is `X` from lucide-react.

### 4.6 What the page-header (PageHeader) breadcrumbs add

The breadcrumbs trail in `PageHeader` continues to render as today (Plan B-v2 ships this). When the user is on a `/communication/channels` page:

```
Workspace › Channels
```

The first breadcrumb (`Workspace` or whatever the parent route is) plus the leaf gives a redundant "where" signal. Combined with the active "More" button, the user has three independent confirmations of where they are:

1. The page-header breadcrumbs.
2. The "More" button's pill-active + two-line caption.
3. The leaf item's `pill-active` styling inside the open `MorePanel`.

Three signals is right for navigation chrome — exactly enough to never lose your place, never excessive.

## 5. Architecture & file structure

### 5.1 New files

| File | Responsibility |
|---|---|
| `boilerplateFE/src/components/layout/MainLayout/useNavOverflow.ts` | Hook that wraps `useNavGroups()` and returns `{ visibleGroups, overflowGroups, activeGroup, isActiveOverflowed, hasOverflow, navRef, moreButtonRef, recompute }`. Owns the `ResizeObserver` and the height-measurement loop. |
| `boilerplateFE/src/components/layout/MainLayout/MorePanel.tsx` | The secondary panel component. Reads `morePanelOpen` from store + `overflowGroups` from `useNavOverflow`. Renders the floating-glass card with title, close button, and the overflowed groups. Accepts `overflowGroups` as a prop so the parent owns the overflow computation. |
| `boilerplateFE/src/components/layout/MainLayout/MoreButton.tsx` | The button rendered at the bottom of the main sidebar. Accepts `overflowGroups`, `activeGroup`, `isActiveOverflowed`, `isCollapsed`, `morePanelOpen`. Renders default / active / collapsed variants per §4.4. |

### 5.2 Modified files

| File | Change |
|---|---|
| `boilerplateFE/src/stores/ui.store.ts` | Add `morePanelOpen: boolean` (default `false`, **NOT** persisted), `setMorePanelOpen(open)`, `toggleMorePanel()`. Add selector `selectMorePanelOpen`. |
| `boilerplateFE/src/components/layout/MainLayout/Sidebar.tsx` | Replace direct call to `useNavGroups()` with `useNavOverflow()`. Render only `visibleGroups`. Append `<MoreButton />` after the nav, before the collapse-expand-chevron block. Wire `navRef` to the `<nav>` element. |
| `boilerplateFE/src/components/layout/MainLayout/MainLayout.tsx` | Mount `<MorePanel />` as a sibling to `<Sidebar />` and `<Header />`. |

### 5.3 The `useNavOverflow` algorithm

Pseudo-code, light on TypeScript:

```
useNavOverflow():
  groups = useNavGroups()  // existing — returns SidebarNavGroup[]
  isCollapsed = useUIStore(selectSidebarCollapsed)
  navRef = useRef<HTMLElement>(null)
  moreButtonRef = useRef<HTMLElement>(null)
  [overflowCount, setOverflowCount] = useState(0)

  recompute = () => {
    if (isCollapsed) {
      setOverflowCount(0)  // collapsed sidebar has tiny items — overflow is rare; skip the work
      return
    }
    if (!navRef.current) return

    container = navRef.current
    available = container.clientHeight - moreButtonRef.current?.offsetHeight ?? 40

    // Measure each group's offsetHeight by iterating children of nav.
    // If we render groups with `data-group-id={group.id}`, we can query them.
    childrenHeights = [...container.children].map(el => el.offsetHeight + parseInt(getComputedStyle(el).marginTop, 10))

    accumulated = 0
    visibleCount = 0
    for h of childrenHeights:
      if accumulated + h <= available:
        accumulated += h
        visibleCount += 1
      else:
        break

    overflow = groups.length - visibleCount
    if overflow !== overflowCount: setOverflowCount(overflow)
  }

  useLayoutEffect: run recompute on every render where groups, isCollapsed, or window size could have changed.
  useEffect: attach ResizeObserver to navRef.current → call recompute on resize.

  visible = groups.slice(0, groups.length - overflowCount)
  overflow = groups.slice(groups.length - overflowCount)

  activeGroup = groups.find(g => g.items.some(i => location.pathname starts with i.path))
  isActiveOverflowed = overflow.includes(activeGroup)

  return { visibleGroups: visible, overflowGroups: overflow, activeGroup, isActiveOverflowed, hasOverflow: overflowCount > 0, navRef, moreButtonRef }
```

Notes:
- The "first group" (top: Dashboard / Notifications) is treated like any other group for overflow purposes, but in practice it's small enough to always fit. If the viewport is so small that even the top group can't fit, the algorithm gracefully degrades (only the top group renders, everything else overflows, "More" button shows count = N − 1).
- When `isCollapsed`, the algorithm short-circuits — collapsed items are 40 × 40 each so 8+ items fit even on small viewports. Overflow on collapsed mode is extremely unlikely. (If it happens, the user can expand to see "More".)
- The match algorithm for `activeGroup` walks all groups (including the visible ones) to figure out the user's location. If the active path is in `top` group → it's a normal active item; `MoreButton` stays passive.

### 5.4 Auto-open on deep-link

Inside the Sidebar component (or a small `useAutoOpenMoreOnDeepLink` hook):

```
useEffect(() => {
  // Run once on mount per session.
  if (!hasMounted.current && isActiveOverflowed) {
    setMorePanelOpen(true)
    hasMounted.current = true
  }
}, [isActiveOverflowed])
```

We use a `useRef<boolean>` flag so the effect only fires once per Sidebar mount. Subsequent navigations do NOT auto-open the panel.

Edge case: user lands on `/dashboard` (visible group), then directly navigates to `/communication/channels` (overflowed). Should the panel auto-open on the second navigation? **No** — auto-open is a one-shot for first-time orientation. If the user navigates between visible and overflowed, they can click "More" themselves. Avoids surprise auto-open during normal use.

### 5.5 Click-outside + Esc

Implemented in the `MorePanel` component:

```
useEffect(() => {
  if (!morePanelOpen) return
  onKeyDown = (e) => {
    if (e.key === 'Escape') setMorePanelOpen(false)
  }
  onClick = (e) => {
    if (e.target is not inside main sidebar AND not inside MorePanel) setMorePanelOpen(false)
  }
  attach listeners; cleanup on close.
}, [morePanelOpen])
```

The "click outside both" check uses `el.closest('[data-shell="sidebar"]')` and `el.closest('[data-shell="more-panel"]')` — both wrappers gain a small `data-shell` attribute for this query.

### 5.6 Auto-close on navigation

When the user clicks any nav item:
- If destination is in `visibleGroups` → close the panel (free up the screen).
- If destination is in `overflowGroups` → keep the panel open.

Implemented inline in `Sidebar.tsx` and `MorePanel.tsx`:

```
const onNavLinkClick = (item) => {
  // existing route navigation
  if (!isInOverflow(item)) setMorePanelOpen(false)
}
```

Or simpler: after navigation, the `useNavOverflow` hook re-evaluates `isActiveOverflowed`. Add an effect:

```
useEffect(() => {
  if (morePanelOpen && !isActiveOverflowed) {
    // user navigated to a visible page — close the panel
    setMorePanelOpen(false)
  }
}, [location.pathname, isActiveOverflowed])
```

Cleaner — single place to manage close-on-nav.

## 6. Styling specifics

The "More" button uses a two-layer caption that cross-fades on state change. Both the default caption (`···  More  [N]`) and the active caption (`[icon]  Channels / IN MORE · COMM`) render simultaneously, stacked absolutely; only one is `opacity-100` at a time. The count badge belongs to the default layer only — when the button enters active state, the badge fades out with the default caption (per Open Question §11.1, resolved: drop the badge when active).

Both layers use `motion-safe:transition-opacity motion-safe:duration-200` so the swap is smooth (and instant when `prefers-reduced-motion: reduce`).

```tsx
<button
  type="button"
  onClick={toggleMorePanel}
  aria-pressed={morePanelOpen}
  aria-label={isActiveOverflowed && activeItem ? t('nav.more.inMore', { group: activeGroup.label }) : t('nav.more.label')}
  className={cn(
    'relative flex items-center rounded-[10px] h-10 px-3 text-sm overflow-hidden',
    'motion-safe:transition-all motion-safe:duration-150',
    'border border-foreground/10 bg-foreground/5 hover:bg-foreground/10',
    isActiveOverflowed && 'pill-active border-transparent'
  )}
>
  {/* Default caption layer — visible when NOT active-overflowed */}
  <span
    aria-hidden={isActiveOverflowed}
    className={cn(
      'absolute inset-0 flex items-center gap-2.5 px-3',
      'motion-safe:transition-opacity motion-safe:duration-200',
      isActiveOverflowed ? 'opacity-0 pointer-events-none' : 'opacity-100'
    )}
  >
    <MoreHorizontal className="h-[18px] w-[18px] shrink-0 opacity-70" />
    <span className="flex-1 text-start">{t('nav.more.label')}</span>
    <span className="rounded-full bg-primary/20 px-1.5 py-0.5 text-[10px] font-mono font-bold text-primary">
      {overflowGroups.length}
    </span>
  </span>

  {/* Active caption layer — visible when current page is in overflow */}
  {isActiveOverflowed && activeItem && activeGroup && (
    <span
      className={cn(
        'absolute inset-0 flex items-center gap-2.5 px-3',
        'motion-safe:transition-opacity motion-safe:duration-200',
        'opacity-100'
      )}
    >
      <activeItem.icon className="h-[18px] w-[18px] shrink-0" />
      <div className="flex flex-col gap-0 leading-tight text-start min-w-0">
        <span className="text-[12px] font-medium truncate">{activeItem.label}</span>
        <span className="text-[8px] uppercase tracking-[0.12em] opacity-70 truncate">
          {t('nav.more.inMore', { group: activeGroup.label })}
        </span>
      </div>
    </span>
  )}

  {/* Invisible spacer — keeps the button's intrinsic height stable across both layers */}
  <span aria-hidden className="invisible flex items-center gap-2.5">
    <span className="h-[18px] w-[18px]" />
    <span className="text-sm">_</span>
  </span>
</button>
```

Notes:
- Both caption layers are `position: absolute` over a transparent button surface. The button's height is held by an invisible spacer at the end so the row stays exactly `h-10` regardless of which caption is rendering.
- `aria-pressed` reflects the panel's open state (matches `<button>` semantics for toggle buttons).
- `aria-label` reflects the *current* meaning of the button — when active-overflowed, the label is the contextual one ("In More · Communication"); otherwise the default ("More").
- The active-state count badge is intentionally absent (per resolution of Open Question §11.1).

Collapsed mode (`lg:w-16`) gets a separate render path inside the same component:
- Default: `MoreHorizontal` icon centered, with a small copper dot pulse if `overflowGroups.length > 0` AND `!isActiveOverflowed`.
- Active: the active item's icon centered with `pill-active` styling.
- Both wrapped in a Radix Tooltip displaying `t('nav.more.label')` (default) or the active two-line label (active), to compensate for hidden text.

The panel:

```tsx
<aside
  data-shell="more-panel"
  className={cn(
    'fixed top-3.5 bottom-3.5 z-35 w-[220px] flex flex-col rounded-[18px]',
    'surface-floating',
    'motion-safe:transition-all motion-safe:duration-300',
    isCollapsed
      ? 'lg:ltr:left-[calc(4rem+1.75rem+0.875rem)] lg:rtl:right-[calc(4rem+1.75rem+0.875rem)]'
      : 'lg:ltr:left-[calc(15rem+1.75rem+0.875rem)] lg:rtl:right-[calc(15rem+1.75rem+0.875rem)]',
    !morePanelOpen && 'opacity-0 pointer-events-none ltr:-translate-x-3 rtl:translate-x-3',
    morePanelOpen && 'opacity-100 translate-x-0',
    'hidden lg:flex'  // desktop only
  )}
>
  <div className="flex h-9 items-center justify-between px-4 pt-3 pb-2 border-b border-border/40">
    <span className="text-xs font-semibold uppercase tracking-[0.1em] text-muted-foreground">
      {t('nav.more.panelTitle')}
    </span>
    <button
      type="button"
      onClick={() => setMorePanelOpen(false)}
      aria-label={t('nav.more.close')}
      className="flex h-6 w-6 items-center justify-center rounded-md hover:bg-foreground/5 motion-safe:transition-colors"
    >
      <X className="h-3.5 w-3.5" />
    </button>
  </div>
  <nav className="flex-1 overflow-y-auto px-3 pt-2 pb-3">
    {overflowGroups.map((group) => (
      <div key={group.id} className="mt-4 first:mt-0">
        {group.label && (
          <div className="px-3 pb-1 pt-1 text-[10px] font-semibold uppercase tracking-[0.12em] text-muted-foreground">
            <span className="inline-block w-1 h-1 rounded-full bg-primary/70 me-1.5 align-middle -translate-y-px" />
            {group.label}
          </div>
        )}
        <ul className="space-y-1">
          {group.items.map((item) => (
            <li key={item.path}>
              <NavLink
                to={item.path}
                end={item.end}
                className={({ isActive }) =>
                  cn(
                    'flex items-center gap-2.5 rounded-[10px] h-9 px-3 text-sm motion-safe:transition-all motion-safe:duration-150',
                    isActive ? 'pill-active' : 'state-hover'
                  )
                }
              >
                <item.icon className="h-[18px] w-[18px] shrink-0" />
                <span className="flex-1">{item.label}</span>
              </NavLink>
            </li>
          ))}
        </ul>
      </div>
    ))}
  </nav>
</aside>
```

The panel's `overflow-y-auto` on `<nav>` is a fallback — if even the overflow groups don't fit, the panel itself can scroll. Acceptable: it's a panel the user opened intentionally, not a permanent chrome element.

## 7. i18n keys to add

Add the same `nav.more.*` block to all three locale files. AR + KU translations are included — not deferred.

**`en/translation.json`:**

```json
"nav": {
  ...existing keys unchanged...
  "more": {
    "label": "More",
    "inMore": "In More · {{group}}",
    "panelTitle": "More groups",
    "close": "Close more groups"
  }
}
```

**`ar/translation.json`:**

```json
"nav": {
  ...existing keys unchanged...
  "more": {
    "label": "المزيد",
    "inMore": "في المزيد · {{group}}",
    "panelTitle": "مجموعات إضافية",
    "close": "إغلاق المجموعات الإضافية"
  }
}
```

**`ku/translation.json`:** (Sorani Kurdish)

```json
"nav": {
  ...existing keys unchanged...
  "more": {
    "label": "زیاتر",
    "inMore": "لە زیاتر · {{group}}",
    "panelTitle": "گرووپە زیادەکان",
    "close": "داخستنی گرووپە زیادەکان"
  }
}
```

These are the implementer's starting translations. A native localizer can refine them later, but they're complete enough to ship — much better than an English fall-through that breaks the RTL flow with Latin characters mid-sentence.

## 8. Light + dark coverage

No new tokens needed. The "More" button uses `bg-foreground/5`, `border-foreground/10`, `bg-foreground/10` (hover) — same pattern as the Header's search-bar trigger which already works in both modes.

The `MorePanel` uses `surface-floating` — also already light/dark-aware.

The active state uses `pill-active` — already preset-aware via `color-mix(in srgb, hsl(var(--primary)) ...)`.

## 9. Implementation order

The work fits one 5-task plan on `fe/redesign-phase-1`:

0. **`ScrollToTopOnNavigate` component** — small self-contained route-tree fix (see §13). Ships first because it's independent and instantly improves UX.
1. **`useNavOverflow` hook** (no UI; just the hook + its measurement logic, with a tiny demo consumer in the styleguide page if present).
2. **Store + `MoreButton` in main sidebar** (reads overflow, renders the button at the bottom of `Sidebar.tsx`, default + active-overflow + collapsed render variants with cross-fade).
3. **`MorePanel` component + `MainLayout` mount** (renders the secondary panel when open, click-outside / Esc / nav-close logic) + AR/KU translations of the new `nav.more.*` keys.
4. **Code-review pass.**

(No StatCard / identity polish / palette content work — those remain deferred.)

## 10. Verification

Same harness as prior plans. Visual targets:

- **Tall viewport (1080+ px):** all groups fit; "More" button hidden; main sidebar has no scrollbar.
- **Short viewport (700–800 px):** at least one group overflows; "More" button visible with count badge.
- **Very short viewport (≤ 600 px):** several groups overflow; only top + workflow visible; rest in More.
- **Resize from tall → short:** sidebar gracefully demotes the lowest-priority group; nothing flickers.
- **Resize from short → tall:** demoted groups promote back; "More" button disappears when count reaches zero.
- **Deep-link to overflowed page (refresh on `/communication/channels`):** secondary panel auto-opens once on mount.
- **Click an overflowed item from main "More":** navigates; panel stays open.
- **Click a visible item while panel is open:** navigates; panel auto-closes.
- **Esc / × / click-outside:** panel closes.
- **Active "More" button caption:** when on an overflowed page, two-line caption renders correctly; when on a visible page, default caption.
- **Light + dark:** both modes look intentional.
- **RTL:** panel slides in from the end edge correctly.
- **Mobile drawer (`<lg`):** unchanged — still scrolls, no "More" button, no panel.
- **Collapsed sidebar (`lg:w-16`):** "More" button collapses to icon-only; panel still works when opened.

## 11. Resolved decisions (was: open questions)

1. **Drop the count badge when "More" is in active-overflow state** — yes. Active state already implies "you have stuff in More"; the badge becomes noise. Spec §6 reflects this — only the default caption layer carries the badge.
2. **Animate the caption swap** — yes, via a stacked-layer cross-fade (`motion-safe:transition-opacity motion-safe:duration-200`). No animation library, no JS — pure CSS. See §6.
3. **No-permission edge case** — `overflowGroups.length === 0` → no "More" button rendered. Handled by the `hasOverflow` flag inside `useNavOverflow`.
4. **Collapsed-sidebar tooltip** — Radix Tooltip on the icon-only "More" button shows the contextual label (default or active two-line). Spec §6 (collapsed mode notes).

## 12. Out of scope (deferred — restate)

- ⌘K palette content (next plan).
- StatCard extraction (deferred to identity cluster plan).
- Identity cluster page polish.
- User-pinnable groups, drag-to-reorder, persisted overflow preferences.
- Mobile drawer overflow behavior — drawer keeps scrolling.
- AI module UI / mobile (Flutter) port — later phases.

## 13. Scroll-to-top on navigation (bundled small fix)

### Bug

When the user scrolls down a long list page (e.g., audit logs at row 200) and then clicks any nav item — sidebar, breadcrumb, or in-page link — the next page renders at the previous scroll position. React Router preserves the scroll because `<main>` doesn't reset the window scroll. This is a real defect, not a feature.

### Behavior to ship

On every route change (`location.pathname` change), reset `window.scrollTo({ top: 0, left: 0, behavior: 'instant' })`. `'instant'` is a `ScrollBehavior` value that skips animation regardless of `scroll-behavior: smooth` CSS — instant feels right for nav (animated scroll-to-top on nav can feel laggy on fast clicks).

Exceptions:
- **Hash links** (`/page#section`) — leave scroll alone; the browser handles hash anchors.
- **Replace navigation that only changes search params** — leave scroll alone (e.g., paginating within a list with `?page=2` shouldn't dump the user back to the top mid-table). React Router's `location.search` change does NOT trigger the scroll-reset; only `location.pathname` change does.

### Implementation

A new component `boilerplateFE/src/components/common/ScrollToTopOnNavigate.tsx`:

```tsx
import { useEffect } from 'react';
import { useLocation } from 'react-router-dom';

export function ScrollToTopOnNavigate() {
  const { pathname, hash } = useLocation();

  useEffect(() => {
    // Hash anchors handle their own scrolling
    if (hash) return;
    window.scrollTo({ top: 0, left: 0, behavior: 'instant' as ScrollBehavior });
  }, [pathname, hash]);

  return null;
}
```

Mount inside the router tree, once. Locate the existing `<RouterProvider>` / `<Routes>` setup (in `src/routes/routes.tsx` or `src/app/providers/index.tsx`) and add `<ScrollToTopOnNavigate />` as a sibling that renders inside the Router context.

Concretely: this component MUST render inside a Router context to use `useLocation`. Mount it as a child of whatever `<BrowserRouter>` / `<RouterProvider>` wrapper exists, before the route tree.

### Why this lives in this spec

Spec scope is "fix the navigation chrome / experience" — sidebar overflow + scroll-to-top are two parts of "make navigating between pages feel right". Ships in the same plan; one extra task.

### Tasks

The plan grows by one task — call it task 0 since it's the smallest and unblocks nothing else. Order:

0. **`ScrollToTopOnNavigate`** — single small component + 1 import in router file. Self-contained, no dependencies on other tasks.
1. `useNavOverflow` hook.
2. Store + `MoreButton` in main sidebar.
3. `MorePanel` + `MainLayout` mount.
4. Code-review pass.
