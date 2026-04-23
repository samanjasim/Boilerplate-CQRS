using FluentAssertions;
using Starter.Api.Tests.Access._Helpers;
using Starter.Application.Common.Access.Contracts;
using Starter.Domain.Common.Access;
using Starter.Domain.Common.Access.Enums;
using Starter.Shared.Constants;
using Xunit;

namespace Starter.Api.Tests.Access;

public sealed class ResourceAccessServiceTests
{
    [Fact]
    public async Task Resolve_returns_explicit_grants_for_user()
    {
        var tenant = Guid.NewGuid();
        var user = Guid.NewGuid();
        var r1 = Guid.NewGuid();
        var r2 = Guid.NewGuid();

        await using var db = TestAccessFactory.CreateDb();
        db.ResourceGrants.AddRange(
            ResourceGrant.Create(tenant, ResourceTypes.File, r1, GrantSubjectType.User, user, AccessLevel.Viewer, Guid.NewGuid()),
            ResourceGrant.Create(tenant, ResourceTypes.File, r2, GrantSubjectType.User, user, AccessLevel.Editor, Guid.NewGuid()));
        await db.SaveChangesAsync();

        var caller = FakeCurrentUser.For(user, tenant);
        var service = TestAccessFactory.BuildService(db, caller);

        var result = await service.ResolveAccessibleResourcesAsync(caller, ResourceTypes.File, CancellationToken.None);

        result.IsAdminBypass.Should().BeFalse();
        result.ExplicitGrantedResourceIds.Should().BeEquivalentTo(new[] { r1, r2 });
    }

    [Fact]
    public async Task Resolve_admin_bypass_when_user_has_Files_Manage()
    {
        var tenant = Guid.NewGuid();
        var user = Guid.NewGuid();

        await using var db = TestAccessFactory.CreateDb();
        var caller = FakeCurrentUser.For(user, tenant, permissions: new[] { Permissions.Files.Manage });

        var service = TestAccessFactory.BuildService(db, caller);
        var result = await service.ResolveAccessibleResourcesAsync(caller, ResourceTypes.File, CancellationToken.None);

        result.IsAdminBypass.Should().BeTrue();
    }

    [Fact]
    public async Task Resolve_assistant_admin_bypass_when_user_has_Admin_role()
    {
        var tenant = Guid.NewGuid();
        var user = Guid.NewGuid();

        await using var db = TestAccessFactory.CreateDb();
        var caller = FakeCurrentUser.For(user, tenant, admin: true);

        var service = TestAccessFactory.BuildService(db, caller);
        var result = await service.ResolveAccessibleResourcesAsync(caller, ResourceTypes.AiAssistant, CancellationToken.None);

        result.IsAdminBypass.Should().BeTrue();
    }

    [Fact]
    public async Task Grant_then_Revoke_removes_the_row()
    {
        var tenant = Guid.NewGuid();
        var caller = FakeCurrentUser.For(Guid.NewGuid(), tenant);
        var resourceId = Guid.NewGuid();
        var target = Guid.NewGuid();

        await using var db = TestAccessFactory.CreateDb();
        var service = TestAccessFactory.BuildService(db, caller);

        var grantId = await service.GrantAsync(
            ResourceTypes.File, resourceId, GrantSubjectType.User, target, AccessLevel.Viewer, CancellationToken.None);

        db.ResourceGrants.Should().HaveCount(1);

        await service.RevokeAsync(grantId, CancellationToken.None);

        db.ResourceGrants.Should().BeEmpty();
    }

    [Fact]
    public async Task Grant_upserts_when_same_subject_already_granted()
    {
        var tenant = Guid.NewGuid();
        var caller = FakeCurrentUser.For(Guid.NewGuid(), tenant);
        var resourceId = Guid.NewGuid();
        var target = Guid.NewGuid();

        await using var db = TestAccessFactory.CreateDb();
        var service = TestAccessFactory.BuildService(db, caller);

        await service.GrantAsync(
            ResourceTypes.File, resourceId, GrantSubjectType.User, target, AccessLevel.Viewer, CancellationToken.None);
        await service.GrantAsync(
            ResourceTypes.File, resourceId, GrantSubjectType.User, target, AccessLevel.Manager, CancellationToken.None);

        db.ResourceGrants.Should().HaveCount(1);
        db.ResourceGrants.Single().Level.Should().Be(AccessLevel.Manager);
    }
}
