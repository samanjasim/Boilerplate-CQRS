import { NavLink } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import {
  LayoutDashboard,
  Users,
  Shield,
  Blocks,
  ChevronLeft,
  ClipboardList,
  Building,
  FolderOpen,
} from 'lucide-react';
import { cn } from '@/lib/utils';
import { useUIStore, selectSidebarCollapsed } from '@/stores';
import { ROUTES } from '@/config';
import { Button } from '@/components/ui/button';
import { usePermissions } from '@/hooks';
import { PERMISSIONS } from '@/constants';

export function Sidebar() {
  const { t } = useTranslation();
  const isCollapsed = useUIStore(selectSidebarCollapsed);
  const toggleCollapse = useUIStore((state) => state.toggleSidebarCollapse);
  const { hasPermission } = usePermissions();

  const navItems = [
    { label: t('nav.dashboard'), icon: LayoutDashboard, path: ROUTES.DASHBOARD },
    ...(hasPermission(PERMISSIONS.Users.View)
      ? [{ label: t('nav.users'), icon: Users, path: ROUTES.USERS.LIST }]
      : []),
    ...(hasPermission(PERMISSIONS.Roles.View)
      ? [{ label: t('nav.roles'), icon: Shield, path: ROUTES.ROLES.LIST }]
      : []),
    ...(hasPermission(PERMISSIONS.Tenants.View)
      ? [{ label: t('nav.tenants'), icon: Building, path: ROUTES.TENANTS.LIST }]
      : []),
    ...(hasPermission(PERMISSIONS.Files.View)
      ? [{ label: t('nav.files'), icon: FolderOpen, path: ROUTES.FILES.LIST }]
      : []),
    ...(hasPermission(PERMISSIONS.System.ViewAuditLogs)
      ? [{ label: t('nav.auditLogs'), icon: ClipboardList, path: ROUTES.AUDIT_LOGS.LIST }]
      : []),
  ];

  return (
    <aside
      className={cn(
        'fixed top-0 z-40 flex h-screen flex-col border-border bg-card transition-all duration-300',
        'ltr:left-0 ltr:border-r rtl:right-0 rtl:border-l',
        isCollapsed ? 'w-16' : 'w-64'
      )}
    >
      {/* Logo */}
      <div className="flex h-16 items-center justify-between border-b border-border px-4">
        <div className="flex items-center gap-3">
          <div className="flex h-10 w-10 items-center justify-center rounded-lg bg-primary/10">
            <Blocks className="h-5 w-5 text-primary" />
          </div>
          {!isCollapsed && (
            <span className="text-lg font-bold text-foreground">{import.meta.env.VITE_APP_NAME}</span>
          )}
        </div>
      </div>

      {/* Navigation */}
      <nav className="flex-1 overflow-y-auto p-3">
        <ul className="space-y-1">
          {navItems.map((item) => (
            <li key={item.path}>
              <NavLink
                to={item.path}
                end={item.path === ROUTES.DASHBOARD}
                className={({ isActive }) =>
                  cn(
                    'flex items-center gap-3 rounded-lg px-3 py-2.5 text-sm font-medium transition-colors',
                    isActive
                      ? 'bg-primary/10 text-primary'
                      : 'text-muted-foreground hover:bg-muted hover:text-foreground'
                  )
                }
              >
                <item.icon className="h-5 w-5 shrink-0" />
                {!isCollapsed && <span>{item.label}</span>}
              </NavLink>
            </li>
          ))}
        </ul>
      </nav>

      {/* Collapse toggle */}
      <div className="border-t border-border p-3">
        <Button
          variant="ghost"
          size="sm"
          onClick={toggleCollapse}
          className={cn('w-full justify-center', !isCollapsed && 'justify-start')}
        >
          <ChevronLeft
            className={cn(
              'h-4 w-4 transition-transform',
              isCollapsed && 'ltr:rotate-180 rtl:rotate-0',
              !isCollapsed && 'rtl:rotate-180'
            )}
          />
          {!isCollapsed && <span className="ltr:ml-2 rtl:mr-2">{t('nav.collapse')}</span>}
        </Button>
      </div>
    </aside>
  );
}
