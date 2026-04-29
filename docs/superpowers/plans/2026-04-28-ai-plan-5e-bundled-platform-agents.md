# Plan 5e — Bundled Platform Agents Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ship the four flagship-vision-required bundled agent templates (`Platform Insights Agent` × 2 providers + `Support Copilot` + `Teacher Tutor` + `Brand Content`), wire `SafetyPresetHint` → `AiAssistant.SafetyPresetOverride` (load-bearing post-5d-2), seed the six flagship demo personas per tenant, and decorate three read-only queries with `[AiTool]` so Platform Insights can deliver on its "users / audit log / billing usage" pitch.

**Architecture:** All five templates are pure-data classes implementing `IAiAgentTemplate`; they live in core (`Starter.Application/Features/Ai/Templates/`) and are scanned by the existing 5c-2 per-module discovery. The interface field `SafetyPresetHint` is renamed to `SafetyPresetOverride` and the install handler stamps it onto the new `AiAssistant.SafetyPresetOverride` field added in 5d-2. Six new flagship demo persona factories on `AiPersona` are seeded for every tenant via the existing `TenantCreatedEvent` handler plus a new idempotent backfill for tenants that already exist. Three pure attribute additions on `GetAuditLogsQuery`, `GetAllSubscriptionsQuery`, `GetUsageQuery` complete the Platform Insights tool surface.

**Tech Stack:** .NET 10, MediatR, EF Core (PostgreSQL), xUnit, Moq, FluentAssertions. AI-module-specific: 5c-1 `[AiTool]` discovery, 5c-2 `IAiAgentTemplate` discovery, 5d-2 `AiAssistant.SetSafetyPreset` aggregate API.

**Spec:** [`docs/superpowers/specs/2026-04-28-ai-plan-5e-bundled-platform-agents-design.md`](../specs/2026-04-28-ai-plan-5e-bundled-platform-agents-design.md)

---

## File structure

| Path | Purpose |
|---|---|
| `boilerplateBE/src/Starter.Abstractions/Capabilities/IAiAgentTemplate.cs` | **Modify** — rename `SafetyPresetHint` → `SafetyPresetOverride` + doc-comment. |
| `boilerplateBE/src/Starter.Abstractions/Capabilities/AiAgentTemplateRegistration.cs` | **Modify** — delegate `SafetyPresetOverride` instead of `SafetyPresetHint`. |
| `boilerplateBE/src/modules/Starter.Module.AI/Application/DTOs/AiAgentTemplateDto.cs` | **Modify** — rename DTO field. |
| `boilerplateBE/src/modules/Starter.Module.AI/Application/DTOs/AiAgentTemplateMappers.cs` | **Modify** — map renamed property. |
| `boilerplateBE/src/Starter.Application/Features/Ai/Templates/SupportAssistantAnthropicTemplate.cs` | **Modify** — rename property; `Standard` → `null`. |
| `boilerplateBE/src/Starter.Application/Features/Ai/Templates/SupportAssistantOpenAiTemplate.cs` | **Modify** — same. |
| `boilerplateBE/src/modules/Starter.Module.Products/Application/Templates/ProductExpertAnthropicTemplate.cs` | **Modify** — same. |
| `boilerplateBE/src/modules/Starter.Module.Products/Application/Templates/ProductExpertOpenAiTemplate.cs` | **Modify** — same. |
| `boilerplateBE/tests/Starter.Api.Tests/Ai/Templates/AiAgentTemplateDiscoveryFixtures.cs` | **Modify** — rename fixture parameter + property. |
| `boilerplateBE/tests/Starter.Api.Tests/Ai/Templates/AiAgentTemplateMappersTests.cs` | **Modify** — assert renamed DTO field. |
| `boilerplateBE/tests/Starter.Api.Tests/Ai/Templates/AiAgentTemplateRegistrationTests.cs` | **Modify** — fixture rename. |
| `boilerplateBE/src/modules/Starter.Module.AI/Application/Commands/InstallTemplate/InstallTemplateCommandHandler.cs` | **Modify** — add step 8.5: stamp safety override onto assistant. |
| `boilerplateBE/tests/Starter.Api.Tests/Ai/Templates/InstallTemplateCommandHandlerSafetyOverrideTests.cs` | **Create** — handler unit tests for step 8.5. |
| `boilerplateBE/src/modules/Starter.Module.AI/Domain/Entities/AiPersona.cs` | **Modify** — six new slug constants + factory methods. |
| `boilerplateBE/tests/Starter.Api.Tests/Ai/Personas/AiPersonaFlagshipFactoriesTests.cs` | **Create** — unit tests for the six factory methods. |
| `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/EventHandlers/SeedTenantPersonasDomainEventHandler.cs` | **Modify** — seed all eight personas (anonymous + default + 6 flagship). |
| `boilerplateBE/tests/Starter.Api.Tests/Ai/Personas/SeedTenantPersonasDomainEventHandlerTests.cs` | **Create** — covers new-tenant seed and idempotency. |
| `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Persistence/Seed/FlagshipPersonasBackfillSeed.cs` | **Create** — idempotent backfill for existing tenants. |
| `boilerplateBE/tests/Starter.Api.Tests/Ai/Personas/FlagshipPersonasBackfillSeedTests.cs` | **Create** — covers cross-tenant backfill + idempotency. |
| `boilerplateBE/src/modules/Starter.Module.AI/AIModule.cs` | **Modify** — wire backfill seed in always-on block. |
| `boilerplateBE/src/Starter.Application/Features/AuditLogs/Queries/GetAuditLogs/GetAuditLogsQuery.cs` | **Modify** — `[AiTool]` attribute. |
| `boilerplateBE/src/modules/Starter.Module.Billing/Application/Queries/GetAllSubscriptions/GetAllSubscriptionsQuery.cs` | **Modify** — `[AiTool]` attribute. |
| `boilerplateBE/src/modules/Starter.Module.Billing/Application/Queries/GetUsage/GetUsageQuery.cs` | **Modify** — `[AiTool]` attribute. |
| `boilerplateBE/src/Starter.Application/Features/Ai/Templates/PlatformInsightsPrompts.cs` | **Create** — shared prompt + description. |
| `boilerplateBE/src/Starter.Application/Features/Ai/Templates/PlatformInsightsAnthropicTemplate.cs` | **Create** — Anthropic variant. |
| `boilerplateBE/src/Starter.Application/Features/Ai/Templates/PlatformInsightsOpenAiTemplate.cs` | **Create** — OpenAI variant. |
| `boilerplateBE/src/Starter.Application/Features/Ai/Templates/SupportCopilotPrompts.cs` | **Create** — prompt. |
| `boilerplateBE/src/Starter.Application/Features/Ai/Templates/SupportCopilotTemplate.cs` | **Create** — template class. |
| `boilerplateBE/src/Starter.Application/Features/Ai/Templates/TeacherTutorPrompts.cs` | **Create** — prompt. |
| `boilerplateBE/src/Starter.Application/Features/Ai/Templates/TeacherTutorTemplate.cs` | **Create** — template class. |
| `boilerplateBE/src/Starter.Application/Features/Ai/Templates/BrandContentPrompts.cs` | **Create** — prompt. |
| `boilerplateBE/src/Starter.Application/Features/Ai/Templates/BrandContentTemplate.cs` | **Create** — template class. |
| `boilerplateBE/tests/Starter.Api.Tests/Ai/AcidTests/Plan5eAcidTests.cs` | **Create** — flagship acid tests. |
| `docs/superpowers/specs/2026-04-25-ai-plan-5c-2-agent-templates-design.md` | **Modify** — append "Superseded by 5e" pointer to §3.1. |

---

## Tooling reference (used in every task)

```bash
# All commands assume cwd = repo root.

# Build the whole BE solution.
dotnet build boilerplateBE/Starter.sln

# Run a focused test by FQN substring.
dotnet test boilerplateBE/Starter.sln --filter "FullyQualifiedName~Plan5e"

# Run a single test method by full identifier.
dotnet test boilerplateBE/Starter.sln \
  --filter "FullyQualifiedName=Starter.Api.Tests.Ai.Templates.InstallTemplateCommandHandlerSafetyOverrideTests.Sets_assistant_override_when_template_has_explicit_value"
```

Tests in this repo use xUnit + Moq + FluentAssertions. `AiDbContext` is created in-memory via `DbContextOptionsBuilder<AiDbContext>().UseInMemoryDatabase(...)`. Look at [`InstallTemplateCommandHandlerTests.cs`](../../../boilerplateBE/tests/Starter.Api.Tests/Ai/Templates/InstallTemplateCommandHandlerTests.cs) for the exact `Setup(...)` helper shape if a test needs the same dependencies.

---

## Task 1: Rename `SafetyPresetHint` → `SafetyPresetOverride` (interface + DTO + decorator + fixture)

**Why:** 5c-2 left this field as "informational hint"; 5d-2 hoisted safety to the assistant; the field is now load-bearing on install. Renaming first lets every downstream reference compile coherently before we wire behaviour.

**Files:**
- Modify: `boilerplateBE/src/Starter.Abstractions/Capabilities/IAiAgentTemplate.cs:39-43`
- Modify: `boilerplateBE/src/Starter.Abstractions/Capabilities/AiAgentTemplateRegistration.cs:21`
- Modify: `boilerplateBE/src/modules/Starter.Module.AI/Application/DTOs/AiAgentTemplateDto.cs:16`
- Modify: `boilerplateBE/src/modules/Starter.Module.AI/Application/DTOs/AiAgentTemplateMappers.cs:20`
- Modify: `boilerplateBE/tests/Starter.Api.Tests/Ai/Templates/AiAgentTemplateDiscoveryFixtures.cs:87, 101`

- [ ] **Step 1: Update the interface**

In `IAiAgentTemplate.cs`, replace the existing `SafetyPresetHint` doc-comment + property (lines 39-43, ending with `SafetyPreset? SafetyPresetHint { get; }`) with:

```csharp
    /// <summary>
    /// Persisted to <c>AiAssistant.SafetyPresetOverride</c> on install by
    /// <c>InstallTemplateCommandHandler</c>. <c>null</c> means
    /// "inherit from the resolved persona's safety preset at runtime".
    /// </summary>
    SafetyPreset? SafetyPresetOverride { get; }
```

- [ ] **Step 2: Update the registration decorator**

In `AiAgentTemplateRegistration.cs`, replace line 21:

```csharp
    public SafetyPreset? SafetyPresetOverride => inner.SafetyPresetOverride;
```

- [ ] **Step 3: Update the DTO**

In `AiAgentTemplateDto.cs`, replace line 16:

```csharp
    string? SafetyPresetOverride);
```

- [ ] **Step 4: Update the mapper**

In `AiAgentTemplateMappers.cs`, replace line 20:

```csharp
        SafetyPresetOverride: template.SafetyPresetOverride?.ToString());
```

- [ ] **Step 5: Update the test fixture**

In `AiAgentTemplateDiscoveryFixtures.cs`, change the constructor parameter on line 87 from `SafetyPreset? safetyHint = null` to `SafetyPreset? safetyOverride = null`, and the property on line 101 from `SafetyPresetHint { get; } = safetyHint;` to `SafetyPresetOverride { get; } = safetyOverride;`.

- [ ] **Step 6: Update existing fixture call sites**

In `AiAgentTemplateMappersTests.cs`, change line 48 from `safetyHint: null` to `safetyOverride: null`. Lines 33 and 51 read `dto.SafetyPresetHint` — update both to `dto.SafetyPresetOverride`.

In `AiAgentTemplateRegistrationTests.cs`, line 36 declares a local class with a `SafetyPresetHint` property — rename it to `SafetyPresetOverride` and (if the test asserts on the value) update the assertion.

- [ ] **Step 7: Build and verify only the four 5c-2 templates fail to compile**

Run: `dotnet build boilerplateBE/Starter.sln`

Expected: **4 errors** — the four 5c-2 templates (`SupportAssistantAnthropicTemplate`, `SupportAssistantOpenAiTemplate`, `ProductExpertAnthropicTemplate`, `ProductExpertOpenAiTemplate`) still implement the old name. We fix them in Task 2 — the build break is the canary that the rename hit every consumer.

- [ ] **Step 8: Update the four existing 5c-2 templates**

For each of the four template files listed under "Files" of Task 2 below, replace line 20 (`public SafetyPreset? SafetyPresetHint => SafetyPreset.Standard;`) with:

```csharp
    public SafetyPreset? SafetyPresetOverride => null;
```

(The semantic change — `Standard` → `null` — is intentional; it expresses "no override; inherit from persona-default" instead of "explicit override that happens to match the persona-default Standard". Runtime behaviour is unchanged because the seeded `default` persona has `SafetyPreset = Standard`.)

- [ ] **Step 9: Build green**

Run: `dotnet build boilerplateBE/Starter.sln`

Expected: 0 errors, 0 warnings.

- [ ] **Step 10: Run all template tests**

Run: `dotnet test boilerplateBE/Starter.sln --filter "FullyQualifiedName~Ai.Templates"`

Expected: all green. The mapper / discovery / registration tests assert on the renamed field via the updated fixture and DTO.

- [ ] **Step 11: Commit**

```bash
git add \
  boilerplateBE/src/Starter.Abstractions/Capabilities/IAiAgentTemplate.cs \
  boilerplateBE/src/Starter.Abstractions/Capabilities/AiAgentTemplateRegistration.cs \
  boilerplateBE/src/modules/Starter.Module.AI/Application/DTOs/AiAgentTemplateDto.cs \
  boilerplateBE/src/modules/Starter.Module.AI/Application/DTOs/AiAgentTemplateMappers.cs \
  boilerplateBE/src/Starter.Application/Features/Ai/Templates/SupportAssistantAnthropicTemplate.cs \
  boilerplateBE/src/Starter.Application/Features/Ai/Templates/SupportAssistantOpenAiTemplate.cs \
  boilerplateBE/src/modules/Starter.Module.Products/Application/Templates/ProductExpertAnthropicTemplate.cs \
  boilerplateBE/src/modules/Starter.Module.Products/Application/Templates/ProductExpertOpenAiTemplate.cs \
  boilerplateBE/tests/Starter.Api.Tests/Ai/Templates/AiAgentTemplateDiscoveryFixtures.cs \
  boilerplateBE/tests/Starter.Api.Tests/Ai/Templates/AiAgentTemplateMappersTests.cs \
  boilerplateBE/tests/Starter.Api.Tests/Ai/Templates/AiAgentTemplateRegistrationTests.cs
git commit -m "refactor(ai): 5e — rename IAiAgentTemplate.SafetyPresetHint → SafetyPresetOverride; existing templates inherit"
```

---

## Task 2: Wire `InstallTemplateCommandHandler` step 8.5

**Why:** The renamed field still doesn't influence the install path. This task wires the assistant's `SafetyPresetOverride` to the template's value (TDD-first).

**Files:**
- Create: `boilerplateBE/tests/Starter.Api.Tests/Ai/Templates/InstallTemplateCommandHandlerSafetyOverrideTests.cs`
- Modify: `boilerplateBE/src/modules/Starter.Module.AI/Application/Commands/InstallTemplate/InstallTemplateCommandHandler.cs:128-138` (between `SetVisibility` and `StampTemplateSource`)

- [ ] **Step 1: Write the failing tests**

Create `boilerplateBE/tests/Starter.Api.Tests/Ai/Templates/InstallTemplateCommandHandlerSafetyOverrideTests.cs`:

```csharp
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
using Starter.Module.AI.Infrastructure.Persistence;
using Starter.Shared.Constants;
using Xunit;

namespace Starter.Api.Tests.Ai.Templates;

public class InstallTemplateCommandHandlerSafetyOverrideTests
{
    [Fact]
    public async Task Sets_assistant_override_when_template_has_explicit_value()
    {
        var (handler, db, _) = Setup(SafetyPreset.ChildSafe);

        var result = await handler.Handle(
            new InstallTemplateCommand("safety_test"), default);

        result.IsSuccess.Should().BeTrue();
        var assistant = await db.AiAssistants.SingleAsync();
        assistant.SafetyPresetOverride.Should().Be(SafetyPreset.ChildSafe);
    }

    [Fact]
    public async Task Leaves_assistant_override_null_when_template_override_is_null()
    {
        var (handler, db, _) = Setup(safetyOverride: null);

        var result = await handler.Handle(
            new InstallTemplateCommand("safety_test"), default);

        result.IsSuccess.Should().BeTrue();
        var assistant = await db.AiAssistants.SingleAsync();
        assistant.SafetyPresetOverride.Should().BeNull();
    }

    private static (
        InstallTemplateCommandHandler handler,
        AiDbContext db,
        Mock<ICurrentUserService> currentUser)
        Setup(SafetyPreset? safetyOverride)
    {
        var tenantId = Guid.NewGuid();
        var cu = new Mock<ICurrentUserService>();
        cu.SetupGet(x => x.UserId).Returns(Guid.NewGuid());
        cu.SetupGet(x => x.TenantId).Returns(tenantId);
        cu.Setup(x => x.IsInRole(Roles.SuperAdmin)).Returns(false);

        var dbOpts = new DbContextOptionsBuilder<AiDbContext>()
            .UseInMemoryDatabase($"safety-{Guid.NewGuid()}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        var db = new AiDbContext(dbOpts, cu.Object);

        // Seed the default persona that the template targets.
        db.AiPersonas.Add(AiPersona.CreateDefault(tenantId, Guid.NewGuid()));
        db.SaveChanges();

        var template = new TestTemplate(
            slug: "safety_test",
            personas: new[] { "default" },
            safetyOverride: safetyOverride);
        var registry = new AiAgentTemplateRegistry(new[] { (IAiAgentTemplate)template });

        var toolReg = new Mock<IAiToolRegistry>();
        var ff = new Mock<IFeatureFlagService>();
        ff.Setup(x => x.GetValueAsync<int>("ai.agents.max_count", It.IsAny<CancellationToken>()))
            .ReturnsAsync(int.MaxValue);

        var handler = new InstallTemplateCommandHandler(
            db, registry, toolReg.Object, cu.Object, ff.Object);
        return (handler, db, cu);
    }
}
```

(`TestTemplate` is the existing fixture in `AiAgentTemplateDiscoveryFixtures.cs`. The constructor parameter is `safetyOverride` after the Task 1 rename.)

- [ ] **Step 2: Run the tests, expect failures**

Run: `dotnet test boilerplateBE/Starter.sln --filter "FullyQualifiedName~InstallTemplateCommandHandlerSafetyOverrideTests"`

Expected: both tests **FAIL**. The first fails because `assistant.SafetyPresetOverride` is `null` (handler never sets it); the second passes accidentally on the same default but for the wrong reason — what matters is the first one fails.

- [ ] **Step 3: Wire step 8.5 in the install handler**

Open `InstallTemplateCommandHandler.cs`. Locate the block ending at line 134 (`assistant.SetVisibility(ResourceVisibility.TenantWide);`). Insert this block immediately after it, before the existing line 137 (`assistant.StampTemplateSource(...)`):

```csharp
        // 8.5 Apply template safety override (Plan 5e). When null, the assistant
        // inherits the resolved persona's safety preset at runtime; the explicit
        // null-skip avoids raising a no-op domain event if SetSafetyPreset ever
        // does so in the future.
        if (template.SafetyPresetOverride.HasValue)
            assistant.SetSafetyPreset(template.SafetyPresetOverride);
```

- [ ] **Step 4: Run the tests, expect green**

Run: `dotnet test boilerplateBE/Starter.sln --filter "FullyQualifiedName~InstallTemplateCommandHandlerSafetyOverrideTests"`

Expected: both PASS.

- [ ] **Step 5: Run the wider install-handler suite to confirm no regressions**

