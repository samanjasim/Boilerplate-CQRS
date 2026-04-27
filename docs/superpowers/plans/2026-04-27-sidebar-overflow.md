# Sidebar Overflow + Scroll-to-Top Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Apply the spec at `docs/superpowers/specs/2026-04-27-sidebar-overflow-design.md` — group-level sidebar overflow into a "More" secondary panel with active-aware "More" button, plus a small `ScrollToTopOnNavigate` fix for the scroll-position bug across route changes.

**Architecture:**
- New `useNavOverflow` hook measures the nav with a `ResizeObserver` and partitions groups into `visibleGroups` / `overflowGroups` based on available height.
- New `MorePanel` component renders the overflow groups as a floating-glass panel beside the main sidebar, gated on `useUIStore.morePanelOpen` state.
- The "More" button at the bottom of the main sidebar uses two stacked caption layers that cross-fade between default ("More" + count) and active-overflow ("Channels" + "in More · Communication") states.
- A new `ScrollToTopOnNavigate` route-tree component resets `window.scrollTo` on `pathname` change, ignoring hash-anchor and search-param-only navigations.

**Tech Stack:** React 19, TypeScript 5.9, Tailwind CSS 4, Zustand 5, react-router-dom 7, react-i18next 15, lucide-react. **No new dependencies** — collapsed-mode tooltip uses the native `title` attribute (Radix Tooltip would be a new dep).

**Verification model:** Same as prior plans — `npm run build` + `npm run lint` + visual check at `http://localhost:3100`. Sync changed files into `_testJ4visual/_testJ4visual-FE/src/` after each task.

**Working directory for `npm`:** `/Users/samanjasim/Projects/forme/cqrs/boilerplate-cqrs-fe/boilerplateFE`. **Working directory for `git`:** the repo root `/Users/samanjasim/Projects/forme/cqrs/boilerplate-cqrs-fe`.

**Spec deviation flagged for the user:** the spec mentioned a Radix Tooltip on the collapsed-mode More button. Radix Tooltip is NOT a dependency of this project. To avoid pulling in `@radix-ui/react-tooltip`, the plan uses the native `title` attribute instead — visually less polished but a11y-equivalent and zero added bundle weight. Easy to upgrade later by adding the dep + a `Tooltip` UI primitive.

---

## Task 0: `ScrollToTopOnNavigate` component + mount in MainLayout

**Why:** Spec §13. Fixes the existing bug where scrolling down a long page and clicking a NavLink keeps the next page scrolled. Self-contained and unblocks nothing else, ships first to deliver instant UX win.

**Files:**
- Create: `boilerplateFE/src/components/common/ScrollToTopOnNavigate.tsx`
- Modify: `boilerplateFE/src/components/common/index.ts` (export)
- Modify: `boilerplateFE/src/components/layout/MainLayout/MainLayout.tsx` (mount it inside the layout JSX)

- [ ] **Step 0.1: Create `ScrollToTopOnNavigate.tsx`**

Create `boilerplateFE/src/components/common/ScrollToTopOnNavigate.tsx` with this exact content:

```tsx
import { useEffect } from 'react';
import { useLocation } from 'react-router-dom';

/**
 * Resets window scroll to the top on every route change (pathname change).
 * Ignores hash-anchor links — those are the browser's job.
 * Ignores search-param-only changes — paginating within a list shouldn't dump
 * the user back to the top mid-table.
 *
 * Renders nothing. Mount inside the router context.
 */
export function ScrollToTopOnNavigate() {
  const { pathname, hash } = useLocation();

  useEffect(() => {
    if (hash) return;
    window.scrollTo({ top: 0, left: 0, behavior: 'instant' as ScrollBehavior });
  }, [pathname, hash]);

  return null;
}
```

- [ ] **Step 0.2: Re-export from `@/components/common`**

In `boilerplateFE/src/components/common/index.ts`, add:

```ts
export { ScrollToTopOnNavigate } from './ScrollToTopOnNavigate';
```

(Place it alongside the other re-exports in alphabetical order if the file uses that convention, otherwise append to the end.)

- [ ] **Step 0.3: Mount inside `MainLayout`**

In `boilerplateFE/src/components/layout/MainLayout/MainLayout.tsx`, find the import block. Add `ScrollToTopOnNavigate` to the existing `@/components/common` import. The current line is:

```tsx
import { RouteErrorBoundary } from '@/components/common';
```

Replace with:

```tsx
import { RouteErrorBoundary, ScrollToTopOnNavigate } from '@/components/common';
```

Then in the component's return JSX, mount the component immediately AFTER the opening `<div>` tag, BEFORE `<Sidebar />`. Find:

```tsx
return (
  <div
    className="aurora-canvas min-h-screen bg-background overflow-x-clip"
    data-page-style="dense"
  >
    <Sidebar />
```

Insert one line so it becomes:

```tsx
return (
  <div
    className="aurora-canvas min-h-screen bg-background overflow-x-clip"
    data-page-style="dense"
  >
    <ScrollToTopOnNavigate />
    <Sidebar />
```

