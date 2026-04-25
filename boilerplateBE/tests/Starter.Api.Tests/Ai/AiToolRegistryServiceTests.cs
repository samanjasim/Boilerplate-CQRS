using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Starter.Abstractions.Capabilities;
using Starter.Application.Common.Interfaces;
using Starter.Module.AI.Domain.Entities;
using Starter.Abstractions.Ai;
using Starter.Module.AI.Domain.Enums;
using Starter.Module.AI.Infrastructure.Persistence;
using Starter.Module.AI.Infrastructure.Services;
using Xunit;

namespace Starter.Api.Tests.Ai;

/// <summary>
/// Locks in the filtering contract of <see cref="AiToolRegistryService.ResolveForAssistantAsync"/>:
/// a tool is returned only when (a) a matching IAiToolDefinition is registered, (b) the AiTool
/// row is enabled, and (c) the current user holds the required permission.
/// </summary>
public sealed class AiToolRegistryServiceTests
{
    [Fact]
    public async Task ResolveForAssistantAsync_Returns_Empty_When_Assistant_Has_No_Enabled_Names()
    {
        await using var harness = await Harness.CreateAsync(
            toolDefinitions: new[] { Tool("alpha", "alpha.run") },
            enabledInDb: new[] { "alpha" },
            userPermissions: new[] { "alpha.run" });

        var assistant = NewAssistant(enabledTools: Array.Empty<string>());

        var result = await harness.Registry.ResolveForAssistantAsync(assistant, CancellationToken.None);

        result.ProviderTools.Should().BeEmpty();
        result.DefinitionsByName.Should().BeEmpty();
    }

    [Fact]
    public async Task ResolveForAssistantAsync_Drops_Tools_The_User_Cannot_Invoke()
    {
        await using var harness = await Harness.CreateAsync(
            toolDefinitions: new[]
            {
                Tool("alpha", "alpha.run"),
                Tool("beta", "beta.run"),
            },
            enabledInDb: new[] { "alpha", "beta" },
            userPermissions: new[] { "alpha.run" }); // missing beta.run

        var assistant = NewAssistant(enabledTools: new[] { "alpha", "beta" });

        var result = await harness.Registry.ResolveForAssistantAsync(assistant, CancellationToken.None);

        result.DefinitionsByName.Keys.Should().BeEquivalentTo(new[] { "alpha" });
    }

    [Fact]
    public async Task ResolveForAssistantAsync_Drops_Tools_Disabled_In_Db()
    {
        await using var harness = await Harness.CreateAsync(
            toolDefinitions: new[]
            {
                Tool("alpha", "alpha.run"),
                Tool("beta", "beta.run"),
            },
            enabledInDb: new[] { "alpha" }, // beta intentionally missing → disabled
            userPermissions: new[] { "alpha.run", "beta.run" });

        var assistant = NewAssistant(enabledTools: new[] { "alpha", "beta" });

        var result = await harness.Registry.ResolveForAssistantAsync(assistant, CancellationToken.None);

        result.DefinitionsByName.Keys.Should().BeEquivalentTo(new[] { "alpha" });
    }

    [Fact]
    public async Task ResolveForAssistantAsync_Ignores_Unknown_Tool_Names_On_Assistant()
    {
        await using var harness = await Harness.CreateAsync(
            toolDefinitions: new[] { Tool("alpha", "alpha.run") },
            enabledInDb: new[] { "alpha" },
            userPermissions: new[] { "alpha.run" });

        // Assistant references a tool name that no IAiToolDefinition exposes (stale config).
        var assistant = NewAssistant(enabledTools: new[] { "alpha", "ghost" });

        var result = await harness.Registry.ResolveForAssistantAsync(assistant, CancellationToken.None);

        result.DefinitionsByName.Keys.Should().BeEquivalentTo(new[] { "alpha" });
    }

    private static IAiToolDefinition Tool(string name, string permission)
    {
        var def = new Mock<IAiToolDefinition>(MockBehavior.Strict);
        def.SetupGet(d => d.Name).Returns(name);
        def.SetupGet(d => d.Description).Returns($"{name} description");
        def.SetupGet(d => d.ParameterSchema).Returns(JsonDocument.Parse("{}").RootElement);
        def.SetupGet(d => d.CommandType).Returns(typeof(object));
        def.SetupGet(d => d.RequiredPermission).Returns(permission);
        def.SetupGet(d => d.Category).Returns("Tests");
        def.SetupGet(d => d.IsReadOnly).Returns(false);
        return def.Object;
    }

    private static AiAssistant NewAssistant(IReadOnlyList<string> enabledTools)
    {
        var a = AiAssistant.Create(
            tenantId: null,
            name: "Test Assistant",
            description: null,
            systemPrompt: "system",
            createdByUserId: Guid.NewGuid(),
            provider: null,
            model: null,
            temperature: 0.2,
            maxTokens: 256,
            executionMode: AssistantExecutionMode.Chat,
            maxAgentSteps: 3,
            isActive: true);
        a.SetEnabledTools(enabledTools);
        return a;
    }

    private sealed class Harness : IAsyncDisposable
    {
        public required AiToolRegistryService Registry { get; init; }
        public required ServiceProvider Root { get; init; }

        public static async Task<Harness> CreateAsync(
            IReadOnlyList<IAiToolDefinition> toolDefinitions,
            IReadOnlyList<string> enabledInDb,
            IReadOnlyList<string> userPermissions)
        {
            var fakeUser = new Mock<ICurrentUserService>(MockBehavior.Strict);
            var permissionSet = new HashSet<string>(userPermissions, StringComparer.OrdinalIgnoreCase);
            fakeUser.Setup(u => u.HasPermission(It.IsAny<string>()))
                .Returns<string>(permissionSet.Contains);
            fakeUser.SetupGet(u => u.TenantId).Returns((Guid?)null);

            var services = new ServiceCollection();
            services.AddSingleton<ICurrentUserService>(fakeUser.Object);
            // Bind the InMemory database name once so every scope shares the same store.
            var dbName = $"ai-registry-tests-{Guid.NewGuid()}";
            services.AddDbContext<AiDbContext>(opt => opt.UseInMemoryDatabase(dbName));

            var root = services.BuildServiceProvider();

            // Seed AiTool rows (only the "enabled" ones — absent names are treated as disabled by
            // the registry because it filters `IsEnabled == true`).
            using (var seedScope = root.CreateScope())
            {
                var db = seedScope.ServiceProvider.GetRequiredService<AiDbContext>();
                foreach (var name in enabledInDb)
                {
                    var def = toolDefinitions.First(d => d.Name == name);
                    db.AiTools.Add(AiTool.Create(
                        name: def.Name,
                        description: def.Description,
                        commandType: def.CommandType.FullName ?? def.CommandType.Name,
                        requiredPermission: def.RequiredPermission,
                        category: def.Category,
                        parameterSchema: def.ParameterSchema.GetRawText(),
                        isEnabled: true,
                        isReadOnly: def.IsReadOnly));
                }
                await db.SaveChangesAsync();
            }

            var registry = new AiToolRegistryService(
                toolDefinitions,
                root.GetRequiredService<IServiceScopeFactory>());

            return new Harness { Registry = registry, Root = root };
        }

        public ValueTask DisposeAsync()
        {
            Root.Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
