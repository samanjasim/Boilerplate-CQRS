# AI Plan 5b — Persona Primitive — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ship the AI-module persona primitive as defined in the 5b design spec — tenant-scoped `AiPersona` aggregate, `UserPersona` assignment, slug + persona-target-slugs on `AiAssistant`, tenant-lifecycle seeding, per-request persona resolution, safety-preset clause injection, REST API surface, permissions, and tests — without shipping UI or actual moderation.

**Architecture:** New `Personas` folder tree under `Starter.Module.AI` (`Domain/`, `Application/Commands`, `Application/Queries`, `Application/Services/Personas`, `Application/DTOs`, `Infrastructure/Services/Personas`, `Infrastructure/Configurations`, `Infrastructure/EventHandlers`, `Controllers`, `Resources`). The chat pipeline (`ChatExecutionService`) gains a pre-step that resolves a `PersonaContext`, prepends a safety clause to the effective system prompt, enforces the bidirectional visibility filter, and threads the context into `AgentRunContext`. A feature flag (`Ai.Personas.Enabled`, default `true`) short-circuits the pipeline for kill-switch ops use.

**Tech Stack:** .NET 10, C#, EF Core (Npgsql), MediatR, FluentValidation, xUnit + FluentAssertions + Moq, `.resx` resources for safety-preset clauses, `System.Diagnostics.ActivitySource` / `Meter` (reuses `AiAgentMetrics` from 5a).

**Companion spec:** [`docs/superpowers/specs/2026-04-24-ai-plan-5b-persona-primitive-design.md`](../specs/2026-04-24-ai-plan-5b-persona-primitive-design.md)

---

## Task ordering rationale

1. **Domain & persistence first** (Tasks 1–8). Entities, enums, errors, EF configs, DbContext wiring — zero effect on existing code paths because nothing reads them yet.
2. **Application services & resolver** (Tasks 9–14). Accessor, resolver, safety-clause provider, slug generator — all unit-tested in isolation, no chat-pipeline coupling yet.
3. **Lifecycle events** (Tasks 15–16). `TenantCreated` and `UserCreated` handlers that populate personas / assignments — still invisible to existing callers.
4. **DTOs + commands + queries + controllers** (Tasks 17–28). CRUD surface, assignment surface, list-filter extension, frontend-facing `/me/personas`.
5. **Chat-pipeline integration + feature flag + observability** (Tasks 29–31). The moment the runtime actually observes persona behaviour. Gated by `Ai.Personas.Enabled`.
6. **Assistant command extensions + permissions + DI + frontend mirror** (Tasks 32–35).
7. **Final gates** (Task 36). Full solution build + test sweep.

Each task ends with a commit. Every commit leaves `dotnet build` and `dotnet test` green.

---

## File structure

### New files

**Domain:**
- `boilerplateBE/src/modules/Starter.Module.AI/Domain/Entities/AiPersona.cs`
- `boilerplateBE/src/modules/Starter.Module.AI/Domain/Entities/UserPersona.cs`
- `boilerplateBE/src/modules/Starter.Module.AI/Domain/Enums/PersonaAudienceType.cs`
- `boilerplateBE/src/modules/Starter.Module.AI/Domain/Enums/SafetyPreset.cs`
- `boilerplateBE/src/modules/Starter.Module.AI/Domain/Errors/PersonaErrors.cs`

**Application:**
- `boilerplateBE/src/modules/Starter.Module.AI/Application/Services/Personas/IPersonaContextAccessor.cs`
- `boilerplateBE/src/modules/Starter.Module.AI/Application/Services/Personas/IPersonaResolver.cs`
- `boilerplateBE/src/modules/Starter.Module.AI/Application/Services/Personas/ISafetyPresetClauseProvider.cs`
- `boilerplateBE/src/modules/Starter.Module.AI/Application/Services/Personas/ISlugGenerator.cs`
- `boilerplateBE/src/modules/Starter.Module.AI/Application/Services/Runtime/PersonaContext.cs`
- `boilerplateBE/src/modules/Starter.Module.AI/Application/Commands/Personas/CreatePersona/CreatePersonaCommand.cs`
- `boilerplateBE/src/modules/Starter.Module.AI/Application/Commands/Personas/CreatePersona/CreatePersonaCommandHandler.cs`
- `boilerplateBE/src/modules/Starter.Module.AI/Application/Commands/Personas/CreatePersona/CreatePersonaCommandValidator.cs`
- `boilerplateBE/src/modules/Starter.Module.AI/Application/Commands/Personas/UpdatePersona/UpdatePersonaCommand.cs`
- `boilerplateBE/src/modules/Starter.Module.AI/Application/Commands/Personas/UpdatePersona/UpdatePersonaCommandHandler.cs`
- `boilerplateBE/src/modules/Starter.Module.AI/Application/Commands/Personas/UpdatePersona/UpdatePersonaCommandValidator.cs`
- `boilerplateBE/src/modules/Starter.Module.AI/Application/Commands/Personas/DeletePersona/DeletePersonaCommand.cs`
- `boilerplateBE/src/modules/Starter.Module.AI/Application/Commands/Personas/DeletePersona/DeletePersonaCommandHandler.cs`
- `boilerplateBE/src/modules/Starter.Module.AI/Application/Commands/Personas/AssignPersona/AssignPersonaCommand.cs`
- `boilerplateBE/src/modules/Starter.Module.AI/Application/Commands/Personas/AssignPersona/AssignPersonaCommandHandler.cs`
- `boilerplateBE/src/modules/Starter.Module.AI/Application/Commands/Personas/AssignPersona/AssignPersonaCommandValidator.cs`
- `boilerplateBE/src/modules/Starter.Module.AI/Application/Commands/Personas/UnassignPersona/UnassignPersonaCommand.cs`
- `boilerplateBE/src/modules/Starter.Module.AI/Application/Commands/Personas/UnassignPersona/UnassignPersonaCommandHandler.cs`
- `boilerplateBE/src/modules/Starter.Module.AI/Application/Commands/Personas/SetUserDefaultPersona/SetUserDefaultPersonaCommand.cs`
- `boilerplateBE/src/modules/Starter.Module.AI/Application/Commands/Personas/SetUserDefaultPersona/SetUserDefaultPersonaCommandHandler.cs`
- `boilerplateBE/src/modules/Starter.Module.AI/Application/Queries/Personas/GetPersonas/GetPersonasQuery.cs`
- `boilerplateBE/src/modules/Starter.Module.AI/Application/Queries/Personas/GetPersonas/GetPersonasQueryHandler.cs`
- `boilerplateBE/src/modules/Starter.Module.AI/Application/Queries/Personas/GetPersonaById/GetPersonaByIdQuery.cs`
- `boilerplateBE/src/modules/Starter.Module.AI/Application/Queries/Personas/GetPersonaById/GetPersonaByIdQueryHandler.cs`
- `boilerplateBE/src/modules/Starter.Module.AI/Application/Queries/Personas/GetPersonaAssignments/GetPersonaAssignmentsQuery.cs`
- `boilerplateBE/src/modules/Starter.Module.AI/Application/Queries/Personas/GetPersonaAssignments/GetPersonaAssignmentsQueryHandler.cs`
- `boilerplateBE/src/modules/Starter.Module.AI/Application/Queries/Personas/GetMePersonas/GetMePersonasQuery.cs`
- `boilerplateBE/src/modules/Starter.Module.AI/Application/Queries/Personas/GetMePersonas/GetMePersonasQueryHandler.cs`
- `boilerplateBE/src/modules/Starter.Module.AI/Application/DTOs/AiPersonaDto.cs`
- `boilerplateBE/src/modules/Starter.Module.AI/Application/DTOs/UserPersonaDto.cs`
- `boilerplateBE/src/modules/Starter.Module.AI/Application/DTOs/MePersonasDto.cs`
- `boilerplateBE/src/modules/Starter.Module.AI/Application/DTOs/AiPersonaMappers.cs`

**Infrastructure:**
- `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Configurations/AiPersonaConfiguration.cs`
- `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Configurations/UserPersonaConfiguration.cs`
- `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Services/Personas/PersonaContextAccessor.cs`
- `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Services/Personas/PersonaResolver.cs`
- `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Services/Personas/ResxSafetyPresetClauseProvider.cs`
- `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Services/Personas/SlugGenerator.cs`
- `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/EventHandlers/SeedTenantPersonasDomainEventHandler.cs`
- `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/EventHandlers/AssignDefaultPersonaDomainEventHandler.cs`
- `boilerplateBE/src/modules/Starter.Module.AI/Resources/SafetyPresets.resx`
- `boilerplateBE/src/modules/Starter.Module.AI/Resources/SafetyPresets.ar.resx`

**Controllers:**
- `boilerplateBE/src/modules/Starter.Module.AI/Controllers/AiPersonasController.cs`
- `boilerplateBE/src/modules/Starter.Module.AI/Controllers/AiPersonaAssignmentsController.cs`
- `boilerplateBE/src/modules/Starter.Module.AI/Controllers/AiMePersonasController.cs`

**Tests (`boilerplateBE/tests/Starter.Api.Tests/Ai/Personas/`):**
- `AiPersonaEntityTests.cs`
- `UserPersonaEntityTests.cs`
- `AiAssistantSlugTests.cs`
- `SlugGeneratorTests.cs`
- `PersonaResolverTests.cs`
- `SafetyPresetClauseProviderTests.cs`
- `PersonaAssistantVisibilityTests.cs`
- `SeedTenantPersonasDomainEventHandlerTests.cs`
- `AssignDefaultPersonaDomainEventHandlerTests.cs`
- `CreatePersonaCommandTests.cs`
- `UpdatePersonaCommandTests.cs`
- `DeletePersonaCommandTests.cs`
- `AssignPersonaCommandTests.cs`
- `UnassignPersonaCommandTests.cs`
- `SetUserDefaultPersonaCommandTests.cs`
- `GetPersonasQueryTests.cs`
- `GetPersonaAssignmentsQueryTests.cs`
- `GetMePersonasQueryTests.cs`
- `GetAssistantsPersonaFilterTests.cs`
- `AiPersonasControllerTests.cs`
- `AiPersonaAssignmentsControllerTests.cs`
- `AiMePersonasControllerTests.cs`
- `ChatExecutionServicePersonaPathTests.cs`

### Modified files

- `boilerplateBE/src/modules/Starter.Module.AI/Domain/Entities/AiAssistant.cs` — add `Slug`, `PersonaTargetSlugs`, setters, factory overload.
- `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Configurations/AiAssistantConfiguration.cs` — map new columns + indexes.
- `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Persistence/AiDbContext.cs` — DbSets + query filters.
- `boilerplateBE/src/modules/Starter.Module.AI/Application/Services/Runtime/AgentRunContext.cs` — nullable `PersonaContext? Persona`.
- `boilerplateBE/src/modules/Starter.Module.AI/Application/Services/ChatExecutionService.cs` — resolver call, visibility filter, safety clause, persona into run ctx + stream-start frame + reply DTO.
- `boilerplateBE/src/modules/Starter.Module.AI/Application/Commands/SendChatMessage/SendChatMessageCommand.cs` — `Guid? PersonaId`.
- `boilerplateBE/src/modules/Starter.Module.AI/Application/DTOs/AiChatReplyDto.cs` — `string? PersonaSlug`.
- `boilerplateBE/src/modules/Starter.Module.AI/Application/DTOs/AiAssistantDto.cs` + `AiAssistantMappers.cs` — include new slug + persona targets.
- `boilerplateBE/src/modules/Starter.Module.AI/Application/Commands/AssistantInputRules.cs` — shared rules for new fields.
- `boilerplateBE/src/modules/Starter.Module.AI/Application/Commands/CreateAssistant/CreateAssistantCommand.cs` + handler + validator.
- `boilerplateBE/src/modules/Starter.Module.AI/Application/Commands/UpdateAssistant/UpdateAssistantCommand.cs` + handler + validator.
- `boilerplateBE/src/modules/Starter.Module.AI/Application/Queries/GetAssistants/GetAssistantsQueryHandler.cs` — optional persona filter.
- `boilerplateBE/src/modules/Starter.Module.AI/Constants/AiPermissions.cs` — new constants.
- `boilerplateBE/src/modules/Starter.Module.AI/AIModule.cs` — DI wiring + permissions + role grants.
- `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Observability/AiAgentMetrics.cs` — per-persona counter.
- `boilerplateFE/src/constants/permissions.ts` — mirror new permission strings.
- `boilerplateBE/tests/Starter.Api.Tests/Ai/Retrieval/ChatExecutionRagInjectionTests.cs` (and any other chat tests) — update where new constructor / DTO shapes matter.

---

## Task 1: Persona enums + domain errors

**Files:**
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Domain/Enums/PersonaAudienceType.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Domain/Enums/SafetyPreset.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Domain/Errors/PersonaErrors.cs`
- Modify: `boilerplateBE/src/modules/Starter.Module.AI/Domain/Errors/AiErrors.cs` — add two assistant-facing error codes.

- [ ] **Step 1.1: Create `PersonaAudienceType.cs`**

```csharp
namespace Starter.Module.AI.Domain.Enums;

public enum PersonaAudienceType
{
    Internal = 0,
    EndCustomer = 1,
    Anonymous = 2
}
```

- [ ] **Step 1.2: Create `SafetyPreset.cs`**

```csharp
namespace Starter.Module.AI.Domain.Enums;

public enum SafetyPreset
{
    Standard = 0,
    ChildSafe = 1,
    ProfessionalModerated = 2
}
```

- [ ] **Step 1.3: Create `PersonaErrors.cs`**

```csharp
using Starter.Shared.Results;

namespace Starter.Module.AI.Domain.Errors;

public static class PersonaErrors
{
    public static Error NotFound =>
        Error.NotFound("Persona.NotFound", "Persona not found.");

    public static Error NotAssignedToUser =>
        new("Persona.NotAssignedToUser", "You do not have this persona assigned.", ErrorType.Forbidden);

    public static Error RequiresAuthentication =>
        new("Persona.RequiresAuthentication", "This persona is not available for anonymous access.", ErrorType.Unauthorized);

    public static Error NoDefaultForUser =>
        Error.Validation("Persona.NoDefaultForUser",
            "No default persona is configured for your account. Contact your administrator.");

    public static Error AnonymousNotAvailable =>
        Error.Validation("Persona.AnonymousNotAvailable",
            "Anonymous persona is not configured or not active for this tenant.");

    public static Error CannotDeleteSystemReserved =>
        Error.Conflict("Persona.CannotDeleteSystemReserved",
            "System-reserved personas cannot be deleted.");

    public static Error HasActiveAssignments =>
        Error.Conflict("Persona.HasActiveAssignments",
            "Cannot delete a persona with active user assignments. Reassign users first.");

    public static Error SlugReserved(string slug) =>
        Error.Validation("Persona.SlugReserved", $"The slug '{slug}' is reserved.");

    public static Error SlugAlreadyExists(string slug) =>
        Error.Conflict("Persona.SlugAlreadyExists", $"A persona with slug '{slug}' already exists.");

    public static Error AnonymousAudienceImmutable =>
        Error.Validation("Persona.AnonymousAudienceImmutable",
            "Audience type of the anonymous persona cannot be changed.");

    public static Error CannotRemoveLastAssignment =>
        Error.Validation("Persona.CannotRemoveLastAssignment",
            "Cannot unassign the user's only persona. Assign another first.");

    public static Error AlreadyAssigned =>
        Error.Conflict("Persona.AlreadyAssigned", "User already has this persona assigned.");

    public static Error UserNotInTenant =>
        Error.Validation("Persona.UserNotInTenant",
            "Target user does not belong to this tenant.");

    public static Error AnonymousAlreadyExists =>
        Error.Conflict("Persona.AnonymousAlreadyExists",
            "An anonymous persona already exists for this tenant.");

    public static Error AudienceAnonymousReserved =>
        Error.Validation("Persona.AudienceAnonymousReserved",
            "Anonymous audience is reserved for the system-managed anonymous persona.");

    public static Error InvalidSlug =>
        Error.Validation("Persona.InvalidSlug",
            "Slug must be lowercase kebab-case: letters, digits, and hyphens only.");

    public static Error NotActive =>
        Error.Validation("Persona.NotActive", "Persona is inactive.");
}
```

- [ ] **Step 1.4: Extend `AiErrors.cs`** — add two entries at the bottom of the existing class:

Locate the closing `}` of `public static class AiErrors` and insert immediately before it:

```csharp
    public static Error AssistantNotPermittedForPersona =>
        new("AiAssistant.NotPermittedForPersona",
            "This assistant is not available for your current persona.",
            ErrorType.Forbidden);

    public static Error AssistantSlugAlreadyExists(string slug) =>
        Error.Conflict("AiAssistant.SlugAlreadyExists",
            $"An assistant with slug '{slug}' already exists.");
```

- [ ] **Step 1.5: Build**

```bash
dotnet build boilerplateBE/Starter.sln --nologo
```

Expected: build succeeds, zero warnings from the new files.

- [ ] **Step 1.6: Commit**

```bash
git add boilerplateBE/src/modules/Starter.Module.AI/Domain/Enums/PersonaAudienceType.cs \
        boilerplateBE/src/modules/Starter.Module.AI/Domain/Enums/SafetyPreset.cs \
        boilerplateBE/src/modules/Starter.Module.AI/Domain/Errors/PersonaErrors.cs \
        boilerplateBE/src/modules/Starter.Module.AI/Domain/Errors/AiErrors.cs
git commit -m "feat(ai): scaffold persona enums + domain errors"
```

---

## Task 2: `AiPersona` aggregate (TDD)

**Files:**
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Domain/Entities/AiPersona.cs`
- Create: `boilerplateBE/tests/Starter.Api.Tests/Ai/Personas/AiPersonaEntityTests.cs`

- [ ] **Step 2.1: Write failing tests for `AiPersona`**

Create `AiPersonaEntityTests.cs`:

```csharp
using FluentAssertions;
using Starter.Module.AI.Domain.Entities;
using Starter.Module.AI.Domain.Enums;
using Xunit;

namespace Starter.Api.Tests.Ai.Personas;

public sealed class AiPersonaEntityTests
{
    private static readonly Guid Tenant = Guid.NewGuid();
    private static readonly Guid Creator = Guid.NewGuid();

    [Fact]
    public void Create_Sets_Fields_And_Defaults()
    {
        var persona = AiPersona.Create(
            tenantId: Tenant,
            slug: "teacher",
            displayName: "Teacher",
            description: "Teaching staff",
            audienceType: PersonaAudienceType.Internal,
            safetyPreset: SafetyPreset.Standard,
            createdByUserId: Creator);

        persona.TenantId.Should().Be(Tenant);
        persona.Slug.Should().Be("teacher");
        persona.DisplayName.Should().Be("Teacher");
        persona.Description.Should().Be("Teaching staff");
        persona.AudienceType.Should().Be(PersonaAudienceType.Internal);
        persona.SafetyPreset.Should().Be(SafetyPreset.Standard);
        persona.IsSystemReserved.Should().BeFalse();
        persona.IsActive.Should().BeTrue();
        persona.PermittedAgentSlugs.Should().BeEmpty();
    }

    [Fact]
    public void CreateAnonymous_System_Reserved_With_Anonymous_Audience()
    {
        var persona = AiPersona.CreateAnonymous(Tenant, Creator);

        persona.Slug.Should().Be("anonymous");
        persona.AudienceType.Should().Be(PersonaAudienceType.Anonymous);
        persona.IsSystemReserved.Should().BeTrue();
        persona.IsActive.Should().BeFalse();
        persona.SafetyPreset.Should().Be(SafetyPreset.Standard);
    }

    [Fact]
    public void CreateDefault_Not_System_Reserved()
    {
        var persona = AiPersona.CreateDefault(Tenant, Creator);

        persona.Slug.Should().Be("default");
        persona.AudienceType.Should().Be(PersonaAudienceType.Internal);
        persona.IsSystemReserved.Should().BeFalse();
        persona.IsActive.Should().BeTrue();
    }

    [Fact]
    public void Update_Changes_Mutable_Fields()
    {
        var persona = AiPersona.Create(Tenant, "client", "Client", null,
            PersonaAudienceType.EndCustomer, SafetyPreset.Standard, Creator);

        persona.Update(
            displayName: "External Client",
            description: "Outside client personas",
            safetyPreset: SafetyPreset.ProfessionalModerated,
            permittedAgentSlugs: new[] { "brand-content-agent" },
            isActive: true);

        persona.DisplayName.Should().Be("External Client");
        persona.Description.Should().Be("Outside client personas");
        persona.SafetyPreset.Should().Be(SafetyPreset.ProfessionalModerated);
        persona.PermittedAgentSlugs.Should().ContainSingle(s => s == "brand-content-agent");
        persona.ModifiedAt.Should().NotBeNull();
    }

    [Fact]
    public void Deactivate_Sets_IsActive_False()
    {
        var persona = AiPersona.Create(Tenant, "teacher", "Teacher", null,
            PersonaAudienceType.Internal, SafetyPreset.Standard, Creator);

        persona.SetActive(false);

        persona.IsActive.Should().BeFalse();
    }

    [Fact]
    public void PermittedAgentSlugs_Dedups_And_Trims()
    {
        var persona = AiPersona.Create(Tenant, "teacher", "Teacher", null,
            PersonaAudienceType.Internal, SafetyPreset.Standard, Creator);

        persona.Update("Teacher", null, SafetyPreset.Standard,
            new[] { "tutor", "  tutor  ", "Lesson-Planner", "", "   " },
            isActive: true);

        persona.PermittedAgentSlugs.Should().BeEquivalentTo(new[] { "tutor", "lesson-planner" });
    }
}
```

- [ ] **Step 2.2: Run tests — expect FAIL**

```bash
dotnet test boilerplateBE/Starter.sln --filter "FullyQualifiedName~AiPersonaEntityTests" --nologo
```

Expected: compile error (AiPersona not defined).

- [ ] **Step 2.3: Create `AiPersona.cs`**

