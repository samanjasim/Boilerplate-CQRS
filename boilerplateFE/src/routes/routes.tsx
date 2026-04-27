import { lazy } from 'react';
import type { RouteObject } from 'react-router-dom';
import { ROUTES } from '@/config';
import { activeModules } from '@/config/modules.config';
import { MainLayout } from '@/components/layout/MainLayout';
import { AuthLayout } from '@/components/layout/AuthLayout';
import { PublicLayout } from '@/components/layout/PublicLayout';
import { AuthGuard, GuestGuard, PermissionGuard } from '@/components/guards';
import { PERMISSIONS } from '@/constants';

// Core pages (always loaded)
const LandingPage = lazy(() => import('@/features/landing/pages/LandingPage'));
const LoginPage = lazy(() => import('@/features/auth/pages/LoginPage'));
// NOTE: Public self-registration disabled — users register via /register-tenant or invitation
// const RegisterPage = lazy(() => import('@/features/auth/pages/RegisterPage'));
const RegisterTenantPage = lazy(() => import('@/features/auth/pages/RegisterTenantPage'));
const DashboardPage = lazy(() => import('@/features/dashboard/pages/DashboardPage'));
const UsersListPage = lazy(() => import('@/features/users/pages/UsersListPage'));
const UserDetailPage = lazy(() => import('@/features/users/pages/UserDetailPage'));
const RolesListPage = lazy(() => import('@/features/roles/pages/RolesListPage'));
const RoleDetailPage = lazy(() => import('@/features/roles/pages/RoleDetailPage'));
const RoleCreatePage = lazy(() => import('@/features/roles/pages/RoleCreatePage'));
const RoleEditPage = lazy(() => import('@/features/roles/pages/RoleEditPage'));
const ForgotPasswordPage = lazy(() => import('@/features/auth/pages/ForgotPasswordPage'));
const VerifyEmailPage = lazy(() => import('@/features/auth/pages/VerifyEmailPage'));
const AcceptInvitePage = lazy(() => import('@/features/auth/pages/AcceptInvitePage'));
const TenantsListPage = lazy(() => import('@/features/tenants/pages/TenantsListPage'));
const TenantDetailPage = lazy(() => import('@/features/tenants/pages/TenantDetailPage'));
const ProfilePage = lazy(() => import('@/features/profile/pages/ProfilePage'));
const SettingsPage = lazy(() => import('@/features/settings/pages/SettingsPage'));
const NotFoundPage = lazy(() => import('@/routes/NotFoundPage'));
// eslint-disable-next-line react-refresh/only-export-components
const StyleguidePage = import.meta.env.DEV
  ? lazy(() => import('@/features/styleguide/pages/StyleguidePage'))
  : (() => null);

// Core feature pages (always loaded — these features ship with every build)
const AuditLogsPage = lazy(() => import('@/features/audit-logs/pages/AuditLogsPage'));
const NotificationsPage = lazy(() => import('@/features/notifications/pages/NotificationsPage'));
const FilesPage = lazy(() => import('@/features/files/pages/FilesPage'));
const ReportsPage = lazy(() => import('@/features/reports/pages/ReportsPage'));
const ApiKeysPage = lazy(() => import('@/features/api-keys/pages/ApiKeysPage'));
const FeatureFlagsPage = lazy(() => import('@/features/feature-flags/pages/FeatureFlagsPage'));

