using FluentAssertions;
using Starter.Abstractions.Capabilities;
using Xunit;

namespace Starter.Api.Tests.Ai.Tools;

public sealed class AiToolAttributeTests
{
    [Fact]
    public void AiToolAttribute_Reads_All_Required_Fields_From_Reflection()
    {
        var attr = typeof(FixtureListThingsQuery)
            .GetCustomAttributes(typeof(AiToolAttribute), inherit: false)
            .Cast<AiToolAttribute>()
            .Single();

        attr.Name.Should().Be("fixture_list_things");
        attr.Description.Should().Be("List test things (read-only fixture).");
        attr.Category.Should().Be("Fixtures");
        attr.RequiredPermission.Should().Be(AiToolDiscoveryFixtures.ReadOnlyPermission);
        attr.IsReadOnly.Should().BeTrue();
        attr.ParameterSchemaJson.Should().BeNull();
    }

    [Fact]
    public void AiToolAttribute_Accepts_Schema_Override()
    {
        var attr = typeof(FixtureWithSchemaOverrideQuery)
            .GetCustomAttributes(typeof(AiToolAttribute), inherit: false)
            .Cast<AiToolAttribute>()
            .Single();

        attr.ParameterSchemaJson.Should().NotBeNullOrWhiteSpace();
        attr.ParameterSchemaJson.Should().Contain("\"custom\"");
    }

    [Fact]
    public void AiParameterIgnoreAttribute_Marks_Property()
    {
        var prop = typeof(FixtureCreateThingCommand).GetProperty("TenantId")!;

        prop.GetCustomAttributes(typeof(AiParameterIgnoreAttribute), inherit: false)
            .Should().ContainSingle();
    }
}