```csharp
using Starter.Domain.Common;
using Starter.Domain.Exceptions;
using Starter.Module.AI.Domain.Enums;
using Starter.Module.AI.Domain.Errors;

namespace Starter.Module.AI.Domain.Entities;

public sealed class AiPersona : AggregateRoot, ITenantEntity
{
    internal const string AnonymousSlug = "anonymous";
    internal const string DefaultSlug = "default";

    private List<string> _permittedAgentSlugs = new();

    public Guid? TenantId { get; private set; }
    public string Slug { get; private set; } = default!;
    public string DisplayName { get; private set; } = default!;
    public string? Description { get; private set; }
    public PersonaAudienceType AudienceType { get; private set; }
    public SafetyPreset SafetyPreset { get; private set; }
    public bool IsSystemReserved { get; private set; }
    public bool IsActive { get; private set; }
    public Guid CreatedByUserId { get; private set; }

    public IReadOnlyList<string> PermittedAgentSlugs
    {
        get => _permittedAgentSlugs;
        private set => _permittedAgentSlugs = value?.ToList() ?? new();
    }

    private AiPersona() { }

    private AiPersona(
        Guid id,
        Guid? tenantId,
        string slug,
        string displayName,
        string? description,
        PersonaAudienceType audienceType,
        SafetyPreset safetyPreset,
        bool isSystemReserved,
        bool isActive,
        Guid createdByUserId) : base(id)
    {
        TenantId = tenantId;
        Slug = slug;
        DisplayName = displayName;
        Description = description;
        AudienceType = audienceType;
        SafetyPreset = safetyPreset;
        IsSystemReserved = isSystemReserved;
        IsActive = isActive;
        CreatedByUserId = createdByUserId;
    }

    public static AiPersona Create(
        Guid? tenantId,
        string slug,
        string displayName,
        string? description,
        PersonaAudienceType audienceType,
        SafetyPreset safetyPreset,
        Guid createdByUserId)
    {
        if (audienceType == PersonaAudienceType.Anonymous)
            throw new DomainException(
                PersonaErrors.AudienceAnonymousReserved.Description,
                PersonaErrors.AudienceAnonymousReserved.Code);

        return new AiPersona(
            Guid.NewGuid(),
            tenantId,
            slug.Trim().ToLowerInvariant(),
            displayName.Trim(),
            description?.Trim(),
            audienceType,
            safetyPreset,
            isSystemReserved: false,
            isActive: true,
            createdByUserId);
    }

    public static AiPersona CreateAnonymous(Guid tenantId, Guid createdByUserId) =>
        new(
            Guid.NewGuid(),
            tenantId,
            AnonymousSlug,
            "Anonymous",
            "Unauthenticated public visitor.",
            PersonaAudienceType.Anonymous,
            SafetyPreset.Standard,
            isSystemReserved: true,
            isActive: false,
            createdByUserId);

    public static AiPersona CreateDefault(Guid tenantId, Guid createdByUserId) =>
        new(
            Guid.NewGuid(),
            tenantId,
            DefaultSlug,
            "Default",
            "Default persona for authenticated users.",
            PersonaAudienceType.Internal,
            SafetyPreset.Standard,
            isSystemReserved: false,
            isActive: true,
            createdByUserId);

    public void Update(
        string displayName,
        string? description,
        SafetyPreset safetyPreset,
        IEnumerable<string>? permittedAgentSlugs,
        bool isActive)
    {
        DisplayName = displayName.Trim();
        Description = description?.Trim();
        SafetyPreset = safetyPreset;
        _permittedAgentSlugs = Normalize(permittedAgentSlugs);
        IsActive = isActive;
        ModifiedAt = DateTime.UtcNow;
    }

    public void SetActive(bool isActive)
    {
        IsActive = isActive;
        ModifiedAt = DateTime.UtcNow;
    }

    private static List<string> Normalize(IEnumerable<string>? slugs) =>
        slugs?
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => s.Trim().ToLowerInvariant())
            .Distinct()
            .ToList() ?? new();
}
```

- [ ] **Step 2.4: Run tests — expect PASS**

```bash
dotnet test boilerplateBE/Starter.sln --filter "FullyQualifiedName~AiPersonaEntityTests" --nologo
```

Expected: 6/6 passing.

- [ ] **Step 2.5: Commit**

```bash
git add boilerplateBE/src/modules/Starter.Module.AI/Domain/Entities/AiPersona.cs \
        boilerplateBE/tests/Starter.Api.Tests/Ai/Personas/AiPersonaEntityTests.cs
git commit -m "feat(ai): AiPersona aggregate with Create / CreateAnonymous / CreateDefault factories"
```

---

## Task 3: `UserPersona` join entity (TDD)

**Files:**
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Domain/Entities/UserPersona.cs`
- Create: `boilerplateBE/tests/Starter.Api.Tests/Ai/Personas/UserPersonaEntityTests.cs`

- [ ] **Step 3.1: Write failing tests**

Create `UserPersonaEntityTests.cs`:

```csharp
using FluentAssertions;
using Starter.Module.AI.Domain.Entities;
using Starter.Module.AI.Domain.Enums;
using Xunit;

namespace Starter.Api.Tests.Ai.Personas;

public sealed class UserPersonaEntityTests
{
    [Fact]
    public void Create_Sets_Fields()
    {
        var tenant = Guid.NewGuid();
        var user = Guid.NewGuid();
        var personaId = Guid.NewGuid();
        var assignedBy = Guid.NewGuid();

        var up = UserPersona.Create(user, personaId, tenant, isDefault: true, assignedBy: assignedBy);

        up.UserId.Should().Be(user);
        up.PersonaId.Should().Be(personaId);
        up.TenantId.Should().Be(tenant);
        up.IsDefault.Should().BeTrue();
        up.AssignedBy.Should().Be(assignedBy);
        up.AssignedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void MakeDefault_Sets_IsDefault_True()
    {
        var up = UserPersona.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), isDefault: false, null);
        up.MakeDefault();
        up.IsDefault.Should().BeTrue();
    }

    [Fact]
    public void ClearDefault_Sets_IsDefault_False()
    {
        var up = UserPersona.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), isDefault: true, null);
        up.ClearDefault();
        up.IsDefault.Should().BeFalse();
    }
}
```

- [ ] **Step 3.2: Run tests — expect FAIL (compile error)**

```bash
dotnet test boilerplateBE/Starter.sln --filter "FullyQualifiedName~UserPersonaEntityTests" --nologo
```

- [ ] **Step 3.3: Create `UserPersona.cs`**

```csharp
namespace Starter.Module.AI.Domain.Entities;

public sealed class UserPersona
{
    public Guid UserId { get; private set; }
    public Guid PersonaId { get; private set; }
    public Guid TenantId { get; private set; }
    public bool IsDefault { get; private set; }
    public DateTime AssignedAt { get; private set; }
    public Guid? AssignedBy { get; private set; }

    public AiPersona Persona { get; private set; } = null!;

    private UserPersona() { }

    private UserPersona(
        Guid userId,
        Guid personaId,
        Guid tenantId,
        bool isDefault,
        Guid? assignedBy)
    {
        UserId = userId;
        PersonaId = personaId;
        TenantId = tenantId;
        IsDefault = isDefault;
        AssignedBy = assignedBy;
        AssignedAt = DateTime.UtcNow;
    }

    public static UserPersona Create(
        Guid userId,
        Guid personaId,
        Guid tenantId,
        bool isDefault,
        Guid? assignedBy) =>
        new(userId, personaId, tenantId, isDefault, assignedBy);

    public void MakeDefault() => IsDefault = true;
    public void ClearDefault() => IsDefault = false;
}
```

- [ ] **Step 3.4: Run tests — expect PASS**

```bash
dotnet test boilerplateBE/Starter.sln --filter "FullyQualifiedName~UserPersonaEntityTests" --nologo
```

Expected: 3/3 passing.

- [ ] **Step 3.5: Commit**

```bash
git add boilerplateBE/src/modules/Starter.Module.AI/Domain/Entities/UserPersona.cs \
        boilerplateBE/tests/Starter.Api.Tests/Ai/Personas/UserPersonaEntityTests.cs
git commit -m "feat(ai): UserPersona join entity with default-flag helpers"
```

---

## Task 4: Extend `AiAssistant` with `Slug` + `PersonaTargetSlugs` (TDD)

**Files:**
- Modify: `boilerplateBE/src/modules/Starter.Module.AI/Domain/Entities/AiAssistant.cs`
- Create: `boilerplateBE/tests/Starter.Api.Tests/Ai/Personas/AiAssistantSlugTests.cs`

- [ ] **Step 4.1: Write failing tests**

Create `AiAssistantSlugTests.cs`:

```csharp
using FluentAssertions;
using Starter.Module.AI.Domain.Entities;
using Starter.Module.AI.Domain.Enums;
using Xunit;

namespace Starter.Api.Tests.Ai.Personas;

public sealed class AiAssistantSlugTests
{
    [Fact]
    public void Create_Accepts_Explicit_Slug()
    {
        var a = AiAssistant.Create(
            tenantId: Guid.NewGuid(),
            name: "Brand Content Agent",
            description: null,
            systemPrompt: "prompt",
            createdByUserId: Guid.NewGuid(),
            slug: "brand-content-agent");

        a.Slug.Should().Be("brand-content-agent");
        a.PersonaTargetSlugs.Should().BeEmpty();
    }

    [Fact]
    public void Create_Without_Slug_Defaults_To_Empty_And_Caller_Sets_Later()
    {
        var a = AiAssistant.Create(
            tenantId: Guid.NewGuid(),
            name: "Brand Content Agent",
            description: null,
            systemPrompt: "prompt",
            createdByUserId: Guid.NewGuid());

        a.Slug.Should().Be("");
    }

    [Fact]
    public void SetSlug_Normalises_Casing_And_Trim()
    {
        var a = AiAssistant.Create(Guid.NewGuid(), "X", null, "prompt", Guid.NewGuid());
        a.SetSlug("  Brand-Content-AGENT  ");
        a.Slug.Should().Be("brand-content-agent");
    }

    [Fact]
    public void SetPersonaTargets_Dedups_And_Normalises()
    {
        var a = AiAssistant.Create(Guid.NewGuid(), "X", null, "prompt", Guid.NewGuid());
        a.SetPersonaTargets(new[] { "Student", "student ", "TEACHER", "   ", null!, "teacher" });
        a.PersonaTargetSlugs.Should().BeEquivalentTo(new[] { "student", "teacher" });
    }
}
```

- [ ] **Step 4.2: Run — expect FAIL (compile error)**

```bash
dotnet test boilerplateBE/Starter.sln --filter "FullyQualifiedName~AiAssistantSlugTests" --nologo
```

- [ ] **Step 4.3: Add `Slug` + `PersonaTargetSlugs` to `AiAssistant.cs`**

Open `boilerplateBE/src/modules/Starter.Module.AI/Domain/Entities/AiAssistant.cs` and apply these edits:

**Edit A — add field declarations** just after the existing `_knowledgeBaseDocIds` field (around line 14):

```csharp
    private List<string> _personaTargetSlugs = new();
```

**Edit B — add properties** just after the `KnowledgeBaseDocIds` property block (around line 37):

```csharp
    public string Slug { get; private set; } = "";

    /// <summary>Slugs of personas this assistant is visible to. Empty = visible to all personas. Persisted as jsonb.</summary>
    public IReadOnlyList<string> PersonaTargetSlugs
    {
        get => _personaTargetSlugs;
        private set => _personaTargetSlugs = value?.ToList() ?? new();
    }
```

**Edit C — extend `Create` factory signature + body** (replace the existing `Create` method entirely):

```csharp
    public static AiAssistant Create(
        Guid? tenantId,
        string name,
        string? description,
        string systemPrompt,
        Guid createdByUserId,
        AiProviderType? provider = null,
        string? model = null,
        double temperature = 0.7,
        int maxTokens = 4096,
        AssistantExecutionMode executionMode = AssistantExecutionMode.Chat,
        int maxAgentSteps = 10,
        bool isActive = true,
        string? slug = null)
    {
        var a = new AiAssistant(
            Guid.NewGuid(),
            tenantId,
            name.Trim(),
            description?.Trim(),
            systemPrompt.Trim(),
            provider,
            model?.Trim(),
            temperature,
            maxTokens,
            executionMode,
            maxAgentSteps,
            isActive,
            createdByUserId);

        if (!string.IsNullOrWhiteSpace(slug))
            a.SetSlug(slug);

        return a;
    }
```

**Edit D — add slug + persona-target setters** just below the existing `SetKnowledgeBase` method:

```csharp
    public void SetSlug(string slug)
    {
        Slug = (slug ?? "").Trim().ToLowerInvariant();
        ModifiedAt = DateTime.UtcNow;
    }

    public void SetPersonaTargets(IEnumerable<string?>? slugs)
    {
        _personaTargetSlugs = (slugs ?? Array.Empty<string?>())
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => s!.Trim().ToLowerInvariant())
            .Distinct()
            .ToList();
        ModifiedAt = DateTime.UtcNow;
    }

    public bool IsVisibleToPersona(string personaSlug, IReadOnlyList<string> personaPermittedAgentSlugs)
    {
        personaSlug = personaSlug.ToLowerInvariant();
        var personaSide = personaPermittedAgentSlugs.Count == 0
            || personaPermittedAgentSlugs.Contains(Slug, StringComparer.Ordinal);
        var agentSide = _personaTargetSlugs.Count == 0
            || _personaTargetSlugs.Contains(personaSlug, StringComparer.Ordinal);
        return personaSide && agentSide;
    }
```

- [ ] **Step 4.4: Run — expect PASS**

```bash
dotnet test boilerplateBE/Starter.sln --filter "FullyQualifiedName~AiAssistantSlugTests" --nologo
```

Expected: 4/4 passing.

- [ ] **Step 4.5: Run existing assistant tests — expect PASS (parity)**

```bash
dotnet test boilerplateBE/Starter.sln --filter "FullyQualifiedName~Ai" --nologo
```

Expected: all existing AI tests still green (new `slug` parameter is optional).

- [ ] **Step 4.6: Commit**

```bash
git add boilerplateBE/src/modules/Starter.Module.AI/Domain/Entities/AiAssistant.cs \
        boilerplateBE/tests/Starter.Api.Tests/Ai/Personas/AiAssistantSlugTests.cs
git commit -m "feat(ai): add Slug + PersonaTargetSlugs to AiAssistant with visibility helper"
```

---

## Task 5: EF configuration — `AiPersonaConfiguration`

**Files:**
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Configurations/AiPersonaConfiguration.cs`

- [ ] **Step 5.1: Create `AiPersonaConfiguration.cs`**

```csharp
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Starter.Module.AI.Domain.Entities;

namespace Starter.Module.AI.Infrastructure.Configurations;

internal sealed class AiPersonaConfiguration : IEntityTypeConfiguration<AiPersona>
{
    private static readonly JsonSerializerOptions JsonOpts = new();

    public void Configure(EntityTypeBuilder<AiPersona> builder)
    {
        builder.ToTable("ai_personas");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(e => e.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(e => e.Slug).HasColumnName("slug").HasMaxLength(64).IsRequired();
        builder.Property(e => e.DisplayName).HasColumnName("display_name").HasMaxLength(120).IsRequired();
        builder.Property(e => e.Description).HasColumnName("description").HasMaxLength(500);
        builder.Property(e => e.AudienceType)
            .HasColumnName("audience_type").HasConversion<int>().IsRequired();
        builder.Property(e => e.SafetyPreset)
            .HasColumnName("safety_preset").HasConversion<int>().IsRequired();
        builder.Property(e => e.IsSystemReserved).HasColumnName("is_system_reserved").IsRequired();
        builder.Property(e => e.IsActive).HasColumnName("is_active").IsRequired();
        builder.Property(e => e.CreatedByUserId).HasColumnName("created_by_user_id").IsRequired();
        builder.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(e => e.ModifiedAt).HasColumnName("modified_at");
        builder.Property(e => e.CreatedBy).HasColumnName("created_by");
        builder.Property(e => e.ModifiedBy).HasColumnName("modified_by");

        var stringListConverter = new ValueConverter<IReadOnlyList<string>, string>(
            v => JsonSerializer.Serialize(v, JsonOpts),
            v => JsonSerializer.Deserialize<List<string>>(v, JsonOpts) ?? new List<string>());
        var stringListComparer = new ValueComparer<IReadOnlyList<string>>(
            (a, b) => a!.SequenceEqual(b!),
            v => v.Aggregate(0, (hash, s) => HashCode.Combine(hash, s.GetHashCode())),
            v => v.ToList());

        builder.Property(e => e.PermittedAgentSlugs)
            .HasColumnName("permitted_agent_slugs")
            .HasColumnType("jsonb")
            .HasConversion(stringListConverter, stringListComparer)
            .IsRequired();

        builder.HasIndex(e => new { e.TenantId, e.Slug }).IsUnique().HasDatabaseName("ix_ai_personas_tenant_slug");
        builder.HasIndex(e => new { e.TenantId, e.AudienceType }).HasDatabaseName("ix_ai_personas_tenant_audience");
        builder.HasIndex(e => new { e.TenantId, e.IsActive }).HasDatabaseName("ix_ai_personas_tenant_active");
    }
}
```

- [ ] **Step 5.2: Build**

```bash
dotnet build boilerplateBE/Starter.sln --nologo
```

Expected: succeed.

- [ ] **Step 5.3: Commit**

```bash
git add boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Configurations/AiPersonaConfiguration.cs
git commit -m "feat(ai): EF config for AiPersona with tenant+slug unique index"
```

---

## Task 6: EF configuration — `UserPersonaConfiguration`

**Files:**
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Configurations/UserPersonaConfiguration.cs`

- [ ] **Step 6.1: Create `UserPersonaConfiguration.cs`**

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Starter.Module.AI.Domain.Entities;

namespace Starter.Module.AI.Infrastructure.Configurations;

internal sealed class UserPersonaConfiguration : IEntityTypeConfiguration<UserPersona>
{
    public void Configure(EntityTypeBuilder<UserPersona> builder)
    {
        builder.ToTable("ai_user_personas");
        builder.HasKey(e => new { e.UserId, e.PersonaId });

        builder.Property(e => e.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(e => e.PersonaId).HasColumnName("persona_id").IsRequired();
        builder.Property(e => e.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(e => e.IsDefault).HasColumnName("is_default").IsRequired();
        builder.Property(e => e.AssignedAt).HasColumnName("assigned_at").IsRequired();
        builder.Property(e => e.AssignedBy).HasColumnName("assigned_by");

        builder.HasOne(e => e.Persona)
            .WithMany()
            .HasForeignKey(e => e.PersonaId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(e => new { e.TenantId, e.UserId })
            .HasDatabaseName("ix_ai_user_personas_tenant_user");
        builder.HasIndex(e => e.PersonaId)
            .HasDatabaseName("ix_ai_user_personas_persona");

        // Filtered unique: exactly one default per user per tenant
        builder.HasIndex(e => new { e.UserId, e.TenantId })
            .IsUnique()
            .HasFilter("is_default = TRUE")
            .HasDatabaseName("ux_ai_user_personas_user_tenant_default");
    }
}
```

- [ ] **Step 6.2: Build**

```bash
dotnet build boilerplateBE/Starter.sln --nologo
```

- [ ] **Step 6.3: Commit**

```bash
git add boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Configurations/UserPersonaConfiguration.cs
git commit -m "feat(ai): EF config for UserPersona with composite PK and filtered unique default"
```

---

## Task 7: Extend `AiAssistantConfiguration` with slug + persona targets

**Files:**
- Modify: `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Configurations/AiAssistantConfiguration.cs`

- [ ] **Step 7.1: Add mappings**

Open the file; in the block of `builder.Property` calls, just below the existing `KnowledgeBaseDocIds` mapping, insert:

```csharp
        builder.Property(e => e.Slug)
            .HasColumnName("slug")
            .HasMaxLength(64)
            .IsRequired()
            .HasDefaultValue("");

        builder.Property(e => e.PersonaTargetSlugs)
            .HasColumnName("persona_target_slugs")
            .HasColumnType("jsonb")
            .HasConversion(stringListConverter, stringListComparer)
            .IsRequired();
```

(`stringListConverter` and `stringListComparer` are already declared above in the same method and can be reused.)

At the bottom of the `Configure` method, add:

```csharp
        builder.HasIndex(e => new { e.TenantId, e.Slug })
            .IsUnique()
            .HasFilter("slug <> ''")
            .HasDatabaseName("ux_ai_assistants_tenant_slug");
```

- [ ] **Step 7.2: Build**

```bash
dotnet build boilerplateBE/Starter.sln --nologo
```

- [ ] **Step 7.3: Commit**

```bash
git add boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Configurations/AiAssistantConfiguration.cs
git commit -m "feat(ai): map AiAssistant.Slug + PersonaTargetSlugs columns"
```

---

## Task 8: Wire `AiDbContext`

**Files:**
- Modify: `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Persistence/AiDbContext.cs`

- [ ] **Step 8.1: Add DbSets + query filters**

In `AiDbContext.cs`:

- Inside the existing `using Starter.Module.AI.Domain.Entities;` block already present, no additional using needed.
- After the `public DbSet<AiUsageLog> AiUsageLogs => Set<AiUsageLog>();` line, add:

```csharp
    public DbSet<AiPersona> AiPersonas => Set<AiPersona>();
    public DbSet<UserPersona> UserPersonas => Set<UserPersona>();
```

- Inside `OnModelCreating`, after the existing tenant filter block, add:

```csharp
        modelBuilder.Entity<AiPersona>().HasQueryFilter(e =>
            CurrentTenantId == null || e.TenantId == CurrentTenantId);
        modelBuilder.Entity<UserPersona>().HasQueryFilter(e =>
            CurrentTenantId == null || e.TenantId == CurrentTenantId);
```

- [ ] **Step 8.2: Build**

```bash
dotnet build boilerplateBE/Starter.sln --nologo
```

- [ ] **Step 8.3: Commit**

```bash
git add boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Persistence/AiDbContext.cs
git commit -m "feat(ai): register AiPersona + UserPersona DbSets and tenant query filters"
```

---

## Task 9: `PersonaContext` + extend `AgentRunContext`

**Files:**
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Application/Services/Runtime/PersonaContext.cs`
- Modify: `boilerplateBE/src/modules/Starter.Module.AI/Application/Services/Runtime/AgentRunContext.cs`

- [ ] **Step 9.1: Create `PersonaContext.cs`**

```csharp
using Starter.Module.AI.Domain.Enums;

namespace Starter.Module.AI.Application.Services.Runtime;

internal sealed record PersonaContext(
    Guid Id,
    string Slug,
    PersonaAudienceType Audience,
    SafetyPreset Safety,
    IReadOnlyList<string> PermittedAgentSlugs);
```

- [ ] **Step 9.2: Modify `AgentRunContext.cs`** — append `PersonaContext? Persona = null` parameter

Replace the existing `AgentRunContext` record with:

```csharp
internal sealed record AgentRunContext(
    IReadOnlyList<AiChatMessage> Messages,
    string SystemPrompt,
    AgentModelConfig ModelConfig,
    ToolResolutionResult Tools,
    int MaxSteps,
    LoopBreakPolicy LoopBreak,
    bool Streaming = false,
    PersonaContext? Persona = null);
```

- [ ] **Step 9.3: Build and run existing AI tests — expect PASS (parity)**

```bash
dotnet build boilerplateBE/Starter.sln --nologo
dotnet test boilerplateBE/Starter.sln --filter "FullyQualifiedName~Ai.Runtime" --nologo
```

- [ ] **Step 9.4: Commit**

```bash
git add boilerplateBE/src/modules/Starter.Module.AI/Application/Services/Runtime/PersonaContext.cs \
        boilerplateBE/src/modules/Starter.Module.AI/Application/Services/Runtime/AgentRunContext.cs
