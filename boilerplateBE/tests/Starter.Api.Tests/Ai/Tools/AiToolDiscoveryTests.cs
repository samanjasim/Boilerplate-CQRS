using System.Text.Json;
using FluentAssertions;
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
}
