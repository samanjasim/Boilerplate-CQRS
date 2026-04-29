// AUTO-GENERATED — DO NOT EDIT.
// Regenerate with `npm run generate:permissions` from the repo root.
// CI fails on drift; the BE permission constants are the single source of truth.
//
// Source files:
//   - boilerplateBE/src/Starter.Shared/Constants/Permissions.cs
//   - boilerplateBE/src/modules/Starter.Module.AI/Constants/AiPermissions.cs
//   - boilerplateBE/src/modules/Starter.Module.Billing/Constants/BillingPermissions.cs
//   - boilerplateBE/src/modules/Starter.Module.CommentsActivity/Constants/CommentsActivityPermissions.cs
//   - boilerplateBE/src/modules/Starter.Module.Communication/Constants/CommunicationPermissions.cs
//   - boilerplateBE/src/modules/Starter.Module.ImportExport/Constants/ImportExportPermissions.cs
//   - boilerplateBE/src/modules/Starter.Module.Products/Constants/ProductPermissions.cs
//   - boilerplateBE/src/modules/Starter.Module.Webhooks/Constants/WebhookPermissions.cs
//   - boilerplateBE/src/modules/Starter.Module.Workflow/Constants/WorkflowPermissions.cs

// ignore_for_file: constant_identifier_names, lines_longer_than_80_chars

abstract final class Permissions {

  // ─── Activity ───
  static const activityView = 'Activity.View';

  // ─── Ai ───
  static const aiAgentsApproveAction = 'Ai.AgentsApproveAction';
  static const aiAgentsViewApprovals = 'Ai.AgentsViewApprovals';
  static const aiAssignAgentRole = 'Ai.AssignAgentRole';
  static const aiAssignPersona = 'Ai.AssignPersona';
  static const aiChat = 'Ai.Chat';
  static const aiDeleteConversation = 'Ai.DeleteConversation';
  static const aiManageAgentBudget = 'Ai.ManageAgentBudget';
  static const aiManageAssistants = 'Ai.ManageAssistants';
  static const aiManageDocuments = 'Ai.ManageDocuments';
  static const aiManagePersonas = 'Ai.ManagePersonas';
  static const aiManagePricing = 'Ai.ManagePricing';
  static const aiManageSettings = 'Ai.ManageSettings';
  static const aiManageTools = 'Ai.ManageTools';
  static const aiManageTriggers = 'Ai.ManageTriggers';
  static const aiModerationView = 'Ai.ModerationView';
  static const aiRunAgentTasks = 'Ai.RunAgentTasks';
  static const aiRunEval = 'Ai.RunEval';
  static const aiSafetyProfilesManage = 'Ai.SafetyProfilesManage';
  static const aiSearchKnowledgeBase = 'Ai.SearchKnowledgeBase';
  static const aiViewConversations = 'Ai.ViewConversations';
  static const aiViewPersonas = 'Ai.ViewPersonas';
  static const aiViewUsage = 'Ai.ViewUsage';

  // ─── ApiKeys ───
  static const apiKeysCreate = 'ApiKeys.Create';
  static const apiKeysCreatePlatform = 'ApiKeys.CreatePlatform';
  static const apiKeysDelete = 'ApiKeys.Delete';
  static const apiKeysDeletePlatform = 'ApiKeys.DeletePlatform';
  static const apiKeysEmergencyRevoke = 'ApiKeys.EmergencyRevoke';
  static const apiKeysUpdate = 'ApiKeys.Update';
  static const apiKeysUpdatePlatform = 'ApiKeys.UpdatePlatform';
  static const apiKeysView = 'ApiKeys.View';
  static const apiKeysViewPlatform = 'ApiKeys.ViewPlatform';

  // ─── Billing ───
  static const billingManage = 'Billing.Manage';
  static const billingManagePlans = 'Billing.ManagePlans';
  static const billingManageTenantSubscriptions = 'Billing.ManageTenantSubscriptions';
  static const billingView = 'Billing.View';
  static const billingViewPlans = 'Billing.ViewPlans';

