# AI Plan 5c-2 — Agent Templates Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ship the agent-template primitive — module-authored bundles of `(prompt + tools + persona targets + safety preset hint + provider/model)` installable into a tenant via a single command, with provenance tracking on the resulting assistant.

**Architecture:** Mirrors 5c-1's tool-discovery pattern. `IAiAgentTemplate` interface lives in `Starter.Abstractions.Capabilities`; modules register templates via `services.AddAiAgentTemplatesFromAssembly(typeof(XModule).Assembly)`. An in-memory singleton `IAiAgentTemplateRegistry` exposes the catalog with fail-fast duplicate-slug detection. `InstallTemplateCommand` materialises a tenant-scoped `AiAssistant` row, stamping `TemplateSourceSlug` (new nullable column) for provenance. Four boilerplate demo templates (Support Assistant + Product Expert × Anthropic + OpenAI) prove the abstraction. The legacy `AI:SeedSampleAssistant` config flag retires; `AI:InstallDemoTemplatesOnStartup` replaces it via the new install path.

**Tech Stack:** .NET 10, C# 12, EF Core 9, MediatR, FluentValidation, xUnit + Moq.

**Spec:** [`docs/superpowers/specs/2026-04-25-ai-plan-5c-2-agent-templates-design.md`](../specs/2026-04-25-ai-plan-5c-2-agent-templates-design.md)

**Branch:** `feature/ai-phase-5c-2`

---

## File structure

### New files

