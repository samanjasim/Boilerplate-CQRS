import { useTranslation } from 'react-i18next';
import {
  ArrowLeftRight,
  Bell,
  Building,
  ClipboardCheck,
  ClipboardList,
  CreditCard,
  FileBarChart2,
  FileText,
  FolderOpen,
  GitBranch,
  History,
  KeyRound,
  LayoutDashboard,
  Link2,
  ListChecks,
  MessageSquare,
  Package,
  ReceiptText,
  ScrollText,
  Settings2,
  Shield,
  ToggleRight,
  Users,
  Webhook,
  Zap,
  type LucideIcon,
} from 'lucide-react';

import { ROUTES } from '@/config';
import { activeModules, isModuleActive } from '@/config/modules.config';
import { PERMISSIONS } from '@/constants';
import { usePendingTaskCount } from '@/features/workflow/api';
import { useFeatureFlag, usePermissions } from '@/hooks';
import { selectUser, useAuthStore } from '@/stores';

export interface SidebarNavItem {
  label: string;
  icon: LucideIcon;
  path: string;
  end?: boolean;
  badge?: number;
}

export interface SidebarNavGroup {
  id: string;
  label?: string;
  items: SidebarNavItem[];
}

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

  const { data: pendingTaskCount = 0 } = usePendingTaskCount(isModuleActive('workflow'));

  const groups: SidebarNavGroup[] = [];

  // Top block (no label, no group divider above it)
  groups.push({
    id: 'top',
    items: [
      { label: t('nav.dashboard'), icon: LayoutDashboard, path: ROUTES.DASHBOARD, end: true },
      { label: t('nav.notifications'), icon: Bell, path: ROUTES.NOTIFICATIONS },
    ],
  });

  // Workflow module
  if (activeModules.workflow) {
    const items: SidebarNavItem[] = [];
    if (hasPermission(PERMISSIONS.Workflows.View)) {
      items.push({
        label: t('workflow.sidebar.taskInbox'),
        icon: ClipboardCheck,
        path: ROUTES.WORKFLOWS.INBOX,
        badge: pendingTaskCount > 0 ? pendingTaskCount : undefined,
      });
      items.push({
        label: t('workflow.sidebar.history'),
        icon: History,
        path: ROUTES.WORKFLOWS.INSTANCES,
      });
    }
    if (hasPermission(PERMISSIONS.Workflows.ManageDefinitions)) {
      items.push({
        label: t('workflow.sidebar.definitions'),
        icon: GitBranch,
        path: ROUTES.WORKFLOWS.DEFINITIONS,
      });
    }
    groups.push({ id: 'workflow', label: t('nav.groups.workflow'), items });
  }

  // Communication module (tenant-scoped only)
  if (activeModules.communication && tenantScoped) {
    const items: SidebarNavItem[] = [];
    if (hasPermission(PERMISSIONS.Communication.View)) {
      items.push({ label: t('nav.channels'), icon: MessageSquare, path: ROUTES.COMMUNICATION.CHANNELS });
      items.push({ label: t('nav.templates'), icon: FileText, path: ROUTES.COMMUNICATION.TEMPLATES });
      items.push({ label: t('nav.triggerRules'), icon: Zap, path: ROUTES.COMMUNICATION.TRIGGER_RULES });
      items.push({ label: t('nav.integrations'), icon: Link2, path: ROUTES.COMMUNICATION.INTEGRATIONS });
    }
    if (hasPermission(PERMISSIONS.Communication.ViewDeliveryLog)) {
      items.push({ label: t('nav.deliveryLog'), icon: ScrollText, path: ROUTES.COMMUNICATION.DELIVERY_LOG });
    }
    groups.push({ id: 'communication', label: t('nav.groups.communication'), items });
  }

  // Products module
  if (activeModules.products) {
    const items: SidebarNavItem[] = [];
    if (hasPermission(PERMISSIONS.Products.View)) {
      items.push({ label: t('nav.products', 'Products'), icon: Package, path: ROUTES.PRODUCTS.LIST });
    }
    groups.push({ id: 'products', label: t('nav.groups.products'), items });
  }

  // Billing module (tenant-scoped only for the main Billing page; Plans/Subscriptions reachable by platform admins too)
  if (activeModules.billing) {
    const items: SidebarNavItem[] = [];
    if (hasPermission(PERMISSIONS.Billing.View) && tenantScoped) {
      items.push({ label: t('nav.billing'), icon: CreditCard, path: ROUTES.BILLING, end: true });
    }
    if (hasPermission(PERMISSIONS.Billing.ViewPlans)) {
      items.push({ label: t('nav.billingPlans'), icon: ReceiptText, path: ROUTES.BILLING_PLANS });
    }
    if (hasPermission(PERMISSIONS.Billing.ManageTenantSubscriptions)) {
      items.push({ label: t('nav.subscriptions'), icon: ListChecks, path: ROUTES.SUBSCRIPTIONS.LIST, end: true });
    }
    groups.push({ id: 'billing', label: t('nav.groups.billing'), items });
  }

  // Webhooks module — tenant-scoped link only (the platform-admin link lives in the Platform group).
  if (activeModules.webhooks && tenantScoped && webhooksFlag.isEnabled) {
    const items: SidebarNavItem[] = [];
    if (hasPermission(PERMISSIONS.Webhooks.View)) {
      items.push({ label: t('nav.webhooks'), icon: Webhook, path: ROUTES.WEBHOOKS });
    }
    groups.push({ id: 'webhooks', label: t('nav.groups.webhooks'), items });
  }

  // Import / Export module
  if (activeModules.importExport) {
    const items: SidebarNavItem[] = [];
    const canExport = hasPermission(PERMISSIONS.System.ExportData) && exportsFlag.isEnabled;
    const canImport = hasPermission(PERMISSIONS.System.ImportData) && importsFlag.isEnabled;
    if (canExport || canImport) {
      items.push({ label: t('nav.importExport'), icon: ArrowLeftRight, path: ROUTES.IMPORT_EXPORT });
    }
    groups.push({ id: 'importExport', label: t('nav.groups.importExport'), items });
  }

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
    if (activeModules.webhooks && hasPermission(PERMISSIONS.Webhooks.ViewPlatform)) {
      items.push({ label: t('nav.webhooksAdmin'), icon: Webhook, path: ROUTES.WEBHOOKS_ADMIN.LIST, end: true });
    }
    if (hasPermission(PERMISSIONS.System.ManageSettings)) {
      items.push({ label: t('nav.settings'), icon: Settings2, path: ROUTES.SETTINGS });
    }
    groups.push({ id: 'platform', label: t('nav.groups.platform'), items });
  }

  // Strip empty groups so labels and dividers never render for nothing.
  return groups.filter((g) => g.items.length > 0);
}