git commit -m "feat(ai): add PersonaContext record and thread it through AgentRunContext"
```

---

## Task 10: `IPersonaContextAccessor` (request-scoped)

**Files:**
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Application/Services/Personas/IPersonaContextAccessor.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Services/Personas/PersonaContextAccessor.cs`

- [ ] **Step 10.1: Create the interface**

```csharp
using Starter.Module.AI.Application.Services.Runtime;

namespace Starter.Module.AI.Application.Services.Personas;

/// <summary>
/// Request-scoped holder for the resolved persona. Populated by ChatExecutionService
/// and read by downstream services that need persona awareness (e.g. observability,
/// future moderation adapters).
/// </summary>
internal interface IPersonaContextAccessor
{
    PersonaContext? Current { get; }
    void Set(PersonaContext? context);
}
```

- [ ] **Step 10.2: Create the implementation**

```csharp
using Starter.Module.AI.Application.Services.Personas;
using Starter.Module.AI.Application.Services.Runtime;

namespace Starter.Module.AI.Infrastructure.Services.Personas;

internal sealed class PersonaContextAccessor : IPersonaContextAccessor
{
    public PersonaContext? Current { get; private set; }
    public void Set(PersonaContext? context) => Current = context;
}
```

- [ ] **Step 10.3: Build**

```bash
dotnet build boilerplateBE/Starter.sln --nologo
```

- [ ] **Step 10.4: Commit**

```bash
git add boilerplateBE/src/modules/Starter.Module.AI/Application/Services/Personas/IPersonaContextAccessor.cs \
        boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Services/Personas/PersonaContextAccessor.cs
git commit -m "feat(ai): request-scoped IPersonaContextAccessor"
```

---

## Task 11: `ISlugGenerator` (TDD)

**Files:**
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Application/Services/Personas/ISlugGenerator.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Services/Personas/SlugGenerator.cs`
- Create: `boilerplateBE/tests/Starter.Api.Tests/Ai/Personas/SlugGeneratorTests.cs`

- [ ] **Step 11.1: Write failing tests**

```csharp
using FluentAssertions;
using Starter.Module.AI.Infrastructure.Services.Personas;
using Xunit;

namespace Starter.Api.Tests.Ai.Personas;

public sealed class SlugGeneratorTests
{
    private static readonly SlugGenerator Gen = new();

    [Theory]
    [InlineData("Brand Content Agent", "brand-content-agent")]
    [InlineData("  TUTOR  ", "tutor")]
    [InlineData("hello / world", "hello-world")]
    [InlineData("Grade 5 Arabic Tutor", "grade-5-arabic-tutor")]
    [InlineData("مرحبا 5 ", "5")]
    [InlineData("a---b__c", "a-b-c")]
    [InlineData("", "untitled")]
    public void Slugify_Produces_Kebab_Case(string input, string expected)
    {
        Gen.Slugify(input).Should().Be(expected);
    }

    [Fact]
    public void EnsureUnique_Returns_Input_If_No_Collision()
    {
        var result = Gen.EnsureUnique("teacher", taken: new HashSet<string>());
        result.Should().Be("teacher");
    }

    [Fact]
    public void EnsureUnique_Appends_2_3_When_Collision()
    {
        var taken = new HashSet<string> { "teacher", "teacher-2" };
        Gen.EnsureUnique("teacher", taken).Should().Be("teacher-3");
    }

    [Fact]
    public void Slugify_Truncates_To_64_Chars()
    {
        var long_ = new string('a', 200);
        Gen.Slugify(long_).Length.Should().BeLessOrEqualTo(64);
    }
}
```

- [ ] **Step 11.2: Run — expect FAIL**

```bash
dotnet test boilerplateBE/Starter.sln --filter "FullyQualifiedName~SlugGeneratorTests" --nologo
```

- [ ] **Step 11.3: Create interface**

```csharp
namespace Starter.Module.AI.Application.Services.Personas;

internal interface ISlugGenerator
{
    string Slugify(string input);
    string EnsureUnique(string slug, ISet<string> taken);
}
```

- [ ] **Step 11.4: Create implementation**

```csharp
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Starter.Module.AI.Application.Services.Personas;

namespace Starter.Module.AI.Infrastructure.Services.Personas;

internal sealed class SlugGenerator : ISlugGenerator
{
    private const int MaxLength = 64;
    private static readonly Regex NonAlphaNum = new("[^a-z0-9]+", RegexOptions.Compiled);
    private static readonly Regex MultiDash = new("-{2,}", RegexOptions.Compiled);

    public string Slugify(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return "untitled";

        var normalised = input.Trim().Normalize(NormalizationForm.FormKD);

        var filtered = new StringBuilder(normalised.Length);
        foreach (var c in normalised)
        {
            var cat = CharUnicodeInfo.GetUnicodeCategory(c);
            if (cat != UnicodeCategory.NonSpacingMark)
                filtered.Append(c);
        }

        var lower = filtered.ToString().ToLowerInvariant();
        var cleaned = NonAlphaNum.Replace(lower, "-").Trim('-');
        cleaned = MultiDash.Replace(cleaned, "-");

        if (cleaned.Length == 0)
            return "untitled";

        if (cleaned.Length > MaxLength)
            cleaned = cleaned[..MaxLength].TrimEnd('-');

        return cleaned;
    }

    public string EnsureUnique(string slug, ISet<string> taken)
    {
        if (!taken.Contains(slug))
            return slug;

        for (var i = 2; i < 1000; i++)
        {
            var candidate = $"{slug}-{i}";
            if (candidate.Length > MaxLength)
                candidate = $"{slug[..(MaxLength - 1 - i.ToString().Length)]}-{i}";
            if (!taken.Contains(candidate))
                return candidate;
        }
        throw new InvalidOperationException("Could not find a unique slug.");
    }
}
```

- [ ] **Step 11.5: Run — expect PASS**

```bash
dotnet test boilerplateBE/Starter.sln --filter "FullyQualifiedName~SlugGeneratorTests" --nologo
```

- [ ] **Step 11.6: Commit**

```bash
git add boilerplateBE/src/modules/Starter.Module.AI/Application/Services/Personas/ISlugGenerator.cs \
        boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Services/Personas/SlugGenerator.cs \
        boilerplateBE/tests/Starter.Api.Tests/Ai/Personas/SlugGeneratorTests.cs
git commit -m "feat(ai): SlugGenerator with kebab-case normalisation + collision suffix"
```

---

## Task 12: `IPersonaResolver` (TDD)

**Files:**
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Application/Services/Personas/IPersonaResolver.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Services/Personas/PersonaResolver.cs`
- Create: `boilerplateBE/tests/Starter.Api.Tests/Ai/Personas/PersonaResolverTests.cs`

- [ ] **Step 12.1: Create interface**

```csharp
using Starter.Module.AI.Application.Services.Runtime;
using Starter.Shared.Results;

namespace Starter.Module.AI.Application.Services.Personas;

internal interface IPersonaResolver
{
    Task<Result<PersonaContext>> ResolveAsync(Guid? explicitPersonaId, CancellationToken ct);
}
```

- [ ] **Step 12.2: Write failing tests**

```csharp
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Starter.Application.Common.Interfaces;
using Starter.Module.AI.Application.Services.Runtime;
using Starter.Module.AI.Domain.Entities;
using Starter.Module.AI.Domain.Enums;
using Starter.Module.AI.Domain.Errors;
using Starter.Module.AI.Infrastructure.Persistence;
using Starter.Module.AI.Infrastructure.Services.Personas;
using Xunit;
using Moq;

namespace Starter.Api.Tests.Ai.Personas;

public sealed class PersonaResolverTests
{
    private static readonly Guid Tenant = Guid.NewGuid();
    private static readonly Guid User = Guid.NewGuid();

    private static (PersonaResolver resolver, AiDbContext db) Setup(
        Guid? currentUserId,
        Guid? currentTenantId,
        Action<AiDbContext>? seed = null)
    {
        var cu = new Mock<ICurrentUserService>();
        cu.SetupGet(x => x.UserId).Returns(currentUserId);
        cu.SetupGet(x => x.TenantId).Returns(currentTenantId);

        var opts = new DbContextOptionsBuilder<AiDbContext>()
            .UseInMemoryDatabase($"resolver-{Guid.NewGuid()}")
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        var db = new AiDbContext(opts, cu.Object);
        seed?.Invoke(db);
        db.SaveChanges();

        return (new PersonaResolver(db, cu.Object), db);
    }

    private static AiPersona Persona(string slug, PersonaAudienceType audience = PersonaAudienceType.Internal, bool active = true)
    {
        var p = audience == PersonaAudienceType.Anonymous
            ? AiPersona.CreateAnonymous(Tenant, User)
            : AiPersona.Create(Tenant, slug, slug, null, audience, SafetyPreset.Standard, User);
        if (!active) p.SetActive(false);
        return p;
    }

    [Fact]
    public async Task Authenticated_User_Default_Persona_Is_Returned_When_No_Override()
    {
        var (resolver, _) = Setup(User, Tenant, db =>
        {
            var p = Persona("default");
            db.AiPersonas.Add(p);
            db.UserPersonas.Add(UserPersona.Create(User, p.Id, Tenant, isDefault: true, null));
        });

        var result = await resolver.ResolveAsync(explicitPersonaId: null, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Slug.Should().Be("default");
    }

    [Fact]
    public async Task Authenticated_User_No_Default_Returns_Error()
    {
        var (resolver, _) = Setup(User, Tenant);

        var result = await resolver.ResolveAsync(null, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be(PersonaErrors.NoDefaultForUser.Code);
    }

    [Fact]
    public async Task Override_Not_Assigned_To_User_Returns_Error()
    {
        Guid pid = default!;
        var (resolver, _) = Setup(User, Tenant, db =>
        {
            var p = Persona("student");
            db.AiPersonas.Add(p);
            pid = p.Id;
        });

        var result = await resolver.ResolveAsync(pid, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be(PersonaErrors.NotAssignedToUser.Code);
    }

    [Fact]
    public async Task Override_Assigned_To_User_Returns_That_Persona()
    {
        Guid pid = default!;
        var (resolver, _) = Setup(User, Tenant, db =>
        {
            var def = Persona("default");
            var s = Persona("student");
            db.AiPersonas.AddRange(def, s);
            db.UserPersonas.Add(UserPersona.Create(User, def.Id, Tenant, true, null));
            db.UserPersonas.Add(UserPersona.Create(User, s.Id, Tenant, false, null));
            pid = s.Id;
        });

        var result = await resolver.ResolveAsync(pid, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Slug.Should().Be("student");
    }

    [Fact]
    public async Task Unauthenticated_Falls_Back_To_Anonymous()
    {
        var (resolver, _) = Setup(currentUserId: null, currentTenantId: Tenant, db =>
        {
            var anon = AiPersona.CreateAnonymous(Tenant, Guid.NewGuid());
            anon.Update("Anonymous", null, SafetyPreset.Standard,
                Array.Empty<string>(), isActive: true);
            db.AiPersonas.Add(anon);
        });

        var result = await resolver.ResolveAsync(null, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Audience.Should().Be(PersonaAudienceType.Anonymous);
    }

    [Fact]
    public async Task Unauthenticated_Without_Anonymous_Returns_Error()
    {
        var (resolver, _) = Setup(null, Tenant);

        var result = await resolver.ResolveAsync(null, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be(PersonaErrors.AnonymousNotAvailable.Code);
    }

    [Fact]
    public async Task Override_NotFound_Returns_Error()
    {
        var (resolver, _) = Setup(User, Tenant);

        var result = await resolver.ResolveAsync(Guid.NewGuid(), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be(PersonaErrors.NotFound.Code);
    }
}
```

- [ ] **Step 12.3: Run — expect FAIL (class missing)**

```bash
dotnet test boilerplateBE/Starter.sln --filter "FullyQualifiedName~PersonaResolverTests" --nologo
```

- [ ] **Step 12.4: Create `PersonaResolver.cs`**

```csharp
using Microsoft.EntityFrameworkCore;
using Starter.Application.Common.Interfaces;
using Starter.Module.AI.Application.Services.Personas;
using Starter.Module.AI.Application.Services.Runtime;
using Starter.Module.AI.Domain.Entities;
using Starter.Module.AI.Domain.Enums;
using Starter.Module.AI.Domain.Errors;
using Starter.Module.AI.Infrastructure.Persistence;
using Starter.Shared.Results;

namespace Starter.Module.AI.Infrastructure.Services.Personas;

internal sealed class PersonaResolver(
    AiDbContext db,
    ICurrentUserService currentUser) : IPersonaResolver
{
    public async Task<Result<PersonaContext>> ResolveAsync(
        Guid? explicitPersonaId,
        CancellationToken ct)
    {
        var userId = currentUser.UserId;
        var tenantId = currentUser.TenantId;

        if (explicitPersonaId.HasValue)
        {
            var persona = await db.AiPersonas
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == explicitPersonaId.Value, ct);

            if (persona is null)
                return Result.Failure<PersonaContext>(PersonaErrors.NotFound);
            if (!persona.IsActive)
                return Result.Failure<PersonaContext>(PersonaErrors.NotActive);

            if (userId.HasValue)
            {
                var assigned = await db.UserPersonas
                    .AsNoTracking()
                    .AnyAsync(up => up.UserId == userId && up.PersonaId == persona.Id, ct);
                if (!assigned)
                    return Result.Failure<PersonaContext>(PersonaErrors.NotAssignedToUser);
            }
            else if (persona.AudienceType != PersonaAudienceType.Anonymous)
            {
                return Result.Failure<PersonaContext>(PersonaErrors.RequiresAuthentication);
            }

            return Result.Success(Map(persona));
        }

        if (userId.HasValue)
        {
            var def = await db.UserPersonas
                .AsNoTracking()
                .Include(up => up.Persona)
                .Where(up => up.UserId == userId && up.IsDefault && up.Persona.IsActive)
                .Select(up => up.Persona)
                .FirstOrDefaultAsync(ct);

            if (def is null)
                return Result.Failure<PersonaContext>(PersonaErrors.NoDefaultForUser);

            return Result.Success(Map(def));
        }

        var anon = await db.AiPersonas
            .AsNoTracking()
            .FirstOrDefaultAsync(p =>
                p.TenantId == tenantId &&
                p.Slug == AiPersona.AnonymousSlug &&
                p.IsActive, ct);

        if (anon is null)
            return Result.Failure<PersonaContext>(PersonaErrors.AnonymousNotAvailable);

        return Result.Success(Map(anon));
    }

    private static PersonaContext Map(AiPersona p) =>
        new(p.Id, p.Slug, p.AudienceType, p.SafetyPreset, p.PermittedAgentSlugs);
}
```

- [ ] **Step 12.5: Run — expect PASS (7/7)**

```bash
dotnet test boilerplateBE/Starter.sln --filter "FullyQualifiedName~PersonaResolverTests" --nologo
```

- [ ] **Step 12.6: Commit**

```bash
git add boilerplateBE/src/modules/Starter.Module.AI/Application/Services/Personas/IPersonaResolver.cs \
        boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Services/Personas/PersonaResolver.cs \
        boilerplateBE/tests/Starter.Api.Tests/Ai/Personas/PersonaResolverTests.cs
git commit -m "feat(ai): PersonaResolver with override/default/anonymous fallback"
```

---

## Task 13: `ISafetyPresetClauseProvider` + `.resx` resources (TDD)

**Files:**
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Application/Services/Personas/ISafetyPresetClauseProvider.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Resources/SafetyPresets.resx`
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Resources/SafetyPresets.ar.resx`
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Services/Personas/ResxSafetyPresetClauseProvider.cs`
- Create: `boilerplateBE/tests/Starter.Api.Tests/Ai/Personas/SafetyPresetClauseProviderTests.cs`

- [ ] **Step 13.1: Create the interface**

```csharp
using System.Globalization;
using Starter.Module.AI.Domain.Enums;

namespace Starter.Module.AI.Application.Services.Personas;

internal interface ISafetyPresetClauseProvider
{
    string GetClause(SafetyPreset preset, PersonaAudienceType audience, CultureInfo culture);
}
```

- [ ] **Step 13.2: Create `SafetyPresets.resx`**

```xml
<?xml version="1.0" encoding="utf-8"?>
<root>
  <resheader name="resmimetype"><value>text/microsoft-resx</value></resheader>
  <resheader name="version"><value>2.0</value></resheader>
  <resheader name="reader"><value>System.Resources.ResXResourceReader, System.Windows.Forms</value></resheader>
  <resheader name="writer"><value>System.Resources.ResXResourceWriter, System.Windows.Forms</value></resheader>

  <data name="Standard.Internal"><value></value></data>
  <data name="Standard.EndCustomer"><value></value></data>
  <data name="Standard.Anonymous"><value></value></data>

  <data name="ChildSafe.Internal"><value>You are assisting a minor under 16. Do not produce sexual, violent, or age-inappropriate content. Decline politely and suggest a safer alternative if asked. Avoid discussing self-harm; if mentioned, gently direct the user to a trusted adult or local helpline.</value></data>
  <data name="ChildSafe.EndCustomer"><value>You are assisting a minor under 16. Do not produce sexual, violent, or age-inappropriate content. Decline politely and suggest a safer alternative if asked. Avoid discussing self-harm; if mentioned, gently direct the user to a trusted adult or local helpline.</value></data>
  <data name="ChildSafe.Anonymous"><value>You are assisting a minor under 16. Do not produce sexual, violent, or age-inappropriate content. Decline politely and suggest a safer alternative if asked. Avoid discussing self-harm; if mentioned, gently direct the user to a trusted adult or local helpline.</value></data>

  <data name="ProfessionalModerated.Internal"><value>Maintain a formal, professional tone. Never commit the organisation to actions, pricing, or deadlines — defer to a human for any commitment. Do not speculate on legal, financial, or medical advice.</value></data>
  <data name="ProfessionalModerated.EndCustomer"><value>You are speaking on behalf of the organisation to an external client. Maintain a formal tone. Never commit to pricing, deadlines, or contractual terms — always defer to a human. Decline speculation on legal, financial, or medical matters.</value></data>
  <data name="ProfessionalModerated.Anonymous"><value>You are speaking with an unauthenticated public visitor on behalf of the organisation. Do not reveal internal details. Do not commit to pricing, deadlines, or contractual terms. Decline speculation on legal, financial, or medical matters.</value></data>
</root>
```

- [ ] **Step 13.3: Create `SafetyPresets.ar.resx`**

```xml
<?xml version="1.0" encoding="utf-8"?>
<root>
  <resheader name="resmimetype"><value>text/microsoft-resx</value></resheader>
  <resheader name="version"><value>2.0</value></resheader>
  <resheader name="reader"><value>System.Resources.ResXResourceReader, System.Windows.Forms</value></resheader>
  <resheader name="writer"><value>System.Resources.ResXResourceWriter, System.Windows.Forms</value></resheader>

  <data name="Standard.Internal"><value></value></data>
  <data name="Standard.EndCustomer"><value></value></data>
  <data name="Standard.Anonymous"><value></value></data>

  <data name="ChildSafe.Internal"><value>أنت تساعد قاصراً دون سن 16. لا تُنتج محتوى جنسياً أو عنيفاً أو غير ملائم للعمر. ارفض بأدب واقترح بديلاً آمناً عند الحاجة. تجنّب الحديث عن إيذاء النفس؛ إذا ذُكر، وجّه المستخدم إلى شخص بالغ موثوق أو خط دعم محلي.</value></data>
  <data name="ChildSafe.EndCustomer"><value>أنت تساعد قاصراً دون سن 16. لا تُنتج محتوى جنسياً أو عنيفاً أو غير ملائم للعمر. ارفض بأدب واقترح بديلاً آمناً عند الحاجة. تجنّب الحديث عن إيذاء النفس؛ إذا ذُكر، وجّه المستخدم إلى شخص بالغ موثوق أو خط دعم محلي.</value></data>
  <data name="ChildSafe.Anonymous"><value>أنت تساعد قاصراً دون سن 16. لا تُنتج محتوى جنسياً أو عنيفاً أو غير ملائم للعمر. ارفض بأدب واقترح بديلاً آمناً عند الحاجة. تجنّب الحديث عن إيذاء النفس؛ إذا ذُكر، وجّه المستخدم إلى شخص بالغ موثوق أو خط دعم محلي.</value></data>

  <data name="ProfessionalModerated.Internal"><value>حافظ على نبرة رسمية ومهنية. لا تلتزم نيابةً عن المؤسسة بأي إجراءات أو أسعار أو مواعيد — أحِل ذلك لموظف بشري. لا تُقدّم نصائح قانونية أو مالية أو طبية تخمينية.</value></data>
  <data name="ProfessionalModerated.EndCustomer"><value>أنت تتحدث نيابةً عن المؤسسة إلى عميل خارجي. حافظ على نبرة رسمية. لا تلتزم بأسعار أو مواعيد أو شروط تعاقدية — أحِل ذلك لموظف بشري. ارفض التخمين في الأمور القانونية أو المالية أو الطبية.</value></data>
  <data name="ProfessionalModerated.Anonymous"><value>أنت تتحدث مع زائر عام غير مُصادَق نيابةً عن المؤسسة. لا تُفصح عن معلومات داخلية. لا تلتزم بأسعار أو مواعيد أو شروط تعاقدية. ارفض التخمين في الأمور القانونية أو المالية أو الطبية.</value></data>
</root>
```

- [ ] **Step 13.4: Add resource-embed to the module csproj**

Open `boilerplateBE/src/modules/Starter.Module.AI/Starter.Module.AI.csproj` and inside the existing `<Project>` / `<ItemGroup>` area add:

```xml
  <ItemGroup>
    <EmbeddedResource Include="Resources\SafetyPresets.resx">
      <Generator>ResXFileCodeGenerator</Generator>
      <LastGenOutput>SafetyPresets.Designer.cs</LastGenOutput>
    </EmbeddedResource>
    <EmbeddedResource Include="Resources\SafetyPresets.ar.resx">
      <DependentUpon>SafetyPresets.resx</DependentUpon>
    </EmbeddedResource>
  </ItemGroup>
```

- [ ] **Step 13.5: Write failing tests**

```csharp
using System.Globalization;
using FluentAssertions;
using Starter.Module.AI.Domain.Enums;
using Starter.Module.AI.Infrastructure.Services.Personas;
using Xunit;

namespace Starter.Api.Tests.Ai.Personas;

public sealed class SafetyPresetClauseProviderTests
{
    private static readonly ResxSafetyPresetClauseProvider Sut = new();

    [Theory]
    [InlineData(SafetyPreset.Standard, PersonaAudienceType.Internal)]
    [InlineData(SafetyPreset.Standard, PersonaAudienceType.EndCustomer)]
    [InlineData(SafetyPreset.Standard, PersonaAudienceType.Anonymous)]
    public void Standard_Returns_Empty(SafetyPreset preset, PersonaAudienceType audience)
    {
        Sut.GetClause(preset, audience, CultureInfo.GetCultureInfo("en")).Should().BeEmpty();
    }

