# AI Module — Plan 5b: Persona Primitive (Design Spec)

**Date:** 2026-04-24
**Status:** Ready for implementation plan
**Predecessor:** [Plan 5a — Agent Runtime Abstraction](2026-04-23-ai-plan-5a-agent-runtime-design.md)
**Roadmap parent:** [AI Module — Revised Vision](2026-04-23-ai-module-vision-revised-design.md)

---

## 1. Purpose

Introduce **Persona** as a first-class primitive in the AI module, orthogonal to the existing Role (permissions) system. Persona answers *"what AI experience does this user get"* — the set of agents they can talk to, the system-prompt framing, and the safety preset applied to every conversation they participate in.

This is foundational for everything downstream in the roadmap:

- **5c** — Agent templates target personas.
- **5d** — Content moderation's preset is inherited from persona defaults.
- **5e** — Bundled platform agents ship with starter persona targets.
- **5f** — Admin AI Settings let tenants override persona defaults.
- **6 / 7a / 7b** — Chat sidebar filters agents by the current user's persona; admin UI manages personas.
- **8a–8f** — Inline AI surfaces (insights, actions, automations, end-customer widgets) are persona-filtered.

5b ships the backend data model, resolution pipeline, system-prompt injection, and REST API surface. **No frontend UI in this plan** — the Persona Manager page lands in Plan 7a.

---

## 2. Scope

### In scope

1. `AiPersona` aggregate (tenant-scoped) — data model, EF config, CRUD command/query handlers, validators.
2. `UserPersona` join table — user↔persona assignment with a default-persona flag.
3. `AiAssistant` additions — `Slug` (unique per tenant) and `PersonaTargetSlugs` (JSONB array).
4. **System-reserved personas** — `anonymous` (not deletable) and `default` (deletable once another persona exists).
5. **Tenant lifecycle** — eager seeding on `TenantCreated`; new users get `default` persona assigned on `UserCreated`.
6. **Persona resolution pipeline** — `IPersonaResolver` returns the active `PersonaContext` for a chat request (override → default → anonymous fallback).
7. **Agent visibility filter** — mutual narrowing between `AiPersona.PermittedAgentSlugs` and `AiAssistant.PersonaTargetSlugs`.
8. **Safety-preset clause injection** — `ISafetyPresetClauseProvider` prepends a preset-specific clause to the effective system prompt, localised EN + AR.
9. **Chat command updates** — `SendChatMessageCommand.PersonaId?` optional override; `AiChatReplyDto.PersonaSlug`; `ChatStreamEvent.Started.PersonaSlug`.
10. **REST API** — `AiPersonasController` with CRUD + assignment routes; `GET /ai/me/personas` for Plan 6.
11. **Permissions** — `Ai.ViewPersonas`, `Ai.ManagePersonas`, `Ai.AssignPersona`; default role mappings.
12. **Tests** — unit (resolver, bootstrap, safety clauses, visibility rules), integration (controller, chat path), validator coverage.

### Out of scope (explicit non-goals)

- **No admin UI** (Plan 7a).
- **No actual content moderation** — safety preset is stored + system-prompt-injected only. Real input/output filters are Plan 5d.
- **No public / anonymous-surface endpoints** — `anonymous` persona exists in the data model and the resolver returns it for unauthenticated contexts, but no unauthenticated controller is added here (Plan 8f).
- **No per-widget API key auth** — Plan 5f.
- **No JWT claim or principal enrichment with persona** — persona is resolved per-request from DB (cached in-memory per request). Claim-level enrichment is deferred until chat sidebar (Plan 6) actually needs it across multiple endpoints.
- **No mobile changes** — persona awareness on Flutter arrives in Plan 9.
- **No migration backfill logic** — no live data / existing tenants to migrate. Boilerplate does not commit EF migration files; downstream apps generate their own.
- **No module decoupling work** — AI module already has its own `AiDbContext`; persona entities land there, referencing core `User.Id` by raw GUID (no navigation property) per existing cross-module pattern.

---

## 3. Locked decisions (from brainstorming)

| # | Decision | Rationale |
|---|---|---|
| 1 | **Multi-persona users with one default.** A user can hold many `UserPersona` rows; exactly one is marked `IsDefault`. The chat command accepts an optional `PersonaId` override; authenticated users default to their `IsDefault` row; unauthenticated contexts resolve to tenant's `anonymous` persona. | Flexible for flagship cases (teacher-who-is-also-a-parent) without introducing a second auth system. Keeps the resolver deterministic. |
| 2 | **Safety preset gets a real system-prompt clause in 5b.** `ISafetyPresetClauseProvider` prepends a preset- and audience-specific sentence to the effective system prompt, localised EN + AR. Plan 5d swaps in the real moderation pipeline behind the same interface. | Makes the flagship acid test ("Student persona gets ChildSafe filter") demonstrable today. No provider changes required. |
| 3 | **Add `Slug` to `AiAssistant` now.** Unique per tenant. Auto-derived from `Name` on create, editable. `PermittedAgentSlugs` on persona references it. | Needed by 5c templates anyway; doing it in 5b avoids churn and keeps cross-tenant template targeting stable. |
| 4 | **Seed `anonymous` + `default` per tenant on `TenantCreated`.** `anonymous` is system-reserved (not deletable, audience immutable). `default` is a convenience — tenant admins can delete it once they've created real personas. New users auto-assigned `default` as their default. | Fresh tenants work on day-one without admin config; no one-shot backfill service required (no live data exists). |

