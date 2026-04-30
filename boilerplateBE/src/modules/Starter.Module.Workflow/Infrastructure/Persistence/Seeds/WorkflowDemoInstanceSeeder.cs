using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Starter.Application.Common.Interfaces;
using Starter.Domain.Identity.Entities;
using Starter.Module.Workflow.Domain.Entities;
using Starter.Module.Workflow.Domain.Enums;

namespace Starter.Module.Workflow.Infrastructure.Persistence.Seeds;

/// <summary>
/// Seeds a small fixture of workflow instances + tasks across the SuperAdmin scope
/// and each demo tenant so the redesigned Phase 5a Inbox / Instance list / Detail
/// pages have realistic data on first launch. Idempotent — checked via the
/// <see cref="DemoEntityType"/> marker.
/// </summary>
internal static class WorkflowDemoInstanceSeeder
{
    private const string DemoEntityType = "WorkflowDemoSeed";
    private const string DefinitionName = "general-approval";
    private const string PendingStateName = "PendingReview";
    private const string ApprovedStateName = "Approved";
    private static readonly string AvailableActionsJson =
        JsonSerializer.Serialize(new[] { "Approve", "Reject", "ReturnForRevision" });

    public static async Task SeedAsync(IServiceProvider serviceProvider, CancellationToken ct = default)
    {
        using var scope = serviceProvider.CreateScope();
        var workflowContext = scope.ServiceProvider.GetRequiredService<WorkflowDbContext>();
        var appContext = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var logger = scope.ServiceProvider
            .GetRequiredService<ILogger<WorkflowDbContext>>();

        // Idempotency — bail out if any demo instance already seeded.
        var alreadySeeded = await workflowContext.WorkflowInstances
            .AnyAsync(i => i.EntityType == DemoEntityType, ct);
        if (alreadySeeded)
        {
            logger.LogDebug("Demo workflow instances already seeded; skipping.");
            return;
        }

        // Resolve the general-approval template definition. It's seeded with
        // TenantId=null so it can be used for any tenant scope.
        var definition = await workflowContext.WorkflowDefinitions
            .FirstOrDefaultAsync(d =>
                    d.Name == DefinitionName && d.TenantId == null && d.IsActive,
                ct);

        if (definition is null)
        {
            logger.LogWarning(
                "Demo workflow seeder: '{Name}' template not found; skipping.",
                DefinitionName);
            return;
        }

        // Resolve seeded users by stable username (rename-script-safe — emails
        // get domain-rewritten, usernames stay constant).
        var users = await appContext.Users
            .Where(u => u.Username == "superadmin"
                || u.Username == "acme.admin"
                || u.Username == "acme.alice"
                || u.Username == "globex.admin"
                || u.Username == "globex.hank"
                || u.Username == "initech.admin"
                || u.Username == "initech.milton")
            .ToListAsync(ct);

        var byUsername = users.ToDictionary(u => u.Username, u => u);

        var now = DateTime.UtcNow;

        // SuperAdmin scope (TenantId = null).
        if (byUsername.TryGetValue("superadmin", out var superadmin))
        {
            CreateScenario(workflowContext, definition, tenantId: null,
                initiatorUserId: superadmin.Id, assigneeUserId: superadmin.Id,
                titlePrefix: "Platform", now);
        }
        else
        {
            logger.LogWarning("Demo workflow seeder: superadmin user not found.");
        }

        // Per-tenant scenarios. Falls back gracefully if a demo user is missing.
        await SeedTenantScenarioAsync(workflowContext, appContext, definition, byUsername,
            tenantSlug: "acme", adminUsername: "acme.admin", peerUsername: "acme.alice",
            now, ct);
        await SeedTenantScenarioAsync(workflowContext, appContext, definition, byUsername,
            tenantSlug: "globex", adminUsername: "globex.admin", peerUsername: "globex.hank",
            now, ct);
        await SeedTenantScenarioAsync(workflowContext, appContext, definition, byUsername,
            tenantSlug: "initech", adminUsername: "initech.admin", peerUsername: "initech.milton",
            now, ct);

        await workflowContext.SaveChangesAsync(ct);
        logger.LogInformation(
            "Demo workflow seeder: seeded instances + tasks for SuperAdmin and demo tenants.");
    }

    private static async Task SeedTenantScenarioAsync(
        WorkflowDbContext context,
        IApplicationDbContext appContext,
        WorkflowDefinition definition,
        Dictionary<string, User> byUsername,
        string tenantSlug,
        string adminUsername,
        string peerUsername,
        DateTime now,
        CancellationToken ct)
    {
        if (!byUsername.TryGetValue(adminUsername, out var admin))
            return;

        var tenant = await appContext.Tenants
            .FirstOrDefaultAsync(t => t.Slug == tenantSlug, ct);
        if (tenant is null)
            return;

        // Two scenarios per tenant — one assigned to admin, one to a peer user
        // (so the admin's inbox has both their own raised + delegated-style tasks).
        CreateScenario(context, definition, tenantId: tenant.Id,
            initiatorUserId: admin.Id, assigneeUserId: admin.Id,
            titlePrefix: $"{Capitalize(tenantSlug)} Admin", now);

        if (byUsername.TryGetValue(peerUsername, out var peer))
        {
            CreateScenario(context, definition, tenantId: tenant.Id,
                initiatorUserId: peer.Id, assigneeUserId: admin.Id,
                titlePrefix: $"{Capitalize(tenantSlug)} Peer", now);
        }
    }

