using FluentAssertions;
using Starter.Abstractions.Ai;
using Starter.Module.AI.Domain.Entities;
using Starter.Module.AI.Domain.Enums;
using Xunit;

namespace Starter.Api.Tests.Ai.Settings;

public sealed class AiSettingsDomainTests
{
    [Fact]
    public void TenantSettings_CreateDefault_Uses_PlatformOnly_And_Standard()
    {
        var tenantId = Guid.NewGuid();

        var settings = AiTenantSettings.CreateDefault(tenantId);

        settings.TenantId.Should().Be(tenantId);
        settings.RequestedProviderCredentialPolicy.Should().Be(ProviderCredentialPolicy.PlatformOnly);
        settings.DefaultSafetyPreset.Should().Be(SafetyPreset.Standard);
        settings.MonthlyCostCapUsd.Should().BeNull();
        settings.PlatformMonthlyCostCapUsd.Should().BeNull();
    }

    [Fact]
    public void TenantSettings_UpdatePolicy_Rejects_Invalid_Public_Rpm()
    {
        var settings = AiTenantSettings.CreateDefault(Guid.NewGuid());

        var act = () => settings.UpdatePublicWidgetDefaults(
            monthlyTokenCap: 1_000,
            dailyTokenCap: 100,
            requestsPerMinute: -1);

        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("requestsPerMinute");
    }

    [Fact]
    public void ProviderCredential_Rotate_Revokes_Old_And_Creates_Active_New()
    {
        var tenantId = Guid.NewGuid();
        var createdBy = Guid.NewGuid();
        var oldCredential = AiProviderCredential.Create(
            tenantId,
            AiProviderType.OpenAI,
            "OpenAI primary",
            encryptedSecret: "cipher-old",
            keyPrefix: "sk-live-old",
            createdBy);

        oldCredential.Revoke();
        var replacement = AiProviderCredential.Create(
            tenantId,
            AiProviderType.OpenAI,
            "OpenAI primary",
            encryptedSecret: "cipher-new",
            keyPrefix: "sk-live-new",
            createdBy);

        oldCredential.Status.Should().Be(ProviderCredentialStatus.Revoked);
        replacement.Status.Should().Be(ProviderCredentialStatus.Active);
        replacement.Provider.Should().Be(AiProviderType.OpenAI);
    }

    [Fact]
    public void PublicWidget_Normalizes_Allowed_Origins()
    {
        var widget = AiPublicWidget.Create(
            tenantId: Guid.NewGuid(),
            name: "Marketing site",
            allowedOrigins: new[] { "https://Example.com/", "https://example.com" },
            defaultAssistantId: null,
            defaultPersonaSlug: "anonymous",
            monthlyTokenCap: 10_000,
            dailyTokenCap: 1_000,
            requestsPerMinute: 20,
            createdByUserId: Guid.NewGuid());

        widget.AllowedOrigins.Should().Equal("https://example.com");
    }

    [Fact]
    public void WidgetCredential_Stores_Hash_Metadata_Only()
    {
        var credential = AiWidgetCredential.Create(
            tenantId: Guid.NewGuid(),
            widgetId: Guid.NewGuid(),
            keyPrefix: "pk_ai_12345678",
            keyHash: "$2a$12$abcdef",
            expiresAt: null,
            createdByUserId: Guid.NewGuid());

        credential.KeyPrefix.Should().Be("pk_ai_12345678");
        credential.KeyHash.Should().Be("$2a$12$abcdef");
        credential.Status.Should().Be(AiWidgetCredentialStatus.Active);
    }
}
