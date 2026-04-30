using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Starter.Abstractions.Capabilities;
using Starter.Abstractions.Modularity;
using Starter.Module.Workflow.Constants;
using Starter.Module.Workflow.Infrastructure.Persistence;
using Starter.Module.Workflow.Infrastructure.Persistence.Seeds;
using Starter.Module.Workflow.Infrastructure.Services;

namespace Starter.Module.Workflow;

public sealed class WorkflowModule : IModule, IModuleBusContributor
{
    public string Name => "Starter.Module.Workflow";
    public string DisplayName => "Workflow & Approvals";
    public string Version => "1.0.0";
    // No hard runtime dependencies — Workflow couples to CommentsActivity and
    // Communication through capability contracts (ICommentableEntityRegistry,
    // ITemplateRegistrar) which have null-fallback registrations. The catalog
    // dependencies array (modules.catalog.json) surfaces this soft coupling
    // at generation time so users don't accidentally ship a Workflow-only app
    // with degraded comments/email; ModuleLoader.ResolveOrder enforces only
    // hard runtime ordering deps. See spec §14 D6.
    public IReadOnlyList<string> Dependencies => [];

    public IServiceCollection ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<WorkflowDbContext>((sp, options) =>
        {
            options.AddInterceptors(sp.GetServices<ISaveChangesInterceptor>());

            options.UseNpgsql(
                configuration.GetConnectionString("DefaultConnection"),
                npgsqlOptions =>
                {
                    npgsqlOptions.MigrationsHistoryTable("__EFMigrationsHistory_Workflow");
                    npgsqlOptions.MigrationsAssembly(typeof(WorkflowDbContext).Assembly.FullName);
                    npgsqlOptions.EnableRetryOnFailure(
                        maxRetryCount: 3,
                        maxRetryDelay: TimeSpan.FromSeconds(10),
                        errorCodesToAdd: ["40001"]);
                });
        });

        // Capability service — replaces NullWorkflowService
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<IWorkflowService, WorkflowEngine>();

        services.AddSingleton<IConditionEvaluator, ConditionEvaluator>();
        services.AddScoped<AssigneeResolverService>();
        services.AddScoped<IAssigneeResolverProvider, BuiltInAssigneeProvider>();
        services.AddScoped<HookExecutor>();
        services.AddScoped<HumanTaskFactory>();
        services.AddScoped<AutoTransitionEvaluator>();
        services.AddScoped<ParallelApprovalCoordinator>();
        services.AddSingleton<IFormDataValidator, FormDataValidator>();

        // Register WorkflowInstance as a commentable entity so the Comments &
        // Activity module accepts comments on workflow instances. This makes the
        // workflow detail page a collaboration hub with unified timeline.
        services.AddCommentableEntity("WorkflowInstance", builder =>
        {
            builder.CustomActivityTypes = ["workflow_transition"];
        });

        services.AddSingleton<IHostedService, SlaEscalationJob>();

        services.AddHealthChecks()
            .AddDbContextCheck<WorkflowDbContext>(
                name: "workflow-db",
                tags: ["db", "workflow"]);