---

## 4. Data model

All entities live in `Starter.Module.AI`'s own DbContext (`AiDbContext`). Cross-module references to core entities (`User.Id`, `Tenant.Id`) are raw `Guid` columns — no EF navigation properties — matching the pattern already used by `AiAssistant.CreatedByUserId`.

### 4.1 `AiPersona` (aggregate root, tenant-scoped)

```csharp
public sealed class AiPersona : AggregateRoot, ITenantEntity
{
    public Guid? TenantId { get; private set; }          // Required in practice; null disallowed via validator
    public string Slug { get; private set; }             // Lowercase kebab-case, unique per tenant, max 64
    public string DisplayName { get; private set; }      // Max 120
    public string? Description { get; private set; }     // Max 500
    public PersonaAudienceType AudienceType { get; private set; }
    public SafetyPreset SafetyPreset { get; private set; }
    public IReadOnlyList<string> PermittedAgentSlugs { get; private set; } // JSONB; empty = no restriction
    public bool IsSystemReserved { get; private set; }   // true only for `anonymous`
    public bool IsActive { get; private set; }

    public Guid CreatedByUserId { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? ModifiedAt { get; private set; }
    public string? CreatedBy { get; private set; }
    public string? ModifiedBy { get; private set; }
}

public enum PersonaAudienceType { Internal, EndCustomer, Anonymous }
public enum SafetyPreset { Standard, ChildSafe, ProfessionalModerated }
```

**EF config** (`AiPersonaConfiguration`):
- Table `ai_personas`.
- `PermittedAgentSlugs` → JSONB with value converter + value comparer (same pattern as `AiAssistant.EnabledToolNames`).
- Indexes:
  - `(TenantId, Slug)` unique
  - `(TenantId, AudienceType)`
  - `(TenantId, IsActive)`
- Global query filter: `CurrentTenantId == null || TenantId == CurrentTenantId`.

**Domain invariants** (enforced in factory methods + validators):
- `Slug` matches `^[a-z0-9]+(-[a-z0-9]+)*$`, length 1..64.
- Reserved slugs `anonymous` and `default` can only be created by the bootstrap service (flag on the factory method).
- `anonymous` persona: `IsSystemReserved=true`, `AudienceType=Anonymous` — both immutable after creation.
- When `AudienceType=Anonymous`, the persona is assumed to represent unauthenticated access; only one such persona allowed per tenant (enforced by bootstrap + validator).

### 4.2 `UserPersona` (join table, owned by AI module)

```csharp
public sealed class UserPersona
{
    public Guid UserId { get; private set; }     // Raw GUID; no navigation to core User
    public Guid PersonaId { get; private set; }  // FK to AiPersona
    public Guid TenantId { get; private set; }   // Denormalised for query filter + indexes
    public bool IsDefault { get; private set; }
    public DateTime AssignedAt { get; private set; }
    public Guid? AssignedBy { get; private set; }

    // Navigation
    public AiPersona Persona { get; private set; } = null!;
}
```

**EF config** (`UserPersonaConfiguration`):
- Table `ai_user_personas`.
- Composite PK: `(UserId, PersonaId)`.
- FK: `PersonaId → ai_personas.Id` with cascade delete.
- Indexes:
  - `(TenantId, UserId)` — default-persona lookup and user's persona list
  - `(PersonaId)` — "who has this persona" list
  - Filtered unique: `(UserId, TenantId) WHERE IsDefault` — exactly one default per user per tenant.
- Global query filter: `CurrentTenantId == null || TenantId == CurrentTenantId`.

### 4.3 `AiAssistant` additions

Existing entity at `boilerplateBE/src/modules/Starter.Module.AI/Domain/Entities/AiAssistant.cs`:

```csharp
public string Slug { get; private set; }                              // NEW — unique per tenant, max 64
public IReadOnlyList<string> PersonaTargetSlugs { get; private set; } // NEW — JSONB, empty = all personas
```

**EF config updates:**
- `Slug`: required, max 64, `(TenantId, Slug)` unique index.
- `PersonaTargetSlugs`: JSONB via same converter pattern as `EnabledToolNames`.