// Module pages (conditionally loaded based on modules.config.ts)
// eslint-disable-next-line react-refresh/only-export-components
const NullPage = () => null;
const WebhooksPage = activeModules.webhooks ? lazy(() => import('@/features/webhooks/pages/WebhooksPage')) : NullPage;
const WebhookAdminPage = activeModules.webhooks ? lazy(() => import('@/features/webhooks/pages/WebhookAdminPage')) : NullPage;
const WebhookAdminDetailPage = activeModules.webhooks ? lazy(() => import('@/features/webhooks/pages/WebhookAdminDetailPage')) : NullPage;
const BillingPage = activeModules.billing ? lazy(() => import('@/features/billing/pages/BillingPage')) : NullPage;
const BillingPlansPage = activeModules.billing ? lazy(() => import('@/features/billing/pages/BillingPlansPage')) : NullPage;
const PricingPage = activeModules.billing ? lazy(() => import('@/features/billing/pages/PricingPage')) : NullPage;
const SubscriptionsPage = activeModules.billing ? lazy(() => import('@/features/billing/pages/SubscriptionsPage')) : NullPage;
const SubscriptionDetailPage = activeModules.billing ? lazy(() => import('@/features/billing/pages/SubscriptionDetailPage')) : NullPage;
const ImportExportPage = activeModules.importExport ? lazy(() => import('@/features/import-export/pages/ImportExportPage')) : NullPage;
const ProductsListPage = activeModules.products ? lazy(() => import('@/features/products/pages/ProductsListPage')) : NullPage;
const ProductCreatePage = activeModules.products ? lazy(() => import('@/features/products/pages/ProductCreatePage')) : NullPage;
const ProductDetailPage = activeModules.products ? lazy(() => import('@/features/products/pages/ProductDetailPage')) : NullPage;
const WorkflowInboxPage = activeModules.workflow ? lazy(() => import('@/features/workflow/pages/WorkflowInboxPage')) : NullPage;
const WorkflowInstancesPage = activeModules.workflow ? lazy(() => import('@/features/workflow/pages/WorkflowInstancesPage')) : NullPage;
const WorkflowInstanceDetailPage = activeModules.workflow ? lazy(() => import('@/features/workflow/pages/WorkflowInstanceDetailPage')) : NullPage;
const WorkflowDefinitionsPage = activeModules.workflow ? lazy(() => import('@/features/workflow/pages/WorkflowDefinitionsPage')) : NullPage;
const WorkflowDefinitionDetailPage = activeModules.workflow ? lazy(() => import('@/features/workflow/pages/WorkflowDefinitionDetailPage')) : NullPage;
const WorkflowDefinitionDesignerPage = activeModules.workflow ? lazy(() => import('@/features/workflow/pages/WorkflowDefinitionDesignerPage')) : NullPage;
const ChannelsPage = activeModules.communication ? lazy(() => import('@/features/communication/pages/ChannelsPage')) : NullPage;
const TemplatesPage = activeModules.communication ? lazy(() => import('@/features/communication/pages/TemplatesPage')) : NullPage;
const TriggerRulesPage = activeModules.communication ? lazy(() => import('@/features/communication/pages/TriggerRulesPage')) : NullPage;
const IntegrationsPage = activeModules.communication ? lazy(() => import('@/features/communication/pages/IntegrationsPage')) : NullPage;
const DeliveryLogPage = activeModules.communication ? lazy(() => import('@/features/communication/pages/DeliveryLogPage')) : NullPage;