  // ─── Comments ───
  static const commentsCreate = 'Comments.Create';
  static const commentsDelete = 'Comments.Delete';
  static const commentsEdit = 'Comments.Edit';
  static const commentsManage = 'Comments.Manage';
  static const commentsView = 'Comments.View';

  // ─── Communication ───
  static const communicationManageChannels = 'Communication.ManageChannels';
  static const communicationManageIntegrations = 'Communication.ManageIntegrations';
  static const communicationManageQuotas = 'Communication.ManageQuotas';
  static const communicationManageTemplates = 'Communication.ManageTemplates';
  static const communicationManageTriggerRules = 'Communication.ManageTriggerRules';
  static const communicationResend = 'Communication.Resend';
  static const communicationView = 'Communication.View';
  static const communicationViewDeliveryLog = 'Communication.ViewDeliveryLog';

  // ─── FeatureFlags ───
  static const featureFlagsCreate = 'FeatureFlags.Create';
  static const featureFlagsDelete = 'FeatureFlags.Delete';
  static const featureFlagsManageTenantOverrides = 'FeatureFlags.ManageTenantOverrides';
  static const featureFlagsOptOut = 'FeatureFlags.OptOut';
  static const featureFlagsUpdate = 'FeatureFlags.Update';
  static const featureFlagsView = 'FeatureFlags.View';

  // ─── Files ───
  static const filesDelete = 'Files.Delete';
  static const filesManage = 'Files.Manage';
  static const filesShareOwn = 'Files.ShareOwn';
  static const filesUpload = 'Files.Upload';
  static const filesView = 'Files.View';

  // ─── Products ───
  static const productsCreate = 'Products.Create';
  static const productsUpdate = 'Products.Update';
  static const productsView = 'Products.View';

  // ─── Roles ───
  static const rolesCreate = 'Roles.Create';
  static const rolesDelete = 'Roles.Delete';
  static const rolesManagePermissions = 'Roles.ManagePermissions';
  static const rolesShow = 'Roles.Show';
  static const rolesUpdate = 'Roles.Update';
  static const rolesView = 'Roles.View';

  // ─── System ───
  static const systemExportData = 'System.ExportData';
  static const systemForceExport = 'System.ForceExport';
  static const systemImportData = 'System.ImportData';
  static const systemManageSettings = 'System.ManageSettings';
  static const systemViewAuditLogs = 'System.ViewAuditLogs';
  static const systemViewDashboard = 'System.ViewDashboard';

  // ─── Tenants ───
  static const tenantsCreate = 'Tenants.Create';
  static const tenantsDelete = 'Tenants.Delete';
  static const tenantsShow = 'Tenants.Show';
  static const tenantsUpdate = 'Tenants.Update';
  static const tenantsView = 'Tenants.View';

  // ─── Users ───
  static const usersCreate = 'Users.Create';
  static const usersDelete = 'Users.Delete';
  static const usersManageRoles = 'Users.ManageRoles';
  static const usersShow = 'Users.Show';
  static const usersUpdate = 'Users.Update';
  static const usersView = 'Users.View';

  // ─── Webhooks ───
  static const webhooksCreate = 'Webhooks.Create';
  static const webhooksDelete = 'Webhooks.Delete';
  static const webhooksUpdate = 'Webhooks.Update';
  static const webhooksView = 'Webhooks.View';
  static const webhooksViewPlatform = 'Webhooks.ViewPlatform';

  // ─── Workflows ───
  static const workflowsActOnTask = 'Workflows.ActOnTask';
  static const workflowsCancel = 'Workflows.Cancel';
  static const workflowsManageDefinitions = 'Workflows.ManageDefinitions';
  static const workflowsStart = 'Workflows.Start';
  static const workflowsView = 'Workflows.View';
  static const workflowsViewAllTasks = 'Workflows.ViewAllTasks';
  static const workflowsViewAnalytics = 'Workflows.ViewAnalytics';
}
