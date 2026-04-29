using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Starter.Abstractions.Ai;
using Starter.Application.Common.Interfaces;
using Starter.Module.AI.Application.Commands.Settings.ModelDefaults.UpsertModelDefault;
using Starter.Module.AI.Application.DTOs;
using Starter.Module.AI.Application.Queries.Settings.ModelDefaults.GetModelDefaults;
using Starter.Module.AI.Application.Services.Settings;
using Starter.Module.AI.Domain.Enums;
using Starter.Module.AI.Infrastructure.Persistence;
using Xunit;

namespace Starter.Api.Tests.Ai.Settings;

public sealed class AiModelDefaultHandlerTests
{
    [Fact]
    public async Task UpsertModelDefault_Rejects_Disallowed_Provider()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateDb(tenantId);
        var handler = new UpsertModelDefaultCommandHandler(
            db,
            CurrentUser(tenantId).Object,
            EntitlementResolver(Entitlements(allowedProviders: ["Anthropic"])).Object);

        var result = await handler.Handle(new UpsertModelDefaultCommand(
            tenantId,
            AiAgentClass.Chat,
            AiProviderType.OpenAI,
            "gpt-4o-mini",
            1000,
            0.2), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("AiSettings.ProviderNotAllowed");
    }

    [Fact]
    public async Task UpsertModelDefault_Stores_And_Updates_Tenant_Default()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateDb(tenantId);
        var handler = new UpsertModelDefaultCommandHandler(
            db,
            CurrentUser(tenantId).Object,
            EntitlementResolver(Entitlements()).Object);

        var created = await handler.Handle(new UpsertModelDefaultCommand(
            tenantId,
            AiAgentClass.Chat,
            AiProviderType.OpenAI,
            "gpt-4o-mini",
            1000,
            0.2), default);
        var updated = await handler.Handle(new UpsertModelDefaultCommand(
            tenantId,
            AiAgentClass.Chat,
            AiProviderType.Anthropic,
            "claude-haiku",
            2000,
            0.4), default);

        created.IsSuccess.Should().BeTrue();
        updated.IsSuccess.Should().BeTrue();
        var rows = await db.AiModelDefaults.IgnoreQueryFilters().ToListAsync();
        rows.Should().ContainSingle();
        rows[0].Provider.Should().Be(AiProviderType.Anthropic);
        rows[0].Model.Should().Be("claude-haiku");
    }

    [Fact]
    public async Task GetModelDefaults_Returns_Tenant_Defaults()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateDb(tenantId);
        var upsert = new UpsertModelDefaultCommandHandler(
            db,
            CurrentUser(tenantId).Object,
            EntitlementResolver(Entitlements()).Object);
        await upsert.Handle(new UpsertModelDefaultCommand(
            tenantId,
            AiAgentClass.RagHelper,
            AiProviderType.OpenAI,
            "gpt-4o-mini",
            512,
            0.1), default);
        var handler = new GetModelDefaultsQueryHandler(db, CurrentUser(tenantId).Object);

        var result = await handler.Handle(new GetModelDefaultsQuery(null), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().ContainSingle();
        result.Value[0].AgentClass.Should().Be(AiAgentClass.RagHelper);
    }

    private static AiDbContext CreateDb(Guid? tenantId)
    {
        var options = new DbContextOptionsBuilder<AiDbContext>()
            .UseInMemoryDatabase($"ai-model-default-handler-{Guid.NewGuid()}")
            .Options;
        return new AiDbContext(options, CurrentUser(tenantId).Object);
    }

    private static Mock<ICurrentUserService> CurrentUser(Guid? tenantId)
    {
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(x => x.IsAuthenticated).Returns(true);
        currentUser.SetupGet(x => x.TenantId).Returns(tenantId);
        currentUser.SetupGet(x => x.UserId).Returns(Guid.NewGuid());
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

    private static AiEntitlementsDto Entitlements(IReadOnlyList<string>? allowedProviders = null) =>
        new(20m, 2m, 10m, 1m, 60, true, true, 3, 50_000, 5_000, 30,
            AllowedProviders: allowedProviders ?? ["OpenAI", "Anthropic"],
            AllowedModels: ["gpt-4o-mini", "claude-haiku"]);
}
