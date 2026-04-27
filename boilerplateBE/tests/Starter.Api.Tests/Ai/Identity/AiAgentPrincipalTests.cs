using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Starter.Application.Common.Interfaces;
using Starter.Module.AI.Domain.Entities;
using Starter.Module.AI.Infrastructure.Persistence;
using Xunit;
using Moq;

namespace Starter.Api.Tests.Ai.Identity;

public sealed class AiAgentPrincipalTests
{
    [Fact]
    public async Task Create_Persists_With_Defaults()
    {
        var tenant = Guid.NewGuid();
        var assistantId = Guid.NewGuid();
        var cu = new Mock<ICurrentUserService>();
        cu.SetupGet(x => x.TenantId).Returns(tenant);
        var opts = new DbContextOptionsBuilder<AiDbContext>()
            .UseInMemoryDatabase($"princ-{Guid.NewGuid()}").Options;
        await using var db = new AiDbContext(opts, cu.Object);

        var p = AiAgentPrincipal.Create(assistantId, tenant, isActive: true);
        db.AiAgentPrincipals.Add(p);
        await db.SaveChangesAsync();

        var found = await db.AiAgentPrincipals.FirstAsync();
        found.AiAssistantId.Should().Be(assistantId);
        found.TenantId.Should().Be(tenant);
        found.IsActive.Should().BeTrue();
        found.RevokedAt.Should().BeNull();
    }

    [Fact]
    public void Revoke_Sets_RevokedAt_And_Deactivates()
    {
        var p = AiAgentPrincipal.Create(Guid.NewGuid(), Guid.NewGuid(), isActive: true);
        p.Revoke();
        p.IsActive.Should().BeFalse();
        p.RevokedAt.Should().NotBeNull();
    }
}