**`Starter.Abstractions/Capabilities/`**
- `IAiAgentTemplate.cs` — public interface; properties for the bundle contents
- `IAiAgentTemplateModuleSource.cs` — public capability interface for module grouping
- `AiAgentTemplateRegistration.cs` — `internal sealed` decorator wrapping author instances
- `AiAgentTemplateDiscoveryExtensions.cs` — `AddAiAgentTemplatesFromAssembly` + `internal ValidateShape` + `internal DeriveModuleSource` (or reuse 5c-1's helper)

**`Starter.Module.AI/Application/Services/`**
- `IAiAgentTemplateRegistry.cs` — public registry interface
- `AiAgentTemplateRegistry.cs` — `internal sealed` impl with fail-fast collision detection

**`Starter.Module.AI/Application/Commands/InstallTemplate/`**
- `InstallTemplateCommand.cs` — record + validator
- `InstallTemplateCommandValidator.cs` — FluentValidation rules
- `InstallTemplateCommandHandler.cs` — handler
- `TemplateErrors.cs` — static error definitions (or co-located in `Domain/Errors/TemplateErrors.cs` — see Task 8)

**`Starter.Module.AI/Application/Queries/GetTemplates/`**
- `GetTemplatesQuery.cs` — record
- `GetTemplatesQueryHandler.cs` — handler

**`Starter.Module.AI/Application/DTOs/`**
- `AiAgentTemplateDto.cs` — DTO record
- `AiAgentTemplateMappers.cs` — extension method `ToDto(this IAiAgentTemplate)` (mirrors `AiToolMappers.cs`)

**`Starter.Module.AI/Domain/Errors/`**
- `TemplateErrors.cs`

**`Starter.Module.AI/Controllers/`**
- `AiTemplatesController.cs`

**`Starter.Application/Features/Ai/Templates/`** (Core demo templates)
- `SupportAssistantPrompts.cs`
- `SupportAssistantAnthropicTemplate.cs`
- `SupportAssistantOpenAiTemplate.cs`

**`Starter.Module.Products/Application/Templates/`** (Products demo templates)
- `ProductExpertPrompts.cs`
- `ProductExpertAnthropicTemplate.cs`
- `ProductExpertOpenAiTemplate.cs`

**`tests/Starter.Api.Tests/Ai/Templates/`** (test files — exact list emerges per task)
- `AiAgentTemplateDiscoveryFixtures.cs`
- `AiAgentTemplateDiscoveryTests.cs`
- `AiAgentTemplateRegistrationTests.cs`
- `AiAgentTemplateRegistryTests.cs`
- `AiAgentTemplateMappersTests.cs`
- `GetTemplatesQueryHandlerTests.cs`
- `InstallTemplateCommandValidatorTests.cs`
- `InstallTemplateCommandHandlerTests.cs`
- `AiAssistantTemplateSourceTests.cs`
- `InstallDemoTemplatesSeedTests.cs`

### Modified files

- `Starter.Module.AI/Domain/Entities/AiAssistant.cs` — add two columns + `StampTemplateSource` method (Task 5)
- `Starter.Module.AI/Infrastructure/Configurations/AiAssistantConfiguration.cs` — declare new column lengths (Task 5)
- `Starter.Module.AI/Application/DTOs/AiAssistantDto.cs` — append two nullable fields with defaults (Task 5)
- Wherever `new AiAssistantDto(...)` is called — call sites compile-clean because new fields default to `null` (Task 5 verifies)
- `Starter.Module.AI/AIModule.cs` — add registry singleton + assembly scan; rewrite `SeedDataAsync` (Tasks 10, 13)
- `Starter.Application/DependencyInjection.cs` — add `AddAiAgentTemplatesFromAssembly` line (Task 11)
- `Starter.Module.Products/ProductsModule.cs` — add `AddAiAgentTemplatesFromAssembly` line (Task 12)
- `boilerplateBE/src/Starter.Api/appsettings.Development.json` — rename `AI:SeedSampleAssistant` → `AI:InstallDemoTemplatesOnStartup` (Task 13)
- `Starter.Abstractions.csproj` — already has `<InternalsVisibleTo Include="Starter.Api.Tests" />` from 5c-1; no change

---

## Self-Test Loop (per task)

After every task:

```bash
dotnet build boilerplateBE/Starter.sln -c Debug
dotnet test boilerplateBE/Starter.sln --filter "FullyQualifiedName~Ai"
```

A green build + green AI tests is the gate to commit.

---

## Task 1: Scaffold `IAiAgentTemplate` + `IAiAgentTemplateModuleSource` interfaces

**Files:**
- Create: `boilerplateBE/src/Starter.Abstractions/Capabilities/IAiAgentTemplate.cs`
- Create: `boilerplateBE/src/Starter.Abstractions/Capabilities/IAiAgentTemplateModuleSource.cs`

There are no behavioural tests for the interface itself — it's a type contract. Subsequent tasks (registration, discovery, registry) test it via fixtures.

- [ ] **Step 1: Create `IAiAgentTemplate.cs`**

```csharp
// boilerplateBE/src/Starter.Abstractions/Capabilities/IAiAgentTemplate.cs
using Starter.Module.AI.Domain.Enums;

namespace Starter.Abstractions.Capabilities;

/// <summary>
/// A module-authored agent preset. Implementations describe an assistant's
/// system prompt, model parameters, capability bindings, and audience targeting.
/// Discovered by <see cref="AiAgentTemplateDiscoveryExtensions.AddAiAgentTemplatesFromAssembly"/>
/// and installed via <c>InstallTemplateCommand</c> to materialise a tenant-scoped
/// <c>AiAssistant</c>.
///
/// Implementations MUST have a public parameterless constructor — templates are
/// pure data, not DI consumers. Use <c>const string</c> fields on a sibling helper
/// type for shared content (system prompts) when multiple variants share the same
/// prose.
/// </summary>
public interface IAiAgentTemplate
{
    /// <summary>Stable identity. Unique across all registered templates.</summary>
    string Slug { get; }

    string DisplayName { get; }
    string Description { get; }
    string Category { get; }

    string SystemPrompt { get; }
    AiProviderType Provider { get; }
    string Model { get; }
    double Temperature { get; }
    int MaxTokens { get; }
    AssistantExecutionMode ExecutionMode { get; }

    /// <summary>Tool slugs from the 5c-1 catalog. Validated at install time.</summary>
    IReadOnlyList<string> EnabledToolNames { get; }

    /// <summary>Persona slugs from <c>AiPersona</c>. Validated at install time.</summary>
    IReadOnlyList<string> PersonaTargetSlugs { get; }

    /// <summary>
    /// Recommended safety preset. Today persona-level safety still applies at runtime;
    /// this field becomes load-bearing when Plan 5d hoists safety onto the assistant.
    /// </summary>
    SafetyPreset? SafetyPresetHint { get; }
}
```

**NOTE on the namespace import.** `AiProviderType`, `AssistantExecutionMode`, and `SafetyPreset` live in `Starter.Module.AI.Domain.Enums`. `Starter.Abstractions` adding a project reference to `Starter.Module.AI` would create a circular dependency. Verify the existing reference graph: `Starter.Module.AI` already references `Starter.Abstractions` (the AI module consumes abstractions). The `using` above won't compile because the dependency would have to flow the other direction.

**Resolution:** move the three enums (`AiProviderType`, `AssistantExecutionMode`, `SafetyPreset`) to `Starter.Abstractions.Ai` (new sub-namespace). They're plain enums with no behaviour — moving them is mechanical.

- [ ] **Step 2: Move enums into `Starter.Abstractions`**

Create `boilerplateBE/src/Starter.Abstractions/Ai/AiProviderType.cs`, `AssistantExecutionMode.cs`, `SafetyPreset.cs`. Each:

```csharp
// AiProviderType.cs
namespace Starter.Abstractions.Ai;
public enum AiProviderType { OpenAI, Anthropic, Ollama }
```

```csharp
// AssistantExecutionMode.cs
namespace Starter.Abstractions.Ai;
public enum AssistantExecutionMode { Chat, Agent }
```

```csharp
// SafetyPreset.cs
namespace Starter.Abstractions.Ai;
public enum SafetyPreset
{
    Standard = 0,
    ChildSafe = 1,
    ProfessionalModerated = 2
}
```

Delete the original three files under `boilerplateBE/src/modules/Starter.Module.AI/Domain/Enums/AiProviderType.cs`, `AssistantExecutionMode.cs`, `SafetyPreset.cs`.

Update every file in the AI module + tests that imports `Starter.Module.AI.Domain.Enums` to also import `Starter.Abstractions.Ai`. Use a global find/replace:

```bash
grep -rln "Starter.Module.AI.Domain.Enums" boilerplateBE/src/modules/Starter.Module.AI \
  boilerplateBE/tests/Starter.Api.Tests
```

For each file, if it uses one of the three moved enums, add `using Starter.Abstractions.Ai;` (or change the existing using). Files that use *other* enums in `Starter.Module.AI.Domain.Enums` (e.g., `AiRagScope`, `ResourceVisibility`, `AssistantAccessMode`, `EmbeddingStatus`, `ConversationStatus`) keep that using.

- [ ] **Step 3: Build to verify the move**

```bash
dotnet build boilerplateBE/Starter.sln -c Debug 2>&1 | tail -20
```

Expected: clean build (0 errors). If you see "type or namespace 'AiProviderType' could not be found", a using statement is missing.

- [ ] **Step 4: Run AI tests to confirm no regression**

```bash
dotnet test boilerplateBE/Starter.sln --filter "FullyQualifiedName~Ai" 2>&1 | tail -10
```

Expected: all green.

- [ ] **Step 5: Now create `IAiAgentTemplate.cs`**

(Same content as Step 1, but `using Starter.Abstractions.Ai;` instead.)

```csharp
using Starter.Abstractions.Ai;

namespace Starter.Abstractions.Capabilities;

public interface IAiAgentTemplate
{
    string Slug { get; }
    string DisplayName { get; }
    string Description { get; }
    string Category { get; }
    string SystemPrompt { get; }
    AiProviderType Provider { get; }
    string Model { get; }
    double Temperature { get; }
    int MaxTokens { get; }
    AssistantExecutionMode ExecutionMode { get; }
    IReadOnlyList<string> EnabledToolNames { get; }
    IReadOnlyList<string> PersonaTargetSlugs { get; }
    SafetyPreset? SafetyPresetHint { get; }
}
```

(Keep the XML doc comments from Step 1.)

- [ ] **Step 6: Create `IAiAgentTemplateModuleSource.cs`**

```csharp
// boilerplateBE/src/Starter.Abstractions/Capabilities/IAiAgentTemplateModuleSource.cs
namespace Starter.Abstractions.Capabilities;

/// <summary>
/// Optional capability surfaced by the template scanner. Authors do not implement
/// this directly — the scanner wraps user-authored <see cref="IAiAgentTemplate"/>
/// instances in a decorator that exposes the source assembly's module name.
/// </summary>
public interface IAiAgentTemplateModuleSource
{
    string ModuleSource { get; }
}
```

- [ ] **Step 7: Build + test**

```bash
dotnet build boilerplateBE/Starter.sln -c Debug 2>&1 | tail -5
dotnet test boilerplateBE/Starter.sln --filter "FullyQualifiedName~Ai" 2>&1 | tail -5
```

Expected: clean build, all green.

- [ ] **Step 8: Commit**

```bash
git add boilerplateBE/src/Starter.Abstractions/Ai \
        boilerplateBE/src/Starter.Abstractions/Capabilities/IAiAgentTemplate.cs \
        boilerplateBE/src/Starter.Abstractions/Capabilities/IAiAgentTemplateModuleSource.cs \
        boilerplateBE/src/modules/Starter.Module.AI \
        boilerplateBE/tests/Starter.Api.Tests
git commit -m "feat(ai): IAiAgentTemplate interface + move provider/safety/exec-mode enums to Starter.Abstractions"
```

---

## Task 2: `AiAgentTemplateRegistration` decorator

**Files:**
- Create: `boilerplateBE/src/Starter.Abstractions/Capabilities/AiAgentTemplateRegistration.cs`
- Create: `boilerplateBE/tests/Starter.Api.Tests/Ai/Templates/AiAgentTemplateRegistrationTests.cs`

The decorator wraps an `IAiAgentTemplate` author instance and adds `IAiAgentTemplateModuleSource`. It's `internal sealed`; the test assembly sees it via `InternalsVisibleTo`.

- [ ] **Step 1: Write the failing test**

```csharp
// boilerplateBE/tests/Starter.Api.Tests/Ai/Templates/AiAgentTemplateRegistrationTests.cs
using Starter.Abstractions.Ai;
using Starter.Abstractions.Capabilities;
using Xunit;

namespace Starter.Api.Tests.Ai.Templates;

public class AiAgentTemplateRegistrationTests
{
    [Fact]
    public void Registration_exposes_module_source_and_delegates_template_properties()
    {
        var inner = new TestTemplate(slug: "x", display: "X");
        var reg = new AiAgentTemplateRegistration(inner, "Products");

        Assert.Equal("Products", ((IAiAgentTemplateModuleSource)reg).ModuleSource);
        Assert.Equal("x", ((IAiAgentTemplate)reg).Slug);
        Assert.Equal("X", ((IAiAgentTemplate)reg).DisplayName);
        Assert.Equal(inner.SystemPrompt, ((IAiAgentTemplate)reg).SystemPrompt);
        Assert.Same(inner.EnabledToolNames, ((IAiAgentTemplate)reg).EnabledToolNames);
    }

    private sealed class TestTemplate(string slug, string display) : IAiAgentTemplate
    {
        public string Slug { get; } = slug;
        public string DisplayName { get; } = display;
        public string Description => "test";
        public string Category => "Test";
        public string SystemPrompt => "You are a test.";
        public AiProviderType Provider => AiProviderType.Anthropic;
        public string Model => "test-model";
        public double Temperature => 0.5;
        public int MaxTokens => 512;
        public AssistantExecutionMode ExecutionMode => AssistantExecutionMode.Chat;
        public IReadOnlyList<string> EnabledToolNames { get; } = new[] { "fixture_tool" };
        public IReadOnlyList<string> PersonaTargetSlugs { get; } = new[] { "default" };
        public SafetyPreset? SafetyPresetHint => SafetyPreset.Standard;
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

```bash
dotnet test boilerplateBE/Starter.sln --filter "AiAgentTemplateRegistrationTests" 2>&1 | tail -5
```

Expected: FAIL — "type or namespace name 'AiAgentTemplateRegistration' could not be found".

- [ ] **Step 3: Implement `AiAgentTemplateRegistration`**

```csharp
// boilerplateBE/src/Starter.Abstractions/Capabilities/AiAgentTemplateRegistration.cs
using Starter.Abstractions.Ai;

namespace Starter.Abstractions.Capabilities;

internal sealed class AiAgentTemplateRegistration(
    IAiAgentTemplate inner,
    string moduleSource) : IAiAgentTemplate, IAiAgentTemplateModuleSource
{
    public string Slug => inner.Slug;
    public string DisplayName => inner.DisplayName;
    public string Description => inner.Description;
    public string Category => inner.Category;
    public string SystemPrompt => inner.SystemPrompt;
    public AiProviderType Provider => inner.Provider;
    public string Model => inner.Model;
    public double Temperature => inner.Temperature;
    public int MaxTokens => inner.MaxTokens;
    public AssistantExecutionMode ExecutionMode => inner.ExecutionMode;
    public IReadOnlyList<string> EnabledToolNames => inner.EnabledToolNames;
    public IReadOnlyList<string> PersonaTargetSlugs => inner.PersonaTargetSlugs;
    public SafetyPreset? SafetyPresetHint => inner.SafetyPresetHint;

    public string ModuleSource => moduleSource;
}
```

- [ ] **Step 4: Run the test to verify it passes**

```bash
dotnet test boilerplateBE/Starter.sln --filter "AiAgentTemplateRegistrationTests" 2>&1 | tail -5
```

Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add boilerplateBE/src/Starter.Abstractions/Capabilities/AiAgentTemplateRegistration.cs \
        boilerplateBE/tests/Starter.Api.Tests/Ai/Templates/AiAgentTemplateRegistrationTests.cs
git commit -m "feat(ai): AiAgentTemplateRegistration decorator"
```

---

## Task 3: `AiAgentTemplateDiscoveryExtensions` (scanner + `ValidateShape`)

**Files:**
- Create: `boilerplateBE/src/Starter.Abstractions/Capabilities/AiAgentTemplateDiscoveryExtensions.cs`
- Create: `boilerplateBE/tests/Starter.Api.Tests/Ai/Templates/AiAgentTemplateDiscoveryFixtures.cs`
- Create: `boilerplateBE/tests/Starter.Api.Tests/Ai/Templates/AiAgentTemplateDiscoveryTests.cs`

`AddAiAgentTemplatesFromAssembly` scans an assembly for parameterless concrete classes implementing `IAiAgentTemplate`, validates their shape, and registers each via `AddSingleton<IAiAgentTemplate>` wrapped in `AiAgentTemplateRegistration`.

`DeriveModuleSource(Assembly)` is the same logic used in 5c-1's `AiToolDiscoveryExtensions`. **Decision: copy-paste rather than extract.** Extracting a shared helper into a third file just to dedupe two ~10-line methods adds indirection. Both copies are stable.

- [ ] **Step 1: Write fixtures**

```csharp
// boilerplateBE/tests/Starter.Api.Tests/Ai/Templates/AiAgentTemplateDiscoveryFixtures.cs
using Starter.Abstractions.Ai;
using Starter.Abstractions.Capabilities;

namespace Starter.Api.Tests.Ai.Templates;

internal sealed class FixtureTemplateA : IAiAgentTemplate
{
    public string Slug => "fixture_a";
    public string DisplayName => "Fixture A";
    public string Description => "First fixture template.";
    public string Category => "FixtureCat";
    public string SystemPrompt => "You are fixture A.";
    public AiProviderType Provider => AiProviderType.Anthropic;
    public string Model => "claude-sonnet-4-20250514";
    public double Temperature => 0.3;
    public int MaxTokens => 1024;
    public AssistantExecutionMode ExecutionMode => AssistantExecutionMode.Chat;
    public IReadOnlyList<string> EnabledToolNames { get; } = new[] { "fixture_tool" };
    public IReadOnlyList<string> PersonaTargetSlugs { get; } = new[] { "default" };
    public SafetyPreset? SafetyPresetHint => SafetyPreset.Standard;
}

internal sealed class FixtureTemplateB : IAiAgentTemplate
{
    public string Slug => "fixture_b";
    public string DisplayName => "Fixture B";
    public string Description => "Second fixture template.";
    public string Category => "FixtureCat";
    public string SystemPrompt => "You are fixture B.";
    public AiProviderType Provider => AiProviderType.OpenAI;
    public string Model => "gpt-4o-mini";
    public double Temperature => 0.4;
    public int MaxTokens => 2048;
    public AssistantExecutionMode ExecutionMode => AssistantExecutionMode.Chat;
    public IReadOnlyList<string> EnabledToolNames { get; } = Array.Empty<string>();
    public IReadOnlyList<string> PersonaTargetSlugs { get; } = new[] { "anonymous" };
    public SafetyPreset? SafetyPresetHint => null;
}

// Skipped by scanner: abstract.
internal abstract class AbstractFixtureTemplate : IAiAgentTemplate
{
    public abstract string Slug { get; }
    public string DisplayName => "abstract";
    public string Description => "abstract";
    public string Category => "abstract";
    public string SystemPrompt => "abstract";
    public AiProviderType Provider => AiProviderType.Anthropic;
    public string Model => "x";
    public double Temperature => 0.5;
    public int MaxTokens => 1;
    public AssistantExecutionMode ExecutionMode => AssistantExecutionMode.Chat;
    public IReadOnlyList<string> EnabledToolNames { get; } = Array.Empty<string>();
    public IReadOnlyList<string> PersonaTargetSlugs { get; } = Array.Empty<string>();
    public SafetyPreset? SafetyPresetHint => null;
}

// Skipped by scanner: no parameterless ctor.
internal sealed class NoParameterlessCtorFixtureTemplate(string slugValue) : IAiAgentTemplate
{
    public string Slug { get; } = slugValue;
    public string DisplayName => "no-ctor";
    public string Description => "no-ctor";
    public string Category => "no-ctor";
    public string SystemPrompt => "no-ctor";
    public AiProviderType Provider => AiProviderType.Anthropic;
    public string Model => "x";
    public double Temperature => 0.5;
    public int MaxTokens => 1;
    public AssistantExecutionMode ExecutionMode => AssistantExecutionMode.Chat;
    public IReadOnlyList<string> EnabledToolNames { get; } = Array.Empty<string>();
    public IReadOnlyList<string> PersonaTargetSlugs { get; } = Array.Empty<string>();
    public SafetyPreset? SafetyPresetHint => null;
}

// Test-only mutable helper. NOT a discovery target — it's not picked up because
// it's not in an assembly we ever pass to AddAiAgentTemplatesFromAssembly.
// Used in handler/registry tests to construct ad-hoc templates with arbitrary fields.
internal sealed class TestTemplate(
    string slug = "test",
    string? displayName = null,
    string? systemPrompt = null,
    string? model = null,
    AiProviderType provider = AiProviderType.Anthropic,
    IReadOnlyList<string>? tools = null,
    IReadOnlyList<string>? personas = null,
    SafetyPreset? safetyHint = null) : IAiAgentTemplate
{
    public string Slug { get; } = slug;
    public string DisplayName { get; } = displayName ?? slug;
    public string Description => "test";
    public string Category => "TestCat";
    public string SystemPrompt { get; } = systemPrompt ?? "You are a test.";
    public AiProviderType Provider { get; } = provider;
    public string Model { get; } = model ?? "test-model";
    public double Temperature => 0.5;
    public int MaxTokens => 512;
    public AssistantExecutionMode ExecutionMode => AssistantExecutionMode.Chat;
    public IReadOnlyList<string> EnabledToolNames { get; } = tools ?? Array.Empty<string>();
    public IReadOnlyList<string> PersonaTargetSlugs { get; } = personas ?? Array.Empty<string>();
    public SafetyPreset? SafetyPresetHint { get; } = safetyHint;
}
```

- [ ] **Step 2: Write the failing discovery tests**

```csharp
// boilerplateBE/tests/Starter.Api.Tests/Ai/Templates/AiAgentTemplateDiscoveryTests.cs
using Microsoft.Extensions.DependencyInjection;
using Starter.Abstractions.Capabilities;
using Xunit;

namespace Starter.Api.Tests.Ai.Templates;

public class AiAgentTemplateDiscoveryTests
{
    [Fact]
    public void Scanner_registers_concrete_parameterless_templates()
    {
        var services = new ServiceCollection();
        services.AddAiAgentTemplatesFromAssembly(typeof(FixtureTemplateA).Assembly);

        var registered = services
            .BuildServiceProvider()
            .GetServices<IAiAgentTemplate>()
            .ToList();

        Assert.Contains(registered, t => t.Slug == "fixture_a");
        Assert.Contains(registered, t => t.Slug == "fixture_b");
    }

    [Fact]
    public void Scanner_skips_abstract_types()
    {
        var services = new ServiceCollection();
        services.AddAiAgentTemplatesFromAssembly(typeof(AbstractFixtureTemplate).Assembly);

        var registered = services
            .BuildServiceProvider()
            .GetServices<IAiAgentTemplate>()
            .ToList();

        Assert.DoesNotContain(registered, t => t.GetType().IsAbstract);
    }

    [Fact]
    public void Scanner_skips_types_without_parameterless_ctor()
    {
        var services = new ServiceCollection();
        services.AddAiAgentTemplatesFromAssembly(typeof(NoParameterlessCtorFixtureTemplate).Assembly);

        var registered = services
            .BuildServiceProvider()
            .GetServices<IAiAgentTemplate>()
            .ToList();

        Assert.DoesNotContain(
            registered,
            t => t.GetType().Name.Contains("NoParameterlessCtor", StringComparison.Ordinal));
    }

    [Fact]
    public void Scanner_decorates_registrations_with_module_source()
    {
        var services = new ServiceCollection();
        services.AddAiAgentTemplatesFromAssembly(typeof(FixtureTemplateA).Assembly);

        var registered = services
            .BuildServiceProvider()
            .GetServices<IAiAgentTemplate>()
            .ToList();

        var a = registered.First(t => t.Slug == "fixture_a");
        var src = Assert.IsAssignableFrom<IAiAgentTemplateModuleSource>(a);
        // The test assembly's name is "Starter.Api.Tests" — DeriveModuleSource
        // strips "Starter." prefix to "Api.Tests".
        Assert.Equal("Api.Tests", src.ModuleSource);
    }

    [Theory]
    [InlineData("Starter.Application", "Core")]
    [InlineData("Starter.Module.Products", "Products")]
    [InlineData("Starter.Module.AI", "AI")]
    [InlineData("Starter.Foo", "Foo")]
    [InlineData("Other.Library", "Other.Library")]
    public void DeriveModuleSource_strips_well_known_prefixes(string assemblyName, string expected)
    {
        Assert.Equal(
            expected,
            AiAgentTemplateDiscoveryExtensions.DeriveModuleSource(assemblyName));
    }
}
```

- [ ] **Step 3: Run discovery tests to verify they fail**

```bash
dotnet test boilerplateBE/Starter.sln --filter "AiAgentTemplateDiscoveryTests" 2>&1 | tail -5
```

Expected: FAIL — "AddAiAgentTemplatesFromAssembly" not found.

- [ ] **Step 4: Implement `AiAgentTemplateDiscoveryExtensions`**

```csharp
// boilerplateBE/src/Starter.Abstractions/Capabilities/AiAgentTemplateDiscoveryExtensions.cs
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace Starter.Abstractions.Capabilities;

public static class AiAgentTemplateDiscoveryExtensions
{
    public static IServiceCollection AddAiAgentTemplatesFromAssembly(
        this IServiceCollection services, Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(assembly);

        var moduleSource = DeriveModuleSource(assembly.GetName().Name ?? "Unknown");

        var templateTypes = assembly.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false, IsGenericTypeDefinition: false })
            .Where(t => typeof(IAiAgentTemplate).IsAssignableFrom(t))
            .Where(t => t.GetConstructor(Type.EmptyTypes) is not null);

        foreach (var type in templateTypes)
        {
            var instance = (IAiAgentTemplate)Activator.CreateInstance(type)!;
            ValidateShape(instance);
            services.AddSingleton<IAiAgentTemplate>(
                new AiAgentTemplateRegistration(instance, moduleSource));
        }

        return services;
    }

    internal static string DeriveModuleSource(string assemblyName)
    {
        if (assemblyName.StartsWith("Starter.Module.", StringComparison.Ordinal))
            return assemblyName["Starter.Module.".Length..];
        if (string.Equals(assemblyName, "Starter.Application", StringComparison.Ordinal))
            return "Core";
        if (assemblyName.StartsWith("Starter.", StringComparison.Ordinal))
            return assemblyName["Starter.".Length..];
        return assemblyName;
    }

    internal static void ValidateShape(IAiAgentTemplate template)
    {
        ArgumentNullException.ThrowIfNull(template);
        var typeName = template.GetType().FullName ?? template.GetType().Name;

        if (string.IsNullOrWhiteSpace(template.Slug))
            throw new InvalidOperationException(
                $"Template {typeName} has empty Slug.");
        if (template.Slug.Length > 128)
            throw new InvalidOperationException(
                $"Template {typeName} Slug exceeds 128 characters.");
        if (string.IsNullOrWhiteSpace(template.DisplayName))
            throw new InvalidOperationException(
                $"Template {typeName} has empty DisplayName.");
        if (string.IsNullOrWhiteSpace(template.Description))
            throw new InvalidOperationException(
                $"Template {typeName} has empty Description.");
        if (string.IsNullOrWhiteSpace(template.Category))
            throw new InvalidOperationException(
                $"Template {typeName} has empty Category.");
        if (string.IsNullOrWhiteSpace(template.SystemPrompt))
            throw new InvalidOperationException(
                $"Template {typeName} has empty SystemPrompt.");
        if (string.IsNullOrWhiteSpace(template.Model))
            throw new InvalidOperationException(
                $"Template {typeName} has empty Model.");
        if (template.Temperature is < 0.0 or > 2.0)
            throw new InvalidOperationException(
                $"Template {typeName} Temperature {template.Temperature} out of [0.0, 2.0].");
        if (template.MaxTokens < 1)
            throw new InvalidOperationException(
                $"Template {typeName} MaxTokens must be ≥ 1.");
        if (template.EnabledToolNames is null)
            throw new InvalidOperationException(
                $"Template {typeName} EnabledToolNames is null (use empty list instead).");
        if (template.PersonaTargetSlugs is null)
            throw new InvalidOperationException(
                $"Template {typeName} PersonaTargetSlugs is null (use empty list instead).");
    }
}
```

- [ ] **Step 5: Run discovery tests to verify they pass**

```bash
dotnet test boilerplateBE/Starter.sln --filter "AiAgentTemplateDiscoveryTests" 2>&1 | tail -5
```

Expected: PASS (5 tests).

- [ ] **Step 6: Add `ValidateShape` failure-mode tests**

Append to `AiAgentTemplateDiscoveryTests.cs`:

```csharp
public class AiAgentTemplateValidateShapeTests
{
    [Fact]
    public void ValidateShape_rejects_empty_slug()
    {
        var t = new TestTemplate(slug: "");
        var ex = Assert.Throws<InvalidOperationException>(
            () => AiAgentTemplateDiscoveryExtensions.ValidateShape(t));
        Assert.Contains("Slug", ex.Message);
    }

    [Fact]
    public void ValidateShape_rejects_too_long_slug()
    {
        var t = new TestTemplate(slug: new string('a', 129));
        var ex = Assert.Throws<InvalidOperationException>(
            () => AiAgentTemplateDiscoveryExtensions.ValidateShape(t));
        Assert.Contains("128", ex.Message);
    }

    [Fact]
    public void ValidateShape_rejects_empty_system_prompt()
    {
        var t = new TestTemplate(systemPrompt: "");
        var ex = Assert.Throws<InvalidOperationException>(
            () => AiAgentTemplateDiscoveryExtensions.ValidateShape(t));
        Assert.Contains("SystemPrompt", ex.Message);
    }

    [Fact]
    public void ValidateShape_rejects_empty_model()
    {
        var t = new TestTemplate(model: "");
        var ex = Assert.Throws<InvalidOperationException>(
            () => AiAgentTemplateDiscoveryExtensions.ValidateShape(t));
        Assert.Contains("Model", ex.Message);
    }

    [Fact]
    public void ValidateShape_accepts_well_formed_template()
    {
        var t = new TestTemplate();
        AiAgentTemplateDiscoveryExtensions.ValidateShape(t);
        // No throw = pass.
    }
}
```

- [ ] **Step 7: Run all template tests**

```bash
dotnet test boilerplateBE/Starter.sln --filter "FullyQualifiedName~Ai.Templates" 2>&1 | tail -5
```

Expected: all green.

- [ ] **Step 8: Commit**

```bash
git add boilerplateBE/src/Starter.Abstractions/Capabilities/AiAgentTemplateDiscoveryExtensions.cs \
        boilerplateBE/tests/Starter.Api.Tests/Ai/Templates/AiAgentTemplateDiscoveryFixtures.cs \
        boilerplateBE/tests/Starter.Api.Tests/Ai/Templates/AiAgentTemplateDiscoveryTests.cs
git commit -m "feat(ai): AddAiAgentTemplatesFromAssembly scanner + ValidateShape"
```

---

## Task 4: `IAiAgentTemplateRegistry` + impl with collision fail-fast

**Files:**
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Application/Services/IAiAgentTemplateRegistry.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Application/Services/AiAgentTemplateRegistry.cs`
- Create: `boilerplateBE/tests/Starter.Api.Tests/Ai/Templates/AiAgentTemplateRegistryTests.cs`

- [ ] **Step 1: Write the failing tests**

```csharp
// boilerplateBE/tests/Starter.Api.Tests/Ai/Templates/AiAgentTemplateRegistryTests.cs
using Starter.Abstractions.Capabilities;
using Starter.Module.AI.Application.Services;
using Xunit;

namespace Starter.Api.Tests.Ai.Templates;

public class AiAgentTemplateRegistryTests
{
    [Fact]
    public void GetAll_returns_all_templates_sorted_by_category_then_slug()
    {
        var registry = new AiAgentTemplateRegistry(new IAiAgentTemplate[]
        {
            new TestTemplate(slug: "z_template"),
            new TestTemplate(slug: "a_template"),
            new TestTemplate(slug: "m_template"),
        });

        var all = registry.GetAll().Select(t => t.Slug).ToList();

        Assert.Equal(new[] { "a_template", "m_template", "z_template" }, all);
    }

    [Fact]
    public void Find_returns_matching_template()
    {
        var t = new TestTemplate(slug: "found");
        var registry = new AiAgentTemplateRegistry(new IAiAgentTemplate[] { t });

        Assert.Same(t, registry.Find("found"));
    }

    [Fact]
    public void Find_returns_null_for_unknown_slug()
    {
        var registry = new AiAgentTemplateRegistry(new IAiAgentTemplate[]
        {
            new TestTemplate(slug: "x"),
        });

        Assert.Null(registry.Find("missing"));
    }

    [Fact]
    public void Constructor_throws_on_duplicate_slug_naming_both_types()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => new AiAgentTemplateRegistry(
            new IAiAgentTemplate[]
            {
                new FixtureTemplateA(),
                new TestTemplate(slug: "fixture_a"),  // collides with FixtureTemplateA
            }));

        Assert.Contains("fixture_a", ex.Message);
        Assert.Contains(nameof(FixtureTemplateA), ex.Message);
        Assert.Contains(nameof(TestTemplate), ex.Message);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

```bash
dotnet test boilerplateBE/Starter.sln --filter "AiAgentTemplateRegistryTests" 2>&1 | tail -5
```

Expected: FAIL — `AiAgentTemplateRegistry` not found.

- [ ] **Step 3: Implement the interface + registry**

```csharp
// boilerplateBE/src/modules/Starter.Module.AI/Application/Services/IAiAgentTemplateRegistry.cs
using Starter.Abstractions.Capabilities;

namespace Starter.Module.AI.Application.Services;

public interface IAiAgentTemplateRegistry
{
    IReadOnlyCollection<IAiAgentTemplate> GetAll();
    IAiAgentTemplate? Find(string slug);
}
```

```csharp
// boilerplateBE/src/modules/Starter.Module.AI/Application/Services/AiAgentTemplateRegistry.cs
using Starter.Abstractions.Capabilities;

namespace Starter.Module.AI.Application.Services;

internal sealed class AiAgentTemplateRegistry : IAiAgentTemplateRegistry
{
    private readonly IReadOnlyList<IAiAgentTemplate> _ordered;
    private readonly IReadOnlyDictionary<string, IAiAgentTemplate> _bySlug;

    public AiAgentTemplateRegistry(IEnumerable<IAiAgentTemplate> templates)
    {
        _bySlug = BuildDictionary(templates);
        _ordered = _bySlug.Values
            .OrderBy(t => t.Category, StringComparer.Ordinal)
            .ThenBy(t => t.Slug, StringComparer.Ordinal)
            .ToList();
    }

    public IReadOnlyCollection<IAiAgentTemplate> GetAll() => _ordered;

    public IAiAgentTemplate? Find(string slug) =>
        _bySlug.TryGetValue(slug, out var t) ? t : null;

    private static Dictionary<string, IAiAgentTemplate> BuildDictionary(
        IEnumerable<IAiAgentTemplate> templates)
    {
        var dict = new Dictionary<string, IAiAgentTemplate>(StringComparer.Ordinal);
        foreach (var t in templates)
        {
            if (!dict.TryAdd(t.Slug, t))
            {
                throw new InvalidOperationException(
                    $"Duplicate AI agent template slug '{t.Slug}': "
                    + $"both {dict[t.Slug].GetType().Name} and {t.GetType().Name} "
                    + "claim the same slug.");
            }
        }
        return dict;
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

```bash
dotnet test boilerplateBE/Starter.sln --filter "AiAgentTemplateRegistryTests" 2>&1 | tail -5
```

Expected: PASS (4 tests).

- [ ] **Step 5: Commit**

```bash
git add boilerplateBE/src/modules/Starter.Module.AI/Application/Services/IAiAgentTemplateRegistry.cs \
        boilerplateBE/src/modules/Starter.Module.AI/Application/Services/AiAgentTemplateRegistry.cs \
        boilerplateBE/tests/Starter.Api.Tests/Ai/Templates/AiAgentTemplateRegistryTests.cs
git commit -m "feat(ai): IAiAgentTemplateRegistry with fail-fast collision detection"
```

---

## Task 5: `AiAssistant` schema delta — `TemplateSourceSlug` + `TemplateSourceVersion`

**Files:**
- Modify: `boilerplateBE/src/modules/Starter.Module.AI/Domain/Entities/AiAssistant.cs`
- Modify: `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Configurations/AiAssistantConfiguration.cs`
- Modify: `boilerplateBE/src/modules/Starter.Module.AI/Application/DTOs/AiAssistantDto.cs`
- Create: `boilerplateBE/tests/Starter.Api.Tests/Ai/Templates/AiAssistantTemplateSourceTests.cs`

- [ ] **Step 1: Write the failing entity test**

```csharp
// boilerplateBE/tests/Starter.Api.Tests/Ai/Templates/AiAssistantTemplateSourceTests.cs
using Starter.Abstractions.Ai;
using Starter.Module.AI.Domain.Entities;
using Xunit;

namespace Starter.Api.Tests.Ai.Templates;

public class AiAssistantTemplateSourceTests
{
    [Fact]
    public void New_assistant_has_null_template_source_fields_by_default()
    {
        var a = AiAssistant.Create(
            tenantId: Guid.NewGuid(),
            name: "X",
            description: null,
            systemPrompt: "You are X.",
            createdByUserId: Guid.NewGuid());

        Assert.Null(a.TemplateSourceSlug);
        Assert.Null(a.TemplateSourceVersion);
    }

    [Fact]
    public void StampTemplateSource_sets_slug_and_version()
    {
        var a = AiAssistant.Create(
            tenantId: Guid.NewGuid(),
            name: "X",
            description: null,
            systemPrompt: "You are X.",
            createdByUserId: Guid.NewGuid());

        a.StampTemplateSource("support_assistant_anthropic", version: null);

        Assert.Equal("support_assistant_anthropic", a.TemplateSourceSlug);
        Assert.Null(a.TemplateSourceVersion);
    }

    [Fact]
    public void StampTemplateSource_accepts_version_when_provided()
    {
        var a = AiAssistant.Create(
            tenantId: Guid.NewGuid(),
            name: "X",
            description: null,
            systemPrompt: "You are X.",
            createdByUserId: Guid.NewGuid());

        a.StampTemplateSource("support_assistant_anthropic", version: "v1");

        Assert.Equal("v1", a.TemplateSourceVersion);
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

```bash
dotnet test boilerplateBE/Starter.sln --filter "AiAssistantTemplateSourceTests" 2>&1 | tail -5
```

Expected: FAIL — `TemplateSourceSlug` not found.

- [ ] **Step 3: Add the columns + method to `AiAssistant.cs`**

Find the property block (around lines 50-85) and append two properties. Find the methods section (after `SetVisibility`/`SetActive`) and append `StampTemplateSource`.

```csharp
// In AiAssistant.cs — properties section
public string? TemplateSourceSlug { get; private set; }
public string? TemplateSourceVersion { get; private set; }

// In AiAssistant.cs — methods section
public void StampTemplateSource(string templateSlug, string? version = null)
{
    if (string.IsNullOrWhiteSpace(templateSlug))
        throw new ArgumentException("Template slug must be non-empty.", nameof(templateSlug));
    TemplateSourceSlug = templateSlug;
    TemplateSourceVersion = version;
}
```

- [ ] **Step 4: Update `AiAssistantConfiguration.cs`**

Find the section where other string properties are configured (e.g., `Slug`, `Model`). Add:

```csharp
builder.Property(e => e.TemplateSourceSlug)
    .HasColumnName("template_source_slug")
    .HasMaxLength(128);

builder.Property(e => e.TemplateSourceVersion)
    .HasColumnName("template_source_version")
    .HasMaxLength(32);
```

- [ ] **Step 5: Update `AiAssistantDto.cs` — append two nullable fields with defaults**

The DTO is a positional record. Adding params at the end with default values keeps existing call sites compiling:

```csharp
// boilerplateBE/src/modules/Starter.Module.AI/Application/DTOs/AiAssistantDto.cs
public sealed record AiAssistantDto(
    Guid Id,
    string Name,
    string? Description,
    string SystemPrompt,
    AiProviderType? Provider,
    string? Model,
    double Temperature,
    int MaxTokens,
    IReadOnlyList<string> EnabledToolNames,
    IReadOnlyList<Guid> KnowledgeBaseDocIds,
    AssistantExecutionMode ExecutionMode,
    int MaxAgentSteps,
    bool IsActive,
    DateTime CreatedAt,
    DateTime? ModifiedAt,
    AiRagScope RagScope,
    ResourceVisibility Visibility,
    AssistantAccessMode AccessMode,
    Guid CreatedByUserId,
    string Slug,
    IReadOnlyList<string> PersonaTargetSlugs,
    string? TemplateSourceSlug = null,        // NEW
    string? TemplateSourceVersion = null);    // NEW
```

- [ ] **Step 6: Update existing assistant mappers to populate the new fields**

Find every place that constructs `new AiAssistantDto(...)`. Use:

```bash
grep -rn "new AiAssistantDto" boilerplateBE/src/modules/Starter.Module.AI \
  boilerplateBE/tests/Starter.Api.Tests
```

For each call site, append the two new arguments mapped from the entity:

```csharp
// at the end of the AiAssistantDto constructor call
TemplateSourceSlug: assistant.TemplateSourceSlug,
TemplateSourceVersion: assistant.TemplateSourceVersion);
```

(If a call site doesn't have an `assistant` variable in scope, pass `null, null` to preserve behaviour — those sites don't surface installed-template assistants.)

- [ ] **Step 7: Run entity tests**

```bash
dotnet test boilerplateBE/Starter.sln --filter "AiAssistantTemplateSourceTests" 2>&1 | tail -5
```

Expected: PASS (3 tests).

- [ ] **Step 8: Run full AI test suite to confirm no regression**

```bash
dotnet build boilerplateBE/Starter.sln -c Debug 2>&1 | tail -5
dotnet test boilerplateBE/Starter.sln --filter "FullyQualifiedName~Ai" 2>&1 | tail -5
```

Expected: clean build, all green.

- [ ] **Step 9: Commit**

```bash
git add boilerplateBE/src/modules/Starter.Module.AI/Domain/Entities/AiAssistant.cs \
        boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Configurations/AiAssistantConfiguration.cs \
        boilerplateBE/src/modules/Starter.Module.AI/Application/DTOs/AiAssistantDto.cs \
        boilerplateBE/tests/Starter.Api.Tests/Ai/Templates/AiAssistantTemplateSourceTests.cs
git commit -m "feat(ai): AiAssistant.TemplateSourceSlug + TemplateSourceVersion + StampTemplateSource"
```

(If grep in Step 6 found additional mapper files, include them in `git add` here.)

---

## Task 6: `AiAgentTemplateDto` + `GetTemplatesQuery` + handler + mapper

**Files:**
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Application/DTOs/AiAgentTemplateDto.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Application/DTOs/AiAgentTemplateMappers.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Application/Queries/GetTemplates/GetTemplatesQuery.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Application/Queries/GetTemplates/GetTemplatesQueryHandler.cs`
- Create: `boilerplateBE/tests/Starter.Api.Tests/Ai/Templates/AiAgentTemplateMappersTests.cs`
- Create: `boilerplateBE/tests/Starter.Api.Tests/Ai/Templates/GetTemplatesQueryHandlerTests.cs`

- [ ] **Step 1: Write failing mapper tests**

```csharp
// boilerplateBE/tests/Starter.Api.Tests/Ai/Templates/AiAgentTemplateMappersTests.cs
using Starter.Abstractions.Ai;
using Starter.Abstractions.Capabilities;
using Starter.Module.AI.Application.DTOs;
using Xunit;

namespace Starter.Api.Tests.Ai.Templates;

public class AiAgentTemplateMappersTests
{
    [Fact]
    public void ToDto_maps_all_fields()
    {
        var template = new TestTemplate(
            slug: "x_slug",
            displayName: "X Display",
            systemPrompt: "X prompt",
            provider: AiProviderType.OpenAI,
            model: "gpt-4o-mini",
            tools: new[] { "tool_a" },
            personas: new[] { "default", "anonymous" },
            safetyHint: SafetyPreset.ChildSafe);

        var reg = new AiAgentTemplateRegistration(template, "Products");
        var dto = ((IAiAgentTemplate)reg).ToDto();

        Assert.Equal("x_slug", dto.Slug);
        Assert.Equal("X Display", dto.DisplayName);
        Assert.Equal("Products", dto.Module);
        Assert.Equal("OpenAI", dto.Provider);
        Assert.Equal("gpt-4o-mini", dto.Model);
        Assert.Equal(new[] { "tool_a" }, dto.EnabledToolNames);
        Assert.Equal(new[] { "default", "anonymous" }, dto.PersonaTargetSlugs);
        Assert.Equal("ChildSafe", dto.SafetyPresetHint);
    }

    [Fact]
    public void ToDto_uses_unknown_module_when_template_lacks_capability()
    {
        var template = new TestTemplate(slug: "no_module");
        var dto = template.ToDto();

        Assert.Equal("Unknown", dto.Module);
    }

    [Fact]
    public void ToDto_serialises_null_safety_hint_as_null()
    {
        var template = new TestTemplate(slug: "no_safety", safetyHint: null);
        var dto = template.ToDto();

        Assert.Null(dto.SafetyPresetHint);
    }
}
```

(Note the test uses `((IAiAgentTemplate)reg).ToDto()` — `AiAgentTemplateRegistration` is `internal`, but `Starter.Api.Tests` has `InternalsVisibleTo` access.)

- [ ] **Step 2: Run mapper tests to verify they fail**

```bash
dotnet test boilerplateBE/Starter.sln --filter "AiAgentTemplateMappersTests" 2>&1 | tail -5
```

Expected: FAIL — `AiAgentTemplateDto` / `ToDto` not found.

- [ ] **Step 3: Create the DTO**

```csharp
// boilerplateBE/src/modules/Starter.Module.AI/Application/DTOs/AiAgentTemplateDto.cs
namespace Starter.Module.AI.Application.DTOs;

public sealed record AiAgentTemplateDto(
    string Slug,
    string DisplayName,
    string Description,
    string Category,
    string Module,
    string Provider,
    string Model,
    double Temperature,
    int MaxTokens,
    string ExecutionMode,
    IReadOnlyList<string> EnabledToolNames,
    IReadOnlyList<string> PersonaTargetSlugs,
    string? SafetyPresetHint);
```

- [ ] **Step 4: Create the mapper**

```csharp
// boilerplateBE/src/modules/Starter.Module.AI/Application/DTOs/AiAgentTemplateMappers.cs
using Starter.Abstractions.Capabilities;

namespace Starter.Module.AI.Application.DTOs;

internal static class AiAgentTemplateMappers
{
    public static AiAgentTemplateDto ToDto(this IAiAgentTemplate template) => new(
        Slug: template.Slug,
        DisplayName: template.DisplayName,
        Description: template.Description,
        Category: template.Category,
        Module: ModuleOf(template),
        Provider: template.Provider.ToString(),
        Model: template.Model,
        Temperature: template.Temperature,
        MaxTokens: template.MaxTokens,
        ExecutionMode: template.ExecutionMode.ToString(),
        EnabledToolNames: template.EnabledToolNames,
        PersonaTargetSlugs: template.PersonaTargetSlugs,
        SafetyPresetHint: template.SafetyPresetHint?.ToString());

    private static string ModuleOf(IAiAgentTemplate template) =>
        template is IAiAgentTemplateModuleSource src ? src.ModuleSource : "Unknown";
}
```

- [ ] **Step 5: Run mapper tests to verify they pass**

```bash
dotnet test boilerplateBE/Starter.sln --filter "AiAgentTemplateMappersTests" 2>&1 | tail -5
```

Expected: PASS (3 tests).

- [ ] **Step 6: Write failing query handler tests**

```csharp
// boilerplateBE/tests/Starter.Api.Tests/Ai/Templates/GetTemplatesQueryHandlerTests.cs
using Moq;
using Starter.Abstractions.Capabilities;
using Starter.Module.AI.Application.Queries.GetTemplates;
using Starter.Module.AI.Application.Services;
using Xunit;

namespace Starter.Api.Tests.Ai.Templates;

public class GetTemplatesQueryHandlerTests
{
    [Fact]
    public async Task Handler_returns_all_templates_ordered_with_module_field()
    {
        var t1 = new AiAgentTemplateRegistration(new TestTemplate(slug: "z"), "Products");
        var t2 = new AiAgentTemplateRegistration(new TestTemplate(slug: "a"), "Core");
        var registry = new AiAgentTemplateRegistry(new IAiAgentTemplate[] { t1, t2 });

        var handler = new GetTemplatesQueryHandler(registry);
        var result = await handler.Handle(new GetTemplatesQuery(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var list = result.Value!;
        // TestTemplate.Category is "TestCat" for both — ordered by slug
        Assert.Equal(new[] { "a", "z" }, list.Select(d => d.Slug));
        Assert.Equal("Core", list.First(d => d.Slug == "a").Module);
        Assert.Equal("Products", list.First(d => d.Slug == "z").Module);
    }

    [Fact]
    public async Task Handler_returns_empty_when_no_templates_registered()
    {
        var registry = new AiAgentTemplateRegistry(Array.Empty<IAiAgentTemplate>());
        var handler = new GetTemplatesQueryHandler(registry);

        var result = await handler.Handle(new GetTemplatesQuery(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value!);
    }
}
```

- [ ] **Step 7: Run handler tests to verify they fail**

```bash
dotnet test boilerplateBE/Starter.sln --filter "GetTemplatesQueryHandlerTests" 2>&1 | tail -5
```

Expected: FAIL — `GetTemplatesQuery` not found.

- [ ] **Step 8: Implement the query + handler**

```csharp
// boilerplateBE/src/modules/Starter.Module.AI/Application/Queries/GetTemplates/GetTemplatesQuery.cs
using MediatR;
using Starter.Module.AI.Application.DTOs;
using Starter.Shared.Results;

namespace Starter.Module.AI.Application.Queries.GetTemplates;

public sealed record GetTemplatesQuery : IRequest<Result<IReadOnlyList<AiAgentTemplateDto>>>;
```

```csharp
// boilerplateBE/src/modules/Starter.Module.AI/Application/Queries/GetTemplates/GetTemplatesQueryHandler.cs
using MediatR;
using Starter.Module.AI.Application.DTOs;
using Starter.Module.AI.Application.Services;
using Starter.Shared.Results;

namespace Starter.Module.AI.Application.Queries.GetTemplates;

internal sealed class GetTemplatesQueryHandler(IAiAgentTemplateRegistry registry)
    : IRequestHandler<GetTemplatesQuery, Result<IReadOnlyList<AiAgentTemplateDto>>>
{
    public Task<Result<IReadOnlyList<AiAgentTemplateDto>>> Handle(
        GetTemplatesQuery request, CancellationToken cancellationToken)
    {
        IReadOnlyList<AiAgentTemplateDto> dtos = registry
            .GetAll()
            .Select(t => t.ToDto())
            .ToList();
        return Task.FromResult(Result<IReadOnlyList<AiAgentTemplateDto>>.Success(dtos));
    }
}
```

- [ ] **Step 9: Run handler tests to verify they pass**

```bash
dotnet test boilerplateBE/Starter.sln --filter "GetTemplatesQueryHandlerTests" 2>&1 | tail -5
```

Expected: PASS (2 tests).

- [ ] **Step 10: Commit**

```bash
git add boilerplateBE/src/modules/Starter.Module.AI/Application/DTOs/AiAgentTemplateDto.cs \
        boilerplateBE/src/modules/Starter.Module.AI/Application/DTOs/AiAgentTemplateMappers.cs \
        boilerplateBE/src/modules/Starter.Module.AI/Application/Queries/GetTemplates \
        boilerplateBE/tests/Starter.Api.Tests/Ai/Templates/AiAgentTemplateMappersTests.cs \
        boilerplateBE/tests/Starter.Api.Tests/Ai/Templates/GetTemplatesQueryHandlerTests.cs
git commit -m "feat(ai): AiAgentTemplateDto + mapper + GetTemplatesQuery handler"
```

---

## Task 7: `InstallTemplateCommand` record + validator

**Files:**
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Application/Commands/InstallTemplate/InstallTemplateCommand.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Application/Commands/InstallTemplate/InstallTemplateCommandValidator.cs`
- Create: `boilerplateBE/tests/Starter.Api.Tests/Ai/Templates/InstallTemplateCommandValidatorTests.cs`

- [ ] **Step 1: Write the failing validator tests**

```csharp
// boilerplateBE/tests/Starter.Api.Tests/Ai/Templates/InstallTemplateCommandValidatorTests.cs
using FluentValidation.TestHelper;
using Starter.Module.AI.Application.Commands.InstallTemplate;
using Xunit;

namespace Starter.Api.Tests.Ai.Templates;

public class InstallTemplateCommandValidatorTests
{
    private readonly InstallTemplateCommandValidator _v = new();

    [Fact]
    public void Empty_slug_fails()
    {
        var result = _v.TestValidate(new InstallTemplateCommand(""));
        result.ShouldHaveValidationErrorFor(c => c.TemplateSlug);
    }

    [Fact]
    public void Whitespace_slug_fails()
    {
        var result = _v.TestValidate(new InstallTemplateCommand("   "));
        result.ShouldHaveValidationErrorFor(c => c.TemplateSlug);
    }

    [Fact]
    public void Slug_over_128_chars_fails()
    {
        var result = _v.TestValidate(new InstallTemplateCommand(new string('a', 129)));
        result.ShouldHaveValidationErrorFor(c => c.TemplateSlug);
    }

    [Fact]
    public void Valid_slug_passes()
    {
        var result = _v.TestValidate(new InstallTemplateCommand("support_assistant_anthropic"));
        result.ShouldNotHaveValidationErrorFor(c => c.TemplateSlug);
    }

    [Fact]
    public void Valid_slug_with_target_tenant_passes()
    {
        var result = _v.TestValidate(
            new InstallTemplateCommand("support_assistant_anthropic", TargetTenantId: Guid.NewGuid()));
        result.ShouldNotHaveAnyValidationErrors();
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

```bash
dotnet test boilerplateBE/Starter.sln --filter "InstallTemplateCommandValidatorTests" 2>&1 | tail -5
```

Expected: FAIL — `InstallTemplateCommand` not found.

- [ ] **Step 3: Implement the command + validator**

```csharp
// boilerplateBE/src/modules/Starter.Module.AI/Application/Commands/InstallTemplate/InstallTemplateCommand.cs
using MediatR;
using Starter.Shared.Results;

namespace Starter.Module.AI.Application.Commands.InstallTemplate;

public sealed record InstallTemplateCommand(
    string TemplateSlug,
    Guid? TargetTenantId = null,
    Guid? CreatedByUserIdOverride = null) : IRequest<Result<Guid>>;
```

```csharp
// boilerplateBE/src/modules/Starter.Module.AI/Application/Commands/InstallTemplate/InstallTemplateCommandValidator.cs
using FluentValidation;

namespace Starter.Module.AI.Application.Commands.InstallTemplate;

public sealed class InstallTemplateCommandValidator : AbstractValidator<InstallTemplateCommand>
{
    public InstallTemplateCommandValidator()
    {
        RuleFor(c => c.TemplateSlug)
            .NotEmpty()
            .MaximumLength(128);
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

```bash
dotnet test boilerplateBE/Starter.sln --filter "InstallTemplateCommandValidatorTests" 2>&1 | tail -5
```

Expected: PASS (5 tests).

- [ ] **Step 5: Commit**

```bash
git add boilerplateBE/src/modules/Starter.Module.AI/Application/Commands/InstallTemplate \
        boilerplateBE/tests/Starter.Api.Tests/Ai/Templates/InstallTemplateCommandValidatorTests.cs
git commit -m "feat(ai): InstallTemplateCommand record + validator"
```

---

## Task 8: `TemplateErrors` + `InstallTemplateCommandHandler`

**Files:**
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Domain/Errors/TemplateErrors.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Application/Commands/InstallTemplate/InstallTemplateCommandHandler.cs`
- Create: `boilerplateBE/tests/Starter.Api.Tests/Ai/Templates/InstallTemplateCommandHandlerTests.cs`

This is the largest task. It implements the full install pipeline: tenant resolution → template lookup → duplicate guard → persona/tool validation → entity creation → provenance stamp → persist.

- [ ] **Step 1: Write the failing handler tests (test setup helper first)**

```csharp
// boilerplateBE/tests/Starter.Api.Tests/Ai/Templates/InstallTemplateCommandHandlerTests.cs
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Moq;
using Starter.Abstractions.Ai;
using Starter.Abstractions.Capabilities;
using Starter.Application.Common.Interfaces;
using Starter.Module.AI.Application.Commands.InstallTemplate;
using Starter.Module.AI.Application.Services;
using Starter.Module.AI.Domain.Entities;
using Starter.Module.AI.Domain.Enums;
using Starter.Module.AI.Domain.Errors;
using Starter.Module.AI.Infrastructure.Persistence;
using Starter.Shared.Constants;
using Xunit;

namespace Starter.Api.Tests.Ai.Templates;

public class InstallTemplateCommandHandlerTests
{
    private static (
        InstallTemplateCommandHandler handler,
        AiDbContext db,
        Mock<ICurrentUserService> currentUser,
        Mock<IAiToolRegistry> tools)
        Setup(
            Guid? callerTenantId,
            IEnumerable<IAiAgentTemplate>? templates = null,
            IEnumerable<string>? toolSlugs = null,
            bool callerIsSuperAdmin = false)
    {
        var cu = new Mock<ICurrentUserService>();
        cu.SetupGet(x => x.UserId).Returns(Guid.NewGuid());
        cu.SetupGet(x => x.TenantId).Returns(callerTenantId);
        cu.Setup(x => x.IsInRole(Roles.SuperAdmin)).Returns(callerIsSuperAdmin);

        var dbOpts = new DbContextOptionsBuilder<AiDbContext>()
            .UseInMemoryDatabase($"install-{Guid.NewGuid()}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        var db = new AiDbContext(dbOpts, cu.Object);

        var registry = new AiAgentTemplateRegistry(templates ?? Array.Empty<IAiAgentTemplate>());

        var toolReg = new Mock<IAiToolRegistry>();
        var toolDefs = (toolSlugs ?? Array.Empty<string>())
            .Select(s =>
            {
                var def = new Mock<IAiToolDefinition>();
                def.SetupGet(x => x.Name).Returns(s);
                return def.Object;
            })
            .ToList();
        toolReg.Setup(x => x.GetAll()).Returns(toolDefs);

        var handler = new InstallTemplateCommandHandler(
            db, registry, toolReg.Object, cu.Object);

        return (handler, db, cu, toolReg);
    }

    private static IAiAgentTemplate MakeTemplate(
        string slug = "my_template",
        IReadOnlyList<string>? tools = null,
        IReadOnlyList<string>? personas = null) =>
        new TestTemplate(
            slug: slug,
            tools: tools ?? Array.Empty<string>(),
            personas: personas ?? new[] { "default" });

    private static AiPersona MakeDefaultPersona(Guid tenantId) =>
        AiPersona.CreateDefault(tenantId);
}
```

(NOTE: `IAiToolRegistry`, `Roles.SuperAdmin`, and `AiPersona.CreateDefault` are existing types. If signatures differ, adjust.)

- [ ] **Step 2: Add the happy-path test**

Append to the test class:

```csharp
[Fact]
public async Task Happy_path_creates_assistant_with_provenance_stamped()
{
    var tenantId = Guid.NewGuid();
    var template = MakeTemplate(slug: "x", tools: new[] { "list_users" }, personas: new[] { "default" });
    var (handler, db, _, _) = Setup(
        callerTenantId: tenantId,
        templates: new[] { template },
        toolSlugs: new[] { "list_users" });

    db.AiPersonas.Add(MakeDefaultPersona(tenantId));
    await db.SaveChangesAsync();

    var result = await handler.Handle(
        new InstallTemplateCommand("x"), CancellationToken.None);

    result.IsSuccess.Should().BeTrue();
    var assistant = await db.AiAssistants.SingleAsync();
    assistant.Slug.Should().Be("x");
    assistant.TenantId.Should().Be(tenantId);
    assistant.TemplateSourceSlug.Should().Be("x");
    assistant.TemplateSourceVersion.Should().BeNull();
    assistant.EnabledToolNames.Should().Equal(new[] { "list_users" });
    assistant.PersonaTargetSlugs.Should().Equal(new[] { "default" });
}
```

- [ ] **Step 3: Add error-case tests (10 in total)**

```csharp
[Fact]
public async Task Returns_NotFound_when_template_slug_unknown()
{
    var tenantId = Guid.NewGuid();
    var (handler, _, _, _) = Setup(callerTenantId: tenantId);

    var result = await handler.Handle(
        new InstallTemplateCommand("does_not_exist"), CancellationToken.None);

    result.IsFailure.Should().BeTrue();
    result.Error.Code.Should().Be(TemplateErrors.NotFound("x").Code);
}

[Fact]
public async Task Returns_AlreadyInstalled_on_second_install_into_same_tenant()
{
    var tenantId = Guid.NewGuid();
    var template = MakeTemplate(slug: "x", personas: new[] { "default" });
    var (handler, db, _, _) = Setup(
        callerTenantId: tenantId,
        templates: new[] { template });

    db.AiPersonas.Add(MakeDefaultPersona(tenantId));
    await db.SaveChangesAsync();

    var first = await handler.Handle(new InstallTemplateCommand("x"), CancellationToken.None);
    first.IsSuccess.Should().BeTrue();

    var second = await handler.Handle(new InstallTemplateCommand("x"), CancellationToken.None);

    second.IsFailure.Should().BeTrue();
    second.Error.Code.Should().Be(TemplateErrors.AlreadyInstalled("x", tenantId).Code);
}

[Fact]
public async Task Returns_PersonaTargetMissing_when_persona_slug_does_not_exist()
{
    var tenantId = Guid.NewGuid();
    var template = MakeTemplate(slug: "x", personas: new[] { "missing_persona" });
    var (handler, _, _, _) = Setup(
        callerTenantId: tenantId,
        templates: new[] { template });

    var result = await handler.Handle(
        new InstallTemplateCommand("x"), CancellationToken.None);

    result.IsFailure.Should().BeTrue();
    result.Error.Code.Should().Be(TemplateErrors.PersonaTargetMissing("missing_persona").Code);
}

[Fact]
public async Task Reserved_persona_slugs_are_accepted_without_db_row()
{
    var tenantId = Guid.NewGuid();
    var template = MakeTemplate(slug: "x", personas: new[] { "anonymous" });
    var (handler, _, _, _) = Setup(
        callerTenantId: tenantId,
        templates: new[] { template });

    var result = await handler.Handle(
        new InstallTemplateCommand("x"), CancellationToken.None);

    result.IsSuccess.Should().BeTrue();
}

[Fact]
public async Task Returns_ToolMissing_when_tool_slug_not_in_registry()
{
    var tenantId = Guid.NewGuid();
    var template = MakeTemplate(slug: "x", tools: new[] { "ghost_tool" }, personas: new[] { "default" });
    var (handler, db, _, _) = Setup(
        callerTenantId: tenantId,
        templates: new[] { template },
        toolSlugs: new[] { "list_users" });

    db.AiPersonas.Add(MakeDefaultPersona(tenantId));
    await db.SaveChangesAsync();

    var result = await handler.Handle(
        new InstallTemplateCommand("x"), CancellationToken.None);

    result.IsFailure.Should().BeTrue();
    result.Error.Code.Should().Be(TemplateErrors.ToolMissing("ghost_tool").Code);
}

[Fact]
public async Task Cross_tenant_install_without_superadmin_returns_forbidden()
{
    var callerTenant = Guid.NewGuid();
    var targetTenant = Guid.NewGuid();
    var template = MakeTemplate(slug: "x", personas: new[] { "default" });
    var (handler, _, _, _) = Setup(
        callerTenantId: callerTenant,
        templates: new[] { template },
        callerIsSuperAdmin: false);

    var result = await handler.Handle(
        new InstallTemplateCommand("x", TargetTenantId: targetTenant),
        CancellationToken.None);

    result.IsFailure.Should().BeTrue();
    // Code-comparison: the handler should produce TemplateErrors.Forbidden — see implementation step 5.
    result.Error.Code.Should().Be(TemplateErrors.Forbidden().Code);
}

[Fact]
public async Task Cross_tenant_install_with_superadmin_writes_to_target_tenant()
{
    var callerTenant = Guid.NewGuid();
    var targetTenant = Guid.NewGuid();
    var template = MakeTemplate(slug: "x", personas: new[] { "default" });
    var (handler, db, _, _) = Setup(
        callerTenantId: callerTenant,
        templates: new[] { template },
        callerIsSuperAdmin: true);

    db.AiPersonas.Add(MakeDefaultPersona(targetTenant));
    await db.SaveChangesAsync();

    var result = await handler.Handle(
        new InstallTemplateCommand("x", TargetTenantId: targetTenant),
        CancellationToken.None);

    result.IsSuccess.Should().BeTrue();
    var assistant = await db.AiAssistants.IgnoreQueryFilters().SingleAsync();
    assistant.TenantId.Should().Be(targetTenant);
}

[Fact]
public async Task Seed_path_with_user_id_override_creates_assistant_with_that_owner()
{
    var tenantId = Guid.NewGuid();
    var template = MakeTemplate(slug: "x", personas: new[] { "default" });
    var (handler, db, cu, _) = Setup(
        callerTenantId: tenantId,
        templates: new[] { template });

    db.AiPersonas.Add(MakeDefaultPersona(tenantId));
    await db.SaveChangesAsync();

    var result = await handler.Handle(
        new InstallTemplateCommand("x", CreatedByUserIdOverride: Guid.Empty),
        CancellationToken.None);

    result.IsSuccess.Should().BeTrue();
    var assistant = await db.AiAssistants.SingleAsync();
    assistant.CreatedByUserId.Should().Be(Guid.Empty);
    cu.VerifyGet(x => x.UserId, Times.Never);
}
```

- [ ] **Step 4: Run handler tests to verify they fail**

```bash
dotnet test boilerplateBE/Starter.sln --filter "InstallTemplateCommandHandlerTests" 2>&1 | tail -10
```

Expected: FAIL — `InstallTemplateCommandHandler` / `TemplateErrors` not found.

- [ ] **Step 5: Implement `TemplateErrors`**

```csharp
// boilerplateBE/src/modules/Starter.Module.AI/Domain/Errors/TemplateErrors.cs
using Starter.Shared.Results;

namespace Starter.Module.AI.Domain.Errors;

public static class TemplateErrors
{
    public static Error NotFound(string slug) => new(
        "Template.NotFound",
        $"AI agent template '{slug}' is not registered.",
        ErrorType.NotFound);

    public static Error AlreadyInstalled(string slug, Guid tenantId) => new(
        "Template.AlreadyInstalled",
        $"Template '{slug}' is already installed in tenant {tenantId}.",
        ErrorType.Conflict);

    public static Error PersonaTargetMissing(string personaSlug) => new(
        "Template.PersonaTargetMissing",
        $"Template references persona '{personaSlug}' which does not exist in the target tenant.",
        ErrorType.Validation);

    public static Error ToolMissing(string toolName) => new(
        "Template.ToolMissing",
        $"Template references tool '{toolName}' which is not in the tool registry.",
        ErrorType.Validation);

    public static Error Forbidden() => new(
        "Template.Forbidden",
        "Cross-tenant install requires superadmin role.",
        ErrorType.Forbidden);
}
```

(NOTE: `Error` and `ErrorType` are existing types from `Starter.Shared.Results`. If the constructor or enum signature differs, mirror an existing error-class file like `AiAssistantErrors.cs` or `PersonaErrors.cs`.)

- [ ] **Step 6: Implement `InstallTemplateCommandHandler`**

```csharp
// boilerplateBE/src/modules/Starter.Module.AI/Application/Commands/InstallTemplate/InstallTemplateCommandHandler.cs
using MediatR;
using Microsoft.EntityFrameworkCore;
using Starter.Abstractions.Capabilities;
using Starter.Application.Common.Interfaces;
using Starter.Module.AI.Application.Services;
using Starter.Module.AI.Domain.Entities;
using Starter.Module.AI.Domain.Enums;
using Starter.Module.AI.Domain.Errors;
using Starter.Module.AI.Infrastructure.Persistence;
using Starter.Shared.Constants;
using Starter.Shared.Results;

namespace Starter.Module.AI.Application.Commands.InstallTemplate;

internal sealed class InstallTemplateCommandHandler(
    AiDbContext db,
    IAiAgentTemplateRegistry templates,
    IAiToolRegistry tools,
    ICurrentUserService currentUser) : IRequestHandler<InstallTemplateCommand, Result<Guid>>
{
    private static readonly HashSet<string> SystemReservedPersonas =
        new(new[] { "anonymous", "default" }, StringComparer.Ordinal);

    public async Task<Result<Guid>> Handle(
        InstallTemplateCommand request, CancellationToken ct)
    {
        // 1. Resolve target tenant
        var callerTenantId = currentUser.TenantId;
        var targetTenantId = request.TargetTenantId ?? callerTenantId;
        if (request.TargetTenantId is { } explicitTarget
            && explicitTarget != callerTenantId
            && !currentUser.IsInRole(Roles.SuperAdmin))
        {
            return Result<Guid>.Failure(TemplateErrors.Forbidden());
        }
        if (targetTenantId is null)
            return Result<Guid>.Failure(TemplateErrors.Forbidden());

        // 2. Resolve template
        var template = templates.Find(request.TemplateSlug);
        if (template is null)
            return Result<Guid>.Failure(TemplateErrors.NotFound(request.TemplateSlug));

        // 3. Duplicate-install guard
        var collision = await db.AiAssistants
            .IgnoreQueryFilters()
            .AnyAsync(
                a => a.TenantId == targetTenantId.Value && a.Slug == template.Slug,
                ct);
        if (collision)
            return Result<Guid>.Failure(
                TemplateErrors.AlreadyInstalled(template.Slug, targetTenantId.Value));

        // 4. Persona validation
        var tenantPersonas = await db.AiPersonas
            .IgnoreQueryFilters()
            .Where(p => p.TenantId == targetTenantId.Value)
            .Select(p => p.Slug)
            .ToListAsync(ct);
        var availableSlugs = new HashSet<string>(
            tenantPersonas.Concat(SystemReservedPersonas), StringComparer.Ordinal);
        foreach (var slug in template.PersonaTargetSlugs)
        {
            if (!availableSlugs.Contains(slug))
                return Result<Guid>.Failure(TemplateErrors.PersonaTargetMissing(slug));
        }

        // 5. Tool validation
        var registeredTools = tools.GetAll().Select(t => t.Name).ToHashSet(StringComparer.Ordinal);
        foreach (var toolName in template.EnabledToolNames)
        {
            if (!registeredTools.Contains(toolName))
                return Result<Guid>.Failure(TemplateErrors.ToolMissing(toolName));
        }

        // 6. Create assistant
        var ownerId = request.CreatedByUserIdOverride
            ?? currentUser.UserId
            ?? throw new InvalidOperationException(
                "InstallTemplateCommand requires either an authenticated user or CreatedByUserIdOverride.");

        var assistant = AiAssistant.Create(
            tenantId: targetTenantId.Value,
            name: template.DisplayName,
            description: template.Description,
            systemPrompt: template.SystemPrompt,
            createdByUserId: ownerId,
            provider: template.Provider,
            model: template.Model,
            temperature: template.Temperature,
            maxTokens: template.MaxTokens,
            executionMode: template.ExecutionMode,
            maxAgentSteps: 10,                         // template-level default
            isActive: true,
            slug: template.Slug);

        if (template.EnabledToolNames.Count > 0)
            assistant.SetEnabledTools(template.EnabledToolNames);
        if (template.PersonaTargetSlugs.Count > 0)
            assistant.SetPersonaTargets(template.PersonaTargetSlugs);
        assistant.SetVisibility(ResourceVisibility.TenantWide);

        // 7. Stamp provenance
        assistant.StampTemplateSource(template.Slug, version: null);

        // 8. Persist
        db.AiAssistants.Add(assistant);
        await db.SaveChangesAsync(ct);

        return Result<Guid>.Success(assistant.Id);
    }
}
```

- [ ] **Step 7: Run handler tests to verify they pass**

```bash
dotnet test boilerplateBE/Starter.sln --filter "InstallTemplateCommandHandlerTests" 2>&1 | tail -10
```

Expected: PASS (8 tests). If `IAiToolRegistry`'s shape differs from `Mock<IAiToolRegistry>` setup, adjust the test fixture accordingly — open `boilerplateBE/src/modules/Starter.Module.AI/Application/Services/IAiToolRegistry.cs` to see the real interface.

- [ ] **Step 8: Commit**

```bash
git add boilerplateBE/src/modules/Starter.Module.AI/Domain/Errors/TemplateErrors.cs \
        boilerplateBE/src/modules/Starter.Module.AI/Application/Commands/InstallTemplate/InstallTemplateCommandHandler.cs \
        boilerplateBE/tests/Starter.Api.Tests/Ai/Templates/InstallTemplateCommandHandlerTests.cs
git commit -m "feat(ai): InstallTemplateCommandHandler with persona/tool validation + provenance stamping"
```

---

## Task 9: `AiTemplatesController`

**Files:**
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Controllers/AiTemplatesController.cs`

The controller is thin plumbing — there are no full WebApplicationFactory tests in this codebase, and handler tests already cover semantics. Add the controller and rely on a manual smoke test at the end.

- [ ] **Step 1: Create the controller**

```csharp
// boilerplateBE/src/modules/Starter.Module.AI/Controllers/AiTemplatesController.cs
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Starter.Module.AI.Application.Commands.InstallTemplate;
using Starter.Module.AI.Application.DTOs;
using Starter.Module.AI.Application.Queries.GetTemplates;
using Starter.Module.AI.Constants;
using Starter.Shared.Models;

namespace Starter.Module.AI.Controllers;

[Route("api/v{version:apiVersion}/ai/templates")]
public sealed class AiTemplatesController(ISender mediator)
    : Starter.Abstractions.Web.BaseApiController(mediator)
{
    [HttpGet]
    [Authorize(Policy = AiPermissions.ManageAssistants)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<AiAgentTemplateDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(CancellationToken ct = default)
    {
        var result = await Mediator.Send(new GetTemplatesQuery(), ct);
        return HandleResult(result);
    }

    [HttpPost("{slug}/install")]
    [Authorize(Policy = AiPermissions.ManageAssistants)]
    [ProducesResponseType(typeof(ApiResponse<Guid>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Install(
        string slug,
        [FromBody] InstallTemplateBody? body,
        CancellationToken ct = default)
    {
        var result = await Mediator.Send(
            new InstallTemplateCommand(slug, body?.TargetTenantId), ct);
        return HandleResult(result);
    }

    public sealed record InstallTemplateBody(Guid? TargetTenantId);
}
```

- [ ] **Step 2: Build to verify the controller compiles**

```bash
dotnet build boilerplateBE/Starter.sln -c Debug 2>&1 | tail -5
```

Expected: clean build.

- [ ] **Step 3: Commit**

```bash
git add boilerplateBE/src/modules/Starter.Module.AI/Controllers/AiTemplatesController.cs
git commit -m "feat(ai): AiTemplatesController exposes catalog + install endpoints"
```

---

## Task 10: AI module DI wiring

**Files:**
- Modify: `boilerplateBE/src/modules/Starter.Module.AI/AIModule.cs`

Add the registry singleton + the assembly-scan call. Do NOT touch `SeedDataAsync` yet — that's Task 13.

- [ ] **Step 1: Find the existing tools-scan line in `AIModule.ConfigureServices`**

```bash
grep -n "AddAiToolsFromAssembly" boilerplateBE/src/modules/Starter.Module.AI/AIModule.cs
```

Expected: one match.

- [ ] **Step 2: Add the template scan + registry singleton next to the tools scan**

Insert after the `services.AddAiToolsFromAssembly(typeof(AIModule).Assembly);` line:

```csharp
services.AddAiAgentTemplatesFromAssembly(typeof(AIModule).Assembly);
services.AddSingleton<IAiAgentTemplateRegistry, AiAgentTemplateRegistry>();
```

You may need to add `using Starter.Module.AI.Application.Services;` and `using Starter.Abstractions.Capabilities;` to `AIModule.cs`.

- [ ] **Step 3: Build + run all AI tests**

```bash
dotnet build boilerplateBE/Starter.sln -c Debug 2>&1 | tail -5
dotnet test boilerplateBE/Starter.sln --filter "FullyQualifiedName~Ai" 2>&1 | tail -5
```

Expected: clean build, all green.

- [ ] **Step 4: Commit**

```bash
git add boilerplateBE/src/modules/Starter.Module.AI/AIModule.cs
git commit -m "feat(ai): register IAiAgentTemplateRegistry + scan AI module assembly for templates"
```

---

## Task 11: Demo templates — Support Assistant (Core, both providers)

**Files:**
- Create: `boilerplateBE/src/Starter.Application/Features/Ai/Templates/SupportAssistantPrompts.cs`
- Create: `boilerplateBE/src/Starter.Application/Features/Ai/Templates/SupportAssistantAnthropicTemplate.cs`
- Create: `boilerplateBE/src/Starter.Application/Features/Ai/Templates/SupportAssistantOpenAiTemplate.cs`
- Modify: `boilerplateBE/src/Starter.Application/DependencyInjection.cs`

- [ ] **Step 1: Create the shared-prompts helper**

```csharp
// boilerplateBE/src/Starter.Application/Features/Ai/Templates/SupportAssistantPrompts.cs
namespace Starter.Application.Features.Ai.Templates;

internal static class SupportAssistantPrompts
{
    public const string Description =
        "Answers questions about users and team members in the current tenant using the list_users tool.";

    public const string SystemPrompt =
        "You are a helpful support assistant. Answer questions about users and team " +
        "members using the list_users tool. Never fabricate user data. If you can't " +
        "find what you're asked about, say so clearly.";
}
```

- [ ] **Step 2: Create the Anthropic variant**

```csharp
// boilerplateBE/src/Starter.Application/Features/Ai/Templates/SupportAssistantAnthropicTemplate.cs
using Starter.Abstractions.Ai;
using Starter.Abstractions.Capabilities;

namespace Starter.Application.Features.Ai.Templates;

public sealed class SupportAssistantAnthropicTemplate : IAiAgentTemplate
{
    public string Slug => "support_assistant_anthropic";
    public string DisplayName => "Support Assistant (Anthropic)";
    public string Description => SupportAssistantPrompts.Description;
    public string Category => "General";
    public string SystemPrompt => SupportAssistantPrompts.SystemPrompt;
    public AiProviderType Provider => AiProviderType.Anthropic;
    public string Model => "claude-sonnet-4-20250514";
    public double Temperature => 0.3;
    public int MaxTokens => 2048;
    public AssistantExecutionMode ExecutionMode => AssistantExecutionMode.Chat;
    public IReadOnlyList<string> EnabledToolNames { get; } = new[] { "list_users" };
    public IReadOnlyList<string> PersonaTargetSlugs { get; } = new[] { "default" };
    public SafetyPreset? SafetyPresetHint => SafetyPreset.Standard;
}
```

- [ ] **Step 3: Create the OpenAI variant**

```csharp
// boilerplateBE/src/Starter.Application/Features/Ai/Templates/SupportAssistantOpenAiTemplate.cs
using Starter.Abstractions.Ai;
using Starter.Abstractions.Capabilities;

namespace Starter.Application.Features.Ai.Templates;

public sealed class SupportAssistantOpenAiTemplate : IAiAgentTemplate
{
    public string Slug => "support_assistant_openai";
    public string DisplayName => "Support Assistant (OpenAI)";
    public string Description => SupportAssistantPrompts.Description;
    public string Category => "General";
    public string SystemPrompt => SupportAssistantPrompts.SystemPrompt;
    public AiProviderType Provider => AiProviderType.OpenAI;
    public string Model => "gpt-4o-mini";
    public double Temperature => 0.3;
    public int MaxTokens => 2048;
    public AssistantExecutionMode ExecutionMode => AssistantExecutionMode.Chat;
    public IReadOnlyList<string> EnabledToolNames { get; } = new[] { "list_users" };
    public IReadOnlyList<string> PersonaTargetSlugs { get; } = new[] { "default" };
    public SafetyPreset? SafetyPresetHint => SafetyPreset.Standard;
}
```

- [ ] **Step 4: Register the scan in `Starter.Application/DependencyInjection.cs`**

Find the line `services.AddAiToolsFromAssembly(typeof(DependencyInjection).Assembly);` and add immediately after:

```csharp
services.AddAiAgentTemplatesFromAssembly(typeof(DependencyInjection).Assembly);
```

(Imports already present from 5c-1.)

- [ ] **Step 5: Build + run all AI tests + spot-check the catalog**

```bash
dotnet build boilerplateBE/Starter.sln -c Debug 2>&1 | tail -5
dotnet test boilerplateBE/Starter.sln --filter "FullyQualifiedName~Ai" 2>&1 | tail -5
```

Expected: clean build, all green. The two demo templates are now in the catalog (provable via Task 14's runtime smoke).

- [ ] **Step 6: Commit**

```bash
git add boilerplateBE/src/Starter.Application/Features/Ai/Templates \
        boilerplateBE/src/Starter.Application/DependencyInjection.cs
git commit -m "feat(ai): demo templates — Support Assistant (Anthropic + OpenAI) in Starter.Application"
```

---

## Task 12: Demo templates — Product Expert (Products, both providers)

**Files:**
- Create: `boilerplateBE/src/modules/Starter.Module.Products/Application/Templates/ProductExpertPrompts.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.Products/Application/Templates/ProductExpertAnthropicTemplate.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.Products/Application/Templates/ProductExpertOpenAiTemplate.cs`
- Modify: `boilerplateBE/src/modules/Starter.Module.Products/ProductsModule.cs`

- [ ] **Step 1: Create the shared-prompts helper**

```csharp
// boilerplateBE/src/modules/Starter.Module.Products/Application/Templates/ProductExpertPrompts.cs
namespace Starter.Module.Products.Application.Templates;

internal static class ProductExpertPrompts
{
    public const string Description =
        "Answers questions about the product catalog using the list_products tool with filters and search.";

    public const string SystemPrompt =
        "You are a product catalog specialist. Use the list_products tool to answer " +
        "questions about what's available, filter by status, and search by name or " +
        "SKU. Always cite the product name when referencing one; do not invent " +
        "products not returned by the tool.";
}
```

- [ ] **Step 2: Create the Anthropic variant**

```csharp
// boilerplateBE/src/modules/Starter.Module.Products/Application/Templates/ProductExpertAnthropicTemplate.cs
using Starter.Abstractions.Ai;
using Starter.Abstractions.Capabilities;

namespace Starter.Module.Products.Application.Templates;

public sealed class ProductExpertAnthropicTemplate : IAiAgentTemplate
{
    public string Slug => "product_expert_anthropic";
    public string DisplayName => "Product Expert (Anthropic)";
    public string Description => ProductExpertPrompts.Description;
    public string Category => "Products";
    public string SystemPrompt => ProductExpertPrompts.SystemPrompt;
    public AiProviderType Provider => AiProviderType.Anthropic;
    public string Model => "claude-sonnet-4-20250514";
    public double Temperature => 0.4;
    public int MaxTokens => 3072;
    public AssistantExecutionMode ExecutionMode => AssistantExecutionMode.Chat;
    public IReadOnlyList<string> EnabledToolNames { get; } = new[] { "list_products" };
    public IReadOnlyList<string> PersonaTargetSlugs { get; } = new[] { "default" };
    public SafetyPreset? SafetyPresetHint => SafetyPreset.Standard;
}
```

- [ ] **Step 3: Create the OpenAI variant**

```csharp
// boilerplateBE/src/modules/Starter.Module.Products/Application/Templates/ProductExpertOpenAiTemplate.cs
using Starter.Abstractions.Ai;
using Starter.Abstractions.Capabilities;

namespace Starter.Module.Products.Application.Templates;

public sealed class ProductExpertOpenAiTemplate : IAiAgentTemplate
{
    public string Slug => "product_expert_openai";
    public string DisplayName => "Product Expert (OpenAI)";
    public string Description => ProductExpertPrompts.Description;
    public string Category => "Products";
    public string SystemPrompt => ProductExpertPrompts.SystemPrompt;
    public AiProviderType Provider => AiProviderType.OpenAI;
    public string Model => "gpt-4o-mini";
    public double Temperature => 0.4;
    public int MaxTokens => 3072;
    public AssistantExecutionMode ExecutionMode => AssistantExecutionMode.Chat;
    public IReadOnlyList<string> EnabledToolNames { get; } = new[] { "list_products" };
    public IReadOnlyList<string> PersonaTargetSlugs { get; } = new[] { "default" };
    public SafetyPreset? SafetyPresetHint => SafetyPreset.Standard;
}
```

- [ ] **Step 4: Register the scan in `ProductsModule.cs`**

Find the `services.AddAiToolsFromAssembly(typeof(ProductsModule).Assembly);` line. Insert after it:

```csharp
services.AddAiAgentTemplatesFromAssembly(typeof(ProductsModule).Assembly);
```

- [ ] **Step 5: Build + run all AI tests**

```bash
dotnet build boilerplateBE/Starter.sln -c Debug 2>&1 | tail -5
dotnet test boilerplateBE/Starter.sln --filter "FullyQualifiedName~Ai" 2>&1 | tail -5
```

Expected: clean build, all green.

- [ ] **Step 6: Commit**

```bash
git add boilerplateBE/src/modules/Starter.Module.Products/Application/Templates \
        boilerplateBE/src/modules/Starter.Module.Products/ProductsModule.cs
git commit -m "feat(ai): demo templates — Product Expert (Anthropic + OpenAI) in Products module"
```

---

## Task 13: Sample-seed migration (`AI:InstallDemoTemplatesOnStartup`)

**Files:**
- Modify: `boilerplateBE/src/modules/Starter.Module.AI/AIModule.cs` (rewrite `SeedDataAsync`)
- Modify: `boilerplateBE/src/Starter.Api/appsettings.Development.json`
- Create: `boilerplateBE/tests/Starter.Api.Tests/Ai/Templates/InstallDemoTemplatesSeedTests.cs`

- [ ] **Step 1: Write the failing seed tests**

The seed runs across all tenants on startup. Test it as a unit — extract the seed loop into a helper method on `AIModule` that takes the `IServiceProvider` (or a smaller scope), so tests can drive it without booting an app.

```csharp
// boilerplateBE/tests/Starter.Api.Tests/Ai/Templates/InstallDemoTemplatesSeedTests.cs
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Starter.Abstractions.Capabilities;
using Starter.Application.Common.Interfaces;
using Starter.Module.AI;
using Starter.Module.AI.Application.Commands.InstallTemplate;
using Starter.Module.AI.Application.Services;
using Starter.Module.AI.Domain.Entities;
using Starter.Module.AI.Infrastructure.Persistence;
using Xunit;

namespace Starter.Api.Tests.Ai.Templates;

public class InstallDemoTemplatesSeedTests
{
    [Fact]
    public async Task Flag_off_does_nothing()
    {
        await using var sp = BuildServiceProvider(flagOn: false);

        var module = new AIModule();
        await module.SeedDataAsync(sp);

        var db = sp.GetRequiredService<AiDbContext>();
        (await db.AiAssistants.IgnoreQueryFilters().CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Flag_on_with_no_tenants_creates_no_assistants()
    {
        await using var sp = BuildServiceProvider(flagOn: true);

        var module = new AIModule();
        await module.SeedDataAsync(sp);

        var db = sp.GetRequiredService<AiDbContext>();
        (await db.AiAssistants.IgnoreQueryFilters().CountAsync()).Should().Be(0);
    }

    // The "flag on + tenants present, install all four" path is a true integration
    // test (requires both AiDbContext and ApplicationDbContext talking to the same
    // backing store with personas seeded). Defer to the manual smoke in Task 14.
    // The unit-level guards above cover the flag-handling logic.
}
```

- [ ] **Step 2: Implement `BuildServiceProvider` helper**

Append to the test class:

```csharp
private static ServiceProvider BuildServiceProvider(bool flagOn)
{
    var services = new ServiceCollection();
    var config = new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["AI:InstallDemoTemplatesOnStartup"] = flagOn ? "true" : "false",
        })
        .Build();
    services.AddSingleton<IConfiguration>(config);

    var cu = new Mock<ICurrentUserService>();
    cu.SetupGet(x => x.UserId).Returns((Guid?)null);
    cu.SetupGet(x => x.TenantId).Returns((Guid?)null);
    services.AddSingleton(cu.Object);

    var dbOpts = new DbContextOptionsBuilder<AiDbContext>()
        .UseInMemoryDatabase($"seed-{Guid.NewGuid()}")
        .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
        .Options;
    services.AddSingleton(dbOpts);
    services.AddScoped<AiDbContext>();

    var appDbMock = new Mock<IApplicationDbContext>();
    appDbMock.SetupGet(x => x.Tenants).Returns(MockDbSet<Tenant>(Array.Empty<Tenant>()));
    services.AddSingleton(appDbMock.Object);

    var registry = new AiAgentTemplateRegistry(Array.Empty<IAiAgentTemplate>());
    services.AddSingleton<IAiAgentTemplateRegistry>(registry);

    var mediator = new Mock<IMediator>();
    services.AddSingleton(mediator.Object);

    return services.BuildServiceProvider();
}

private static DbSet<T> MockDbSet<T>(IEnumerable<T> items) where T : class
{
    var queryable = items.AsQueryable();
    var mock = new Mock<DbSet<T>>();
    mock.As<IQueryable<T>>().Setup(m => m.Provider).Returns(queryable.Provider);
    mock.As<IQueryable<T>>().Setup(m => m.Expression).Returns(queryable.Expression);
    mock.As<IQueryable<T>>().Setup(m => m.ElementType).Returns(queryable.ElementType);
    mock.As<IQueryable<T>>().Setup(m => m.GetEnumerator()).Returns(queryable.GetEnumerator());
    return mock.Object;
}
```

(NOTE: `Starter.Domain.Entities.Tenant` namespace may differ — verify with `grep -rn "class Tenant" boilerplateBE/src/Starter.Domain` and adjust the using.)

- [ ] **Step 3: Run seed tests to verify they fail**

```bash
dotnet test boilerplateBE/Starter.sln --filter "InstallDemoTemplatesSeedTests" 2>&1 | tail -10
```

Expected: FAIL — `AI:InstallDemoTemplatesOnStartup` not handled by `SeedDataAsync` (the legacy flag is still in place).

- [ ] **Step 4: Rewrite `AIModule.SeedDataAsync`**

Replace the existing method (currently lines ~221-263 of `AIModule.cs`) with:

```csharp
public async Task SeedDataAsync(IServiceProvider services, CancellationToken cancellationToken = default)
{
    using var scope = services.CreateScope();
    var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
    if (!configuration.GetValue<bool>("AI:InstallDemoTemplatesOnStartup"))
        return;

    var registry = scope.ServiceProvider.GetRequiredService<IAiAgentTemplateRegistry>();
    var demoSlugs = registry.GetAll().Select(t => t.Slug).ToList();
    if (demoSlugs.Count == 0)
        return;

    var appDb = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
    var tenantIds = await appDb.Tenants
        .IgnoreQueryFilters()
        .Select(t => t.Id)
        .ToListAsync(cancellationToken);
    if (tenantIds.Count == 0)
        return;

    var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
    var logger = scope.ServiceProvider.GetService<ILogger<AIModule>>();

    foreach (var tenantId in tenantIds)
    {
        foreach (var slug in demoSlugs)
        {
            var result = await mediator.Send(
                new InstallTemplateCommand(slug, TargetTenantId: tenantId, CreatedByUserIdOverride: Guid.Empty),
                cancellationToken);

            if (result.IsFailure)
            {
                var code = result.Error.Code;
                if (code == "Template.AlreadyInstalled")
                    logger?.LogDebug("Demo template {Slug} already installed in tenant {TenantId}; skipping.",
                        slug, tenantId);
                else
                    logger?.LogWarning("Demo template install failed: tenant={TenantId} slug={Slug} code={Code} message={Message}",
                        tenantId, slug, code, result.Error.Message);
            }
        }
    }
}
```

Add the necessary `using` statements at the top of `AIModule.cs`:

```csharp
using MediatR;
using Microsoft.Extensions.Logging;
using Starter.Module.AI.Application.Commands.InstallTemplate;
using Starter.Module.AI.Application.Services;
```

(The legacy demo-assistant code inside the old `SeedDataAsync` is removed entirely. Existing dev DBs keep their "AI Tools Demo" rows untouched — the new seed only writes via `InstallTemplateCommand`.)

- [ ] **Step 5: Update `appsettings.Development.json`**

Find:

```jsonc
"AI": {
    "Enabled": true,
    ...
    "SeedSampleAssistant": true,
    ...
```

Replace `"SeedSampleAssistant"` with `"InstallDemoTemplatesOnStartup"`. Same value (true). All other keys unchanged.

```bash
grep -n "SeedSampleAssistant" boilerplateBE/src/Starter.Api/appsettings*.json
```

Expected: zero results after the rename.

- [ ] **Step 6: Build + run seed tests**

```bash
dotnet build boilerplateBE/Starter.sln -c Debug 2>&1 | tail -5
dotnet test boilerplateBE/Starter.sln --filter "InstallDemoTemplatesSeedTests" 2>&1 | tail -5
```

Expected: clean build, both tests PASS.

- [ ] **Step 7: Commit**

```bash
git add boilerplateBE/src/modules/Starter.Module.AI/AIModule.cs \
        boilerplateBE/src/Starter.Api/appsettings.Development.json \
        boilerplateBE/tests/Starter.Api.Tests/Ai/Templates/InstallDemoTemplatesSeedTests.cs
git commit -m "feat(ai): replace AI:SeedSampleAssistant with InstallDemoTemplatesOnStartup template seed"
```

---

## Task 14: End-to-end verification + full suite + runtime smoke

This is the integration gate. Run the full backend test suite, then boot the app and smoke-test the catalog + install flow against a real Postgres + the four registered demo templates.

- [ ] **Step 1: Run the full backend test suite**

```bash
dotnet test boilerplateBE/Starter.sln 2>&1 | tail -15
```

Expected: all green, no failures across all test projects.

- [ ] **Step 2: Boot the dev app**

In a fresh terminal:

```bash
cd boilerplateBE/src/Starter.Api && dotnet run --launch-profile http
```

Wait for `Now listening on: http://localhost:5000`.

- [ ] **Step 3: Login as superadmin and capture the token**

```bash
TOKEN=$(curl -s -X POST http://localhost:5000/api/v1/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"superadmin@starter.com","password":"Admin@123456"}' \
  | python3 -c "import json,sys; print(json.load(sys.stdin)['data']['accessToken'])")
echo "token length: ${#TOKEN}"
```

Expected: a 3000+ character token.

- [ ] **Step 4: Hit the templates catalog endpoint**

```bash
curl -s -H "Authorization: Bearer $TOKEN" http://localhost:5000/api/v1/ai/templates | python3 -m json.tool
```

Expected: an `ApiResponse` with `data` containing four entries — `support_assistant_anthropic` (Module=Core), `support_assistant_openai` (Module=Core), `product_expert_anthropic` (Module=Products), `product_expert_openai` (Module=Products). Each with `provider`, `model`, `enabledToolNames`, `personaTargetSlugs`, `safetyPresetHint` populated.

- [ ] **Step 5: Install one template into a tenant and verify provenance**

Pick acme tenant id from the seed data (find via `psql` or look at `DataSeeder`):

```bash
ACME_ID=$(PGPASSWORD=123456 psql -U postgres -h localhost -d starterdb -tAc \
  "SELECT id FROM tenants WHERE slug='acme'")
echo "$ACME_ID"

curl -s -X POST http://localhost:5000/api/v1/ai/templates/support_assistant_anthropic/install \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d "{\"targetTenantId\":\"$ACME_ID\"}" | python3 -m json.tool
```

Expected: 200 with `data` = the new assistant's GUID.

Verify provenance was stamped:

```bash
PGPASSWORD=123456 psql -U postgres -h localhost -d starterdb -c \
  "SELECT slug, template_source_slug FROM ai_assistants WHERE slug = 'support_assistant_anthropic'"
```

Expected: one row, `template_source_slug = support_assistant_anthropic`.

- [ ] **Step 6: Re-install the same template into the same tenant — expect 409**

```bash
curl -s -o /tmp/_resp.json -w "%{http_code}\n" -X POST \
  http://localhost:5000/api/v1/ai/templates/support_assistant_anthropic/install \
  -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" \
  -d "{\"targetTenantId\":\"$ACME_ID\"}"
cat /tmp/_resp.json | python3 -m json.tool
```

Expected: HTTP 409. Body has `errors` containing `Template.AlreadyInstalled`.

- [ ] **Step 7: Install the OpenAI variant into the same tenant — expect success**

```bash
curl -s -X POST http://localhost:5000/api/v1/ai/templates/support_assistant_openai/install \
  -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" \
  -d "{\"targetTenantId\":\"$ACME_ID\"}" | python3 -m json.tool
```

Expected: 200. Both variants now installed in acme.

- [ ] **Step 8: Try install with unknown slug — expect 404**

```bash
curl -s -o /tmp/_resp.json -w "%{http_code}\n" -X POST \
  http://localhost:5000/api/v1/ai/templates/does_not_exist/install \
  -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" \
  -d "{\"targetTenantId\":\"$ACME_ID\"}"
```

Expected: HTTP 404 with `Template.NotFound` error.

- [ ] **Step 9: Stop the app**

`Ctrl+C` in the terminal running `dotnet run`.

- [ ] **Step 10: Verify no migrations were committed**

Per the project's `feedback_no_migrations` rule:

```bash
git status
git diff --stat
```

Expected: only doc updates / source changes. No `*.cs` files under `*/Persistence/Migrations/` should be staged or committed.

- [ ] **Step 11: Run the full test suite one more time**

```bash
dotnet test boilerplateBE/Starter.sln 2>&1 | tail -10
```

Expected: all green.

- [ ] **Step 12: Update memory roadmap**

Update `/Users/samanjasim/.claude/projects/-Users-samanjasim-Projects-forme-Boilerplate-CQRS/memory/project_ai_plan_4_roadmap.md` to advance the position from "5c (CURRENT)" to "5c-1 + 5c-2 (DONE), 5d (CURRENT)" — done as a memory edit at the end of implementation, separate from the code commit.

---

## Self-Review

Re-checked spec → plan coverage:

| Spec section | Covered by task |
|---|---|
| 3.1 `IAiAgentTemplate` | Task 1 |
| 3.2 `IAiAgentTemplateModuleSource` | Tasks 1 + 2 |
| 3.3 `AddAiAgentTemplatesFromAssembly` | Task 3 |
| 3.4 Registry + DI singleton | Tasks 4 + 10 |
| 3.5 `InstallTemplateCommand` + handler | Tasks 7 + 8 |
| 3.6 `AiAssistant` schema delta | Task 5 |
| 3.7 API surface (controller) | Task 9 |
| 3.8 `AiAgentTemplateDto` | Task 6 |
| 3.9 `GetTemplatesQuery` | Task 6 |
| §4 Demo templates (4 of them) | Tasks 11 + 12 |
| §5 Sample-seed migration | Task 13 |
| §6 Tests | Each task includes its own; Task 14 runs the lot |
| §7 Permissions (`Ai.ManageAssistants` reuse) | Task 9 |

No gaps. Type/method names consistent across tasks (verified):
- `AiAgentTemplateRegistration(IAiAgentTemplate, string)` constructor — defined Task 2, used Task 6 + Task 8 tests
- `IAiAgentTemplateRegistry.Find` / `GetAll` — defined Task 4, used Tasks 6 + 8
- `InstallTemplateCommand(string, Guid?, Guid?)` — defined Task 7, used Tasks 8 + 13 + 14
- `TemplateErrors.NotFound` / `AlreadyInstalled` / `PersonaTargetMissing` / `ToolMissing` / `Forbidden` — defined Task 8, used in Task 8 tests
- `AiAssistant.StampTemplateSource(string, string?)` — defined Task 5, used Task 8

Risks called out for the implementer:
- **Task 1 enum move** is a refactor that touches many files. If `Starter.Abstractions.csproj` doesn't already reference where the enum sub-namespace will live, no project-reference fix is needed (it's same project). But every consumer of the three enums needs a `using Starter.Abstractions.Ai;` add or replace. Watch for warnings about ambiguous types if the old namespace lingers.
- **Task 5 mapper hunt** — `grep -rn "new AiAssistantDto"` may surface 5+ call sites. Adding the two parameters with defaults keeps them compiling without edits, but installed-template provenance won't surface in those DTOs unless the mapping is updated. Update the primary mapping site (likely `GetAssistantsQueryHandler` and `GetAssistantByIdQueryHandler`); leave inline DTOs in tests/responses with their existing arg lists.
- **Task 8 `IAiToolRegistry` shape** — verify `.GetAll()` returns `IEnumerable<IAiToolDefinition>` (or similar). Adjust the tool-registry mock in handler tests if the interface differs.
- **Task 13 seed handler** — uses `IMediator` instead of constructing the handler directly to keep the seed loop readable. The mediator is registered by `AddMediatR` in `Starter.Application.AddApplication`, which runs before module `ConfigureServices`.
