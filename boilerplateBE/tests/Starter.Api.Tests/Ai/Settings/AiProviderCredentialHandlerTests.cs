using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Starter.Abstractions.Ai;
using Starter.Application.Common.Interfaces;
using Starter.Domain.Common.Enums;
using Starter.Infrastructure.Persistence;
using Starter.Module.AI.Application.Commands.Settings.ProviderCredentials.CreateProviderCredential;
using Starter.Module.AI.Application.Commands.Settings.ProviderCredentials.RevokeProviderCredential;
using Starter.Module.AI.Application.Commands.Settings.ProviderCredentials.RotateProviderCredential;
using Starter.Module.AI.Application.Commands.Settings.ProviderCredentials.TestProviderCredential;
using Starter.Module.AI.Application.DTOs;
using Starter.Module.AI.Application.Queries.Settings.ProviderCredentials.GetProviderCredentials;
using Starter.Module.AI.Application.Services.Settings;
using Starter.Module.AI.Domain.Entities;
using Starter.Module.AI.Domain.Enums;
using Starter.Module.AI.Infrastructure.Persistence;
using Xunit;

namespace Starter.Api.Tests.Ai.Settings;

public sealed class AiProviderCredentialHandlerTests
{
    private const string PlaintextSecret = "sk-live-secret-plaintext";