**Slug auto-generation.** When no slug is provided on `CreateAssistantCommand`, the handler slugifies `Name` (kebab-case, strip non-alphanum, collapse dashes, trim to 64). If the candidate collides with another slug in the tenant, append `-2`, `-3`, … until unique. Update / rename flow re-validates uniqueness but does not auto-rename.

### 4.4 Visibility rule (bidirectional narrowing)

An assistant is **visible to** a persona iff:

```
(persona.PermittedAgentSlugs.Count == 0 OR persona.PermittedAgentSlugs.Contains(assistant.Slug))
AND
(assistant.PersonaTargetSlugs.Count == 0 OR assistant.PersonaTargetSlugs.Contains(persona.Slug))
```

Empty on either side means "no restriction from this side". Both empty = visible. Either non-empty = that side must list the other explicitly. This keeps each side's configuration local: a tenant admin defining a persona can say "only these agents," and an agent author can say "only for these personas." Intersection wins.

---

## 5. Tenant & user lifecycle

### 5.1 On `TenantCreated` (domain event)

An AI-module handler `SeedTenantPersonasDomainEventHandler` runs inside the creating `UnitOfWork` and inserts two personas:

| Slug | Display Name | Audience | Safety | PermittedAgentSlugs | IsSystemReserved | IsActive |
|---|---|---|---|---|---|---|
| `anonymous` | *"Anonymous"* | `Anonymous` | `Standard` | `[]` | `true` | `false` *(admins enable when going live)* |
| `default` | *"Default"* | `Internal` | `Standard` | `[]` | `false` | `true` |

Idempotency: the handler checks `AiPersonas.AnyAsync(p => p.TenantId == tenantId && p.Slug == slug)` before insert, so reruns are safe. The handler tolerates the `anonymous` persona already existing without failing tenant creation.

### 5.2 On `UserCreated` (domain event)

A handler `AssignDefaultPersonaDomainEventHandler` resolves the tenant's `default` persona and inserts a `UserPersona(userId, defaultPersonaId, tenantId, IsDefault=true)`.

If the tenant has no `default` persona (admin deleted it), the handler falls back to:
1. Any other active `Internal` persona in the tenant, marked `IsDefault` for this user.
2. If none exists, no persona is assigned; the chat resolver will return `Persona.NoDefaultForUser` and the admin must assign one explicitly.

### 5.3 Delete protections

- `DeletePersonaCommand` rejects if `IsSystemReserved = true` — `Errors.Persona.CannotDeleteSystemReserved`.
- `DeletePersonaCommand` rejects if any `UserPersona` row still references it — `Errors.Persona.HasActiveAssignments`. Admin must reassign users first.
- `UnassignPersonaCommand` rejects removal of a user's *only* persona — `Errors.Persona.CannotRemoveLastAssignment` — to keep the resolver invariant "authenticated user always resolves to a persona."

---

## 6. Persona resolution pipeline

### 6.1 `PersonaContext`

New record in `Starter.Module.AI/Application/Services/Runtime/PersonaContext.cs`:

```csharp
internal sealed record PersonaContext(
    Guid Id,
    string Slug,
    PersonaAudienceType Audience,
    SafetyPreset Safety,
    IReadOnlyList<string> PermittedAgentSlugs);
```

Added to `AgentRunContext` as `PersonaContext? Persona` — nullable so non-chat agent runs (e.g., future task-triggered agents without a caller persona) can pass `null`.

### 6.2 `IPersonaResolver`

Located at `Application/Services/Personas/IPersonaResolver.cs`; implementation in `Infrastructure/Services/Personas/PersonaResolver.cs`.

```csharp
internal interface IPersonaResolver
{
    Task<Result<PersonaContext>> ResolveAsync(
        Guid? explicitPersonaId,      // From SendChatMessageCommand.PersonaId
        CancellationToken ct);
}
```

**Algorithm:**

```
if (explicitPersonaId.HasValue)
{
    persona = AiPersonas.Where(Id = explicitPersonaId && IsActive).FirstOrDefault()
    if (persona is null) return Persona.NotFound
    if (currentUser.IsAuthenticated)
    {
        assigned = UserPersonas.Any(UserId = currentUser.Id && PersonaId = persona.Id)
        if (!assigned) return Persona.NotAssignedToUser
    }
    else if (persona.AudienceType != Anonymous) return Persona.RequiresAuthentication
    return Ok(Map(persona))
}

if (currentUser.IsAuthenticated)
{
    default = UserPersonas
        .Include(up => up.Persona)
        .Where(up => up.UserId = currentUser.Id && up.IsDefault && up.Persona.IsActive)
        .FirstOrDefault()
    if (default is null) return Persona.NoDefaultForUser
    return Ok(Map(default.Persona))
}

// Unauthenticated fallback (wired for 8f public surface; not actually reachable from 5b auth'd chat)
anonymous = AiPersonas
    .Where(p => p.TenantId = CurrentTenantId && p.Slug = "anonymous" && p.IsActive)
    .FirstOrDefault()
if (anonymous is null) return Persona.AnonymousNotAvailable
return Ok(Map(anonymous))
```

