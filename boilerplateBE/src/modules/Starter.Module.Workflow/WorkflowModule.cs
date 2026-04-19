using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Starter.Abstractions.Capabilities;
using Starter.Abstractions.Modularity;
using Starter.Module.Workflow.Constants;
using Starter.Module.Workflow.Infrastructure.Persistence;
using Starter.Module.Workflow.Infrastructure.Services;

namespace Starter.Module.Workflow;

public sealed class WorkflowModule : IModule
{
    public string Name => "Starter.Module.Workflow";
    public string DisplayName => "Workflow & Approvals";
    public string Version => "1.0.0";
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
        services.AddScoped<IWorkflowService, WorkflowEngine>();

        services.AddSingleton<IConditionEvaluator, ConditionEvaluator>();
        services.AddScoped<AssigneeResolverService>();
        services.AddScoped<IAssigneeResolverProvider, BuiltInAssigneeProvider>();
        services.AddScoped<HookExecutor>();

        services.AddHealthChecks()
            .AddDbContextCheck<WorkflowDbContext>(
                name: "workflow-db",
                tags: ["db", "workflow"]);

        return services;
    }

    public IEnumerable<(string Name, string Description, string Module)> GetPermissions()
    {
        yield return (WorkflowPermissions.View, "View workflow definitions and instances", "Workflow");
        yield return (WorkflowPermissions.ManageDefinitions, "Clone, edit, activate/deactivate definitions", "Workflow");
        yield return (WorkflowPermissions.Start, "Start a workflow on an entity", "Workflow");
        yield return (WorkflowPermissions.ActOnTask, "Approve/reject/return assigned tasks", "Workflow");
        yield return (WorkflowPermissions.Cancel, "Cancel an active workflow instance", "Workflow");
        yield return (WorkflowPermissions.ViewAllTasks, "See all pending tasks across users", "Workflow");
    }

    public IEnumerable<(string Role, string[] Permissions)> GetDefaultRolePermissions()
    {
        yield return ("SuperAdmin", [
            WorkflowPermissions.View,
            WorkflowPermissions.ManageDefinitions,
            WorkflowPermissions.Start,
            WorkflowPermissions.ActOnTask,
            WorkflowPermissions.Cancel,
            WorkflowPermissions.ViewAllTasks]);
        yield return ("Admin", [
            WorkflowPermissions.View,
            WorkflowPermissions.ManageDefinitions,
            WorkflowPermissions.Start,
            WorkflowPermissions.ActOnTask,
            WorkflowPermissions.Cancel,
            WorkflowPermissions.ViewAllTasks]);
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
    }
}
