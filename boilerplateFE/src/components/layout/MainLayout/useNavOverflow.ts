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
