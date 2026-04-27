using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Starter.Application.Common.Interfaces;
using Starter.Domain.Identity.Entities;
using Starter.Infrastructure.Persistence;
using Starter.Module.AI.Domain.Entities;
using Starter.Module.AI.Infrastructure.Identity;
using Starter.Module.AI.Infrastructure.Persistence;
using Xunit;

namespace Starter.Api.Tests.Ai.Identity;

public sealed class AgentPermissionResolverTests
{
    private static (ApplicationDbContext appDb, AiDbContext aiDb) NewSetup()
    {
        var dbName = $"perm-{Guid.NewGuid():N}";
        var cu = new Mock<ICurrentUserService>();

        var appOpts = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"{dbName}-app").Options;
        var appDb = new ApplicationDbContext(appOpts, cu.Object);

        var aiOpts = new DbContextOptionsBuilder<AiDbContext>()
            .UseInMemoryDatabase($"{dbName}-ai").Options;
        var aiDb = new AiDbContext(aiOpts, cu.Object);

        return (appDb, aiDb);
    }

    private static (Role role, Permission[] perms) SeedRoleWithPermissions(
        ApplicationDbContext appDb, string roleName, string[] permissionNames)
    {
        var role = Role.Create(roleName, $"{roleName} role", isSystemRole: false, tenantId: null);
        appDb.Roles.Add(role);

        var perms = permissionNames.Select(p => Permission.Create(p, p, "Test")).ToArray();
        foreach (var perm in perms)
        {
            appDb.Permissions.Add(perm);
            appDb.RolePermissions.Add(new RolePermission(role.Id, perm.Id));
        }
        return (role, perms);
    }

    [Fact]
    public async Task Returns_Empty_When_No_Roles_Assigned()
    {
        var (appDb, aiDb) = NewSetup();
        var sut = new AgentPermissionResolver(appDb, aiDb);
        var result = await sut.GetPermissionsAsync(Guid.NewGuid());
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task Returns_Single_Role_Permissions()
    {
        var (appDb, aiDb) = NewSetup();
        var (editorRole, _) = SeedRoleWithPermissions(appDb, "Editor", new[] { "Files.Read", "Files.Update" });
        await appDb.SaveChangesAsync();

        var principalId = Guid.NewGuid();
        aiDb.AiAgentRoles.Add(AiAgentRole.Create(principalId, editorRole.Id, Guid.NewGuid()));
        await aiDb.SaveChangesAsync();

        var sut = new AgentPermissionResolver(appDb, aiDb);
        var perms = await sut.GetPermissionsAsync(principalId);
        perms.Should().BeEquivalentTo(new[] { "Files.Read", "Files.Update" });
    }

    [Fact]
    public async Task Returns_Union_Of_Multiple_Role_Permissions_Without_Duplicates()
    {
        var (appDb, aiDb) = NewSetup();
        var (editor, _) = SeedRoleWithPermissions(appDb, "Editor", new[] { "Files.Read", "Files.Update" });
        var (viewer, _) = SeedRoleWithPermissions(appDb, "Viewer", new[] { "Files.Read" });
        await appDb.SaveChangesAsync();

        var principalId = Guid.NewGuid();
        aiDb.AiAgentRoles.AddRange(
            AiAgentRole.Create(principalId, editor.Id, Guid.NewGuid()),
            AiAgentRole.Create(principalId, viewer.Id, Guid.NewGuid()));
        await aiDb.SaveChangesAsync();

        var sut = new AgentPermissionResolver(appDb, aiDb);
        var perms = await sut.GetPermissionsAsync(principalId);

        perms.Should().BeEquivalentTo(new[] { "Files.Read", "Files.Update" });
        perms.Should().HaveCount(2, "Files.Read appears in both roles but should be deduplicated");
    }

    [Fact]
    public async Task Permission_Lookup_Is_Case_Insensitive()
    {
        var (appDb, aiDb) = NewSetup();
        var (role, _) = SeedRoleWithPermissions(appDb, "Editor", new[] { "Files.Read" });
        await appDb.SaveChangesAsync();

        var principalId = Guid.NewGuid();
        aiDb.AiAgentRoles.Add(AiAgentRole.Create(principalId, role.Id, Guid.NewGuid()));
        await aiDb.SaveChangesAsync();

        var sut = new AgentPermissionResolver(appDb, aiDb);
        var perms = await sut.GetPermissionsAsync(principalId);

        perms.Contains("files.read").Should().BeTrue("HashSet should be case-insensitive");
        perms.Contains("FILES.READ").Should().BeTrue();
    }

    [Fact]
    public async Task Other_Principals_Roles_Are_Not_Included()
    {
        var (appDb, aiDb) = NewSetup();
        var (editor, _) = SeedRoleWithPermissions(appDb, "Editor", new[] { "Files.Update" });
        var (viewer, _) = SeedRoleWithPermissions(appDb, "Viewer", new[] { "Files.Read" });
        await appDb.SaveChangesAsync();

        var alice = Guid.NewGuid();
        var bob = Guid.NewGuid();
        aiDb.AiAgentRoles.AddRange(
            AiAgentRole.Create(alice, editor.Id, Guid.NewGuid()),
            AiAgentRole.Create(bob, viewer.Id, Guid.NewGuid()));
        await aiDb.SaveChangesAsync();

        var sut = new AgentPermissionResolver(appDb, aiDb);
        var alicePerms = await sut.GetPermissionsAsync(alice);

        alicePerms.Should().BeEquivalentTo(new[] { "Files.Update" });
        alicePerms.Should().NotContain("Files.Read", "Bob's permissions must not leak to Alice");
    }
}
