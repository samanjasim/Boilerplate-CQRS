using FluentAssertions;
using FluentValidation;
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

    [Theory]
    [InlineData("not-json", "[]")]
    [InlineData("{\"sexual\":1.5}", "[]")] // out-of-range threshold
    [InlineData("{\"sexual\":-0.1}", "[]")] // negative threshold
    [InlineData("{\"sexual\":0.5}", "not-json")] // bad blocked-categories
    [InlineData("{\"sexual\":0.5}", "{\"a\":1}")] // blocked-categories must be array, not object
    public void Validator_Rejects_Invalid_Json_Payloads(string thresholds, string blocked)
    {
        var v = new UpsertSafetyPresetProfileCommandValidator();
        var cmd = new UpsertSafetyPresetProfileCommand(
            TenantId: Guid.NewGuid(),
            Preset: SafetyPreset.Standard,
            Provider: ModerationProvider.OpenAi,
            CategoryThresholdsJson: thresholds,
            BlockedCategoriesJson: blocked,
            FailureMode: ModerationFailureMode.FailOpen,
            RedactPii: false);

        var result = v.Validate(cmd);
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validator_Accepts_Well_Formed_Payloads()
    {
        var v = new UpsertSafetyPresetProfileCommandValidator();
        var cmd = new UpsertSafetyPresetProfileCommand(
            TenantId: Guid.NewGuid(),
            Preset: SafetyPreset.ChildSafe,
            Provider: ModerationProvider.OpenAi,
            CategoryThresholdsJson: """{"sexual":0.5,"violence":0.0,"hate":1.0}""",
            BlockedCategoriesJson: """["sexual-minors","self-harm"]""",
            FailureMode: ModerationFailureMode.FailClosed,
            RedactPii: false);

        var result = v.Validate(cmd);
        result.IsValid.Should().BeTrue();
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
