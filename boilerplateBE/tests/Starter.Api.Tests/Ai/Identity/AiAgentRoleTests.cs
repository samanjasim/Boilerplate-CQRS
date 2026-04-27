using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Starter.Application.Common.Interfaces;
using Starter.Module.AI.Domain.Entities;
using Starter.Module.AI.Infrastructure.Persistence;
using Xunit;
using Moq;

namespace Starter.Api.Tests.Ai.Identity;

public sealed class AiAgentRoleTests
{
    private static AiDbContext NewDb(Guid? tenant = null)
    {
        var cu = new Mock<ICurrentUserService>();
        cu.SetupGet(x => x.TenantId).Returns(tenant);
        var opts = new DbContextOptionsBuilder<AiDbContext>()
            .UseInMemoryDatabase($"role-{Guid.NewGuid()}").Options;
        return new AiDbContext(opts, cu.Object);
    }

    [Fact]
    public async Task Round_Trip_Persists_AssignmentMetadata()
    {
        await using var db = NewDb();
        var principalId = Guid.NewGuid();
        var roleId = Guid.NewGuid();
        var assigner = Guid.NewGuid();
        var assignment = AiAgentRole.Create(principalId, roleId, assigner);
        db.AiAgentRoles.Add(assignment);
        await db.SaveChangesAsync();

        var found = await db.AiAgentRoles.FirstAsync();
        found.AgentPrincipalId.Should().Be(principalId);
        found.RoleId.Should().Be(roleId);
        found.AssignedByUserId.Should().Be(assigner);
        found.AssignedAt.Should().NotBe(default);
    }

    [Fact]
    public void Create_Sets_AssignedAt_To_UtcNow()
    {
        var before = DateTimeOffset.UtcNow.AddSeconds(-1);
        var a = AiAgentRole.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        var after = DateTimeOffset.UtcNow.AddSeconds(1);
        a.AssignedAt.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
    }
}
