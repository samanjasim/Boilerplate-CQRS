using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Starter.Abstractions.Ai;
using Starter.Application.Common.Interfaces;
using Starter.Module.AI.Application.Commands.Settings.UpsertAiTenantSettings;
using Starter.Module.AI.Application.DTOs;
using Starter.Module.AI.Application.Queries.Settings.GetAiTenantSettings;
using Starter.Module.AI.Application.Services.Costs;
using Starter.Module.AI.Application.Services.Settings;
using Starter.Module.AI.Domain.Entities;
using Starter.Module.AI.Domain.Enums;
using Starter.Module.AI.Infrastructure.Persistence;
using Starter.Module.AI.Infrastructure.Services.Settings;
using Xunit;

namespace Starter.Api.Tests.Ai.Settings;

public sealed class AiTenantSettingsHandlerTests
{
    [Fact]
    public async Task Get_Returns_Default_PlatformOnly_When_Row_Missing()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateDb(tenantId);
        var handler = CreateGetHandler(db, tenantId, Entitlements(byokEnabled: true));

        var result = await handler.Handle(new GetAiTenantSettingsQuery(TenantId: null), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.TenantId.Should().Be(tenantId);
        result.Value.RequestedProviderCredentialPolicy.Should().Be(ProviderCredentialPolicy.PlatformOnly);
        result.Value.EffectiveProviderCredentialPolicy.Should().Be(ProviderCredentialPolicy.PlatformOnly);
        result.Value.DefaultSafetyPreset.Should().Be(SafetyPreset.Standard);
    }

    [Fact]
    public async Task Get_Byok_Disabled_Makes_EffectivePolicy_PlatformOnly()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateDb(tenantId);
        var settings = AiTenantSettings.CreateDefault(tenantId);
        settings.UpdatePolicy(ProviderCredentialPolicy.TenantKeysAllowed, SafetyPreset.ProfessionalModerated);
        db.AiTenantSettings.Add(settings);
        await db.SaveChangesAsync();
        var handler = CreateGetHandler(db, tenantId, Entitlements(byokEnabled: false));

        var result = await handler.Handle(new GetAiTenantSettingsQuery(TenantId: null), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.RequestedProviderCredentialPolicy.Should().Be(ProviderCredentialPolicy.TenantKeysAllowed);
        result.Value.EffectiveProviderCredentialPolicy.Should().Be(ProviderCredentialPolicy.PlatformOnly);
    }

    [Fact]
    public async Task Get_With_Explicit_TenantId_Uses_That_Tenant_For_Entitlements()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateDb(null);
        var settings = AiTenantSettings.CreateDefault(tenantId);
        settings.UpdatePolicy(ProviderCredentialPolicy.TenantKeysAllowed, SafetyPreset.Standard);
        db.AiTenantSettings.Add(settings);
        await db.SaveChangesAsync();