Run: `dotnet test boilerplateBE/Starter.sln --filter "FullyQualifiedName~InstallTemplateCommandHandler"`

Expected: all green (existing 5c-2 tests unchanged).

- [ ] **Step 6: Commit**

```bash
git add \
  boilerplateBE/src/modules/Starter.Module.AI/Application/Commands/InstallTemplate/InstallTemplateCommandHandler.cs \
  boilerplateBE/tests/Starter.Api.Tests/Ai/Templates/InstallTemplateCommandHandlerSafetyOverrideTests.cs
git commit -m "feat(ai): 5e — InstallTemplateCommandHandler stamps SafetyPresetOverride from template"
```

---

## Task 3: `AiPersona` — six new flagship factory methods + slug constants

**Why:** Templates `teacher_tutor` (Task 9) and `brand_content` (Task 10) declare `student` and `editor` as their `PersonaTargetSlugs`. The install handler validates persona existence, so the personas must be seedable first. Doing it via factory methods (mirroring `CreateAnonymous` / `CreateDefault`) keeps the entity self-describing.

**Files:**
- Modify: `boilerplateBE/src/modules/Starter.Module.AI/Domain/Entities/AiPersona.cs:11-12, after line 108`
- Create: `boilerplateBE/tests/Starter.Api.Tests/Ai/Personas/AiPersonaFlagshipFactoriesTests.cs`

- [ ] **Step 1: Write the failing tests**

Create the directory if needed (`mkdir -p boilerplateBE/tests/Starter.Api.Tests/Ai/Personas`) then create `AiPersonaFlagshipFactoriesTests.cs`:

```csharp
using FluentAssertions;
using Starter.Abstractions.Ai;
using Starter.Module.AI.Domain.Entities;
using Starter.Module.AI.Domain.Enums;
using Xunit;

namespace Starter.Api.Tests.Ai.Personas;

public class AiPersonaFlagshipFactoriesTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid Actor    = Guid.NewGuid();

    [Fact]
    public void Student_persona_is_internal_childsafe_active_not_reserved()
    {
        var p = AiPersona.CreateStudent(TenantId, Actor);
        p.Slug.Should().Be(AiPersona.StudentSlug).And.Be("student");
        p.AudienceType.Should().Be(PersonaAudienceType.Internal);
        p.SafetyPreset.Should().Be(SafetyPreset.ChildSafe);
        p.IsActive.Should().BeTrue();
        p.IsSystemReserved.Should().BeFalse();
    }

    [Fact]
    public void Teacher_persona_is_internal_standard()
    {
        var p = AiPersona.CreateTeacher(TenantId, Actor);
        p.Slug.Should().Be(AiPersona.TeacherSlug).And.Be("teacher");
        p.AudienceType.Should().Be(PersonaAudienceType.Internal);
        p.SafetyPreset.Should().Be(SafetyPreset.Standard);
    }

    [Fact]
    public void Parent_persona_is_endcustomer_standard()
    {
        var p = AiPersona.CreateParent(TenantId, Actor);
        p.Slug.Should().Be(AiPersona.ParentSlug).And.Be("parent");
        p.AudienceType.Should().Be(PersonaAudienceType.EndCustomer);
        p.SafetyPreset.Should().Be(SafetyPreset.Standard);
    }

    [Fact]
    public void Editor_persona_is_internal_standard()
    {
        var p = AiPersona.CreateEditor(TenantId, Actor);
        p.Slug.Should().Be(AiPersona.EditorSlug).And.Be("editor");
        p.AudienceType.Should().Be(PersonaAudienceType.Internal);
        p.SafetyPreset.Should().Be(SafetyPreset.Standard);
    }

    [Fact]
    public void Approver_persona_is_internal_standard()
    {
        var p = AiPersona.CreateApprover(TenantId, Actor);
        p.Slug.Should().Be(AiPersona.ApproverSlug).And.Be("approver");
        p.AudienceType.Should().Be(PersonaAudienceType.Internal);
        p.SafetyPreset.Should().Be(SafetyPreset.Standard);
    }

    [Fact]
    public void Client_persona_is_endcustomer_professionally_moderated()
    {
        var p = AiPersona.CreateClient(TenantId, Actor);
        p.Slug.Should().Be(AiPersona.ClientSlug).And.Be("client");
        p.AudienceType.Should().Be(PersonaAudienceType.EndCustomer);
        p.SafetyPreset.Should().Be(SafetyPreset.ProfessionalModerated);
    }
}
```

- [ ] **Step 2: Run tests, expect compile failure**

Run: `dotnet test boilerplateBE/Starter.sln --filter "FullyQualifiedName~AiPersonaFlagshipFactoriesTests"`

Expected: BUILD FAILS with "AiPersona does not contain a definition for 'StudentSlug' / 'CreateStudent' / …".

- [ ] **Step 3: Add slug constants and factory methods to `AiPersona`**

In `AiPersona.cs`, replace lines 11-12:

```csharp
    public const string AnonymousSlug = "anonymous";
    public const string DefaultSlug   = "default";
    public const string StudentSlug   = "student";
    public const string TeacherSlug   = "teacher";
    public const string ParentSlug    = "parent";
    public const string EditorSlug    = "editor";
    public const string ApproverSlug  = "approver";
    public const string ClientSlug    = "client";
```

Then, after the existing `CreateDefault` method (after line 108 `createdByUserId);`), insert the six new factories:

```csharp

    public static AiPersona CreateStudent(Guid tenantId, Guid createdByUserId) =>
        new(
            Guid.NewGuid(),
            tenantId,
            StudentSlug,
            "Student",
            "Boilerplate flagship demo persona — school-age learner. ChildSafe by default.",
            PersonaAudienceType.Internal,
            SafetyPreset.ChildSafe,
            isSystemReserved: false,
            isActive: true,
            createdByUserId);

    public static AiPersona CreateTeacher(Guid tenantId, Guid createdByUserId) =>
        new(
            Guid.NewGuid(),
            tenantId,
            TeacherSlug,
            "Teacher",
            "Boilerplate flagship demo persona — classroom teacher / instructor.",
            PersonaAudienceType.Internal,
            SafetyPreset.Standard,
            isSystemReserved: false,
            isActive: true,
            createdByUserId);

    public static AiPersona CreateParent(Guid tenantId, Guid createdByUserId) =>
        new(
            Guid.NewGuid(),
            tenantId,
            ParentSlug,
            "Parent",
            "Boilerplate flagship demo persona — parent / guardian (end-customer).",
            PersonaAudienceType.EndCustomer,
            SafetyPreset.Standard,
            isSystemReserved: false,
            isActive: true,
            createdByUserId);

    public static AiPersona CreateEditor(Guid tenantId, Guid createdByUserId) =>
        new(
            Guid.NewGuid(),
            tenantId,
            EditorSlug,
            "Editor",
            "Boilerplate flagship demo persona — content editor / copywriter.",
            PersonaAudienceType.Internal,
            SafetyPreset.Standard,
            isSystemReserved: false,
            isActive: true,
            createdByUserId);

    public static AiPersona CreateApprover(Guid tenantId, Guid createdByUserId) =>
        new(
            Guid.NewGuid(),
            tenantId,
            ApproverSlug,
            "Approver",
            "Boilerplate flagship demo persona — content reviewer / approver.",
            PersonaAudienceType.Internal,
            SafetyPreset.Standard,
            isSystemReserved: false,
            isActive: true,
            createdByUserId);

    public static AiPersona CreateClient(Guid tenantId, Guid createdByUserId) =>
        new(
            Guid.NewGuid(),
            tenantId,
            ClientSlug,
            "Client",
            "Boilerplate flagship demo persona — external client (end-customer). ProfessionallyModerated.",
            PersonaAudienceType.EndCustomer,
            SafetyPreset.ProfessionalModerated,
            isSystemReserved: false,
            isActive: true,
            createdByUserId);
```

- [ ] **Step 4: Run the tests, expect green**

Run: `dotnet test boilerplateBE/Starter.sln --filter "FullyQualifiedName~AiPersonaFlagshipFactoriesTests"`

Expected: all six PASS.

- [ ] **Step 5: Run the wider AI test suite to confirm no regressions**

Run: `dotnet test boilerplateBE/Starter.sln --filter "FullyQualifiedName~Ai"`

Expected: all green.

- [ ] **Step 6: Commit**

```bash
git add \
  boilerplateBE/src/modules/Starter.Module.AI/Domain/Entities/AiPersona.cs \
  boilerplateBE/tests/Starter.Api.Tests/Ai/Personas/AiPersonaFlagshipFactoriesTests.cs
git commit -m "feat(ai): 5e — AiPersona flagship demo factories (student/teacher/parent/editor/approver/client)"
```

---

## Task 4: Extend `SeedTenantPersonasDomainEventHandler` to seed all eight personas

**Why:** New tenants must come up with the full flagship-persona set so future flagship templates install without seed-side edits.

**Files:**
- Modify: `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/EventHandlers/SeedTenantPersonasDomainEventHandler.cs`
- Create: `boilerplateBE/tests/Starter.Api.Tests/Ai/Personas/SeedTenantPersonasDomainEventHandlerTests.cs`

- [ ] **Step 1: Write the failing test**

Create `SeedTenantPersonasDomainEventHandlerTests.cs`:

```csharp
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Starter.Application.Common.Interfaces;
using Starter.Domain.Tenants.Events;
using Starter.Module.AI.Domain.Entities;
using Starter.Module.AI.Infrastructure.EventHandlers;
using Starter.Module.AI.Infrastructure.Persistence;
using Xunit;

namespace Starter.Api.Tests.Ai.Personas;

public class SeedTenantPersonasDomainEventHandlerTests
{
    [Fact]
    public async Task Handle_for_new_tenant_seeds_all_eight_personas()
    {
        var (db, handler, tenantId) = Setup();

        await handler.Handle(new TenantCreatedEvent(tenantId), default);

        var slugs = await db.AiPersonas
            .Where(p => p.TenantId == tenantId)
            .Select(p => p.Slug)
            .OrderBy(s => s)
            .ToListAsync();

        slugs.Should().BeEquivalentTo(new[]
        {
            "anonymous", "approver", "client", "default",
            "editor", "parent", "student", "teacher",
        });
    }

    [Fact]
    public async Task Handle_is_idempotent_when_some_personas_already_exist()
    {
        var (db, handler, tenantId) = Setup();
        db.AiPersonas.Add(AiPersona.CreateDefault(tenantId, Guid.NewGuid()));
        db.AiPersonas.Add(AiPersona.CreateStudent(tenantId, Guid.NewGuid()));
        await db.SaveChangesAsync();

        await handler.Handle(new TenantCreatedEvent(tenantId), default);

        (await db.AiPersonas.CountAsync(p => p.TenantId == tenantId)).Should().Be(8);
    }

    private static (
        AiDbContext db,
        SeedTenantPersonasDomainEventHandler handler,
        Guid tenantId)
        Setup()
    {
        var tenantId = Guid.NewGuid();
        var cu = new Mock<ICurrentUserService>();
        cu.SetupGet(x => x.UserId).Returns(Guid.NewGuid());
        cu.SetupGet(x => x.TenantId).Returns(tenantId);

        var opts = new DbContextOptionsBuilder<AiDbContext>()
            .UseInMemoryDatabase($"persona-seed-{Guid.NewGuid()}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        var db = new AiDbContext(opts, cu.Object);
        var handler = new SeedTenantPersonasDomainEventHandler(
            db, NullLogger<SeedTenantPersonasDomainEventHandler>.Instance);
        return (db, handler, tenantId);
    }
}
```

> **Note:** `TenantCreatedEvent` is a single-arg event taking `Guid TenantId` (`Tenant.Create` raises `new TenantCreatedEvent(tenant.Id)` at `boilerplateBE/src/Starter.Domain/Tenants/Entities/Tenant.cs:63`). If the signature has changed by execution time, update the test calls to match the current constructor.

- [ ] **Step 2: Run, expect first test to fail**

Run: `dotnet test boilerplateBE/Starter.sln --filter "FullyQualifiedName~SeedTenantPersonasDomainEventHandlerTests"`

Expected: `Handle_for_new_tenant_seeds_all_eight_personas` **FAILS** (only 2 personas seeded). The idempotency test may pass accidentally — both tests should pass after the fix.

- [ ] **Step 3: Extend the handler**

Replace the contents of `SeedTenantPersonasDomainEventHandler.cs` with:

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

    private static readonly IReadOnlyList<string> AllSlugs = new[]
    {
        AiPersona.AnonymousSlug, AiPersona.DefaultSlug,
        AiPersona.StudentSlug,   AiPersona.TeacherSlug, AiPersona.ParentSlug,
        AiPersona.EditorSlug,    AiPersona.ApproverSlug, AiPersona.ClientSlug,
    };

    public async Task Handle(TenantCreatedEvent notification, CancellationToken cancellationToken)
    {
        var tenantId = notification.TenantId;

        var existing = await db.AiPersonas
            .IgnoreQueryFilters()
            .Where(p => p.TenantId == tenantId && AllSlugs.Contains(p.Slug))
            .Select(p => p.Slug)
            .ToListAsync(cancellationToken);
        var have = existing.ToHashSet(StringComparer.Ordinal);

        var added = 0;
        if (!have.Contains(AiPersona.AnonymousSlug)) { db.AiPersonas.Add(AiPersona.CreateAnonymous(tenantId, SystemSeedActor)); added++; }
        if (!have.Contains(AiPersona.DefaultSlug))   { db.AiPersonas.Add(AiPersona.CreateDefault  (tenantId, SystemSeedActor)); added++; }
        if (!have.Contains(AiPersona.StudentSlug))   { db.AiPersonas.Add(AiPersona.CreateStudent  (tenantId, SystemSeedActor)); added++; }
        if (!have.Contains(AiPersona.TeacherSlug))   { db.AiPersonas.Add(AiPersona.CreateTeacher  (tenantId, SystemSeedActor)); added++; }
        if (!have.Contains(AiPersona.ParentSlug))    { db.AiPersonas.Add(AiPersona.CreateParent   (tenantId, SystemSeedActor)); added++; }
        if (!have.Contains(AiPersona.EditorSlug))    { db.AiPersonas.Add(AiPersona.CreateEditor   (tenantId, SystemSeedActor)); added++; }
        if (!have.Contains(AiPersona.ApproverSlug))  { db.AiPersonas.Add(AiPersona.CreateApprover (tenantId, SystemSeedActor)); added++; }
        if (!have.Contains(AiPersona.ClientSlug))    { db.AiPersonas.Add(AiPersona.CreateClient   (tenantId, SystemSeedActor)); added++; }

        if (added > 0)
        {
            await db.SaveChangesAsync(cancellationToken);
            logger.LogInformation(
                "Seeded {Count} personas for tenant {TenantId} (had {ExistingCount} of 8).",
                added, tenantId, existing.Count);
        }
    }
}
```

- [ ] **Step 4: Run tests, expect green**

Run: `dotnet test boilerplateBE/Starter.sln --filter "FullyQualifiedName~SeedTenantPersonasDomainEventHandlerTests"`

Expected: both PASS.

- [ ] **Step 5: Commit**

```bash
git add \
  boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/EventHandlers/SeedTenantPersonasDomainEventHandler.cs \
  boilerplateBE/tests/Starter.Api.Tests/Ai/Personas/SeedTenantPersonasDomainEventHandlerTests.cs
git commit -m "feat(ai): 5e — TenantCreatedEvent seeds all eight personas (anonymous + default + 6 flagship)"
```

---

## Task 5: `FlagshipPersonasBackfillSeed` — idempotent seed for existing tenants

**Why:** Tenants created before 5e shipped only have `anonymous` + `default`. A startup seed walks every tenant and adds the missing flagship demo personas.

**Files:**
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Persistence/Seed/FlagshipPersonasBackfillSeed.cs`
- Create: `boilerplateBE/tests/Starter.Api.Tests/Ai/Personas/FlagshipPersonasBackfillSeedTests.cs`

- [ ] **Step 1: Write the failing tests**

Create `FlagshipPersonasBackfillSeedTests.cs`:

```csharp
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Moq;
using Starter.Application.Common.Interfaces;
using Starter.Module.AI.Domain.Entities;
using Starter.Module.AI.Infrastructure.Persistence;
using Starter.Module.AI.Infrastructure.Persistence.Seed;
using Xunit;

namespace Starter.Api.Tests.Ai.Personas;

public class FlagshipPersonasBackfillSeedTests
{
    [Fact]
    public async Task Adds_missing_flagship_personas_for_each_tenant()
    {
        var (db, appDb, tenantA, tenantB) = await SetupAsync();

        // Tenant A has only anonymous + default (legacy state).
        db.AiPersonas.Add(AiPersona.CreateAnonymous(tenantA, Guid.NewGuid()));
        db.AiPersonas.Add(AiPersona.CreateDefault(tenantA, Guid.NewGuid()));
        // Tenant B has the full set already (mid-run install).
        db.AiPersonas.Add(AiPersona.CreateAnonymous(tenantB, Guid.NewGuid()));
        db.AiPersonas.Add(AiPersona.CreateDefault(tenantB, Guid.NewGuid()));
        db.AiPersonas.Add(AiPersona.CreateStudent(tenantB, Guid.NewGuid()));
        db.AiPersonas.Add(AiPersona.CreateTeacher(tenantB, Guid.NewGuid()));
        db.AiPersonas.Add(AiPersona.CreateParent(tenantB, Guid.NewGuid()));
        db.AiPersonas.Add(AiPersona.CreateEditor(tenantB, Guid.NewGuid()));
        db.AiPersonas.Add(AiPersona.CreateApprover(tenantB, Guid.NewGuid()));
        db.AiPersonas.Add(AiPersona.CreateClient(tenantB, Guid.NewGuid()));
        await db.SaveChangesAsync();

        await FlagshipPersonasBackfillSeed.SeedAsync(db, appDb, default);

        (await db.AiPersonas.CountAsync(p => p.TenantId == tenantA)).Should().Be(8);
        (await db.AiPersonas.CountAsync(p => p.TenantId == tenantB)).Should().Be(8);
    }

    [Fact]
    public async Task Is_idempotent_on_second_run()
    {
        var (db, appDb, tenantA, _) = await SetupAsync();
        db.AiPersonas.Add(AiPersona.CreateAnonymous(tenantA, Guid.NewGuid()));
        db.AiPersonas.Add(AiPersona.CreateDefault(tenantA, Guid.NewGuid()));
        await db.SaveChangesAsync();

        await FlagshipPersonasBackfillSeed.SeedAsync(db, appDb, default);
        await FlagshipPersonasBackfillSeed.SeedAsync(db, appDb, default);

        (await db.AiPersonas.CountAsync(p => p.TenantId == tenantA)).Should().Be(8);
    }

    private static async Task<(AiDbContext db, IApplicationDbContext appDb, Guid tenantA, Guid tenantB)>
        SetupAsync()
    {
        // Pattern mirrors AgentPermissionResolverTests.NewSetup at
        // boilerplateBE/tests/Starter.Api.Tests/Ai/Identity/AgentPermissionResolverTests.cs:16.
        var dbName = $"backfill-{Guid.NewGuid():N}";
        var cu = new Mock<ICurrentUserService>();
        cu.SetupGet(x => x.UserId).Returns(Guid.NewGuid());
        cu.SetupGet(x => x.TenantId).Returns((Guid?)null);

        var appOpts = new DbContextOptionsBuilder<Starter.Infrastructure.Persistence.ApplicationDbContext>()
            .UseInMemoryDatabase($"{dbName}-app").Options;
        var appDb = new Starter.Infrastructure.Persistence.ApplicationDbContext(appOpts, cu.Object);

        var aiOpts = new DbContextOptionsBuilder<AiDbContext>()
            .UseInMemoryDatabase($"{dbName}-ai")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        var aiDb = new AiDbContext(aiOpts, cu.Object);

        // Seed two tenants via the domain factory.
        var ta = Starter.Domain.Tenants.Entities.Tenant.Create("Acme", "acme");
        var tb = Starter.Domain.Tenants.Entities.Tenant.Create("Globex", "globex");
        appDb.Tenants.Add(ta);
        appDb.Tenants.Add(tb);
        await appDb.SaveChangesAsync();

        return (aiDb, appDb, ta.Id, tb.Id);
    }
}
```

