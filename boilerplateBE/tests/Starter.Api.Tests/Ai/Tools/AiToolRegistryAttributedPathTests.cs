using System.Reflection;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Starter.Abstractions.Capabilities;
using Starter.Application.Common.Interfaces;
using Starter.Module.AI.Domain.Entities;
using Starter.Module.AI.Infrastructure.Persistence;
using Starter.Module.AI.Infrastructure.Services;
using Xunit;

namespace Starter.Api.Tests.Ai.Tools;

/// <summary>
/// Proves an <see cref="AttributedAiToolDefinition"/> flows through
/// <see cref="AiToolRegistryService.ResolveForAssistantAsync"/> identically to a hand-authored
/// <see cref="IAiToolDefinition"/>: permission gating + admin-disable gating still apply.
/// </summary>
public sealed class AiToolRegistryAttributedPathTests
{
    [Fact]
    public async Task AttributedTool_Resolves_Through_Registry_When_User_Has_Permission()
    {
        await using var harness = await Harness.CreateAsync(
            attributedDef: BuildAttributedDef(),
            userHasPermission: true,
            dbToolEnabled: true);

        var assistant = NewAssistantEnabled(harness.ToolName);

        var result = await harness.Registry.ResolveForAssistantAsync(assistant, CancellationToken.None);

        result.ProviderTools.Should().HaveCount(1);
        result.ProviderTools[0].Name.Should().Be(harness.ToolName);
        result.DefinitionsByName.Should().ContainKey(harness.ToolName);
    }

    [Fact]
    public async Task AttributedTool_Filtered_When_User_Lacks_Permission()
    {
        await using var harness = await Harness.CreateAsync(
            attributedDef: BuildAttributedDef(),
            userHasPermission: false,
            dbToolEnabled: true);

        var assistant = NewAssistantEnabled(harness.ToolName);

        var result = await harness.Registry.ResolveForAssistantAsync(assistant, CancellationToken.None);

        result.ProviderTools.Should().BeEmpty();
        result.DefinitionsByName.Should().BeEmpty();
    }

    [Fact]
    public async Task AttributedTool_Filtered_When_DB_Row_Disabled()
    {
        await using var harness = await Harness.CreateAsync(
            attributedDef: BuildAttributedDef(),
            userHasPermission: true,
            dbToolEnabled: false);

        var assistant = NewAssistantEnabled(harness.ToolName);

        var result = await harness.Registry.ResolveForAssistantAsync(assistant, CancellationToken.None);

        result.ProviderTools.Should().BeEmpty();
        result.DefinitionsByName.Should().BeEmpty();
    }

    private static AttributedAiToolDefinition BuildAttributedDef()
    {
        var attr = typeof(FixtureListThingsQuery).GetCustomAttribute<AiToolAttribute>()!;
        var schema = AiToolSchemaGenerator.Generate(typeof(FixtureListThingsQuery), attr);
        return new AttributedAiToolDefinition(
            typeof(FixtureListThingsQuery), attr, schema, moduleSource: "Fixtures");
    }

    private static AiAssistant NewAssistantEnabled(string toolName)
    {
        var a = AiAssistant.Create(
            tenantId: Guid.NewGuid(),
            name: "Test",
            description: null,
            systemPrompt: "sp",
            createdByUserId: Guid.NewGuid());
        a.SetEnabledTools(new[] { toolName });
        return a;
    }

    // Minimal harness: in-memory DbContext + attributed definition + stubbed ICurrentUserService.
    private sealed class Harness : IAsyncDisposable
    {
        public required AiToolRegistryService Registry { get; init; }
        public required string ToolName { get; init; }
        public required ServiceProvider Sp { get; init; }

        public static async Task<Harness> CreateAsync(
            AttributedAiToolDefinition attributedDef,
            bool userHasPermission,
            bool dbToolEnabled)
        {
            var services = new ServiceCollection();
            var dbName = $"reg-attr-{Guid.NewGuid()}";
            services.AddDbContext<AiDbContext>(o => o.UseInMemoryDatabase(dbName));

            var userMock = new Mock<ICurrentUserService>();
            userMock.Setup(u => u.HasPermission(attributedDef.RequiredPermission))
                .Returns(userHasPermission);
            services.AddScoped(_ => userMock.Object);

            var sp = services.BuildServiceProvider();

            // Seed the admin-enable row for the tool.
            using (var scope = sp.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AiDbContext>();
                db.AiTools.Add(AiTool.Create(
                    name: attributedDef.Name,
                    description: attributedDef.Description,
                    commandType: attributedDef.CommandType.AssemblyQualifiedName!,
                    requiredPermission: attributedDef.RequiredPermission,
                    category: attributedDef.Category,
                    parameterSchema: attributedDef.ParameterSchema.GetRawText(),
                    isEnabled: dbToolEnabled,
                    isReadOnly: attributedDef.IsReadOnly));
                await db.SaveChangesAsync();
            }

            var registry = new AiToolRegistryService(
                new IAiToolDefinition[] { attributedDef },
                sp.GetRequiredService<IServiceScopeFactory>());

            return new Harness { Registry = registry, ToolName = attributedDef.Name, Sp = sp };
        }

        public async ValueTask DisposeAsync()
        {
            await Sp.DisposeAsync();
        }
    }
}
