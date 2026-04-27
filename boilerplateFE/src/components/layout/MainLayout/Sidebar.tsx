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

export function Sidebar() {
  const isCollapsed = useUIStore(selectSidebarCollapsed);
  const toggleCollapse = useUIStore((state) => state.toggleSidebarCollapse);
  const user = useAuthStore(selectUser);
  const groups = useNavGroups();
  const sidebarOpen = useUIStore(selectSidebarOpen);
  const setSidebarOpen = useUIStore((state) => state.setSidebarOpen);
  const location = useLocation();

  // Auto-close mobile drawer on route change. Harmless on desktop (`sidebarOpen`
  // has no UI effect at lg+).
  useEffect(() => {
    setSidebarOpen(false);
  }, [location.pathname, setSidebarOpen]);

  // Auto-close on Escape while the drawer is open.
  useEffect(() => {
    if (!sidebarOpen) return;
    const onKeyDown = (e: KeyboardEvent) => {
      if (e.key === 'Escape') setSidebarOpen(false);
    };
    document.addEventListener('keydown', onKeyDown);
    return () => document.removeEventListener('keydown', onKeyDown);
  }, [sidebarOpen, setSidebarOpen]);

  const tenantLogoUrl = user?.tenantLogoUrl;
  const tenantName = user?.tenantName;
  const appName = tenantName ?? import.meta.env.VITE_APP_NAME ?? 'Starter';

  return (
    <aside
      className={cn(
        // floating geometry — 14px margin, no longer edge-to-edge
        'fixed top-3.5 bottom-3.5 z-40 flex flex-col rounded-[18px]',
        'surface-floating',
        'motion-safe:transition-all motion-safe:duration-300',
        // width: drawer always w-60 on <lg; lg+ follows collapse state
        'w-60',
        isCollapsed && 'lg:w-16',
        // position: 14px from start edge in both ltr / rtl
        'ltr:left-3.5 rtl:right-3.5 lg:translate-x-0',
        !sidebarOpen && 'max-lg:ltr:-translate-x-full max-lg:rtl:translate-x-full'
      )}
    >
      {/* Logo */}
      <div className={cn('flex h-14 items-center gap-2.5 px-5', isCollapsed && 'justify-center px-0')}>
        <button
          type="button"
          onClick={isCollapsed ? toggleCollapse : undefined}
          className={cn('flex items-center gap-2.5 min-w-0', isCollapsed && 'cursor-pointer')}
        >
          <div className="flex h-9 w-9 items-center justify-center rounded-lg btn-primary-gradient glow-primary-md shrink-0">
            {tenantLogoUrl ? (
              <img src={tenantLogoUrl} alt={appName} className="h-8 w-8 rounded object-cover" />
            ) : (
              <span className="text-[15px] font-bold text-white">{appName.charAt(0)}</span>
            )}
          </div>
          {!isCollapsed && (
            <span className="text-lg font-semibold text-foreground tracking-tight">{appName}</span>
          )}
        </button>
        {!isCollapsed && (
          <button
            type="button"
            onClick={toggleCollapse}
            className="hidden lg:flex h-7 w-7 items-center justify-center rounded-md text-muted-foreground hover:text-foreground motion-safe:transition-colors motion-safe:duration-150 shrink-0 ltr:ml-auto rtl:mr-auto"
          >
            <ChevronsLeft className="h-[18px] w-[18px] rtl:rotate-180" />
          </button>
        )}
      </div>

      {/* Navigation */}
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
            {!isCollapsed && group.label && (
              <div className="px-3 pb-1 pt-1 text-[10px] font-semibold uppercase tracking-[0.12em] text-muted-foreground">
                <span
                  className="inline-block w-1 h-1 rounded-full bg-primary/70 me-1.5 align-middle"
                  style={{ transform: 'translateY(-1px)' }}
                />
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
      </nav>

      {/* Collapsed: expand chevron (desktop only) */}
      {isCollapsed && (
        <div className="hidden lg:block p-2 border-t border-border">
          <button
            type="button"
            onClick={toggleCollapse}
            className="flex w-full items-center justify-center rounded-lg h-9 text-muted-foreground hover:bg-secondary hover:text-foreground motion-safe:transition-colors motion-safe:duration-150 cursor-pointer"
          >
            <ChevronsRight className="h-4 w-4 rtl:rotate-180" />
          </button>
        </div>
      )}
    </aside>
  );
}