        var entitlementResolver = new Mock<IAiEntitlementResolver>();
        entitlementResolver.Setup(x => x.ResolveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Entitlements(byokEnabled: false));
        entitlementResolver.Setup(x => x.ResolveAsync(tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Entitlements(byokEnabled: true));

        var handler = new GetAiTenantSettingsQueryHandler(
            new AiTenantSettingsResolver(db, entitlementResolver.Object),
            entitlementResolver.Object,
            CurrentUser(tenantId: null).Object);

        var result = await handler.Handle(new GetAiTenantSettingsQuery(tenantId), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.RequestedProviderCredentialPolicy.Should().Be(ProviderCredentialPolicy.TenantKeysAllowed);
        result.Value.EffectiveProviderCredentialPolicy.Should().Be(ProviderCredentialPolicy.TenantKeysAllowed);
        entitlementResolver.Verify(x => x.ResolveAsync(tenantId, It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }

    [Fact]
    public async Task Upsert_Rejects_Total_Monthly_Limit_Above_Entitlement()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateDb(tenantId);
        var handler = CreateUpsertHandler(db, tenantId, Entitlements(totalMonthlyUsd: 10m));

        var result = await handler.Handle(DefaultCommand(tenantId) with
        {
            MonthlyCostCapUsd = 10.01m
        }, default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("AiSettings.SelfLimitExceedsEntitlement");
    }

    [Fact]
    public async Task Upsert_Stores_Brand_Profile_And_Public_Defaults()
    {
        var tenantId = Guid.NewGuid();
        var avatarFileId = Guid.NewGuid();
        await using var db = CreateDb(tenantId);
        var handler = CreateUpsertHandler(db, tenantId, Entitlements());

        var result = await handler.Handle(DefaultCommand(tenantId) with
        {
            PublicMonthlyTokenCap = 1_000,
            PublicDailyTokenCap = 100,
            PublicRequestsPerMinute = 20,
            AssistantDisplayName = "  Support Copilot  ",
            Tone = "  concise  ",
            AvatarFileId = avatarFileId,
            BrandInstructions = "  Stay on brand.  "
        }, default);

        result.IsSuccess.Should().BeTrue();
        var row = await db.AiTenantSettings.IgnoreQueryFilters().SingleAsync();
        row.PublicMonthlyTokenCap.Should().Be(1_000);
        row.PublicDailyTokenCap.Should().Be(100);
        row.PublicRequestsPerMinute.Should().Be(20);
        row.AssistantDisplayName.Should().Be("Support Copilot");
        row.Tone.Should().Be("concise");
        row.AvatarFileId.Should().Be(avatarFileId);
        row.BrandInstructions.Should().Be("Stay on brand.");
    }

    [Fact]
    public async Task Upsert_Invalidates_CostCap_Cache_For_Tenant()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateDb(tenantId);
        var costCaps = new Mock<ICostCapResolver>();
        var handler = CreateUpsertHandler(db, tenantId, Entitlements(), costCaps);

        var result = await handler.Handle(DefaultCommand(tenantId) with
        {
            MonthlyCostCapUsd = 5m
        }, default);

        result.IsSuccess.Should().BeTrue();
        costCaps.Verify(x => x.InvalidateTenantAsync(tenantId, It.IsAny<CancellationToken>()), Times.Once);
    }

    private static GetAiTenantSettingsQueryHandler CreateGetHandler(
        AiDbContext db,
        Guid tenantId,
        AiEntitlementsDto entitlements)
    {
        var entitlementResolver = EntitlementResolver(entitlements);
        var settingsResolver = new AiTenantSettingsResolver(db, entitlementResolver.Object);

        return new GetAiTenantSettingsQueryHandler(
            settingsResolver,
            entitlementResolver.Object,
            CurrentUser(tenantId).Object);
    }

    private static UpsertAiTenantSettingsCommandHandler CreateUpsertHandler(
        AiDbContext db,
        Guid tenantId,
        AiEntitlementsDto entitlements,
        Mock<ICostCapResolver>? costCaps = null)
    {
        var entitlementResolver = EntitlementResolver(entitlements);

        return new UpsertAiTenantSettingsCommandHandler(
            db,
            CurrentUser(tenantId).Object,
            entitlementResolver.Object,
            costCaps?.Object ?? Mock.Of<ICostCapResolver>());
    }

    private static AiDbContext CreateDb(Guid? tenantId)
    {
        var options = new DbContextOptionsBuilder<AiDbContext>()
            .UseInMemoryDatabase($"tenant-settings-{Guid.NewGuid()}")
            .Options;

        return new AiDbContext(options, CurrentUser(tenantId).Object);
    }

    private static Mock<ICurrentUserService> CurrentUser(Guid? tenantId)
    {
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(x => x.IsAuthenticated).Returns(true);
        currentUser.SetupGet(x => x.TenantId).Returns(tenantId);
        currentUser.Setup(x => x.HasPermission(It.IsAny<string>())).Returns(true);
        return currentUser;
    }

    private static Mock<IAiEntitlementResolver> EntitlementResolver(AiEntitlementsDto entitlements)
    {
        var resolver = new Mock<IAiEntitlementResolver>();
        resolver.Setup(x => x.ResolveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(entitlements);
        resolver.Setup(x => x.ResolveAsync(It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(entitlements);
        return resolver;
    }

    private static AiEntitlementsDto Entitlements(
        decimal totalMonthlyUsd = 20m,
        decimal totalDailyUsd = 2m,
        decimal platformMonthlyUsd = 10m,
        decimal platformDailyUsd = 1m,
        int requestsPerMinute = 60,
        bool byokEnabled = true,
        bool widgetsEnabled = true,
        int widgetMonthlyTokens = 50_000,
        int widgetDailyTokens = 5_000,
        int widgetRequestsPerMinute = 30) =>
        new(
            totalMonthlyUsd,
            totalDailyUsd,
            platformMonthlyUsd,
            platformDailyUsd,
            requestsPerMinute,
            byokEnabled,
            widgetsEnabled,
            WidgetMaxCount: 3,
            widgetMonthlyTokens,
            widgetDailyTokens,
            widgetRequestsPerMinute,
            AllowedProviders: ["OpenAI", "Anthropic"],
            AllowedModels: ["gpt-4o-mini"]);

    private static UpsertAiTenantSettingsCommand DefaultCommand(Guid? tenantId) =>
        new(
            TenantId: tenantId,
            RequestedProviderCredentialPolicy: ProviderCredentialPolicy.PlatformOnly,
            DefaultSafetyPreset: SafetyPreset.Standard,
            MonthlyCostCapUsd: null,
            DailyCostCapUsd: null,
            PlatformMonthlyCostCapUsd: null,
            PlatformDailyCostCapUsd: null,
            RequestsPerMinute: null,
            PublicMonthlyTokenCap: null,
            PublicDailyTokenCap: null,
            PublicRequestsPerMinute: null,
            AssistantDisplayName: null,
            Tone: null,
            AvatarFileId: null,
            BrandInstructions: null);
}
