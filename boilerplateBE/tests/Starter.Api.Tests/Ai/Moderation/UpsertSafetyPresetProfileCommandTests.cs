using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Starter.Abstractions.Ai;
using Starter.Application.Common.Interfaces;
using Starter.Module.AI.Application.Commands.Safety.UpsertSafetyPresetProfile;
using Starter.Module.AI.Domain.Enums;
using Starter.Module.AI.Infrastructure.Persistence;
using Starter.Shared.Results;
using Xunit;

namespace Starter.Api.Tests.Ai.Moderation;

public sealed class UpsertSafetyPresetProfileCommandTests
{
    [Fact]
    public async Task Tenant_Admin_Upserts_Own_Tenant_Row()
    {
        var tenant = Guid.NewGuid();
        var cu = new Mock<ICurrentUserService>();
        cu.SetupGet(x => x.TenantId).Returns(tenant);
        cu.Setup(x => x.HasPermission(It.IsAny<string>())).Returns(true);

        var opts = new DbContextOptionsBuilder<AiDbContext>()
            .UseInMemoryDatabase($"db-{Guid.NewGuid()}").Options;
        var db = new AiDbContext(opts, cu.Object);

        var handler = new UpsertSafetyPresetProfileCommandHandler(db, cu.Object);
        var result = await handler.Handle(new UpsertSafetyPresetProfileCommand(
            TenantId: tenant, Preset: SafetyPreset.ChildSafe, Provider: ModerationProvider.OpenAi,
            CategoryThresholdsJson: """{"sexual":0.5}""",
            BlockedCategoriesJson: """["sexual-minors"]""",
            FailureMode: ModerationFailureMode.FailClosed,
            RedactPii: false), default);

        result.IsSuccess.Should().BeTrue();
        var row = await db.AiSafetyPresetProfiles.FirstAsync();
        row.TenantId.Should().Be(tenant);
        row.Preset.Should().Be(SafetyPreset.ChildSafe);
    }

    [Fact]
    public async Task Tenant_Admin_Cannot_Edit_Platform_Default()
    {
        var tenant = Guid.NewGuid();
        var cu = new Mock<ICurrentUserService>();
        cu.SetupGet(x => x.TenantId).Returns(tenant);
        cu.Setup(x => x.HasPermission(It.IsAny<string>())).Returns(true);
        var opts = new DbContextOptionsBuilder<AiDbContext>().UseInMemoryDatabase($"db-{Guid.NewGuid()}").Options;
        var db = new AiDbContext(opts, cu.Object);

        var handler = new UpsertSafetyPresetProfileCommandHandler(db, cu.Object);
        var result = await handler.Handle(new UpsertSafetyPresetProfileCommand(
            TenantId: null, Preset: SafetyPreset.Standard, Provider: ModerationProvider.OpenAi,
            CategoryThresholdsJson: "{}", BlockedCategoriesJson: "[]",
            FailureMode: ModerationFailureMode.FailOpen, RedactPii: false), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Forbidden);
    }
}
