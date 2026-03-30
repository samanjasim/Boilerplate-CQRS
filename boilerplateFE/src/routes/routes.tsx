import { lazy } from 'react';
import type { RouteObject } from 'react-router-dom';
import { ROUTES } from '@/config';
import { MainLayout } from '@/components/layout/MainLayout';
import { AuthLayout } from '@/components/layout/AuthLayout';
import { PublicLayout } from '@/components/layout/PublicLayout';
import { AuthGuard, GuestGuard, PermissionGuard } from '@/components/guards';
import { PERMISSIONS } from '@/constants';

// Lazy-loaded pages
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
const AuditLogsPage = lazy(() => import('@/features/audit-logs/pages/AuditLogsPage'));
const TenantsListPage = lazy(() => import('@/features/tenants/pages/TenantsListPage'));
const TenantDetailPage = lazy(() => import('@/features/tenants/pages/TenantDetailPage'));
const ProfilePage = lazy(() => import('@/features/profile/pages/ProfilePage'));
const NotificationsPage = lazy(() => import('@/features/notifications/pages/NotificationsPage'));
const FilesPage = lazy(() => import('@/features/files/pages/FilesPage'));
const ReportsPage = lazy(() => import('@/features/reports/pages/ReportsPage'));
const SettingsPage = lazy(() => import('@/features/settings/pages/SettingsPage'));
const ApiKeysPage = lazy(() => import('@/features/api-keys/pages/ApiKeysPage'));
const FeatureFlagsPage = lazy(() => import('@/features/feature-flags/pages/FeatureFlagsPage'));
const NotFoundPage = lazy(() => import('@/routes/NotFoundPage'));

export const routes: RouteObject[] = [
  // Public landing page (always accessible)
  {
    element: <PublicLayout />,
    children: [
      { path: ROUTES.LANDING, element: <LandingPage /> },
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
          { path: ROUTES.DASHBOARD, element: <DashboardPage /> },
          { path: ROUTES.PROFILE, element: <ProfilePage /> },
          { path: ROUTES.NOTIFICATIONS, element: <NotificationsPage /> },

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

          // Tenants
          {
            element: <PermissionGuard permission={PERMISSIONS.Tenants.View} />,
            children: [
              { path: ROUTES.TENANTS.LIST, element: <TenantsListPage /> },
              { path: ROUTES.TENANTS.DETAIL, element: <TenantDetailPage /> },
            ],
          },

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

          // Settings
          {
            element: <PermissionGuard permission={PERMISSIONS.System.ManageSettings} />,
            children: [
              { path: ROUTES.SETTINGS, element: <SettingsPage /> },
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
        ],
      },
    ],
  },

  // Catch-all
  { path: '*', element: <NotFoundPage /> },
];
