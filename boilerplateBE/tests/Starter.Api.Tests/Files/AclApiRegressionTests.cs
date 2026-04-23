using System.Text.Json;
using FluentAssertions;
using Starter.Api.Tests.Access._Helpers;
using Starter.Application.Common.Access.Contracts;
using Starter.Application.Features.Access.Commands.GrantResourceAccess;
using Starter.Application.Features.Access.Commands.RevokeResourceAccess;
using Starter.Application.Features.Access.Commands.SetResourceVisibility;
using Starter.Application.Features.Access.Commands.TransferResourceOwnership;
using Starter.Domain.Common.Access;
using Starter.Domain.Common.Access.Enums;
using Starter.Domain.Common.Enums;
using Starter.Shared.Constants;
using Xunit;

namespace Starter.Api.Tests.Files;

/// <summary>
/// Security regression for ACL gates on file-related operations (spec §7.3).
/// Tests run at the command-handler level with in-memory DB + fakes.
/// </summary>
public sealed class AclApiRegressionTests
{
    // ── helpers ──────────────────────────────────────────────────────────────

    private static (
        Starter.Infrastructure.Persistence.ApplicationDbContext db,
        Starter.Infrastructure.Services.Access.ResourceAccessService svc)
        BuildAccess(FakeCurrentUser caller)
    {
        var db = TestAccessFactory.CreateDb();
        var svc = TestAccessFactory.BuildService(db, caller);
        return (db, svc);
    }

    // ── §7.3: SetResourceVisibility guards ────────────────────────────────