Cached for the duration of the HTTP request via a request-scoped `IPersonaContextAccessor` (similar pattern to `ICurrentUserService`).

### 6.3 Chat pipeline integration

In `ChatExecutionService.PrepareTurnAsync`:

```
// New step — runs BEFORE existing assistant load
var personaResult = await personaResolver.ResolveAsync(command.PersonaId, ct);
if (personaResult.IsFailure) return personaResult.Error;
var persona = personaResult.Value;

// Existing assistant load (unchanged)
assistant = await context.AiAssistants.FirstOrDefaultAsync(...);

// NEW — visibility filter, after ACL check
if (!persona.IsAssistantVisible(assistant))
    return AiAssistant.NotPermittedForPersona;

// Existing RAG + prompt assembly
var ragContext = await RetrieveContextSafelyAsync(...);
var systemPrompt = ResolveSystemPrompt(assistant, ragContext, persona);  // persona added

// Existing AgentRunContext, with new Persona field
var runCtx = new AgentRunContext(
    messages, systemPrompt, modelConfig, tools, maxSteps, loopBreak,
    Streaming: streaming,
    Persona: persona);   // NEW
```

List endpoints (`GetAssistantsQuery`) gain a `WhereVisibleToPersona(persona)` EF extension applied after tenant filter.

---

## 7. Safety-preset clause injection

### 7.1 Interface

`Application/Services/Personas/ISafetyPresetClauseProvider.cs`:

```csharp
internal interface ISafetyPresetClauseProvider
{
    string GetClause(SafetyPreset preset, PersonaAudienceType audience, CultureInfo culture);
}
```

Default implementation `Infrastructure/Services/Personas/ResxSafetyPresetClauseProvider` reads from embedded resources:
- `Starter.Module.AI/Resources/SafetyPresets.en.resx`
- `Starter.Module.AI/Resources/SafetyPresets.ar.resx`

### 7.2 Clause keys (resource file contents)