(`ScrollToTopOnNavigate` returns `null`, so it doesn't affect layout. It just needs to be inside the router context, which `MainLayout` already is via the route tree.)

- [ ] **Step 0.4: Build + lint**

From `boilerplateFE/`:

```bash
npm run build && npm run lint
```

Expected: both pass.

- [ ] **Step 0.5: Visual smoke test**

Sync changes:

```bash
cp /Users/samanjasim/Projects/forme/cqrs/boilerplate-cqrs-fe/boilerplateFE/src/components/common/ScrollToTopOnNavigate.tsx \
   /Users/samanjasim/Projects/forme/cqrs/boilerplate-cqrs-fe/_testJ4visual/_testJ4visual-FE/src/components/common/ScrollToTopOnNavigate.tsx
cp /Users/samanjasim/Projects/forme/cqrs/boilerplate-cqrs-fe/boilerplateFE/src/components/common/index.ts \
   /Users/samanjasim/Projects/forme/cqrs/boilerplate-cqrs-fe/_testJ4visual/_testJ4visual-FE/src/components/common/index.ts
cp /Users/samanjasim/Projects/forme/cqrs/boilerplate-cqrs-fe/boilerplateFE/src/components/layout/MainLayout/MainLayout.tsx \
   /Users/samanjasim/Projects/forme/cqrs/boilerplate-cqrs-fe/_testJ4visual/_testJ4visual-FE/src/components/layout/MainLayout/MainLayout.tsx
```

In Chrome at `http://localhost:3100`: scroll down on a long list page (e.g. `/audit-logs`), then click any sidebar nav item. The new page should render at scroll position 0, not preserve the previous scroll. Pagination within a list (`?page=2` style) should NOT scroll to top — verify on a paginated list.

- [ ] **Step 0.6: Commit**

From the repo root:

```bash
git add boilerplateFE/src/components/common/ScrollToTopOnNavigate.tsx \
        boilerplateFE/src/components/common/index.ts \
        boilerplateFE/src/components/layout/MainLayout/MainLayout.tsx
git commit -m "feat(fe/router): scroll-to-top on pathname change (skips hash anchors and search-param-only nav)"
```

Do NOT include any "Co-Authored-By" trailer.

---

## Task 1: `useNavOverflow` hook

**Why:** Spec §5. Owns the `ResizeObserver` + height-measurement logic. Returns `{ visibleGroups, overflowGroups, activeGroup, activeItem, isActiveOverflowed, hasOverflow, navRef, moreButtonRef }` for the Sidebar to consume.

**Files:**
- Create: `boilerplateFE/src/components/layout/MainLayout/useNavOverflow.ts`

This task creates the hook only. Sidebar still consumes the old `useNavGroups` directly — Task 2 swaps the call site. The hook is testable on its own via the existing test app once it's wired (Task 2).

- [ ] **Step 1.1: Create `useNavOverflow.ts`**

Create `boilerplateFE/src/components/layout/MainLayout/useNavOverflow.ts` with this exact content:

```ts
import { useEffect, useLayoutEffect, useRef, useState } from 'react';
import { useLocation } from 'react-router-dom';

import { selectSidebarCollapsed, useUIStore } from '@/stores';

import { useNavGroups, type SidebarNavGroup, type SidebarNavItem } from './useNavGroups';

interface NavOverflow {
  /** Groups that fit within the visible nav area. */
  visibleGroups: SidebarNavGroup[];
  /** Groups that don't fit — surfaced via the More panel. */
  overflowGroups: SidebarNavGroup[];
  /** The group containing the currently-active route, or undefined if none. */
  activeGroup: SidebarNavGroup | undefined;
  /** The currently-active nav item, or undefined if none. */
  activeItem: SidebarNavItem | undefined;
  /** True when the active route lives inside an overflowed group. */
  isActiveOverflowed: boolean;
  /** True when at least one group is overflowed. */
  hasOverflow: boolean;
  /** Attach to the <nav> element so the hook can measure it. */
  navRef: React.RefObject<HTMLElement | null>;
  /** Attach to the More button so the hook can subtract its height from the available space. */
  moreButtonRef: React.RefObject<HTMLElement | null>;
}

/**
 * Partitions sidebar nav groups into visible vs overflow based on measured space.
 * Renders all groups inside `navRef` once, measures them, and computes how many
 * fit. Re-runs on viewport resize via ResizeObserver.
 *
 * Collapsed-sidebar short-circuit: items are 40x40 each so overflow is rare —
 * skip the work and treat all groups as visible.
 */
export function useNavOverflow(): NavOverflow {
  const groups = useNavGroups();
  const isCollapsed = useUIStore(selectSidebarCollapsed);
  const location = useLocation();

  const navRef = useRef<HTMLElement>(null);
  const moreButtonRef = useRef<HTMLElement>(null);
  const [visibleCount, setVisibleCount] = useState<number>(groups.length);

  // Compute which groups fit. Reads element heights synchronously (useLayoutEffect)
  // so the next paint reflects the partition without a flash.
  useLayoutEffect(() => {
    if (isCollapsed) {
      if (visibleCount !== groups.length) setVisibleCount(groups.length);
      return;
    }
    const nav = navRef.current;
    if (!nav) return;

    const moreButtonHeight = moreButtonRef.current?.offsetHeight ?? 0;
    const available = nav.clientHeight - moreButtonHeight;

    // Each direct child of <nav> is a group wrapper (`<div data-group-id="...">`).
    const groupEls = Array.from(nav.querySelectorAll<HTMLElement>('[data-group-id]'));

    let accumulated = 0;
    let visible = 0;
    for (const el of groupEls) {
      const style = getComputedStyle(el);
      const marginTop = parseFloat(style.marginTop) || 0;
      const marginBottom = parseFloat(style.marginBottom) || 0;
      const totalHeight = el.offsetHeight + marginTop + marginBottom;

      if (accumulated + totalHeight <= available) {
        accumulated += totalHeight;
        visible += 1;
      } else {
        break;
      }
    }

    if (visible !== visibleCount) setVisibleCount(visible);
    // Intentionally NOT including visibleCount in deps — that would loop. We only
    // re-run when the inputs that affect layout change.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [groups, isCollapsed, location.pathname]);

  // Re-measure on viewport / sidebar resize.
  useEffect(() => {
    const nav = navRef.current;
    if (!nav || isCollapsed) return;

    const observer = new ResizeObserver(() => {
      // Trigger the layout effect by bumping a ref-only counter via setState.
      // We re-measure inline rather than calling the layout effect to keep
      // dependency lists tight.
      const moreButtonHeight = moreButtonRef.current?.offsetHeight ?? 0;
      const available = nav.clientHeight - moreButtonHeight;
      const groupEls = Array.from(nav.querySelectorAll<HTMLElement>('[data-group-id]'));

      let accumulated = 0;
      let visible = 0;
      for (const el of groupEls) {
        const style = getComputedStyle(el);
        const marginTop = parseFloat(style.marginTop) || 0;
        const marginBottom = parseFloat(style.marginBottom) || 0;
        const totalHeight = el.offsetHeight + marginTop + marginBottom;

        if (accumulated + totalHeight <= available) {
          accumulated += totalHeight;
          visible += 1;
        } else {
          break;
        }
      }
      setVisibleCount((current) => (current === visible ? current : visible));
    });

    observer.observe(nav);
    return () => observer.disconnect();
  }, [isCollapsed]);

  const visibleGroups = isCollapsed ? groups : groups.slice(0, visibleCount);
  const overflowGroups = isCollapsed ? [] : groups.slice(visibleCount);

  // Find the group + item containing the active route.
  let activeGroup: SidebarNavGroup | undefined;
  let activeItem: SidebarNavItem | undefined;
  for (const group of groups) {
    const match = group.items.find((item) =>
      item.end
        ? location.pathname === item.path
        : location.pathname === item.path || location.pathname.startsWith(item.path + '/')
    );
    if (match) {
      activeGroup = group;
      activeItem = match;
      break;
    }
  }

  const isActiveOverflowed = activeGroup ? overflowGroups.includes(activeGroup) : false;
  const hasOverflow = overflowGroups.length > 0;

  return {
    visibleGroups,
    overflowGroups,
    activeGroup,
    activeItem,
    isActiveOverflowed,
    hasOverflow,
    navRef,
    moreButtonRef,
  };
}
```

Notes:
- The hook walks `groups` (the full list) when computing `activeGroup` so the active state stays correct even when the active group is currently overflowed.
- The `useLayoutEffect` runs after every render where inputs change; the `ResizeObserver` covers viewport-size changes that don't re-render React.
- The `// eslint-disable-next-line react-hooks/exhaustive-deps` is required because including `visibleCount` would infinite-loop. The comment documents intent.
- The hook intentionally returns refs typed as `React.RefObject<HTMLElement | null>` so consumers can attach them to any HTML element. (The current `<nav>` is `HTMLElement`-compatible.)

- [ ] **Step 1.2: Build + lint**

From `boilerplateFE/`:

```bash
npm run build && npm run lint
```

Expected: build passes (no consumers yet — type-only). Lint passes (the eslint-disable comment is documented).

- [ ] **Step 1.3: Commit**

From the repo root:

```bash
git add boilerplateFE/src/components/layout/MainLayout/useNavOverflow.ts
git commit -m "feat(fe/sidebar): useNavOverflow hook — measure-based group partition for overflow handling"
```

---

## Task 2: Store additions + integrate `useNavOverflow` into Sidebar + render `MoreButton`

**Why:** Spec §5.2 + §6 + §4.4. Adds `morePanelOpen` to the store, swaps `useNavGroups` → `useNavOverflow` in Sidebar, renders the visible groups, and pins a "More" button at the bottom of the nav with the cross-fading caption layers.

**Files:**
- Modify: `boilerplateFE/src/stores/ui.store.ts` — add `morePanelOpen` slice + selector
- Modify: `boilerplateFE/src/components/layout/MainLayout/Sidebar.tsx` — swap hook, render visible groups, mount More button

- [ ] **Step 2.1: Add `morePanelOpen` to the UI store**

In `boilerplateFE/src/stores/ui.store.ts`, find the `interface UIState` block. Add a new line:

```ts
interface UIState {
  theme: Theme;
  language: Language;
  sidebarOpen: boolean;
  sidebarCollapsed: boolean;
  morePanelOpen: boolean;        // NEW
  activeModal: string | null;
  modalData: unknown;
  activeTenantId: string | null;
  tenantSlug: string | null;
  backNavigation: BackNavigation | null;
}
```

Find `interface UIActions`. Add:

```ts
interface UIActions {
  setTheme: (theme: Theme) => void;
  setLanguage: (language: Language) => void;
  toggleSidebar: () => void;
  setSidebarOpen: (open: boolean) => void;
  toggleSidebarCollapse: () => void;
  setMorePanelOpen: (open: boolean) => void;     // NEW
  toggleMorePanel: () => void;                    // NEW
  openModal: (modalId: string, data?: unknown) => void;
  closeModal: () => void;
  setActiveTenantId: (tenantId: string | null) => void;
  setTenantSlug: (slug: string | null) => void;
  setBackNavigation: (nav: BackNavigation | null) => void;
}
```

Find the initial state inside `create<UIStore>()(persist(...))`. Add `morePanelOpen: false,` adjacent to `sidebarOpen`:

```ts
sidebarOpen: false,
sidebarCollapsed: false,
morePanelOpen: false,    // NEW — never persisted; always closed on fresh load
```

Find the actions block. Add (near `toggleSidebarCollapse`):

```ts
toggleSidebarCollapse: () =>
  set((state) => ({ sidebarCollapsed: !state.sidebarCollapsed })),

setMorePanelOpen: (morePanelOpen) => set({ morePanelOpen }),       // NEW
toggleMorePanel: () =>                                              // NEW
  set((state) => ({ morePanelOpen: !state.morePanelOpen })),
```

`morePanelOpen` is intentionally NOT included in the `partialize` block — it always starts as `false` on a fresh page load. (The "auto-open on deep-link" behavior in Task 3 sets it `true` when needed.)

Find the selectors block at the bottom of the file. Add:

```ts
export const selectMorePanelOpen = (state: UIStore) => state.morePanelOpen;
```

(Place it in alphabetical-ish order — adjacent to `selectSidebarOpen`.)

- [ ] **Step 2.2: Update Sidebar imports**

In `boilerplateFE/src/components/layout/MainLayout/Sidebar.tsx`, find the imports block:

```tsx
import { useEffect } from 'react';
import { NavLink, useLocation } from 'react-router-dom';
import { ChevronsLeft, ChevronsRight } from 'lucide-react';

import { cn } from '@/lib/utils';
import {
  selectSidebarCollapsed,
  selectSidebarOpen,
  selectUser,
  useAuthStore,
  useUIStore,
} from '@/stores';
import { useNavGroups } from './useNavGroups';
```

Replace with:

```tsx
import { useEffect, useRef } from 'react';
import { NavLink, useLocation } from 'react-router-dom';
import { ChevronsLeft, ChevronsRight, MoreHorizontal } from 'lucide-react';
import { useTranslation } from 'react-i18next';

import { cn } from '@/lib/utils';
import {
  selectMorePanelOpen,
  selectSidebarCollapsed,
  selectSidebarOpen,
  selectUser,
  useAuthStore,
  useUIStore,
} from '@/stores';
import { useNavOverflow } from './useNavOverflow';
```

Notes: dropped the now-unused `useNavGroups` import (replaced by `useNavOverflow`); added `MoreHorizontal` for the More button icon and `useTranslation` for the `nav.more.*` labels. `useRef` is for the auto-open one-shot guard.

- [ ] **Step 2.3: Replace the data-flow inside `Sidebar()`**

In `boilerplateFE/src/components/layout/MainLayout/Sidebar.tsx`, find the existing hooks block at the top of the component:

```tsx
export function Sidebar() {
  const isCollapsed = useUIStore(selectSidebarCollapsed);
  const toggleCollapse = useUIStore((state) => state.toggleSidebarCollapse);
  const user = useAuthStore(selectUser);
  const groups = useNavGroups();
  const sidebarOpen = useUIStore(selectSidebarOpen);
  const setSidebarOpen = useUIStore((state) => state.setSidebarOpen);
  const location = useLocation();
```

Replace with:

```tsx
export function Sidebar() {
  const { t } = useTranslation();
  const isCollapsed = useUIStore(selectSidebarCollapsed);
  const toggleCollapse = useUIStore((state) => state.toggleSidebarCollapse);
  const user = useAuthStore(selectUser);
  const sidebarOpen = useUIStore(selectSidebarOpen);
  const setSidebarOpen = useUIStore((state) => state.setSidebarOpen);
  const morePanelOpen = useUIStore(selectMorePanelOpen);
  const setMorePanelOpen = useUIStore((state) => state.setMorePanelOpen);
  const toggleMorePanel = useUIStore((state) => state.toggleMorePanel);
  const location = useLocation();

  const {
    visibleGroups,
    overflowGroups,
    activeGroup,
    activeItem,
    isActiveOverflowed,
    hasOverflow,
    navRef,
    moreButtonRef,
  } = useNavOverflow();

  // Auto-open MorePanel once on first mount if the active route is overflowed
  // (deep-link / page refresh into an overflowed page).
  const autoOpenedRef = useRef(false);
  useEffect(() => {
    if (!autoOpenedRef.current && isActiveOverflowed) {
      setMorePanelOpen(true);
      autoOpenedRef.current = true;
    }
  }, [isActiveOverflowed, setMorePanelOpen]);

  // Auto-close MorePanel when the user navigates to a page that's NOT in overflow.
  useEffect(() => {
    if (morePanelOpen && !isActiveOverflowed) {
      setMorePanelOpen(false);
    }
  }, [location.pathname, isActiveOverflowed, morePanelOpen, setMorePanelOpen]);
```

Then keep the existing two effects (route-change auto-close of mobile drawer, Esc-close of mobile drawer) unchanged. Below those, the rest of the existing component body continues with the JSX render.

- [ ] **Step 2.4: Render visible groups + More button in the JSX**

Find the existing `<nav>` block (around lines 86-142 of the post-shell-refresh Sidebar):

```tsx
<nav className="flex-1 overflow-y-auto px-3 pt-2 pb-3">
  {groups.map((group, groupIndex) => (
    <div
      key={group.id}
      className={cn(
        groupIndex > 0 && (
          isCollapsed
            ? 'mx-3 my-2 border-t border-border/40'
            : 'mt-4'
        )
      )}
    >
      ...
    </div>
  ))}
</nav>
```

Replace the entire `<nav>` element (and the More button immediately after it) with:

```tsx
<nav
  ref={navRef}
  className="flex-1 overflow-hidden px-3 pt-2 pb-3 flex flex-col"
>
  <div className="flex-1">
    {visibleGroups.map((group, groupIndex) => (
      <div
        key={group.id}
        data-group-id={group.id}
        className={cn(
          groupIndex > 0 && (
            isCollapsed
              ? 'mx-3 my-2 border-t border-border/40'
              : 'mt-4'
          )
        )}
      >
        {!isCollapsed && group.label && (
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
                    'flex items-center gap-2.5 rounded-[10px] h-10 px-3 text-sm motion-safe:transition-all motion-safe:duration-150 cursor-pointer',
                    isCollapsed && 'justify-center px-0',
                    isActive ? 'pill-active' : 'state-hover'
                  )
                }
              >
                {({ isActive }) => (
                  <>
                    <item.icon
                      className={cn(
                        'h-[18px] w-[18px] shrink-0',
                        isActive && 'drop-shadow-[0_0_6px_color-mix(in_srgb,var(--color-primary)_45%,transparent)]'
                      )}
                    />
                    {!isCollapsed && <span className="flex-1">{item.label}</span>}
                    {!isCollapsed && item.badge != null && (
                      <span className="flex h-5 min-w-5 items-center justify-center rounded-full btn-primary-gradient glow-primary-sm px-1.5 text-[10px] font-bold text-primary-foreground font-mono">
                        {item.badge > 99 ? '99+' : item.badge}
                      </span>
                    )}
                  </>
                )}
              </NavLink>
            </li>
          ))}
        </ul>
      </div>
    ))}
  </div>

  {hasOverflow && (
    <button
      ref={moreButtonRef as React.RefObject<HTMLButtonElement>}
      type="button"
      onClick={toggleMorePanel}
      aria-pressed={morePanelOpen}
      aria-label={
        isActiveOverflowed && activeItem && activeGroup
          ? t('nav.more.inMore', { group: activeGroup.label ?? '' })
          : t('nav.more.label')
      }
      title={
        isCollapsed
          ? isActiveOverflowed && activeItem && activeGroup
            ? `${activeItem.label} · ${t('nav.more.inMore', { group: activeGroup.label ?? '' })}`
            : t('nav.more.label')
          : undefined
      }
      className={cn(
        'relative mt-2 flex items-center rounded-[10px] h-10 overflow-hidden',
        'motion-safe:transition-all motion-safe:duration-150',
        isCollapsed ? 'justify-center px-0 w-full' : 'px-3',
        isActiveOverflowed
          ? 'pill-active border border-transparent'
          : 'border border-foreground/10 bg-foreground/5 hover:bg-foreground/10'
      )}
    >
      {/* Default caption layer — visible when NOT active-overflowed */}
      <span
        aria-hidden={isActiveOverflowed}
        className={cn(
          'absolute inset-0 flex items-center',
          isCollapsed ? 'justify-center' : 'gap-2.5 px-3',
          'motion-safe:transition-opacity motion-safe:duration-200',
          isActiveOverflowed ? 'opacity-0 pointer-events-none' : 'opacity-100'
        )}
      >
        <MoreHorizontal className="h-[18px] w-[18px] shrink-0 opacity-70" />
        {!isCollapsed && (
          <>
            <span className="flex-1 text-start text-sm">{t('nav.more.label')}</span>
            <span className="rounded-full bg-primary/20 px-1.5 py-0.5 text-[10px] font-mono font-bold text-primary">
              {overflowGroups.length}
            </span>
          </>
        )}
      </span>

      {/* Active caption layer — visible when current page is in overflow */}
      {isActiveOverflowed && activeItem && activeGroup && (
        <span
          className={cn(
            'absolute inset-0 flex items-center',
            isCollapsed ? 'justify-center' : 'gap-2.5 px-3',
            'motion-safe:transition-opacity motion-safe:duration-200',
            'opacity-100'
          )}
        >
          <activeItem.icon
            className={cn(
              'h-[18px] w-[18px] shrink-0',
              'drop-shadow-[0_0_6px_color-mix(in_srgb,var(--color-primary)_45%,transparent)]'
            )}
          />
          {!isCollapsed && (
            <div className="flex flex-col gap-0 leading-tight text-start min-w-0">
              <span className="text-[12px] font-medium truncate">{activeItem.label}</span>
              <span className="text-[8px] uppercase tracking-[0.12em] opacity-70 truncate">
                {t('nav.more.inMore', { group: activeGroup.label ?? '' })}
              </span>
            </div>
          )}
        </span>
      )}

      {/* Invisible spacer — keeps button's intrinsic width stable when expanded */}
      {!isCollapsed && (
        <span aria-hidden className="invisible flex items-center gap-2.5">
          <span className="h-[18px] w-[18px]" />
          <span className="text-sm">_</span>
        </span>
      )}
    </button>
  )}
</nav>
```

Notes on the changes:
- `<nav>` swaps `overflow-y-auto` → `overflow-hidden` and adds `flex flex-col`. The visible groups live in a `<div className="flex-1">` wrapper so they take all remaining height; the More button sits below them. We never want the visible nav to scroll — that's the whole point of the overflow feature.
- Each group gets `data-group-id={group.id}` so the hook's measurement query can find them.
- The More button only renders when `hasOverflow` is true (the hook returns `false` when collapsed; in that mode the More button never appears).
- The `title` attribute provides a native browser tooltip on the collapsed button. Visually less polished than Radix Tooltip but a11y-equivalent and zero new deps.
- `isCollapsed` rendering: button is icon-only, centered, no caption text, no count badge (badge would be invisible without a label). The active-overflowed state still swaps the icon to the active item's icon for the "you are here" cue.

- [ ] **Step 2.5: Build + lint**

From `boilerplateFE/`:

```bash
npm run build && npm run lint
```

Expected: build passes; lint passes (or only one expected warning from the eslint-disable comment in `useNavOverflow`).

- [ ] **Step 2.6: Visual sanity test (no MorePanel yet)**

Sync changes:

```bash
cp /Users/samanjasim/Projects/forme/cqrs/boilerplate-cqrs-fe/boilerplateFE/src/stores/ui.store.ts \
   /Users/samanjasim/Projects/forme/cqrs/boilerplate-cqrs-fe/_testJ4visual/_testJ4visual-FE/src/stores/ui.store.ts
cp /Users/samanjasim/Projects/forme/cqrs/boilerplate-cqrs-fe/boilerplateFE/src/components/layout/MainLayout/Sidebar.tsx \
   /Users/samanjasim/Projects/forme/cqrs/boilerplate-cqrs-fe/_testJ4visual/_testJ4visual-FE/src/components/layout/MainLayout/Sidebar.tsx
```

Reload `http://localhost:3100` at a tall viewport (1080+ px). Expected: all groups fit; no More button (everything visible).

Resize the browser window to ~700px tall. Expected: lowest-priority groups demote; "More" button appears at the bottom of the sidebar showing the count of overflowed groups.

Click "More" → it toggles `morePanelOpen` in the store but nothing renders yet (Task 3 lands the panel). For now, verify in DevTools that the click flips the state.

- [ ] **Step 2.7: Commit**

From the repo root:

```bash
git add boilerplateFE/src/stores/ui.store.ts \
        boilerplateFE/src/components/layout/MainLayout/Sidebar.tsx
git commit -m "feat(fe/sidebar): morePanelOpen store slice + More button with cross-fading caption layers"
```

---

## Task 3: `MorePanel` component + mount in MainLayout + AR/KU translations

**Why:** Spec §5.5 + §7. Renders the secondary panel that displays the overflow groups when `morePanelOpen` is true. Closes on Esc / click-outside / nav-into-visible. Adds the `nav.more.*` keys to en + ar + ku.

**Files:**
- Create: `boilerplateFE/src/components/layout/MainLayout/MorePanel.tsx`
- Modify: `boilerplateFE/src/components/layout/MainLayout/MainLayout.tsx` — mount `<MorePanel />` next to `<Sidebar />`
- Modify: `boilerplateFE/src/components/layout/MainLayout/Sidebar.tsx` — add `data-shell="sidebar"` for the click-outside check
- Modify: `boilerplateFE/src/i18n/locales/en/translation.json`, `ar/translation.json`, `ku/translation.json` — add `nav.more.*` keys

- [ ] **Step 3.1: Create `MorePanel.tsx`**

Create `boilerplateFE/src/components/layout/MainLayout/MorePanel.tsx`:

```tsx
import { useEffect } from 'react';
import { NavLink } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { X } from 'lucide-react';

import { cn } from '@/lib/utils';
import {
  selectMorePanelOpen,
  selectSidebarCollapsed,
  useUIStore,
} from '@/stores';

import { useNavOverflow } from './useNavOverflow';

export function MorePanel() {
  const { t } = useTranslation();
  const morePanelOpen = useUIStore(selectMorePanelOpen);
  const setMorePanelOpen = useUIStore((state) => state.setMorePanelOpen);
  const isCollapsed = useUIStore(selectSidebarCollapsed);
  const { overflowGroups, hasOverflow } = useNavOverflow();

  // Esc closes the panel.
  useEffect(() => {
    if (!morePanelOpen) return;
    const onKeyDown = (e: KeyboardEvent) => {
      if (e.key === 'Escape') setMorePanelOpen(false);
    };
    document.addEventListener('keydown', onKeyDown);
    return () => document.removeEventListener('keydown', onKeyDown);
  }, [morePanelOpen, setMorePanelOpen]);

  // Click-outside closes the panel. The sidebar and the panel both carry
  // data-shell attributes so we can detect clicks "on chrome" cheaply.
  useEffect(() => {
    if (!morePanelOpen) return;
    const onPointerDown = (e: MouseEvent) => {
      const target = e.target as HTMLElement | null;
      if (!target) return;
      const onChrome =
        target.closest('[data-shell="sidebar"]') ||
        target.closest('[data-shell="more-panel"]') ||
        target.closest('[data-shell="header"]');
      if (!onChrome) {
        setMorePanelOpen(false);
      }
    };
    document.addEventListener('mousedown', onPointerDown);
    return () => document.removeEventListener('mousedown', onPointerDown);
  }, [morePanelOpen, setMorePanelOpen]);

  // No render when there's nothing to overflow.
  if (!hasOverflow) return null;

  return (
    <aside
      data-shell="more-panel"
      aria-hidden={!morePanelOpen}
      className={cn(
        'fixed top-3.5 bottom-3.5 z-[35] w-[220px] flex-col rounded-[18px]',
        'surface-floating',
        'motion-safe:transition-all motion-safe:duration-300',
        // Position: beside the main sidebar (sidebar width + 14px sidebar margin + 14px gap)
        isCollapsed
          ? 'lg:ltr:left-[calc(4rem+1.75rem+0.875rem)] lg:rtl:right-[calc(4rem+1.75rem+0.875rem)]'
          : 'lg:ltr:left-[calc(15rem+1.75rem+0.875rem)] lg:rtl:right-[calc(15rem+1.75rem+0.875rem)]',
        // Open / closed state — slide + fade
        morePanelOpen
          ? 'opacity-100 translate-x-0 flex'
          : 'opacity-0 pointer-events-none ltr:-translate-x-3 rtl:translate-x-3 hidden',
        // Desktop only — mobile drawer already shows everything with scroll
        'max-lg:hidden'
      )}
    >
      <div className="flex h-9 items-center justify-between border-b border-border/40 px-4 pt-3 pb-2 shrink-0">
        <span className="text-xs font-semibold uppercase tracking-[0.1em] text-muted-foreground">
          {t('nav.more.panelTitle')}
        </span>
        <button
          type="button"
          onClick={() => setMorePanelOpen(false)}
          aria-label={t('nav.more.close')}
          className="flex h-6 w-6 items-center justify-center rounded-md hover:bg-foreground/5 motion-safe:transition-colors motion-safe:duration-150"
        >
          <X className="h-3.5 w-3.5" />
        </button>
      </div>
      <nav className="flex-1 overflow-y-auto px-3 pt-2 pb-3">
        {overflowGroups.map((group, idx) => (
          <div
            key={group.id}
            data-group-id={group.id}
            className={cn(idx > 0 && 'mt-4')}
          >
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
                    {({ isActive }) => (
                      <>
                        <item.icon
                          className={cn(
                            'h-[18px] w-[18px] shrink-0',
                            isActive && 'drop-shadow-[0_0_6px_color-mix(in_srgb,var(--color-primary)_45%,transparent)]'
                          )}
                        />
                        <span className="flex-1">{item.label}</span>
                        {item.badge != null && (
                          <span className="flex h-5 min-w-5 items-center justify-center rounded-full btn-primary-gradient glow-primary-sm px-1.5 text-[10px] font-bold text-primary-foreground font-mono">
                            {item.badge > 99 ? '99+' : item.badge}
                          </span>
                        )}
                      </>
                    )}
                  </NavLink>
                </li>
              ))}
            </ul>
          </div>
        ))}
      </nav>
    </aside>
  );
}
```

Notes:
- The panel calls `useNavOverflow()` itself rather than receiving overflow data as props. The hook is cheap (memoized via React's rendering) and this keeps `MorePanel` self-contained.
- `z-[35]` puts it above main content (`z-30`) but below modals. The mobile drawer backdrop is also `z-30` — they never coexist (panel is `max-lg:hidden`).
- Click-outside check excludes the Header (`[data-shell="header"]`) so clicking the search bar / avatar / theme toggle in the header doesn't accidentally close the panel.
- The panel gets `data-group-id` on each group wrapper so future code (e.g., a "scroll active group into view" effect) can find them. Not used in this task; kept for parity with the main sidebar.

- [ ] **Step 3.2: Add `data-shell="sidebar"` and `data-shell="header"` attributes**

In `boilerplateFE/src/components/layout/MainLayout/Sidebar.tsx`, find the `<aside>` opening tag and add `data-shell="sidebar"` immediately after `className`:

```tsx
<aside
  data-shell="sidebar"
  className={cn(
    'fixed top-3.5 bottom-3.5 z-40 flex flex-col rounded-[18px]',
    ...
  )}
>
```

In `boilerplateFE/src/components/layout/MainLayout/Header.tsx`, find the `<header>` opening tag and add `data-shell="header"`:

```tsx
<header
  data-shell="header"
  className={cn(
    'fixed top-3.5 z-30 h-12 flex items-center gap-2 rounded-2xl px-3',
    ...
  )}
>
```

These attributes are read by `MorePanel`'s click-outside detection.

- [ ] **Step 3.3: Mount `<MorePanel />` inside `MainLayout`**

In `boilerplateFE/src/components/layout/MainLayout/MainLayout.tsx`, add `MorePanel` to the existing `'./MorePanel'` import. The current imports of `Header` and `Sidebar` are:

```tsx
import { Header } from './Header';
import { Sidebar } from './Sidebar';
```

Replace with:

```tsx
import { Header } from './Header';
import { MorePanel } from './MorePanel';
import { Sidebar } from './Sidebar';
```

Then in the JSX, find the spot where `<Sidebar />` and `<Header />` are mounted (immediately after `<ScrollToTopOnNavigate />` from Task 0):

```tsx
<ScrollToTopOnNavigate />
<Sidebar />
<Header />
```

Insert `<MorePanel />` between `<Sidebar />` and `<Header />`:

```tsx
<ScrollToTopOnNavigate />
<Sidebar />
<MorePanel />
<Header />
```

- [ ] **Step 3.4: Add `nav.more.*` keys to all three locale files**

In `boilerplateFE/src/i18n/locales/en/translation.json`, find the `"nav": { ... }` block. Add a `"more"` sub-object after the `"toggle"` block (or wherever the `nav` block currently ends):

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

Then mirror the same structure in `boilerplateFE/src/i18n/locales/ar/translation.json`. The `nav` block in AR currently has `dashboard` etc. but is missing `groups.*` and `toggle.*`. **For this plan, only add the `more` block** — the broader AR/KU backfill of `groups.*` / `toggle.*` is a tracked deferred item from earlier specs and out of scope here. So in AR:

```json
"nav": {
  ...existing AR nav keys unchanged...
  "more": {
    "label": "المزيد",
    "inMore": "في المزيد · {{group}}",
    "panelTitle": "مجموعات إضافية",
    "close": "إغلاق المجموعات الإضافية"
  }
}
```

In `boilerplateFE/src/i18n/locales/ku/translation.json`:

```json
"nav": {
  ...existing KU nav keys unchanged...
  "more": {
    "label": "زیاتر",
    "inMore": "لە زیاتر · {{group}}",
    "panelTitle": "گرووپە زیادەکان",
    "close": "داخستنی گرووپە زیادەکان"
  }
}
```

Use the Edit tool for each — open the file, find the closing `}` of the `nav` block, add a comma after the previous key, and insert the new `more` block before the closing `}`. Validate the JSON parses (`python3 -c "import json; json.load(open('<path>'))"`) before committing.

- [ ] **Step 3.5: Build + lint**

From `boilerplateFE/`:

```bash
npm run build && npm run lint
```

Expected: build passes; lint passes.

- [ ] **Step 3.6: Visual verification — full feature**

Sync changes:

```bash
cp /Users/samanjasim/Projects/forme/cqrs/boilerplate-cqrs-fe/boilerplateFE/src/components/layout/MainLayout/MorePanel.tsx \
   /Users/samanjasim/Projects/forme/cqrs/boilerplate-cqrs-fe/_testJ4visual/_testJ4visual-FE/src/components/layout/MainLayout/MorePanel.tsx
cp /Users/samanjasim/Projects/forme/cqrs/boilerplate-cqrs-fe/boilerplateFE/src/components/layout/MainLayout/MainLayout.tsx \
   /Users/samanjasim/Projects/forme/cqrs/boilerplate-cqrs-fe/_testJ4visual/_testJ4visual-FE/src/components/layout/MainLayout/MainLayout.tsx
cp /Users/samanjasim/Projects/forme/cqrs/boilerplate-cqrs-fe/boilerplateFE/src/components/layout/MainLayout/Sidebar.tsx \
   /Users/samanjasim/Projects/forme/cqrs/boilerplate-cqrs-fe/_testJ4visual/_testJ4visual-FE/src/components/layout/MainLayout/Sidebar.tsx
cp /Users/samanjasim/Projects/forme/cqrs/boilerplate-cqrs-fe/boilerplateFE/src/components/layout/MainLayout/Header.tsx \
   /Users/samanjasim/Projects/forme/cqrs/boilerplate-cqrs-fe/_testJ4visual/_testJ4visual-FE/src/components/layout/MainLayout/Header.tsx
cp /Users/samanjasim/Projects/forme/cqrs/boilerplate-cqrs-fe/boilerplateFE/src/i18n/locales/en/translation.json \
   /Users/samanjasim/Projects/forme/cqrs/boilerplate-cqrs-fe/_testJ4visual/_testJ4visual-FE/src/i18n/locales/en/translation.json
cp /Users/samanjasim/Projects/forme/cqrs/boilerplate-cqrs-fe/boilerplateFE/src/i18n/locales/ar/translation.json \
   /Users/samanjasim/Projects/forme/cqrs/boilerplate-cqrs-fe/_testJ4visual/_testJ4visual-FE/src/i18n/locales/ar/translation.json
cp /Users/samanjasim/Projects/forme/cqrs/boilerplate-cqrs-fe/boilerplateFE/src/i18n/locales/ku/translation.json \
   /Users/samanjasim/Projects/forme/cqrs/boilerplate-cqrs-fe/_testJ4visual/_testJ4visual-FE/src/i18n/locales/ku/translation.json
```

Verify each scenario at `http://localhost:3100`:

1. **Tall viewport (1080+ px):** all groups fit; no "More" button visible; no scroll on the sidebar.
2. **Short viewport (~700 px):** "More" button appears at the bottom of the main sidebar with `[N]` count badge. Click it → MorePanel slides in beside the sidebar with the overflowed groups.
3. **Click an overflow item in MorePanel:** navigates; panel stays open; "More" button transitions to active-overflow state with the leaf's icon + the two-line caption.
4. **Click a visible-group nav item while panel is open:** navigates; panel auto-closes.
5. **Esc key while panel is open:** panel closes.
6. **Click main content area while panel is open:** panel closes.
7. **Click on Header (search bar, theme toggle, avatar) while panel is open:** panel does NOT close.
8. **Refresh on an overflowed page (deep-link):** MorePanel auto-opens once on first paint.
9. **Resize from 700 → 1100 px:** "More" button disappears; panel auto-closes (because no overflow exists); previously-overflowed groups become visible.
10. **Light + dark modes:** both look correct.
11. **RTL (switch to Arabic):** panel slides in from the end (right) edge; AR translations render correctly.
12. **Mobile (`<lg` viewport, 768 px):** sidebar drawer still scrolls all groups; no "More" button; no MorePanel.
13. **Collapsed sidebar (click chevron):** "More" button shrinks to icon-only; native browser tooltip appears on hover (`title` attr).

- [ ] **Step 3.7: Commit**

From the repo root:

```bash
git add boilerplateFE/src/components/layout/MainLayout/MorePanel.tsx \
        boilerplateFE/src/components/layout/MainLayout/MainLayout.tsx \
        boilerplateFE/src/components/layout/MainLayout/Sidebar.tsx \
        boilerplateFE/src/components/layout/MainLayout/Header.tsx \
        boilerplateFE/src/i18n/locales/en/translation.json \
        boilerplateFE/src/i18n/locales/ar/translation.json \
        boilerplateFE/src/i18n/locales/ku/translation.json
git commit -m "feat(fe/sidebar): MorePanel with click-outside / Esc / nav-close + nav.more.* in en/ar/ku"
```

---

## Task 4: Code-review pass

**Why:** Standard cadence — catch regressions before declaring done.

- [ ] **Step 4.1: Dispatch code-reviewer subagent**

Invoke `superpowers:code-reviewer` with:

> Review all commits since `d1a05fbf` on `fe/redesign-phase-1`. Four commits to verify against `docs/superpowers/specs/2026-04-27-sidebar-overflow-design.md`:
>
> 1. `feat(fe/router): scroll-to-top on pathname change ...` (Task 0)
> 2. `feat(fe/sidebar): useNavOverflow hook ...` (Task 1)
> 3. `feat(fe/sidebar): morePanelOpen store slice + More button with cross-fading caption layers` (Task 2)
> 4. `feat(fe/sidebar): MorePanel with click-outside / Esc / nav-close + nav.more.* in en/ar/ku` (Task 3)
>
> Specific things to check:
> 1. **CLAUDE.md Frontend Rules** — no hardcoded `primary-{shade}` classes; no `dark:` overrides on primary colors. Tokens via `hsl(var(--primary))` / `var(--color-primary*)` only.
> 2. **`useNavOverflow` measurement** — confirm both the `useLayoutEffect` and the `ResizeObserver` paths produce identical partitioning. The eslint-disable comment is justified (would loop without it).
> 3. **Active-state detection** — `activeGroup` walks the FULL groups list, not just visible. Sub-paths trigger active too (`item.end ? exact : startsWith`).
> 4. **Auto-open one-shot** — fires only once per Sidebar mount via `useRef` flag. Does NOT re-fire on subsequent navigations.
> 5. **Auto-close on navigation** — only triggers when `morePanelOpen && !isActiveOverflowed`. Doesn't churn on every re-render.
> 6. **Cross-fade caption layers** — both layers are absolutely positioned over the button; only one has `opacity-100` at a time; button height is held by an invisible spacer.
> 7. **Click-outside** — checks `data-shell="sidebar"`, `data-shell="more-panel"`, `data-shell="header"`. Clicking inside any chrome doesn't close the panel.
> 8. **`type="button"`** on every state-mutating button (More button, panel close button).
> 9. **`motion-safe:`** on every transition.
> 10. **RTL** — panel translates the right way in RTL (`rtl:translate-x-3`); position offset uses paired `lg:ltr:left-[calc(...)] lg:rtl:right-[calc(...)]`.
> 11. **Mobile (`<lg`)** — MorePanel never renders (`max-lg:hidden`); More button only renders when `hasOverflow` and `!isCollapsed` short-circuit applies (collapsed sidebar = no overflow detection ⇒ no More button).
> 12. **JSON validity** — all three locale files parse; none of the prior keys were modified.
> 13. **`ScrollToTopOnNavigate`** — fires on `pathname` change but skips hash anchors and preserves scroll on search-param-only nav.
>
> Do NOT flag the broader AR/KU backfill (`nav.toggle.*`, `nav.groups.*`) as missing — that's an explicit deferred item from earlier specs.

Address findings with follow-up commits before considering the plan done.

---

## Self-Review

**Spec coverage** (`docs/superpowers/specs/2026-04-27-sidebar-overflow-design.md`):

- §4.1–§4.5 UX behavior (visible vs overflowed, open/close, button shapes, panel shape) → Tasks 2 + 3.
- §5.1–§5.6 Architecture (hook, panel, button, store, sidebar render, click-outside, auto-open, auto-close-on-nav) → Tasks 1, 2, 3.
- §6 Styling specifics (cross-fade caption, button + panel) → Tasks 2 + 3.
- §7 i18n keys (en + ar + ku) → Task 3.
- §8 Light + dark coverage → handled by the `surface-floating` and `pill-active` utilities already shipped; no new code needed.
- §9 Implementation order matches Tasks 0 → 4.
- §10 Verification checklist mirrored in Step 3.6.
- §11 Resolved decisions all reflected in code (drop badge in active state, cross-fade duration 200 ms, `title` attr instead of Radix Tooltip on collapsed mode).
- §13 Scroll-to-top fix → Task 0.

**Spec deviation** (intentional, flagged in plan header): Radix Tooltip on collapsed-mode More button → replaced with native `title` attribute to avoid a new dependency. Easy to upgrade later.

**Placeholder scan:** No `TBD` / `TODO` / vague guidance. Every step has concrete code or commands.

**Type consistency:**
- `useNavOverflow` returns the same `NavOverflow` shape consumed by Sidebar (Task 2) and MorePanel (Task 3).
- `morePanelOpen` / `setMorePanelOpen` / `toggleMorePanel` / `selectMorePanelOpen` consistent across `ui.store.ts`, Sidebar, MorePanel.
- `data-shell` attribute values (`"sidebar"`, `"more-panel"`, `"header"`) consistent across MorePanel's click-outside check and the three component opening tags.
- `data-group-id` on group wrappers in both Sidebar (visible groups, used for measurement) and MorePanel (overflow groups, for parity).

**Verification model:** Each task ends with build + lint + visual check + commit. The full visual suite in Step 3.6 covers 13 scenarios spanning desktop expanded, desktop collapsed, mobile drawer, light + dark, RTL, deep-link, viewport-resize.

**Out of scope (deferred elsewhere — re-state):**
- ⌘K palette content (next plan).
- StatCard extraction (identity cluster plan).
- Identity cluster page polish.
- AR/KU backfill of `nav.toggle.*` and `nav.groups.*` from prior specs.
- User-pinnable / drag-to-reorder overflow groups.
- Mobile drawer overflow (drawer keeps scrolling).
