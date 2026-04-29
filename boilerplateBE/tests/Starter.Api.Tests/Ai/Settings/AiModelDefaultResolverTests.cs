using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Starter.Abstractions.Ai;
using Starter.Application.Common.Interfaces;
using Starter.Module.AI.Application.DTOs;
using Starter.Module.AI.Application.Services.Settings;
using Starter.Module.AI.Domain.Entities;
using Starter.Module.AI.Domain.Enums;
using Starter.Module.AI.Infrastructure.Persistence;
using Starter.Module.AI.Infrastructure.Providers;
using Starter.Module.AI.Infrastructure.Services.Settings;
using Xunit;

namespace Starter.Api.Tests.Ai.Settings;

public sealed class AiModelDefaultResolverTests
{
    [Fact]
    public async Task Resolve_Assistant_Explicit_Model_Wins()
    {
        await using var db = CreateDb();
        var sut = CreateResolver(db);

        var result = await sut.ResolveAsync(Guid.NewGuid(), AiAgentClass.Chat, AiProviderType.OpenAI, "gpt-4o", 0.2, 1000);

        result.IsSuccess.Should().BeTrue();
        result.Value.Provider.Should().Be(AiProviderType.OpenAI);
        result.Value.Model.Should().Be("gpt-4o");
        result.Value.Temperature.Should().Be(0.2);
        result.Value.MaxTokens.Should().Be(1000);
    }

    [Fact]
    public async Task Resolve_Tenant_Class_Default_Wins_When_Assistant_Model_Missing()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateDb();
        db.AiModelDefaults.Add(AiModelDefault.Create(tenantId, AiAgentClass.Chat, AiProviderType.Anthropic, "claude-haiku", 1200, 0.4));
        await db.SaveChangesAsync();
        var sut = CreateResolver(db);

        var result = await sut.ResolveAsync(tenantId, AiAgentClass.Chat, null, null, null, null);

        result.IsSuccess.Should().BeTrue();
        result.Value.Provider.Should().Be(AiProviderType.Anthropic);
        result.Value.Model.Should().Be("claude-haiku");
        result.Value.Temperature.Should().Be(0.4);
        result.Value.MaxTokens.Should().Be(1200);
    }

    [Fact]
    public async Task Resolve_Platform_Default_Wins_When_No_Tenant_Default()
    {
        await using var db = CreateDb();
        var sut = CreateResolver(db);

        var result = await sut.ResolveAsync(Guid.NewGuid(), AiAgentClass.Chat, null, null, null, null);

        result.IsSuccess.Should().BeTrue();
        result.Value.Provider.Should().Be(AiProviderType.OpenAI);
        result.Value.Model.Should().Be("gpt-4o-mini");
    }

    [Fact]
    public async Task Resolve_Disallowed_Model_Fails()
    {
        await using var db = CreateDb();
        var sut = CreateResolver(db, Entitlements(allowedModels: ["gpt-4o-mini"]));

        var result = await sut.ResolveAsync(Guid.NewGuid(), AiAgentClass.Chat, AiProviderType.OpenAI, "gpt-4o", null, null);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("AiSettings.ModelNotAllowed");
    }

    private static IAiModelDefaultResolver CreateResolver(
        AiDbContext db,
        AiEntitlementsDto? entitlements = null)
    {
        var entitlementResolver = new Mock<IAiEntitlementResolver>();
        entitlementResolver.Setup(x => x.ResolveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(entitlements ?? Entitlements());
        entitlementResolver.Setup(x => x.ResolveAsync(It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(entitlements ?? Entitlements());

        return new AiModelDefaultResolver(db, entitlementResolver.Object, new StubProviderFactory());
    }

    private static AiDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AiDbContext>()
            .UseInMemoryDatabase($"ai-model-default-resolver-{Guid.NewGuid()}")
            .Options;
        return new AiDbContext(options, Mock.Of<ICurrentUserService>());
    }

    private static AiEntitlementsDto Entitlements(IReadOnlyList<string>? allowedModels = null) =>
        new(20m, 2m, 10m, 1m, 60, true, true, 3, 50_000, 5_000, 30,
            AllowedProviders: ["OpenAI", "Anthropic"],
            AllowedModels: allowedModels ?? ["gpt-4o-mini", "gpt-4o", "claude-haiku"]);

    private sealed class StubProviderFactory : IAiProviderFactory
    {
        public IAiProvider Create(AiProviderType providerType) => throw new NotSupportedException();
        public AiProviderType GetDefaultProviderType() => AiProviderType.OpenAI;
        public AiProviderType GetEmbeddingProviderType() => AiProviderType.OpenAI;
        public IAiProvider CreateDefault() => throw new NotSupportedException();
        public IAiProvider CreateForEmbeddings() => throw new NotSupportedException();
        public string GetEmbeddingModelId() => "OpenAI:text-embedding-3-small";
        public string GetDefaultChatModelId() => "OpenAI:gpt-4o-mini";
    }
}
