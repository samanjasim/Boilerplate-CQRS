using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Starter.Abstractions.Capabilities;
using Xunit;

namespace Starter.Api.Tests.Ai.Tools;

public sealed class AiToolDiscoveryTests
{
    [Fact]
    public void AttributedAdapter_Surfaces_Attribute_Fields_And_ModuleSource()
    {
        var attr = typeof(FixtureListThingsQuery)
            .GetCustomAttributes(typeof(AiToolAttribute), false)
            .Cast<AiToolAttribute>()
            .Single();
        var schema = AiToolSchemaGenerator.Generate(typeof(FixtureListThingsQuery), attr);

        IAiToolDefinition adapter = new AttributedAiToolDefinition(
            typeof(FixtureListThingsQuery), attr, schema, moduleSource: "Fixtures");

        adapter.Name.Should().Be("fixture_list_things");
        adapter.Description.Should().Be("List test things (read-only fixture).");
        adapter.Category.Should().Be("Fixtures");
        adapter.RequiredPermission.Should().Be(AiToolDiscoveryFixtures.ReadOnlyPermission);
        adapter.IsReadOnly.Should().BeTrue();
        adapter.CommandType.Should().Be(typeof(FixtureListThingsQuery));
        adapter.ParameterSchema.ValueKind.Should().Be(JsonValueKind.Object);

        (adapter as IAiToolDefinitionModuleSource)!.ModuleSource.Should().Be("Fixtures");
    }

    [Fact]
    public void AddAiToolsFromAssembly_Registers_Attributed_Types()
    {
        var services = new ServiceCollection();
        services.AddAiToolsFromAssembly(typeof(FixtureListThingsQuery).Assembly);

        var sp = services.BuildServiceProvider();
        var defs = sp.GetServices<IAiToolDefinition>().ToList();

        defs.Select(d => d.Name).Should()
            .Contain("fixture_list_things")
            .And.Contain("fixture_create_thing")
            .And.Contain("fixture_with_schema_override");
    }

    [Fact]
    public void AddAiToolsFromAssembly_Derives_ModuleSource_From_Assembly_Name()
    {
        var services = new ServiceCollection();
        services.AddAiToolsFromAssembly(typeof(FixtureListThingsQuery).Assembly);

        var sp = services.BuildServiceProvider();
        var def = sp.GetServices<IAiToolDefinition>()
            .Single(d => d.Name == "fixture_list_things");

        // Test assembly is "Starter.Api.Tests" → stripped "Starter." prefix → "Api.Tests".
        (def as IAiToolDefinitionModuleSource)!.ModuleSource.Should().Be("Api.Tests");
    }
}
