# AI Module Plan 5c-1 — `[AiTool]` Attribute + Auto-Discovery Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ship `[AiTool]` attribute + auto-discovery so modules expose MediatR commands as AI-callable tools with zero per-tool boilerplate.

**Architecture:** An `[AiTool]` attribute lives in `Starter.Abstractions.Capabilities`; each module calls `services.AddAiToolsFromAssembly(...)` in its `ConfigureServices`; a scanner wraps each attributed type in an `AttributedAiToolDefinition` adapter implementing the existing `IAiToolDefinition` contract; schema is auto-derived via .NET's `JsonSchemaExporter` with `[AiParameterIgnore]` stripping trust-boundary fields and `[Description]` enriching property docs; the existing registry, DB sync, and chat execution paths see no contract change.

**Tech Stack:** .NET 10, xUnit + FluentAssertions + Moq, Microsoft.Extensions.DependencyInjection, MediatR, System.Text.Json.Schema (`JsonSchemaExporter`).

**Spec:** [docs/superpowers/specs/2026-04-24-ai-plan-5c-1-aitool-attribute-design.md](../specs/2026-04-24-ai-plan-5c-1-aitool-attribute-design.md)

**Branch:** `feature/ai-phase-5c`

---

## File Structure

### New files

| Path | Responsibility |
|---|---|
| `boilerplateBE/src/Starter.Abstractions/Capabilities/AiToolAttribute.cs` | Public attribute marking a MediatR request as an AI tool. |
| `boilerplateBE/src/Starter.Abstractions/Capabilities/AiParameterIgnoreAttribute.cs` | Property-level attribute that strips a property from the auto-derived schema. |
| `boilerplateBE/src/Starter.Abstractions/Capabilities/IAiToolDefinitionModuleSource.cs` | Optional capability exposing an `IAiToolDefinition`'s owning module. |
| `boilerplateBE/src/Starter.Abstractions/Capabilities/AiToolSchemaGenerator.cs` | Pure helper: type + attribute → `JsonElement` schema. |
| `boilerplateBE/src/Starter.Abstractions/Capabilities/AttributedAiToolDefinition.cs` | Internal adapter: attributed type → `IAiToolDefinition`. |
| `boilerplateBE/src/Starter.Abstractions/Capabilities/AiToolDiscoveryExtensions.cs` | `AddAiToolsFromAssembly` DI extension method. |
| `boilerplateBE/tests/Starter.Api.Tests/Ai/Tools/AiToolSchemaGenerationTests.cs` | Unit tests for the schema generator (description enrichment, ignore-attr stripping, trust-boundary fail, override). |
| `boilerplateBE/tests/Starter.Api.Tests/Ai/Tools/AiToolDiscoveryTests.cs` | Unit tests for assembly scan + adapter wiring + module-source derivation. |
| `boilerplateBE/tests/Starter.Api.Tests/Ai/Tools/AiToolRegistrationCollisionTests.cs` | Tests for intra-assembly + cross-assembly collision detection (fail-fast). |
| `boilerplateBE/tests/Starter.Api.Tests/Ai/Tools/AiToolDiscoveryFixtures.cs` | Shared internal test types (attributed records) used across the test classes. |

### Modified files

| Path | Change |
|---|---|
| `boilerplateBE/src/Starter.Abstractions/Capabilities/IAiToolDefinition.cs` | Docstring: recommend `[AiTool]`; describe interface as escape hatch. |
| `boilerplateBE/src/modules/Starter.Module.AI/AIModule.cs` | Remove explicit `ListMyConversationsAiTool` singleton registration; call `services.AddAiToolsFromAssembly(typeof(AIModule).Assembly)`. |
| `boilerplateBE/src/modules/Starter.Module.AI/Application/Queries/GetConversations/GetConversationsQuery.cs` | Add `[AiTool]` + property `[Description]`s. |
| `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Tools/ListMyConversationsAiTool.cs` | **Delete.** |
| `boilerplateBE/src/modules/Starter.Module.AI/Application/DTOs/AiToolDto.cs` | Add `Module` field. |
| `boilerplateBE/src/modules/Starter.Module.AI/Application/DTOs/AiToolMappers.cs` | Populate `Module` from `IAiToolDefinitionModuleSource` (or `"Unknown"`). |
| `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Services/AiToolRegistryService.cs` | Throw on duplicate `Name` (fail-fast) instead of first-wins. |
| `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Services/AiToolRegistrySyncHostedService.cs` | Throw on duplicate `Name` instead of log-and-skip. |
| `boilerplateBE/src/modules/Starter.Module.Products/ProductsModule.cs` | Add `services.AddAiToolsFromAssembly(typeof(ProductsModule).Assembly)`. |
| `boilerplateBE/src/modules/Starter.Module.Products/Application/Queries/GetProducts/GetProductsQuery.cs` | Add `[AiTool]`; mark `TenantId` with `[AiParameterIgnore]`; add `[Description]`s. |
| `boilerplateBE/src/Starter.Application/DependencyInjection.cs` | Add `services.AddAiToolsFromAssembly(typeof(DependencyInjection).Assembly)`. |
| `boilerplateBE/src/Starter.Application/Features/Users/Queries/GetUsers/GetUsersQuery.cs` | Add `[AiTool]`; add `[Description]`s. |
| `boilerplateBE/tests/Starter.Api.Tests/Ai/AiToolRegistryServiceTests.cs` | Adjust tests that instantiated `ListMyConversationsAiTool` to use the new attributed path. |

---

## Conventions used in this plan

- **Build command:** `dotnet build boilerplateBE/Starter.sln -c Debug --nologo`.
- **All-tests command:** `dotnet test boilerplateBE/Starter.sln --nologo`.
- **Focused-test command:** `dotnet test boilerplateBE/Starter.sln --filter "FullyQualifiedName~<pattern>" --nologo`.
- **Commit scope prefix:** `feat(ai):` for code adding behaviour, `refactor(ai):` for pure moves, `test(ai):` for test-only. Never add `Co-Authored-By` or any Claude mention (project rule).
- **Working directory:** `/Users/samanjasim/Projects/forme/Boilerplate-CQRS-ai-integration` (the `feature/ai-phase-5c` worktree).
- **TDD flow per task:** write failing test → run it (confirm red) → write minimal implementation → run it (confirm green) → commit. Commit only when tests pass.

---

## Task 1: Scaffold test fixtures folder + attribute contract test

**Goal:** Create the `Ai/Tools/` test folder with a fixture file that hosts attributed record types used across later tests; land the `[AiTool]` and `[AiParameterIgnore]` attributes alongside a test that loads them via reflection.

**Files:**
- Create: `boilerplateBE/src/Starter.Abstractions/Capabilities/AiToolAttribute.cs`
- Create: `boilerplateBE/src/Starter.Abstractions/Capabilities/AiParameterIgnoreAttribute.cs`
- Create: `boilerplateBE/tests/Starter.Api.Tests/Ai/Tools/AiToolDiscoveryFixtures.cs`
- Create: `boilerplateBE/tests/Starter.Api.Tests/Ai/Tools/AiToolAttributeTests.cs`

- [ ] **Step 1: Write the failing attribute-contract test**

Create `boilerplateBE/tests/Starter.Api.Tests/Ai/Tools/AiToolDiscoveryFixtures.cs`:

```csharp
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
    [AiParameterIgnore] Guid? TenantId = null)
    : IRequest<Result<Guid>>;

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
```

Create `boilerplateBE/tests/Starter.Api.Tests/Ai/Tools/AiToolAttributeTests.cs`:

```csharp
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
```

- [ ] **Step 2: Run to confirm it fails (compile error: types don't exist)**

```bash
dotnet test boilerplateBE/Starter.sln --filter "FullyQualifiedName~AiToolAttributeTests" --nologo
```

Expected: build failure — `AiToolAttribute` and `AiParameterIgnoreAttribute` do not exist.

- [ ] **Step 3: Create `AiToolAttribute`**

Create `boilerplateBE/src/Starter.Abstractions/Capabilities/AiToolAttribute.cs`:

```csharp
namespace Starter.Abstractions.Capabilities;

/// <summary>
/// Marks a MediatR request type (command or query) as an AI-callable tool. At DI
/// registration time the attributed type is wrapped in an <see cref="IAiToolDefinition"/>
/// adapter — the JSON Schema is auto-derived from the record shape, the adapter is added
/// to the tool catalog, and the tool becomes available to assistants that enable it.
///
/// <para>The LLM-safe command contract documented on <see cref="IAiToolDefinition"/> applies
/// identically here. In particular: do not attribute a command whose record shape contains
/// fields bound to server-trusted state (user id, tenant id, role flags) unless those fields
/// are explicitly excluded from the schema via <see cref="AiParameterIgnoreAttribute"/>.</para>
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, Inherited = false)]
public sealed class AiToolAttribute : Attribute
{
    /// <summary>Tool name used in LLM function calling (snake_case, unique across the process).</summary>
    public required string Name { get; init; }

    /// <summary>Human-readable description for the LLM to decide when to call this tool.</summary>
    public required string Description { get; init; }

    /// <summary>Grouping category shown in the admin UI and LLM catalog.</summary>
    public required string Category { get; init; }

    /// <summary>Permission the current user must hold for the tool to be offered.</summary>
    public required string RequiredPermission { get; init; }

    /// <summary>Hint for UI + LLM only; does not bypass <see cref="RequiredPermission"/>.</summary>
    public bool IsReadOnly { get; init; }

    /// <summary>
    /// Optional JSON Schema override. When set, skips auto-derivation and uses the supplied
    /// schema verbatim. Use when the schema cannot be expressed by the record shape
    /// (dynamic enums, polymorphic payloads). Prefer auto-derivation when possible.
    /// </summary>
    public string? ParameterSchemaJson { get; init; }
}
```

- [ ] **Step 4: Create `AiParameterIgnoreAttribute`**

Create `boilerplateBE/src/Starter.Abstractions/Capabilities/AiParameterIgnoreAttribute.cs`:

```csharp
namespace Starter.Abstractions.Capabilities;

/// <summary>
/// Excludes a property from the auto-derived JSON Schema of a tool. Apply to fields that
/// exist on the command for non-LLM callers (e.g., superadmin cross-tenant TenantId
/// override) but must not be set by the LLM. The property is left on the type and
/// unchanged during non-LLM dispatch.
/// </summary>
[AttributeUsage(AttributeTargets.Property, Inherited = false)]
public sealed class AiParameterIgnoreAttribute : Attribute;
```

- [ ] **Step 5: Run tests — expect green**

```bash
dotnet test boilerplateBE/Starter.sln --filter "FullyQualifiedName~AiToolAttributeTests" --nologo
```

Expected: 3 passed.

- [ ] **Step 6: Commit**

```bash
git add boilerplateBE/src/Starter.Abstractions/Capabilities/AiToolAttribute.cs \
        boilerplateBE/src/Starter.Abstractions/Capabilities/AiParameterIgnoreAttribute.cs \
        boilerplateBE/tests/Starter.Api.Tests/Ai/Tools/AiToolDiscoveryFixtures.cs \
        boilerplateBE/tests/Starter.Api.Tests/Ai/Tools/AiToolAttributeTests.cs
git commit -m "feat(ai): introduce [AiTool] + [AiParameterIgnore] attributes"
```

---

## Task 2: `IAiToolDefinitionModuleSource` capability interface

**Goal:** Add the optional capability interface that exposes an `IAiToolDefinition`'s owning module. Placed here so later tasks (adapter, DTO mapper) can reference it.

**Files:**
- Create: `boilerplateBE/src/Starter.Abstractions/Capabilities/IAiToolDefinitionModuleSource.cs`

- [ ] **Step 1: Create the interface**

```csharp
namespace Starter.Abstractions.Capabilities;

/// <summary>
/// Optional capability an <see cref="IAiToolDefinition"/> implementation can expose to report
/// the module it originated from. The admin tool catalog uses this to group tools by module.
/// Hand-authored definitions are free to leave this unimplemented — the DTO layer falls back
/// to "Unknown" in that case.
/// </summary>
public interface IAiToolDefinitionModuleSource
{
    /// <summary>Module identifier, e.g. "Products", "AI", "Core". Non-null when implemented.</summary>
    string ModuleSource { get; }
}
```

- [ ] **Step 2: Build to confirm no errors**

```bash
dotnet build boilerplateBE/src/Starter.Abstractions/Starter.Abstractions.csproj --nologo
```

Expected: Build succeeded.

- [ ] **Step 3: Commit**

```bash
git add boilerplateBE/src/Starter.Abstractions/Capabilities/IAiToolDefinitionModuleSource.cs
git commit -m "feat(ai): IAiToolDefinitionModuleSource capability for module-grouped tool catalog"
```

---

## Task 3: Schema generator (pure helper)

**Goal:** Ship the pure schema-generation function used by the discovery scanner. Takes a `Type` + `AiToolAttribute` and returns a `JsonElement` schema (auto-derived or from override) with `[AiParameterIgnore]` stripping and `[Description]` enrichment. Fails fast on trust-boundary leaks and generation errors.

**Files:**
- Create: `boilerplateBE/src/Starter.Abstractions/Capabilities/AiToolSchemaGenerator.cs`
- Create: `boilerplateBE/tests/Starter.Api.Tests/Ai/Tools/AiToolSchemaGenerationTests.cs`

- [ ] **Step 1: Write the failing tests**

Create `boilerplateBE/tests/Starter.Api.Tests/Ai/Tools/AiToolSchemaGenerationTests.cs`:

```csharp
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
    public void Omits_Properties_Marked_With_AiParameterIgnore()
    {
        var schema = AiToolSchemaGenerator.Generate(
            typeof(FixtureCreateThingCommand),
            GetAttr<FixtureCreateThingCommand>());

        var props = schema.GetProperty("properties");
        props.TryGetProperty("tenantId", out _).Should().BeFalse();
        props.TryGetProperty("name", out _).Should().BeTrue();
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
```

- [ ] **Step 2: Run — expect fail (generator does not exist)**

```bash
dotnet test boilerplateBE/Starter.sln --filter "FullyQualifiedName~AiToolSchemaGenerationTests" --nologo
```

Expected: build failure — `AiToolSchemaGenerator` does not exist.

- [ ] **Step 3: Implement `AiToolSchemaGenerator`**

Create `boilerplateBE/src/Starter.Abstractions/Capabilities/AiToolSchemaGenerator.cs`:

```csharp
using System.ComponentModel;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Schema;
using System.Text.Json.Serialization;

namespace Starter.Abstractions.Capabilities;

/// <summary>
/// Pure helper: converts an <c>[AiTool]</c>-attributed type into a JSON Schema
/// <see cref="JsonElement"/>. Applies <see cref="AiParameterIgnoreAttribute"/> stripping,
/// trust-boundary validation, and <see cref="DescriptionAttribute"/>-based enrichment — or
/// uses <see cref="AiToolAttribute.ParameterSchemaJson"/> verbatim when set.
/// </summary>
public static class AiToolSchemaGenerator
{
    // Property names (camelCase, after JsonSerializerOptions name policy) that the LLM
    // must never be allowed to supply — they bind to server-trusted state resolved by
    // handlers from ICurrentUserService.
    private static readonly HashSet<string> TrustBoundaryPropertyNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "tenantId",
        "userId",
        "createdByUserId",
        "modifiedByUserId",
        "impersonatedBy",
        "isSystemAdmin",
    };

    private static readonly JsonSerializerOptions SchemaOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public static JsonElement Generate(Type attributedType, AiToolAttribute attribute)
    {
        ArgumentNullException.ThrowIfNull(attributedType);
        ArgumentNullException.ThrowIfNull(attribute);

        return attribute.ParameterSchemaJson is { } overrideJson
            ? ParseOverride(overrideJson, attributedType)
            : AutoDerive(attributedType);
    }

    private static JsonElement ParseOverride(string overrideJson, Type attributedType)
    {
        try
        {
            using var doc = JsonDocument.Parse(overrideJson);
            return doc.RootElement.Clone();
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException(
                $"[AiTool] on '{attributedType.FullName}': ParameterSchemaJson override is not valid JSON. {ex.Message}",
                ex);
        }
    }

    private static JsonElement AutoDerive(Type attributedType)
    {
        JsonNode? node;
        try
        {
            node = JsonSchemaExporter.GetJsonSchemaAsNode(SchemaOptions, attributedType);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"[AiTool] on '{attributedType.FullName}': schema generation failed. {ex.Message}",
                ex);
        }

        if (node is not JsonObject obj)
            throw new InvalidOperationException(
                $"[AiTool] on '{attributedType.FullName}': expected a JSON object schema but got '{node?.GetValueKind()}'.");

        // JsonSchemaExporter emits draft-2020-12 shape: `type`, `properties`, `required`.
        StripIgnoredProperties(obj, attributedType);
        EnrichPropertyDescriptions(obj, attributedType);
        EnforceTrustBoundary(obj, attributedType);
        EnsureAdditionalPropertiesFalse(obj);

        return JsonSerializer.SerializeToElement(obj);
    }

    private static void StripIgnoredProperties(JsonObject root, Type type)
    {
        if (root["properties"] is not JsonObject propsObj) return;

        var ignoredCamel = type
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.GetCustomAttribute<AiParameterIgnoreAttribute>() is not null)
            .Select(p => JsonNamingPolicy.CamelCase.ConvertName(p.Name))
            .ToHashSet(StringComparer.Ordinal);

        if (ignoredCamel.Count == 0) return;

        foreach (var name in ignoredCamel)
            propsObj.Remove(name);

        if (root["required"] is JsonArray requiredArr)
        {
            for (var i = requiredArr.Count - 1; i >= 0; i--)
            {
                if (requiredArr[i]?.GetValue<string>() is { } n && ignoredCamel.Contains(n))
                    requiredArr.RemoveAt(i);
            }
        }
    }

    private static void EnrichPropertyDescriptions(JsonObject root, Type type)
    {
        if (root["properties"] is not JsonObject propsObj) return;

        foreach (var clrProp in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            var camel = JsonNamingPolicy.CamelCase.ConvertName(clrProp.Name);
            if (propsObj[camel] is not JsonObject propNode) continue;

            var desc = clrProp.GetCustomAttribute<DescriptionAttribute>()?.Description;
            if (!string.IsNullOrWhiteSpace(desc))
                propNode["description"] = desc;
        }
    }

    private static void EnforceTrustBoundary(JsonObject root, Type type)
    {
        if (root["properties"] is not JsonObject propsObj) return;

        foreach (var propName in propsObj.Select(kv => kv.Key).ToList())
        {
            if (TrustBoundaryPropertyNames.Contains(propName))
                throw new InvalidOperationException(
                    $"[AiTool] on '{type.FullName}': property '{propName}' is a server-trusted field. " +
                    $"Mark it with [AiParameterIgnore] or remove it from the record.");
        }
    }

    private static void EnsureAdditionalPropertiesFalse(JsonObject root)
    {
        // Explicit — JsonSchemaExporter may omit this by default; the contract requires it.
        root["additionalProperties"] = false;
    }
}
```

- [ ] **Step 4: Run tests — expect green**

```bash
dotnet test boilerplateBE/Starter.sln --filter "FullyQualifiedName~AiToolSchemaGenerationTests" --nologo
```

Expected: 6 passed.

- [ ] **Step 5: Commit**

```bash
git add boilerplateBE/src/Starter.Abstractions/Capabilities/AiToolSchemaGenerator.cs \
        boilerplateBE/tests/Starter.Api.Tests/Ai/Tools/AiToolSchemaGenerationTests.cs
git commit -m "feat(ai): AiToolSchemaGenerator — auto-derive, ignore-strip, trust-boundary guard"
```

---

## Task 4: `AttributedAiToolDefinition` adapter

**Goal:** Internal class that wraps an attributed type + generated schema as an `IAiToolDefinition` and reports its module via `IAiToolDefinitionModuleSource`.

**Files:**
- Create: `boilerplateBE/src/Starter.Abstractions/Capabilities/AttributedAiToolDefinition.cs`

- [ ] **Step 1: Add adapter test to `AiToolDiscoveryTests` fixture — create the test file with one test**

Create `boilerplateBE/tests/Starter.Api.Tests/Ai/Tools/AiToolDiscoveryTests.cs`:

```csharp
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
```

- [ ] **Step 2: Run — expect fail (adapter type does not exist)**

```bash
dotnet test boilerplateBE/Starter.sln --filter "FullyQualifiedName~AiToolDiscoveryTests.AttributedAdapter" --nologo
```

Expected: build failure.

- [ ] **Step 3: Implement the adapter**

Create `boilerplateBE/src/Starter.Abstractions/Capabilities/AttributedAiToolDefinition.cs`:

```csharp
using System.Text.Json;

namespace Starter.Abstractions.Capabilities;

/// <summary>
/// Adapter that makes an <c>[AiTool]</c>-decorated MediatR request type look like an
/// <see cref="IAiToolDefinition"/>. Constructed by <see cref="AiToolDiscoveryExtensions"/>
/// during assembly scan. Also exposes <see cref="IAiToolDefinitionModuleSource"/> so the
/// tool catalog can group by module.
/// </summary>
internal sealed class AttributedAiToolDefinition : IAiToolDefinition, IAiToolDefinitionModuleSource
{
    public AttributedAiToolDefinition(
        Type commandType,
        AiToolAttribute attribute,
        JsonElement parameterSchema,
        string moduleSource)
    {
        ArgumentNullException.ThrowIfNull(commandType);
        ArgumentNullException.ThrowIfNull(attribute);
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleSource);

        CommandType = commandType;
        Name = attribute.Name;
        Description = attribute.Description;
        Category = attribute.Category;
        RequiredPermission = attribute.RequiredPermission;
        IsReadOnly = attribute.IsReadOnly;
        ParameterSchema = parameterSchema;
        ModuleSource = moduleSource;
    }

    public string Name { get; }
    public string Description { get; }
    public JsonElement ParameterSchema { get; }
    public Type CommandType { get; }
    public string RequiredPermission { get; }
    public string Category { get; }
    public bool IsReadOnly { get; }
    public string ModuleSource { get; }
}
```

- [ ] **Step 4: Adjust the test to construct via internal visibility**

The adapter is `internal sealed` but the test project needs access. Add `InternalsVisibleTo` if not already present.

Check the .csproj:

```bash
grep -A 1 InternalsVisibleTo boilerplateBE/src/Starter.Abstractions/Starter.Abstractions.csproj
```

If not present, add to `boilerplateBE/src/Starter.Abstractions/Starter.Abstractions.csproj` (inside an existing `<ItemGroup>` or a new one):

```xml
<ItemGroup>
  <InternalsVisibleTo Include="Starter.Api.Tests" />
</ItemGroup>
```

- [ ] **Step 5: Run tests — expect green**

```bash
dotnet test boilerplateBE/Starter.sln --filter "FullyQualifiedName~AiToolDiscoveryTests.AttributedAdapter" --nologo
```

Expected: 1 passed.

- [ ] **Step 6: Commit**

```bash
git add boilerplateBE/src/Starter.Abstractions/Capabilities/AttributedAiToolDefinition.cs \
        boilerplateBE/src/Starter.Abstractions/Starter.Abstractions.csproj \
        boilerplateBE/tests/Starter.Api.Tests/Ai/Tools/AiToolDiscoveryTests.cs
git commit -m "feat(ai): AttributedAiToolDefinition adapter for attribute-driven tools"
```

---

## Task 5: `AddAiToolsFromAssembly` DI extension

**Goal:** Extension method that scans an assembly for `[AiTool]`-decorated types, validates them, generates schemas, and registers one `IAiToolDefinition` singleton per tool. Module-source string is derived from the assembly name.

**Files:**
- Create: `boilerplateBE/src/Starter.Abstractions/Capabilities/AiToolDiscoveryExtensions.cs`
- Modify: `boilerplateBE/tests/Starter.Api.Tests/Ai/Tools/AiToolDiscoveryTests.cs`

- [ ] **Step 1: Extend `AiToolDiscoveryTests` with scan tests**

Append to `AiToolDiscoveryTests.cs` (inside the existing class):

```csharp
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

        // Test assembly is "Starter.Api.Tests" → "Tests"? No — we strip "Starter.Module." or
        // "Starter." prefix. For "Starter.Api.Tests" the stripped source is "Api.Tests".
        (def as IAiToolDefinitionModuleSource)!.ModuleSource.Should().Be("Api.Tests");
    }
```

Add the missing using statement to the top of the test file:

```csharp
using Microsoft.Extensions.DependencyInjection;
```

- [ ] **Step 2: Run — expect fail (method does not exist)**

```bash
dotnet test boilerplateBE/Starter.sln --filter "FullyQualifiedName~AiToolDiscoveryTests" --nologo
```

Expected: build failure — `AddAiToolsFromAssembly` does not exist.

- [ ] **Step 3: Implement `AiToolDiscoveryExtensions`**

Create `boilerplateBE/src/Starter.Abstractions/Capabilities/AiToolDiscoveryExtensions.cs`:

```csharp
using System.Reflection;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace Starter.Abstractions.Capabilities;

/// <summary>
/// DI extension methods that scan an assembly for <c>[AiTool]</c>-decorated MediatR request
/// types and register each as a singleton <see cref="IAiToolDefinition"/>. Modules call this
/// once from their <c>ConfigureServices</c>.
/// </summary>
public static class AiToolDiscoveryExtensions
{
    /// <summary>
    /// Scan the supplied assembly for <c>[AiTool]</c>-decorated types and register each as
    /// an <see cref="IAiToolDefinition"/> singleton. Validates each attributed type up-front:
    /// the type must implement <see cref="IBaseRequest"/>, <c>RequiredPermission</c> must be
    /// a non-empty string, and the schema must be generatable. Any failure throws from this
    /// call — the service collection is not partially mutated. Idempotent: calling twice with
    /// the same assembly registers each tool twice, which is a collision detected later by
    /// the registry (see <c>AiToolRegistryService</c>).
    /// </summary>
    public static IServiceCollection AddAiToolsFromAssembly(
        this IServiceCollection services,
        Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(assembly);

        var moduleSource = DeriveModuleSource(assembly);

        var candidates = assembly
            .GetTypes()
            .Where(t => t.IsClass || t.IsValueType && !t.IsEnum)
            .Where(t => !t.IsAbstract)
            .Select(t => (Type: t, Attr: t.GetCustomAttribute<AiToolAttribute>(inherit: false)))
            .Where(x => x.Attr is not null)
            .ToList();

        if (candidates.Count == 0)
            return services;

        var seenNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var adapters = new List<AttributedAiToolDefinition>(candidates.Count);

        foreach (var (type, attr) in candidates)
        {
            ValidateShape(type, attr!);

            if (!seenNames.Add(attr!.Name))
                throw new InvalidOperationException(
                    $"[AiTool] duplicate Name '{attr.Name}' inside assembly '{assembly.GetName().Name}'. " +
                    $"Tool names must be unique.");

            var schema = AiToolSchemaGenerator.Generate(type, attr);
            adapters.Add(new AttributedAiToolDefinition(type, attr, schema, moduleSource));
        }

        foreach (var adapter in adapters)
            services.AddSingleton<IAiToolDefinition>(adapter);

        return services;
    }

    private static void ValidateShape(Type type, AiToolAttribute attr)
    {
        if (!typeof(IBaseRequest).IsAssignableFrom(type))
            throw new InvalidOperationException(
                $"[AiTool] on '{type.FullName}': attributed type must implement MediatR.IBaseRequest " +
                $"(IRequest or IRequest<T>).");

        if (string.IsNullOrWhiteSpace(attr.Name))
            throw new InvalidOperationException(
                $"[AiTool] on '{type.FullName}': Name is required.");

        if (string.IsNullOrWhiteSpace(attr.Description))
            throw new InvalidOperationException(
                $"[AiTool] on '{type.FullName}': Description is required.");

        if (string.IsNullOrWhiteSpace(attr.Category))
            throw new InvalidOperationException(
                $"[AiTool] on '{type.FullName}': Category is required.");

        if (string.IsNullOrWhiteSpace(attr.RequiredPermission))
            throw new InvalidOperationException(
                $"[AiTool] on '{type.FullName}': RequiredPermission is required.");
    }

    internal static string DeriveModuleSource(Assembly assembly)
    {
        var name = assembly.GetName().Name ?? "Unknown";
        const string modulePrefix = "Starter.Module.";
        const string starterPrefix = "Starter.";

        if (name.StartsWith(modulePrefix, StringComparison.Ordinal))
            return name[modulePrefix.Length..];
        if (name.StartsWith(starterPrefix, StringComparison.Ordinal))
            return name[starterPrefix.Length..];
        return name;
    }
}
```

- [ ] **Step 4: Run the full Tools test group — expect green**

```bash
dotnet test boilerplateBE/Starter.sln --filter "FullyQualifiedName~Starter.Api.Tests.Ai.Tools" --nologo
```

Expected: all Tools tests pass (attribute tests, schema tests, discovery tests).

- [ ] **Step 5: Commit**

```bash
git add boilerplateBE/src/Starter.Abstractions/Capabilities/AiToolDiscoveryExtensions.cs \
        boilerplateBE/tests/Starter.Api.Tests/Ai/Tools/AiToolDiscoveryFixtures.cs \
        boilerplateBE/tests/Starter.Api.Tests/Ai/Tools/AiToolDiscoveryTests.cs \
        boilerplateBE/tests/Starter.Api.Tests/Ai/Tools/AiToolSchemaGenerationTests.cs
git commit -m "feat(ai): AddAiToolsFromAssembly extension — scan + validate + register"
```

---

## Task 6: Fail-fast collision detection in registry + sync

**Goal:** The existing `AiToolRegistryService` and `AiToolRegistrySyncHostedService` currently silently keep the first of a duplicate `Name` and warn. Spec requires fail-fast. Change both to throw.

**Files:**
- Modify: `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Services/AiToolRegistryService.cs`
- Modify: `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Services/AiToolRegistrySyncHostedService.cs`
- Create: `boilerplateBE/tests/Starter.Api.Tests/Ai/Tools/AiToolRegistrationCollisionTests.cs`

- [ ] **Step 1: Write the failing collision tests**

Create `boilerplateBE/tests/Starter.Api.Tests/Ai/Tools/AiToolRegistrationCollisionTests.cs`:

```csharp
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Starter.Abstractions.Capabilities;
using Starter.Application.Common.Interfaces;
using Starter.Module.AI.Infrastructure.Persistence;
using Starter.Module.AI.Infrastructure.Services;
using Xunit;

namespace Starter.Api.Tests.Ai.Tools;

public sealed class AiToolRegistrationCollisionTests
{
    [Fact]
    public void Registry_Throws_When_Two_Definitions_Share_A_Name()
    {
        var scopeFactory = new Mock<IServiceScopeFactory>().Object;
        var defs = new IAiToolDefinition[]
        {
            StubDef("dup_tool"),
            StubDef("dup_tool"),
        };

        var act = () => new AiToolRegistryService(defs, scopeFactory);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*duplicate*dup_tool*");
    }

    [Fact]
    public async Task SyncHostedService_Throws_When_Duplicates_Present()
    {
        var defs = new IAiToolDefinition[]
        {
            StubDef("dup_tool"),
            StubDef("dup_tool"),
        };

        var services = new ServiceCollection();
        services.AddDbContext<AiDbContext>(o => o.UseInMemoryDatabase($"sync-{Guid.NewGuid()}"));
        var sp = services.BuildServiceProvider();

        var sync = new AiToolRegistrySyncHostedService(
            sp.GetRequiredService<IServiceScopeFactory>(),
            defs,
            NullLogger<AiToolRegistrySyncHostedService>.Instance);

        var act = () => sync.StartAsync(CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*duplicate*dup_tool*");
    }

    private static IAiToolDefinition StubDef(string name)
    {
        var mock = new Mock<IAiToolDefinition>();
        mock.SetupGet(m => m.Name).Returns(name);
        mock.SetupGet(m => m.Description).Returns("d");
        mock.SetupGet(m => m.Category).Returns("c");
        mock.SetupGet(m => m.RequiredPermission).Returns("p");
        mock.SetupGet(m => m.IsReadOnly).Returns(true);
        mock.SetupGet(m => m.CommandType).Returns(typeof(object));
        mock.SetupGet(m => m.ParameterSchema)
            .Returns(JsonDocument.Parse("""{ "type": "object" }""").RootElement);
        return mock.Object;
    }
}
```

- [ ] **Step 2: Run — expect fail (current code warns, does not throw)**

```bash
dotnet test boilerplateBE/Starter.sln --filter "FullyQualifiedName~AiToolRegistrationCollisionTests" --nologo
```

Expected: both tests fail — the constructor and the hosted service currently keep the first and do not throw.

- [ ] **Step 3: Update `AiToolRegistryService`**

Open `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Services/AiToolRegistryService.cs` and replace the current duplicate-swallowing group-by with fail-fast:

```csharp
internal sealed class AiToolRegistryService(
    IEnumerable<IAiToolDefinition> definitions,
    IServiceScopeFactory scopeFactory)
    : IAiToolRegistry
{
    private readonly IReadOnlyDictionary<string, IAiToolDefinition> _byName = BuildDictionary(definitions);

    private static IReadOnlyDictionary<string, IAiToolDefinition> BuildDictionary(
        IEnumerable<IAiToolDefinition> definitions)
    {
        var dict = new Dictionary<string, IAiToolDefinition>(StringComparer.OrdinalIgnoreCase);
        foreach (var d in definitions)
        {
            if (!dict.TryAdd(d.Name, d))
                throw new InvalidOperationException(
                    $"AI tool registry has duplicate Name '{d.Name}' — registered by " +
                    $"both '{dict[d.Name].CommandType.FullName}' and '{d.CommandType.FullName}'. " +
                    "Tool names must be unique across the process.");
        }
        return dict;
    }

    // rest of the class (FindByName, ListAllAsync, ResolveForAssistantAsync, EmptyResolution) is unchanged
```

Keep every other method in the file untouched.

- [ ] **Step 4: Update `AiToolRegistrySyncHostedService`**

Open `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Services/AiToolRegistrySyncHostedService.cs`. Replace the top of `StartAsync` — the `foreach` that logs warnings on duplicate becomes a throw:

```csharp
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var distinct = new Dictionary<string, IAiToolDefinition>(StringComparer.OrdinalIgnoreCase);
        foreach (var def in definitions)
        {
            if (!distinct.TryAdd(def.Name, def))
                throw new InvalidOperationException(
                    $"AI tool sync has duplicate Name '{def.Name}' — registered by " +
                    $"both '{distinct[def.Name].CommandType.FullName}' and '{def.CommandType.FullName}'. " +
                    "Tool names must be unique across the process.");
        }

        if (distinct.Count == 0)
        {
            logger.LogInformation("No IAiToolDefinition registrations to sync.");
            return;
        }

        // rest of StartAsync is unchanged
```

- [ ] **Step 5: Run the collision tests — expect green**

```bash
dotnet test boilerplateBE/Starter.sln --filter "FullyQualifiedName~AiToolRegistrationCollisionTests" --nologo
```

Expected: 2 passed.

- [ ] **Step 6: Run the full AI tests to confirm no regression**

```bash
dotnet test boilerplateBE/Starter.sln --filter "FullyQualifiedName~Starter.Api.Tests.Ai" --nologo
```

Expected: all AI tests pass.

- [ ] **Step 7: Commit**

```bash
git add boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Services/AiToolRegistryService.cs \
        boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Services/AiToolRegistrySyncHostedService.cs \
        boilerplateBE/tests/Starter.Api.Tests/Ai/Tools/AiToolRegistrationCollisionTests.cs
git commit -m "feat(ai): fail-fast on duplicate AI tool Name in registry and sync"
```

---

## Task 7: `AiToolDto.Module` field + mapper + handler

**Goal:** Surface the newly-available module source on the admin tool catalog.

**Files:**
- Modify: `boilerplateBE/src/modules/Starter.Module.AI/Application/DTOs/AiToolDto.cs`
- Modify: `boilerplateBE/src/modules/Starter.Module.AI/Application/DTOs/AiToolMappers.cs`

- [ ] **Step 1: Write test — extend `AiToolRegistryAttributedPathTests` inside `AiToolDiscoveryTests`**

Append a new test to `AiToolDiscoveryTests.cs`:

```csharp
    [Fact]
    public void AdapterMapper_Emits_Module_From_CapabilityInterface()
    {
        var attr = typeof(FixtureListThingsQuery)
            .GetCustomAttribute<AiToolAttribute>()!;
        var schema = AiToolSchemaGenerator.Generate(typeof(FixtureListThingsQuery), attr);
        IAiToolDefinition def = new AttributedAiToolDefinition(
            typeof(FixtureListThingsQuery), attr, schema, "Fixtures");

        var dto = def.ToDto(dbRow: null);

        dto.Module.Should().Be("Fixtures");
        dto.IsEnabled.Should().BeTrue();
    }

    [Fact]
    public void HandAuthored_Definition_Without_ModuleSource_Emits_Unknown()
    {
        var defMock = new Mock<IAiToolDefinition>();
        defMock.SetupGet(d => d.Name).Returns("hand_authored");
        defMock.SetupGet(d => d.Description).Returns("d");
        defMock.SetupGet(d => d.Category).Returns("c");
        defMock.SetupGet(d => d.RequiredPermission).Returns("p");
        defMock.SetupGet(d => d.IsReadOnly).Returns(true);
        defMock.SetupGet(d => d.CommandType).Returns(typeof(object));
        defMock.SetupGet(d => d.ParameterSchema)
            .Returns(JsonDocument.Parse("""{ "type": "object" }""").RootElement);

        var dto = defMock.Object.ToDto(dbRow: null);

        dto.Module.Should().Be("Unknown");
    }
```

Also add these usings to the top of `AiToolDiscoveryTests.cs` (next to the existing `using`s):

```csharp
using System.Reflection;
using Moq;
using Starter.Module.AI.Application.DTOs;
```

- [ ] **Step 2: Run — expect fail (`AiToolDto.Module` does not exist)**

```bash
dotnet test boilerplateBE/Starter.sln --filter "FullyQualifiedName~AiToolDiscoveryTests.AdapterMapper_Emits_Module_From_CapabilityInterface|FullyQualifiedName~AiToolDiscoveryTests.HandAuthored_Definition_Without_ModuleSource_Emits_Unknown" --nologo
```

Expected: build failure — `AiToolDto.Module` does not exist.

- [ ] **Step 3: Add `Module` to `AiToolDto`**

Replace `boilerplateBE/src/modules/Starter.Module.AI/Application/DTOs/AiToolDto.cs` with:

```csharp
using System.Text.Json;

namespace Starter.Module.AI.Application.DTOs;

public sealed record AiToolDto(
    string Name,
    string Description,
    string Category,
    string Module,
    string RequiredPermission,
    bool IsReadOnly,
    bool IsEnabled,
    JsonElement ParameterSchema);
```

- [ ] **Step 4: Update mappers to populate `Module`**

Replace `boilerplateBE/src/modules/Starter.Module.AI/Application/DTOs/AiToolMappers.cs` with:

```csharp
using System.Text.Json;
using Starter.Abstractions.Capabilities;
using Starter.Module.AI.Domain.Entities;

namespace Starter.Module.AI.Application.DTOs;

internal static class AiToolMappers
{
    private const string UnknownModule = "Unknown";

    public static AiToolDto ToDto(this IAiToolDefinition definition, AiTool? dbRow) =>
        new(
            definition.Name,
            definition.Description,
            definition.Category,
            ModuleOf(definition),
            definition.RequiredPermission,
            definition.IsReadOnly,
            IsEnabled: dbRow?.IsEnabled ?? true,
            definition.ParameterSchema);

    public static AiToolDto ToDto(this AiTool row, IAiToolDefinition definition) =>
        new(
            row.Name,
            row.Description,
            row.Category,
            ModuleOf(definition),
            row.RequiredPermission,
            row.IsReadOnly,
            row.IsEnabled,
            definition.ParameterSchema);

    private static string ModuleOf(IAiToolDefinition definition) =>
        definition is IAiToolDefinitionModuleSource src ? src.ModuleSource : UnknownModule;
}
```

- [ ] **Step 5: Run tests — expect green (for the two new tests + any existing DTO-consumers)**

```bash
dotnet test boilerplateBE/Starter.sln --filter "FullyQualifiedName~Starter.Api.Tests.Ai" --nologo
```

Expected: all AI tests pass.

If any existing test constructs `AiToolDto` directly with positional args it will fail to compile after the new `Module` parameter — fix those test sites by inserting the `Module` argument. Search: `new AiToolDto(` inside tests.

```bash
grep -rn 'new AiToolDto(' boilerplateBE/tests --include='*.cs'
```

If matches exist, update each to include the new `Module` argument in the correct position.

- [ ] **Step 6: Commit**

```bash
git add boilerplateBE/src/modules/Starter.Module.AI/Application/DTOs/AiToolDto.cs \
        boilerplateBE/src/modules/Starter.Module.AI/Application/DTOs/AiToolMappers.cs \
        boilerplateBE/tests/Starter.Api.Tests/Ai/Tools/AiToolDiscoveryTests.cs
git commit -m "feat(ai): AiToolDto.Module populated from IAiToolDefinitionModuleSource"
```

---

## Task 8: Migrate `GetConversationsQuery` to `[AiTool]`

**Goal:** Apply `[AiTool]` to `GetConversationsQuery`, delete `ListMyConversationsAiTool`, switch `AIModule` from explicit registration to assembly scan. The existing `"list_my_conversations"` name is preserved so the DB row updates in place.

**Files:**
- Modify: `boilerplateBE/src/modules/Starter.Module.AI/Application/Queries/GetConversations/GetConversationsQuery.cs`
- Delete: `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Tools/ListMyConversationsAiTool.cs`
- Modify: `boilerplateBE/src/modules/Starter.Module.AI/AIModule.cs`
- Modify: `boilerplateBE/tests/Starter.Api.Tests/Ai/AiToolRegistryServiceTests.cs` (if it references `ListMyConversationsAiTool`)

- [ ] **Step 1: Apply `[AiTool]` to `GetConversationsQuery`**

Replace `boilerplateBE/src/modules/Starter.Module.AI/Application/Queries/GetConversations/GetConversationsQuery.cs`:

```csharp
using System.ComponentModel;
using MediatR;
using Starter.Abstractions.Capabilities;
using Starter.Abstractions.Paging;
using Starter.Module.AI.Application.DTOs;
using Starter.Module.AI.Constants;
using Starter.Shared.Results;

namespace Starter.Module.AI.Application.Queries.GetConversations;

[AiTool(
    Name = "list_my_conversations",
    Description = "List the current user's recent AI conversations with title, message count, and last-message timestamp.",
    Category = "AI",
    RequiredPermission = AiPermissions.ViewConversations,
    IsReadOnly = true)]
public sealed record GetConversationsQuery(
    [property: Description("Page number (1-based). Default 1.")]
    int PageNumber = 1,
    [property: Description("Page size (1–100). Default 20.")]
    int PageSize = 20,
    [property: Description("Free-text search across conversation title.")]
    string? SearchTerm = null,
    [property: Description("Filter to a single assistant by id.")]
    Guid? AssistantId = null)
    : IRequest<Result<PaginatedList<AiConversationDto>>>;
```

- [ ] **Step 2: Delete the legacy tool class**

```bash
rm boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Tools/ListMyConversationsAiTool.cs
```

- [ ] **Step 3: Update `AIModule.ConfigureServices`**

Edit `boilerplateBE/src/modules/Starter.Module.AI/AIModule.cs`. Remove the line:

```csharp
services.AddSingleton<IAiToolDefinition, Infrastructure.Tools.ListMyConversationsAiTool>();
```

Replace it with:

```csharp
services.AddAiToolsFromAssembly(typeof(AIModule).Assembly);
```

The surrounding `services.AddSingleton<IAiToolRegistry, AiToolRegistryService>();` and `services.AddHostedService<AiToolRegistrySyncHostedService>();` lines stay. Ensure the `using Starter.Abstractions.Capabilities;` import is present at the top of the file (it should be already).

- [ ] **Step 4: Fix any test references to the deleted class**

```bash
grep -rn 'ListMyConversationsAiTool' boilerplateBE
```

Expected: zero matches. If any remain (e.g., inside `AiToolRegistryServiceTests.cs`), replace the instantiation with either (a) the `FindByName("list_my_conversations")` path via the registry, or (b) a hand-stubbed `IAiToolDefinition` using Moq. The existing test's `Harness.CreateAsync(toolDefinitions: ...)` already expects abstract `IAiToolDefinition` stubs — nothing should need changing unless a test explicitly constructed `new ListMyConversationsAiTool()`.

- [ ] **Step 5: Build**

```bash
dotnet build boilerplateBE/Starter.sln -c Debug --nologo
```

Expected: Build succeeded.

- [ ] **Step 6: Run the AI tests**

```bash
dotnet test boilerplateBE/Starter.sln --filter "FullyQualifiedName~Starter.Api.Tests.Ai" --nologo
```

Expected: all AI tests pass.

- [ ] **Step 7: Commit**

```bash
git add boilerplateBE/src/modules/Starter.Module.AI/Application/Queries/GetConversations/GetConversationsQuery.cs \
        boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Tools/ListMyConversationsAiTool.cs \
        boilerplateBE/src/modules/Starter.Module.AI/AIModule.cs \
        boilerplateBE/tests/Starter.Api.Tests/Ai/AiToolRegistryServiceTests.cs
git commit -m "refactor(ai): migrate list_my_conversations to [AiTool] on GetConversationsQuery"
```

---

## Task 9: Decorate `GetProductsQuery` + register Products module assembly

**Goal:** Prove the attribute against a non-AI module. Strip `TenantId` from the LLM schema via `[AiParameterIgnore]`.

**Files:**
- Modify: `boilerplateBE/src/modules/Starter.Module.Products/Application/Queries/GetProducts/GetProductsQuery.cs`
- Modify: `boilerplateBE/src/modules/Starter.Module.Products/ProductsModule.cs`

- [ ] **Step 1: Apply `[AiTool]` + `[AiParameterIgnore]` to `GetProductsQuery`**

Replace `boilerplateBE/src/modules/Starter.Module.Products/Application/Queries/GetProducts/GetProductsQuery.cs`:

```csharp
using System.ComponentModel;
using MediatR;
using Starter.Abstractions.Capabilities;
using Starter.Abstractions.Paging;
using Starter.Module.Products.Application.DTOs;
using Starter.Module.Products.Constants;
using Starter.Shared.Results;

namespace Starter.Module.Products.Application.Queries.GetProducts;

[AiTool(
    Name = "list_products",
    Description = "List products in the current tenant, paged and optionally filtered by status or search term.",
    Category = "Products",
    RequiredPermission = ProductPermissions.View,
    IsReadOnly = true)]
public sealed record GetProductsQuery(
    [property: Description("Page number, 1-based. Default 1.")]
    int PageNumber = 1,
    [property: Description("Page size, 1–100. Default 20.")]
    int PageSize = 20,
    [property: Description("Free-text search across product name and SKU.")]
    string? SearchTerm = null,
    [property: Description("Status filter: 'active', 'draft', or 'archived'.")]
    string? Status = null,
    [property: AiParameterIgnore] Guid? TenantId = null)
    : IRequest<Result<PaginatedList<ProductDto>>>;
```

Note the `[property: AiParameterIgnore]` targeting — positional record params need the `property:` target to place the attribute on the compiler-generated property.

- [ ] **Step 2: Register the Products assembly**

Edit `boilerplateBE/src/modules/Starter.Module.Products/ProductsModule.cs`. Inside `ConfigureServices`, after the existing `services.AddWorkflowableEntity` call and before `return services;`, add:

```csharp
        services.AddAiToolsFromAssembly(typeof(ProductsModule).Assembly);
```

- [ ] **Step 3: Build + run whole solution**

```bash
dotnet build boilerplateBE/Starter.sln -c Debug --nologo
```

Expected: Build succeeded.

```bash
dotnet test boilerplateBE/Starter.sln --nologo
```

Expected: all tests pass. If a Products test harness surfaces the new attribute (e.g., module-level test that constructs a ServiceCollection via `ProductsModule.ConfigureServices`), verify the tool is now registered.

- [ ] **Step 4: Commit**

```bash
git add boilerplateBE/src/modules/Starter.Module.Products/Application/Queries/GetProducts/GetProductsQuery.cs \
        boilerplateBE/src/modules/Starter.Module.Products/ProductsModule.cs
git commit -m "feat(products): expose GetProductsQuery as AI tool 'list_products'"
```

---

## Task 10: Decorate `GetUsersQuery` + register core application assembly

**Goal:** Prove the attribute against a core (non-module) query. Ensures `Starter.Application` is scanned so future core attributions work.

**Files:**
- Modify: `boilerplateBE/src/Starter.Application/DependencyInjection.cs`
- Modify: `boilerplateBE/src/Starter.Application/Features/Users/Queries/GetUsers/GetUsersQuery.cs`

- [ ] **Step 1: Apply `[AiTool]` to `GetUsersQuery`**

Replace `boilerplateBE/src/Starter.Application/Features/Users/Queries/GetUsers/GetUsersQuery.cs`:

```csharp
using System.ComponentModel;
using MediatR;
using Starter.Abstractions.Capabilities;
using Starter.Abstractions.Paging;
using Starter.Application.Common.Models;
using Starter.Application.Features.Users.DTOs;
using Starter.Shared.Constants;
using Starter.Shared.Results;

namespace Starter.Application.Features.Users.Queries.GetUsers;

[AiTool(
    Name = "list_users",
    Description = "List users in the current tenant, paged and optionally filtered by status or role.",
    Category = "Users",
    RequiredPermission = Permissions.Users.View,
    IsReadOnly = true)]
public sealed record GetUsersQuery : PaginationQuery, IRequest<Result<PaginatedList<UserDto>>>
{
    [Description("Filter by user status, e.g. 'Active', 'Suspended'.")]
    public string? Status { get; init; }

    [Description("Filter by role name, e.g. 'Admin', 'User'.")]
    public string? Role { get; init; }
}
```

Note that paging fields (`PageNumber`, `PageSize`, `SortBy`, `SortDescending`, `SearchTerm`) are inherited from `PaginationQuery` — they will appear in the auto-derived schema automatically. If you want descriptions on those inherited props, add `[Description]` attributes on `PaginationQuery` in a separate change — this plan leaves them undescribed to keep the change surface tight.

- [ ] **Step 2: Register the core application assembly**

Edit `boilerplateBE/src/Starter.Application/DependencyInjection.cs`. Add the using:

```csharp
using Starter.Abstractions.Capabilities;
```

Inside `AddApplication`, after the `foreach (var assembly in assemblies) services.AddValidatorsFromAssembly(assembly);` line and before `return services;`, add:

```csharp
        services.AddAiToolsFromAssembly(typeof(DependencyInjection).Assembly);
```

Only this assembly — not all module assemblies — because each module is responsible for calling `AddAiToolsFromAssembly` itself. Adding them all here would double-register.

- [ ] **Step 3: Build + full test run**

```bash
dotnet build boilerplateBE/Starter.sln -c Debug --nologo
dotnet test boilerplateBE/Starter.sln --nologo
```

Expected: all pass.

- [ ] **Step 4: Commit**

```bash
git add boilerplateBE/src/Starter.Application/Features/Users/Queries/GetUsers/GetUsersQuery.cs \
        boilerplateBE/src/Starter.Application/DependencyInjection.cs
git commit -m "feat(users): expose GetUsersQuery as AI tool 'list_users'"
```

---

## Task 11: Update `IAiToolDefinition` docstring

**Goal:** Recommend `[AiTool]` as the default; describe the interface as the escape hatch.

**Files:**
- Modify: `boilerplateBE/src/Starter.Abstractions/Capabilities/IAiToolDefinition.cs`

- [ ] **Step 1: Replace the XML summary block**

Open `boilerplateBE/src/Starter.Abstractions/Capabilities/IAiToolDefinition.cs`. Replace the existing `<summary>...</summary>` block above `public interface IAiToolDefinition` with:

```csharp
/// <summary>
/// Registers a MediatR request type as an AI-callable tool. In most cases prefer the
/// <see cref="AiToolAttribute"/> + <see cref="AiToolDiscoveryExtensions.AddAiToolsFromAssembly"/>
/// path — it auto-derives the JSON Schema from the record shape and removes per-tool
/// boilerplate. Implement this interface directly only when the schema cannot be expressed
/// by a static record shape (dynamic enums, runtime-computed polymorphism) or when the tool
/// is not backed by a plain MediatR type.
///
/// <para><b>LLM-safe command contract.</b> A command registered as an AI tool is invoked with
/// arguments the LLM synthesised from <see cref="ParameterSchema"/>. The registrar is
/// responsible for making sure the command type is safe to expose in this trust boundary:</para>
/// <list type="bullet">
///   <item><description>Every field the LLM can set MUST appear in <see cref="ParameterSchema"/>.
///     Properties on the command that are not in the schema will deserialize as default values —
///     do not depend on them being populated by the caller.</description></item>
///   <item><description>The command MUST NOT accept fields that bind to server-trusted state (user id,
///     tenant id, role, elevated flags). Those are resolved by handlers from
///     <c>ICurrentUserService</c>, not from LLM-provided JSON.</description></item>
///   <item><description>All mutating commands MUST be authorized by <see cref="RequiredPermission"/>;
///     the registry filters out tools the current user is not permitted to invoke. <see cref="IsReadOnly"/>
///     is a hint to the UI/LLM and does not relax authorization.</description></item>
///   <item><description>Commands SHOULD define a FluentValidation validator. The dispatcher relies on the
///     validation pipeline behavior to reject malformed LLM arguments and surface actionable error text
///     back to the model.</description></item>
///   <item><description>Handlers MUST tolerate adversarial inputs: treat LLM arguments as untrusted user input,
///     never interpolate into queries, and prefer enums/ids over free-form strings where possible.</description></item>
/// </list>
/// </summary>
```

Leave the body of the interface (property declarations) untouched.

- [ ] **Step 2: Build**

```bash
dotnet build boilerplateBE/src/Starter.Abstractions/Starter.Abstractions.csproj --nologo
```

Expected: Build succeeded.

- [ ] **Step 3: Commit**

```bash
git add boilerplateBE/src/Starter.Abstractions/Capabilities/IAiToolDefinition.cs
git commit -m "docs(ai): recommend [AiTool] in IAiToolDefinition summary"
```

---

## Task 12: End-to-end verification + full suite

**Goal:** Confirm nothing is broken anywhere.

- [ ] **Step 1: Full-solution build**

```bash
dotnet build boilerplateBE/Starter.sln -c Debug --nologo
```

Expected: Build succeeded. 0 warnings related to this change.

- [ ] **Step 2: Full-solution tests**

```bash
dotnet test boilerplateBE/Starter.sln --nologo
```

Expected: all tests pass.

- [ ] **Step 3: Boot check via Swagger**

Start the API and hit the tools catalog to confirm the attributed tools appear with their `Module` field populated.

```bash
cd boilerplateBE/src/Starter.Api
dotnet run --launch-profile http &
API_PID=$!
sleep 8

# Expect three attributed tools: list_my_conversations, list_products, list_users.
curl -s -H "X-Api-Key: dev-superadmin-key-if-configured" \
     "http://localhost:5000/api/v1/ai/tools" | jq '.data[] | {name, category, module, isReadOnly}'

kill $API_PID
```

If the `jq` output includes `list_my_conversations (AI, AI)`, `list_products (Products, Products)`, `list_users (Users, Application)` — success.

Expected: three rows returned; `module` field is populated for each.

Note: `list_users` reports module `"Application"` (from the assembly name `Starter.Application` after stripping `Starter.` prefix). A later task (outside this plan) may prettify that to `"Core"` in the DTO layer.

- [ ] **Step 4: Manual DB sync smoke check**

Query the DB to confirm the `AiTool` rows for the three tools exist:

```bash
docker exec -it starter-postgres psql -U postgres -d starter_db -c \
  "SELECT name, category, is_enabled, is_read_only FROM ai_module.ai_tools ORDER BY name;"
```

Expected: three rows — `list_my_conversations`, `list_products`, `list_users` — all `is_enabled = true`.

If the existing `list_my_conversations` row is present from a prior run, it should have been updated in place (by `AiToolRegistrySyncHostedService.RefreshFromDefinition`), not duplicated.

- [ ] **Step 5: If anything failed above, fix inline and re-commit. Otherwise, confirm state.**

```bash
git log --oneline origin/main..HEAD
```

Expected: roughly 10 commits implementing the plan (one per task).

```bash
git status
```

Expected: clean working tree, branch ahead of `origin/main` by the Task-count number of commits.

---

## Self-review notes

Checked against the spec (2026-04-24-ai-plan-5c-1-aitool-attribute-design.md) — every deliverables-checklist item in spec section 14 maps to at least one task here:

| Spec deliverable | Task |
|---|---|
| `AiToolAttribute` | Task 1 |
| `AiParameterIgnoreAttribute` | Task 1 |
| `AiToolDiscoveryExtensions.AddAiToolsFromAssembly` | Task 5 |
| `AttributedAiToolDefinition` + `IAiToolDefinitionModuleSource` | Tasks 2, 4 |
| Schema generation with trust-boundary validation + description enrichment | Task 3 |
| `AIModule.ConfigureServices` migration + deletion of `ListMyConversationsAiTool` + decoration of `GetConversationsQuery` | Task 8 |
| `ProductsModule.ConfigureServices` call + decoration of `GetProductsQuery` with `[AiParameterIgnore]` | Task 9 |
| `Starter.Application.DependencyInjection.AddApplication` call + decoration of `GetUsersQuery` | Task 10 |
| `AiToolDto` gains `Module` field; `GetToolsQueryHandler` populates it | Task 7 |
| Tests per spec section 9 | Tasks 1, 3, 4, 5, 6, 7 |
| Build + all existing tests green | Task 12 |
| `IAiToolDefinition` docstring updated | Task 11 |

Fail-fast semantics (spec section 8) are covered by:
- Intra-assembly Name collision — Task 5 (in `AddAiToolsFromAssembly`)
- Cross-assembly Name collision — Task 6 (in `AiToolRegistryService` + `AiToolRegistrySyncHostedService`)
- Missing `IBaseRequest` — Task 5 (in `ValidateShape`)
- Missing required attribute fields — Task 5 (in `ValidateShape`)
- Schema generation failure — Task 3 (in `AiToolSchemaGenerator.AutoDerive`)
- Trust-boundary leak — Task 3 (in `AiToolSchemaGenerator.EnforceTrustBoundary`)
- Invalid JSON override — Task 3 (in `AiToolSchemaGenerator.ParseOverride`)

Frontend DTO mirror (spec section 14 deliverable "frontend type mirrored") is **not** included — verified via `grep` that no frontend AI-tools type exists today; the admin UI hasn't shipped a tools page. When Plan 7a adds the UI it will build the type from the current DTO.