    private static string Capitalize(string s) =>
        string.IsNullOrEmpty(s) ? s : char.ToUpper(s[0]) + s[1..];

    /// <summary>
    /// Seeds a 5-instance scenario covering every status bucket + SLA bucket
    /// the redesigned UI surfaces:
    /// <list type="bullet">
    ///   <item>Active + Awaiting + Overdue</item>
    ///   <item>Active + Awaiting + Due today</item>
    ///   <item>Active + Awaiting + Upcoming (no SLA)</item>
    ///   <item>Completed</item>
    ///   <item>Cancelled</item>
    /// </list>
    /// </summary>
    private static void CreateScenario(
        WorkflowDbContext context,
        WorkflowDefinition definition,
        Guid? tenantId,
        Guid initiatorUserId,
        Guid assigneeUserId,
        string titlePrefix,
        DateTime now)
    {
        AddPendingInstance(context, definition, tenantId, initiatorUserId, assigneeUserId,
            displayName: $"{titlePrefix} budget approval (overdue)",
            dueDate: now.AddDays(-2));

        AddPendingInstance(context, definition, tenantId, initiatorUserId, assigneeUserId,
            displayName: $"{titlePrefix} contract review (due today)",
            dueDate: now.AddHours(4));

        AddPendingInstance(context, definition, tenantId, initiatorUserId, assigneeUserId,
            displayName: $"{titlePrefix} hire request (upcoming)",
            dueDate: null);

        AddCompletedInstance(context, definition, tenantId, initiatorUserId, assigneeUserId,
            displayName: $"{titlePrefix} expense report (approved)",
            now);

        AddCancelledInstance(context, definition, tenantId, initiatorUserId,
            displayName: $"{titlePrefix} travel request (cancelled)",
            now);
    }

    private static void AddPendingInstance(
        WorkflowDbContext context,
        WorkflowDefinition definition,
        Guid? tenantId,
        Guid initiatorUserId,
        Guid assigneeUserId,
        string displayName,
        DateTime? dueDate)
    {
        var instance = WorkflowInstance.Create(
            tenantId,
            definition.Id,
            DemoEntityType,
            entityId: Guid.NewGuid(),
            initialState: PendingStateName,
            startedByUserId: initiatorUserId,
            contextJson: null,
            definitionName: definition.DisplayName ?? definition.Name,
            entityDisplayName: displayName);

        context.WorkflowInstances.Add(instance);

        var task = ApprovalTask.Create(
            tenantId: tenantId,
            instanceId: instance.Id,
            stepName: PendingStateName,
            assigneeUserId: assigneeUserId,
            assigneeRole: null,
            assigneeStrategyJson: null,
            entityType: DemoEntityType,
            entityId: instance.EntityId,
            definitionName: definition.Name,
            availableActionsJson: AvailableActionsJson,
            dueDate: dueDate,
            definitionDisplayName: definition.DisplayName,
            entityDisplayName: displayName);

        context.ApprovalTasks.Add(task);
    }

    private static void AddCompletedInstance(
        WorkflowDbContext context,
        WorkflowDefinition definition,
        Guid? tenantId,
        Guid initiatorUserId,
        Guid assigneeUserId,
        string displayName,
        DateTime now)
    {
        var instance = WorkflowInstance.Create(
            tenantId,
            definition.Id,
            DemoEntityType,
            entityId: Guid.NewGuid(),
            initialState: ApprovedStateName,
            startedByUserId: initiatorUserId,
            contextJson: null,
            definitionName: definition.DisplayName ?? definition.Name,
            entityDisplayName: displayName);

        instance.Complete();
        context.WorkflowInstances.Add(instance);

        // History trail — one synthetic step shows the approval.
        var step = WorkflowStep.Create(
            instanceId: instance.Id,
            fromState: PendingStateName,
            toState: ApprovedStateName,
            stepType: StepType.HumanTask,
            action: "Approve",
            actorUserId: assigneeUserId,
            comment: "Seeded as completed for demo purposes.",
            metadataJson: null);

        context.WorkflowSteps.Add(step);
    }

    private static void AddCancelledInstance(
        WorkflowDbContext context,
        WorkflowDefinition definition,
        Guid? tenantId,
        Guid initiatorUserId,
        string displayName,
        DateTime now)
    {
        var instance = WorkflowInstance.Create(
            tenantId,
            definition.Id,
            DemoEntityType,
            entityId: Guid.NewGuid(),
            initialState: PendingStateName,
            startedByUserId: initiatorUserId,
            contextJson: null,
            definitionName: definition.DisplayName ?? definition.Name,
            entityDisplayName: displayName);

        instance.Cancel("Seeded as cancelled for demo purposes.", initiatorUserId);
        context.WorkflowInstances.Add(instance);
    }
}
