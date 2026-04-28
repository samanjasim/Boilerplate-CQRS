# AI Module — Plan 5c-2: Agent Templates (Design)

**Date:** 2026-04-25
**Status:** Design approved; plan pending
**Parent plan family:** Plan 5c — Agent Templates (see revised vision `2026-04-23-ai-module-vision-revised-design.md`)
**Predecessor:** Plan 5c-1 — `[AiTool]` Attribute + Auto-Discovery (PR #19, merged 2026-04-25)
**Successor:** Plan 5d — Agent-level safety presets, cost caps, dangerous-action pause

---

## 1. Purpose

Introduce **agent templates** — module-authored bundles of `(prompt + tools + persona targets + safety preset hint + provider/model config)` that admins install into a tenant to create a ready-to-use `AiAssistant`. Today the only way to spin up an assistant is to fill in every field through `CreateAssistantCommand`. Templates collapse that into a one-call install, with the bundle authored once in code by the team that owns the relevant tools/personas.

This plan ships:

- The `IAiAgentTemplate` interface + `AddAiAgentTemplatesFromAssembly` per-module DI scan (parallel to 5c-1's tool primitive)
- An in-memory `IAiAgentTemplateRegistry` with fail-fast duplicate-slug detection
- `InstallTemplateCommand` + handler + validator + error constants
- Two nullable provenance columns on `AiAssistant` (`TemplateSourceSlug`, `TemplateSourceVersion`) and a `StampTemplateSource` method
- `AiTemplatesController` exposing the catalog and install endpoints
- Four boilerplate demo templates — `support_assistant_anthropic`, `support_assistant_openai` (Core), `product_expert_anthropic`, `product_expert_openai` (Products module)
- Migration of the existing `AI:SeedSampleAssistant` flag to a template-driven `AI:InstallDemoTemplatesOnStartup` startup install

### Out of scope

All deferred items are tracked in `docs/modules/ai/backlog.md` so future plans can pick them up explicitly. Headline deferrals:

- **KB bundling** — templates carry no `KnowledgeBaseDocIds`; KB attachment remains a post-install manual step.
- **Auto-install on tenant creation** — every install is admin-initiated; no `IAiAgentTemplate.AutoInstallOnTenantCreate` flag.
- **Inline overrides at install time** — `InstallTemplateCommand` copies template values verbatim; customisation goes through the existing `UpdateAssistantCommand`.
- **Multiple installs of the same template into the same tenant** — second install fails with `AlreadyInstalled`.
- **Template versioning / "update available" indicator** — `TemplateSourceVersion` is reserved-nullable and never populated in 5c-2.
- **Reset-to-template-defaults** — no endpoint.
- **Uninstall endpoint** — use existing `DeleteAssistantCommand`.
- **Assistant-level `SafetyPreset`** — `SafetyPresetHint` is a doc-level recommendation; enforcement lands in Plan 5d when safety hoists onto `AiAssistant`.
- **Flagship acid tests** (School Tutor / Social Brand Content) — those flagship modules don't exist yet; 5c-2 proves the abstraction with boilerplate demo templates and the flagship acid tests re-run when their plans land.
- **Frontend admin UI** — backend + API surface only; React templates browser is part of Plan 7a.

---

## 2. Context (current state as of 2026-04-25)

Verified against the current tree on `feature/ai-phase-5c-2`:

- **5c-1 substrate available.** `[AiTool]`, `[AiParameterIgnore]`, `IAiToolDefinitionModuleSource`, `AddAiToolsFromAssembly`, and `AiToolDto.Module` are all merged on `main`. The naming, fail-fast semantics, scanner internals, and `DeriveModuleSource` helper are the patterns 5c-2 mirrors directly.
- **`AiAssistant` already carries every template-relevant field.** `Slug`, `SystemPrompt`, `Provider`, `Model`, `Temperature`, `MaxTokens`, `ExecutionMode`, `MaxAgentSteps`, `EnabledToolNames`, `KnowledgeBaseDocIds`, `PersonaTargetSlugs`, `RagScope`, `Visibility`, `AccessMode`, `IsActive`. Templates only need to reference what's already there.
- **`AiPersona` carries `SafetyPreset`.** Safety lives on persona today (5b). Hoisting to assistant level is Plan 5d. For 5c-2, `SafetyPresetHint` on the template is informational.
- **Sample-assistant seed exists today.** `AIModule.SeedDataAsync` reads `AI:SeedSampleAssistant`; if true, it creates one hardcoded `"AI Tools Demo"` assistant. The 5c-2 install flow replaces this — same flag's intent, new mechanism.
- **Tenant-persona seed pattern exists.** `SeedTenantPersonasDomainEventHandler` auto-creates `anonymous` and `default` personas on `TenantCreatedEvent` using `SystemSeedActor = Guid.Empty`. The 5c-2 startup-install handler reuses the same `Guid.Empty` actor when seeding via the config flag.
- **Permissions.** `Ai.ManageAssistants` is the existing permission to create/edit assistants. 5c-2 reuses it for both list-templates and install-template — no new permission constants.
- **No template entity, no template registry today.** Greenfield.

---

## 3. Public contract

### 3.1 `IAiAgentTemplate` interface

New file: `boilerplateBE/src/Starter.Abstractions/Capabilities/IAiAgentTemplate.cs`.

```csharp
namespace Starter.Abstractions.Capabilities;

/// <summary>
/// A module-authored agent preset. Implementations describe an assistant's
/// system prompt, model parameters, capability bindings, and audience targeting.
/// Discovered by <see cref="AiAgentTemplateDiscoveryExtensions.AddAiAgentTemplatesFromAssembly"/>
/// and installed via <c>InstallTemplateCommand</c> to materialise a tenant-scoped
/// <c>AiAssistant</c>.
///
/// Implementations MUST have a parameterless public constructor — templates are
/// pure data, not DI consumers. Use <c>const string</c> fields for shared
/// content (system prompts) when multiple variants share the same prose.
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

> **Update (Plan 5e, 2026-04-28):** `SafetyPresetHint` was renamed to `SafetyPresetOverride` and made load-bearing in 5e. `InstallTemplateCommandHandler` now stamps the value onto `AiAssistant.SafetyPresetOverride` (added in 5d-2). The four 5c-2 demo templates were updated to return `null` (inherit from persona) since their original `Standard` value matched the persona-default and added no information. See [Plan 5e design](./2026-04-28-ai-plan-5e-bundled-platform-agents-design.md).

### 3.2 `IAiAgentTemplateModuleSource` capability

New file: `boilerplateBE/src/Starter.Abstractions/Capabilities/IAiAgentTemplateModuleSource.cs`.

```csharp
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

The scanner-internal `AiAgentTemplateRegistration` decorator (`internal sealed`, lives in `Starter.Abstractions.Capabilities`) implements both `IAiAgentTemplate` (delegating to the wrapped instance) and `IAiAgentTemplateModuleSource`.

### 3.3 `AddAiAgentTemplatesFromAssembly` DI extension

New file: `boilerplateBE/src/Starter.Abstractions/Capabilities/AiAgentTemplateDiscoveryExtensions.cs`.

```csharp
public static IServiceCollection AddAiAgentTemplatesFromAssembly(
    this IServiceCollection services, Assembly assembly);
```

Scans `assembly` for non-abstract, non-generic concrete classes implementing `IAiAgentTemplate` with a public parameterless constructor. Each match is instantiated, validated by `ValidateShape`, decorated with `AiAgentTemplateRegistration`, and registered as `AddSingleton<IAiAgentTemplate>`.

`DeriveModuleSource(Assembly)` — reuses the helper from 5c-1 (or extracts the shared logic into a private static if both helpers grow): `Starter.Application` → `"Core"`, `Starter.Module.Products` → `"Products"`, `Starter.Module.AI` → `"AI"`.

`ValidateShape(IAiAgentTemplate)` — composition-time guard:

- `Slug` non-empty, ≤ 128 chars
- `SystemPrompt` non-empty
- `DisplayName` / `Description` / `Category` / `Model` non-empty
- `Temperature` between 0.0 and 2.0
- `MaxTokens` ≥ 1
- `EnabledToolNames` not null (may be empty)
- `PersonaTargetSlugs` not null (may be empty)
- Throws `InvalidOperationException` on any failure with a message naming the offending CLR type

### 3.4 Registry

New files in `boilerplateBE/src/modules/Starter.Module.AI/Application/Services/`:

```csharp
public interface IAiAgentTemplateRegistry
{
    IReadOnlyCollection<IAiAgentTemplate> GetAll();
    IAiAgentTemplate? Find(string slug);
}

internal sealed class AiAgentTemplateRegistry : IAiAgentTemplateRegistry
{
    public AiAgentTemplateRegistry(IEnumerable<IAiAgentTemplate> templates);
    // Throws InvalidOperationException on duplicate slug — message names both
    // colliding CLR types and the slug. Mirrors AiToolRegistryService's BuildDictionary.
}
```

`GetAll` returns templates ordered deterministically by `(Category, Slug)`. Registered as `Singleton`. No DB sync, no hosted service — templates are purely code-resident.

DI wire-up in `AIModule.ConfigureServices`:

```csharp
services.AddAiToolsFromAssembly(typeof(AIModule).Assembly);
services.AddAiAgentTemplatesFromAssembly(typeof(AIModule).Assembly);   // new
services.AddSingleton<IAiAgentTemplateRegistry, AiAgentTemplateRegistry>();
```

`Starter.Application.DependencyInjection` and `ProductsModule.ConfigureServices` each add their own `AddAiAgentTemplatesFromAssembly(typeof(X).Assembly)` line next to the existing tools-scan line.

### 3.5 `InstallTemplateCommand`

New folder: `boilerplateBE/src/modules/Starter.Module.AI/Application/Commands/InstallTemplate/`.

```csharp
public sealed record InstallTemplateCommand(
    string TemplateSlug,
    Guid? TargetTenantId = null,
    Guid? CreatedByUserIdOverride = null)        // seed path passes Guid.Empty
    : IRequest<Result<Guid>>;                     // returns new AiAssistant.Id
```

**Validator** (`InstallTemplateCommandValidator`):

- `TemplateSlug` non-empty, ≤ 128 chars

**Handler logic** (in order):

1. Resolve target tenant: if `TargetTenantId` is set and differs from `CurrentUser.TenantId`, assert `CurrentUser.IsSuperAdmin`; else use `CurrentUser.TenantId`.
2. Resolve template via `_templates.Find(command.TemplateSlug)`. Null → `Result.Failure(TemplateErrors.NotFound)`.
3. Duplicate guard: query `AiAssistant` for `(TenantId == target, Slug == template.Slug)`. Match → `Result.Failure(TemplateErrors.AlreadyInstalled)`.
4. Persona validation: each slug in `template.PersonaTargetSlugs` must resolve in `AiPersona` for the target tenant *or* be one of the system-reserved slugs (`anonymous`, `default`). Missing → `Result.Failure(TemplateErrors.PersonaTargetMissing)`.
5. Tool validation: each slug in `template.EnabledToolNames` must exist in `IAiToolRegistry.GetAll()`. Missing → `Result.Failure(TemplateErrors.ToolMissing)`.
6. Create assistant: `AiAssistant.Create(...)` with template's values, `slug = template.Slug`, `createdByUserId = command.CreatedByUserIdOverride ?? CurrentUser.UserId`.
7. Stamp provenance: `assistant.StampTemplateSource(template.Slug, version: null)`.
8. Apply tool list, persona targets, and visibility — every install is created `ResourceVisibility.TenantWide` (no per-install visibility override in 5c-2).
9. Persist + commit; return `Result.Success(assistant.Id)`.

**Errors** (`Starter.Module.AI/Domain/Errors/TemplateErrors.cs`):

- `NotFound(string slug)` → 404
- `AlreadyInstalled(string slug, Guid tenantId)` → 409
- `PersonaTargetMissing(string personaSlug)` → 400
- `ToolMissing(string toolName)` → 400

Cross-tenant install without superadmin returns the standard `Forbidden` failure (existing pattern, not a template-specific error).

### 3.6 `AiAssistant` schema delta

Two nullable columns added to `AiAssistant`:

```csharp
public string? TemplateSourceSlug { get; private set; }      // maxLen 128
public string? TemplateSourceVersion { get; private set; }   // maxLen 32, reserved

public void StampTemplateSource(string templateSlug, string? version = null)
{
    TemplateSourceSlug = templateSlug;
    TemplateSourceVersion = version;
}
```

EF configuration updates the `AiAssistantConfiguration` to declare both string lengths. No index. No data migration needed for existing rows — both columns stay null.

`AiAssistantDto` gains the two nullable fields. The mapper populates them.

Per project rule (`feedback_no_migrations`), no migration is committed; downstream apps regenerate `InitialCreate`.

### 3.7 API surface

New file: `boilerplateBE/src/modules/Starter.Module.AI/Controllers/AiTemplatesController.cs`.

```
GET  /api/v{version}/ai/templates                 [Authorize(Ai.ManageAssistants)]
POST /api/v{version}/ai/templates/{slug}/install  [Authorize(Ai.ManageAssistants)]
```

`GET` returns `ApiResponse<IReadOnlyList<AiAgentTemplateDto>>` (not paginated; the catalog is bounded by code).

`POST` body: `InstallTemplateBody(Guid? TargetTenantId)`. Returns `ApiResponse<Guid>` (the new assistant id) on success.

### 3.8 `AiAgentTemplateDto`

```csharp
public sealed record AiAgentTemplateDto(
    string Slug,
    string DisplayName,
    string Description,
    string Category,
    string Module,                    // from IAiAgentTemplateModuleSource
    string Provider,                  // enum-as-string
    string Model,
    double Temperature,
    int MaxTokens,
    string ExecutionMode,             // enum-as-string
    IReadOnlyList<string> EnabledToolNames,
    IReadOnlyList<string> PersonaTargetSlugs,
    string? SafetyPresetHint);
```

Mapper lives next to `AiToolMappers.cs` — same file or a sibling, follow what the team prefers in code review.

### 3.9 `GetTemplatesQuery`

```csharp
public sealed record GetTemplatesQuery() : IRequest<Result<IReadOnlyList<AiAgentTemplateDto>>>;
```

Handler reads `IAiAgentTemplateRegistry.GetAll()`, maps to `AiAgentTemplateDto`, returns sorted by `(Category, Slug)`. Pure read, no DB access.

---

## 4. Demo templates

Four demo templates ship in-tree — two products × two providers. Provider/model is part of the preset's contract (system prompts are tuned to a model's capabilities); Anthropic + OpenAI variants ship side-by-side so admins can compare agents head-to-head.

### 4.1 Support Assistant (Core)

Lives in `boilerplateBE/src/Starter.Application/Features/Ai/Templates/`:

```
SupportAssistantPrompts.cs                    (const SystemPrompt, const Description)
SupportAssistantAnthropicTemplate.cs
SupportAssistantOpenAiTemplate.cs
```

Shared prompt (in `SupportAssistantPrompts.SystemPrompt`):

> "You are a helpful support assistant. Answer questions about users and team members using the `list_users` tool. Never fabricate user data. If you can't find what you're asked about, say so clearly."

| Slug                          | Provider  | Model                       | Temp | MaxTokens |
|-------------------------------|-----------|-----------------------------|------|-----------|
| `support_assistant_anthropic` | Anthropic | `claude-sonnet-4-20250514`  | 0.3  | 2048      |
| `support_assistant_openai`    | OpenAI    | `gpt-4o-mini`               | 0.3  | 2048      |

Both: `Category = "General"`, `EnabledToolNames = ["list_users"]`, `PersonaTargetSlugs = ["default"]`, `SafetyPresetHint = SafetyPreset.Standard`, `ExecutionMode = AssistantExecutionMode.Chat`. `DisplayName` differs only by provider tag (`"Support Assistant (Anthropic)"`, `"Support Assistant (OpenAI)"`).

Registered via the existing `services.AddAiAgentTemplatesFromAssembly(typeof(DependencyInjection).Assembly)` line in `Starter.Application.DependencyInjection`.

### 4.2 Product Expert (Products module)

Lives in `boilerplateBE/src/modules/Starter.Module.Products/Application/Templates/`:

```
ProductExpertPrompts.cs                       (const SystemPrompt, const Description)
ProductExpertAnthropicTemplate.cs
ProductExpertOpenAiTemplate.cs
```

Shared prompt (in `ProductExpertPrompts.SystemPrompt`):

> "You are a product catalog specialist. Use the `list_products` tool to answer questions about what's available, filter by status, and search by name or SKU. Always cite the product name when referencing one; do not invent products not returned by the tool."

| Slug                       | Provider  | Model                       | Temp | MaxTokens |
|----------------------------|-----------|-----------------------------|------|-----------|
| `product_expert_anthropic` | Anthropic | `claude-sonnet-4-20250514`  | 0.4  | 3072      |
| `product_expert_openai`    | OpenAI    | `gpt-4o-mini`               | 0.4  | 3072      |

Both: `Category = "Products"`, `EnabledToolNames = ["list_products"]`, `PersonaTargetSlugs = ["default"]`, `SafetyPresetHint = SafetyPreset.Standard`, `ExecutionMode = AssistantExecutionMode.Chat`.

Registered via `services.AddAiAgentTemplatesFromAssembly(typeof(ProductsModule).Assembly)` in `ProductsModule.ConfigureServices` next to the existing tool-scan line.

### 4.3 Provider availability and install

Install does NOT validate runtime provider availability — a tenant can install `support_assistant_openai` even if OpenAI isn't configured for that tenant. Chat against an unconfigured provider fails at execution time with the existing provider-not-configured error, mirroring how user-created assistants behave today. This keeps install validation focused on what the install owns (template existence, persona/tool resolution, slug uniqueness).

---

## 5. Sample-seed migration

The legacy `AI:SeedSampleAssistant` flag and its hardcoded `"AI Tools Demo"` assistant are retired. Replaced by `AI:InstallDemoTemplatesOnStartup` which exercises the new template install path.

### 5.1 Configuration change

```jsonc
// appsettings.Development.json — old
"AI": { "SeedSampleAssistant": true }

// appsettings.Development.json — new
"AI": { "InstallDemoTemplatesOnStartup": true }
```

The old key is removed (not aliased) — it's a dev convenience flag, not a persisted setting. Updating dev appsettings is a manual coordinated change in this PR.

### 5.2 Seed handler

`AIModule.SeedDataAsync` rewrites to:

1. If `AI:InstallDemoTemplatesOnStartup` is false → return.
2. Read all tenants from `IApplicationDbContext.Tenants`.
3. For each tenant × each demo template slug (`support_assistant_anthropic`, `support_assistant_openai`, `product_expert_anthropic`, `product_expert_openai`):
   - Send `InstallTemplateCommand(slug, tenantId, CreatedByUserIdOverride: Guid.Empty)` via `IMediator`.
   - Idempotency: the command's own `AlreadyInstalled` guard makes restarts safe — already-installed templates return a failure result that the seed loop logs at Debug and skips.
   - Soft-fail: `PersonaTargetMissing` and `ToolMissing` errors during seed are logged at Warning and skipped (partial seeding beats crash-on-boot). Admin-triggered installs still surface these as 400.

### 5.3 Existing "AI Tools Demo" rows

Existing dev databases retain their row from the legacy seed. We don't delete it (users may have played with it). It stops being re-created on subsequent boots; new tenants get the four new demos instead.

---

## 6. Tests

Test counts below are targets, not commitments — actual breakdown emerges during TDD.

### 6.1 `Starter.Abstractions`

- **`AiAgentTemplateDiscoveryExtensionsTests`**
  - Scanner registers concrete classes implementing `IAiAgentTemplate`
  - Scanner skips abstract / generic / interface types
  - Scanner throws when a candidate type lacks a parameterless constructor (message names the type)
  - `DeriveModuleSource` produces `"Core"` for `Starter.Application`, `"Products"` for `Starter.Module.Products`, `"AI"` for `Starter.Module.AI`
  - `ValidateShape` rejects every empty/invalid field with a message naming the offending property
- **`AiAgentTemplateRegistrationTests`**
  - Wraps an instance and exposes `IAiAgentTemplateModuleSource` with the correct module string
  - Delegates all `IAiAgentTemplate` properties to the wrapped instance

### 6.2 AI module — registry

- **`AiAgentTemplateRegistryTests`**
  - `GetAll` returns all registered templates ordered by `(Category, Slug)`
  - `Find(slug)` returns the matching instance or null
  - Constructor throws `InvalidOperationException` on duplicate slug — message names both colliding types

### 6.3 AI module — install command

- **`InstallTemplateCommandValidatorTests`** — empty slug rejected; over-128-char slug rejected
- **`InstallTemplateCommandHandlerTests`**
  - Happy path: existing tenant + valid slug → creates `AiAssistant` with template values + `TemplateSourceSlug` stamped
  - Template not found → `TemplateErrors.NotFound`
  - Duplicate install → `TemplateErrors.AlreadyInstalled`
  - Missing tenant persona → `TemplateErrors.PersonaTargetMissing`
  - Tool slug not in registry → `TemplateErrors.ToolMissing`
  - Cross-tenant without superadmin → `Forbidden`
  - Cross-tenant with superadmin → writes to target tenant
  - `CreatedByUserIdOverride = Guid.Empty` → assistant has `CreatedByUserId = Guid.Empty` (seed path)
  - Persona validation queries target tenant's personas (not caller's)

