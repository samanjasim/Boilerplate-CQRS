using System.ComponentModel;
using System.Text.Json;
using FluentAssertions;
using MediatR;
using Starter.Abstractions.Capabilities;
using Starter.Shared.Results;
using Xunit;

namespace Starter.Api.Tests.Ai.Tools;

public sealed class AiToolSchemaGenerationTests
{
    private static AiToolAttribute GetAttr<T>() =>
        typeof(T).GetCustomAttributes(typeof(AiToolAttribute), false).Cast<AiToolAttribute>().Single();

    [Fact]
    public void Generates_Schema_From_Record_Shape()
    {
        var schema = AiToolSchemaGenerator.Generate(
            typeof(FixtureListThingsQuery),
            GetAttr<FixtureListThingsQuery>());

        schema.GetProperty("type").GetString().Should().Be("object");
        schema.GetProperty("additionalProperties").GetBoolean().Should().BeFalse();

        var props = schema.GetProperty("properties");
        props.TryGetProperty("pageNumber", out _).Should().BeTrue("camelCase naming is required");
        props.TryGetProperty("pageSize", out _).Should().BeTrue();
    }

    [Fact]
    public void Enriches_Property_Descriptions_From_DescriptionAttribute()
    {
        var schema = AiToolSchemaGenerator.Generate(
            typeof(FixtureListThingsQuery),
            GetAttr<FixtureListThingsQuery>());

        var pageNumber = schema.GetProperty("properties").GetProperty("pageNumber");
        pageNumber.GetProperty("description").GetString().Should().Be("Page number (1-based).");
    }

    [Fact]
    public void Omits_Properties_Marked_With_AiParameterIgnore_Even_When_Name_Is_TrustBoundary()
    {
        // Invariant: ignore-strip runs BEFORE trust-boundary enforcement. A TrustBoundary
        // name (like TenantId) carrying [AiParameterIgnore] is allowed — it's removed from
        // the schema before the trust-boundary check sees it.
        var schema = AiToolSchemaGenerator.Generate(
            typeof(FixtureCreateThingCommand),
            GetAttr<FixtureCreateThingCommand>());

        var props = schema.GetProperty("properties");
        props.TryGetProperty("tenantId", out _).Should().BeFalse();
        props.TryGetProperty("name", out _).Should().BeTrue();
    }

    [Fact]
    public void Required_Array_Is_Trimmed_When_An_Ignored_Property_Was_Required()
    {
        var schema = AiToolSchemaGenerator.Generate(
            typeof(FixtureRequireIgnoreQuery),
            GetAttr<FixtureRequireIgnoreQuery>());

        schema.GetProperty("properties").TryGetProperty("secretField", out _).Should().BeFalse();

        if (schema.TryGetProperty("required", out var required))
        {
            required.EnumerateArray()
                .Select(e => e.GetString())
                .Should().NotContain("secretField")
                .And.Contain("kept", "the non-ignored required property must stay");
        }
    }

    [Fact]
    public void Throws_When_Server_Trusted_Property_Is_Exposed()
    {
        // FixtureUnsafeTrustedFieldQuery intentionally has no [AiTool] — construct inline.
        var attr = new AiToolAttribute
        {
            Name = "fixture_unsafe_trusted_field",
            Description = "x",
            Category = "x",
            RequiredPermission = AiToolDiscoveryFixtures.ReadOnlyPermission,
        };

        var act = () => AiToolSchemaGenerator.Generate(typeof(FixtureUnsafeTrustedFieldQuery), attr);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*FixtureUnsafeTrustedFieldQuery*userId*");
    }

    [Fact]
    public void Uses_ParameterSchemaJson_Override_When_Present()
    {
        var schema = AiToolSchemaGenerator.Generate(
            typeof(FixtureWithSchemaOverrideQuery),
            GetAttr<FixtureWithSchemaOverrideQuery>());

        var props = schema.GetProperty("properties");
        props.TryGetProperty("custom", out _).Should().BeTrue();
        props.TryGetProperty("ignored", out _).Should().BeFalse("override replaces auto-derivation");
        props.GetProperty("custom").GetProperty("description").GetString()
            .Should().Be("Override.", "override JSON is returned verbatim — no enrichment");
    }

    [Fact]
    public void Throws_When_Override_Is_Invalid_Json()
    {
        var attr = new AiToolAttribute
        {
            Name = "bad_override",
            Description = "x",
            Category = "x",
            RequiredPermission = "x",
            ParameterSchemaJson = "{ not json"
        };

        var act = () => AiToolSchemaGenerator.Generate(typeof(FixtureListThingsQuery), attr);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*ParameterSchemaJson*");
    }
}
