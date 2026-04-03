/**
 * Frontend mirror of Starter.Shared.Constants.Permissions.
 *
 * Pattern: {Module}.{Action}
 *
 * Standard CRUD actions:
 *   View   -> list / read-many
 *   Show   -> read single / detail
 *   Create -> create resource
 *   Update -> update resource
 *   Delete -> delete resource
 *
 * Keep this file in sync with the backend Permissions.cs.
 */
export const PERMISSIONS = {
  Users: {
    View: 'Users.View',
    Show: 'Users.Show',
    Create: 'Users.Create',
    Update: 'Users.Update',
    Delete: 'Users.Delete',
    ManageRoles: 'Users.ManageRoles',
  },
  Roles: {
    View: 'Roles.View',
    Show: 'Roles.Show',
    Create: 'Roles.Create',
    Update: 'Roles.Update',
    Delete: 'Roles.Delete',
    ManagePermissions: 'Roles.ManagePermissions',
  },
  Tenants: {
    View: 'Tenants.View',
    Show: 'Tenants.Show',
    Create: 'Tenants.Create',
    Update: 'Tenants.Update',
    Delete: 'Tenants.Delete',
  },
  Files: {
    View: 'Files.View',
    Upload: 'Files.Upload',
    Delete: 'Files.Delete',
    Manage: 'Files.Manage',
  },
  ApiKeys: {
    View: 'ApiKeys.View',
    Create: 'ApiKeys.Create',
    Update: 'ApiKeys.Update',
    Delete: 'ApiKeys.Delete',
    ViewPlatform: 'ApiKeys.ViewPlatform',
    CreatePlatform: 'ApiKeys.CreatePlatform',
    UpdatePlatform: 'ApiKeys.UpdatePlatform',
    DeletePlatform: 'ApiKeys.DeletePlatform',
    EmergencyRevoke: 'ApiKeys.EmergencyRevoke',
  },
  FeatureFlags: {
    View: 'FeatureFlags.View',
    Create: 'FeatureFlags.Create',
    Update: 'FeatureFlags.Update',
    Delete: 'FeatureFlags.Delete',
    ManageTenantOverrides: 'FeatureFlags.ManageTenantOverrides',
    OptOut: 'FeatureFlags.OptOut',
  },
  Billing: {
    View: 'Billing.View',
    Manage: 'Billing.Manage',
    ViewPlans: 'Billing.ViewPlans',
    ManagePlans: 'Billing.ManagePlans',
    ManageTenantSubscriptions: 'Billing.ManageTenantSubscriptions',
  },
  System: {
    ViewAuditLogs: 'System.ViewAuditLogs',
    ManageSettings: 'System.ManageSettings',
    ViewDashboard: 'System.ViewDashboard',
    ExportData: 'System.ExportData',
    ForceExport: 'System.ForceExport',
  },
  Webhooks: {
    View: 'Webhooks.View',
    Create: 'Webhooks.Create',
    Update: 'Webhooks.Update',
    Delete: 'Webhooks.Delete',
    ViewPlatform: 'Webhooks.ViewPlatform',
  },
} as const;

type PermissionMap = typeof PERMISSIONS;
export type Permission = {
  [M in keyof PermissionMap]: PermissionMap[M][keyof PermissionMap[M]];
}[keyof PermissionMap];