| Key | English (approximate) |
|---|---|
| `Standard.*` | *(empty — no prepended clause)* |
| `ChildSafe.Internal` | *"You are assisting a minor under 16. Do not produce sexual, violent, or age-inappropriate content. Decline politely and suggest a safer alternative if asked. Avoid discussing self-harm; if mentioned, gently direct the user to a trusted adult or local helpline."* |
| `ChildSafe.EndCustomer` | *(same as Internal — audience doesn't modify ChildSafe)* |
| `ChildSafe.Anonymous` | *(same as Internal)* |
| `ProfessionalModerated.Internal` | *"Maintain a formal, professional tone. Never commit the organisation to actions, pricing, or deadlines — defer to a human for any commitment. Do not speculate on legal, financial, or medical advice."* |
| `ProfessionalModerated.EndCustomer` | *"You are speaking on behalf of the organisation to an external client. Maintain a formal tone. Never commit to pricing, deadlines, or contractual terms — always defer to a human. Decline speculation on legal, financial, or medical matters."* |
| `ProfessionalModerated.Anonymous` | *"You are speaking with an unauthenticated public visitor on behalf of the organisation. Do not reveal internal details. Do not commit to pricing, deadlines, or contractual terms. Decline speculation on legal, financial, or medical matters."* |

### 7.3 Prompt assembly order

```
effectivePrompt = SafetyClause(persona.Safety, persona.Audience, culture)  // may be empty
               + "\n\n" + assistant.SystemPrompt
               + (ragContext is not null ? "\n\n" + ragContext : "")
```

Empty clause → no leading blank line. Culture resolved from `IRequestCultureProvider` (existing boilerplate hook); defaults to `en` if missing.

### 7.4 Plan 5d contract

Plan 5d replaces `ResxSafetyPresetClauseProvider` with a composite that also runs input/output moderation adapters. The interface stays stable. The `ChatExecutionService` call site does not change.

---

## 8. REST API

### 8.1 Persona management — `AiPersonasController` (`api/v1/ai/personas`)

| Verb | Route | Permission | Command/Query |
|---|---|---|---|
| GET | `/` | `Ai.ViewPersonas` | `GetPersonasQuery(includeSystem=true, includeInactive=false)` → `List<AiPersonaDto>` |
| GET | `/{id}` | `Ai.ViewPersonas` | `GetPersonaByIdQuery(id)` → `AiPersonaDto` |
| POST | `/` | `Ai.ManagePersonas` | `CreatePersonaCommand(displayName, description?, slug?, audienceType, safetyPreset, permittedAgentSlugs)` → `AiPersonaDto` |
| PUT | `/{id}` | `Ai.ManagePersonas` | `UpdatePersonaCommand(id, displayName, description?, safetyPreset, permittedAgentSlugs, isActive)` — slug + audience immutable post-create |
| DELETE | `/{id}` | `Ai.ManagePersonas` | `DeletePersonaCommand(id)` — guard checks |

### 8.2 Assignment — `AiPersonaAssignmentsController` (`api/v1/ai/personas/{personaId}/assignments`)

| Verb | Route | Permission | Command/Query |
|---|---|---|---|
| GET | `/` | `Ai.ViewPersonas` | `GetPersonaAssignmentsQuery(personaId)` → `List<UserPersonaDto>` |
| POST | `/` | `Ai.AssignPersona` | `AssignPersonaCommand(personaId, userId, makeDefault=false)` |
| DELETE | `/{userId}` | `Ai.AssignPersona` | `UnassignPersonaCommand(personaId, userId)` |
| PUT | `/{userId}/default` | `Ai.AssignPersona` | `SetUserDefaultPersonaCommand(personaId, userId)` — flips the default flag, unflipping the previous default |

### 8.3 Current-user endpoint — `AiMePersonasController` (`api/v1/ai/me/personas`)

| Verb | Route | Permission | Purpose |
|---|---|---|---|
| GET | `/` | `Ai.Chat` | Returns `{ personas: UserPersonaDto[], defaultPersonaId: Guid }` — used by Plan 6 sidebar for the persona switcher. |

### 8.4 DTOs

```csharp
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

public sealed record UserPersonaDto(
    Guid UserId,
    string UserDisplayName,       // Looked up via core User at query time
    Guid PersonaId,
    string PersonaSlug,
    string PersonaDisplayName,
    bool IsDefault,
    DateTime AssignedAt);
```

### 8.5 Chat command & reply updates

```csharp
public sealed record SendChatMessageCommand(
    Guid? ConversationId,
    Guid? AssistantId,
    string Message,
    Guid? PersonaId = null)      // NEW
    : IRequest<Result<AiChatReplyDto>>;

public sealed record AiChatReplyDto(
    // existing fields...
    string PersonaSlug);          // NEW
```

`ChatStreamEvent.Started` frame gains `PersonaSlug` so streaming clients can render the active persona without a separate call.

### 8.6 Assistant command updates

```csharp
public sealed record CreateAssistantCommand(
    string Name,
    string? Slug,                                        // NEW — optional, auto-derived if null
    string SystemPrompt,
    // existing fields...
    IReadOnlyList<string>? PersonaTargetSlugs = null);   // NEW — null treated as empty

public sealed record UpdateAssistantCommand(
    Guid Id,
    string Name,
    string? Slug,                                        // NEW — null keeps existing
    string SystemPrompt,
    // existing fields...
    IReadOnlyList<string>? PersonaTargetSlugs = null);
```

---

## 9. Permissions

New entries in `boilerplateBE/src/modules/Starter.Module.AI/Constants/AiPermissions.cs`:

```csharp
public const string ViewPersonas = "Ai.ViewPersonas";
public const string ManagePersonas = "Ai.ManagePersonas";
public const string AssignPersona = "Ai.AssignPersona";
```

Mirrored in the frontend `boilerplateFE/src/constants/permissions.ts` under the `Ai` group (no UI consumer yet, but kept in sync per project rule).

**Default role mappings** (via `AIModule.GetDefaultRolePermissions()`):

| Role | ViewPersonas | ManagePersonas | AssignPersona |
|---|---|---|---|
| SuperAdmin | ✅ | ✅ | ✅ |
| Admin | ✅ | ✅ | ✅ |
| User | ✅ | — | — |

`User` gets read-only access so the persona switcher in the chat sidebar (Plan 6) can list the current user's persona options and names. It cannot create, edit, delete, or reassign.

---

## 10. Validators

### `CreatePersonaCommandValidator`
- `DisplayName`: required, 1..120.
- `Description`: optional, max 500.
- `Slug`: optional; if supplied, matches `^[a-z0-9]+(-[a-z0-9]+)*$`, 1..64. Rejected if `== "anonymous"` (system-reserved) or `== "default"` unless called from the bootstrap service.
- `AudienceType`: required enum; `Anonymous` rejected (`anonymous` persona is created only by bootstrap).
- `SafetyPreset`: required enum.
- `PermittedAgentSlugs`: optional; each entry matches slug regex; duplicates rejected.

### `UpdatePersonaCommandValidator`
- Same field rules as create, except `Slug` and `AudienceType` are immutable — the update command does not accept them.
- On the system-reserved `anonymous` persona: `IsSystemReserved` flag is also immutable. `DisplayName`, `Description`, `SafetyPreset`, `PermittedAgentSlugs`, and `IsActive` are freely editable.

### `DeletePersonaCommandValidator`
- Rejects `IsSystemReserved = true`.
- Rejects if any `UserPersona` rows reference the persona (explicit error pointing admin at reassignment flow).

### `AssignPersonaCommandValidator`
- `PersonaId`: exists in tenant, `IsActive`.
- `UserId`: exists and is in the same tenant as the persona.
- Duplicate assignment rejected (`(UserId, PersonaId)` PK already handles this; validator returns a friendly error).

### `UnassignPersonaCommandValidator`
- Rejects removal of a user's *only* persona.
- Rejects unassignment of the user's `IsDefault` persona unless at least one other assignment remains (in which case the handler transparently promotes the most-recent other assignment to default).

### `SetUserDefaultPersonaCommandValidator`
- Persona must already be assigned to the user.
- Handler flips `IsDefault` in a single transaction: unset the old default, set the new one.

### `CreateAssistantCommandValidator` & `UpdateAssistantCommandValidator` (additions)
- `Slug`: if supplied, matches slug regex, length 1..64, unique within `(TenantId, Slug)`. If omitted on create, handler derives from `Name`.
- `PersonaTargetSlugs`: optional; each entry matches slug regex; duplicates rejected. No validation that the slugs correspond to existing personas — deleted personas leave stale slugs in assistant configs and the visibility filter simply fails-closed (not-in-list). Handled in a housekeeping pass only if needed.

---

## 11. Domain errors

New entries in `AiErrors` (or a dedicated `PersonaErrors` static class):

| Code | Message |
|---|---|
| `Persona.NotFound` | Persona not found. |
| `Persona.NotAssignedToUser` | You do not have this persona assigned. |
| `Persona.RequiresAuthentication` | This persona is not available for anonymous access. |
| `Persona.NoDefaultForUser` | No default persona is configured for your account. Contact your administrator. |
| `Persona.AnonymousNotAvailable` | Anonymous persona is not configured or not active for this tenant. |
| `Persona.CannotDeleteSystemReserved` | System-reserved personas cannot be deleted. |
| `Persona.HasActiveAssignments` | Cannot delete a persona with active user assignments. Reassign users first. |
| `Persona.SlugReserved` | The slug '{slug}' is reserved. |
| `Persona.SlugAlreadyExists` | A persona with slug '{slug}' already exists. |
| `Persona.AnonymousAudienceImmutable` | Audience type of the anonymous persona cannot be changed. |
| `Persona.CannotRemoveLastAssignment` | Cannot unassign the user's only persona. Assign another first. |
| `AiAssistant.NotPermittedForPersona` | This assistant is not available for your current persona. |
| `AiAssistant.SlugAlreadyExists` | An assistant with slug '{slug}' already exists. |

---

## 12. Tests

Directory: `boilerplateBE/tests/Starter.Api.Tests/Ai/Personas/`.

### Unit

| File | Covers |
|---|---|
| `PersonaResolverTests.cs` | Override supplied → resolved; override not assigned to user → error; override for non-anonymous by unauth user → error; authenticated user, no explicit override → default; user without default + authenticated → error; unauthenticated → anonymous; anonymous missing → error |
| `SeedTenantPersonasDomainEventHandlerTests.cs` | First invocation seeds anonymous + default; rerun is a no-op; partial state (one of two present) seeds the missing one |
| `AssignDefaultPersonaDomainEventHandlerTests.cs` | New user gets default persona; tenant with no default persona falls back to first Internal persona; no Internal persona → no assignment |
| `SafetyPresetClauseProviderTests.cs` | Standard → empty clause; ChildSafe (EN/AR) returns localised clauses; ProfessionalModerated returns audience-specific clauses; unknown culture falls back to `en` |
| `PersonaAssistantVisibilityTests.cs` | Both empty → visible; persona lists assistant → visible; persona excludes assistant → hidden; assistant targets persona → visible; assistant excludes persona → hidden; intersection rules (both sides non-empty) |
| `CreatePersonaCommandValidatorTests.cs` | Slug regex, reserved-slug rejection, audience required, etc. |
| `UpdatePersonaCommandValidatorTests.cs` | Slug / audience immutability on anonymous |
| `DeletePersonaCommandValidatorTests.cs` | System-reserved rejection, assignments-exist rejection |
| `AssignPersonaCommandValidatorTests.cs` | Duplicate assignment rejection, cross-tenant rejection |
| `UnassignPersonaCommandValidatorTests.cs` | Last-assignment rejection, default-reassignment behaviour |
| `AiAssistantSlugTests.cs` | Slug auto-derivation, collision `-2 / -3` suffix, uniqueness per tenant |

### Integration (using `FakeAiProvider` + EF Core SQLite or in-memory)

| File | Covers |
|---|---|
| `AiPersonasControllerTests.cs` | CRUD happy path + all delete guards + assignment endpoints |
| `AiMePersonasControllerTests.cs` | Current user's personas list with default flag |
| `ChatExecutionServicePersonaPathTests.cs` | Explicit override reaches runtime; default used when no override; unpermitted assistant → error; safety clause prepended to system prompt (ChildSafe, ProfessionalModerated); anonymous persona path with unauth principal reaches runtime |
| `AgentListPersonaFilterTests.cs` | `GetAssistantsQuery` filters by current persona |
| `SendChatMessageCommandTests.cs` (existing test file, updates) | New `PersonaId` field wiring; `AiChatReplyDto.PersonaSlug` populated; stream `started` frame carries slug |

### End-to-end (smoke)

One extra case added to `AgentRuntimeEndToEndTests.cs`:
- Full chat turn with an explicit `PersonaId` for a ChildSafe persona; asserts the outgoing system prompt (captured via `FakeAiProvider`) begins with the ChildSafe clause followed by the assistant's base prompt.

---

## 13. Observability

- `PersonaContext` attached to the OpenTelemetry Activity under `ChatExecutionService` span as attributes: `ai.persona.slug`, `ai.persona.audience`, `ai.persona.safety`.
- `AiAgentMetrics` adds a counter `ai_agent_runs_by_persona_total{persona_slug, audience, safety}` (tag cardinality is bounded: per-tenant × persona count).
- Persona resolution failures emit structured logs with `persona.slug` context.

---

## 14. Feature flag

A new feature flag `Ai.Personas.Enabled` (default `true`) guards the resolver. When disabled:
- `IPersonaResolver.ResolveAsync` returns `null` (via `Result<PersonaContext?>`), skipping the safety clause and visibility filter entirely.
- Controllers and commands return 404 for persona endpoints.
- Provides an operational kill-switch if persona-driven filtering surfaces issues post-deploy.

The flag is considered short-lived — removed once 5c lands (at which point persona targeting is core to agent selection).

---

## 15. Acceptance criteria

5b is done when:

1. `AiPersona` and `UserPersona` tables exist with EF configs + query filters; tests pass.
2. `AiAssistant` has `Slug` and `PersonaTargetSlugs`; slug auto-generation works; uniqueness enforced.
3. `TenantCreated` handler seeds `anonymous` + `default`; `UserCreated` handler assigns `default`; both are idempotent.
4. `IPersonaResolver` returns the right persona for every path in the algorithm; covered by tests.
5. `ChatExecutionService`:
   - Calls the resolver before assistant load.
   - Applies the visibility filter after ACL check.
   - Prepends the safety-preset clause to the effective system prompt.
   - Puts `PersonaContext` on `AgentRunContext`.
   - Populates `PersonaSlug` on reply DTO and stream-started frame.
6. `AiPersonasController` + `AiPersonaAssignmentsController` + `AiMePersonasController` expose the documented endpoints with validators and permission gates.
7. `Ai.ViewPersonas` / `Ai.ManagePersonas` / `Ai.AssignPersona` permissions exist, wired to default role grants, mirrored in frontend permission constants.
8. Feature flag `Ai.Personas.Enabled` short-circuits the pipeline when off.
9. All tests (unit + integration + e2e smoke) pass.
10. `dotnet test boilerplateBE/Starter.sln` green; `npm run build` green.

---

## 16. Flagship acid test

Per the roadmap, the School SaaS test case for 5b is:

> *Teacher / Student / Parent personas created; Student gets ChildSafe filter.*

Demonstrable after 5b ships:
1. Tenant admin creates three personas: `teacher` (Internal, Standard), `student` (Internal, ChildSafe), `parent` (EndCustomer, Standard).
2. Assigns a Student user to the `student` persona as default.
3. Sends a chat message. The outgoing system prompt (visible via OpenTelemetry trace) begins with the ChildSafe clause. Safety behaviour is clause-driven for now; Plan 5d replaces it with real moderation.

Social SaaS test case (same plan):
> *Editor / Approver / Client personas created.*

All three exist with the right audience + preset. Client persona carries `ProfessionalModerated`; its clause drops into every Client-persona chat system prompt.

---

## 17. Risks & open questions

| Risk | Mitigation |
|---|---|
| Persona resolution cost added to every chat turn | Request-scoped cache; single indexed query `(UserId, TenantId)`; default-persona filtered unique index keeps the query O(1). |
| Slug collisions between personas and agents | Separate tables, separate slug namespaces. Nothing references both by slug in the same context. |
| Admins deleting `default` persona before assigning users to another persona | `UserCreated` handler falls back to any other active Internal persona; if none, no assignment, admin gets clear error on first chat attempt. Acceptable — admin intent is clear. |
| Safety-clause wording is imperfect / gameable | Intended as a starting point; Plan 5d is the real safety layer. Clauses live in resource files for easy tuning without code changes. |
| Stale slugs in `PersonaTargetSlugs` after persona delete | Visibility filter fails closed (stale slug → no match from that side). Housekeeping deferred until 5c audit surfaces need. |
| Feature flag behaviour when persona references stale agents | Flag disables the pipeline entirely — no persona, no visibility filter, no clause. Chat works identically to pre-5b. |

**Open items resolved during brainstorming:** none remaining.

---

## 18. UI / UX — deferred to later phases

5b ships **backend-only**. All user-facing persona UI is intentionally deferred, per the roadmap's foundation-first sequencing (Decision 11 in `2026-04-23-ai-module-vision-revised-design.md`). Concretely:

| UI surface | Deferred to | Depends on 5b endpoint |
|---|---|---|
| Persona Manager admin page — full CRUD, safety-preset selector, permitted-agent picker | **Plan 7a — Core Admin Pages** | `AiPersonasController` |
| Persona assignment UI — user-detail page showing personas, set-default toggle | **Plan 7a** | `AiPersonaAssignmentsController` |
| Persona switcher in chat sidebar + filtered agent list | **Plan 6 — Chat Sidebar UI** | `GET /api/v1/ai/me/personas` |
| Assistant builder with persona-target picker + slug field | **Plan 7b — Advanced Admin Pages** | `CreateAssistantCommand` / `UpdateAssistantCommand` extensions |
| AI Settings: tenant-level default safety preset | **Plan 7b** | (future tenant-settings endpoint; not in 5b) |

The 5b acceptance tests are all API- / trace-observable. No screens are required to validate the work.

## 19. Non-goals restated

5b ships **backend-only** and **non-destructive**. No UI, no mobile changes, no public (unauthenticated) endpoints, no actual moderation, no JWT enrichment, no migration backfill. The implementation plan will mirror this scope.

---

## 20. Files touched (summary)

**New (approximate):**
- `Domain/Entities/AiPersona.cs`, `UserPersona.cs`
- `Domain/Enums/PersonaAudienceType.cs`, `SafetyPreset.cs`
- `Domain/Errors/PersonaErrors.cs` (or additions to `AiErrors`)
- `Application/Services/Personas/IPersonaResolver.cs`, `ISafetyPresetClauseProvider.cs`, `IPersonaContextAccessor.cs`
- `Application/Services/Runtime/PersonaContext.cs`
- `Application/Commands/Personas/*` (Create, Update, Delete, Assign, Unassign, SetDefault) — command + handler + validator each
- `Application/Queries/Personas/*` (List, ById, Assignments, MePersonas)
- `Application/DTOs/AiPersonaDto.cs`, `UserPersonaDto.cs`
- `Infrastructure/Configurations/AiPersonaConfiguration.cs`, `UserPersonaConfiguration.cs`
- `Infrastructure/Services/Personas/PersonaResolver.cs`, `ResxSafetyPresetClauseProvider.cs`, `PersonaContextAccessor.cs`, `TenantPersonaBootstrapService.cs` (if a service wrapper is preferred over raw handlers)
- `Infrastructure/DomainEventHandlers/SeedTenantPersonasDomainEventHandler.cs`, `AssignDefaultPersonaDomainEventHandler.cs`
- `Resources/SafetyPresets.en.resx`, `SafetyPresets.ar.resx`
- `Controllers/AiPersonasController.cs`, `AiPersonaAssignmentsController.cs`, `AiMePersonasController.cs`
- Test files under `tests/Starter.Api.Tests/Ai/Personas/`

**Modified:**
- `Domain/Entities/AiAssistant.cs` — `Slug`, `PersonaTargetSlugs` fields + factory updates
- `Infrastructure/Configurations/AiAssistantConfiguration.cs` — new columns / indexes
- `Application/Services/Runtime/AgentRunContext.cs` — `PersonaContext? Persona`
- `Application/Services/ChatExecutionService.cs` — resolver call, visibility filter, clause injection, context population
- `Application/Commands/SendChatMessage/SendChatMessageCommand.cs` — `PersonaId`
- `Application/DTOs/AiChatReplyDto.cs` — `PersonaSlug`
- `Application/Services/ChatAgentRunSink.cs` / streaming event shapes — `PersonaSlug` on `started` frame
- `Application/Commands/CreateAssistant/*`, `UpdateAssistant/*` — slug + persona-targets fields
- `Constants/AiPermissions.cs` — new permissions
- `AIModule.cs` — DI for resolver, clause provider, accessor, domain event handlers; role-permission seed for new permissions
- `boilerplateFE/src/constants/permissions.ts` — mirror new permissions (no UI consumer yet)
- Existing assistant / chat tests — updated to account for new optional fields without breaking