    [Fact]
    public async Task CreateCredential_Fails_When_Byok_Disabled()
    {
        var tenantId = Guid.NewGuid();
        await using var aiDb = CreateAiDb(tenantId);
        await using var appDb = CreateAppDb(tenantId);
        var handler = CreateCreateHandler(aiDb, appDb, tenantId, Entitlements(byokEnabled: false));

        var result = await handler.Handle(new CreateProviderCredentialCommand(
            tenantId,
            AiProviderType.OpenAI,
            "OpenAI primary",
            PlaintextSecret), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("AiSettings.ByokDisabledByPlan");
        (await aiDb.AiProviderCredentials.IgnoreQueryFilters().CountAsync()).Should().Be(0);
        (await appDb.AuditLogs.IgnoreQueryFilters().CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task CreateCredential_Revokes_Existing_Active_For_Same_Provider()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        await using var aiDb = CreateAiDb(tenantId, userId);
        await using var appDb = CreateAppDb(tenantId, userId);
        var existing = AiProviderCredential.Create(
            tenantId,
            AiProviderType.OpenAI,
            "OpenAI old",
            "protected-old",
            "sk-old-prefix",
            userId);
        aiDb.AiProviderCredentials.Add(existing);
        await aiDb.SaveChangesAsync();
        var protector = SecretProtector("protected-new", "sk-live-new1", "sk-l****");
        var handler = CreateCreateHandler(aiDb, appDb, tenantId, Entitlements(), protector, userId);

        var result = await handler.Handle(new CreateProviderCredentialCommand(
            tenantId,
            AiProviderType.OpenAI,
            "OpenAI replacement",
            PlaintextSecret), default);

        result.IsSuccess.Should().BeTrue();
        var rows = await aiDb.AiProviderCredentials.IgnoreQueryFilters().OrderBy(c => c.CreatedAt).ToListAsync();
        rows.Should().HaveCount(2);
        rows[0].Status.Should().Be(ProviderCredentialStatus.Revoked);
        rows[1].Status.Should().Be(ProviderCredentialStatus.Active);
        rows[1].EncryptedSecret.Should().Be("protected-new");
        rows[1].KeyPrefix.Should().Be("sk-live-new1");
        result.Value.MaskedKey.Should().Be("sk-l****");
    }

    [Fact]
    public async Task ListCredentials_Returns_Masked_Metadata()
    {
        var tenantId = Guid.NewGuid();
        await using var aiDb = CreateAiDb(tenantId);
        var credential = AiProviderCredential.Create(
            tenantId,
            AiProviderType.OpenAI,
            "OpenAI primary",
            "protected-secret",
            "sk-live-list",
            Guid.NewGuid());
        aiDb.AiProviderCredentials.Add(credential);
        await aiDb.SaveChangesAsync();
        var handler = new GetProviderCredentialsQueryHandler(
            aiDb,
            CurrentUser(tenantId).Object,
            SecretProtector(mask: "sk-l****").Object);

        var result = await handler.Handle(new GetProviderCredentialsQuery(TenantId: null), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().ContainSingle();
        var dto = result.Value.Single();
        dto.Id.Should().Be(credential.Id);
        dto.Provider.Should().Be(AiProviderType.OpenAI);
        dto.DisplayName.Should().Be("OpenAI primary");
        dto.MaskedKey.Should().Be("sk-l****");
        dto.Status.Should().Be(ProviderCredentialStatus.Active);
        JsonSerializer.Serialize(dto).Should().NotContain("protected-secret");
        JsonSerializer.Serialize(dto).Should().NotContain("sk-live-list");
    }

    [Fact]
    public async Task RotateCredential_Replaces_Secret_And_Keeps_Metadata_Masked()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        await using var aiDb = CreateAiDb(tenantId, userId);
        await using var appDb = CreateAppDb(tenantId, userId);
        var existing = AiProviderCredential.Create(
            tenantId,
            AiProviderType.Anthropic,
            "Anthropic primary",
            "protected-old",
            "sk-old-prefix",
            userId);
        aiDb.AiProviderCredentials.Add(existing);
        await aiDb.SaveChangesAsync();
        var handler = CreateRotateHandler(
            aiDb,
            appDb,
            tenantId,
            Entitlements(),
            SecretProtector("protected-rotated", "sk-rotated12", "sk-r****"),
            userId);

        var result = await handler.Handle(new RotateProviderCredentialCommand(existing.Id, PlaintextSecret), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().NotBe(existing.Id);
        result.Value.DisplayName.Should().Be("Anthropic primary");
        result.Value.MaskedKey.Should().Be("sk-r****");
        var rows = await aiDb.AiProviderCredentials.IgnoreQueryFilters().OrderBy(c => c.CreatedAt).ToListAsync();
        rows.Should().HaveCount(2);
        rows[0].Status.Should().Be(ProviderCredentialStatus.Revoked);
        rows[1].Status.Should().Be(ProviderCredentialStatus.Active);
        rows[1].EncryptedSecret.Should().Be("protected-rotated");
    }

    [Fact]
    public async Task RevokeCredential_Marks_Credential_Revoked()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        await using var aiDb = CreateAiDb(tenantId, userId);
        await using var appDb = CreateAppDb(tenantId, userId);
        var credential = AiProviderCredential.Create(
            tenantId,
            AiProviderType.OpenAI,
            "OpenAI primary",
            "protected-secret",
            "sk-live-revoke",
            userId);
        aiDb.AiProviderCredentials.Add(credential);
        await aiDb.SaveChangesAsync();
        var handler = CreateRevokeHandler(aiDb, appDb, tenantId, userId);

        var result = await handler.Handle(new RevokeProviderCredentialCommand(credential.Id), default);

        result.IsSuccess.Should().BeTrue();
        var row = await aiDb.AiProviderCredentials.IgnoreQueryFilters().SingleAsync();
        row.Status.Should().Be(ProviderCredentialStatus.Revoked);
    }

    [Fact]
    public async Task CreateCredential_Writes_Audit_Without_Secret()
    {
        var tenantId = Guid.NewGuid();
        await using var aiDb = CreateAiDb(tenantId);
        await using var appDb = CreateAppDb(tenantId);
        var handler = CreateCreateHandler(aiDb, appDb, tenantId, Entitlements());

        var result = await handler.Handle(new CreateProviderCredentialCommand(
            tenantId,
            AiProviderType.OpenAI,
            "OpenAI primary",
            PlaintextSecret), default);

        result.IsSuccess.Should().BeTrue();
        var audit = await appDb.AuditLogs.IgnoreQueryFilters().SingleAsync();
        AssertAudit(audit, "AiProviderCredential.Created", result.Value.Id);
        audit.Action.Should().Be(AuditAction.Created);
    }

    [Fact]
    public async Task RotateCredential_Writes_Audit_Without_Secret()
    {
        var tenantId = Guid.NewGuid();
        await using var aiDb = CreateAiDb(tenantId);
        await using var appDb = CreateAppDb(tenantId);
        var credential = AiProviderCredential.Create(
            tenantId,
            AiProviderType.OpenAI,
            "OpenAI primary",
            "protected-old",
            "sk-live-old",
            Guid.NewGuid());
        aiDb.AiProviderCredentials.Add(credential);
        await aiDb.SaveChangesAsync();
        var handler = CreateRotateHandler(aiDb, appDb, tenantId, Entitlements());

        var result = await handler.Handle(new RotateProviderCredentialCommand(credential.Id, PlaintextSecret), default);

        result.IsSuccess.Should().BeTrue();
        var audit = await appDb.AuditLogs.IgnoreQueryFilters().SingleAsync();
        AssertAudit(audit, "AiProviderCredential.Rotated", result.Value.Id);
        audit.Action.Should().Be(AuditAction.Updated);
    }

    [Fact]
    public async Task RevokeCredential_Writes_Audit_Without_Secret()
    {
        var tenantId = Guid.NewGuid();
        await using var aiDb = CreateAiDb(tenantId);
        await using var appDb = CreateAppDb(tenantId);
        var credential = AiProviderCredential.Create(
            tenantId,
            AiProviderType.OpenAI,
            "OpenAI primary",
            "protected-secret",
            "sk-live-revoke",
            Guid.NewGuid());
        aiDb.AiProviderCredentials.Add(credential);
        await aiDb.SaveChangesAsync();
        var handler = CreateRevokeHandler(aiDb, appDb, tenantId);

        var result = await handler.Handle(new RevokeProviderCredentialCommand(credential.Id), default);

        result.IsSuccess.Should().BeTrue();
        var audit = await appDb.AuditLogs.IgnoreQueryFilters().SingleAsync();
        AssertAudit(audit, "AiProviderCredential.Revoked", credential.Id);
        audit.Action.Should().Be(AuditAction.Deleted);
    }

    [Fact]
    public async Task TestCredential_Writes_Audit_Without_Secret()
    {
        var tenantId = Guid.NewGuid();
        await using var aiDb = CreateAiDb(tenantId);
        await using var appDb = CreateAppDb(tenantId);
        var credential = AiProviderCredential.Create(
            tenantId,
            AiProviderType.OpenAI,
            "OpenAI primary",
            "protected-secret",
            "sk-live-test",
            Guid.NewGuid());
        aiDb.AiProviderCredentials.Add(credential);
        await aiDb.SaveChangesAsync();
        var protector = SecretProtector();
        protector.Setup(x => x.Unprotect("protected-secret")).Returns(PlaintextSecret);
        var handler = CreateTestHandler(aiDb, appDb, tenantId, protector);

        var result = await handler.Handle(new TestProviderCredentialCommand(credential.Id), default);

        result.IsSuccess.Should().BeTrue();
        var row = await aiDb.AiProviderCredentials.IgnoreQueryFilters().SingleAsync();
        row.LastValidatedAt.Should().NotBeNull();
        var audit = await appDb.AuditLogs.IgnoreQueryFilters().SingleAsync();
        AssertAudit(audit, "AiProviderCredential.Tested", credential.Id);
        audit.Action.Should().Be(AuditAction.Updated);
    }

    private static void AssertAudit(Starter.Domain.Common.AuditLog audit, string actionCode, Guid credentialId)
    {
        audit.EntityType.Should().Be(AuditEntityType.AiProviderCredential);
        audit.EntityId.Should().Be(credentialId);
        audit.Changes.Should().NotBeNullOrWhiteSpace();
        audit.Changes.Should().Contain(actionCode);
        audit.Changes.Should().Contain("\"actionCode\"");
        audit.Changes.Should().Contain("\"Provider\"");
        audit.Changes.Should().Contain("\"KeyPrefix\"");
        audit.Changes.Should().NotContain(PlaintextSecret);
        audit.Changes.Should().NotContain("protected-secret");
        audit.Changes.Should().NotContain("protected-old");
        audit.Changes.Should().NotContain("protected-new");
        audit.Changes.Should().NotContain("protected-rotated");
    }

    private static CreateProviderCredentialCommandHandler CreateCreateHandler(
        AiDbContext aiDb,
        ApplicationDbContext appDb,
        Guid tenantId,
        AiEntitlementsDto entitlements,
        Mock<IAiSecretProtector>? protector = null,
        Guid? userId = null)
    {
        return new CreateProviderCredentialCommandHandler(
            aiDb,
            appDb,
            CurrentUser(tenantId, userId).Object,
            EntitlementResolver(entitlements).Object,
            (protector ?? SecretProtector()).Object);
    }

    private static RotateProviderCredentialCommandHandler CreateRotateHandler(
        AiDbContext aiDb,
        ApplicationDbContext appDb,
        Guid tenantId,
        AiEntitlementsDto entitlements,
        Mock<IAiSecretProtector>? protector = null,
        Guid? userId = null)
    {
        return new RotateProviderCredentialCommandHandler(
            aiDb,
            appDb,
            CurrentUser(tenantId, userId).Object,
            EntitlementResolver(entitlements).Object,
            (protector ?? SecretProtector()).Object);
    }

    private static RevokeProviderCredentialCommandHandler CreateRevokeHandler(
        AiDbContext aiDb,
        ApplicationDbContext appDb,
        Guid tenantId,
        Guid? userId = null)
    {
        return new RevokeProviderCredentialCommandHandler(
            aiDb,
            appDb,
            CurrentUser(tenantId, userId).Object);
    }

    private static TestProviderCredentialCommandHandler CreateTestHandler(
        AiDbContext aiDb,
        ApplicationDbContext appDb,
        Guid tenantId,
        Mock<IAiSecretProtector> protector,
        Guid? userId = null)
    {
        return new TestProviderCredentialCommandHandler(
            aiDb,
            appDb,
            CurrentUser(tenantId, userId).Object,
            protector.Object);
    }

    private static AiDbContext CreateAiDb(Guid? tenantId, Guid? userId = null)
    {
        var options = new DbContextOptionsBuilder<AiDbContext>()
            .UseInMemoryDatabase($"ai-provider-credentials-{Guid.NewGuid()}")
            .Options;

        return new AiDbContext(options, CurrentUser(tenantId, userId).Object);
    }

    private static ApplicationDbContext CreateAppDb(Guid? tenantId, Guid? userId = null)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"ai-provider-credential-audit-{Guid.NewGuid()}")
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

    private static Mock<IAiSecretProtector> SecretProtector(
        string protectedSecret = "protected-secret",
        string keyPrefix = "sk-live-secr",
        string mask = "sk-l****")
    {
        var protector = new Mock<IAiSecretProtector>();
        protector.Setup(x => x.Protect(It.IsAny<string>())).Returns(protectedSecret);
        protector.Setup(x => x.Prefix(It.IsAny<string>())).Returns(keyPrefix);
        protector.Setup(x => x.Mask(It.IsAny<string>())).Returns(mask);
        return protector;
    }

    private static AiEntitlementsDto Entitlements(bool byokEnabled = true) =>
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
            AllowedProviders: ["OpenAI", "Anthropic"],
            AllowedModels: ["gpt-4o-mini"]);
}
