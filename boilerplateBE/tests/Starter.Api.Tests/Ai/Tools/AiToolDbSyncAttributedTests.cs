using System.Reflection;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Starter.Abstractions.Capabilities;
using Starter.Module.AI.Domain.Entities;
using Starter.Module.AI.Infrastructure.Persistence;
using Starter.Module.AI.Infrastructure.Services;
using Xunit;

namespace Starter.Api.Tests.Ai.Tools;

/// <summary>
/// Proves <see cref="AiToolRegistrySyncHostedService"/> correctly upserts a DB row for an
/// <see cref="AttributedAiToolDefinition"/>: creates when missing, refreshes fields while
/// preserving <c>IsEnabled</c> when the row already exists.
/// </summary>
public sealed class AiToolDbSyncAttributedTests
{
    [Fact]
    public async Task SyncHostedService_Creates_Row_For_New_Attributed_Tool()
    {
        var def = BuildAttributedDef();
        var (sp, sync) = BuildSync(def);
        await using var _ = sp;

        await sync.StartAsync(CancellationToken.None);

        using var scope = sp.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AiDbContext>();
        var row = await db.AiTools.AsNoTracking().SingleOrDefaultAsync(t => t.Name == def.Name);

        row.Should().NotBeNull();
        row!.Name.Should().Be(def.Name);
        row.Description.Should().Be(def.Description);
        row.Category.Should().Be(def.Category);
        row.RequiredPermission.Should().Be(def.RequiredPermission);
        row.ParameterSchema.Should().Be(def.ParameterSchema.GetRawText());
        row.IsReadOnly.Should().Be(def.IsReadOnly);
        row.IsEnabled.Should().BeTrue(); // new rows default enabled
    }

    [Fact]
    public async Task SyncHostedService_Updates_Row_For_Existing_Attributed_Tool_Preserving_IsEnabled()
    {
        var def = BuildAttributedDef();
        var (sp, sync) = BuildSync(def);
        await using var _ = sp;

        // Pre-populate with IsEnabled=false and stale description.
        using (var scope = sp.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AiDbContext>();
            db.AiTools.Add(AiTool.Create(
                name: def.Name,
                description: "stale description",
                commandType: "stale.type",
                requiredPermission: def.RequiredPermission,
                category: "stale-category",
                parameterSchema: "{}",
                isEnabled: false,
                isReadOnly: !def.IsReadOnly));
            await db.SaveChangesAsync();
        }

        await sync.StartAsync(CancellationToken.None);

        using var verifyScope = sp.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<AiDbContext>();
        var row = await verifyDb.AiTools.AsNoTracking().SingleAsync(t => t.Name == def.Name);

        row.IsEnabled.Should().BeFalse("IsEnabled is admin-managed and must survive a sync refresh");
        row.Description.Should().Be(def.Description);
        row.Category.Should().Be(def.Category);
        row.ParameterSchema.Should().Be(def.ParameterSchema.GetRawText());
        row.IsReadOnly.Should().Be(def.IsReadOnly);
    }

    private static AttributedAiToolDefinition BuildAttributedDef()
    {
        var attr = typeof(FixtureListThingsQuery).GetCustomAttribute<AiToolAttribute>()!;
        var schema = AiToolSchemaGenerator.Generate(typeof(FixtureListThingsQuery), attr);
        return new AttributedAiToolDefinition(
            typeof(FixtureListThingsQuery), attr, schema, moduleSource: "Fixtures");
    }

    private static (ServiceProvider Sp, AiToolRegistrySyncHostedService Sync) BuildSync(
        AttributedAiToolDefinition def)
    {
        var services = new ServiceCollection();
        // Stable per-test DB name so rows survive across scopes resolved from this provider.
        var dbName = $"sync-attr-{Guid.NewGuid()}";
        services.AddDbContext<AiDbContext>(o =>
        {
            o.UseInMemoryDatabase(dbName);
            // InMemory provider warns about transactions since SaveChanges opens one; ignore
            // so the hosted service can run unchanged.
            o.ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning));
        });
        var sp = services.BuildServiceProvider();

        var sync = new AiToolRegistrySyncHostedService(
            sp.GetRequiredService<IServiceScopeFactory>(),
            new IAiToolDefinition[] { def },
            NullLogger<AiToolRegistrySyncHostedService>.Instance);

        return (sp, sync);
    }
}
