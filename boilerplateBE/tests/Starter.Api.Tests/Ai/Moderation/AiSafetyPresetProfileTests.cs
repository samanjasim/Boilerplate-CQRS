using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Starter.Abstractions.Ai;
using Starter.Application.Common.Interfaces;
using Starter.Module.AI.Domain.Entities;
using Starter.Module.AI.Domain.Enums;
using Starter.Module.AI.Domain.Events;
using Starter.Module.AI.Infrastructure.Persistence;
using Xunit;

namespace Starter.Api.Tests.Ai.Moderation;

public sealed class AiSafetyPresetProfileTests
{
    private static (AiDbContext db, Mock<ICurrentUserService> cu) MakeAiDb(Guid? tenant)
    {
        var cu = new Mock<ICurrentUserService>();
        cu.SetupGet(x => x.TenantId).Returns(tenant);
        var opts = new DbContextOptionsBuilder<AiDbContext>()
            .UseInMemoryDatabase($"db-{Guid.NewGuid()}").Options;
        return (new AiDbContext(opts, cu.Object), cu);
    }

    [Fact]
    public async Task Create_Round_Trips_And_Raises_Updated_Event()
    {
        var (db, _) = MakeAiDb(null);
        var entity = AiSafetyPresetProfile.Create(
            tenantId: null,
            preset: SafetyPreset.ChildSafe,
            provider: ModerationProvider.OpenAi,
            thresholdsJson: """{"sexual":0.5}""",
            blockedCategoriesJson: """["sexual-minors"]""",
            failureMode: ModerationFailureMode.FailClosed,
            redactPii: false);

        entity.DomainEvents.Should().ContainSingle(e => e is SafetyPresetProfileUpdatedEvent);

        db.AiSafetyPresetProfiles.Add(entity);
        await db.SaveChangesAsync();

        var loaded = await db.AiSafetyPresetProfiles.FirstAsync();
        loaded.Preset.Should().Be(SafetyPreset.ChildSafe);
        loaded.RedactPii.Should().BeFalse();
        loaded.FailureMode.Should().Be(ModerationFailureMode.FailClosed);
        loaded.Version.Should().Be(1);
    }

    [Fact]
    public void Update_Bumps_Version_And_Raises_Event()
    {
        var entity = AiSafetyPresetProfile.Create(
            null, SafetyPreset.Standard, ModerationProvider.OpenAi,
            "{}", "[]", ModerationFailureMode.FailOpen, false);
        entity.ClearDomainEvents();

        entity.Update("""{"sexual":0.9}""", "[]", ModerationFailureMode.FailOpen, false);

        entity.Version.Should().Be(2);
        entity.DomainEvents.Should().ContainSingle(e => e is SafetyPresetProfileUpdatedEvent);
    }
}
