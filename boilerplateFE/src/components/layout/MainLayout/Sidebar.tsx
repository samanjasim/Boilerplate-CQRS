import { NavLink } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import {
  LayoutDashboard,
  Users,
  Shield,
  ChevronsLeft,
  ChevronsRight,
  ClipboardList,
  Building,
  FolderOpen,
  FileText,
  Settings2,
  KeyRound,
} from 'lucide-react';
import { cn } from '@/lib/utils';
import { useUIStore, useAuthStore, selectSidebarCollapsed, selectUser } from '@/stores';
import { ROUTES } from '@/config';
import { usePermissions } from '@/hooks';
import { PERMISSIONS } from '@/constants';

export function Sidebar() {
  const { t } = useTranslation();
  const isCollapsed = useUIStore(selectSidebarCollapsed);
  const toggleCollapse = useUIStore((state) => state.toggleSidebarCollapse);
  const { hasPermission } = usePermissions();
  const user = useAuthStore(selectUser);

  const tenantLogoUrl = user?.tenantLogoUrl;
  const tenantName = user?.tenantName;
  const appName = tenantName ?? import.meta.env.VITE_APP_NAME ?? 'Starter';

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
    ...(hasPermission(PERMISSIONS.System.ExportData)
      ? [{ label: t('nav.reports'), icon: FileText, path: ROUTES.REPORTS.LIST }]
      : []),
    ...(hasPermission(PERMISSIONS.System.ViewAuditLogs)
      ? [{ label: t('nav.auditLogs'), icon: ClipboardList, path: ROUTES.AUDIT_LOGS.LIST }]
      : []),
    ...(hasPermission(PERMISSIONS.ApiKeys.View)
      ? [{ label: t('nav.apiKeys'), icon: KeyRound, path: ROUTES.API_KEYS.LIST }]
      : []),
    ...(hasPermission(PERMISSIONS.System.ManageSettings)
      ? [{ label: t('nav.settings'), icon: Settings2, path: ROUTES.SETTINGS }]
      : []),
  ];

  return (
    <aside
      className={cn(
        'fixed top-0 z-40 flex h-screen flex-col bg-card transition-all duration-300',
        'ltr:left-0 ltr:border-r rtl:right-0 rtl:border-l border-border',
        isCollapsed ? 'w-16' : 'w-60'
      )}
    >
      {/* Logo */}
      <div className={cn('flex h-14 items-center gap-2.5 px-5', isCollapsed && 'justify-center px-0')}>
        <button
          onClick={isCollapsed ? toggleCollapse : undefined}
          className={cn('flex items-center gap-2.5 min-w-0', isCollapsed && 'cursor-pointer')}
        >
          <div className="flex h-8 w-8 items-center justify-center rounded-lg bg-primary shrink-0">
            {tenantLogoUrl ? (
              <img src={tenantLogoUrl} alt={appName} className="h-7 w-7 rounded object-cover" />
            ) : (
              <span className="text-sm font-bold text-white">{appName.charAt(0)}</span>
            )}
          </div>
          {!isCollapsed && (
            <span className="text-lg font-semibold text-foreground tracking-tight">{appName}</span>
          )}
        </button>
        {!isCollapsed && (
          <button
            onClick={toggleCollapse}
            className="flex h-7 w-7 items-center justify-center rounded-md text-muted-foreground hover:text-foreground transition-colors duration-150 shrink-0 ltr:ml-auto rtl:mr-auto"
          >
            <ChevronsLeft className="h-[18px] w-[18px] rtl:rotate-180" />
          </button>
        )}
      </div>

      {/* Navigation */}
      <nav className="flex-1 overflow-y-auto px-3 pt-2">
        <ul className="space-y-1">
          {navItems.map((item) => (
            <li key={item.path}>
              <NavLink
                to={item.path}
                end={item.path === ROUTES.DASHBOARD}
                className={({ isActive }) =>
                  cn(
                    'flex items-center gap-2.5 rounded-lg h-10 px-3 text-sm transition-colors duration-150 cursor-pointer',
                    isCollapsed && 'justify-center px-0',
                    isActive
                      ? 'state-active'
                      : 'state-hover'
                  )
                }
              >
                <item.icon className="h-[18px] w-[18px] shrink-0" />
                {!isCollapsed && <span>{item.label}</span>}
              </NavLink>
            </li>
          ))}
        </ul>
      </nav>

      {/* Collapsed: expand */}
      {isCollapsed && (
        <div className="p-2 border-t border-border">
          <button
            onClick={toggleCollapse}
            className="flex w-full items-center justify-center rounded-lg h-9 text-muted-foreground hover:bg-secondary hover:text-foreground transition-colors duration-150 cursor-pointer"
          >
            <ChevronsRight className="h-4 w-4 rtl:rotate-180" />
          </button>
        </div>
      )}
    </aside>
  );
}
