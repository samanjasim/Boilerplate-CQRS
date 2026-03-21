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
  System: {
    ViewAuditLogs: 'System.ViewAuditLogs',
    ManageSettings: 'System.ManageSettings',
    ViewDashboard: 'System.ViewDashboard',
  },
} as const;

type PermissionMap = typeof PERMISSIONS;
export type Permission = {
  [M in keyof PermissionMap]: PermissionMap[M][keyof PermissionMap[M]];
}[keyof PermissionMap];