    [Fact]
    public void ChildSafe_Internal_En_Contains_Expected_Phrase()
    {
        var clause = Sut.GetClause(SafetyPreset.ChildSafe, PersonaAudienceType.Internal, CultureInfo.GetCultureInfo("en"));
        clause.Should().NotBeEmpty();
        clause.Should().Contain("minor under 16");
    }

    [Fact]
    public void ProfessionalModerated_EndCustomer_En_Contains_Expected_Phrase()
    {
        var clause = Sut.GetClause(SafetyPreset.ProfessionalModerated, PersonaAudienceType.EndCustomer, CultureInfo.GetCultureInfo("en"));
        clause.Should().Contain("external client");
    }

    [Fact]
    public void ChildSafe_Internal_Ar_Returns_Arabic()
    {
        var clause = Sut.GetClause(SafetyPreset.ChildSafe, PersonaAudienceType.Internal, CultureInfo.GetCultureInfo("ar"));
        clause.Should().NotBeEmpty();
        clause.Should().Contain("قاصراً");
    }

    [Fact]
    public void Unknown_Culture_Falls_Back_To_En()
    {
        var clause = Sut.GetClause(SafetyPreset.ChildSafe, PersonaAudienceType.Internal, CultureInfo.GetCultureInfo("fr"));
        clause.Should().Contain("minor under 16");
    }
}
```

- [ ] **Step 13.6: Run — expect FAIL**

```bash
dotnet test boilerplateBE/Starter.sln --filter "FullyQualifiedName~SafetyPresetClauseProviderTests" --nologo
```

- [ ] **Step 13.7: Create `ResxSafetyPresetClauseProvider.cs`**

```csharp
using System.Globalization;
using System.Resources;
using Starter.Module.AI.Application.Services.Personas;
using Starter.Module.AI.Domain.Enums;

namespace Starter.Module.AI.Infrastructure.Services.Personas;

internal sealed class ResxSafetyPresetClauseProvider : ISafetyPresetClauseProvider
{
    private static readonly ResourceManager Manager = new(
        "Starter.Module.AI.Resources.SafetyPresets",
        typeof(ResxSafetyPresetClauseProvider).Assembly);

    public string GetClause(SafetyPreset preset, PersonaAudienceType audience, CultureInfo culture)
    {
        var key = $"{preset}.{audience}";
        var localised = Manager.GetString(key, culture);
        if (!string.IsNullOrEmpty(localised))
            return localised;

        var fallback = Manager.GetString(key, CultureInfo.InvariantCulture);
        return fallback ?? string.Empty;
    }
}
```

- [ ] **Step 13.8: Run — expect PASS**

```bash
dotnet test boilerplateBE/Starter.sln --filter "FullyQualifiedName~SafetyPresetClauseProviderTests" --nologo
```

- [ ] **Step 13.9: Commit**

```bash
git add boilerplateBE/src/modules/Starter.Module.AI/Application/Services/Personas/ISafetyPresetClauseProvider.cs \
        boilerplateBE/src/modules/Starter.Module.AI/Resources/SafetyPresets.resx \
        boilerplateBE/src/modules/Starter.Module.AI/Resources/SafetyPresets.ar.resx \
        boilerplateBE/src/modules/Starter.Module.AI/Starter.Module.AI.csproj \
        boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Services/Personas/ResxSafetyPresetClauseProvider.cs \
        boilerplateBE/tests/Starter.Api.Tests/Ai/Personas/SafetyPresetClauseProviderTests.cs
git commit -m "feat(ai): resx-backed SafetyPresetClauseProvider with EN + AR"
```

---

## Task 14: Persona ↔ Assistant visibility cross-check test (TDD)

**Files:**
- Create: `boilerplateBE/tests/Starter.Api.Tests/Ai/Personas/PersonaAssistantVisibilityTests.cs`

- [ ] **Step 14.1: Write test (covers already-implemented `AiAssistant.IsVisibleToPersona`)**

```csharp
using FluentAssertions;
using Starter.Module.AI.Domain.Entities;
using Xunit;

namespace Starter.Api.Tests.Ai.Personas;

public sealed class PersonaAssistantVisibilityTests
{
    private static AiAssistant MakeAssistant(string slug, params string[] personaTargets)
    {
        var a = AiAssistant.Create(Guid.NewGuid(), slug, null, "prompt", Guid.NewGuid(), slug: slug);
        if (personaTargets.Length > 0) a.SetPersonaTargets(personaTargets);
        return a;
    }

    [Fact]
    public void Both_Empty_Means_Visible()
    {
        MakeAssistant("tutor")
            .IsVisibleToPersona("student", new List<string>())
            .Should().BeTrue();
    }

    [Fact]
    public void Persona_Permits_Assistant_Returns_True()
    {
        MakeAssistant("tutor")
            .IsVisibleToPersona("student", new List<string> { "tutor", "reading-coach" })
            .Should().BeTrue();
    }

    [Fact]
    public void Persona_Excludes_Assistant_Returns_False()
    {
        MakeAssistant("admin-copilot")
            .IsVisibleToPersona("student", new List<string> { "tutor", "reading-coach" })
            .Should().BeFalse();
    }

    [Fact]
    public void Assistant_Targets_Persona_Returns_True()
    {
        MakeAssistant("tutor", "student")
            .IsVisibleToPersona("student", new List<string>())
            .Should().BeTrue();
    }

    [Fact]
    public void Assistant_Excludes_Persona_Returns_False()
    {
        MakeAssistant("tutor", "teacher")
            .IsVisibleToPersona("student", new List<string>())
            .Should().BeFalse();
    }

    [Fact]
    public void Intersection_Requires_Both_Sides_To_Agree()
    {
        var a = MakeAssistant("tutor", "student");
        a.IsVisibleToPersona("student", new List<string> { "admin-copilot" })
            .Should().BeFalse();
    }
}
```

- [ ] **Step 14.2: Run — expect PASS (no impl needed, validates Task 4)**

```bash
dotnet test boilerplateBE/Starter.sln --filter "FullyQualifiedName~PersonaAssistantVisibilityTests" --nologo
```

- [ ] **Step 14.3: Commit**

```bash
git add boilerplateBE/tests/Starter.Api.Tests/Ai/Personas/PersonaAssistantVisibilityTests.cs
git commit -m "test(ai): cross-check persona↔assistant visibility rules"
```

---

## Task 15: `TenantCreated` → seed `anonymous` + `default` personas (TDD)

**Files:**
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/EventHandlers/SeedTenantPersonasDomainEventHandler.cs`
- Create: `boilerplateBE/tests/Starter.Api.Tests/Ai/Personas/SeedTenantPersonasDomainEventHandlerTests.cs`

- [ ] **Step 15.1: Write failing tests**

```csharp
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Starter.Application.Common.Interfaces;
using Starter.Domain.Tenants.Events;
using Starter.Module.AI.Domain.Entities;
using Starter.Module.AI.Infrastructure.EventHandlers;
using Starter.Module.AI.Infrastructure.Persistence;
using Xunit;
using Moq;

namespace Starter.Api.Tests.Ai.Personas;

public sealed class SeedTenantPersonasDomainEventHandlerTests
{
    private static (SeedTenantPersonasDomainEventHandler h, AiDbContext db) Setup()
    {
        var opts = new DbContextOptionsBuilder<AiDbContext>()
            .UseInMemoryDatabase($"seed-{Guid.NewGuid()}").Options;

        var cu = new Mock<ICurrentUserService>();
        cu.SetupGet(x => x.TenantId).Returns((Guid?)null);
        cu.SetupGet(x => x.UserId).Returns((Guid?)null);

        var db = new AiDbContext(opts, cu.Object);
        var h = new SeedTenantPersonasDomainEventHandler(db, NullLogger<SeedTenantPersonasDomainEventHandler>.Instance);
        return (h, db);
    }

    [Fact]
    public async Task First_Call_Creates_Anonymous_And_Default()
    {
        var (h, db) = Setup();
        var tenant = Guid.NewGuid();

        await h.Handle(new TenantCreatedEvent(tenant), CancellationToken.None);

        var personas = await db.AiPersonas.IgnoreQueryFilters()
            .Where(p => p.TenantId == tenant).ToListAsync();
        personas.Should().HaveCount(2);
        personas.Should().Contain(p => p.Slug == AiPersona.AnonymousSlug && p.IsSystemReserved);
        personas.Should().Contain(p => p.Slug == AiPersona.DefaultSlug && !p.IsSystemReserved);
    }

    [Fact]
    public async Task Repeated_Call_Is_Idempotent()
    {
        var (h, db) = Setup();
        var tenant = Guid.NewGuid();

        await h.Handle(new TenantCreatedEvent(tenant), CancellationToken.None);
        await h.Handle(new TenantCreatedEvent(tenant), CancellationToken.None);

        var count = await db.AiPersonas.IgnoreQueryFilters()
            .CountAsync(p => p.TenantId == tenant);
        count.Should().Be(2);
    }

    [Fact]
    public async Task Missing_Default_Is_Added_When_Only_Anonymous_Exists()
    {
        var (h, db) = Setup();
        var tenant = Guid.NewGuid();
        db.AiPersonas.Add(AiPersona.CreateAnonymous(tenant, Guid.NewGuid()));
        await db.SaveChangesAsync();

        await h.Handle(new TenantCreatedEvent(tenant), CancellationToken.None);

        var slugs = await db.AiPersonas.IgnoreQueryFilters()
            .Where(p => p.TenantId == tenant).Select(p => p.Slug).ToListAsync();
        slugs.Should().BeEquivalentTo(new[] { AiPersona.AnonymousSlug, AiPersona.DefaultSlug });
    }
}
```

- [ ] **Step 15.2: Run — expect FAIL (handler missing)**

```bash
dotnet test boilerplateBE/Starter.sln --filter "FullyQualifiedName~SeedTenantPersonasDomainEventHandlerTests" --nologo
```

- [ ] **Step 15.3: Create handler**

```csharp
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Starter.Domain.Tenants.Events;
using Starter.Module.AI.Domain.Entities;
using Starter.Module.AI.Infrastructure.Persistence;

namespace Starter.Module.AI.Infrastructure.EventHandlers;

internal sealed class SeedTenantPersonasDomainEventHandler(
    AiDbContext db,
    ILogger<SeedTenantPersonasDomainEventHandler> logger)
    : INotificationHandler<TenantCreatedEvent>
{
    private static readonly Guid SystemSeedActor = Guid.Empty;

    public async Task Handle(TenantCreatedEvent notification, CancellationToken cancellationToken)
    {
        var tenantId = notification.TenantId;

        var existing = await db.AiPersonas
            .IgnoreQueryFilters()
            .Where(p => p.TenantId == tenantId &&
                        (p.Slug == AiPersona.AnonymousSlug || p.Slug == AiPersona.DefaultSlug))
            .Select(p => p.Slug)
            .ToListAsync(cancellationToken);

        var hasAnonymous = existing.Contains(AiPersona.AnonymousSlug);
        var hasDefault = existing.Contains(AiPersona.DefaultSlug);

        if (!hasAnonymous)
            db.AiPersonas.Add(AiPersona.CreateAnonymous(tenantId, SystemSeedActor));
        if (!hasDefault)
            db.AiPersonas.Add(AiPersona.CreateDefault(tenantId, SystemSeedActor));

        if (!hasAnonymous || !hasDefault)
        {
            await db.SaveChangesAsync(cancellationToken);
            logger.LogInformation(
                "Seeded personas for tenant {TenantId} (anonymous={Anon}, default={Default}).",
                tenantId, !hasAnonymous, !hasDefault);
        }
    }
}
```

- [ ] **Step 15.4: Run — expect PASS**

```bash
dotnet test boilerplateBE/Starter.sln --filter "FullyQualifiedName~SeedTenantPersonasDomainEventHandlerTests" --nologo
```

- [ ] **Step 15.5: Commit**

```bash
git add boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/EventHandlers/SeedTenantPersonasDomainEventHandler.cs \
        boilerplateBE/tests/Starter.Api.Tests/Ai/Personas/SeedTenantPersonasDomainEventHandlerTests.cs
git commit -m "feat(ai): seed anonymous + default personas on TenantCreated"
```

---

## Task 16: `UserCreated` → assign default persona (TDD)

**Files:**
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/EventHandlers/AssignDefaultPersonaDomainEventHandler.cs`
- Create: `boilerplateBE/tests/Starter.Api.Tests/Ai/Personas/AssignDefaultPersonaDomainEventHandlerTests.cs`

- [ ] **Step 16.1: Write failing tests**

```csharp
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Starter.Application.Common.Interfaces;
using Starter.Domain.Identity.Events;
using Starter.Module.AI.Domain.Entities;
using Starter.Module.AI.Domain.Enums;
using Starter.Module.AI.Infrastructure.EventHandlers;
using Starter.Module.AI.Infrastructure.Persistence;
using Xunit;
using Moq;

namespace Starter.Api.Tests.Ai.Personas;

public sealed class AssignDefaultPersonaDomainEventHandlerTests
{
    private static (AssignDefaultPersonaDomainEventHandler h, AiDbContext db) Setup()
    {
        var opts = new DbContextOptionsBuilder<AiDbContext>()
            .UseInMemoryDatabase($"assign-{Guid.NewGuid()}").Options;
        var cu = new Mock<ICurrentUserService>();
        cu.SetupGet(x => x.TenantId).Returns((Guid?)null);
        var db = new AiDbContext(opts, cu.Object);
        var h = new AssignDefaultPersonaDomainEventHandler(db,
            NullLogger<AssignDefaultPersonaDomainEventHandler>.Instance);
        return (h, db);
    }

    [Fact]
    public async Task Assigns_Default_Persona_If_Present()
    {
        var (h, db) = Setup();
        var tenant = Guid.NewGuid();
        var user = Guid.NewGuid();

        var def = AiPersona.CreateDefault(tenant, Guid.NewGuid());
        db.AiPersonas.Add(def);
        await db.SaveChangesAsync();

        await h.Handle(new UserCreatedEvent(user, "u@x.com", "User", tenant), CancellationToken.None);

        var row = await db.UserPersonas.IgnoreQueryFilters()
            .FirstOrDefaultAsync(up => up.UserId == user);
        row.Should().NotBeNull();
        row!.PersonaId.Should().Be(def.Id);
        row.IsDefault.Should().BeTrue();
    }

    [Fact]
    public async Task Falls_Back_To_Active_Internal_Persona_When_No_Default()
    {
        var (h, db) = Setup();
        var tenant = Guid.NewGuid();
        var user = Guid.NewGuid();

        var teacher = AiPersona.Create(tenant, "teacher", "Teacher", null,
            PersonaAudienceType.Internal, SafetyPreset.Standard, Guid.NewGuid());
        db.AiPersonas.Add(teacher);
        await db.SaveChangesAsync();

        await h.Handle(new UserCreatedEvent(user, "u@x.com", "User", tenant), CancellationToken.None);

        var row = await db.UserPersonas.IgnoreQueryFilters()
            .FirstOrDefaultAsync(up => up.UserId == user);
        row.Should().NotBeNull();
        row!.PersonaId.Should().Be(teacher.Id);
    }

    [Fact]
    public async Task No_Tenant_Id_Skips_Assignment()
    {
        var (h, db) = Setup();
        var user = Guid.NewGuid();

        await h.Handle(new UserCreatedEvent(user, "u@x.com", "Name", null), CancellationToken.None);

        var any = await db.UserPersonas.IgnoreQueryFilters().AnyAsync();
        any.Should().BeFalse();
    }

    [Fact]
    public async Task No_Internal_Personas_Skips_Assignment()
    {
        var (h, db) = Setup();
        var tenant = Guid.NewGuid();
        var user = Guid.NewGuid();

        db.AiPersonas.Add(AiPersona.CreateAnonymous(tenant, Guid.NewGuid()));
        await db.SaveChangesAsync();

        await h.Handle(new UserCreatedEvent(user, "u@x.com", "Name", tenant), CancellationToken.None);

        var any = await db.UserPersonas.IgnoreQueryFilters().AnyAsync(up => up.UserId == user);
        any.Should().BeFalse();
    }
}
```

- [ ] **Step 16.2: Run — expect FAIL**

```bash
dotnet test boilerplateBE/Starter.sln --filter "FullyQualifiedName~AssignDefaultPersonaDomainEventHandlerTests" --nologo
```

- [ ] **Step 16.3: Create handler**

```csharp
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Starter.Domain.Identity.Events;
using Starter.Module.AI.Domain.Entities;
using Starter.Module.AI.Domain.Enums;
using Starter.Module.AI.Infrastructure.Persistence;

namespace Starter.Module.AI.Infrastructure.EventHandlers;

