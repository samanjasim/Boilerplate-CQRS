using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Starter.Application.Common.Interfaces;
using Starter.Module.AI.Domain.Entities;
using Starter.Module.AI.Infrastructure.Persistence;
using Xunit;
using Moq;

namespace Starter.Api.Tests.Ai.Identity;

public sealed class AiRoleMetadataTests
{
    [Fact]
    public async Task Round_Trip_Persists_Flag()
    {
        var cu = new Mock<ICurrentUserService>();
        var opts = new DbContextOptionsBuilder<AiDbContext>()
            .UseInMemoryDatabase($"meta-{Guid.NewGuid()}").Options;
        await using var db = new AiDbContext(opts, cu.Object);

        var roleId = Guid.NewGuid();
        db.AiRoleMetadataEntries.Add(AiRoleMetadata.Create(roleId, isAgentAssignable: false));
        await db.SaveChangesAsync();

        var found = await db.AiRoleMetadataEntries.FirstAsync(m => m.RoleId == roleId);
        found.IsAgentAssignable.Should().BeFalse();
    }

    [Fact]
    public async Task SetAgentAssignable_Updates_Persisted_Flag()
    {
        var cu = new Mock<ICurrentUserService>();
        var opts = new DbContextOptionsBuilder<AiDbContext>()
            .UseInMemoryDatabase($"meta-{Guid.NewGuid()}").Options;
        await using var db = new AiDbContext(opts, cu.Object);

        var roleId = Guid.NewGuid();
        var entry = AiRoleMetadata.Create(roleId, isAgentAssignable: true);
        db.AiRoleMetadataEntries.Add(entry);
        await db.SaveChangesAsync();

        entry.SetAgentAssignable(false);
        await db.SaveChangesAsync();

        db.ChangeTracker.Clear();
        var found = await db.AiRoleMetadataEntries.FirstAsync(m => m.RoleId == roleId);
        found.IsAgentAssignable.Should().BeFalse();
    }
}
