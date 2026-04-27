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