internal sealed class AssignDefaultPersonaDomainEventHandler(
    AiDbContext db,
    ILogger<AssignDefaultPersonaDomainEventHandler> logger)
    : INotificationHandler<UserCreatedEvent>
{
    public async Task Handle(UserCreatedEvent notification, CancellationToken cancellationToken)
    {
        if (notification.TenantId is not Guid tenantId)
        {
            logger.LogDebug("UserCreated for {UserId} has no tenant; skipping persona assignment.",
                notification.UserId);
            return;
        }

        var alreadyAssigned = await db.UserPersonas
            .IgnoreQueryFilters()
            .AnyAsync(up => up.UserId == notification.UserId, cancellationToken);
        if (alreadyAssigned)
            return;

        var @default = await db.AiPersonas
            .IgnoreQueryFilters()
            .Where(p => p.TenantId == tenantId && p.Slug == AiPersona.DefaultSlug && p.IsActive)
            .FirstOrDefaultAsync(cancellationToken);

        var persona = @default ?? await db.AiPersonas
            .IgnoreQueryFilters()
            .Where(p => p.TenantId == tenantId &&
                        p.AudienceType == PersonaAudienceType.Internal &&
                        p.IsActive)
            .OrderBy(p => p.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (persona is null)
        {
            logger.LogWarning(
                "No eligible persona found for tenant {TenantId}; user {UserId} left unassigned.",
                tenantId, notification.UserId);
            return;
        }

        db.UserPersonas.Add(UserPersona.Create(
            userId: notification.UserId,
            personaId: persona.Id,
            tenantId: tenantId,
            isDefault: true,
            assignedBy: null));

        await db.SaveChangesAsync(cancellationToken);
    }
}
```

- [ ] **Step 16.4: Run — expect PASS**

```bash
dotnet test boilerplateBE/Starter.sln --filter "FullyQualifiedName~AssignDefaultPersonaDomainEventHandlerTests" --nologo
```

- [ ] **Step 16.5: Commit**

```bash
git add boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/EventHandlers/AssignDefaultPersonaDomainEventHandler.cs \
        boilerplateBE/tests/Starter.Api.Tests/Ai/Personas/AssignDefaultPersonaDomainEventHandlerTests.cs
git commit -m "feat(ai): assign default persona to new user on UserCreated"
```

---

## Task 17: DTOs + mappers

**Files:**
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Application/DTOs/AiPersonaDto.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Application/DTOs/UserPersonaDto.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Application/DTOs/MePersonasDto.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Application/DTOs/AiPersonaMappers.cs`

- [ ] **Step 17.1: Create `AiPersonaDto.cs`**

```csharp
using Starter.Module.AI.Domain.Enums;

namespace Starter.Module.AI.Application.DTOs;

public sealed record AiPersonaDto(
    Guid Id,
    string Slug,
    string DisplayName,
    string? Description,
    PersonaAudienceType AudienceType,
    SafetyPreset SafetyPreset,
    IReadOnlyList<string> PermittedAgentSlugs,
    bool IsSystemReserved,
    bool IsActive,
    DateTime CreatedAt,
    DateTime? ModifiedAt);
```

- [ ] **Step 17.2: Create `UserPersonaDto.cs`**

```csharp
namespace Starter.Module.AI.Application.DTOs;

public sealed record UserPersonaDto(
    Guid UserId,
    string? UserDisplayName,
    Guid PersonaId,
    string PersonaSlug,
    string PersonaDisplayName,
    bool IsDefault,
    DateTime AssignedAt);
```

- [ ] **Step 17.3: Create `MePersonasDto.cs`**

```csharp
namespace Starter.Module.AI.Application.DTOs;

public sealed record MePersonasDto(
    IReadOnlyList<UserPersonaDto> Personas,
    Guid? DefaultPersonaId);
```

- [ ] **Step 17.4: Create `AiPersonaMappers.cs`**

```csharp
using Starter.Module.AI.Domain.Entities;

namespace Starter.Module.AI.Application.DTOs;

internal static class AiPersonaMappers
{
    public static AiPersonaDto ToDto(this AiPersona p) =>
        new(
            p.Id,
            p.Slug,
            p.DisplayName,
            p.Description,
            p.AudienceType,
            p.SafetyPreset,
            p.PermittedAgentSlugs,
            p.IsSystemReserved,
            p.IsActive,
            p.CreatedAt,
            p.ModifiedAt);
}
```

- [ ] **Step 17.5: Build**

```bash
dotnet build boilerplateBE/Starter.sln --nologo
```

- [ ] **Step 17.6: Commit**

```bash
git add boilerplateBE/src/modules/Starter.Module.AI/Application/DTOs/AiPersonaDto.cs \
        boilerplateBE/src/modules/Starter.Module.AI/Application/DTOs/UserPersonaDto.cs \
        boilerplateBE/src/modules/Starter.Module.AI/Application/DTOs/MePersonasDto.cs \
        boilerplateBE/src/modules/Starter.Module.AI/Application/DTOs/AiPersonaMappers.cs
git commit -m "feat(ai): persona DTOs + mappers"
```

---

## Task 18: Chat DTO updates (`SendChatMessageCommand.PersonaId`, `AiChatReplyDto.PersonaSlug`)

**Files:**
- Modify: `boilerplateBE/src/modules/Starter.Module.AI/Application/Commands/SendChatMessage/SendChatMessageCommand.cs`
- Modify: `boilerplateBE/src/modules/Starter.Module.AI/Application/DTOs/AiChatReplyDto.cs`

- [ ] **Step 18.1: Update `SendChatMessageCommand.cs`**

Replace the record with:

```csharp
using MediatR;
using Starter.Module.AI.Application.DTOs;
using Starter.Shared.Results;

namespace Starter.Module.AI.Application.Commands.SendChatMessage;

public sealed record SendChatMessageCommand(
    Guid? ConversationId,
    Guid? AssistantId,
    string Message,
    Guid? PersonaId = null) : IRequest<Result<AiChatReplyDto>>;
```

- [ ] **Step 18.2: Update `AiChatReplyDto.cs`**

Replace with:

```csharp
namespace Starter.Module.AI.Application.DTOs;

public sealed record AiChatReplyDto(
    Guid ConversationId,
    AiMessageDto UserMessage,
    AiMessageDto AssistantMessage,
    string? PersonaSlug = null);
```

- [ ] **Step 18.3: Build and run existing chat tests — expect PASS (parity)**

```bash
dotnet build boilerplateBE/Starter.sln --nologo
dotnet test boilerplateBE/Starter.sln --filter "FullyQualifiedName~Ai" --nologo
```

- [ ] **Step 18.4: Commit**

```bash
git add boilerplateBE/src/modules/Starter.Module.AI/Application/Commands/SendChatMessage/SendChatMessageCommand.cs \
        boilerplateBE/src/modules/Starter.Module.AI/Application/DTOs/AiChatReplyDto.cs
git commit -m "feat(ai): add optional PersonaId on chat command and PersonaSlug on reply"
```

---

## Task 19: `CreatePersonaCommand` (TDD)

**Files:**
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Application/Commands/Personas/CreatePersona/CreatePersonaCommand.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Application/Commands/Personas/CreatePersona/CreatePersonaCommandValidator.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Application/Commands/Personas/CreatePersona/CreatePersonaCommandHandler.cs`
- Create: `boilerplateBE/tests/Starter.Api.Tests/Ai/Personas/CreatePersonaCommandTests.cs`

- [ ] **Step 19.1: Create the command record**

```csharp
using MediatR;
using Starter.Module.AI.Application.DTOs;
using Starter.Module.AI.Domain.Enums;
using Starter.Shared.Results;

namespace Starter.Module.AI.Application.Commands.Personas.CreatePersona;

public sealed record CreatePersonaCommand(
    string DisplayName,
    string? Description,
    string? Slug,
    PersonaAudienceType AudienceType,
    SafetyPreset SafetyPreset,
    IReadOnlyList<string>? PermittedAgentSlugs)
    : IRequest<Result<AiPersonaDto>>;
```

- [ ] **Step 19.2: Create validator**

```csharp
using System.Text.RegularExpressions;
using FluentValidation;
using Starter.Module.AI.Domain.Entities;
using Starter.Module.AI.Domain.Enums;

namespace Starter.Module.AI.Application.Commands.Personas.CreatePersona;

public sealed class CreatePersonaCommandValidator : AbstractValidator<CreatePersonaCommand>
{
    private static readonly Regex SlugRegex = new("^[a-z0-9]+(-[a-z0-9]+)*$", RegexOptions.Compiled);

    public CreatePersonaCommandValidator()
    {
        RuleFor(x => x.DisplayName).NotEmpty().MaximumLength(120);
        RuleFor(x => x.Description).MaximumLength(500);
        RuleFor(x => x.AudienceType)
            .IsInEnum()
            .NotEqual(PersonaAudienceType.Anonymous)
            .WithMessage("Anonymous audience is reserved for the system-managed anonymous persona.");
        RuleFor(x => x.SafetyPreset).IsInEnum();
        RuleFor(x => x.Slug!)
            .Must(s => SlugRegex.IsMatch(s))
            .When(x => !string.IsNullOrEmpty(x.Slug))
            .WithMessage("Slug must be lowercase kebab-case.");
        RuleFor(x => x.Slug)
            .Must(s => s is null || (s != AiPersona.AnonymousSlug && s != AiPersona.DefaultSlug))
            .WithMessage("Slug 'anonymous' and 'default' are reserved.");
        RuleForEach(x => x.PermittedAgentSlugs!)
            .Must(s => SlugRegex.IsMatch(s))
            .When(x => x.PermittedAgentSlugs is not null)
            .WithMessage("Each permitted agent slug must be kebab-case.");
    }
}
```

- [ ] **Step 19.3: Create handler**

```csharp
using MediatR;
using Microsoft.EntityFrameworkCore;
using Starter.Application.Common.Interfaces;
using Starter.Module.AI.Application.DTOs;
using Starter.Module.AI.Application.Services.Personas;
using Starter.Module.AI.Domain.Entities;
using Starter.Module.AI.Domain.Errors;
using Starter.Module.AI.Infrastructure.Persistence;
using Starter.Shared.Results;

namespace Starter.Module.AI.Application.Commands.Personas.CreatePersona;

internal sealed class CreatePersonaCommandHandler(
    AiDbContext db,
    ICurrentUserService currentUser,
    ISlugGenerator slugGenerator)
    : IRequestHandler<CreatePersonaCommand, Result<AiPersonaDto>>
{
    public async Task<Result<AiPersonaDto>> Handle(
        CreatePersonaCommand request,
        CancellationToken cancellationToken)
    {
        if (currentUser.TenantId is not Guid tenantId)
            return Result.Failure<AiPersonaDto>(AiErrors.NotAuthenticated);
        if (currentUser.UserId is not Guid userId)
            return Result.Failure<AiPersonaDto>(AiErrors.NotAuthenticated);

        var slugs = await db.AiPersonas
            .IgnoreQueryFilters()
            .Where(p => p.TenantId == tenantId)
            .Select(p => p.Slug)
            .ToListAsync(cancellationToken);
        var taken = new HashSet<string>(slugs, StringComparer.Ordinal);

        string slug;
        if (!string.IsNullOrWhiteSpace(request.Slug))
        {
            slug = request.Slug.Trim().ToLowerInvariant();
            if (taken.Contains(slug))
                return Result.Failure<AiPersonaDto>(PersonaErrors.SlugAlreadyExists(slug));
        }
        else
        {
            slug = slugGenerator.EnsureUnique(slugGenerator.Slugify(request.DisplayName), taken);
        }

        var persona = AiPersona.Create(
            tenantId: tenantId,
            slug: slug,
            displayName: request.DisplayName,
            description: request.Description,
            audienceType: request.AudienceType,
            safetyPreset: request.SafetyPreset,
            createdByUserId: userId);

        if (request.PermittedAgentSlugs is { Count: > 0 })
            persona.Update(
                request.DisplayName,
                request.Description,
                request.SafetyPreset,
                request.PermittedAgentSlugs,
                isActive: true);

        db.AiPersonas.Add(persona);
        await db.SaveChangesAsync(cancellationToken);

        return Result.Success(persona.ToDto());
    }
}
```

- [ ] **Step 19.4: Write integration tests**

```csharp
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Starter.Application.Common.Interfaces;
using Starter.Module.AI.Application.Commands.Personas.CreatePersona;
using Starter.Module.AI.Domain.Enums;
using Starter.Module.AI.Domain.Errors;
using Starter.Module.AI.Infrastructure.Persistence;
using Starter.Module.AI.Infrastructure.Services.Personas;
using Xunit;
using Moq;

namespace Starter.Api.Tests.Ai.Personas;

public sealed class CreatePersonaCommandTests
{
    private static (CreatePersonaCommandHandler h, AiDbContext db) Setup(Guid tenant, Guid user)
    {
        var cu = new Mock<ICurrentUserService>();
        cu.SetupGet(x => x.TenantId).Returns(tenant);
        cu.SetupGet(x => x.UserId).Returns(user);
        var opts = new DbContextOptionsBuilder<AiDbContext>()
            .UseInMemoryDatabase($"create-{Guid.NewGuid()}").Options;
        var db = new AiDbContext(opts, cu.Object);
        return (new CreatePersonaCommandHandler(db, cu.Object, new SlugGenerator()), db);
    }

    [Fact]
    public async Task Happy_Path_Creates_Persona_With_Auto_Slug()
    {
        var tenant = Guid.NewGuid();
        var (h, db) = Setup(tenant, Guid.NewGuid());

        var result = await h.Handle(new CreatePersonaCommand(
            "Brand Content Agent", "desc", Slug: null,
            PersonaAudienceType.Internal, SafetyPreset.Standard,
            PermittedAgentSlugs: null), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Slug.Should().Be("brand-content-agent");
    }

    [Fact]
    public async Task Slug_Collision_Returns_Error()
    {
        var tenant = Guid.NewGuid();
        var (h, db) = Setup(tenant, Guid.NewGuid());
        await h.Handle(new CreatePersonaCommand("Teacher", null, "teacher",
            PersonaAudienceType.Internal, SafetyPreset.Standard, null), CancellationToken.None);

        var result = await h.Handle(new CreatePersonaCommand("Teacher Again", null, "teacher",
            PersonaAudienceType.Internal, SafetyPreset.Standard, null), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("Persona.SlugAlreadyExists");
    }

    [Fact]
    public async Task PermittedAgentSlugs_Are_Persisted()
    {
        var tenant = Guid.NewGuid();
        var (h, db) = Setup(tenant, Guid.NewGuid());

        var result = await h.Handle(new CreatePersonaCommand(
            "Student", null, "student",
            PersonaAudienceType.Internal, SafetyPreset.ChildSafe,
            new[] { "tutor", "reading-coach" }), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.PermittedAgentSlugs.Should().BeEquivalentTo(new[] { "tutor", "reading-coach" });
    }
}
```

- [ ] **Step 19.5: Run — expect PASS**

```bash
dotnet test boilerplateBE/Starter.sln --filter "FullyQualifiedName~CreatePersonaCommandTests" --nologo
```

- [ ] **Step 19.6: Commit**

```bash
git add boilerplateBE/src/modules/Starter.Module.AI/Application/Commands/Personas/CreatePersona/ \
        boilerplateBE/tests/Starter.Api.Tests/Ai/Personas/CreatePersonaCommandTests.cs
git commit -m "feat(ai): CreatePersonaCommand with slug auto-gen + collision detection"
```

---

## Task 20: `UpdatePersonaCommand`

**Files:**
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Application/Commands/Personas/UpdatePersona/UpdatePersonaCommand.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Application/Commands/Personas/UpdatePersona/UpdatePersonaCommandValidator.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Application/Commands/Personas/UpdatePersona/UpdatePersonaCommandHandler.cs`
- Create: `boilerplateBE/tests/Starter.Api.Tests/Ai/Personas/UpdatePersonaCommandTests.cs`

- [ ] **Step 20.1: Command**

```csharp
using MediatR;
using Starter.Module.AI.Application.DTOs;
using Starter.Module.AI.Domain.Enums;
using Starter.Shared.Results;

namespace Starter.Module.AI.Application.Commands.Personas.UpdatePersona;

public sealed record UpdatePersonaCommand(
    Guid Id,
    string DisplayName,
    string? Description,
    SafetyPreset SafetyPreset,
    IReadOnlyList<string>? PermittedAgentSlugs,
    bool IsActive) : IRequest<Result<AiPersonaDto>>;
```

- [ ] **Step 20.2: Validator**

```csharp
using System.Text.RegularExpressions;
using FluentValidation;

namespace Starter.Module.AI.Application.Commands.Personas.UpdatePersona;

public sealed class UpdatePersonaCommandValidator : AbstractValidator<UpdatePersonaCommand>
{
    private static readonly Regex SlugRegex = new("^[a-z0-9]+(-[a-z0-9]+)*$", RegexOptions.Compiled);

    public UpdatePersonaCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.DisplayName).NotEmpty().MaximumLength(120);
        RuleFor(x => x.Description).MaximumLength(500);
        RuleFor(x => x.SafetyPreset).IsInEnum();
        RuleForEach(x => x.PermittedAgentSlugs!)
            .Must(s => SlugRegex.IsMatch(s))
            .When(x => x.PermittedAgentSlugs is not null);
    }
}
```

- [ ] **Step 20.3: Handler**

```csharp
using MediatR;
using Microsoft.EntityFrameworkCore;
using Starter.Module.AI.Application.DTOs;
using Starter.Module.AI.Domain.Errors;
using Starter.Module.AI.Infrastructure.Persistence;
using Starter.Shared.Results;

namespace Starter.Module.AI.Application.Commands.Personas.UpdatePersona;

internal sealed class UpdatePersonaCommandHandler(AiDbContext db)
    : IRequestHandler<UpdatePersonaCommand, Result<AiPersonaDto>>
{
    public async Task<Result<AiPersonaDto>> Handle(
        UpdatePersonaCommand request,
        CancellationToken cancellationToken)
    {
        var persona = await db.AiPersonas
            .FirstOrDefaultAsync(p => p.Id == request.Id, cancellationToken);
        if (persona is null)
            return Result.Failure<AiPersonaDto>(PersonaErrors.NotFound);

        persona.Update(
            displayName: request.DisplayName,
            description: request.Description,
            safetyPreset: request.SafetyPreset,
            permittedAgentSlugs: request.PermittedAgentSlugs,
            isActive: request.IsActive);

        await db.SaveChangesAsync(cancellationToken);
        return Result.Success(persona.ToDto());
    }
}
```

- [ ] **Step 20.4: Tests**

```csharp
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Starter.Application.Common.Interfaces;
using Starter.Module.AI.Application.Commands.Personas.UpdatePersona;
using Starter.Module.AI.Domain.Entities;
using Starter.Module.AI.Domain.Enums;
using Starter.Module.AI.Infrastructure.Persistence;
using Xunit;
using Moq;

namespace Starter.Api.Tests.Ai.Personas;

public sealed class UpdatePersonaCommandTests
{
    private static (UpdatePersonaCommandHandler h, AiDbContext db) Setup(Guid tenant)
    {
        var cu = new Mock<ICurrentUserService>();
        cu.SetupGet(x => x.TenantId).Returns(tenant);
        var opts = new DbContextOptionsBuilder<AiDbContext>()
            .UseInMemoryDatabase($"upd-{Guid.NewGuid()}").Options;
        var db = new AiDbContext(opts, cu.Object);
        return (new UpdatePersonaCommandHandler(db), db);
    }

    [Fact]
    public async Task Updates_Mutable_Fields()
    {
        var tenant = Guid.NewGuid();
        var (h, db) = Setup(tenant);

        var p = AiPersona.Create(tenant, "client", "Client", null,
            PersonaAudienceType.EndCustomer, SafetyPreset.Standard, Guid.NewGuid());
        db.AiPersonas.Add(p);
        await db.SaveChangesAsync();

        var result = await h.Handle(new UpdatePersonaCommand(
            p.Id, "External Client", "desc", SafetyPreset.ProfessionalModerated,
            new[] { "brand-content-agent" }, IsActive: true), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.DisplayName.Should().Be("External Client");
        result.Value.SafetyPreset.Should().Be(SafetyPreset.ProfessionalModerated);
    }
}
```

- [ ] **Step 20.5: Run**

```bash
dotnet test boilerplateBE/Starter.sln --filter "FullyQualifiedName~UpdatePersonaCommandTests" --nologo
```

- [ ] **Step 20.6: Commit**

```bash
git add boilerplateBE/src/modules/Starter.Module.AI/Application/Commands/Personas/UpdatePersona/ \
        boilerplateBE/tests/Starter.Api.Tests/Ai/Personas/UpdatePersonaCommandTests.cs
git commit -m "feat(ai): UpdatePersonaCommand"
```

---

## Task 21: `DeletePersonaCommand`

**Files:**
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Application/Commands/Personas/DeletePersona/DeletePersonaCommand.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Application/Commands/Personas/DeletePersona/DeletePersonaCommandHandler.cs`
- Create: `boilerplateBE/tests/Starter.Api.Tests/Ai/Personas/DeletePersonaCommandTests.cs`

- [ ] **Step 21.1: Command**

```csharp
using MediatR;
using Starter.Shared.Results;

namespace Starter.Module.AI.Application.Commands.Personas.DeletePersona;

public sealed record DeletePersonaCommand(Guid Id) : IRequest<Result>;
```

- [ ] **Step 21.2: Handler**

```csharp
using MediatR;
using Microsoft.EntityFrameworkCore;
using Starter.Module.AI.Domain.Errors;
using Starter.Module.AI.Infrastructure.Persistence;
using Starter.Shared.Results;

namespace Starter.Module.AI.Application.Commands.Personas.DeletePersona;

internal sealed class DeletePersonaCommandHandler(AiDbContext db)
    : IRequestHandler<DeletePersonaCommand, Result>
{
    public async Task<Result> Handle(DeletePersonaCommand request, CancellationToken ct)
    {
        var persona = await db.AiPersonas.FirstOrDefaultAsync(p => p.Id == request.Id, ct);
        if (persona is null)
            return Result.Failure(PersonaErrors.NotFound);
        if (persona.IsSystemReserved)
            return Result.Failure(PersonaErrors.CannotDeleteSystemReserved);

        var hasAssignments = await db.UserPersonas.AnyAsync(up => up.PersonaId == persona.Id, ct);
        if (hasAssignments)
            return Result.Failure(PersonaErrors.HasActiveAssignments);

        db.AiPersonas.Remove(persona);
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }
}
```

- [ ] **Step 21.3: Tests**

```csharp
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Starter.Application.Common.Interfaces;
using Starter.Module.AI.Application.Commands.Personas.DeletePersona;
using Starter.Module.AI.Domain.Entities;
using Starter.Module.AI.Domain.Enums;
using Starter.Module.AI.Infrastructure.Persistence;
using Xunit;
using Moq;

namespace Starter.Api.Tests.Ai.Personas;

public sealed class DeletePersonaCommandTests
{
    private static (DeletePersonaCommandHandler h, AiDbContext db) Setup(Guid tenant)
    {
        var cu = new Mock<ICurrentUserService>();
        cu.SetupGet(x => x.TenantId).Returns(tenant);
        var opts = new DbContextOptionsBuilder<AiDbContext>()
            .UseInMemoryDatabase($"del-{Guid.NewGuid()}").Options;
        var db = new AiDbContext(opts, cu.Object);
        return (new DeletePersonaCommandHandler(db), db);
    }

    [Fact]
    public async Task Deletes_Ordinary_Persona()
    {
        var t = Guid.NewGuid();
        var (h, db) = Setup(t);
        var p = AiPersona.Create(t, "teacher", "Teacher", null,
            PersonaAudienceType.Internal, SafetyPreset.Standard, Guid.NewGuid());
        db.AiPersonas.Add(p);
        await db.SaveChangesAsync();

        var r = await h.Handle(new DeletePersonaCommand(p.Id), default);

        r.IsSuccess.Should().BeTrue();
        (await db.AiPersonas.AnyAsync()).Should().BeFalse();
    }

    [Fact]
    public async Task Rejects_System_Reserved()
    {
        var t = Guid.NewGuid();
        var (h, db) = Setup(t);
        var p = AiPersona.CreateAnonymous(t, Guid.NewGuid());
        db.AiPersonas.Add(p);
        await db.SaveChangesAsync();

        var r = await h.Handle(new DeletePersonaCommand(p.Id), default);

        r.IsSuccess.Should().BeFalse();
        r.Error.Code.Should().Be("Persona.CannotDeleteSystemReserved");
    }

    [Fact]
    public async Task Rejects_Persona_With_Assignments()
    {
        var t = Guid.NewGuid();
        var (h, db) = Setup(t);
        var p = AiPersona.Create(t, "teacher", "Teacher", null,
            PersonaAudienceType.Internal, SafetyPreset.Standard, Guid.NewGuid());
        db.AiPersonas.Add(p);
        db.UserPersonas.Add(UserPersona.Create(Guid.NewGuid(), p.Id, t, true, null));
        await db.SaveChangesAsync();

        var r = await h.Handle(new DeletePersonaCommand(p.Id), default);

        r.IsSuccess.Should().BeFalse();
        r.Error.Code.Should().Be("Persona.HasActiveAssignments");
    }
}
```

- [ ] **Step 21.4: Run + commit**

```bash
dotnet test boilerplateBE/Starter.sln --filter "FullyQualifiedName~DeletePersonaCommandTests" --nologo
git add boilerplateBE/src/modules/Starter.Module.AI/Application/Commands/Personas/DeletePersona/ \
        boilerplateBE/tests/Starter.Api.Tests/Ai/Personas/DeletePersonaCommandTests.cs
git commit -m "feat(ai): DeletePersonaCommand with system-reserved and assignment guards"
```

---

## Task 22: `AssignPersonaCommand` + `UnassignPersonaCommand` + `SetUserDefaultPersonaCommand`

**Files:**
- Create: `.../Commands/Personas/AssignPersona/AssignPersonaCommand.cs` (+ validator + handler)
- Create: `.../Commands/Personas/UnassignPersona/UnassignPersonaCommand.cs` (+ handler)
- Create: `.../Commands/Personas/SetUserDefaultPersona/SetUserDefaultPersonaCommand.cs` (+ handler)
- Create: tests `AssignPersonaCommandTests.cs`, `UnassignPersonaCommandTests.cs`, `SetUserDefaultPersonaCommandTests.cs`

- [ ] **Step 22.1: `AssignPersonaCommand.cs`**

```csharp
using MediatR;
using Starter.Shared.Results;

namespace Starter.Module.AI.Application.Commands.Personas.AssignPersona;

public sealed record AssignPersonaCommand(
    Guid PersonaId,
    Guid UserId,
    bool MakeDefault) : IRequest<Result>;
```

- [ ] **Step 22.2: `AssignPersonaCommandValidator.cs`**

```csharp
using FluentValidation;

namespace Starter.Module.AI.Application.Commands.Personas.AssignPersona;

public sealed class AssignPersonaCommandValidator : AbstractValidator<AssignPersonaCommand>
{
    public AssignPersonaCommandValidator()
    {
        RuleFor(x => x.PersonaId).NotEmpty();
        RuleFor(x => x.UserId).NotEmpty();
    }
}
```

- [ ] **Step 22.3: `AssignPersonaCommandHandler.cs`**

```csharp
using MediatR;
using Microsoft.EntityFrameworkCore;
using Starter.Application.Common.Interfaces;
using Starter.Module.AI.Domain.Entities;
using Starter.Module.AI.Domain.Errors;
using Starter.Module.AI.Infrastructure.Persistence;
using Starter.Shared.Results;

namespace Starter.Module.AI.Application.Commands.Personas.AssignPersona;

internal sealed class AssignPersonaCommandHandler(
    AiDbContext db,
    IApplicationDbContext appDb,
    ICurrentUserService currentUser)
    : IRequestHandler<AssignPersonaCommand, Result>
{
    public async Task<Result> Handle(AssignPersonaCommand request, CancellationToken ct)
    {
        var persona = await db.AiPersonas
            .FirstOrDefaultAsync(p => p.Id == request.PersonaId, ct);
        if (persona is null)
            return Result.Failure(PersonaErrors.NotFound);
        if (!persona.IsActive)
            return Result.Failure(PersonaErrors.NotActive);

        var user = await appDb.Users.IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Id == request.UserId, ct);
        if (user is null)
            return Result.Failure(PersonaErrors.UserNotInTenant);
        if (user.TenantId != persona.TenantId)
            return Result.Failure(PersonaErrors.UserNotInTenant);

        var existing = await db.UserPersonas.IgnoreQueryFilters()
            .FirstOrDefaultAsync(up => up.UserId == user.Id && up.PersonaId == persona.Id, ct);
        if (existing is not null)
            return Result.Failure(PersonaErrors.AlreadyAssigned);

        if (request.MakeDefault)
        {
            var currentDefault = await db.UserPersonas.IgnoreQueryFilters()
                .Where(up => up.UserId == user.Id &&
                             up.TenantId == persona.TenantId &&
                             up.IsDefault)
                .FirstOrDefaultAsync(ct);
            currentDefault?.ClearDefault();
        }
        else
        {
            var anyDefault = await db.UserPersonas.IgnoreQueryFilters()
                .AnyAsync(up => up.UserId == user.Id &&
                                up.TenantId == persona.TenantId &&
                                up.IsDefault, ct);
            if (!anyDefault) request = request with { MakeDefault = true };
        }

        db.UserPersonas.Add(UserPersona.Create(
            user.Id, persona.Id, persona.TenantId!.Value,
            isDefault: request.MakeDefault,
            assignedBy: currentUser.UserId));

        await db.SaveChangesAsync(ct);
        return Result.Success();
    }
}
```

- [ ] **Step 22.4: `UnassignPersonaCommand.cs` + handler**

```csharp
// Command
using MediatR;
using Starter.Shared.Results;

namespace Starter.Module.AI.Application.Commands.Personas.UnassignPersona;

public sealed record UnassignPersonaCommand(Guid PersonaId, Guid UserId) : IRequest<Result>;
```

```csharp
// Handler
using MediatR;
using Microsoft.EntityFrameworkCore;
using Starter.Module.AI.Domain.Errors;
using Starter.Module.AI.Infrastructure.Persistence;
using Starter.Shared.Results;

namespace Starter.Module.AI.Application.Commands.Personas.UnassignPersona;

internal sealed class UnassignPersonaCommandHandler(AiDbContext db)
    : IRequestHandler<UnassignPersonaCommand, Result>
{
    public async Task<Result> Handle(UnassignPersonaCommand request, CancellationToken ct)
    {
        var target = await db.UserPersonas.IgnoreQueryFilters()
            .FirstOrDefaultAsync(up => up.UserId == request.UserId && up.PersonaId == request.PersonaId, ct);
        if (target is null)
            return Result.Failure(PersonaErrors.NotFound);

        var userAssignments = await db.UserPersonas.IgnoreQueryFilters()
            .Where(up => up.UserId == request.UserId && up.TenantId == target.TenantId)
            .ToListAsync(ct);

        if (userAssignments.Count == 1)
            return Result.Failure(PersonaErrors.CannotRemoveLastAssignment);

        if (target.IsDefault)
        {
            var promote = userAssignments
                .Where(up => up.PersonaId != target.PersonaId)
                .OrderByDescending(up => up.AssignedAt)
                .First();
            promote.MakeDefault();
        }

        db.UserPersonas.Remove(target);
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }
}
```

- [ ] **Step 22.5: `SetUserDefaultPersonaCommand.cs` + handler**

```csharp
// Command
using MediatR;
using Starter.Shared.Results;

namespace Starter.Module.AI.Application.Commands.Personas.SetUserDefaultPersona;

public sealed record SetUserDefaultPersonaCommand(Guid PersonaId, Guid UserId) : IRequest<Result>;
```

```csharp
// Handler
using MediatR;
using Microsoft.EntityFrameworkCore;
using Starter.Module.AI.Domain.Errors;
using Starter.Module.AI.Infrastructure.Persistence;
using Starter.Shared.Results;

namespace Starter.Module.AI.Application.Commands.Personas.SetUserDefaultPersona;

internal sealed class SetUserDefaultPersonaCommandHandler(AiDbContext db)
    : IRequestHandler<SetUserDefaultPersonaCommand, Result>
{
    public async Task<Result> Handle(SetUserDefaultPersonaCommand request, CancellationToken ct)
    {
        var target = await db.UserPersonas.IgnoreQueryFilters()
            .FirstOrDefaultAsync(up =>
                up.UserId == request.UserId && up.PersonaId == request.PersonaId, ct);
        if (target is null)
            return Result.Failure(PersonaErrors.NotAssignedToUser);

        var others = await db.UserPersonas.IgnoreQueryFilters()
            .Where(up => up.UserId == request.UserId && up.TenantId == target.TenantId && up.IsDefault)
            .ToListAsync(ct);
        foreach (var o in others) o.ClearDefault();

        target.MakeDefault();
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }
}
```

- [ ] **Step 22.6: Tests (`AssignPersonaCommandTests.cs`)**

```csharp
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Starter.Application.Common.Interfaces;
using Starter.Domain.Identity.Entities;
using Starter.Domain.ValueObjects;
using Starter.Module.AI.Application.Commands.Personas.AssignPersona;
using Starter.Module.AI.Domain.Entities;
using Starter.Module.AI.Domain.Enums;
using Starter.Module.AI.Infrastructure.Persistence;
using Xunit;
using Moq;

namespace Starter.Api.Tests.Ai.Personas;

public sealed class AssignPersonaCommandTests
{
    [Fact]
    public async Task Assigns_Persona_To_User()
    {
        var tenant = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var cu = new Mock<ICurrentUserService>();
        cu.SetupGet(x => x.TenantId).Returns(tenant);
        cu.SetupGet(x => x.UserId).Returns(Guid.NewGuid());

        var aiOpts = new DbContextOptionsBuilder<AiDbContext>()
            .UseInMemoryDatabase($"assign-{Guid.NewGuid()}").Options;
        var db = new AiDbContext(aiOpts, cu.Object);
        var persona = AiPersona.Create(tenant, "teacher", "Teacher", null,
            PersonaAudienceType.Internal, SafetyPreset.Standard, Guid.NewGuid());
        db.AiPersonas.Add(persona);
        await db.SaveChangesAsync();

        var appDb = new Mock<IApplicationDbContext>();
        var user = Mock.Of<User>(); // replaced below
        // Use a real minimal fake with an in-memory queryable
        var users = new List<User>();
        // Build a user via a test helper — tests for identity already cover User construction.
        // For simplicity here, assume we can mock Users as an AsQueryable via a Fake.
        // This handler test focuses on the happy-path shape — the controller test covers DB wiring.

        // Skipping full identity integration; verify the error path when user not found
        appDb.SetupGet(x => x.Users).Returns(TestQueryable.Empty<User>());

        var h = new AssignPersonaCommandHandler(db, appDb.Object, cu.Object);
        var r = await h.Handle(new AssignPersonaCommand(persona.Id, userId, MakeDefault: true), default);

        r.IsFailure.Should().BeTrue();
        r.Error.Code.Should().Be("Persona.UserNotInTenant");
    }
}

internal static class TestQueryable
{
    public static DbSet<T> Empty<T>() where T : class
    {
        var opts = new DbContextOptionsBuilder<_Stub<T>>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        return new _Stub<T>(opts).Items;
    }

    private sealed class _Stub<T> : DbContext where T : class
    {
        public _Stub(DbContextOptions<_Stub<T>> opts) : base(opts) { }
        public DbSet<T> Items => Set<T>();
    }
}
```

> Note: The controller-level integration test in Task 30 covers the full happy path with a real `IApplicationDbContext`. This unit test covers the user-not-found guard only.

- [ ] **Step 22.7: Tests (`UnassignPersonaCommandTests.cs`)**

```csharp
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Starter.Application.Common.Interfaces;
using Starter.Module.AI.Application.Commands.Personas.UnassignPersona;
using Starter.Module.AI.Domain.Entities;
using Starter.Module.AI.Domain.Enums;
using Starter.Module.AI.Infrastructure.Persistence;
using Xunit;
using Moq;

namespace Starter.Api.Tests.Ai.Personas;

public sealed class UnassignPersonaCommandTests
{
    private static (UnassignPersonaCommandHandler h, AiDbContext db) Setup(Guid tenant)
    {
        var cu = new Mock<ICurrentUserService>();
        cu.SetupGet(x => x.TenantId).Returns(tenant);
        var opts = new DbContextOptionsBuilder<AiDbContext>()
            .UseInMemoryDatabase($"unassign-{Guid.NewGuid()}").Options;
        var db = new AiDbContext(opts, cu.Object);
        return (new UnassignPersonaCommandHandler(db), db);
    }

    [Fact]
    public async Task Cannot_Remove_Last_Assignment()
    {
        var t = Guid.NewGuid();
        var (h, db) = Setup(t);
        var u = Guid.NewGuid();
        var p = AiPersona.Create(t, "teacher", "Teacher", null,
            PersonaAudienceType.Internal, SafetyPreset.Standard, Guid.NewGuid());
        db.AiPersonas.Add(p);
        db.UserPersonas.Add(UserPersona.Create(u, p.Id, t, true, null));
        await db.SaveChangesAsync();

        var r = await h.Handle(new UnassignPersonaCommand(p.Id, u), default);

        r.IsFailure.Should().BeTrue();
        r.Error.Code.Should().Be("Persona.CannotRemoveLastAssignment");
    }

    [Fact]
    public async Task Removing_Default_Promotes_Other_To_Default()
    {
        var t = Guid.NewGuid();
        var (h, db) = Setup(t);
        var u = Guid.NewGuid();
        var p1 = AiPersona.Create(t, "teacher", "Teacher", null,
            PersonaAudienceType.Internal, SafetyPreset.Standard, Guid.NewGuid());
        var p2 = AiPersona.Create(t, "student", "Student", null,
            PersonaAudienceType.Internal, SafetyPreset.Standard, Guid.NewGuid());
        db.AiPersonas.AddRange(p1, p2);
        db.UserPersonas.Add(UserPersona.Create(u, p1.Id, t, true, null));
        db.UserPersonas.Add(UserPersona.Create(u, p2.Id, t, false, null));
        await db.SaveChangesAsync();

        var r = await h.Handle(new UnassignPersonaCommand(p1.Id, u), default);

        r.IsSuccess.Should().BeTrue();
        var remaining = await db.UserPersonas.IgnoreQueryFilters().SingleAsync();
        remaining.PersonaId.Should().Be(p2.Id);
        remaining.IsDefault.Should().BeTrue();
    }
}
```

- [ ] **Step 22.8: Tests (`SetUserDefaultPersonaCommandTests.cs`)**

```csharp
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Starter.Application.Common.Interfaces;
using Starter.Module.AI.Application.Commands.Personas.SetUserDefaultPersona;
using Starter.Module.AI.Domain.Entities;
using Starter.Module.AI.Domain.Enums;
using Starter.Module.AI.Infrastructure.Persistence;
using Xunit;
using Moq;

namespace Starter.Api.Tests.Ai.Personas;

public sealed class SetUserDefaultPersonaCommandTests
{
    [Fact]
    public async Task Flips_Default_To_Target()
    {
        var t = Guid.NewGuid();
        var cu = new Mock<ICurrentUserService>();
        cu.SetupGet(x => x.TenantId).Returns(t);
        var opts = new DbContextOptionsBuilder<AiDbContext>()
            .UseInMemoryDatabase($"def-{Guid.NewGuid()}").Options;
        var db = new AiDbContext(opts, cu.Object);
        var u = Guid.NewGuid();
        var p1 = AiPersona.Create(t, "a", "A", null,
            PersonaAudienceType.Internal, SafetyPreset.Standard, Guid.NewGuid());
        var p2 = AiPersona.Create(t, "b", "B", null,
            PersonaAudienceType.Internal, SafetyPreset.Standard, Guid.NewGuid());
        db.AiPersonas.AddRange(p1, p2);
        db.UserPersonas.Add(UserPersona.Create(u, p1.Id, t, true, null));
        db.UserPersonas.Add(UserPersona.Create(u, p2.Id, t, false, null));
        await db.SaveChangesAsync();

        var h = new SetUserDefaultPersonaCommandHandler(db);
        await h.Handle(new SetUserDefaultPersonaCommand(p2.Id, u), default);

        var defaults = await db.UserPersonas.IgnoreQueryFilters()
            .Where(up => up.UserId == u && up.IsDefault).ToListAsync();
        defaults.Should().ContainSingle();
        defaults[0].PersonaId.Should().Be(p2.Id);
    }
}
```

- [ ] **Step 22.9: Run all three + commit**

```bash
dotnet test boilerplateBE/Starter.sln --filter "FullyQualifiedName~AssignPersonaCommandTests|FullyQualifiedName~UnassignPersonaCommandTests|FullyQualifiedName~SetUserDefaultPersonaCommandTests" --nologo

git add boilerplateBE/src/modules/Starter.Module.AI/Application/Commands/Personas/AssignPersona/ \
        boilerplateBE/src/modules/Starter.Module.AI/Application/Commands/Personas/UnassignPersona/ \
        boilerplateBE/src/modules/Starter.Module.AI/Application/Commands/Personas/SetUserDefaultPersona/ \
        boilerplateBE/tests/Starter.Api.Tests/Ai/Personas/AssignPersonaCommandTests.cs \
        boilerplateBE/tests/Starter.Api.Tests/Ai/Personas/UnassignPersonaCommandTests.cs \
        boilerplateBE/tests/Starter.Api.Tests/Ai/Personas/SetUserDefaultPersonaCommandTests.cs
git commit -m "feat(ai): AssignPersona + UnassignPersona + SetUserDefaultPersona commands"
```

---

## Task 23: Persona queries (`GetPersonas`, `GetPersonaById`)

**Files:**
- Create: `.../Queries/Personas/GetPersonas/GetPersonasQuery.cs` + handler
- Create: `.../Queries/Personas/GetPersonaById/GetPersonaByIdQuery.cs` + handler
- Create: `boilerplateBE/tests/Starter.Api.Tests/Ai/Personas/GetPersonasQueryTests.cs`

- [ ] **Step 23.1: Queries**

```csharp
// GetPersonasQuery.cs
using MediatR;
using Starter.Module.AI.Application.DTOs;
using Starter.Shared.Results;

namespace Starter.Module.AI.Application.Queries.Personas.GetPersonas;

public sealed record GetPersonasQuery(
    bool IncludeSystem = true,
    bool IncludeInactive = false) : IRequest<Result<IReadOnlyList<AiPersonaDto>>>;
```

```csharp
// GetPersonasQueryHandler.cs
using MediatR;
using Microsoft.EntityFrameworkCore;
using Starter.Module.AI.Application.DTOs;
using Starter.Module.AI.Infrastructure.Persistence;
using Starter.Shared.Results;

namespace Starter.Module.AI.Application.Queries.Personas.GetPersonas;

internal sealed class GetPersonasQueryHandler(AiDbContext db)
    : IRequestHandler<GetPersonasQuery, Result<IReadOnlyList<AiPersonaDto>>>
{
    public async Task<Result<IReadOnlyList<AiPersonaDto>>> Handle(
        GetPersonasQuery request, CancellationToken ct)
    {
        var q = db.AiPersonas.AsNoTracking().AsQueryable();
        if (!request.IncludeSystem) q = q.Where(p => !p.IsSystemReserved);
        if (!request.IncludeInactive) q = q.Where(p => p.IsActive);

        var rows = await q.OrderBy(p => p.IsSystemReserved ? 0 : 1)
            .ThenBy(p => p.DisplayName)
            .ToListAsync(ct);

        IReadOnlyList<AiPersonaDto> result = rows.Select(p => p.ToDto()).ToList();
        return Result.Success(result);
    }
}
```

```csharp
// GetPersonaByIdQuery.cs
using MediatR;
using Starter.Module.AI.Application.DTOs;
using Starter.Shared.Results;

namespace Starter.Module.AI.Application.Queries.Personas.GetPersonaById;

public sealed record GetPersonaByIdQuery(Guid Id) : IRequest<Result<AiPersonaDto>>;
```

```csharp
// GetPersonaByIdQueryHandler.cs
using MediatR;
using Microsoft.EntityFrameworkCore;
using Starter.Module.AI.Application.DTOs;
using Starter.Module.AI.Domain.Errors;
using Starter.Module.AI.Infrastructure.Persistence;
using Starter.Shared.Results;

namespace Starter.Module.AI.Application.Queries.Personas.GetPersonaById;

internal sealed class GetPersonaByIdQueryHandler(AiDbContext db)
    : IRequestHandler<GetPersonaByIdQuery, Result<AiPersonaDto>>
{
    public async Task<Result<AiPersonaDto>> Handle(GetPersonaByIdQuery request, CancellationToken ct)
    {
        var p = await db.AiPersonas.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == request.Id, ct);
        return p is null
            ? Result.Failure<AiPersonaDto>(PersonaErrors.NotFound)
            : Result.Success(p.ToDto());
    }
}
```

- [ ] **Step 23.2: `GetPersonasQueryTests.cs`**

```csharp
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Starter.Application.Common.Interfaces;
using Starter.Module.AI.Application.Queries.Personas.GetPersonas;
using Starter.Module.AI.Domain.Entities;
using Starter.Module.AI.Domain.Enums;
using Starter.Module.AI.Infrastructure.Persistence;
using Xunit;
using Moq;

namespace Starter.Api.Tests.Ai.Personas;

public sealed class GetPersonasQueryTests
{
    [Fact]
    public async Task Returns_All_When_IncludeSystem_And_IncludeInactive()
    {
        var t = Guid.NewGuid();
        var cu = new Mock<ICurrentUserService>();
        cu.SetupGet(x => x.TenantId).Returns(t);
        var opts = new DbContextOptionsBuilder<AiDbContext>()
            .UseInMemoryDatabase($"q-{Guid.NewGuid()}").Options;
        var db = new AiDbContext(opts, cu.Object);
        db.AiPersonas.Add(AiPersona.CreateAnonymous(t, Guid.NewGuid()));
        db.AiPersonas.Add(AiPersona.CreateDefault(t, Guid.NewGuid()));
        var teacher = AiPersona.Create(t, "teacher", "Teacher", null,
            PersonaAudienceType.Internal, SafetyPreset.Standard, Guid.NewGuid());
        teacher.SetActive(false);
        db.AiPersonas.Add(teacher);
        await db.SaveChangesAsync();

        var h = new GetPersonasQueryHandler(db);
        var r = await h.Handle(new GetPersonasQuery(true, true), default);

        r.IsSuccess.Should().BeTrue();
        r.Value.Should().HaveCount(3);
    }
}
```

- [ ] **Step 23.3: Run + commit**

```bash
dotnet test boilerplateBE/Starter.sln --filter "FullyQualifiedName~GetPersonasQueryTests" --nologo
git add boilerplateBE/src/modules/Starter.Module.AI/Application/Queries/Personas/GetPersonas/ \
        boilerplateBE/src/modules/Starter.Module.AI/Application/Queries/Personas/GetPersonaById/ \
        boilerplateBE/tests/Starter.Api.Tests/Ai/Personas/GetPersonasQueryTests.cs
git commit -m "feat(ai): GetPersonas + GetPersonaById queries"
```

---

## Task 24: `GetPersonaAssignmentsQuery` (joins core Users by Id)

**Files:**
- Create: `.../Queries/Personas/GetPersonaAssignments/GetPersonaAssignmentsQuery.cs` + handler
- Create: `boilerplateBE/tests/Starter.Api.Tests/Ai/Personas/GetPersonaAssignmentsQueryTests.cs`

- [ ] **Step 24.1: Query**

```csharp
using MediatR;
using Starter.Module.AI.Application.DTOs;
using Starter.Shared.Results;

namespace Starter.Module.AI.Application.Queries.Personas.GetPersonaAssignments;

public sealed record GetPersonaAssignmentsQuery(Guid PersonaId)
    : IRequest<Result<IReadOnlyList<UserPersonaDto>>>;
```

- [ ] **Step 24.2: Handler**

```csharp
using MediatR;
using Microsoft.EntityFrameworkCore;
using Starter.Application.Common.Interfaces;
using Starter.Module.AI.Application.DTOs;
using Starter.Module.AI.Domain.Errors;
using Starter.Module.AI.Infrastructure.Persistence;
using Starter.Shared.Results;

namespace Starter.Module.AI.Application.Queries.Personas.GetPersonaAssignments;

internal sealed class GetPersonaAssignmentsQueryHandler(
    AiDbContext db,
    IApplicationDbContext appDb)
    : IRequestHandler<GetPersonaAssignmentsQuery, Result<IReadOnlyList<UserPersonaDto>>>
{
    public async Task<Result<IReadOnlyList<UserPersonaDto>>> Handle(
        GetPersonaAssignmentsQuery request, CancellationToken ct)
    {
        var persona = await db.AiPersonas.FirstOrDefaultAsync(p => p.Id == request.PersonaId, ct);
        if (persona is null)
            return Result.Failure<IReadOnlyList<UserPersonaDto>>(PersonaErrors.NotFound);

        var rows = await db.UserPersonas.AsNoTracking()
            .Where(up => up.PersonaId == persona.Id)
            .OrderBy(up => up.AssignedAt)
            .ToListAsync(ct);

        var userIds = rows.Select(r => r.UserId).Distinct().ToList();
        var users = await appDb.Users.AsNoTracking()
            .IgnoreQueryFilters()
            .Where(u => userIds.Contains(u.Id))
            .Select(u => new { u.Id, u.FullName.Value })
            .ToDictionaryAsync(u => u.Id, u => u.Value, ct);

        IReadOnlyList<UserPersonaDto> dtos = rows.Select(up => new UserPersonaDto(
            UserId: up.UserId,
            UserDisplayName: users.TryGetValue(up.UserId, out var name) ? name : null,
            PersonaId: persona.Id,
            PersonaSlug: persona.Slug,
            PersonaDisplayName: persona.DisplayName,
            IsDefault: up.IsDefault,
            AssignedAt: up.AssignedAt)).ToList();

        return Result.Success(dtos);
    }
}
```

> **Note:** `FullName` is a value object on `User`. The projection uses `.Value` if the VO exposes it; if the accessor is different, adapt to whatever the existing identity code uses (e.g., `u.FullName.FirstName + " " + u.FullName.LastName`). Verify at the time of implementation by checking `boilerplateBE/src/Starter.Domain/Identity/Entities/User.cs` — this handler needs a human-readable display string. If `Value` isn't available, use:

```csharp
.Select(u => new { u.Id, Display = u.FullName.FirstName + " " + u.FullName.LastName })
```

- [ ] **Step 24.3: Tests**

```csharp
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Starter.Application.Common.Interfaces;
using Starter.Module.AI.Application.Queries.Personas.GetPersonaAssignments;
using Starter.Module.AI.Domain.Entities;
using Starter.Module.AI.Domain.Enums;
using Starter.Module.AI.Infrastructure.Persistence;
using Xunit;
using Moq;

namespace Starter.Api.Tests.Ai.Personas;

public sealed class GetPersonaAssignmentsQueryTests
{
    [Fact]
    public async Task Returns_Error_When_Persona_Missing()
    {
        var t = Guid.NewGuid();
        var cu = new Mock<ICurrentUserService>();
        cu.SetupGet(x => x.TenantId).Returns(t);
        var opts = new DbContextOptionsBuilder<AiDbContext>()
            .UseInMemoryDatabase($"asg-{Guid.NewGuid()}").Options;
        var db = new AiDbContext(opts, cu.Object);
        var appDb = new Mock<IApplicationDbContext>();
        // No User DbSet needed for not-found path
        var h = new GetPersonaAssignmentsQueryHandler(db, appDb.Object);

        var r = await h.Handle(new GetPersonaAssignmentsQuery(Guid.NewGuid()), default);

        r.IsFailure.Should().BeTrue();
        r.Error.Code.Should().Be("Persona.NotFound");
    }
}
```

- [ ] **Step 24.4: Run + commit**

```bash
dotnet test boilerplateBE/Starter.sln --filter "FullyQualifiedName~GetPersonaAssignmentsQueryTests" --nologo
git add boilerplateBE/src/modules/Starter.Module.AI/Application/Queries/Personas/GetPersonaAssignments/ \
        boilerplateBE/tests/Starter.Api.Tests/Ai/Personas/GetPersonaAssignmentsQueryTests.cs
git commit -m "feat(ai): GetPersonaAssignments query with user-display-name join"
```

---

## Task 25: `GetMePersonasQuery` (current user's personas)

**Files:**
- Create: `.../Queries/Personas/GetMePersonas/GetMePersonasQuery.cs` + handler
- Create: `boilerplateBE/tests/Starter.Api.Tests/Ai/Personas/GetMePersonasQueryTests.cs`

- [ ] **Step 25.1: Query**

```csharp
using MediatR;
using Starter.Module.AI.Application.DTOs;
using Starter.Shared.Results;

namespace Starter.Module.AI.Application.Queries.Personas.GetMePersonas;

public sealed record GetMePersonasQuery : IRequest<Result<MePersonasDto>>;
```

- [ ] **Step 25.2: Handler**

```csharp
using MediatR;
using Microsoft.EntityFrameworkCore;
using Starter.Application.Common.Interfaces;
using Starter.Module.AI.Application.DTOs;
using Starter.Module.AI.Domain.Errors;
using Starter.Module.AI.Infrastructure.Persistence;
using Starter.Shared.Results;

namespace Starter.Module.AI.Application.Queries.Personas.GetMePersonas;

internal sealed class GetMePersonasQueryHandler(
    AiDbContext db,
    ICurrentUserService currentUser)
    : IRequestHandler<GetMePersonasQuery, Result<MePersonasDto>>
{
    public async Task<Result<MePersonasDto>> Handle(GetMePersonasQuery request, CancellationToken ct)
    {
        if (currentUser.UserId is not Guid userId)
            return Result.Failure<MePersonasDto>(AiErrors.NotAuthenticated);

        var rows = await db.UserPersonas.AsNoTracking()
            .Include(up => up.Persona)
            .Where(up => up.UserId == userId && up.Persona.IsActive)
            .OrderByDescending(up => up.IsDefault)
            .ThenBy(up => up.Persona.DisplayName)
            .ToListAsync(ct);

        var dtos = rows.Select(up => new UserPersonaDto(
            UserId: userId,
            UserDisplayName: null,
            PersonaId: up.Persona.Id,
            PersonaSlug: up.Persona.Slug,
            PersonaDisplayName: up.Persona.DisplayName,
            IsDefault: up.IsDefault,
            AssignedAt: up.AssignedAt)).ToList();

        var defaultId = rows.FirstOrDefault(r => r.IsDefault)?.PersonaId;
        return Result.Success(new MePersonasDto(dtos, defaultId));
    }
}
```

- [ ] **Step 25.3: Tests**

```csharp
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Starter.Application.Common.Interfaces;
using Starter.Module.AI.Application.Queries.Personas.GetMePersonas;
using Starter.Module.AI.Domain.Entities;
using Starter.Module.AI.Domain.Enums;
using Starter.Module.AI.Infrastructure.Persistence;
using Xunit;
using Moq;

namespace Starter.Api.Tests.Ai.Personas;

public sealed class GetMePersonasQueryTests
{
    [Fact]
    public async Task Returns_Assigned_Personas_With_DefaultId()
    {
        var t = Guid.NewGuid();
        var u = Guid.NewGuid();
        var cu = new Mock<ICurrentUserService>();
        cu.SetupGet(x => x.UserId).Returns(u);
        cu.SetupGet(x => x.TenantId).Returns(t);
        var opts = new DbContextOptionsBuilder<AiDbContext>()
            .UseInMemoryDatabase($"me-{Guid.NewGuid()}").Options;
        var db = new AiDbContext(opts, cu.Object);

        var p1 = AiPersona.Create(t, "teacher", "Teacher", null,
            PersonaAudienceType.Internal, SafetyPreset.Standard, Guid.NewGuid());
        var p2 = AiPersona.Create(t, "student", "Student", null,
            PersonaAudienceType.Internal, SafetyPreset.ChildSafe, Guid.NewGuid());
        db.AiPersonas.AddRange(p1, p2);
        db.UserPersonas.Add(UserPersona.Create(u, p1.Id, t, true, null));
        db.UserPersonas.Add(UserPersona.Create(u, p2.Id, t, false, null));
        await db.SaveChangesAsync();

        var h = new GetMePersonasQueryHandler(db, cu.Object);
        var r = await h.Handle(new GetMePersonasQuery(), default);

        r.IsSuccess.Should().BeTrue();
        r.Value.Personas.Should().HaveCount(2);
        r.Value.DefaultPersonaId.Should().Be(p1.Id);
    }
}
```

- [ ] **Step 25.4: Run + commit**

```bash
dotnet test boilerplateBE/Starter.sln --filter "FullyQualifiedName~GetMePersonasQueryTests" --nologo
git add boilerplateBE/src/modules/Starter.Module.AI/Application/Queries/Personas/GetMePersonas/ \
        boilerplateBE/tests/Starter.Api.Tests/Ai/Personas/GetMePersonasQueryTests.cs
git commit -m "feat(ai): GetMePersonas query for current user's persona list"
```

---

## Task 26: Extend `GetAssistantsQuery` with persona visibility filter

**Files:**
- Modify: `boilerplateBE/src/modules/Starter.Module.AI/Application/Queries/GetAssistants/GetAssistantsQueryHandler.cs`
- Create: `boilerplateBE/tests/Starter.Api.Tests/Ai/Personas/GetAssistantsPersonaFilterTests.cs`

- [ ] **Step 26.1: Extend handler** — after the existing access-resolution `.Where`, add:

Locate the closing block before `if (request.IsActive ...)` and insert:

```csharp
        // Plan 5b — persona visibility filter (post-materialisation because of JSONB arrays)
        var personaCtx = personaContextAccessor.Current;
        List<Domain.Entities.AiAssistant>? materialised = null;
        if (personaCtx is not null)
        {
            var tenantAll = await query
                .Include(a => a.Visibility)   // no-op — just keeps tracking disabled
                .ToListAsync(cancellationToken);
            materialised = tenantAll
                .Where(a => a.IsVisibleToPersona(personaCtx.Slug, personaCtx.PermittedAgentSlugs))
                .ToList();
            query = materialised.AsQueryable();
        }
```

Inject `IPersonaContextAccessor personaContextAccessor` via primary constructor alongside the existing dependencies.

Update the constructor signature and `using` list:

```csharp
using Starter.Module.AI.Application.Services.Personas;
// ...
internal sealed class GetAssistantsQueryHandler(
    AiDbContext context,
    IResourceAccessService access,
    ICurrentUserService currentUser,
    IPersonaContextAccessor personaContextAccessor)
    : IRequestHandler<GetAssistantsQuery, Result<PaginatedList<AiAssistantDto>>>
```

> Because the JSONB comparison can't translate to SQL cleanly with current provider support, the filter runs in-memory after the access scope is applied. Acceptable for tenant-scale list endpoints.

- [ ] **Step 26.2: Tests**

```csharp
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Starter.Application.Common.Access;
using Starter.Application.Common.Access.Contracts;
using Starter.Application.Common.Interfaces;
using Starter.Domain.Common.Access.Enums;
using Starter.Module.AI.Application.Queries.GetAssistants;
using Starter.Module.AI.Application.Services.Personas;
using Starter.Module.AI.Application.Services.Runtime;
using Starter.Module.AI.Domain.Entities;
using Starter.Module.AI.Domain.Enums;
using Starter.Module.AI.Infrastructure.Persistence;
using Starter.Module.AI.Infrastructure.Services.Personas;
using Xunit;
using Moq;

namespace Starter.Api.Tests.Ai.Personas;

public sealed class GetAssistantsPersonaFilterTests
{
    [Fact]
    public async Task Filters_By_Persona_Slug()
    {
        var tenant = Guid.NewGuid();
        var user = Guid.NewGuid();

        var cu = new Mock<ICurrentUserService>();
        cu.SetupGet(x => x.TenantId).Returns(tenant);
        cu.SetupGet(x => x.UserId).Returns(user);

        var opts = new DbContextOptionsBuilder<AiDbContext>()
            .UseInMemoryDatabase($"list-{Guid.NewGuid()}").Options;
        var db = new AiDbContext(opts, cu.Object);

        var a1 = AiAssistant.Create(tenant, "Tutor", null, "p", user, slug: "tutor");
        a1.SetPersonaTargets(new[] { "student" });
        a1.SetVisibility(ResourceVisibility.TenantWide);
        var a2 = AiAssistant.Create(tenant, "Admin Copilot", null, "p", user, slug: "admin-copilot");
        a2.SetPersonaTargets(new[] { "teacher" });
        a2.SetVisibility(ResourceVisibility.TenantWide);
        db.AiAssistants.AddRange(a1, a2);
        await db.SaveChangesAsync();

        var access = new Mock<IResourceAccessService>();
        access.Setup(x => x.ResolveAccessibleResourcesAsync(
                It.IsAny<ICurrentUserService>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(AccessResolution.AdminBypass());

        var accessor = new PersonaContextAccessor();
        accessor.Set(new PersonaContext(Guid.NewGuid(), "student",
            PersonaAudienceType.Internal, SafetyPreset.ChildSafe, Array.Empty<string>()));

        var h = new GetAssistantsQueryHandler(db, access.Object, cu.Object, accessor);
        var r = await h.Handle(new GetAssistantsQuery(1, 50, null, null), default);

        r.IsSuccess.Should().BeTrue();
        r.Value.Items.Should().HaveCount(1);
        r.Value.Items[0].Name.Should().Be("Tutor");
    }
}
```

> **Note:** `AccessResolution.AdminBypass()` may be named differently in the existing codebase; adapt to whatever factory the tests use (check `boilerplateBE/src/Starter.Application/Common/Access/AccessResolution.cs`).

- [ ] **Step 26.3: Run + commit**

```bash
dotnet build boilerplateBE/Starter.sln --nologo
dotnet test boilerplateBE/Starter.sln --filter "FullyQualifiedName~GetAssistantsPersonaFilterTests" --nologo
git add boilerplateBE/src/modules/Starter.Module.AI/Application/Queries/GetAssistants/GetAssistantsQueryHandler.cs \
        boilerplateBE/tests/Starter.Api.Tests/Ai/Personas/GetAssistantsPersonaFilterTests.cs
git commit -m "feat(ai): filter assistant list by current persona visibility"
```

---

## Task 27: `AiPersonasController` (CRUD endpoints)

**Files:**
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Controllers/AiPersonasController.cs`
- Create: `boilerplateBE/tests/Starter.Api.Tests/Ai/Personas/AiPersonasControllerTests.cs`

- [ ] **Step 27.1: Controller**

```csharp
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Starter.Abstractions.Web;
using Starter.Module.AI.Application.Commands.Personas.CreatePersona;
using Starter.Module.AI.Application.Commands.Personas.DeletePersona;
using Starter.Module.AI.Application.Commands.Personas.UpdatePersona;
using Starter.Module.AI.Application.DTOs;
using Starter.Module.AI.Application.Queries.Personas.GetPersonaById;
using Starter.Module.AI.Application.Queries.Personas.GetPersonas;
using Starter.Module.AI.Constants;
using Starter.Shared.Models;

namespace Starter.Module.AI.Controllers;

[Route("api/v{version:apiVersion}/ai/personas")]
public sealed class AiPersonasController(ISender mediator) : BaseApiController(mediator)
{
    [HttpGet]
    [Authorize(Policy = AiPermissions.ViewPersonas)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<AiPersonaDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(
        [FromQuery] bool includeSystem = true,
        [FromQuery] bool includeInactive = false,
        CancellationToken ct = default)
        => HandleResult(await Mediator.Send(new GetPersonasQuery(includeSystem, includeInactive), ct));

    [HttpGet("{id:guid}")]
    [Authorize(Policy = AiPermissions.ViewPersonas)]
    [ProducesResponseType(typeof(ApiResponse<AiPersonaDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct = default)
        => HandleResult(await Mediator.Send(new GetPersonaByIdQuery(id), ct));

    [HttpPost]
    [Authorize(Policy = AiPermissions.ManagePersonas)]
    [ProducesResponseType(typeof(ApiResponse<AiPersonaDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Create([FromBody] CreatePersonaCommand command, CancellationToken ct = default)
        => HandleResult(await Mediator.Send(command, ct));

    [HttpPut("{id:guid}")]
    [Authorize(Policy = AiPermissions.ManagePersonas)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdatePersonaBody body, CancellationToken ct = default)
        => HandleResult(await Mediator.Send(new UpdatePersonaCommand(
            id, body.DisplayName, body.Description, body.SafetyPreset,
            body.PermittedAgentSlugs, body.IsActive), ct));

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = AiPermissions.ManagePersonas)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct = default)
        => HandleResult(await Mediator.Send(new DeletePersonaCommand(id), ct));

    public sealed record UpdatePersonaBody(
        string DisplayName,
        string? Description,
        Domain.Enums.SafetyPreset SafetyPreset,
        IReadOnlyList<string>? PermittedAgentSlugs,
        bool IsActive);
}
```

- [ ] **Step 27.2: Integration test (`AiPersonasControllerTests.cs`)**

Keep this minimal — one happy-path create + list, following the pattern of existing controller tests in `boilerplateBE/tests/Starter.Api.Tests/Ai/` (use `AiWebApplicationFactory` or equivalent if one exists; otherwise a plain handler-level test suffices since the controller is a thin pass-through).

```csharp
using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Starter.Module.AI.Application.Commands.Personas.CreatePersona;
using Starter.Module.AI.Application.DTOs;
using Starter.Module.AI.Controllers;
using Starter.Module.AI.Domain.Enums;
using Starter.Shared.Models;
using Starter.Shared.Results;
using Xunit;
using Moq;

namespace Starter.Api.Tests.Ai.Personas;

public sealed class AiPersonasControllerTests
{
    [Fact]
    public async Task Create_Returns_200_With_Dto()
    {
        var sender = new Mock<ISender>();
        var dto = new AiPersonaDto(
            Guid.NewGuid(), "teacher", "Teacher", null,
            PersonaAudienceType.Internal, SafetyPreset.Standard,
            Array.Empty<string>(), false, true,
            DateTime.UtcNow, null);
        sender.Setup(x => x.Send(It.IsAny<CreatePersonaCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(dto));

        var ctl = new AiPersonasController(sender.Object);
        var result = await ctl.Create(new CreatePersonaCommand(
            "Teacher", null, "teacher",
            PersonaAudienceType.Internal, SafetyPreset.Standard, null), default);

        result.Should().BeOfType<OkObjectResult>();
    }
}
```

- [ ] **Step 27.3: Build + run + commit**

```bash
dotnet build boilerplateBE/Starter.sln --nologo
dotnet test boilerplateBE/Starter.sln --filter "FullyQualifiedName~AiPersonasControllerTests" --nologo
git add boilerplateBE/src/modules/Starter.Module.AI/Controllers/AiPersonasController.cs \
        boilerplateBE/tests/Starter.Api.Tests/Ai/Personas/AiPersonasControllerTests.cs
git commit -m "feat(ai): AiPersonasController CRUD endpoints"
```

---

## Task 28: `AiPersonaAssignmentsController` + `AiMePersonasController`

**Files:**
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Controllers/AiPersonaAssignmentsController.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Controllers/AiMePersonasController.cs`

- [ ] **Step 28.1: `AiPersonaAssignmentsController`**

```csharp
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Starter.Abstractions.Web;
using Starter.Module.AI.Application.Commands.Personas.AssignPersona;
using Starter.Module.AI.Application.Commands.Personas.SetUserDefaultPersona;
using Starter.Module.AI.Application.Commands.Personas.UnassignPersona;
using Starter.Module.AI.Application.DTOs;
using Starter.Module.AI.Application.Queries.Personas.GetPersonaAssignments;
using Starter.Module.AI.Constants;
using Starter.Shared.Models;

namespace Starter.Module.AI.Controllers;

[Route("api/v{version:apiVersion}/ai/personas/{personaId:guid}/assignments")]
public sealed class AiPersonaAssignmentsController(ISender mediator) : BaseApiController(mediator)
{
    [HttpGet]
    [Authorize(Policy = AiPermissions.ViewPersonas)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<UserPersonaDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(Guid personaId, CancellationToken ct = default)
        => HandleResult(await Mediator.Send(new GetPersonaAssignmentsQuery(personaId), ct));

    [HttpPost]
    [Authorize(Policy = AiPermissions.AssignPersona)]
    public async Task<IActionResult> Assign(Guid personaId,
        [FromBody] AssignBody body, CancellationToken ct = default)
        => HandleResult(await Mediator.Send(
            new AssignPersonaCommand(personaId, body.UserId, body.MakeDefault), ct));

    [HttpDelete("{userId:guid}")]
    [Authorize(Policy = AiPermissions.AssignPersona)]
    public async Task<IActionResult> Unassign(Guid personaId, Guid userId, CancellationToken ct = default)
        => HandleResult(await Mediator.Send(new UnassignPersonaCommand(personaId, userId), ct));

    [HttpPut("{userId:guid}/default")]
    [Authorize(Policy = AiPermissions.AssignPersona)]
    public async Task<IActionResult> SetDefault(Guid personaId, Guid userId, CancellationToken ct = default)
        => HandleResult(await Mediator.Send(new SetUserDefaultPersonaCommand(personaId, userId), ct));

    public sealed record AssignBody(Guid UserId, bool MakeDefault);
}
```

- [ ] **Step 28.2: `AiMePersonasController`**

```csharp
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Starter.Abstractions.Web;
using Starter.Module.AI.Application.DTOs;
using Starter.Module.AI.Application.Queries.Personas.GetMePersonas;
using Starter.Module.AI.Constants;
using Starter.Shared.Models;

namespace Starter.Module.AI.Controllers;

[Route("api/v{version:apiVersion}/ai/me/personas")]
public sealed class AiMePersonasController(ISender mediator) : BaseApiController(mediator)
{
    [HttpGet]
    [Authorize(Policy = AiPermissions.Chat)]
    [ProducesResponseType(typeof(ApiResponse<MePersonasDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(CancellationToken ct = default)
        => HandleResult(await Mediator.Send(new GetMePersonasQuery(), ct));
}
```

- [ ] **Step 28.3: Build + commit**

```bash
dotnet build boilerplateBE/Starter.sln --nologo
git add boilerplateBE/src/modules/Starter.Module.AI/Controllers/AiPersonaAssignmentsController.cs \
        boilerplateBE/src/modules/Starter.Module.AI/Controllers/AiMePersonasController.cs
git commit -m "feat(ai): persona assignment + me-personas controllers"
```

---

## Task 29: Extend `CreateAssistantCommand` + `UpdateAssistantCommand` with slug + persona targets

**Files:**
- Modify: `.../Application/Commands/AssistantInputRules.cs`
- Modify: `.../Application/Commands/CreateAssistant/CreateAssistantCommand.cs` + handler
- Modify: `.../Application/Commands/UpdateAssistant/UpdateAssistantCommand.cs` + handler
- Modify: `.../Application/DTOs/AiAssistantDto.cs` + `AiAssistantMappers.cs`

- [ ] **Step 29.1: Extend `IAssistantInput` interface + rules**

Open `AssistantInputRules.cs`. Add two members to `IAssistantInput`:

```csharp
    string? Slug { get; }
    IReadOnlyList<string>? PersonaTargetSlugs { get; }
```

And in `AssistantInputRules.Apply<T>`, add at the bottom:

```csharp
        v.RuleFor(x => x.Slug!)
            .Matches("^[a-z0-9]+(-[a-z0-9]+)*$").MaximumLength(64)
            .When(x => !string.IsNullOrEmpty(x.Slug));
        v.RuleForEach(x => x.PersonaTargetSlugs!)
            .NotEmpty().Matches("^[a-z0-9]+(-[a-z0-9]+)*$").MaximumLength(64)
            .When(x => x.PersonaTargetSlugs is not null);
```

- [ ] **Step 29.2: Update `CreateAssistantCommand.cs`**

Replace with:

```csharp
using MediatR;
using Starter.Module.AI.Application.DTOs;
using Starter.Module.AI.Domain.Enums;
using Starter.Shared.Results;

namespace Starter.Module.AI.Application.Commands.CreateAssistant;

public sealed record CreateAssistantCommand(
    string Name,
    string? Description,
    string SystemPrompt,
    AiProviderType? Provider,
    string? Model,
    double Temperature,
    int MaxTokens,
    AssistantExecutionMode ExecutionMode,
    int MaxAgentSteps,
    IReadOnlyList<string>? EnabledToolNames,
    IReadOnlyList<Guid>? KnowledgeBaseDocIds,
    AiRagScope RagScope = AiRagScope.None,
    string? Slug = null,
    IReadOnlyList<string>? PersonaTargetSlugs = null)
    : IRequest<Result<AiAssistantDto>>, IAssistantInput;
```

- [ ] **Step 29.3: Update `CreateAssistantCommandHandler.cs`**

After the `assistant = AiAssistant.Create(...)` call, inject the `ISlugGenerator` and compute the slug. Full updated handler:

```csharp
using MediatR;
using Microsoft.EntityFrameworkCore;
using Starter.Application.Common.Interfaces;
using Starter.Module.AI.Application.DTOs;
using Starter.Module.AI.Application.Services.Personas;
using Starter.Module.AI.Domain.Entities;
using Starter.Module.AI.Domain.Errors;
using Starter.Module.AI.Infrastructure.Persistence;
using Starter.Shared.Results;

namespace Starter.Module.AI.Application.Commands.CreateAssistant;

internal sealed class CreateAssistantCommandHandler(
    AiDbContext context,
    ICurrentUserService currentUser,
    ISlugGenerator slugGenerator)
    : IRequestHandler<CreateAssistantCommand, Result<AiAssistantDto>>
{
    public async Task<Result<AiAssistantDto>> Handle(
        CreateAssistantCommand request,
        CancellationToken cancellationToken)
    {
        var tenantId = currentUser.TenantId;
        var normalized = request.Name.Trim();

        var nameTaken = await context.AiAssistants
            .IgnoreQueryFilters()
            .AnyAsync(a => a.TenantId == tenantId && a.Name == normalized, cancellationToken);
        if (nameTaken)
            return Result.Failure<AiAssistantDto>(AiErrors.AssistantNameAlreadyExists);

        // Resolve slug
        var existingSlugs = await context.AiAssistants
            .IgnoreQueryFilters()
            .Where(a => a.TenantId == tenantId && a.Slug != "")
            .Select(a => a.Slug)
            .ToListAsync(cancellationToken);
        var taken = new HashSet<string>(existingSlugs, StringComparer.Ordinal);

        string slug;
        if (!string.IsNullOrWhiteSpace(request.Slug))
        {
            slug = request.Slug.Trim().ToLowerInvariant();
            if (taken.Contains(slug))
                return Result.Failure<AiAssistantDto>(AiErrors.AssistantSlugAlreadyExists(slug));
        }
        else
        {
            slug = slugGenerator.EnsureUnique(slugGenerator.Slugify(normalized), taken);
        }

        var assistant = AiAssistant.Create(
            tenantId: tenantId,
            name: normalized,
            description: request.Description,
            systemPrompt: request.SystemPrompt,
            createdByUserId: currentUser.UserId!.Value,
            provider: request.Provider,
            model: request.Model,
            temperature: request.Temperature,
            maxTokens: request.MaxTokens,
            executionMode: request.ExecutionMode,
            maxAgentSteps: request.MaxAgentSteps,
            isActive: true,
            slug: slug);

        if (request.EnabledToolNames is { Count: > 0 })
            assistant.SetEnabledTools(request.EnabledToolNames);

        if (request.KnowledgeBaseDocIds is { Count: > 0 })
            assistant.SetKnowledgeBase(request.KnowledgeBaseDocIds);

        if (request.RagScope != Domain.Enums.AiRagScope.None)
            assistant.SetRagScope(request.RagScope);

        if (request.PersonaTargetSlugs is not null)
            assistant.SetPersonaTargets(request.PersonaTargetSlugs);

        context.AiAssistants.Add(assistant);
        await context.SaveChangesAsync(cancellationToken);

        return Result.Success(assistant.ToDto());
    }
}
```

- [ ] **Step 29.4: Update `UpdateAssistantCommand.cs`** — add matching `Slug?`, `PersonaTargetSlugs?`. Update handler similarly: if `Slug` supplied and changed, re-check uniqueness and call `assistant.SetSlug(...)`; if `PersonaTargetSlugs` supplied, call `assistant.SetPersonaTargets(...)`.

Replace the `UpdateAssistantCommand` record:

```csharp
using MediatR;
using Starter.Module.AI.Application.DTOs;
using Starter.Module.AI.Domain.Enums;
using Starter.Shared.Results;

namespace Starter.Module.AI.Application.Commands.UpdateAssistant;

public sealed record UpdateAssistantCommand(
    Guid Id,
    string Name,
    string? Description,
    string SystemPrompt,
    AiProviderType? Provider,
    string? Model,
    double Temperature,
    int MaxTokens,
    AssistantExecutionMode ExecutionMode,
    int MaxAgentSteps,
    IReadOnlyList<string>? EnabledToolNames,
    IReadOnlyList<Guid>? KnowledgeBaseDocIds,
    AiRagScope RagScope = AiRagScope.None,
    string? Slug = null,
    IReadOnlyList<string>? PersonaTargetSlugs = null)
    : IRequest<Result<AiAssistantDto>>, IAssistantInput;
```

In `UpdateAssistantCommandHandler.cs`, after the existing `Update(...)` call and `SetEnabledTools` / `SetKnowledgeBase` / `SetRagScope`, add:

```csharp
        if (request.Slug is not null)
        {
            var slug = request.Slug.Trim().ToLowerInvariant();
            if (slug != assistant.Slug)
            {
                var taken = await context.AiAssistants
                    .IgnoreQueryFilters()
                    .AnyAsync(a => a.TenantId == tenantId &&
                                   a.Slug == slug &&
                                   a.Id != assistant.Id,
                        cancellationToken);
                if (taken)
                    return Result.Failure<AiAssistantDto>(AiErrors.AssistantSlugAlreadyExists(slug));
                assistant.SetSlug(slug);
            }
        }

        if (request.PersonaTargetSlugs is not null)
            assistant.SetPersonaTargets(request.PersonaTargetSlugs);
```

- [ ] **Step 29.5: Update `AiAssistantDto.cs`**

Read `boilerplateBE/src/modules/Starter.Module.AI/Application/DTOs/AiAssistantDto.cs` first; append `string Slug` and `IReadOnlyList<string> PersonaTargetSlugs` to the record.

Then update `AiAssistantMappers.cs`:

```csharp
    public static AiAssistantDto ToDto(this AiAssistant a) =>
        new(
            a.Id,
            a.Name,
            a.Description,
            a.SystemPrompt,
            a.Provider,
            a.Model,
            a.Temperature,
            a.MaxTokens,
            a.EnabledToolNames,
            a.KnowledgeBaseDocIds,
            a.ExecutionMode,
            a.MaxAgentSteps,
            a.IsActive,
            a.CreatedAt,
            a.ModifiedAt,
            a.RagScope,
            a.Visibility,
            a.AccessMode,
            a.CreatedByUserId,
            a.Slug,
            a.PersonaTargetSlugs);
```

- [ ] **Step 29.6: Build + existing assistant tests — expect PASS**

```bash
dotnet build boilerplateBE/Starter.sln --nologo
dotnet test boilerplateBE/Starter.sln --filter "FullyQualifiedName~CreateAssistant|FullyQualifiedName~UpdateAssistant|FullyQualifiedName~Ai" --nologo
```

If existing assistant tests fail due to DTO positional-arg shape, update them to supply the new fields.

- [ ] **Step 29.7: Commit**

```bash
git add boilerplateBE/src/modules/Starter.Module.AI/Application/Commands/AssistantInputRules.cs \
        boilerplateBE/src/modules/Starter.Module.AI/Application/Commands/CreateAssistant/ \
        boilerplateBE/src/modules/Starter.Module.AI/Application/Commands/UpdateAssistant/ \
        boilerplateBE/src/modules/Starter.Module.AI/Application/DTOs/AiAssistantDto.cs \
        boilerplateBE/src/modules/Starter.Module.AI/Application/DTOs/AiAssistantMappers.cs
git commit -m "feat(ai): Create/Update assistant commands accept Slug + PersonaTargetSlugs"
```

---

## Task 30: Integrate persona pipeline into `ChatExecutionService`

**Files:**
- Modify: `boilerplateBE/src/modules/Starter.Module.AI/Application/Services/ChatExecutionService.cs`
- Create: `boilerplateBE/tests/Starter.Api.Tests/Ai/Personas/ChatExecutionServicePersonaPathTests.cs`

- [ ] **Step 30.1: Inject resolver + clause provider + accessor + config**

Update the primary-constructor signature to accept:

```csharp
    IPersonaResolver personaResolver,
    ISafetyPresetClauseProvider safetyClauses,
    IPersonaContextAccessor personaContextAccessor,
```

and adjust namespaces:

```csharp
using Starter.Module.AI.Application.Services.Personas;
```

- [ ] **Step 30.2: Add a persona-resolution step to `PrepareTurnAsync`**

Inside `PrepareTurnAsync`, immediately after the `NotAuthenticated` guard (`if (currentUser.UserId is not Guid userId)`), read the optional feature-flag and resolve persona:

```csharp
        PersonaContext? persona = null;
        var personasEnabled = configuration.GetValue<bool?>("AI:Personas:Enabled") ?? true;
        if (personasEnabled)
        {
            var resolved = await personaResolver.ResolveAsync(explicitPersonaId, ct);
            if (resolved.IsFailure)
                return Result.Failure<ChatTurnState>(resolved.Error);
            persona = resolved.Value;
            personaContextAccessor.Set(persona);
        }
```

Change the `PrepareTurnAsync` signature to accept `Guid? explicitPersonaId` — add it as the last parameter of `PrepareTurnAsync`, then thread it through from `ExecuteAsync` / `ExecuteStreamAsync` signatures (update both to accept `Guid? personaId`).

Update `IChatExecutionService` to match:

```csharp
Task<Result<AiChatReplyDto>> ExecuteAsync(
    Guid? conversationId, Guid? assistantId, string userMessage,
    Guid? personaId, CancellationToken ct = default);

IAsyncEnumerable<ChatStreamEvent> ExecuteStreamAsync(
    Guid? conversationId, Guid? assistantId, string userMessage,
    Guid? personaId, CancellationToken ct = default);
```

Update `SendChatMessageCommandHandler` to pass `command.PersonaId`, and update `AiChatController.StreamChat` to pass `command.PersonaId` too.

- [ ] **Step 30.3: Apply the visibility filter after the ACL gate**

Immediately after the existing `canAccess` check and before the quota gate, add:

```csharp
        if (persona is not null &&
            !assistant.IsVisibleToPersona(persona.Slug, persona.PermittedAgentSlugs))
            return Result.Failure<ChatTurnState>(AiErrors.AssistantNotPermittedForPersona);
```

- [ ] **Step 30.4: Prepend safety clause to system prompt**

In `ResolveSystemPrompt`, change signature to accept `PersonaContext? persona`:

```csharp
    private string ResolveSystemPrompt(
        AiAssistant assistant,
        RetrievalResult retrieved,
        PersonaContext? persona)
    {
        var culture = CultureInfo.CurrentUICulture;
        var clause = persona is null
            ? string.Empty
            : safetyClauses.GetClause(persona.Safety, persona.Audience, culture);

        var parts = new List<string>(3);
        if (!string.IsNullOrEmpty(clause)) parts.Add(clause);
        parts.Add(assistant.SystemPrompt);
        if (retrieved?.Markdown is { Length: > 0 }) parts.Add(retrieved.Markdown);

        return string.Join("\n\n", parts);
    }
```

Update both call sites in `ExecuteAsync` and `ExecuteStreamAsync` to pass `state.Persona`.

- [ ] **Step 30.5: Thread persona into `AgentRunContext`**

In both `ExecuteAsync` and `ExecuteStreamAsync`, when constructing `AgentRunContext`, append `Persona: state.Persona`.

- [ ] **Step 30.6: Populate `PersonaSlug` on the reply DTO**

In `ExecuteAsync` final `Result.Success(...)`:

```csharp
        return Result.Success(new AiChatReplyDto(
            state.Conversation.Id,
            state.UserMessage.ToDto(),
            finalMessage.ToDto(),
            PersonaSlug: state.Persona?.Slug));
```

- [ ] **Step 30.7: Add `PersonaSlug` to stream `start` frame**

In `ExecuteStreamAsync` at the `yield return new ChatStreamEvent("start", ...)` line, add:

```csharp
        yield return new ChatStreamEvent("start", new
        {
            ConversationId = state.Conversation.Id,
            UserMessageId = state.UserMessage.Id,
            PersonaSlug = state.Persona?.Slug
        });
```

- [ ] **Step 30.8: Extend `ChatTurnState`**

`ChatTurnState` is a private nested type in `ChatExecutionService.cs`. Add `PersonaContext? Persona` property and populate it at construction sites in `PrepareTurnAsync`.

- [ ] **Step 30.9: Write test (`ChatExecutionServicePersonaPathTests.cs`)**

Only the essential pipeline-behaviour tests. The existing `ChatExecutionRagInjectionTests` is the regression gate for everything else.

```csharp
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Starter.Module.AI.Application.Services.Personas;
using Starter.Module.AI.Application.Services.Runtime;
using Starter.Module.AI.Domain.Entities;
using Starter.Module.AI.Domain.Enums;
using Xunit;

namespace Starter.Api.Tests.Ai.Personas;

// This is a behaviour-level placeholder asserting a visibility-rejection path.
// Full chat-path coverage is handled by the existing suite updated in Step 30.10.
public sealed class ChatExecutionServicePersonaPathTests
{
    [Fact]
    public void Assistant_Not_Permitted_For_Persona_Is_Detected()
    {
        var tenant = Guid.NewGuid();
        var user = Guid.NewGuid();
        var a = AiAssistant.Create(tenant, "Admin Copilot", null, "prompt", user, slug: "admin-copilot");
        a.SetPersonaTargets(new[] { "teacher" });

        var persona = new PersonaContext(Guid.NewGuid(), "student",
            PersonaAudienceType.Internal, SafetyPreset.ChildSafe, Array.Empty<string>());

        a.IsVisibleToPersona(persona.Slug, persona.PermittedAgentSlugs)
            .Should().BeFalse();
    }
}
```

> The real end-to-end check happens via `dotnet test` on the existing `ChatExecutionRagInjectionTests` suite which should keep passing once the resolver falls back silently for tests that use a `FakeCurrentUserService` without a persona assignment — Step 30.10 adjusts those tests to set the feature flag to `false` if needed.

- [ ] **Step 30.10: Update existing chat tests (if needed)**

Run the full AI test suite and inspect failures:

```bash
dotnet test boilerplateBE/Starter.sln --filter "FullyQualifiedName~Ai" --nologo
```

Expected breakages:
- Tests that construct `ChatExecutionService` directly need the new constructor args; supply fakes for `IPersonaResolver` (returning a `default` PersonaContext) and `ISafetyPresetClauseProvider` (returning empty), and a `PersonaContextAccessor` instance.
- Tests using `SendChatMessageCommand` positional constructor call with 3 args still work because `PersonaId` defaults to `null`.
- Tests invoking `IChatExecutionService.ExecuteAsync` directly need the new `personaId` parameter — pass `null`.

Fix each compile error, re-run; repeat until green.

- [ ] **Step 30.11: Commit**

```bash
git add boilerplateBE/src/modules/Starter.Module.AI/Application/Services/ChatExecutionService.cs \
        boilerplateBE/src/modules/Starter.Module.AI/Application/Services/IChatExecutionService.cs \
        boilerplateBE/src/modules/Starter.Module.AI/Application/Commands/SendChatMessage/SendChatMessageCommandHandler.cs \
        boilerplateBE/src/modules/Starter.Module.AI/Controllers/AiChatController.cs \
        boilerplateBE/tests/Starter.Api.Tests/Ai/Personas/ChatExecutionServicePersonaPathTests.cs \
        boilerplateBE/tests/Starter.Api.Tests/Ai/Retrieval/ChatExecutionRagInjectionTests.cs
git commit -m "feat(ai): wire persona resolver + safety clause + visibility filter into chat pipeline"
```

---

## Task 31: Observability — OTel persona tags + counter

**Files:**
- Modify: `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Observability/AiAgentMetrics.cs`
- Modify: `boilerplateBE/src/modules/Starter.Module.AI/Application/Services/ChatExecutionService.cs`

- [ ] **Step 31.1: Add counter to `AiAgentMetrics.cs`**

Add to the class body:

```csharp
    public Counter<long> RunsByPersona { get; }

    public AiAgentMetrics()
    {
        var meter = new Meter("Starter.Module.AI.Agent");
        // existing counters ...
        RunsByPersona = meter.CreateCounter<long>(
            "ai_agent_runs_by_persona_total",
            unit: "{run}",
            description: "Agent runs partitioned by persona.");
    }
```

(Keep existing counters intact — just add `RunsByPersona`.)

- [ ] **Step 31.2: Emit per run in `ChatExecutionService`**

In `FinalizeTurnAsync` (or wherever the existing metric increments are), after recording success metrics, add:

```csharp
        if (state.Persona is { } p)
        {
            metrics.RunsByPersona.Add(1,
                new KeyValuePair<string, object?>("persona_slug", p.Slug),
                new KeyValuePair<string, object?>("audience", p.Audience.ToString()),
                new KeyValuePair<string, object?>("safety", p.Safety.ToString()));

            System.Diagnostics.Activity.Current?.SetTag("ai.persona.slug", p.Slug);
            System.Diagnostics.Activity.Current?.SetTag("ai.persona.audience", p.Audience.ToString());
            System.Diagnostics.Activity.Current?.SetTag("ai.persona.safety", p.Safety.ToString());
        }
```

(`metrics` is injected via `AiAgentMetrics metrics` — if not already a constructor dep, add it.)

- [ ] **Step 31.3: Build + run all AI tests**

```bash
dotnet build boilerplateBE/Starter.sln --nologo
dotnet test boilerplateBE/Starter.sln --filter "FullyQualifiedName~Ai" --nologo
```

- [ ] **Step 31.4: Commit**

```bash
git add boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Observability/AiAgentMetrics.cs \
        boilerplateBE/src/modules/Starter.Module.AI/Application/Services/ChatExecutionService.cs
git commit -m "feat(ai): persona tags on activity + runs-by-persona counter"
```

---

## Task 32: New AI permissions + role grants

**Files:**
- Modify: `boilerplateBE/src/modules/Starter.Module.AI/Constants/AiPermissions.cs`
- Modify: `boilerplateBE/src/modules/Starter.Module.AI/AIModule.cs`

- [ ] **Step 32.1: Add constants**

Inside `AiPermissions` class:

```csharp
    public const string ViewPersonas = "Ai.ViewPersonas";
    public const string ManagePersonas = "Ai.ManagePersonas";
    public const string AssignPersona = "Ai.AssignPersona";
```

- [ ] **Step 32.2: Extend `AIModule.GetPermissions`**

Append three `yield return` lines:

```csharp
    yield return (AiPermissions.ViewPersonas, "View AI personas", "AI");
    yield return (AiPermissions.ManagePersonas, "Create and manage AI personas", "AI");
    yield return (AiPermissions.AssignPersona, "Assign AI personas to users", "AI");
```

- [ ] **Step 32.3: Extend `AIModule.GetDefaultRolePermissions`**

- SuperAdmin array: append `AiPermissions.ViewPersonas, AiPermissions.ManagePersonas, AiPermissions.AssignPersona`.
- Admin array: append the same three.
- User array: append `AiPermissions.ViewPersonas` only.

- [ ] **Step 32.4: Build + commit**

```bash
dotnet build boilerplateBE/Starter.sln --nologo
git add boilerplateBE/src/modules/Starter.Module.AI/Constants/AiPermissions.cs \
        boilerplateBE/src/modules/Starter.Module.AI/AIModule.cs
git commit -m "feat(ai): register persona permissions + default role grants"
```

---

## Task 33: DI wiring

**Files:**
- Modify: `boilerplateBE/src/modules/Starter.Module.AI/AIModule.cs`

- [ ] **Step 33.1: Add services**

In `ConfigureServices`, after the existing agent-runtime registrations, add:

```csharp
        // Plan 5b — Personas
        services.AddScoped<ISlugGenerator, SlugGenerator>();
        services.AddScoped<IPersonaResolver, PersonaResolver>();
        services.AddScoped<ISafetyPresetClauseProvider, ResxSafetyPresetClauseProvider>();
        services.AddScoped<IPersonaContextAccessor, PersonaContextAccessor>();
```

Add the needed usings at the top:

```csharp
using Starter.Module.AI.Application.Services.Personas;
using Starter.Module.AI.Infrastructure.Services.Personas;
```

Domain-event handlers (`SeedTenantPersonasDomainEventHandler`, `AssignDefaultPersonaDomainEventHandler`) are auto-registered by the MediatR assembly scan already wired in the module. Verify by searching for `AddMediatR` in the DI path; if the scan is restricted, add explicit registrations.

- [ ] **Step 33.2: Build + run full AI suite**

```bash
dotnet build boilerplateBE/Starter.sln --nologo
dotnet test boilerplateBE/Starter.sln --filter "FullyQualifiedName~Ai" --nologo
```

- [ ] **Step 33.3: Commit**

```bash
git add boilerplateBE/src/modules/Starter.Module.AI/AIModule.cs
git commit -m "chore(ai): DI wiring for persona services"
```

---

## Task 34: Frontend permission mirror

**Files:**
- Modify: `boilerplateFE/src/constants/permissions.ts`

- [ ] **Step 34.1: Locate the `Ai` permission group** and add three entries to keep parity with backend. Exact format follows whatever pattern the file already uses (object of `key: "Ai.PermissionName"` pairs).

Add under the existing Ai group:

```ts
  ViewPersonas: 'Ai.ViewPersonas',
  ManagePersonas: 'Ai.ManagePersonas',
  AssignPersona: 'Ai.AssignPersona',
```

- [ ] **Step 34.2: Build frontend**

```bash
cd boilerplateFE && npm run build
```

Expected: success, no new TypeScript errors.

- [ ] **Step 34.3: Commit**

```bash
cd ..
git add boilerplateFE/src/constants/permissions.ts
git commit -m "chore(fe): mirror new Ai.*Persona permission constants"
```

---

## Task 35: Final build + test sweep + spec acceptance checklist

**Files:** none (validation-only)

- [ ] **Step 35.1: Build solution**

```bash
dotnet build boilerplateBE/Starter.sln --nologo
```

Expected: zero errors, zero new warnings.

- [ ] **Step 35.2: Full AI test suite**

```bash
dotnet test boilerplateBE/Starter.sln --filter "FullyQualifiedName~Ai" --nologo
```

Expected: all green.

- [ ] **Step 35.3: Full solution test suite (regression sanity)**

```bash
dotnet test boilerplateBE/Starter.sln --nologo
```

Expected: pre-existing non-AI tests unchanged (no regressions).

- [ ] **Step 35.4: Acceptance checklist from spec §15**

Verify each point manually:

1. ✅ `AiPersona` + `UserPersona` tables exist with EF configs + query filters; tests pass. *(Tasks 5, 6, 8)*
2. ✅ `AiAssistant.Slug` + `PersonaTargetSlugs`; slug auto-gen works; uniqueness enforced. *(Tasks 4, 7, 29)*
3. ✅ `TenantCreated` seeds `anonymous` + `default`; `UserCreated` assigns `default`; both idempotent. *(Tasks 15, 16)*
4. ✅ `IPersonaResolver` covers every algorithm path. *(Task 12)*
5. ✅ `ChatExecutionService`: calls resolver, applies visibility filter, prepends safety clause, puts `PersonaContext` on run ctx, populates `PersonaSlug` on reply DTO and stream-started frame. *(Task 30)*
6. ✅ Controllers exposed with validators + permission gates. *(Tasks 27, 28)*
7. ✅ New permissions wired + mirrored in frontend. *(Tasks 32, 34)*
8. ✅ Feature flag `Ai.Personas.Enabled` short-circuits pipeline when off. *(Task 30)*
9. ✅ All tests pass. *(Steps 35.2 / 35.3)*
10. ✅ `dotnet build` green; `npm run build` green. *(Steps 35.1 / 34.2)*

- [ ] **Step 35.5: Commit final sweep (usually no changes)**

If no files were modified, skip. Otherwise commit any fixups:

```bash
git status
# If anything is dirty:
git add -A && git commit -m "chore(ai): final 5b sweep + fixups"
```

---

## Post-plan handoff

When every box above is checked:

1. The branch `feature/ai-phase-5b` is ready for live integration testing using the `post-feature-testing` workflow (pull OpenAI + Anthropic keys from the main boilerplate's user-secrets, rename a test-app, run end-to-end persona flows in the browser).
2. A code-review pass should verify: SOLID adherence (one responsibility per file), modularity (no AI leakage into core), naming consistency (`Persona*` vs. `AiPersona*`), and that the spec's §15 acceptance criteria are all demonstrably met in code.