export const routes: RouteObject[] = [
  // Public landing page (always accessible)
  {
    element: <PublicLayout />,
    children: [
      { path: ROUTES.LANDING, element: <LandingPage /> },
      ...(activeModules.billing ? [{ path: ROUTES.PRICING, element: <PricingPage /> }] : []),
      ...(import.meta.env.DEV ? [{ path: ROUTES.STYLEGUIDE, element: <StyleguidePage /> }] : []),
    ],
  },

  // Public routes (guest only)
  {
    element: <GuestGuard />,
    children: [
      {
        element: <AuthLayout />,
        children: [
          { path: ROUTES.LOGIN, element: <LoginPage /> },
          // { path: ROUTES.REGISTER, element: <RegisterPage /> },
          { path: ROUTES.REGISTER_TENANT, element: <RegisterTenantPage /> },
          { path: ROUTES.FORGOT_PASSWORD, element: <ForgotPasswordPage /> },
          { path: ROUTES.RESET_PASSWORD, element: <ForgotPasswordPage /> },
          { path: ROUTES.VERIFY_EMAIL, element: <VerifyEmailPage /> },
          { path: ROUTES.ACCEPT_INVITE, element: <AcceptInvitePage /> },
        ],
      },
    ],
  },

  // Protected routes (authenticated only)
  {
    element: <AuthGuard />,
    children: [
      {
        element: <MainLayout />,
        children: [
          // ── Core routes (always present) ──
          { path: ROUTES.DASHBOARD, element: <DashboardPage /> },
          { path: ROUTES.PROFILE, element: <ProfilePage /> },

          // Users
          {
            element: <PermissionGuard permission={PERMISSIONS.Users.View} />,
            children: [
              { path: ROUTES.USERS.LIST, element: <UsersListPage /> },
              { path: ROUTES.USERS.DETAIL, element: <UserDetailPage /> },
            ],
          },

          // Roles
          {
            element: <PermissionGuard permission={PERMISSIONS.Roles.View} />,
            children: [
              { path: ROUTES.ROLES.LIST, element: <RolesListPage /> },
              { path: ROUTES.ROLES.DETAIL, element: <RoleDetailPage /> },
            ],
          },
          {
            element: <PermissionGuard permission={PERMISSIONS.Roles.Create} />,
            children: [
              { path: ROUTES.ROLES.CREATE, element: <RoleCreatePage /> },
            ],
          },
          {
            element: (
              <PermissionGuard
                permissions={[PERMISSIONS.Roles.Update, PERMISSIONS.Roles.ManagePermissions]}
                mode="any"
              />
            ),
            children: [
              { path: ROUTES.ROLES.EDIT, element: <RoleEditPage /> },
            ],
          },

          // Tenants (platform admin)
          {
            element: <PermissionGuard permission={PERMISSIONS.Tenants.View} />,
            children: [
              { path: ROUTES.TENANTS.LIST, element: <TenantsListPage /> },
              { path: ROUTES.TENANTS.DETAIL, element: <TenantDetailPage /> },
            ],
          },

          // Organization (tenant self-service)
          {
            element: <PermissionGuard permission={PERMISSIONS.Tenants.View} />,
            children: [
              { path: ROUTES.ORGANIZATION, element: <TenantDetailPage /> },
            ],
          },

          // Settings
          {
            element: <PermissionGuard permission={PERMISSIONS.System.ManageSettings} />,
            children: [
              { path: ROUTES.SETTINGS, element: <SettingsPage /> },
            ],
          },

          // ── Core feature routes (always present) ──

          // Notifications — self-service: every authenticated user can read their own
          // notifications. Backend enforces per-user scoping; no permission gate needed
          // (same as ProfilePage). No PermissionGuard is intentional.
          { path: ROUTES.NOTIFICATIONS, element: <NotificationsPage /> },

          // Audit Logs
          {
            element: <PermissionGuard permission={PERMISSIONS.System.ViewAuditLogs} />,
            children: [
              { path: ROUTES.AUDIT_LOGS.LIST, element: <AuditLogsPage /> },
            ],
          },

          // Files
          {
            element: <PermissionGuard permission={PERMISSIONS.Files.View} />,
            children: [
              { path: ROUTES.FILES.LIST, element: <FilesPage /> },
            ],
          },

          // Reports
          {
            element: <PermissionGuard permission={PERMISSIONS.System.ExportData} />,
            children: [
              { path: ROUTES.REPORTS.LIST, element: <ReportsPage /> },
            ],
          },

          // API Keys
          {
            element: <PermissionGuard permission={PERMISSIONS.ApiKeys.View} />,
            children: [
              { path: ROUTES.API_KEYS.LIST, element: <ApiKeysPage /> },
            ],
          },

          // Feature Flags
          {
            element: <PermissionGuard permission={PERMISSIONS.FeatureFlags.View} />,
            children: [
              { path: ROUTES.FEATURE_FLAGS.LIST, element: <FeatureFlagsPage /> },
            ],
          },

          // ── Module routes (conditional on modules.config.ts) ──

          // Import / Export
          ...(activeModules.importExport ? [{
            element: (
              <PermissionGuard
                permissions={[PERMISSIONS.System.ExportData, PERMISSIONS.System.ImportData]}
                mode="any"
              />
            ),
            children: [
              { path: ROUTES.IMPORT_EXPORT, element: <ImportExportPage /> },
            ],
          }] : []),

          // Webhooks
          ...(activeModules.webhooks ? [
            {
              element: <PermissionGuard permission={PERMISSIONS.Webhooks.View} />,
              children: [
                { path: ROUTES.WEBHOOKS, element: <WebhooksPage /> },
              ],
            },
            {
              element: <PermissionGuard permission={PERMISSIONS.Webhooks.ViewPlatform} />,
              children: [
                { path: ROUTES.WEBHOOKS_ADMIN.LIST, element: <WebhookAdminPage /> },
                { path: ROUTES.WEBHOOKS_ADMIN.DETAIL, element: <WebhookAdminDetailPage /> },
              ],
            },
          ] : []),

          // Products
          ...(activeModules.products ? [
            {
              element: <PermissionGuard permission={PERMISSIONS.Products.View} />,
              children: [
                { path: ROUTES.PRODUCTS.LIST, element: <ProductsListPage /> },
                { path: ROUTES.PRODUCTS.DETAIL, element: <ProductDetailPage /> },
              ],
            },
            {
              element: <PermissionGuard permission={PERMISSIONS.Products.Create} />,
              children: [
                { path: ROUTES.PRODUCTS.CREATE, element: <ProductCreatePage /> },
              ],
            },
          ] : []),

          // Workflows
          ...(activeModules.workflow ? [
            {
              element: <PermissionGuard permission={PERMISSIONS.Workflows.View} />,
              children: [
                { path: ROUTES.WORKFLOWS.INBOX, element: <WorkflowInboxPage /> },
                { path: ROUTES.WORKFLOWS.INSTANCES, element: <WorkflowInstancesPage /> },
                { path: ROUTES.WORKFLOWS.INSTANCE_DETAIL, element: <WorkflowInstanceDetailPage /> },
              ],
            },
            {
              element: <PermissionGuard permission={PERMISSIONS.Workflows.ManageDefinitions} />,
              children: [
                { path: ROUTES.WORKFLOWS.DEFINITIONS, element: <WorkflowDefinitionsPage /> },
                { path: ROUTES.WORKFLOWS.DEFINITION_DETAIL, element: <WorkflowDefinitionDetailPage /> },
                { path: ROUTES.WORKFLOWS.DEFINITION_DESIGNER, element: <WorkflowDefinitionDesignerPage /> },
              ],
            },
          ] : []),

          // Communication
          ...(activeModules.communication ? [
            {
              element: <PermissionGuard permission={PERMISSIONS.Communication.View} />,
              children: [
                { path: ROUTES.COMMUNICATION.CHANNELS, element: <ChannelsPage /> },
                { path: ROUTES.COMMUNICATION.TEMPLATES, element: <TemplatesPage /> },
                { path: ROUTES.COMMUNICATION.TRIGGER_RULES, element: <TriggerRulesPage /> },
                { path: ROUTES.COMMUNICATION.INTEGRATIONS, element: <IntegrationsPage /> },
              ],
            },
            {
              element: <PermissionGuard permission={PERMISSIONS.Communication.ViewDeliveryLog} />,
              children: [
                { path: ROUTES.COMMUNICATION.DELIVERY_LOG, element: <DeliveryLogPage /> },
              ],
            },
          ] : []),

          // Billing
          ...(activeModules.billing ? [
            {
              element: <PermissionGuard permission={PERMISSIONS.Billing.View} />,
              children: [
                { path: ROUTES.BILLING, element: <BillingPage /> },
              ],
            },
            {
              element: <PermissionGuard permission={PERMISSIONS.Billing.ViewPlans} />,
              children: [
                { path: ROUTES.BILLING_PLANS, element: <BillingPlansPage /> },
              ],
            },
            {
              element: <PermissionGuard permission={PERMISSIONS.Billing.ManageTenantSubscriptions} />,
              children: [
                { path: ROUTES.SUBSCRIPTIONS.LIST, element: <SubscriptionsPage /> },
                { path: ROUTES.SUBSCRIPTIONS.DETAIL, element: <SubscriptionDetailPage /> },
              ],
            },
          ] : []),
        ],
      },
    ],
  },

  // Catch-all
  { path: '*', element: <NotFoundPage /> },
];
