using System.ComponentModel;
using MediatR;
using Starter.Abstractions.Capabilities;
using Starter.Shared.Results;

namespace Starter.Api.Tests.Ai.Tools;

// Fixture types used across discovery / schema / collision tests. Kept in one file so the
// attribute surface is visible to every test class at a glance.

internal static class AiToolDiscoveryFixtures
{
    public const string ReadOnlyPermission = "Test.Read";
    public const string WritePermission = "Test.Write";
}

[AiTool(
    Name = "fixture_list_things",
    Description = "List test things (read-only fixture).",
    Category = "Fixtures",
    RequiredPermission = AiToolDiscoveryFixtures.ReadOnlyPermission,
    IsReadOnly = true)]
internal sealed record FixtureListThingsQuery(
    [property: Description("Page number (1-based).")] int PageNumber = 1,
    [property: Description("Page size (1–100).")] int PageSize = 20)
    : IRequest<Result<IReadOnlyList<string>>>;

[AiTool(
    Name = "fixture_create_thing",
    Description = "Create a fixture thing.",
    Category = "Fixtures",
    RequiredPermission = AiToolDiscoveryFixtures.WritePermission)]
internal sealed record FixtureCreateThingCommand(
    [property: Description("Display name for the thing.")] string Name,
    [property: AiParameterIgnore] Guid? TenantId = null)
    : IRequest<Result<Guid>>;

// Fixture with a required property that is [AiParameterIgnore]-marked — exercises the
// trim-required-array branch in StripIgnoredProperties.
[AiTool(
    Name = "fixture_require_ignore",
    Description = "Fixture where a required property is ignored — verifies required-array trimming.",
    Category = "Fixtures",
    RequiredPermission = AiToolDiscoveryFixtures.ReadOnlyPermission)]
internal sealed record FixtureRequireIgnoreQuery(
    [property: Description("Kept in the schema.")] string Kept,
    [property: AiParameterIgnore] string SecretField)
    : IRequest<Result<string>>;

// Intentionally NOT attributed — used only in direct-call tests that construct an
// AiToolAttribute inline. Having [AiTool] here would break the assembly-scan test in
// Task 5 because these types violate the trust-boundary / IBaseRequest rules on purpose.
internal sealed record FixtureUnsafeTrustedFieldQuery(Guid UserId)
    : IRequest<Result<string>>;

internal sealed record FixtureNotAMediatRRequest(string Value);

// Attribute with an explicit schema override — used in AiToolSchemaGenerationTests.
[AiTool(
    Name = "fixture_with_schema_override",
    Description = "Uses ParameterSchemaJson override.",
    Category = "Fixtures",
    RequiredPermission = AiToolDiscoveryFixtures.ReadOnlyPermission,
    IsReadOnly = true,
    ParameterSchemaJson = """
    {
      "type": "object",
      "properties": { "custom": { "type": "string", "description": "Override." } },
      "additionalProperties": false
    }
    """)]
internal sealed record FixtureWithSchemaOverrideQuery(
    [property: Description("This property is ignored because the override is used.")] string Ignored = "x")
    : IRequest<Result<string>>;
