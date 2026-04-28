using FluentAssertions;
using Starter.Module.Workflow;
using Starter.Module.Workflow.Constants;
using Xunit;

namespace Starter.Api.Tests.Workflow;

/// <summary>
/// Invariant tests for the Workflow module's permission contract. These are
/// intentionally narrow — they pin the exposed surface so accidental removals or
/// renames during maintenance fail fast. Role mapping changes MUST update these
/// tests consciously.
/// </summary>
public sealed class WorkflowModulePermissionsTests
{
    private readonly WorkflowModule _module = new();

    [Fact]
    public void GetPermissions_ReturnsAllSevenPermissions()
    {
        var names = _module.GetPermissions().Select(p => p.Name).ToHashSet();

        names.Should().BeEquivalentTo(new[]
        {
            WorkflowPermissions.View,
            WorkflowPermissions.ManageDefinitions,
            WorkflowPermissions.Start,
            WorkflowPermissions.ActOnTask,
            WorkflowPermissions.Cancel,
            WorkflowPermissions.ViewAllTasks,
            WorkflowPermissions.ViewAnalytics,
        });
    }

    [Fact]
    public void GetPermissions_AllCategorizedUnderWorkflowModule()
    {
        _module.GetPermissions().Should().OnlyContain(p => p.Module == "Workflow");
    }

    [Fact]
    public void DefaultRolePermissions_SuperAdmin_GetsAllSeven()
    {
        var superAdmin = _module.GetDefaultRolePermissions().Single(r => r.Role == "SuperAdmin");

        superAdmin.Permissions.Should().HaveCount(7);
        superAdmin.Permissions.Should().Contain(WorkflowPermissions.ManageDefinitions);
        superAdmin.Permissions.Should().Contain(WorkflowPermissions.Cancel);
        superAdmin.Permissions.Should().Contain(WorkflowPermissions.ViewAllTasks);
        superAdmin.Permissions.Should().Contain(WorkflowPermissions.ViewAnalytics);
    }

    [Fact]
    public void DefaultRolePermissions_Admin_GetsAllSeven()
    {
        var admin = _module.GetDefaultRolePermissions().Single(r => r.Role == "Admin");

        admin.Permissions.Should().HaveCount(7);
        admin.Permissions.Should().Contain(WorkflowPermissions.ManageDefinitions);
        admin.Permissions.Should().Contain(WorkflowPermissions.Cancel);
        admin.Permissions.Should().Contain(WorkflowPermissions.ViewAllTasks);
        admin.Permissions.Should().Contain(WorkflowPermissions.ViewAnalytics);
    }

    [Fact]
    public void DefaultRolePermissions_User_GetsViewStartActOnTask()
    {
        // Regular users can participate in workflows but cannot manage definitions,
        // cancel instances, or see all tasks across users.
        var user = _module.GetDefaultRolePermissions().Single(r => r.Role == "User");

        user.Permissions.Should().BeEquivalentTo(new[]
        {
            WorkflowPermissions.View,
            WorkflowPermissions.Start,
            WorkflowPermissions.ActOnTask,
        });

        user.Permissions.Should().NotContain(WorkflowPermissions.ManageDefinitions);
        user.Permissions.Should().NotContain(WorkflowPermissions.Cancel);
        user.Permissions.Should().NotContain(WorkflowPermissions.ViewAllTasks);
    }

    [Fact]
    public void Module_DeclaresNoHardDependencies()
    {
        // Workflow couples to CommentsActivity and Communication through capability
        // contracts (ICommentableEntityRegistry, ITemplateRegistrar) that have
        // null-fallback registrations. Soft coupling lets a deployment ship Workflow
        // without those modules and degrade gracefully — declaring them as hard
        // IModule.Dependencies would defeat the Null Object pattern by forcing
        // ModuleLoader.ResolveOrder to fail startup. Composition-time guidance
        // (don't ship a half-baked Workflow) is surfaced via the catalog
        // dependencies array consumed by rename.ps1, not at runtime. See spec §14 D6.
        _module.Dependencies.Should().BeEmpty();
    }
}
