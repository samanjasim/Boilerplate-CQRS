using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Moq;
using Starter.Abstractions.Ai;
using Starter.Application.Common.Interfaces;
using Starter.Module.AI.Application.DTOs;
using Starter.Module.AI.Application.Services.Settings;
using Starter.Module.AI.Domain.Entities;
using Starter.Module.AI.Domain.Enums;
using Starter.Module.AI.Infrastructure.Persistence;
using Starter.Module.AI.Infrastructure.Services.Settings;
using Xunit;

namespace Starter.Api.Tests.Ai.Settings;

public sealed class AiProviderCredentialResolverTests
{
    private const string PlatformOpenAiSecret = "sk-platform-openai";
    private const string TenantSecret = "sk-tenant-openai";
    private const string EncryptedTenantSecret = "protected-tenant-openai";

    [Fact]
    public async Task Resolve_PlatformOnly_Uses_Platform_Secret()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateDb(tenantId);
        var settings = AiTenantSettings.CreateDefault(tenantId);
        settings.UpdatePolicy(ProviderCredentialPolicy.PlatformOnly, SafetyPreset.Standard);
        db.AiTenantSettings.Add(settings);
        await db.SaveChangesAsync();
        var resolver = CreateResolver(db, Entitlements(), PlatformConfig());

        var result = await resolver.ResolveAsync(tenantId, AiProviderType.OpenAI);

