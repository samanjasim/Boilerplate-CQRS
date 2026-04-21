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
    public void GetPermissions_ReturnsAllSixPermissions()
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
        });
    }

    [Fact]
    public void GetPermissions_AllCategorizedUnderWorkflowModule()
    {
        _module.GetPermissions().Should().OnlyContain(p => p.Module == "Workflow");
    }

    [Fact]
    public void DefaultRolePermissions_SuperAdmin_GetsAllSix()
    {
        var superAdmin = _module.GetDefaultRolePermissions().Single(r => r.Role == "SuperAdmin");

        superAdmin.Permissions.Should().HaveCount(6);
        superAdmin.Permissions.Should().Contain(WorkflowPermissions.ManageDefinitions);
        superAdmin.Permissions.Should().Contain(WorkflowPermissions.Cancel);
        superAdmin.Permissions.Should().Contain(WorkflowPermissions.ViewAllTasks);
    }

    [Fact]
    public void DefaultRolePermissions_Admin_GetsAllSix()
    {
        var admin = _module.GetDefaultRolePermissions().Single(r => r.Role == "Admin");

        admin.Permissions.Should().HaveCount(6);
        admin.Permissions.Should().Contain(WorkflowPermissions.ManageDefinitions);
        admin.Permissions.Should().Contain(WorkflowPermissions.Cancel);
        admin.Permissions.Should().Contain(WorkflowPermissions.ViewAllTasks);
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
        // Cross-module coupling must go through capability contracts + events.
        // A non-empty Dependencies list would break composability.
        _module.Dependencies.Should().BeEmpty();
    }
}
