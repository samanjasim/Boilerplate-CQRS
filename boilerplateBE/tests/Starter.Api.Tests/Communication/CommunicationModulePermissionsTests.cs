using FluentAssertions;
using Starter.Module.Communication;
using Starter.Module.Communication.Constants;
using Xunit;

namespace Starter.Api.Tests.Communication;

/// <summary>
/// Invariant tests for the Communication module's permission contract. These are
/// intentionally narrow — they pin the exposed surface so accidental removals or
/// renames during maintenance fail fast. Role mapping changes MUST update these
/// tests consciously.
/// </summary>
public sealed class CommunicationModulePermissionsTests
{
    private readonly CommunicationModule _module = new();

    [Fact]
    public void GetPermissions_ReturnsAllEightRegisteredPermissions()
    {
        var names = _module.GetPermissions().Select(p => p.Name).ToHashSet();

        names.Should().BeEquivalentTo(new[]
        {
            CommunicationPermissions.View,
            CommunicationPermissions.ManageChannels,
            CommunicationPermissions.ManageIntegrations,
            CommunicationPermissions.ManageTemplates,
            CommunicationPermissions.ManageTriggerRules,
            CommunicationPermissions.ViewDeliveryLog,
            CommunicationPermissions.Resend,
            CommunicationPermissions.ManageQuotas,
        });
    }

    [Fact]
    public void GetPermissions_AllCategorizedUnderCommunicationModule()
    {
        _module.GetPermissions().Should().OnlyContain(p => p.Module == "Communication");
    }

    [Fact]
    public void DefaultRolePermissions_SuperAdmin_GetsAllEight()
    {
        var superAdmin = _module.GetDefaultRolePermissions().Single(r => r.Role == "SuperAdmin");

        superAdmin.Permissions.Should().HaveCount(8);
        superAdmin.Permissions.Should().Contain(CommunicationPermissions.ManageQuotas);
    }

    [Fact]
    public void DefaultRolePermissions_Admin_GetsAllExceptManageQuotas()
    {
        // Quota management is a platform-admin concern; tenant admins should not
        // be able to override their own quotas.
        var admin = _module.GetDefaultRolePermissions().Single(r => r.Role == "Admin");

        admin.Permissions.Should().NotContain(CommunicationPermissions.ManageQuotas);
        admin.Permissions.Should().HaveCount(7);
    }

    [Fact]
    public void DefaultRolePermissions_User_GetsOnlyViewAndViewDeliveryLog()
    {
        var user = _module.GetDefaultRolePermissions().Single(r => r.Role == "User");

        user.Permissions.Should().BeEquivalentTo(new[]
        {
            CommunicationPermissions.View,
            CommunicationPermissions.ViewDeliveryLog,
        });
    }

    [Fact]
    public void Module_DeclaresNoHardDependencies()
    {
        // Cross-module coupling must go through capability contracts + events.
        // A non-empty Dependencies list would break composability.
        _module.Dependencies.Should().BeEmpty();
    }
}