        result.IsSuccess.Should().BeTrue();
        result.Value.Provider.Should().Be(AiProviderType.OpenAI);
        result.Value.Secret.Should().Be(PlatformOpenAiSecret);
        result.Value.Source.Should().Be(ProviderCredentialSource.Platform);
        result.Value.ProviderCredentialId.Should().BeNull();
    }

    [Fact]
    public async Task Resolve_TenantKeysAllowed_Uses_Tenant_Secret_When_Active()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateDb(tenantId);
        var settings = AiTenantSettings.CreateDefault(tenantId);
        settings.UpdatePolicy(ProviderCredentialPolicy.TenantKeysAllowed, SafetyPreset.Standard);
        var credential = AiProviderCredential.Create(
            tenantId,
            AiProviderType.OpenAI,
            "OpenAI primary",
            EncryptedTenantSecret,
            "sk-tenant-pr",
            createdByUserId: Guid.NewGuid());
        db.AiTenantSettings.Add(settings);
        db.AiProviderCredentials.Add(credential);
        await db.SaveChangesAsync();
        var protector = SecretProtector();
        protector.Setup(x => x.Unprotect(EncryptedTenantSecret)).Returns(TenantSecret);
        var resolver = CreateResolver(db, Entitlements(), PlatformConfig(), protector);

        var result = await resolver.ResolveAsync(tenantId, AiProviderType.OpenAI);

        result.IsSuccess.Should().BeTrue();
        result.Value.Secret.Should().Be(TenantSecret);
        result.Value.Source.Should().Be(ProviderCredentialSource.Tenant);
        result.Value.ProviderCredentialId.Should().Be(credential.Id);
    }

    [Fact]
    public async Task Resolve_TenantKeysAllowed_Falls_Back_To_Platform_Secret_When_Missing()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateDb(tenantId);
        var settings = AiTenantSettings.CreateDefault(tenantId);
        settings.UpdatePolicy(ProviderCredentialPolicy.TenantKeysAllowed, SafetyPreset.Standard);
        db.AiTenantSettings.Add(settings);
        await db.SaveChangesAsync();
        var resolver = CreateResolver(db, Entitlements(), PlatformConfig());

        var result = await resolver.ResolveAsync(tenantId, AiProviderType.OpenAI);

        result.IsSuccess.Should().BeTrue();
        result.Value.Secret.Should().Be(PlatformOpenAiSecret);
        result.Value.Source.Should().Be(ProviderCredentialSource.Platform);
        result.Value.ProviderCredentialId.Should().BeNull();
    }

    [Fact]
    public async Task Resolve_TenantKeysRequired_Fails_When_Tenant_Secret_Missing()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateDb(tenantId);
        var settings = AiTenantSettings.CreateDefault(tenantId);
        settings.UpdatePolicy(ProviderCredentialPolicy.TenantKeysRequired, SafetyPreset.Standard);
        db.AiTenantSettings.Add(settings);
        await db.SaveChangesAsync();
        var resolver = CreateResolver(db, Entitlements(), PlatformConfig());

        var result = await resolver.ResolveAsync(tenantId, AiProviderType.OpenAI);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("AiSettings.TenantKeyRequired");
    }

    [Fact]
    public async Task Resolve_Byok_Disabled_Forces_PlatformOnly()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateDb(tenantId);
        var settings = AiTenantSettings.CreateDefault(tenantId);
        settings.UpdatePolicy(ProviderCredentialPolicy.TenantKeysRequired, SafetyPreset.Standard);
        var credential = AiProviderCredential.Create(
            tenantId,
            AiProviderType.OpenAI,
            "OpenAI primary",
            EncryptedTenantSecret,
            "sk-tenant-pr",
            createdByUserId: Guid.NewGuid());
        db.AiTenantSettings.Add(settings);
        db.AiProviderCredentials.Add(credential);
        await db.SaveChangesAsync();
        var resolver = CreateResolver(db, Entitlements(byokEnabled: false), PlatformConfig());

        var result = await resolver.ResolveAsync(tenantId, AiProviderType.OpenAI);

        result.IsSuccess.Should().BeTrue();
        result.Value.Secret.Should().Be(PlatformOpenAiSecret);
        result.Value.Source.Should().Be(ProviderCredentialSource.Platform);
        result.Value.ProviderCredentialId.Should().BeNull();
    }

    [Fact]
    public async Task Resolve_Disallowed_Provider_Fails()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateDb(tenantId);
        var resolver = CreateResolver(
            db,
            Entitlements(allowedProviders: ["Anthropic"]),
            PlatformConfig());

        var result = await resolver.ResolveAsync(tenantId, AiProviderType.OpenAI);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("AiSettings.ProviderNotAllowed");
    }

    private static IAiProviderCredentialResolver CreateResolver(
        AiDbContext db,
        AiEntitlementsDto entitlements,
        IConfiguration configuration,
        Mock<IAiSecretProtector>? protector = null)
    {
        var entitlementResolver = EntitlementResolver(entitlements);
        var settingsResolver = new AiTenantSettingsResolver(db, entitlementResolver.Object);

        return new AiProviderCredentialResolver(
            db,
            settingsResolver,
            entitlementResolver.Object,
            configuration,
            (protector ?? SecretProtector()).Object);
    }

    private static AiDbContext CreateDb(Guid? tenantId)
    {
        var options = new DbContextOptionsBuilder<AiDbContext>()
            .UseInMemoryDatabase($"ai-provider-credential-resolver-{Guid.NewGuid()}")
            .Options;

        return new AiDbContext(options, CurrentUser(tenantId).Object);
    }

    private static IConfiguration PlatformConfig() =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AI:Providers:OpenAI:ApiKey"] = PlatformOpenAiSecret,
                ["AI:Providers:Anthropic:ApiKey"] = "sk-platform-anthropic"
            })
            .Build();

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

    private static Mock<IAiSecretProtector> SecretProtector()
    {
        var protector = new Mock<IAiSecretProtector>();
        protector.Setup(x => x.Unprotect(It.IsAny<string>())).Returns(TenantSecret);
        return protector;
    }

    private static AiEntitlementsDto Entitlements(
        bool byokEnabled = true,
        IReadOnlyList<string>? allowedProviders = null) =>
        new(
            TotalMonthlyUsd: 20m,
            TotalDailyUsd: 2m,
            PlatformMonthlyUsd: 10m,
            PlatformDailyUsd: 1m,
            RequestsPerMinute: 60,
            ByokEnabled: byokEnabled,
            WidgetsEnabled: true,
            WidgetMaxCount: 3,
            WidgetMonthlyTokens: 50_000,
            WidgetDailyTokens: 5_000,
            WidgetRequestsPerMinute: 30,
            AllowedProviders: allowedProviders ?? ["OpenAI", "Anthropic"],
            AllowedModels: ["gpt-4o-mini"]);
}