### 6.4 AI module — catalog API

- **`GetTemplatesQueryHandlerTests`** — maps registry to DTO including `Module` field; sorted output
- **`AiTemplatesControllerTests`** (in-memory DB) — `GET` returns catalog; `POST /install` returns 201 + id; second `POST /install` returns 409

### 6.5 Seed migration

- **`InstallDemoTemplatesSeedTests`**
  - Flag off → no installs
  - Flag on + fresh tenant → installs all four demo templates
  - Flag on + tenant already installed → idempotent (zero new rows, zero errors)
  - Missing persona on seed → logged at Warning, boot continues

### 6.6 Entity-level

- **`AiAssistantTemplateSourceTests`** — `StampTemplateSource` sets both fields; default values are null/null; `AiAssistantDto` mapper exposes the fields

### 6.7 Coverage explicitly NOT in 5c-2

- Frontend types / admin UI (Plan 7a)
- Runtime chat execution against installed-template assistants (existing chat-execution tests cover this — templates don't change the runtime pipeline)
- KB ingest paths (deferred — see backlog)

---

## 7. Permissions

Reuses `Ai.ManageAssistants`. No new permission constants. No changes to seed roles or FE permission mirror.

Cross-tenant install (any non-null `TargetTenantId` differing from `CurrentUser.TenantId`) requires superadmin, validated by the same check pattern used in `CreatePersonaCommand` and the 5b cross-tenant guards.

---

## 8. Risks and mitigations

- **Drift between template's tool/persona slug references and reality.** Mitigated by install-time validation (PersonaTargetMissing / ToolMissing) — broken template fails loudly rather than installing a half-working agent.
- **Slug collision across modules.** Caught at registry construction (composition-time). A failing app boot is preferable to a runtime tool/template mix-up.
- **Provider config drift breaking demo templates.** Templates pin specific model IDs; if `claude-sonnet-4-20250514` or `gpt-4o-mini` is later removed from the provider config, install still succeeds but chat fails at runtime. Acceptable — same behaviour as user-created assistants today.
- **Reserved-nullable `TemplateSourceVersion` invites speculation.** The column is genuinely reserved for the update-detection feature. Plan 5c-2 commits zero code that reads or writes it. Removing the column "for YAGNI" then re-adding it later in 5c-3 is migration churn we sidestep by adding both columns at once.

---

## 9. Future work

All deferred items from this plan are tracked in `docs/modules/ai/backlog.md`. That file is the source of truth for follow-up scope; this design doc is frozen at submission time.
