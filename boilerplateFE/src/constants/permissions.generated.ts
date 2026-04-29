/**
 * AUTO-GENERATED — DO NOT EDIT.
 * Regenerate with `npm run generate:permissions` from the repo root.
 * CI fails on drift; the BE permission constants are the single source of truth.
 *
 * Source files:
 *   - boilerplateBE/src/Starter.Shared/Constants/Permissions.cs
 *   - boilerplateBE/src/modules/Starter.Module.AI/Constants/AiPermissions.cs
 *   - boilerplateBE/src/modules/Starter.Module.Billing/Constants/BillingPermissions.cs
 *   - boilerplateBE/src/modules/Starter.Module.CommentsActivity/Constants/CommentsActivityPermissions.cs
 *   - boilerplateBE/src/modules/Starter.Module.Communication/Constants/CommunicationPermissions.cs
 *   - boilerplateBE/src/modules/Starter.Module.ImportExport/Constants/ImportExportPermissions.cs
 *   - boilerplateBE/src/modules/Starter.Module.Products/Constants/ProductPermissions.cs
 *   - boilerplateBE/src/modules/Starter.Module.Webhooks/Constants/WebhookPermissions.cs
 *   - boilerplateBE/src/modules/Starter.Module.Workflow/Constants/WorkflowPermissions.cs
 */
export const PERMISSIONS = {
  Activity: {
    View: 'Activity.View',
  },
  Ai: {
    AgentsApproveAction: 'Ai.AgentsApproveAction',
    AgentsViewApprovals: 'Ai.AgentsViewApprovals',
    AssignAgentRole: 'Ai.AssignAgentRole',
    AssignPersona: 'Ai.AssignPersona',
    Chat: 'Ai.Chat',
    DeleteConversation: 'Ai.DeleteConversation',
    ManageAgentBudget: 'Ai.ManageAgentBudget',
    ManageAssistants: 'Ai.ManageAssistants',
    ManageDocuments: 'Ai.ManageDocuments',
    ManagePersonas: 'Ai.ManagePersonas',
    ManagePricing: 'Ai.ManagePricing',
    ManageSettings: 'Ai.ManageSettings',
    ManageTools: 'Ai.ManageTools',
    ManageTriggers: 'Ai.ManageTriggers',
    ModerationView: 'Ai.ModerationView',
    RunAgentTasks: 'Ai.RunAgentTasks',
    RunEval: 'Ai.RunEval',
    SafetyProfilesManage: 'Ai.SafetyProfilesManage',
    SearchKnowledgeBase: 'Ai.SearchKnowledgeBase',
    ViewConversations: 'Ai.ViewConversations',
    ViewPersonas: 'Ai.ViewPersonas',
    ViewUsage: 'Ai.ViewUsage',
  },
  ApiKeys: {
    Create: 'ApiKeys.Create',
    CreatePlatform: 'ApiKeys.CreatePlatform',
    Delete: 'ApiKeys.Delete',
    DeletePlatform: 'ApiKeys.DeletePlatform',
    EmergencyRevoke: 'ApiKeys.EmergencyRevoke',
    Update: 'ApiKeys.Update',
    UpdatePlatform: 'ApiKeys.UpdatePlatform',
    View: 'ApiKeys.View',
    ViewPlatform: 'ApiKeys.ViewPlatform',
  },
  Billing: {
    Manage: 'Billing.Manage',
    ManagePlans: 'Billing.ManagePlans',
    ManageTenantSubscriptions: 'Billing.ManageTenantSubscriptions',
    View: 'Billing.View',
    ViewPlans: 'Billing.ViewPlans',
  },
  Comments: {
    Create: 'Comments.Create',
    Delete: 'Comments.Delete',
    Edit: 'Comments.Edit',
    Manage: 'Comments.Manage',
    View: 'Comments.View',
  },
  Communication: {
    ManageChannels: 'Communication.ManageChannels',
    ManageIntegrations: 'Communication.ManageIntegrations',
    ManageQuotas: 'Communication.ManageQuotas',
    ManageTemplates: 'Communication.ManageTemplates',
    ManageTriggerRules: 'Communication.ManageTriggerRules',
    Resend: 'Communication.Resend',
    View: 'Communication.View',
    ViewDeliveryLog: 'Communication.ViewDeliveryLog',
  },
  FeatureFlags: {
    Create: 'FeatureFlags.Create',
    Delete: 'FeatureFlags.Delete',
    ManageTenantOverrides: 'FeatureFlags.ManageTenantOverrides',
    OptOut: 'FeatureFlags.OptOut',
    Update: 'FeatureFlags.Update',
    View: 'FeatureFlags.View',
  },
  Files: {
    Delete: 'Files.Delete',
    Manage: 'Files.Manage',
    ShareOwn: 'Files.ShareOwn',
    Upload: 'Files.Upload',
    View: 'Files.View',
  },
  Products: {
    Create: 'Products.Create',
    Update: 'Products.Update',
    View: 'Products.View',
  },
  Roles: {
    Create: 'Roles.Create',
    Delete: 'Roles.Delete',
    ManagePermissions: 'Roles.ManagePermissions',
    Show: 'Roles.Show',
    Update: 'Roles.Update',
    View: 'Roles.View',
  },
  System: {
    ExportData: 'System.ExportData',
    ForceExport: 'System.ForceExport',
    ImportData: 'System.ImportData',
    ManageSettings: 'System.ManageSettings',
    ViewAuditLogs: 'System.ViewAuditLogs',
    ViewDashboard: 'System.ViewDashboard',
  },
  Tenants: {
    Create: 'Tenants.Create',
    Delete: 'Tenants.Delete',
    Show: 'Tenants.Show',
    Update: 'Tenants.Update',
    View: 'Tenants.View',
  },
  Users: {
    Create: 'Users.Create',
    Delete: 'Users.Delete',
    ManageRoles: 'Users.ManageRoles',
    Show: 'Users.Show',
    Update: 'Users.Update',
    View: 'Users.View',
  },
  Webhooks: {
    Create: 'Webhooks.Create',
    Delete: 'Webhooks.Delete',
    Update: 'Webhooks.Update',
    View: 'Webhooks.View',
    ViewPlatform: 'Webhooks.ViewPlatform',
  },
  Workflows: {
    ActOnTask: 'Workflows.ActOnTask',
    Cancel: 'Workflows.Cancel',
    ManageDefinitions: 'Workflows.ManageDefinitions',
    Start: 'Workflows.Start',
    View: 'Workflows.View',
    ViewAllTasks: 'Workflows.ViewAllTasks',
    ViewAnalytics: 'Workflows.ViewAnalytics',
  },
} as const;

type PermissionMap = typeof PERMISSIONS;
export type Permission = {
  [M in keyof PermissionMap]: PermissionMap[M][keyof PermissionMap[M]];
}[keyof PermissionMap];
