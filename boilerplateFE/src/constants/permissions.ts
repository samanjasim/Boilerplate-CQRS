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
    ShareOwn: 'Files.ShareOwn',
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
  Products: {
    View: 'Products.View',
    Create: 'Products.Create',
    Update: 'Products.Update',
  },
  Comments: {
    View: 'Comments.View',
    Create: 'Comments.Create',
    Edit: 'Comments.Edit',
    Delete: 'Comments.Delete',
    Manage: 'Comments.Manage',
  },
  Activity: {
    View: 'Activity.View',
  },
  System: {
    ViewAuditLogs: 'System.ViewAuditLogs',
    ManageSettings: 'System.ManageSettings',
    ViewDashboard: 'System.ViewDashboard',
    ExportData: 'System.ExportData',
    ForceExport: 'System.ForceExport',
    ImportData: 'System.ImportData',
  },
  Communication: {
    View: 'Communication.View',
    ManageChannels: 'Communication.ManageChannels',
    ManageIntegrations: 'Communication.ManageIntegrations',
    ManageTemplates: 'Communication.ManageTemplates',
    ManageTriggerRules: 'Communication.ManageTriggerRules',
    ViewDeliveryLog: 'Communication.ViewDeliveryLog',
    Resend: 'Communication.Resend',
    ManageQuotas: 'Communication.ManageQuotas',
  },
  Webhooks: {
    View: 'Webhooks.View',
    Create: 'Webhooks.Create',
    Update: 'Webhooks.Update',
    Delete: 'Webhooks.Delete',
    ViewPlatform: 'Webhooks.ViewPlatform',
  },
  Ai: {
    Chat: 'Ai.Chat',
    ViewConversations: 'Ai.ViewConversations',
    DeleteConversation: 'Ai.DeleteConversation',
    ManageAssistants: 'Ai.ManageAssistants',
    ManageDocuments: 'Ai.ManageDocuments',
    ManageTools: 'Ai.ManageTools',
    ManageTriggers: 'Ai.ManageTriggers',
    ViewUsage: 'Ai.ViewUsage',
    RunAgentTasks: 'Ai.RunAgentTasks',
    ManageSettings: 'Ai.ManageSettings',
    SearchKnowledgeBase: 'Ai.SearchKnowledgeBase',
    RunEval: 'Ai.RunEval',
  },
  Workflows: {
    View: 'Workflows.View',
    ManageDefinitions: 'Workflows.ManageDefinitions',
    Start: 'Workflows.Start',
    ActOnTask: 'Workflows.ActOnTask',
    Cancel: 'Workflows.Cancel',
    ViewAllTasks: 'Workflows.ViewAllTasks',
  },
} as const;

type PermissionMap = typeof PERMISSIONS;
export type Permission = {
  [M in keyof PermissionMap]: PermissionMap[M][keyof PermissionMap[M]];
}[keyof PermissionMap];