> **Note:** This uses the real `ApplicationDbContext` with the EF InMemory provider — same pattern as `AgentPermissionResolverTests` line 16, and consistent with how the production seed reads `appDb.Tenants`. Avoids fragile `Moq<IApplicationDbContext>` work for `IQueryable` / `DbSet` shims.

- [ ] **Step 2: Run, expect failure**

Run: `dotnet test boilerplateBE/Starter.sln --filter "FullyQualifiedName~FlagshipPersonasBackfillSeedTests"`

Expected: BUILD FAILS (the seed class doesn't exist).

- [ ] **Step 3: Implement the seed**

Create `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Persistence/Seed/FlagshipPersonasBackfillSeed.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Starter.Application.Common.Interfaces;
using Starter.Module.AI.Domain.Entities;

namespace Starter.Module.AI.Infrastructure.Persistence.Seed;

/// <summary>
/// Plan 5e: idempotently adds the six flagship demo personas (student, teacher,
/// parent, editor, approver, client) to every tenant that doesn't yet have them.
/// New tenants get these via <c>SeedTenantPersonasDomainEventHandler</c>; this seed
/// covers tenants that pre-date 5e.
/// </summary>
internal static class FlagshipPersonasBackfillSeed
{
    private static readonly Guid SystemSeedActor = Guid.Empty;

    public static async Task SeedAsync(
        AiDbContext db,
        IApplicationDbContext appDb,
        CancellationToken ct = default)
    {
        var tenantIds = await appDb.Tenants
            .IgnoreQueryFilters()
            .Select(t => t.Id)
            .ToListAsync(ct);
        if (tenantIds.Count == 0) return;

        foreach (var tenantId in tenantIds)
        {
            var existing = await db.AiPersonas
                .IgnoreQueryFilters()
                .Where(p => p.TenantId == tenantId)
                .Select(p => p.Slug)
                .ToListAsync(ct);
            var have = existing.ToHashSet(StringComparer.Ordinal);

            if (!have.Contains(AiPersona.StudentSlug))   db.AiPersonas.Add(AiPersona.CreateStudent  (tenantId, SystemSeedActor));
            if (!have.Contains(AiPersona.TeacherSlug))   db.AiPersonas.Add(AiPersona.CreateTeacher  (tenantId, SystemSeedActor));
            if (!have.Contains(AiPersona.ParentSlug))    db.AiPersonas.Add(AiPersona.CreateParent   (tenantId, SystemSeedActor));
            if (!have.Contains(AiPersona.EditorSlug))    db.AiPersonas.Add(AiPersona.CreateEditor   (tenantId, SystemSeedActor));
            if (!have.Contains(AiPersona.ApproverSlug))  db.AiPersonas.Add(AiPersona.CreateApprover (tenantId, SystemSeedActor));
            if (!have.Contains(AiPersona.ClientSlug))    db.AiPersonas.Add(AiPersona.CreateClient   (tenantId, SystemSeedActor));

            if (db.ChangeTracker.HasChanges())
                await db.SaveChangesAsync(ct);
        }
    }
}
```

- [ ] **Step 4: Run tests, expect green**

Run: `dotnet test boilerplateBE/Starter.sln --filter "FullyQualifiedName~FlagshipPersonasBackfillSeedTests"`

Expected: both PASS.

- [ ] **Step 5: Commit**

```bash
git add \
  boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Persistence/Seed/FlagshipPersonasBackfillSeed.cs \
  boilerplateBE/tests/Starter.Api.Tests/Ai/Personas/FlagshipPersonasBackfillSeedTests.cs
git commit -m "feat(ai): 5e — FlagshipPersonasBackfillSeed for tenants pre-dating 5e"
```

---

## Task 6: Wire `FlagshipPersonasBackfillSeed` into `AIModule.SeedDataAsync`

**Why:** The seed must run on every startup so existing tenants get the new personas without a manual migration. It must run before any template-install loop or `teacher_tutor` / `brand_content` will fail persona-target validation.

**Files:**
- Modify: `boilerplateBE/src/modules/Starter.Module.AI/AIModule.cs:301-318` (the always-on block of `SeedDataAsync`)

- [ ] **Step 1: Insert the call**

In `AIModule.cs`, locate the always-on block of `SeedDataAsync` (lines 308–318). Insert the new seed call between `SafetyPresetProfileSeed.SeedAsync` (line 314) and `AgentPrincipalBackfill.RunAsync` (line 318):

```csharp
        // Always seed platform-default safety preset profiles (idempotent — skips if any platform row exists)
        await SafetyPresetProfileSeed.SeedAsync(aiDb, cancellationToken);

        // Plan 5e: backfill flagship demo personas for every existing tenant
        // (idempotent — only inserts missing personas per tenant).
        await FlagshipPersonasBackfillSeed.SeedAsync(aiDb, appDb, cancellationToken);

        // Plan 5d-1 backfill: pair any pre-existing assistant with an AiAgentPrincipal
        // (idempotent — only inserts for assistants without a paired principal).
        await AgentPrincipalBackfill.RunAsync(aiDb, cancellationToken);
```

(No new `using` needed — `Starter.Module.AI.Infrastructure.Persistence.Seed` is already imported on line 44.)

- [ ] **Step 2: Build green**

Run: `dotnet build boilerplateBE/Starter.sln`

Expected: 0 errors.

- [ ] **Step 3: Run the AI-module test suite**

Run: `dotnet test boilerplateBE/Starter.sln --filter "FullyQualifiedName~Ai"`

Expected: all green.

- [ ] **Step 4: Commit**

```bash
git add boilerplateBE/src/modules/Starter.Module.AI/AIModule.cs
git commit -m "feat(ai): 5e — wire FlagshipPersonasBackfillSeed into AIModule.SeedDataAsync"
```

---

## Task 7: Add `[AiTool]` attributes to three read-only queries

**Why:** Platform Insights' "users / audit log / billing usage" pitch needs `list_audit_logs`, `list_subscriptions`, `list_usage` in the catalog. Pure attribute additions on existing queries.

**Files:**
- Modify: `boilerplateBE/src/Starter.Application/Features/AuditLogs/Queries/GetAuditLogs/GetAuditLogsQuery.cs`
- Modify: `boilerplateBE/src/modules/Starter.Module.Billing/Application/Queries/GetAllSubscriptions/GetAllSubscriptionsQuery.cs`
- Modify: `boilerplateBE/src/modules/Starter.Module.Billing/Application/Queries/GetUsage/GetUsageQuery.cs`

- [ ] **Step 1: Decorate `GetAuditLogsQuery`**

Replace the contents of `GetAuditLogsQuery.cs` with:

```csharp
using System.ComponentModel;
using Starter.Abstractions.Capabilities;
using Starter.Abstractions.Paging;
using Starter.Application.Common.Models;
using Starter.Application.Features.AuditLogs.DTOs;
using Starter.Domain.Common.Enums;
using Starter.Shared.Constants;
using Starter.Shared.Results;
using MediatR;

namespace Starter.Application.Features.AuditLogs.Queries.GetAuditLogs;

[AiTool(
    Name = "list_audit_logs",
    Description = "List audit-log entries for the current tenant. Supports filtering by entity type, entity id, action, performing user, and date range. Read-only.",
    Category = "Audit",
    RequiredPermission = Permissions.System.ViewAuditLogs,
    IsReadOnly = true)]
public sealed record GetAuditLogsQuery(
    [Description("Filter by audit entity type, e.g. 'User', 'Role', 'AiAssistant'.")]
    AuditEntityType? EntityType = null,

    [Description("Filter by the id of the audited entity.")]
    Guid? EntityId = null,

    [Description("Filter by audit action, e.g. 'Create', 'Update', 'Delete'.")]
    AuditAction? Action = null,

    [Description("Filter by the user id that performed the action.")]
    Guid? PerformedBy = null,

    [Description("Earliest occurred-at timestamp (UTC) to include.")]
    DateTime? DateFrom = null,

    [Description("Latest occurred-at timestamp (UTC) to include.")]
    DateTime? DateTo = null) : PaginationQuery, IRequest<Result<PaginatedList<AuditLogDto>>>;
```

- [ ] **Step 2: Decorate `GetAllSubscriptionsQuery`**

Replace `GetAllSubscriptionsQuery.cs` with:

```csharp
using System.ComponentModel;
using MediatR;
using Starter.Abstractions.Capabilities;
using Starter.Abstractions.Paging;
using Starter.Application.Common.Models;
using Starter.Module.Billing.Application.DTOs;
using Starter.Module.Billing.Constants;
using Starter.Shared.Results;

namespace Starter.Module.Billing.Application.Queries.GetAllSubscriptions;

[AiTool(
    Name = "list_subscriptions",
    Description = "List active and past tenant subscriptions, including plan name, status, and renewal date. Read-only.",
    Category = "Billing",
    RequiredPermission = BillingPermissions.View,
    IsReadOnly = true)]
public sealed record GetAllSubscriptionsQuery(
    [Description("Page number, 1-indexed.")]
    int PageNumber = 1,

    [Description("Page size; max 100.")]
    int PageSize = 20,

    [Description("Optional free-text search across tenant or plan name.")]
    string? SearchTerm = null) : IRequest<Result<PaginatedList<SubscriptionSummaryDto>>>;
```

- [ ] **Step 3: Decorate `GetUsageQuery`**

Replace `GetUsageQuery.cs` with:

```csharp
using System.ComponentModel;
using MediatR;
using Starter.Abstractions.Capabilities;
using Starter.Module.Billing.Application.DTOs;
using Starter.Module.Billing.Constants;
using Starter.Shared.Results;

namespace Starter.Module.Billing.Application.Queries.GetUsage;

[AiTool(
    Name = "list_usage",
    Description = "Report the current tenant's usage records (requests, storage, AI tokens) for the current billing period. Read-only.",
    Category = "Billing",
    RequiredPermission = BillingPermissions.View,
    IsReadOnly = true)]
public sealed record GetUsageQuery(
    [Description("Optional tenant id; superadmin-only when set to a value other than the caller's tenant.")]
    Guid? TenantId = null) : IRequest<Result<UsageDto>>;
```

- [ ] **Step 4: Build green**

Run: `dotnet build boilerplateBE/Starter.sln`

Expected: 0 errors. (5c-1's `AddAiToolsFromAssembly` runs at startup and validates each `[AiTool]`. Build-time ensures the attribute and using-directives compile.)

- [ ] **Step 5: Run the [AiTool] discovery tests**

Run: `dotnet test boilerplateBE/Starter.sln --filter "FullyQualifiedName~AiToolDiscovery"`

Expected: all green. The discovery scanner now sees the three new tools alongside `list_users`, `list_products`, `list_conversations`.

- [ ] **Step 6: Commit**

```bash
git add \
  boilerplateBE/src/Starter.Application/Features/AuditLogs/Queries/GetAuditLogs/GetAuditLogsQuery.cs \
  boilerplateBE/src/modules/Starter.Module.Billing/Application/Queries/GetAllSubscriptions/GetAllSubscriptionsQuery.cs \
  boilerplateBE/src/modules/Starter.Module.Billing/Application/Queries/GetUsage/GetUsageQuery.cs
git commit -m "feat(ai): 5e — [AiTool] on GetAuditLogsQuery, GetAllSubscriptionsQuery, GetUsageQuery"
```

---

## Task 8: Platform Insights templates (× 2 providers) + shared prompt

**Why:** First two of the five new templates. Anthropic + OpenAI variants exercise both providers from day one.

**Files:**
- Create: `boilerplateBE/src/Starter.Application/Features/Ai/Templates/PlatformInsightsPrompts.cs`
- Create: `boilerplateBE/src/Starter.Application/Features/Ai/Templates/PlatformInsightsAnthropicTemplate.cs`
- Create: `boilerplateBE/src/Starter.Application/Features/Ai/Templates/PlatformInsightsOpenAiTemplate.cs`

- [ ] **Step 1: Create the shared prompt**

Create `PlatformInsightsPrompts.cs`:

```csharp
namespace Starter.Application.Features.Ai.Templates;

internal static class PlatformInsightsPrompts
{
    public const string Description =
        "Read-only Q&A over your tenant's data — users, audit log, subscriptions, " +
        "usage records, and AI conversations. Answers are grounded in tool calls; " +
        "this agent never mutates data.";

    public const string SystemPrompt =
        "You are Platform Insights, a read-only analytics assistant for the current tenant. " +
        "You have access to these tools: " +
        "list_users (users, roles, statuses), " +
        "list_audit_logs (admin-action history with entity, action, actor, time), " +
        "list_subscriptions (subscription plans and renewal status), " +
        "list_usage (current-period usage records — requests, storage, AI tokens), " +
        "list_conversations (AI assistant conversation history). " +
        "Always call a tool before answering questions about data; never fabricate. " +
        "If you cannot find what was asked, say so clearly. " +
        "If asked about a different tenant's data and you are not a superadmin, " +
        "politely refuse and explain that you only have visibility into the caller's tenant.";
}
```

- [ ] **Step 2: Create the Anthropic variant**

Create `PlatformInsightsAnthropicTemplate.cs`:

```csharp
using Starter.Abstractions.Ai;
using Starter.Abstractions.Capabilities;

namespace Starter.Application.Features.Ai.Templates;

public sealed class PlatformInsightsAnthropicTemplate : IAiAgentTemplate
{
    public string Slug => "platform_insights_anthropic";
    public string DisplayName => "Platform Insights (Anthropic)";
    public string Description => PlatformInsightsPrompts.Description;
    public string Category => "Platform";
    public string SystemPrompt => PlatformInsightsPrompts.SystemPrompt;
    public AiProviderType Provider => AiProviderType.Anthropic;
    public string Model => "claude-sonnet-4-20250514";
    public double Temperature => 0.3;
    public int MaxTokens => 2048;
    public AssistantExecutionMode ExecutionMode => AssistantExecutionMode.Chat;
    public IReadOnlyList<string> EnabledToolNames { get; } = new[]
    {
        "list_users", "list_audit_logs", "list_subscriptions", "list_usage", "list_conversations",
    };
    public IReadOnlyList<string> PersonaTargetSlugs { get; } = new[] { "default" };
    public SafetyPreset? SafetyPresetOverride => null;
}
```

- [ ] **Step 3: Create the OpenAI variant**

Create `PlatformInsightsOpenAiTemplate.cs`:

```csharp
using Starter.Abstractions.Ai;
using Starter.Abstractions.Capabilities;

namespace Starter.Application.Features.Ai.Templates;

public sealed class PlatformInsightsOpenAiTemplate : IAiAgentTemplate
{
    public string Slug => "platform_insights_openai";
    public string DisplayName => "Platform Insights (OpenAI)";
    public string Description => PlatformInsightsPrompts.Description;
    public string Category => "Platform";
    public string SystemPrompt => PlatformInsightsPrompts.SystemPrompt;
    public AiProviderType Provider => AiProviderType.OpenAI;
    public string Model => "gpt-4o-mini";
    public double Temperature => 0.3;
    public int MaxTokens => 2048;
    public AssistantExecutionMode ExecutionMode => AssistantExecutionMode.Chat;
    public IReadOnlyList<string> EnabledToolNames { get; } = new[]
    {
        "list_users", "list_audit_logs", "list_subscriptions", "list_usage", "list_conversations",
    };
    public IReadOnlyList<string> PersonaTargetSlugs { get; } = new[] { "default" };
    public SafetyPreset? SafetyPresetOverride => null;
}
```

- [ ] **Step 4: Build + run discovery tests**

Run: `dotnet test boilerplateBE/Starter.sln --filter "FullyQualifiedName~AiAgentTemplateDiscovery"`

Expected: all green. The discovery scan now picks up the two new templates.

- [ ] **Step 5: Commit**

```bash
git add boilerplateBE/src/Starter.Application/Features/Ai/Templates/PlatformInsights*
git commit -m "feat(ai): 5e — Platform Insights templates (Anthropic + OpenAI variants)"
```

---

## Task 9: Support Copilot template + prompt

**Why:** Third of five templates — pure-prose helper for boilerplate features.

**Files:**
- Create: `boilerplateBE/src/Starter.Application/Features/Ai/Templates/SupportCopilotPrompts.cs`
- Create: `boilerplateBE/src/Starter.Application/Features/Ai/Templates/SupportCopilotTemplate.cs`

- [ ] **Step 1: Create the prompt**

Create `SupportCopilotPrompts.cs`:

```csharp
namespace Starter.Application.Features.Ai.Templates;

internal static class SupportCopilotPrompts
{
    public const string Description =
        "Answers 'how do I configure X' questions about the boilerplate's own features — " +
        "auth, RBAC, tenancy, billing, webhooks, audit logs, AI module, settings.";

    public const string SystemPrompt =
        "You are Support Copilot, a feature-help assistant for this boilerplate platform. " +
        "Answer admin questions about authentication, RBAC, tenancy, billing, webhooks, " +
        "audit logs, AI agents, and platform settings. " +
        "Always describe the actual UI page or CLI command the admin should use; " +
        "never invent endpoints or buttons that don't exist. " +
        "If a question is outside the platform's feature surface, say so plainly. " +
        "Prefer short, actionable answers over long essays.";
}
```

- [ ] **Step 2: Create the template class**

Create `SupportCopilotTemplate.cs`:

```csharp
using Starter.Abstractions.Ai;
using Starter.Abstractions.Capabilities;

namespace Starter.Application.Features.Ai.Templates;

public sealed class SupportCopilotTemplate : IAiAgentTemplate
{
    public string Slug => "support_copilot";
    public string DisplayName => "Support Copilot";
    public string Description => SupportCopilotPrompts.Description;
    public string Category => "Platform";
    public string SystemPrompt => SupportCopilotPrompts.SystemPrompt;
    public AiProviderType Provider => AiProviderType.Anthropic;
    public string Model => "claude-sonnet-4-20250514";
    public double Temperature => 0.3;
    public int MaxTokens => 2048;
    public AssistantExecutionMode ExecutionMode => AssistantExecutionMode.Chat;
    public IReadOnlyList<string> EnabledToolNames { get; } = Array.Empty<string>();
    public IReadOnlyList<string> PersonaTargetSlugs { get; } = new[] { "default" };
    public SafetyPreset? SafetyPresetOverride => null;
}
```

- [ ] **Step 3: Build green**

Run: `dotnet build boilerplateBE/Starter.sln`

Expected: 0 errors.

- [ ] **Step 4: Commit**

```bash
git add boilerplateBE/src/Starter.Application/Features/Ai/Templates/SupportCopilot*
git commit -m "feat(ai): 5e — Support Copilot template (boilerplate-feature Q&A)"
```

---

## Task 10: Teacher Tutor template + prompt

**Why:** First flagship template (School SaaS). Targets `student` persona; ChildSafe explicit override per the 5d-2 forward-link commitment.

**Files:**
- Create: `boilerplateBE/src/Starter.Application/Features/Ai/Templates/TeacherTutorPrompts.cs`
- Create: `boilerplateBE/src/Starter.Application/Features/Ai/Templates/TeacherTutorTemplate.cs`

- [ ] **Step 1: Create the prompt**

Create `TeacherTutorPrompts.cs`:

```csharp
namespace Starter.Application.Features.Ai.Templates;

internal static class TeacherTutorPrompts
{
    public const string Description =
        "Socratic tutor for school-age learners. Adapts to grade level and subject; " +
        "guides students step by step rather than giving finished answers.";

    public const string SystemPrompt =
        "You are a patient Socratic tutor for a school-age student. " +
        "Your job is to guide the student to the answer, not to hand it to them. " +
        "Follow these rules without exception: " +
        "1) Ask one focused question at a time and wait for the student's reply before continuing. " +
        "2) Keep your language age-appropriate, warm, and encouraging. " +
        "3) Never produce a finished homework answer — break problems into smaller steps the student works on themselves. " +
        "4) When the student struggles, give a small hint, not the solution. " +
        "5) Confirm understanding before moving on. " +
        "6) If a question is outside school subjects (math, science, language, history, geography, basic study skills), " +
        "politely steer the conversation back to learning. " +
        "Begin by asking the student what subject and topic they want to work on today.";
}
```

- [ ] **Step 2: Create the template class**

Create `TeacherTutorTemplate.cs`:

```csharp
using Starter.Abstractions.Ai;
using Starter.Abstractions.Capabilities;

namespace Starter.Application.Features.Ai.Templates;

public sealed class TeacherTutorTemplate : IAiAgentTemplate
{
    public string Slug => "teacher_tutor";
    public string DisplayName => "Teacher Tutor";
    public string Description => TeacherTutorPrompts.Description;
    public string Category => "Education";
    public string SystemPrompt => TeacherTutorPrompts.SystemPrompt;
    public AiProviderType Provider => AiProviderType.Anthropic;
    public string Model => "claude-sonnet-4-20250514";
    public double Temperature => 0.5;
    public int MaxTokens => 2048;
    public AssistantExecutionMode ExecutionMode => AssistantExecutionMode.Chat;
    public IReadOnlyList<string> EnabledToolNames { get; } = Array.Empty<string>();
    public IReadOnlyList<string> PersonaTargetSlugs { get; } = new[] { "student" };
    public SafetyPreset? SafetyPresetOverride => SafetyPreset.ChildSafe;
}
```

- [ ] **Step 3: Build green**

Run: `dotnet build boilerplateBE/Starter.sln`

Expected: 0 errors.

- [ ] **Step 4: Commit**

```bash
git add boilerplateBE/src/Starter.Application/Features/Ai/Templates/TeacherTutor*
git commit -m "feat(ai): 5e — Teacher Tutor template (Student persona, explicit ChildSafe)"
```

---

## Task 11: Brand Content template + prompt

**Why:** Second flagship template (Social media SaaS). Targets `editor` persona; explicit `Standard` safety override per the 5d-2 forward-link commitment. Higher temperature for creative copy.

**Files:**
- Create: `boilerplateBE/src/Starter.Application/Features/Ai/Templates/BrandContentPrompts.cs`
- Create: `boilerplateBE/src/Starter.Application/Features/Ai/Templates/BrandContentTemplate.cs`

- [ ] **Step 1: Create the prompt**

Create `BrandContentPrompts.cs`:

```csharp
namespace Starter.Application.Features.Ai.Templates;

internal static class BrandContentPrompts
{
    public const string Description =
        "Brand-voice copywriter for social media editors. Drafts captions, posts, and " +
        "campaign copy; adapts to the editor's stated brand voice and audience.";

    public const string SystemPrompt =
        "You are a creative copywriter helping a social-media editor draft brand content. " +
        "Before producing any copy, ask the editor for: " +
        "(a) the brand's voice (e.g. playful, authoritative, clinical), " +
        "(b) the target audience, " +
        "(c) the format (caption, thread, long-form post, ad), " +
        "(d) any product or claim facts that must appear verbatim. " +
        "Once you have those four inputs, draft three short variations the editor can choose between. " +
        "Never invent product features, prices, or claims — only use what the editor provided. " +
        "Stay on-brand once the editor has chosen a voice; flag anything off-tone you might be tempted to write. " +
        "Keep drafts concise; the editor is the final author.";
}
```

- [ ] **Step 2: Create the template class**

Create `BrandContentTemplate.cs`:

```csharp
using Starter.Abstractions.Ai;
using Starter.Abstractions.Capabilities;

namespace Starter.Application.Features.Ai.Templates;

public sealed class BrandContentTemplate : IAiAgentTemplate
{
    public string Slug => "brand_content";
    public string DisplayName => "Brand Content Agent";
    public string Description => BrandContentPrompts.Description;
    public string Category => "Content";
    public string SystemPrompt => BrandContentPrompts.SystemPrompt;
    public AiProviderType Provider => AiProviderType.Anthropic;
    public string Model => "claude-sonnet-4-20250514";
    public double Temperature => 0.8;
    public int MaxTokens => 2048;
    public AssistantExecutionMode ExecutionMode => AssistantExecutionMode.Chat;
    public IReadOnlyList<string> EnabledToolNames { get; } = Array.Empty<string>();
    public IReadOnlyList<string> PersonaTargetSlugs { get; } = new[] { "editor" };
    public SafetyPreset? SafetyPresetOverride => SafetyPreset.Standard;
}
```

- [ ] **Step 3: Build green**

Run: `dotnet build boilerplateBE/Starter.sln`

Expected: 0 errors.

- [ ] **Step 4: Commit**

```bash
git add boilerplateBE/src/Starter.Application/Features/Ai/Templates/BrandContent*
git commit -m "feat(ai): 5e — Brand Content template (Editor persona, explicit Standard)"
```

---

## Task 12: Plan 5e acid tests

**Why:** End-to-end validation of every locked decision. These tests guard the future against regressions to the rename, the install wiring, the persona seed, and the template registry contents.

**Files:**
- Create: `boilerplateBE/tests/Starter.Api.Tests/Ai/AcidTests/Plan5eAcidTests.cs`

- [ ] **Step 1: Write the acid tests**

Create `Plan5eAcidTests.cs`:

```csharp
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Moq;
using Starter.Abstractions.Ai;
using Starter.Abstractions.Capabilities;
using Starter.Application.Common.Interfaces;
using Starter.Application.Features.Ai.Templates;
using Starter.Module.AI.Application.Commands.InstallTemplate;
using Starter.Module.AI.Application.Services;
using Starter.Module.AI.Domain.Entities;
using Starter.Module.AI.Infrastructure.Persistence;
using Starter.Shared.Constants;
using Xunit;

namespace Starter.Api.Tests.Ai.AcidTests;

public class Plan5eAcidTests
{
    [Fact]
    public void All_five_5e_templates_have_parameterless_ctors_and_unique_slugs()
    {
        // Each template must be instantiable by the discovery scan.
        var templates = new IAiAgentTemplate[]
        {
            new PlatformInsightsAnthropicTemplate(),
            new PlatformInsightsOpenAiTemplate(),
            new SupportCopilotTemplate(),
            new TeacherTutorTemplate(),
            new BrandContentTemplate(),
        };

        templates.Select(t => t.Slug).Should().OnlyHaveUniqueItems();
        templates.Should().AllSatisfy(t =>
        {
            t.SystemPrompt.Should().NotBeNullOrWhiteSpace();
            t.DisplayName.Should().NotBeNullOrWhiteSpace();
            t.Model.Should().NotBeNullOrWhiteSpace();
        });
    }

    [Fact]
    public void Teacher_tutor_template_targets_student_with_explicit_childsafe()
    {
        var t = new TeacherTutorTemplate();
        t.Slug.Should().Be("teacher_tutor");
        t.PersonaTargetSlugs.Should().ContainSingle().Which.Should().Be("student");
        t.SafetyPresetOverride.Should().Be(SafetyPreset.ChildSafe);
    }

    [Fact]
    public void Brand_content_template_targets_editor_with_explicit_standard()
    {
        var t = new BrandContentTemplate();
        t.Slug.Should().Be("brand_content");
        t.PersonaTargetSlugs.Should().ContainSingle().Which.Should().Be("editor");
        t.SafetyPresetOverride.Should().Be(SafetyPreset.Standard);
    }

    [Fact]
    public void Platform_insights_templates_inherit_from_default_persona()
    {
        new PlatformInsightsAnthropicTemplate().SafetyPresetOverride.Should().BeNull();
        new PlatformInsightsOpenAiTemplate().SafetyPresetOverride.Should().BeNull();
        new SupportCopilotTemplate().SafetyPresetOverride.Should().BeNull();
    }

    [Fact]
    public void Existing_5c2_templates_now_inherit_from_persona()
    {
        new SupportAssistantAnthropicTemplate().SafetyPresetOverride.Should().BeNull();
        new SupportAssistantOpenAiTemplate().SafetyPresetOverride.Should().BeNull();
        new Starter.Module.Products.Application.Templates.ProductExpertAnthropicTemplate()
            .SafetyPresetOverride.Should().BeNull();
        new Starter.Module.Products.Application.Templates.ProductExpertOpenAiTemplate()
            .SafetyPresetOverride.Should().BeNull();
    }

    [Fact]
    public async Task Installing_teacher_tutor_persists_childsafe_override()
    {
        var (handler, db, _) = SetupHandler(
            templates: new IAiAgentTemplate[] { new TeacherTutorTemplate() },
            seedFlagshipPersonas: true);

        var result = await handler.Handle(
            new InstallTemplateCommand("teacher_tutor"), default);

        result.IsSuccess.Should().BeTrue($"install failed: {result.Error?.Description}");
        var assistant = await db.AiAssistants.SingleAsync();
        assistant.SafetyPresetOverride.Should().Be(SafetyPreset.ChildSafe);
        assistant.PersonaTargetSlugs.Should().ContainSingle().Which.Should().Be("student");
    }

    [Fact]
    public async Task Installing_brand_content_persists_standard_override()
    {
        var (handler, db, _) = SetupHandler(
            templates: new IAiAgentTemplate[] { new BrandContentTemplate() },
            seedFlagshipPersonas: true);

        var result = await handler.Handle(
            new InstallTemplateCommand("brand_content"), default);

        result.IsSuccess.Should().BeTrue($"install failed: {result.Error?.Description}");
        var assistant = await db.AiAssistants.SingleAsync();
        assistant.SafetyPresetOverride.Should().Be(SafetyPreset.Standard);
        assistant.PersonaTargetSlugs.Should().ContainSingle().Which.Should().Be("editor");
    }

    [Fact]
    public async Task Installing_platform_insights_anthropic_leaves_override_null_and_enables_five_tools()
    {
        var (handler, db, _) = SetupHandler(
            templates: new IAiAgentTemplate[] { new PlatformInsightsAnthropicTemplate() },
            tools: new[] { "list_users", "list_audit_logs", "list_subscriptions", "list_usage", "list_conversations" });

        var result = await handler.Handle(
            new InstallTemplateCommand("platform_insights_anthropic"), default);

        result.IsSuccess.Should().BeTrue($"install failed: {result.Error?.Description}");
        var assistant = await db.AiAssistants.SingleAsync();
        assistant.SafetyPresetOverride.Should().BeNull();
        assistant.EnabledToolNames.Should().BeEquivalentTo(new[]
        {
            "list_users", "list_audit_logs", "list_subscriptions", "list_usage", "list_conversations",
        });
    }

    private static (
        InstallTemplateCommandHandler handler,
        AiDbContext db,
        Guid tenantId)
        SetupHandler(
            IAiAgentTemplate[] templates,
            IEnumerable<string>? tools = null,
            bool seedFlagshipPersonas = false)
    {
        var tenantId = Guid.NewGuid();
        var cu = new Mock<ICurrentUserService>();
        cu.SetupGet(x => x.UserId).Returns(Guid.NewGuid());
        cu.SetupGet(x => x.TenantId).Returns(tenantId);
        cu.Setup(x => x.IsInRole(Roles.SuperAdmin)).Returns(false);

        var dbOpts = new DbContextOptionsBuilder<AiDbContext>()
            .UseInMemoryDatabase($"5e-{Guid.NewGuid()}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        var db = new AiDbContext(dbOpts, cu.Object);
        db.AiPersonas.Add(AiPersona.CreateDefault(tenantId, Guid.NewGuid()));
        if (seedFlagshipPersonas)
        {
            db.AiPersonas.Add(AiPersona.CreateStudent(tenantId, Guid.NewGuid()));
            db.AiPersonas.Add(AiPersona.CreateEditor(tenantId, Guid.NewGuid()));
        }
        db.SaveChanges();

        var registry = new AiAgentTemplateRegistry(templates);

        var toolReg = new Mock<IAiToolRegistry>();
        var toolSlugs = new HashSet<string>(tools ?? Array.Empty<string>(), StringComparer.Ordinal);
        toolReg.Setup(x => x.FindByName(It.IsAny<string>()))
            .Returns<string>(name =>
            {
                if (!toolSlugs.Contains(name)) return null;
                var def = new Mock<IAiToolDefinition>();
                def.SetupGet(x => x.Name).Returns(name);
                return def.Object;
            });

        var ff = new Mock<IFeatureFlagService>();
        ff.Setup(x => x.GetValueAsync<int>("ai.agents.max_count", It.IsAny<CancellationToken>()))
            .ReturnsAsync(int.MaxValue);

        var handler = new InstallTemplateCommandHandler(
            db, registry, toolReg.Object, cu.Object, ff.Object);
        return (handler, db, tenantId);
    }
}
```

- [ ] **Step 2: Run the acid tests**

Run: `dotnet test boilerplateBE/Starter.sln --filter "FullyQualifiedName~Plan5eAcidTests"`

Expected: all 8 PASS.

- [ ] **Step 3: Run the full AI test suite for any regression**

Run: `dotnet test boilerplateBE/Starter.sln --filter "FullyQualifiedName~Ai"`

Expected: all green.

- [ ] **Step 4: Commit**

```bash
git add boilerplateBE/tests/Starter.Api.Tests/Ai/AcidTests/Plan5eAcidTests.cs
git commit -m "test(ai): 5e — acid tests for bundled platform agents"
```

---

## Task 13: Append "Superseded by 5e" pointer to 5c-2 design doc

**Why:** Future readers landing on the 5c-2 design need a forward link to 5e for the rename + wiring decision. Match the same style as the 5d-2 spec's forward links.

**Files:**
- Modify: `docs/superpowers/specs/2026-04-25-ai-plan-5c-2-agent-templates-design.md` (locate §3.1 — the `IAiAgentTemplate` interface section)

- [ ] **Step 1: Append the pointer**

In the 5c-2 design doc's §3.1 (`IAiAgentTemplate` interface section), immediately after the C# code block defining the interface, insert:

```markdown
> **Update (Plan 5e, 2026-04-28):** `SafetyPresetHint` was renamed to `SafetyPresetOverride` and made load-bearing in 5e. `InstallTemplateCommandHandler` now stamps the value onto `AiAssistant.SafetyPresetOverride` (added in 5d-2). The four 5c-2 demo templates were updated to return `null` (inherit from persona) since their original `Standard` value matched the persona-default and added no information. See [Plan 5e design](./2026-04-28-ai-plan-5e-bundled-platform-agents-design.md).
```

- [ ] **Step 2: Commit**

```bash
git add docs/superpowers/specs/2026-04-25-ai-plan-5c-2-agent-templates-design.md
git commit -m "docs(ai): 5c-2 — superseded-by-5e pointer on IAiAgentTemplate section"
```

---

## Task 14: Final verification

- [ ] **Step 1: Build the whole solution**

Run: `dotnet build boilerplateBE/Starter.sln`

Expected: 0 errors, 0 warnings.

- [ ] **Step 2: Run every AI test**

Run: `dotnet test boilerplateBE/Starter.sln --filter "FullyQualifiedName~Ai"`

Expected: all green. Watch in particular for the existing 5d-2 acid tests — the safety override wiring must not have broken any of them.

- [ ] **Step 3: Run the full test suite**

Run: `dotnet test boilerplateBE/Starter.sln`

Expected: all green. The `[AiTool]` additions on `GetAuditLogsQuery` etc. could in theory break unrelated tests if the tools registry has a max-count guard or a category-uniqueness rule — investigate any failure here before declaring the plan complete.

- [ ] **Step 4: Smoke check the seed loop manually (optional but recommended)**

Reload the dev environment. With `AI:InstallDemoTemplatesOnStartup = true` (the dev default), run the API and inspect:

```bash
psql -h localhost -U postgres -d starter -c "SELECT slug, safety_preset_override FROM ai.ai_assistants ORDER BY slug;"
```

Expected output: nine rows (`brand_content`, `platform_insights_anthropic`, `platform_insights_openai`, `product_expert_anthropic`, `product_expert_openai`, `support_assistant_anthropic`, `support_assistant_openai`, `support_copilot`, `teacher_tutor`). `safety_preset_override` is `ChildSafe` for `teacher_tutor`, `Standard` for `brand_content`, and `NULL` for the other seven.

```bash
psql -h localhost -U postgres -d starter -c "SELECT tenant_id, slug FROM ai.ai_personas ORDER BY tenant_id, slug;"
```

Expected: every tenant has all eight persona slugs.

If both checks pass, the plan is verified end-to-end. (The dev DB schema name may differ — adjust the query if the personas table is unschemed or in a `public` schema.)

- [ ] **Step 5: Open the PR**

Push the branch and open a PR against `main`:

```bash
git push -u origin feature/ai-phase-5e
gh pr create --title "feat(ai): plan 5e — bundled platform agents" --body "$(cat <<'EOF'
## Summary
- Five new agent templates: Platform Insights (Anthropic + OpenAI variants), Support Copilot, Teacher Tutor (ChildSafe, Student persona), Brand Content (Editor persona).
- `IAiAgentTemplate.SafetyPresetHint` renamed to `SafetyPresetOverride` and wired through `InstallTemplateCommandHandler` onto `AiAssistant.SafetyPresetOverride` (closes the 5d-2 forward-link commitment).
- Six new flagship demo personas (`student`, `teacher`, `parent`, `editor`, `approver`, `client`) seeded per tenant; idempotent backfill for existing tenants.
- `[AiTool]` decorations on `GetAuditLogsQuery`, `GetAllSubscriptionsQuery`, `GetUsageQuery` complete the Platform Insights tool surface.

## Test plan
- [ ] `dotnet test boilerplateBE/Starter.sln` — all green
- [ ] Acid tests in `Plan5eAcidTests.cs` cover the eight required scenarios from the design §9.1
- [ ] Smoke-check the seed: nine assistants per tenant, eight personas per tenant
EOF
)"
```

---

## Self-review

After completing all tasks, verify spec coverage:

- [x] §3 five templates → Tasks 8, 9, 10, 11
- [x] §4 prompt files → bundled into Tasks 8, 9, 10, 11
- [x] §5 six personas + factories + slug constants → Task 3
- [x] §5.1 new-tenant seed extension → Task 4
- [x] §5.2 backfill seed → Tasks 5, 6
- [x] §6 interface rename + install handler wiring → Tasks 1, 2
- [x] §6.3 existing 5c-2 templates updated → Task 1 (Step 8)
- [x] §6.4 5c-2 design doc pointer → Task 13
- [x] §7 three `[AiTool]` decorations → Task 7
- [x] §8 single install flag (existing) — no work; verified via Task 14 Step 4
- [x] §9.1 acid tests (8 scenarios) → Task 12 (8 tests)
- [x] §9.2 unit tests (5 listed) → Tasks 2 (handler), 3 (factories), 4 (seed handler), 5 (backfill); discovery covered by existing tests in Task 7's verification
- [x] §9.3 existing tests updated → Task 1 (Steps 6, 10)

No gaps.