    [Fact]
    public async Task SetVisibility_Public_onFile_byNonAdmin_owner_fails_with_VisibilityNotAllowed()
    {
        var owner = Guid.NewGuid();
        var tenant = Guid.NewGuid();
        var fileId = Guid.NewGuid();

        // Owner who does NOT have Files.Manage permission
        var caller = FakeCurrentUser.For(owner, tenant);
        var (db, _) = BuildAccess(caller);

        var probe = new FakeResourceOwnershipProbe();
        var handler = new SetResourceVisibilityCommandHandler(probe, db, caller);

        var result = await handler.Handle(new SetResourceVisibilityCommand(
            ResourceType: ResourceTypes.File,
            ResourceId: fileId,
            Visibility: ResourceVisibility.Public), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Access.VisibilityNotAllowedForResourceType");
    }

    [Fact]
    public async Task SetVisibility_Public_onFile_byAdmin_succeeds()
    {
        var owner = Guid.NewGuid();
        var tenant = Guid.NewGuid();
        var fileId = Guid.NewGuid();

        var caller = FakeCurrentUser.For(owner, tenant, permissions: new[] { Permissions.Files.Manage });
        var (db, _) = BuildAccess(caller);

        var probe = new FakeResourceOwnershipProbe { OwnerId = owner };
        var handler = new SetResourceVisibilityCommandHandler(probe, db, caller);

        var result = await handler.Handle(new SetResourceVisibilityCommand(
            ResourceType: ResourceTypes.File,
            ResourceId: fileId,
            Visibility: ResourceVisibility.Public), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        probe.LastSetVisibility.Should().NotBeNull();
        probe.LastSetVisibility!.Visibility.Should().Be(ResourceVisibility.Public);
    }

    [Fact]
    public async Task SetVisibility_Public_onFile_byAdmin_writes_two_audit_rows()
    {
        var owner = Guid.NewGuid();
        var tenant = Guid.NewGuid();
        var fileId = Guid.NewGuid();

        var caller = FakeCurrentUser.For(owner, tenant, permissions: new[] { Permissions.Files.Manage });
        var (db, _) = BuildAccess(caller);

        var probe = new FakeResourceOwnershipProbe { OwnerId = owner };
        var handler = new SetResourceVisibilityCommandHandler(probe, db, caller);

        await handler.Handle(new SetResourceVisibilityCommand(
            ResourceType: ResourceTypes.File,
            ResourceId: fileId,
            Visibility: ResourceVisibility.Public), CancellationToken.None);

        var logs = db.AuditLogs.ToList();
        logs.Should().HaveCount(2, "visibility change + made-public = two audit rows");
        logs.Should().Contain(l => l.Changes!.Contains("ResourceVisibilityChanged"));
        logs.Should().Contain(l => l.Changes!.Contains("ResourceVisibilityMadePublic"));
    }

    [Fact]
    public async Task GrantResourceAccess_writes_audit_row_with_expected_event()
    {
        var owner = Guid.NewGuid();
        var grantee = Guid.NewGuid();
        var tenant = Guid.NewGuid();
        var fileId = Guid.NewGuid();

        var caller = FakeCurrentUser.For(owner, tenant);
        var (db, accessSvc) = BuildAccess(caller);

        var probe = new FakeResourceOwnershipProbe { OwnerId = owner };
        var notifications = new FakeNotificationService();

        var handler = new GrantResourceAccessCommandHandler(accessSvc, probe, db, notifications, caller);
        var result = await handler.Handle(new GrantResourceAccessCommand(
            ResourceType: ResourceTypes.File,
            ResourceId: fileId,
            SubjectType: GrantSubjectType.User,
            SubjectId: grantee,
            Level: AccessLevel.Viewer), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var log = db.AuditLogs.SingleOrDefault();
        log.Should().NotBeNull();
        log!.EntityType.Should().Be(AuditEntityType.ResourceGrant);
        log.Action.Should().Be(AuditAction.Created);
        log.Changes.Should().Contain("ResourceGrantCreated");
    }

    [Fact]
    public async Task RevokeResourceAccess_writes_audit_row_with_Revoked_event()
    {
        var owner = Guid.NewGuid();
        var grantee = Guid.NewGuid();
        var tenant = Guid.NewGuid();
        var fileId = Guid.NewGuid();

        var caller = FakeCurrentUser.For(owner, tenant);
        var (db, accessSvc) = BuildAccess(caller);

        // Pre-create a grant
        var grant = ResourceGrant.Create(tenant, ResourceTypes.File, fileId,
            GrantSubjectType.User, grantee, AccessLevel.Viewer, owner);
        db.ResourceGrants.Add(grant);
        await db.SaveChangesAsync();

        var probe = new FakeResourceOwnershipProbe { OwnerId = owner };
        var handler = new RevokeResourceAccessCommandHandler(db, accessSvc, probe, caller);

        var result = await handler.Handle(new RevokeResourceAccessCommand(grant.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var log = db.AuditLogs.SingleOrDefault();
        log.Should().NotBeNull();
        log!.EntityType.Should().Be(AuditEntityType.ResourceGrant);
        log.Action.Should().Be(AuditAction.Deleted);
        log.Changes.Should().Contain("ResourceGrantRevoked");
    }

    [Fact]
    public async Task GrantResourceAccess_notifies_user_grantee_with_ResourceShared_type()
    {
        var owner = Guid.NewGuid();
        var grantee = Guid.NewGuid();
        var tenant = Guid.NewGuid();
        var fileId = Guid.NewGuid();

        var caller = FakeCurrentUser.For(owner, tenant);
        var (db, accessSvc) = BuildAccess(caller);

        var probe = new FakeResourceOwnershipProbe { OwnerId = owner, DisplayName = "report.pdf" };
        var notifications = new FakeNotificationService();

        var handler = new GrantResourceAccessCommandHandler(accessSvc, probe, db, notifications, caller);
        await handler.Handle(new GrantResourceAccessCommand(
            ResourceType: ResourceTypes.File,
            ResourceId: fileId,
            SubjectType: GrantSubjectType.User,
            SubjectId: grantee,
            Level: AccessLevel.Viewer), CancellationToken.None);

        notifications.Sent.Should().ContainSingle(n =>
            n.UserId == grantee && n.Type == "ResourceShared");
    }

    [Fact]
    public async Task GrantResourceAccess_doesNotNotify_when_role_grantee()
    {
        var owner = Guid.NewGuid();
        var roleId = Guid.NewGuid();
        var tenant = Guid.NewGuid();
        var fileId = Guid.NewGuid();

        var caller = FakeCurrentUser.For(owner, tenant);
        var (db, accessSvc) = BuildAccess(caller);

        var probe = new FakeResourceOwnershipProbe { OwnerId = owner };
        var notifications = new FakeNotificationService();

        var handler = new GrantResourceAccessCommandHandler(accessSvc, probe, db, notifications, caller);
        await handler.Handle(new GrantResourceAccessCommand(
            ResourceType: ResourceTypes.File,
            ResourceId: fileId,
            SubjectType: GrantSubjectType.Role,
            SubjectId: roleId,
            Level: AccessLevel.Viewer), CancellationToken.None);

        notifications.Sent.Should().BeEmpty("role grants do not trigger user notifications");
    }
}
