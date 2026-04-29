using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Starter.Application.Common.Interfaces;
using Starter.Module.AI.Infrastructure.Persistence;
using Xunit;

namespace Starter.Api.Tests.Ai.Moderation;

public sealed class AiDbContextSetsTests
{
    [Fact]
    public void Three_New_DbSets_Are_Available()
    {
        var cu = new Mock<ICurrentUserService>();
        var opts = new DbContextOptionsBuilder<AiDbContext>()
            .UseInMemoryDatabase($"sets-{Guid.NewGuid()}").Options;
        using var db = new AiDbContext(opts, cu.Object);

        db.AiSafetyPresetProfiles.Should().NotBeNull();
        db.AiModerationEvents.Should().NotBeNull();
        db.AiPendingApprovals.Should().NotBeNull();
    }
}
