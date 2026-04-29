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
    public void TenantSettings_CreateDefault_Rejects_Empty_Tenant()
    {
        var act = () => AiTenantSettings.CreateDefault(Guid.Empty);

        act.Should().Throw<ArgumentException>()
            .WithParameterName("tenantId");
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
            allowedOrigins: new[]
            {
                "https://Example.com/",
                "https://example.com",
                "https://example.com:443",
                "http://Example.com:80",
                "https://Example.com:8443"
            },
            defaultAssistantId: null,
            defaultPersonaSlug: "anonymous",
            monthlyTokenCap: 10_000,
            dailyTokenCap: 1_000,
            requestsPerMinute: 20,
            createdByUserId: Guid.NewGuid());

        widget.AllowedOrigins.Should().Equal(
            "http://example.com",
            "https://example.com",
            "https://example.com:8443");
    }

    [Fact]
    public void PublicWidget_Create_Rejects_Empty_Tenant()
    {
        var act = () => AiPublicWidget.Create(
            tenantId: Guid.Empty,
            name: "Marketing site",
            allowedOrigins: new[] { "https://example.com" },
            defaultAssistantId: null,
            defaultPersonaSlug: "anonymous",
            monthlyTokenCap: 10_000,
            dailyTokenCap: 1_000,
            requestsPerMinute: 20,
            createdByUserId: Guid.NewGuid());

        act.Should().Throw<ArgumentException>()
            .WithParameterName("tenantId");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void PublicWidget_Create_Rejects_Blank_Name(string name)
    {
        var act = () => AiPublicWidget.Create(
            tenantId: Guid.NewGuid(),
            name,
            allowedOrigins: new[] { "https://example.com" },
            defaultAssistantId: null,
            defaultPersonaSlug: "anonymous",
            monthlyTokenCap: 10_000,
            dailyTokenCap: 1_000,
            requestsPerMinute: 20,
            createdByUserId: Guid.NewGuid());

        act.Should().Throw<ArgumentException>()
            .WithParameterName("name");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void PublicWidget_Update_Rejects_Blank_Name(string name)
    {
        var widget = AiPublicWidget.Create(
            tenantId: Guid.NewGuid(),
            name: "Marketing site",
            allowedOrigins: new[] { "https://example.com" },
            defaultAssistantId: null,
            defaultPersonaSlug: "anonymous",
            monthlyTokenCap: 10_000,
            dailyTokenCap: 1_000,
            requestsPerMinute: 20,
            createdByUserId: Guid.NewGuid());

        var act = () => widget.Update(
            name,
            allowedOrigins: new[] { "https://example.com" },
            defaultAssistantId: null,
            defaultPersonaSlug: "anonymous",
            monthlyTokenCap: 10_000,
            dailyTokenCap: 1_000,
            requestsPerMinute: 20,
            metadataJson: null);

        act.Should().Throw<ArgumentException>()
            .WithParameterName("name");
    }

    [Fact]
    public void PublicWidget_Create_Rejects_Null_Allowed_Origins()
    {
        var act = () => AiPublicWidget.Create(
            tenantId: Guid.NewGuid(),
            name: "Marketing site",
            allowedOrigins: null!,
            defaultAssistantId: null,
            defaultPersonaSlug: "anonymous",
            monthlyTokenCap: 10_000,
            dailyTokenCap: 1_000,
            requestsPerMinute: 20,
            createdByUserId: Guid.NewGuid());

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("allowedOrigins");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-a-url")]
    [InlineData("https://example.com/path")]
    [InlineData("https://example.com?debug=true")]
    [InlineData("https://example.com/#frag")]
    [InlineData("https://user@example.com")]
    public void PublicWidget_Rejects_Invalid_Allowed_Origins(string origin)
    {
        var act = () => AiPublicWidget.Create(
            tenantId: Guid.NewGuid(),
            name: "Marketing site",
            allowedOrigins: new[] { origin },
            defaultAssistantId: null,
            defaultPersonaSlug: "anonymous",
            monthlyTokenCap: 10_000,
            dailyTokenCap: 1_000,
            requestsPerMinute: 20,
            createdByUserId: Guid.NewGuid());

        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData("https://*.example.com")]
    [InlineData("https://example.*")]
    [InlineData("https://*")]
    public void PublicWidget_Rejects_Wildcard_Allowed_Origins(string origin)
    {
        var act = () => AiPublicWidget.Create(
            tenantId: Guid.NewGuid(),
            name: "Marketing site",
            allowedOrigins: new[] { origin },
            defaultAssistantId: null,
            defaultPersonaSlug: "anonymous",
            monthlyTokenCap: 10_000,
            dailyTokenCap: 1_000,
            requestsPerMinute: 20,
            createdByUserId: Guid.NewGuid());

        act.Should().Throw<ArgumentException>()
            .WithMessage("*wildcard*");
    }

    [Fact]
    public void PublicWidget_Create_Rejects_Invalid_MetadataJson()
    {
        var act = () => AiPublicWidget.Create(
            tenantId: Guid.NewGuid(),
            name: "Marketing site",
            allowedOrigins: new[] { "https://example.com" },
            defaultAssistantId: null,
            defaultPersonaSlug: "anonymous",
            monthlyTokenCap: 10_000,
            dailyTokenCap: 1_000,
            requestsPerMinute: 20,
            createdByUserId: Guid.NewGuid(),
            metadataJson: "{not-json}");

        act.Should().Throw<ArgumentException>()
            .WithParameterName("metadataJson");
    }

    [Fact]
    public void PublicWidget_Update_Validates_And_Normalizes_MetadataJson()
    {
        var widget = AiPublicWidget.Create(
            tenantId: Guid.NewGuid(),
            name: "Marketing site",
            allowedOrigins: new[] { "https://example.com" },
            defaultAssistantId: null,
            defaultPersonaSlug: "anonymous",
            monthlyTokenCap: 10_000,
            dailyTokenCap: 1_000,
            requestsPerMinute: 20,
            createdByUserId: Guid.NewGuid());

        var invalidAct = () => widget.Update(
            name: "Marketing site",
            allowedOrigins: new[] { "https://example.com" },
            defaultAssistantId: null,
            defaultPersonaSlug: "anonymous",
            monthlyTokenCap: 10_000,
            dailyTokenCap: 1_000,
            requestsPerMinute: 20,
            metadataJson: "{not-json}");

        invalidAct.Should().Throw<ArgumentException>()
            .WithParameterName("metadataJson");

        widget.Update(
            name: "Marketing site",
            allowedOrigins: new[] { "https://example.com" },
            defaultAssistantId: null,
            defaultPersonaSlug: "anonymous",
            monthlyTokenCap: 10_000,
            dailyTokenCap: 1_000,
            requestsPerMinute: 20,
            metadataJson: "   ");

        widget.MetadataJson.Should().BeNull();

        widget.Update(
            name: "Marketing site",
            allowedOrigins: new[] { "https://example.com" },
            defaultAssistantId: null,
            defaultPersonaSlug: "anonymous",
            monthlyTokenCap: 10_000,
            dailyTokenCap: 1_000,
            requestsPerMinute: 20,
            metadataJson: """ { "source": "docs" } """);

        widget.MetadataJson.Should().Be("""{ "source": "docs" }""");
    }

    [Fact]
    public void ModelDefault_Create_Rejects_Invalid_State()
    {
        var tenantId = Guid.NewGuid();

        var emptyTenantAct = () => AiModelDefault.Create(
            Guid.Empty,
            AiAgentClass.Chat,
            AiProviderType.OpenAI,
            "gpt-4.1-mini",
            maxTokens: 1024,
            temperature: 0.2);
        var blankModelAct = () => AiModelDefault.Create(
            tenantId,
            AiAgentClass.Chat,
            AiProviderType.OpenAI,
            " ",
            maxTokens: 1024,
            temperature: 0.2);
        var negativeMaxTokensAct = () => AiModelDefault.Create(
            tenantId,
            AiAgentClass.Chat,
            AiProviderType.OpenAI,
            "gpt-4.1-mini",
            maxTokens: -1,
            temperature: 0.2);
        var nanTemperatureAct = () => AiModelDefault.Create(
            tenantId,
            AiAgentClass.Chat,
            AiProviderType.OpenAI,
            "gpt-4.1-mini",
            maxTokens: 1024,
            temperature: double.NaN);
        var highTemperatureAct = () => AiModelDefault.Create(
            tenantId,
            AiAgentClass.Chat,
            AiProviderType.OpenAI,
            "gpt-4.1-mini",
            maxTokens: 1024,
            temperature: 2.1);

        emptyTenantAct.Should().Throw<ArgumentException>().WithParameterName("tenantId");
        blankModelAct.Should().Throw<ArgumentException>().WithParameterName("model");
        negativeMaxTokensAct.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("maxTokens");
        nanTemperatureAct.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("temperature");
        highTemperatureAct.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("temperature");
    }

    [Fact]
    public void ModelDefault_Update_Rejects_Invalid_State()
    {
        var modelDefault = AiModelDefault.Create(
            Guid.NewGuid(),
            AiAgentClass.Chat,
            AiProviderType.OpenAI,
            "gpt-4.1-mini",
            maxTokens: 1024,
            temperature: 0.2);

        var blankModelAct = () => modelDefault.Update(
            AiProviderType.OpenAI,
            " ",
            maxTokens: 1024,
            temperature: 0.2);
        var negativeMaxTokensAct = () => modelDefault.Update(
            AiProviderType.OpenAI,
            "gpt-4.1-mini",
            maxTokens: -1,
            temperature: 0.2);
        var infiniteTemperatureAct = () => modelDefault.Update(
            AiProviderType.OpenAI,
            "gpt-4.1-mini",
            maxTokens: 1024,
            temperature: double.PositiveInfinity);
        var negativeTemperatureAct = () => modelDefault.Update(
            AiProviderType.OpenAI,
            "gpt-4.1-mini",
            maxTokens: 1024,
            temperature: -0.1);

        blankModelAct.Should().Throw<ArgumentException>().WithParameterName("model");
        negativeMaxTokensAct.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("maxTokens");
        infiniteTemperatureAct.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("temperature");
        negativeTemperatureAct.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("temperature");
    }

    [Fact]
    public void ProviderCredential_Create_Rejects_Invalid_State()
    {
        var tenantId = Guid.NewGuid();

        var emptyTenantAct = () => AiProviderCredential.Create(
            Guid.Empty,
            AiProviderType.OpenAI,
            "OpenAI primary",
            encryptedSecret: "cipher",
            keyPrefix: "sk-live",
            createdByUserId: null);
        var blankDisplayNameAct = () => AiProviderCredential.Create(
            tenantId,
            AiProviderType.OpenAI,
            " ",
            encryptedSecret: "cipher",
            keyPrefix: "sk-live",
            createdByUserId: null);
        var blankSecretAct = () => AiProviderCredential.Create(
            tenantId,
            AiProviderType.OpenAI,
            "OpenAI primary",
            encryptedSecret: " ",
            keyPrefix: "sk-live",
            createdByUserId: null);
        var blankKeyPrefixAct = () => AiProviderCredential.Create(
            tenantId,
            AiProviderType.OpenAI,
            "OpenAI primary",
            encryptedSecret: "cipher",
            keyPrefix: " ",
            createdByUserId: null);

        emptyTenantAct.Should().Throw<ArgumentException>().WithParameterName("tenantId");
        blankDisplayNameAct.Should().Throw<ArgumentException>().WithParameterName("displayName");
        blankSecretAct.Should().Throw<ArgumentException>().WithParameterName("encryptedSecret");
        blankKeyPrefixAct.Should().Throw<ArgumentException>().WithParameterName("keyPrefix");
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

    [Fact]
    public void WidgetCredential_Create_Rejects_Invalid_State()
    {
        var tenantId = Guid.NewGuid();
        var widgetId = Guid.NewGuid();

        var emptyTenantAct = () => AiWidgetCredential.Create(
            Guid.Empty,
            widgetId,
            keyPrefix: "pk_ai_12345678",
            keyHash: "$2a$12$abcdef",
            expiresAt: null,
            createdByUserId: null);
        var emptyWidgetAct = () => AiWidgetCredential.Create(
            tenantId,
            Guid.Empty,
            keyPrefix: "pk_ai_12345678",
            keyHash: "$2a$12$abcdef",
            expiresAt: null,
            createdByUserId: null);
        var blankKeyPrefixAct = () => AiWidgetCredential.Create(
            tenantId,
            widgetId,
            keyPrefix: " ",
            keyHash: "$2a$12$abcdef",
            expiresAt: null,
            createdByUserId: null);
        var blankKeyHashAct = () => AiWidgetCredential.Create(
            tenantId,
            widgetId,
            keyPrefix: "pk_ai_12345678",
            keyHash: " ",
            expiresAt: null,
            createdByUserId: null);

        emptyTenantAct.Should().Throw<ArgumentException>().WithParameterName("tenantId");
        emptyWidgetAct.Should().Throw<ArgumentException>().WithParameterName("widgetId");
        blankKeyPrefixAct.Should().Throw<ArgumentException>().WithParameterName("keyPrefix");
        blankKeyHashAct.Should().Throw<ArgumentException>().WithParameterName("keyHash");
    }
}
