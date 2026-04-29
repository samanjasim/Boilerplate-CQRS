using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Starter.Module.AI.Domain.Entities;
using Starter.Module.AI.Infrastructure.Persistence;
using Xunit;

namespace Starter.Api.Tests.Ai.Settings;

public sealed class AiSettingsModelTests
{
    [Fact]
    public void PublicWidget_Json_Columns_Use_Postgres_Jsonb()
    {
        using var db = CreateNpgsqlModelContext();

        var widgetEntity = db.Model.FindEntityType(typeof(AiPublicWidget));

        widgetEntity.Should().NotBeNull();
        widgetEntity!.FindProperty(nameof(AiPublicWidget.AllowedOrigins))!
            .GetColumnType()
            .Should().Be("jsonb");
        widgetEntity.FindProperty(nameof(AiPublicWidget.MetadataJson))!
            .GetColumnType()
            .Should().Be("jsonb");
    }

    [Fact]
    public void WidgetCredential_Has_TenantScoped_PublicWidget_ForeignKey_In_Postgres_Model()
    {
        using var db = CreateNpgsqlModelContext();

        var credentialEntity = db.Model.FindEntityType(typeof(AiWidgetCredential));
        var widgetEntity = db.Model.FindEntityType(typeof(AiPublicWidget));

        credentialEntity.Should().NotBeNull();
        widgetEntity.Should().NotBeNull();

        var foreignKey = credentialEntity!.GetForeignKeys()
            .Single(fk => fk.PrincipalEntityType == widgetEntity);

        foreignKey.Properties.Select(p => p.Name).Should().Equal(
            nameof(AiWidgetCredential.TenantId),
            nameof(AiWidgetCredential.WidgetId));
        foreignKey.PrincipalKey.Properties.Select(p => p.Name).Should().Equal(
            nameof(AiPublicWidget.TenantId),
            nameof(AiPublicWidget.Id));
        foreignKey.DeleteBehavior.Should().Be(DeleteBehavior.Cascade);
    }

    [Fact]
    public void ProviderCredential_Active_TenantProvider_Index_Is_Filtered_Unique_In_Postgres_Model()
    {
        using var db = CreateNpgsqlModelContext();

        var credentialEntity = db.Model.FindEntityType(typeof(AiProviderCredential));

        credentialEntity.Should().NotBeNull();
        var index = FindIndex(
            credentialEntity!,
            nameof(AiProviderCredential.TenantId),
            nameof(AiProviderCredential.Provider));

        index.Should().NotBeNull();
        index!.IsUnique.Should().BeTrue();
        index.GetFilter().Should().Be("status = 0");
        index.GetDatabaseName().Should().Be("ux_ai_provider_credentials_active_tenant_provider");
    }

    [Fact]
    public void WidgetCredential_KeyPrefix_Index_Is_Unique_In_Postgres_Model()
    {
        using var db = CreateNpgsqlModelContext();

        var credentialEntity = db.Model.FindEntityType(typeof(AiWidgetCredential));

        credentialEntity.Should().NotBeNull();
        var index = FindIndex(credentialEntity!, nameof(AiWidgetCredential.KeyPrefix));

        index.Should().NotBeNull();
        index!.IsUnique.Should().BeTrue();
        index.GetDatabaseName().Should().Be("ux_ai_widget_credentials_key_prefix");
    }

    private static AiDbContext CreateNpgsqlModelContext()
    {
        var options = new DbContextOptionsBuilder<AiDbContext>()
            .UseNpgsql("Host=localhost;Database=ai_settings_model_tests;Username=model;Password=model")
            .Options;

        return new AiDbContext(options, currentUserService: null);
    }

    private static IIndex? FindIndex(IEntityType entityType, params string[] propertyNames) =>
        entityType.GetIndexes()
            .SingleOrDefault(i => i.Properties.Select(p => p.Name).SequenceEqual(propertyNames));
}
