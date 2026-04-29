import { createContext, useContext, useEffect, useLayoutEffect, useRef, useState } from 'react';
import { useLocation } from 'react-router-dom';

import { selectSidebarCollapsed, useUIStore } from '@/stores';

import { useNavGroups, type SidebarNavGroup, type SidebarNavItem } from './useNavGroups';

export interface NavOverflow {
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
 * Sidebar (which attaches the measurement refs) and MorePanel (which renders
 * overflow groups) MUST share one hook instance — a separate instance has no
 * way to compute the partition. This context distributes the single result.
 */
export const NavOverflowContext = createContext<NavOverflow | null>(null);

export function useNavOverflow(): NavOverflow {
  const ctx = useContext(NavOverflowContext);
  if (!ctx) {
    throw new Error('useNavOverflow must be used within <NavOverflowProvider>');
  }
  return ctx;
}

/**
 * Internal — call only from <NavOverflowProvider>. Measures how many groups
 * fit in the available nav height and partitions them into visible / overflow.
 */
export function useNavOverflowImpl(): NavOverflow {
  const groups = useNavGroups();
  const isCollapsed = useUIStore(selectSidebarCollapsed);
  const location = useLocation();

  const navRef = useRef<HTMLElement>(null);
  const moreButtonRef = useRef<HTMLElement>(null);
  const [visibleCount, setVisibleCount] = useState<number>(groups.length);
  // Two-pass measurement: render ALL groups first so the DOM contains every
  // `data-group-id` element, then measure to compute how many fit. Without this
  // the second pass only sees the previously-visible subset and can never
  // increase the count back when space grows.
  const measurePendingRef = useRef(true);

  const measureVisibleCount = (nav: HTMLElement): number => {
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
    return visible;
  };

  // Pass 1 — when inputs change, request a fresh measurement and render all groups.
  // `groups` is a fresh array reference every render, so depend on stable primitives
  // (length, collapsed flag, path). Composition changes that don't change length —
  // rare — are still caught by the ResizeObserver when group heights shift.
  useLayoutEffect(() => {
    measurePendingRef.current = true;
    setVisibleCount(groups.length);
  }, [groups.length, isCollapsed, location.pathname]);

  // Pass 2 — runs after every render. When a measurement is pending AND all
  // groups are in the DOM, measure synchronously (before paint) so the user
  // never sees the all-rendered intermediate state.
  // eslint-disable-next-line react-hooks/exhaustive-deps
  useLayoutEffect(() => {
    if (!measurePendingRef.current) return;
    const nav = navRef.current;
    if (!nav) return;
    const groupEls = nav.querySelectorAll('[data-group-id]');
    if (groupEls.length < groups.length) return;

    measurePendingRef.current = false;
    const visible = measureVisibleCount(nav);
    if (visible !== visibleCount) setVisibleCount(visible);
  });

  // Re-measure on viewport / sidebar resize.
  useEffect(() => {
    const nav = navRef.current;
    if (!nav) return;
    const observer = new ResizeObserver(() => {
      const navEl = navRef.current;
      if (!navEl) return;
      const renderedGroupCount = navEl.querySelectorAll('[data-group-id]').length;
      if (renderedGroupCount === groups.length) {
        // All groups are in DOM — measure directly. (Avoids a bail-out when
        // visibleCount already equals groups.length and the viewport shrinks.)
        const visible = measureVisibleCount(navEl);
        setVisibleCount((current) => (current === visible ? current : visible));
      } else {
        // Sliced — re-render with all groups, then pass-2 measures.
        measurePendingRef.current = true;
        setVisibleCount(groups.length);
      }
    });
    observer.observe(nav);
    return () => observer.disconnect();
  }, [groups.length]);

  const visibleGroups = groups.slice(0, visibleCount);
  const overflowGroups = groups.slice(visibleCount);

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
