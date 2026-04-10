/// Permission constants mirroring the backend's
/// `Starter.Shared/Constants/Permissions.cs`.
///
/// Keep in sync manually — when the BE adds/removes permissions,
/// update this file. Module-specific permissions (e.g. Billing) are
/// declared in their respective module's constants file, not here.
abstract final class Permissions {
  // --- Users ---
  static const usersView = 'Users.View';
  static const usersShow = 'Users.Show';
  static const usersCreate = 'Users.Create';
  static const usersUpdate = 'Users.Update';
  static const usersDelete = 'Users.Delete';
  static const usersManageRoles = 'Users.ManageRoles';

  // --- Roles ---
  static const rolesView = 'Roles.View';
  static const rolesShow = 'Roles.Show';
  static const rolesCreate = 'Roles.Create';
  static const rolesUpdate = 'Roles.Update';
  static const rolesDelete = 'Roles.Delete';
  static const rolesManagePermissions = 'Roles.ManagePermissions';

  // --- System ---
  static const systemManageSettings = 'System.ManageSettings';
  static const systemViewDashboard = 'System.ViewDashboard';
  static const systemViewAuditLogs = 'System.ViewAuditLogs';
  static const systemExportData = 'System.ExportData';
  static const systemForceExport = 'System.ForceExport';

  // --- Tenants ---
  static const tenantsView = 'Tenants.View';
  static const tenantsShow = 'Tenants.Show';
  static const tenantsCreate = 'Tenants.Create';
  static const tenantsUpdate = 'Tenants.Update';
  static const tenantsDelete = 'Tenants.Delete';

  // --- Files ---
  static const filesView = 'Files.View';
  static const filesUpload = 'Files.Upload';
  static const filesDelete = 'Files.Delete';
  static const filesManage = 'Files.Manage';

  // --- Feature Flags ---
  static const featureFlagsView = 'FeatureFlags.View';
  static const featureFlagsCreate = 'FeatureFlags.Create';
  static const featureFlagsUpdate = 'FeatureFlags.Update';
  static const featureFlagsDelete = 'FeatureFlags.Delete';
  static const featureFlagsManageTenantOverrides =
      'FeatureFlags.ManageTenantOverrides';
  static const featureFlagsOptOut = 'FeatureFlags.OptOut';

  // --- API Keys ---
  static const apiKeysView = 'ApiKeys.View';
  static const apiKeysCreate = 'ApiKeys.Create';
  static const apiKeysUpdate = 'ApiKeys.Update';
  static const apiKeysDelete = 'ApiKeys.Delete';
  static const apiKeysViewPlatform = 'ApiKeys.ViewPlatform';
  static const apiKeysCreatePlatform = 'ApiKeys.CreatePlatform';
  static const apiKeysUpdatePlatform = 'ApiKeys.UpdatePlatform';
  static const apiKeysDeletePlatform = 'ApiKeys.DeletePlatform';
  static const apiKeysEmergencyRevoke = 'ApiKeys.EmergencyRevoke';
}
