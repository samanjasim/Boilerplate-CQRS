import { useTranslation } from 'react-i18next';
import {
  Bell,
  Building,
  ClipboardList,
  FileBarChart2,
  FolderOpen,
  KeyRound,
  LayoutDashboard,
  Settings2,
  Shield,
  ToggleRight,
  Users,
} from 'lucide-react';

import { ROUTES } from '@/config';
import { PERMISSIONS } from '@/constants';
import { useFeatureFlag, usePermissions } from '@/hooks';
import {
  getModuleNavGroups,
  getModuleNavItems,
  type ModuleNavContext,
  type ModuleNavGroup,
  type ModuleNavItem,
} from '@/lib/modules';
import { selectUser, useAuthStore } from '@/stores';

export type SidebarNavItem = ModuleNavItem;
export type SidebarNavGroup = ModuleNavGroup;

/**
 * Builds the sidebar nav as a list of permission/module/flag-gated groups.
 * Empty groups are stripped, so consumers can render the result directly.
 */
export function useNavGroups(): SidebarNavGroup[] {
  const { t } = useTranslation();
  const { hasPermission } = usePermissions();
  const user = useAuthStore(selectUser);
  const tenantScoped = Boolean(user?.tenantId);

  const webhooksFlag = useFeatureFlag('webhooks.enabled');
  const importsFlag = useFeatureFlag('imports.enabled');
  const exportsFlag = useFeatureFlag('exports.enabled');

  const navCtx: ModuleNavContext = {
    t,
    hasPermission,
    tenantScoped,
    isFeatureEnabled: (key) => {
      if (key === 'webhooks.enabled') return webhooksFlag.isEnabled;
      if (key === 'imports.enabled') return importsFlag.isEnabled;
      if (key === 'exports.enabled') return exportsFlag.isEnabled;
      return false;
    },
  };

  const groups: SidebarNavGroup[] = [];

  // Top block (no label, no group divider above it)
  groups.push({
    id: 'top',
    items: [
      { label: t('nav.dashboard'), icon: LayoutDashboard, path: ROUTES.DASHBOARD, end: true },
      { label: t('nav.notifications'), icon: Bell, path: ROUTES.NOTIFICATIONS },
    ],
  });

  groups.push(...getModuleNavGroups(navCtx));

  // People (core)
  {
    const items: SidebarNavItem[] = [];
    if (hasPermission(PERMISSIONS.Users.View)) {
      items.push({ label: t('nav.users'), icon: Users, path: ROUTES.USERS.LIST });
    }
    if (hasPermission(PERMISSIONS.Roles.View)) {
      items.push({ label: t('nav.roles'), icon: Shield, path: ROUTES.ROLES.LIST });
    }
    if (hasPermission(PERMISSIONS.Tenants.View)) {
      items.push(
        tenantScoped
          ? { label: t('nav.organization'), icon: Building, path: ROUTES.ORGANIZATION }
          : { label: t('nav.tenants'), icon: Building, path: ROUTES.TENANTS.LIST }
      );
    }
    groups.push({ id: 'people', label: t('nav.groups.people'), items });
  }

  // Content (core)
  {
    const items: SidebarNavItem[] = [];
    if (hasPermission(PERMISSIONS.Files.View)) {
      items.push({ label: t('nav.files'), icon: FolderOpen, path: ROUTES.FILES.LIST });
    }
    if (hasPermission(PERMISSIONS.System.ExportData)) {
      items.push({ label: t('nav.reports'), icon: FileBarChart2, path: ROUTES.REPORTS.LIST });
    }
    groups.push({ id: 'content', label: t('nav.groups.content'), items });
  }

  // Platform (core + cross-tenant admin)
  {
    const items: SidebarNavItem[] = [];
    if (hasPermission(PERMISSIONS.System.ViewAuditLogs)) {
      items.push({ label: t('nav.auditLogs'), icon: ClipboardList, path: ROUTES.AUDIT_LOGS.LIST });
    }
    if (hasPermission(PERMISSIONS.ApiKeys.View)) {
      items.push({ label: t('nav.apiKeys'), icon: KeyRound, path: ROUTES.API_KEYS.LIST });
    }
    if (hasPermission(PERMISSIONS.FeatureFlags.View)) {
      items.push({ label: t('nav.featureFlags'), icon: ToggleRight, path: ROUTES.FEATURE_FLAGS.LIST });
    }
    if (hasPermission(PERMISSIONS.System.ManageSettings)) {
      items.push({ label: t('nav.settings'), icon: Settings2, path: ROUTES.SETTINGS });
    }
    items.push(...getModuleNavItems('platform', navCtx));
    if (items.length > 0) {
      groups.push({ id: 'platform', label: t('nav.groups.platform'), items });
    }
  }

  // Strip empty groups so labels and dividers never render for nothing.
  return groups.filter((g) => g.items.length > 0);
}
