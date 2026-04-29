using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Starter.Application.Common.Interfaces;
using Starter.Domain.ApiKeys.Entities;
using Starter.Infrastructure.Persistence;
using Starter.Module.AI.Application.Commands.Settings.Widgets.CreatePublicWidget;
using Starter.Module.AI.Application.Commands.Settings.Widgets.CreateWidgetCredential;
using Starter.Module.AI.Application.Commands.Settings.Widgets.RevokeWidgetCredential;
using Starter.Module.AI.Application.Commands.Settings.Widgets.RotateWidgetCredential;
using Starter.Module.AI.Application.DTOs;
using Starter.Module.AI.Application.Services.Settings;
using Starter.Module.AI.Domain.Entities;
using Starter.Module.AI.Domain.Enums;
using Starter.Module.AI.Infrastructure.Persistence;
using Xunit;

namespace Starter.Api.Tests.Ai.Settings;

public sealed class AiPublicWidgetHandlerTests
{
    [Fact]
    public async Task CreateWidget_Fails_When_Widgets_Disabled_By_Plan()
    {
        var tenantId = Guid.NewGuid();
        await using var aiDb = CreateAiDb(tenantId);
        var handler = CreateWidgetHandler(aiDb, tenantId, Entitlements(widgetsEnabled: false));

        var result = await handler.Handle(CreateWidgetCommand(tenantId), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("AiSettings.WidgetDisabledByPlan");
        (await aiDb.AiPublicWidgets.IgnoreQueryFilters().CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task CreateWidget_Fails_When_Widget_Count_Exceeds_Entitlement()
    {
        var tenantId = Guid.NewGuid();
        await using var aiDb = CreateAiDb(tenantId);
        aiDb.AiPublicWidgets.Add(Widget(tenantId, "Existing widget"));
        await aiDb.SaveChangesAsync();
        var handler = CreateWidgetHandler(aiDb, tenantId, Entitlements(widgetMaxCount: 1));

        var result = await handler.Handle(CreateWidgetCommand(tenantId, name: "Second widget"), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("AiSettings.WidgetLimitExceeded");
        (await aiDb.AiPublicWidgets.IgnoreQueryFilters().CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task CreateWidget_Fails_When_Quota_Exceeds_Entitlement()
    {
        var tenantId = Guid.NewGuid();
        await using var aiDb = CreateAiDb(tenantId);
        var handler = CreateWidgetHandler(aiDb, tenantId, Entitlements(widgetMonthlyTokens: 10_000));

        var result = await handler.Handle(CreateWidgetCommand(tenantId, monthlyTokenCap: 10_001), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("AiSettings.WidgetQuotaExceedsEntitlement");
        (await aiDb.AiPublicWidgets.IgnoreQueryFilters().CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task CreateWidget_Stores_Normalized_Origins()
    {
        var tenantId = Guid.NewGuid();
        await using var aiDb = CreateAiDb(tenantId);
        var handler = CreateWidgetHandler(aiDb, tenantId, Entitlements());

        var result = await handler.Handle(CreateWidgetCommand(
            tenantId,
            origins:
            [
                " https://Example.com/ ",
                "https://example.com:443",
                "http://LOCALHOST:3000/"
            ]), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.AllowedOrigins.Should().Equal("http://localhost:3000", "https://example.com");

        var row = await aiDb.AiPublicWidgets.IgnoreQueryFilters().SingleAsync();
        row.AllowedOrigins.Should().Equal("http://localhost:3000", "https://example.com");
    }

    [Fact]
    public async Task CreateCredential_Returns_Full_Key_Once_And_Stores_Hash()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        await using var aiDb = CreateAiDb(tenantId, userId);
        var widget = Widget(tenantId);
        aiDb.AiPublicWidgets.Add(widget);
        await aiDb.SaveChangesAsync();
        var handler = new CreateWidgetCredentialCommandHandler(aiDb, CurrentUser(tenantId, userId).Object);

        var result = await handler.Handle(new CreateWidgetCredentialCommand(widget.Id, ExpiresAt: null), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.FullKey.Should().StartWith("pk_ai_");
        result.Value.Credential.MaskedKey.Should().Be($"{result.Value.Credential.KeyPrefix}****");

        var row = await aiDb.AiWidgetCredentials.IgnoreQueryFilters().SingleAsync();
        row.KeyPrefix.Should().Be(result.Value.Credential.KeyPrefix);
        row.KeyHash.Should().NotBe(result.Value.FullKey);
        row.KeyHash.Should().NotContain(result.Value.FullKey);
        BCrypt.Net.BCrypt.Verify(result.Value.FullKey, row.KeyHash).Should().BeTrue();
    }

    [Fact]
    public async Task RotateCredential_Creates_New_Active_Credential()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        await using var aiDb = CreateAiDb(tenantId, userId);
        var widget = Widget(tenantId);
        var old = AiWidgetCredential.Create(
            tenantId,
            widget.Id,
            "pk_ai_oldkey12",
            BCrypt.Net.BCrypt.HashPassword("pk_ai_old-secret"),
            expiresAt: null,
            userId);
        aiDb.AiPublicWidgets.Add(widget);
        aiDb.AiWidgetCredentials.Add(old);
        await aiDb.SaveChangesAsync();
        var handler = new RotateWidgetCredentialCommandHandler(aiDb, CurrentUser(tenantId, userId).Object);

        var result = await handler.Handle(new RotateWidgetCredentialCommand(widget.Id, old.Id, ExpiresAt: null), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.FullKey.Should().StartWith("pk_ai_");
        var rows = await aiDb.AiWidgetCredentials.IgnoreQueryFilters().OrderBy(c => c.CreatedAt).ToListAsync();
        rows.Should().HaveCount(2);
        rows[0].Status.Should().Be(AiWidgetCredentialStatus.Revoked);
        rows[1].Status.Should().Be(AiWidgetCredentialStatus.Active);
        BCrypt.Net.BCrypt.Verify(result.Value.FullKey, rows[1].KeyHash).Should().BeTrue();
    }

    [Fact]
    public async Task RevokeCredential_Marks_Credential_Revoked()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        await using var aiDb = CreateAiDb(tenantId, userId);
        var widget = Widget(tenantId);
        var credential = AiWidgetCredential.Create(
            tenantId,
            widget.Id,
            "pk_ai_revoke1",
            BCrypt.Net.BCrypt.HashPassword("pk_ai_revoke-secret"),
            expiresAt: null,
            userId);
        aiDb.AiPublicWidgets.Add(widget);
        aiDb.AiWidgetCredentials.Add(credential);
        await aiDb.SaveChangesAsync();
        var handler = new RevokeWidgetCredentialCommandHandler(aiDb, CurrentUser(tenantId, userId).Object);

        var result = await handler.Handle(new RevokeWidgetCredentialCommand(widget.Id, credential.Id), default);

        result.IsSuccess.Should().BeTrue();
        var row = await aiDb.AiWidgetCredentials.IgnoreQueryFilters().SingleAsync();
        row.Status.Should().Be(AiWidgetCredentialStatus.Revoked);
    }

    [Fact]
    public async Task WidgetCredential_Does_Not_Create_Core_ApiKey_Row()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        await using var aiDb = CreateAiDb(tenantId, userId);
        await using var appDb = CreateAppDb(tenantId, userId);
        var widget = Widget(tenantId);
        aiDb.AiPublicWidgets.Add(widget);
        await aiDb.SaveChangesAsync();
        var handler = new CreateWidgetCredentialCommandHandler(aiDb, CurrentUser(tenantId, userId).Object);

        var result = await handler.Handle(new CreateWidgetCredentialCommand(widget.Id, ExpiresAt: null), default);

        result.IsSuccess.Should().BeTrue();
        (await aiDb.AiWidgetCredentials.IgnoreQueryFilters().CountAsync()).Should().Be(1);
        (await appDb.Set<ApiKey>().IgnoreQueryFilters().CountAsync()).Should().Be(0);
    }

    private static CreatePublicWidgetCommandHandler CreateWidgetHandler(
        AiDbContext aiDb,
        Guid tenantId,
        AiEntitlementsDto entitlements,
        Guid? userId = null) =>
        new(aiDb, CurrentUser(tenantId, userId).Object, EntitlementResolver(entitlements).Object);

    private static CreatePublicWidgetCommand CreateWidgetCommand(
        Guid tenantId,
        string name = "Public support widget",
        IReadOnlyList<string>? origins = null,
        int? monthlyTokenCap = 10_000,
        int? dailyTokenCap = 1_000,
        int? requestsPerMinute = 20) =>
        new(
            tenantId,
            name,
            origins ?? ["https://example.com"],
            DefaultAssistantId: null,
            DefaultPersonaSlug: AiPersona.AnonymousSlug,
            monthlyTokenCap,
            dailyTokenCap,
            requestsPerMinute,
            MetadataJson: null);

    private static AiPublicWidget Widget(Guid tenantId, string name = "Public support widget") =>
        AiPublicWidget.Create(
            tenantId,
            name,
            ["https://example.com"],
            defaultAssistantId: null,
            defaultPersonaSlug: AiPersona.AnonymousSlug,
            monthlyTokenCap: 10_000,
            dailyTokenCap: 1_000,
            requestsPerMinute: 20,
            createdByUserId: Guid.NewGuid());

    private static AiDbContext CreateAiDb(Guid? tenantId, Guid? userId = null)
    {
        var options = new DbContextOptionsBuilder<AiDbContext>()
            .UseInMemoryDatabase($"ai-public-widgets-{Guid.NewGuid()}")
            .Options;

        return new AiDbContext(options, CurrentUser(tenantId, userId).Object);
    }

    private static ApplicationDbContext CreateAppDb(Guid? tenantId, Guid? userId = null)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"ai-public-widget-core-{Guid.NewGuid()}")
            .Options;

        return new ApplicationDbContext(options, CurrentUser(tenantId, userId).Object);
    }

    private static Mock<ICurrentUserService> CurrentUser(Guid? tenantId, Guid? userId = null)
    {
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(x => x.IsAuthenticated).Returns(true);
        currentUser.SetupGet(x => x.TenantId).Returns(tenantId);
        currentUser.SetupGet(x => x.UserId).Returns(userId ?? Guid.NewGuid());
        currentUser.SetupGet(x => x.Email).Returns("admin@example.test");
        currentUser.Setup(x => x.HasPermission(It.IsAny<string>())).Returns(true);
        return currentUser;
    }

    private static Mock<IAiEntitlementResolver> EntitlementResolver(AiEntitlementsDto entitlements)
    {
        var resolver = new Mock<IAiEntitlementResolver>();
        resolver.Setup(x => x.ResolveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(entitlements);
        return resolver;
    }

    private static AiEntitlementsDto Entitlements(
        bool widgetsEnabled = true,
        int widgetMaxCount = 3,
        int widgetMonthlyTokens = 50_000,
        int widgetDailyTokens = 5_000,
        int widgetRequestsPerMinute = 30) =>
        new(
            TotalMonthlyUsd: 20m,
            TotalDailyUsd: 2m,
            PlatformMonthlyUsd: 10m,
            PlatformDailyUsd: 1m,
            RequestsPerMinute: 60,
            ByokEnabled: true,
            WidgetsEnabled: widgetsEnabled,
            WidgetMaxCount: widgetMaxCount,
            WidgetMonthlyTokens: widgetMonthlyTokens,
            WidgetDailyTokens: widgetDailyTokens,
            WidgetRequestsPerMinute: widgetRequestsPerMinute,
            AllowedProviders: ["OpenAI", "Anthropic"],
            AllowedModels: ["gpt-4o-mini"]);
}