        return services;
    }

    public void ConfigureBus(IBusRegistrationConfigurator bus)
    {
        // Modules own their own bus surface — the host no longer auto-discovers
        // consumers from module assemblies (Tier 2.5 Theme 5 Phase D), so each
        // module that ships an IConsumer<> opts in here.
        bus.AddConsumers(typeof(WorkflowModule).Assembly);

        // Registers a transactional EF outbox against WorkflowDbContext. With UseBusOutbox(),
        // IPublishEndpoint.Publish and ISendEndpoint.Send calls made while WorkflowDbContext is
        // the active DbContext are queued in the workflow outbox table and committed in the
        // same transaction as WorkflowDbContext.SaveChanges. MassTransit's background delivery
        // service then drains the outbox to the broker.
        bus.AddEntityFrameworkOutbox<WorkflowDbContext>(o =>
        {
            o.QueryDelay = TimeSpan.FromSeconds(1);
            o.UsePostgres();
            o.UseBusOutbox();
        });
    }

    public IEnumerable<(string Name, string Description, string Module)> GetPermissions()
    {
        yield return (WorkflowPermissions.View, "View workflow definitions and instances", "Workflow");
        yield return (WorkflowPermissions.ManageDefinitions, "Clone, edit, activate/deactivate definitions", "Workflow");
        yield return (WorkflowPermissions.Start, "Start a workflow on an entity", "Workflow");
        yield return (WorkflowPermissions.ActOnTask, "Approve/reject/return assigned tasks", "Workflow");
        yield return (WorkflowPermissions.Cancel, "Cancel an active workflow instance", "Workflow");
        yield return (WorkflowPermissions.ViewAllTasks, "See all pending tasks across users", "Workflow");
        yield return (WorkflowPermissions.ViewAnalytics, "View workflow analytics dashboards per definition", "Workflow");
    }

    public IEnumerable<(string Role, string[] Permissions)> GetDefaultRolePermissions()
    {
        yield return ("SuperAdmin", [
            WorkflowPermissions.View,
            WorkflowPermissions.ManageDefinitions,
            WorkflowPermissions.Start,
            WorkflowPermissions.ActOnTask,
            WorkflowPermissions.Cancel,
            WorkflowPermissions.ViewAllTasks,
            WorkflowPermissions.ViewAnalytics]);
        yield return ("Admin", [
            WorkflowPermissions.View,
            WorkflowPermissions.ManageDefinitions,
            WorkflowPermissions.Start,
            WorkflowPermissions.ActOnTask,
            WorkflowPermissions.Cancel,
            WorkflowPermissions.ViewAllTasks,
            WorkflowPermissions.ViewAnalytics]);
        yield return ("User", [
            WorkflowPermissions.View,
            WorkflowPermissions.Start,
            WorkflowPermissions.ActOnTask]);
    }

    public async Task MigrateAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        using var scope = services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<WorkflowDbContext>();
        await context.Database.MigrateAsync(cancellationToken);
    }

    public async Task SeedDataAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        using var scope = services.CreateScope();

        var templateRegistrar = scope.ServiceProvider.GetRequiredService<ITemplateRegistrar>();

        // Register notification templates. When the Communication module is not
        // installed, NullTemplateRegistrar silently no-ops.

        await templateRegistrar.RegisterTemplateAsync(
            name: "workflow.task-assigned",
            moduleSource: "Workflow",
            category: "workflow",
            description: "Notification sent to a user when they are assigned an approval task",
            subjectTemplate: "You have a new approval task",
            bodyTemplate: "Hi,\n\nYou have been assigned a new approval task for {{entityType}} ({{entityId}}).\n\nStep: {{stepName}}\nWorkflow: {{workflowName}}\n\nPlease review and take action in the app: {{appUrl}}",
            defaultChannel: NotificationChannelType.Email,
            availableChannels: ["Email", "InApp"],
            variableSchema: new()
            {
                ["entityType"] = "Type of entity the workflow is running on (e.g. LeaveRequest)",
                ["entityId"] = "ID of the entity",
                ["stepName"] = "Name of the workflow step requiring action",
                ["workflowName"] = "Name of the workflow definition",
                ["appUrl"] = "Base URL of the application",
            },
            sampleVariables: new()
            {
                ["entityType"] = "LeaveRequest",
                ["entityId"] = "3fa85f64-5717-4562-b3fc-2c963f66afa6",
                ["stepName"] = "Manager Approval",
                ["workflowName"] = "Leave Approval",
                ["appUrl"] = "https://app.example.com",
            },
            ct: cancellationToken);

        await templateRegistrar.RegisterTemplateAsync(
            name: "workflow.request-approved",
            moduleSource: "Workflow",
            category: "workflow",
            description: "Notification sent to the initiator when their request is approved",
            subjectTemplate: "Your request was approved",
            bodyTemplate: "Hi,\n\nYour {{entityType}} request has been approved.\n\nWorkflow: {{workflowName}}\nApproved by: {{actorName}}\n\nView details in the app: {{appUrl}}",
            defaultChannel: NotificationChannelType.Email,
            availableChannels: ["Email", "InApp"],
            variableSchema: new()
            {
                ["entityType"] = "Type of entity the workflow is running on",
                ["entityId"] = "ID of the entity",
                ["workflowName"] = "Name of the workflow definition",
                ["actorName"] = "Display name of the approver",
                ["appUrl"] = "Base URL of the application",
            },
            sampleVariables: new()
            {
                ["entityType"] = "LeaveRequest",
                ["entityId"] = "3fa85f64-5717-4562-b3fc-2c963f66afa6",
                ["workflowName"] = "Leave Approval",
                ["actorName"] = "Saman Jasim",
                ["appUrl"] = "https://app.example.com",
            },
            ct: cancellationToken);

        await templateRegistrar.RegisterTemplateAsync(
            name: "workflow.request-rejected",
            moduleSource: "Workflow",
            category: "workflow",
            description: "Notification sent to the initiator when their request is rejected",
            subjectTemplate: "Your request was rejected",
            bodyTemplate: "Hi,\n\nYour {{entityType}} request has been rejected.\n\nWorkflow: {{workflowName}}\nRejected by: {{actorName}}\nReason: {{comment}}\n\nView details in the app: {{appUrl}}",
            defaultChannel: NotificationChannelType.Email,
            availableChannels: ["Email", "InApp"],
            variableSchema: new()
            {
                ["entityType"] = "Type of entity the workflow is running on",
                ["entityId"] = "ID of the entity",
                ["workflowName"] = "Name of the workflow definition",
                ["actorName"] = "Display name of the reviewer who rejected",
                ["comment"] = "Optional rejection reason or comment",
                ["appUrl"] = "Base URL of the application",
            },
            sampleVariables: new()
            {
                ["entityType"] = "LeaveRequest",
                ["entityId"] = "3fa85f64-5717-4562-b3fc-2c963f66afa6",
                ["workflowName"] = "Leave Approval",
                ["actorName"] = "Saman Jasim",
                ["comment"] = "Insufficient remaining leave balance.",
                ["appUrl"] = "https://app.example.com",
            },
            ct: cancellationToken);

        await templateRegistrar.RegisterTemplateAsync(
            name: "workflow.task-returned",
            moduleSource: "Workflow",
            category: "workflow",
            description: "Notification sent to the initiator when their request is returned for revision",
            subjectTemplate: "Your request was returned for revision",
            bodyTemplate: "Hi,\n\nYour {{entityType}} request has been returned for revision.\n\nWorkflow: {{workflowName}}\nReturned by: {{actorName}}\nComments: {{comment}}\n\nPlease update your request in the app: {{appUrl}}",
            defaultChannel: NotificationChannelType.Email,
            availableChannels: ["Email", "InApp"],
            variableSchema: new()
            {
                ["entityType"] = "Type of entity the workflow is running on",
                ["entityId"] = "ID of the entity",
                ["workflowName"] = "Name of the workflow definition",
                ["actorName"] = "Display name of the reviewer who returned it",
                ["comment"] = "Reviewer's comments for revision",
                ["appUrl"] = "Base URL of the application",
            },
            sampleVariables: new()
            {
                ["entityType"] = "LeaveRequest",
                ["entityId"] = "3fa85f64-5717-4562-b3fc-2c963f66afa6",
                ["workflowName"] = "Leave Approval",
                ["actorName"] = "Saman Jasim",
                ["comment"] = "Please attach supporting documentation.",
                ["appUrl"] = "https://app.example.com",
            },
            ct: cancellationToken);

        await templateRegistrar.RegisterTemplateAsync(
            name: "workflow.sla-reminder",
            moduleSource: "Workflow",
            category: "workflow",
            description: "Reminder sent to an assignee when their approval task is approaching the SLA deadline",
            subjectTemplate: "Reminder: approval task overdue",
            bodyTemplate: "Hi,\n\nYour approval task for {{entityType}} ({{entityId}}) is overdue.\n\nStep: {{stepName}}\nWorkflow: {{workflowName}}\n\nPlease take action in the app: {{appUrl}}",
            defaultChannel: NotificationChannelType.Email,
            availableChannels: ["Email", "InApp"],
            variableSchema: new()
            {
                ["entityType"] = "Type of entity the workflow is running on",
                ["entityId"] = "ID of the entity",
                ["stepName"] = "Name of the workflow step requiring action",
                ["workflowName"] = "Name of the workflow definition",
                ["appUrl"] = "Base URL of the application",
            },
            sampleVariables: new()
            {
                ["entityType"] = "LeaveRequest",
                ["entityId"] = "3fa85f64-5717-4562-b3fc-2c963f66afa6",
                ["stepName"] = "Manager Approval",
                ["workflowName"] = "Leave Approval",
                ["appUrl"] = "https://app.example.com",
            },
            ct: cancellationToken);

        await templateRegistrar.RegisterTemplateAsync(
            name: "workflow.sla-escalated",
            moduleSource: "Workflow",
            category: "workflow",
            description: "Notification sent to a fallback assignee when a task is escalated due to SLA breach",
            subjectTemplate: "Escalated: approval task reassigned to you",
            bodyTemplate: "Hi,\n\nAn approval task for {{entityType}} ({{entityId}}) has been escalated to you because the original assignee did not act within the SLA window.\n\nStep: {{stepName}}\nWorkflow: {{workflowName}}\n\nPlease take action in the app: {{appUrl}}",
            defaultChannel: NotificationChannelType.Email,
            availableChannels: ["Email", "InApp"],
            variableSchema: new()
            {
                ["entityType"] = "Type of entity the workflow is running on",
                ["entityId"] = "ID of the entity",
                ["stepName"] = "Name of the workflow step requiring action",
                ["workflowName"] = "Name of the workflow definition",
                ["appUrl"] = "Base URL of the application",
            },
            sampleVariables: new()
            {
                ["entityType"] = "LeaveRequest",
                ["entityId"] = "3fa85f64-5717-4562-b3fc-2c963f66afa6",
                ["stepName"] = "Manager Approval",
                ["workflowName"] = "Leave Approval",
                ["appUrl"] = "https://app.example.com",
            },
            ct: cancellationToken);

        await templateRegistrar.RegisterTemplateAsync(
            name: "workflow.delegation-created",
            moduleSource: "Workflow",
            category: "workflow",
            description: "Notification sent to a user when someone delegates their approval tasks to them",
            subjectTemplate: "You have been assigned as a delegate",
            bodyTemplate: "Hi,\n\nYou have been assigned as a delegate for approval tasks from {{startDate}} to {{endDate}}.\n\nAny tasks assigned to the delegator during this period will be routed to you instead.",
            defaultChannel: NotificationChannelType.Email,
            availableChannels: ["Email", "InApp"],
            variableSchema: new()
            {
                ["startDate"] = "Delegation start date",
                ["endDate"] = "Delegation end date",
            },
            sampleVariables: new()
            {
                ["startDate"] = "2026-04-20",
                ["endDate"] = "2026-04-27",
            },
            ct: cancellationToken);

        // Register workflow lifecycle events for use in Trigger Rules
        await templateRegistrar.RegisterEventAsync(
            eventName: "workflow.started",
            moduleSource: "Workflow",
            displayName: "Workflow Started",
            description: "Fires when a workflow instance is started on an entity",
            ct: cancellationToken);

        await templateRegistrar.RegisterEventAsync(
            eventName: "workflow.transitioned",
            moduleSource: "Workflow",
            displayName: "Workflow State Transitioned",
            description: "Fires when a workflow instance transitions from one state to another",
            ct: cancellationToken);

        await templateRegistrar.RegisterEventAsync(
            eventName: "workflow.completed",
            moduleSource: "Workflow",
            displayName: "Workflow Completed",
            description: "Fires when a workflow instance reaches a terminal state",
            ct: cancellationToken);

        await templateRegistrar.RegisterEventAsync(
            eventName: "workflow.task-assigned",
            moduleSource: "Workflow",
            displayName: "Workflow Task Assigned",
            description: "Fires when an approval task is assigned to a user",
            ct: cancellationToken);

        // ── Seed workflow definition templates ──
        var workflowService = scope.ServiceProvider.GetRequiredService<IWorkflowService>();

        // 1. General Approval — the most common pattern: submit → manager review → approved/rejected
        await workflowService.SeedTemplateAsync(
            name: "general-approval",
            entityType: "General",
            config: new WorkflowTemplateConfig(
                DisplayName: "General Approval",
                Description: "A simple approval workflow: submit for review, approve or reject. Clone and customize for any entity type.",
                States:
                [
                    new("Draft", "Draft", "Initial"),
                    new("PendingReview", "Pending Review", "HumanTask",
                        Assignee: new("Role", new() { ["roleName"] = "Admin" }),
                        Actions: ["Approve", "Reject", "ReturnForRevision"],
                        OnEnter:
                        [
                            new("notify", Template: "workflow.task-assigned", To: "assignee"),
                            new("inAppNotify", To: "assignee"),
                            new("activity", Action: "workflow_transition"),
                        ]),
                    new("Approved", "Approved", "Terminal",
                        OnEnter:
                        [
                            new("notify", Template: "workflow.request-approved", To: "initiator"),
                            new("inAppNotify", To: "initiator"),
                            new("activity", Action: "workflow_transition"),
                        ]),
                    new("Rejected", "Rejected", "Terminal",
                        OnEnter:
                        [
                            new("notify", Template: "workflow.request-rejected", To: "initiator"),
                            new("inAppNotify", To: "initiator"),
                            new("activity", Action: "workflow_transition"),
                        ]),
                ],
                Transitions:
                [
                    new("Draft", "PendingReview", "Submit"),
                    new("PendingReview", "Approved", "Approve"),
                    new("PendingReview", "Rejected", "Reject"),
                    new("PendingReview", "Draft", "ReturnForRevision"),
                ]),
            ct: cancellationToken);

        // 2. Two-Level Approval — submit → manager → director → approved/rejected
        await workflowService.SeedTemplateAsync(
            name: "two-level-approval",
            entityType: "General",
            config: new WorkflowTemplateConfig(
                DisplayName: "Two-Level Approval",
                Description: "Two-step approval chain: first-level reviewer, then second-level approver. Clone and assign specific users or roles per level.",
                States:
                [
                    new("Draft", "Draft", "Initial"),
                    new("PendingFirstReview", "Pending First Review", "HumanTask",
                        Assignee: new("Role", new() { ["roleName"] = "Admin" }),
                        Actions: ["Approve", "Reject", "ReturnForRevision"],
                        OnEnter:
                        [
                            new("notify", Template: "workflow.task-assigned", To: "assignee"),
                            new("inAppNotify", To: "assignee"),
                            new("activity", Action: "workflow_transition"),
                        ]),
                    new("PendingFinalApproval", "Pending Final Approval", "HumanTask",
                        Assignee: new("Role", new() { ["roleName"] = "Admin" },
                            Fallback: new("Role", new() { ["roleName"] = "SuperAdmin" })),
                        Actions: ["Approve", "Reject"],
                        OnEnter:
                        [
                            new("notify", Template: "workflow.task-assigned", To: "assignee"),
                            new("inAppNotify", To: "assignee"),
                            new("activity", Action: "workflow_transition"),
                        ]),
                    new("Approved", "Approved", "Terminal",
                        OnEnter:
                        [
                            new("notify", Template: "workflow.request-approved", To: "initiator"),
                            new("inAppNotify", To: "initiator"),
                            new("activity", Action: "workflow_transition"),
                            new("webhook", Event: "workflow.completed"),
                        ]),
                    new("Rejected", "Rejected", "Terminal",
                        OnEnter:
                        [
                            new("notify", Template: "workflow.request-rejected", To: "initiator"),
                            new("inAppNotify", To: "initiator"),
                            new("activity", Action: "workflow_transition"),
                        ]),
                ],
                Transitions:
                [
                    new("Draft", "PendingFirstReview", "Submit"),
                    new("PendingFirstReview", "PendingFinalApproval", "Approve"),
                    new("PendingFirstReview", "Rejected", "Reject"),
                    new("PendingFirstReview", "Draft", "ReturnForRevision"),
                    new("PendingFinalApproval", "Approved", "Approve"),
                    new("PendingFinalApproval", "Rejected", "Reject"),
                ]),
            ct: cancellationToken);

        // 3. Document Review — draft → review → revision loop or publish
        await workflowService.SeedTemplateAsync(
            name: "document-review",
            entityType: "Document",
            config: new WorkflowTemplateConfig(
                DisplayName: "Document Review",
                Description: "Review workflow for documents and content: submit for review, approve to publish or return for revision.",
                States:
                [
                    new("Draft", "Draft", "Initial"),
                    new("InReview", "In Review", "HumanTask",
                        Assignee: new("Role", new() { ["roleName"] = "Admin" }),
                        Actions: ["Publish", "RequestChanges"],
                        OnEnter:
                        [
                            new("notify", Template: "workflow.task-assigned", To: "assignee"),
                            new("inAppNotify", To: "assignee"),
                            new("activity", Action: "workflow_transition"),
                        ]),
                    new("Published", "Published", "Terminal",
                        OnEnter:
                        [
                            new("notify", Template: "workflow.request-approved", To: "initiator"),
                            new("activity", Action: "workflow_transition"),
                            new("webhook", Event: "workflow.completed"),
                        ]),
                ],
                Transitions:
                [
                    new("Draft", "InReview", "Submit"),
                    new("InReview", "Published", "Publish"),
                    new("InReview", "Draft", "RequestChanges"),
                ]),
            ct: cancellationToken);

        // 4. Expense Approval with Dynamic Forms + SLA — demonstrates formFields and SLA tracking
        await workflowService.SeedTemplateAsync(
            name: "expense-approval",
            entityType: "Expense",
            config: new WorkflowTemplateConfig(
                DisplayName: "Expense Approval with Review Form",
                Description: "Approval with structured form data collection. Reviewer must fill in approved amount and reason. SLA: reminder after 4h, escalation after 8h.",
                States:
                [
                    new("Draft", "Draft", "Initial"),
                    new("ManagerReview", "Manager Review", "HumanTask",
                        Assignee: new("Role", new() { ["roleName"] = "Admin" }),
                        Actions: ["Approve", "Reject", "ReturnForRevision"],
                        FormFields:
                        [
                            new("approvedAmount", "Approved Amount ($)", "number", Required: true, Min: 0, Max: 100000),
                            new("category", "Expense Category", "select", Required: true,
                                Options: [new("travel", "Travel"), new("equipment", "Equipment"), new("software", "Software"), new("other", "Other")]),
                            new("notes", "Review Notes", "textarea", Required: false, MaxLength: 500, Placeholder: "Any additional notes about this expense..."),
                            new("confirmed", "I confirm this expense complies with company policy", "checkbox", Required: true),
                        ],
                        Sla: new(ReminderAfterHours: 4, EscalateAfterHours: 8),
                        OnEnter:
                        [
                            new("notify", Template: "workflow.task-assigned", To: "assignee"),
                            new("inAppNotify", To: "assignee"),
                            new("activity", Action: "workflow_transition"),
                        ]),
                    new("Approved", "Approved", "Terminal",
                        OnEnter:
                        [
                            new("notify", Template: "workflow.request-approved", To: "initiator"),
                            new("activity", Action: "workflow_transition"),
                        ]),
                    new("Rejected", "Rejected", "Terminal",
                        OnEnter:
                        [
                            new("notify", Template: "workflow.request-rejected", To: "initiator"),
                            new("activity", Action: "workflow_transition"),
                        ]),
                ],
                Transitions:
                [
                    new("Draft", "ManagerReview", "Submit"),
                    new("ManagerReview", "Approved", "Approve"),
                    new("ManagerReview", "Rejected", "Reject"),
                    new("ManagerReview", "Draft", "ReturnForRevision"),
                ]),
            ct: cancellationToken);

        // 5. Board Resolution with Parallel Approval — demonstrates AllOf parallel + compound conditions
        await workflowService.SeedTemplateAsync(
            name: "board-resolution",
            entityType: "Resolution",
            config: new WorkflowTemplateConfig(
                DisplayName: "Board Resolution (Parallel Approval)",
                Description: "Requires approval from multiple board members simultaneously (AllOf). All must approve for the resolution to pass.",
                States:
                [
                    new("Draft", "Draft", "Initial"),
                    new("BoardVote", "Board Vote", "HumanTask",
                        Parallel: new("AllOf",
                        [
                            new("Role", new() { ["roleName"] = "Admin" }),
                        ]),
                        Actions: ["Approve", "Reject"],
                        OnEnter:
                        [
                            new("notify", Template: "workflow.task-assigned", To: "assignee"),
                            new("inAppNotify", To: "assignee"),
                            new("activity", Action: "workflow_transition"),
                        ]),
                    new("Passed", "Resolution Passed", "Terminal",
                        OnEnter:
                        [
                            new("notify", Template: "workflow.request-approved", To: "initiator"),
                            new("activity", Action: "workflow_transition"),
                            new("webhook", Event: "workflow.completed"),
                        ]),
                    new("Failed", "Resolution Failed", "Terminal",
                        OnEnter:
                        [
                            new("notify", Template: "workflow.request-rejected", To: "initiator"),
                            new("activity", Action: "workflow_transition"),
                        ]),
                ],
                Transitions:
                [
                    new("Draft", "BoardVote", "Submit"),
                    new("BoardVote", "Passed", "Approve"),
                    new("BoardVote", "Failed", "Reject"),
                ]),
            ct: cancellationToken);

        // Seed a demo fixture of instances + tasks spanning every status / SLA
        // bucket the redesigned Phase 5a inbox + instance list surfaces. Idempotent;
        // skips after the first run via a marker entity type.
        await WorkflowDemoInstanceSeeder.SeedAsync(services, cancellationToken);
    }
}
