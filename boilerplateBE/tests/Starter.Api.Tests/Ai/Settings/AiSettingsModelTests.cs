using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Starter.Module.AI.Domain.Entities;
using Starter.Module.AI.Infrastructure.Persistence;
using Xunit;

namespace Starter.Api.Tests.Ai.Settings;

public sealed class AiSettingsModelTests
{
    [Fact]
    public void WidgetCredential_Has_TenantScoped_PublicWidget_ForeignKey()
    {
        var options = new DbContextOptionsBuilder<AiDbContext>()
            .UseInMemoryDatabase($"ai-settings-model-{Guid.NewGuid()}")
            .Options;
        using var db = new AiDbContext(options, currentUserService: null);

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
}
