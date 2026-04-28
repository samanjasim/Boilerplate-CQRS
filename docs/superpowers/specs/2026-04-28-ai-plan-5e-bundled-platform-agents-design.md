# AI Module — Plan 5e: Bundled Platform Agents (Design)

Status: **Spec / pre-plan**
Sequence: follows 5d-2 (Safety + Content Moderation, shipped as PR #30), precedes 5f (Admin AI Settings backend).
Branch: `feature/ai-phase-5e`.
Size: **S**.

---

## 1. Purpose

Plan 5e ships the four bundled platform agents the [revised AI vision](./2026-04-23-ai-module-vision-revised-design.md) row 205 commits to:

1. **Platform Insights Agent** — read-only Q&A over tenant data (users, audit log, billing usage). Two provider variants (Anthropic + OpenAI) so the boilerplate exercises both providers from day one.
2. **Support Copilot** — pure-prose helper for boilerplate features ("how do I configure X").
3. **Teacher Tutor** — flagship School SaaS starter; targets the new `student` persona; ships with explicit `ChildSafe` safety override (per the [5d-2 forward-link](./2026-04-27-ai-plan-5d-2-safety-moderation-design.md#5e-bundled-platform-agents)).
4. **Brand Content Agent** — flagship Social SaaS starter; targets the new `editor` persona; ships with explicit `Standard` safety override.

Five templates total (Platform Insights × 2 variants). Each is a pure-data class implementing `IAiAgentTemplate`, materialised through the existing 5c-2 `InstallTemplateCommand` flow. The plan also closes three loose ends that 5d-2 left dangling:

- **Make `SafetyPresetHint` load-bearing.** 5c-2 added the field with the explicit comment *"becomes load-bearing when Plan 5d hoists safety onto the assistant"*. 5d-2 hoisted safety but never wired the install path. This plan finishes that wiring.
- **Seed the six flagship demo personas** (`student`, `teacher`, `parent`, `editor`, `approver`, `client`) on every tenant. Today only `anonymous` and `default` are seeded — neither flagship template can install without this.
- **Decorate three read-only queries with `[AiTool]`** so Platform Insights can deliver on its "users / audit log / billing usage" pitch instead of being a `list_users`-only toy.

### Out of scope

- **Flagship `[AiTool]` commands** — `Teacher Tutor`'s "generate quiz" and `Brand Content`'s "draft caption" tools belong to Plan 8b inline AI. Both flagship templates ship chat-only.
- **Flagship modules** (`Starter.Module.School`, `Starter.Module.Social`) — deferred to flagship-product plans. All five templates live in core for now.
- **KB attachment** — 5c-2 design rule §29 stands: templates carry no `KnowledgeBaseDocIds`. Admins attach KB after install.
- **Frontend template browser** — Plan 7a.
- **Tenant-level safety preset default** — Plan 5f.
- **Operational / scheduled triggers** — Plan 8c.
- **Cross-tenant Platform Insights** — already covered by existing `IsInRole(SuperAdmin)` checks + `IgnoreQueryFilters` in tool handlers; no new code in 5e.

---

## 2. Why now

5d-2 closed the moderation pipeline and committed in §11.3 *"the `Teacher Tutor` and `Brand Content Agent` starter templates ship with explicit `SafetyPresetOverride` set in the template definition"* — that commitment lands here. The vision's flagship acid tests for 5e (row 251) are *"Teacher Tutor starter ready to fork per grade"* and *"Brand Content starter ready to fork per client"*. Both depend on the personas being seeded and the safety override being applied — this plan is the smallest unit that delivers both.

Platform Insights and Support Copilot are also part of row 205 ("`Platform Insights Agent` + `Support Copilot` as before"). The "as before" refers to the original vision (`2026-04-21`) which described Platform Insights as *"read-only Q&A over tenant data (users, audit log, billing usage)"*. Today's `[AiTool]` catalog has only `list_users`, `list_products`, `list_conversations` — without the audit + billing additions in this plan, Platform Insights cannot live up to its name.

---

## 3. Existing constraints honoured

- **`IAiAgentTemplate` shape** (5c-2). Pure-data interface; parameterless constructor; per-module assembly scan in `Starter.Application.DependencyInjection` and module DI.
- **`InstallTemplateCommand` flow** (5c-2 §3.5–§3.6). Tenant resolution + cross-tenant guard, persona/tool validation, slug + name uniqueness, principal pairing (5d-1), cost-cap pre-check (5d-1).
- **`AiAssistant.SetSafetyPreset(SafetyPreset?)`** (5d-2). Existing public method on the aggregate; the only legitimate way to set the override.
- **`SeedTenantPersonasDomainEventHandler`** (5b). Idempotent on `TenantCreatedEvent`; we extend, not replace.
- **`AIModule.SeedDataAsync`** ordering. Persona backfill must run *before* template install or persona-target validation will fail.
- **5c-2 design rule §29.** No KB bundling.
- **Memory rule** (`feedback_no_migrations.md`). No EF migrations are committed in the boilerplate; no schema changes are introduced anyway.

---

## 4. The five templates

All five are pure-data classes in `boilerplateBE/src/Starter.Application/Features/Ai/Templates/`. System prompts live in sibling `*Prompts` static-class files mirroring the existing `SupportAssistantPrompts.cs` pattern.

| Slug | Class | Provider / Model | `Category` | Persona target | `SafetyPresetOverride` | Tools |
|---|---|---|---|---|---|---|
| `platform_insights_anthropic` | `PlatformInsightsAnthropicTemplate` | Anthropic / `claude-sonnet-4-20250514` | `"Platform"` | `default` | `null` (inherit) | `list_users`, `list_audit_logs`, `list_subscriptions`, `list_usage`, `list_conversations` |
| `platform_insights_openai` | `PlatformInsightsOpenAiTemplate` | OpenAI / `gpt-4o-mini` | `"Platform"` | `default` | `null` (inherit) | (same five) |
| `support_copilot` | `SupportCopilotTemplate` | Anthropic / `claude-sonnet-4-20250514` | `"Platform"` | `default` | `null` (inherit) | (none) |
| `teacher_tutor` | `TeacherTutorTemplate` | Anthropic / `claude-sonnet-4-20250514` | `"Education"` | `student` | `SafetyPreset.ChildSafe` | (none) |
| `brand_content` | `BrandContentTemplate` | Anthropic / `claude-sonnet-4-20250514` | `"Content"` | `editor` | `SafetyPreset.Standard` | (none) |

Common settings: `MaxTokens = 2048`, `ExecutionMode = AssistantExecutionMode.Chat`. Per-template `Temperature`:

| Template | Temperature | Reason |
|---|---|---|
| `platform_insights_*` | `0.3` | Factual Q&A; matches 5c-2 convention. |
| `support_copilot` | `0.3` | Factual feature Q&A; deterministic answers preferred. |
| `teacher_tutor` | `0.5` | Slight creativity for Socratic prompts and rephrasings, but still grounded. |
| `brand_content` | `0.8` | Creative copywriting; encourages voice variety and divergent drafts. |

### 4.1 Prompt files

Four new sibling helpers (Platform Insights variants share one):

- `PlatformInsightsPrompts.cs` — shared `SystemPrompt` and `Description` consts. The system prompt covers the agent's read-only stance (never mutates), enumerates the five tools by purpose, and explicitly tells the model to refuse cross-tenant questions when called by a non-superadmin (the tools enforce this; the prompt makes it observable to the user via a polite refusal).
- `SupportCopilotPrompts.cs` — covers the boilerplate's feature surface (auth, RBAC, tenancy, billing, webhooks, audit logs, AI module) and tells the model to point readers to the actual UI/CLI commands rather than fabricating internal APIs.
- `TeacherTutorPrompts.cs` — Socratic tutor stance, age-appropriate language, explicit "do not provide finished homework answers; guide the student step by step" rule.
- `BrandContentPrompts.cs` — copywriter stance, asks for brand voice + audience + format up front, produces drafts the editor can iterate on, refuses to invent product facts.

### 4.2 Provider rationale

Anthropic is the default for `support_copilot`, `teacher_tutor`, and `brand_content`: long-context Q&A, long-form pedagogy, and creative writing all favour Claude Sonnet 4. Platform Insights ships in both providers so the boilerplate proves end-to-end provider parity — the vision Pillar 1 ("System-wide AI") demands portability, and 5g (Gemini) will need a third variant landing the same way.

---

## 5. Six flagship demo personas

`AiPersona` gains six new factory methods next to the existing `CreateAnonymous` / `CreateDefault`. All have `IsSystemReserved = false` (tenants can edit / delete them), `IsActive = true`, and `Description` summarising the persona's role.

| Slug | Audience | Safety preset | Factory method |
|---|---|---|---|
| `student` | `Internal` | `ChildSafe` | `AiPersona.CreateStudent(tenantId, createdByUserId)` |
| `teacher` | `Internal` | `Standard` | `AiPersona.CreateTeacher(...)` |
| `parent` | `EndCustomer` | `Standard` | `AiPersona.CreateParent(...)` |
| `editor` | `Internal` | `Standard` | `AiPersona.CreateEditor(...)` |
| `approver` | `Internal` | `Standard` | `AiPersona.CreateApprover(...)` |
| `client` | `EndCustomer` | `ProfessionalModerated` | `AiPersona.CreateClient(...)` |

The safety presets on `student` and `client` mirror the vision tables (school: "Student gets ChildSafe filter"; social: "Client | EndCustomer | ProfessionalModerated"). The remaining four default to `Standard` — explicit overrides only where the vision requires.

Slug constants added next to existing `AnonymousSlug` / `DefaultSlug`:

```csharp
public const string StudentSlug  = "student";
public const string TeacherSlug  = "teacher";
public const string ParentSlug   = "parent";
public const string EditorSlug   = "editor";
public const string ApproverSlug = "approver";
public const string ClientSlug   = "client";
```

### 5.1 New-tenant seed

`SeedTenantPersonasDomainEventHandler.Handle` is extended to query for all eight slugs and add any missing. Idempotent — same shape as today.

### 5.2 Backfill for existing tenants

New static seed `FlagshipPersonasBackfillSeed.SeedAsync(AiDbContext db, IApplicationDbContext appDb, CancellationToken ct)`:

1. Load every tenant id (via `appDb.Tenants.IgnoreQueryFilters()`).
2. For each tenant, load existing persona slugs and add only the missing ones from the six new slugs.
3. Single `SaveChangesAsync` per tenant. Idempotent.
4. Uses `SystemSeedActor = Guid.Empty` (matches `SeedTenantPersonasDomainEventHandler`).

Invoked from `AIModule.SeedDataAsync` after `SafetyPresetProfileSeed.SeedAsync` and before any template-install loop.

---

## 6. `IAiAgentTemplate.SafetyPresetOverride` rename + wiring

### 6.1 Interface change

`Starter.Abstractions/Capabilities/IAiAgentTemplate.cs`:

```csharp
- /// <summary>
- /// Recommended safety preset. Today persona-level safety still applies at runtime;
- /// this field becomes load-bearing when Plan 5d hoists safety onto the assistant.
- /// </summary>
- SafetyPreset? SafetyPresetHint { get; }
+ /// <summary>
+ /// Persisted to <c>AiAssistant.SafetyPresetOverride</c> on install.
+ /// <c>null</c> means "inherit from the resolved persona's safety preset at runtime".
+ /// </summary>
+ SafetyPreset? SafetyPresetOverride { get; }
```

### 6.2 Install handler delta

`InstallTemplateCommandHandler.Handle` — new step **8.5** between "Create assistant" (step 8) and "Stamp provenance" (step 9):

```csharp
// 8.5 Apply template safety override (5e). When null, the assistant
// inherits the resolved persona's safety preset at runtime.
if (template.SafetyPresetOverride.HasValue)
    assistant.SetSafetyPreset(template.SafetyPresetOverride);
```

The `null`-skip is intentional: `AiAssistant.SetSafetyPreset(null)` is a no-op on a freshly created assistant (`SafetyPresetOverride` defaults to `null`), but skipping the call avoids a domain event with `preset == null` if `SetSafetyPreset` ever raises one. Defensive — keeps the handler honest if `SetSafetyPreset` evolves.

### 6.3 Existing 5c-2 templates

All four 5c-2 templates change `Standard` → `null`:

- `SupportAssistantAnthropicTemplate.cs` — `SafetyPresetHint => SafetyPreset.Standard;` → `SafetyPresetOverride => null;`
- `SupportAssistantOpenAiTemplate.cs` — same change.
- `ProductExpertAnthropicTemplate.cs` — same change.
- `ProductExpertOpenAiTemplate.cs` — same change.

Behavior at runtime is unchanged: the seeded `default` persona has `SafetyPreset = Standard`, so a freshly installed `support_assistant_anthropic` resolves to Standard either way. The override row staying `null` is more honest — it expresses *"no explicit override; inherit"* instead of *"explicit override that happens to match the inherited value"*.

Existing test apps that already installed these templates have `SafetyPresetOverride == null` on disk (since 5c-2 never wired the field). No data migration required.

### 6.4 5c-2 design doc pointer

Append a "Superseded by 5e" note to §3.1 of `2026-04-25-ai-plan-5c-2-agent-templates-design.md` linking to this design and noting that `SafetyPresetHint` was renamed and wired in 5e.

---

## 7. New `[AiTool]` decorations

Three pure attribute additions on existing read-only MediatR queries. No behavioural changes; the queries already enforce tenant scoping and call the right authorization handlers.

### 7.1 `list_audit_logs`

`boilerplateBE/src/Starter.Application/Features/AuditLogs/Queries/GetAuditLogs/GetAuditLogsQuery.cs`:

```csharp
[AiTool(
    Name = "list_audit_logs",
    Description = "List audit-log entries for the current tenant. Supports filtering by entity, action, user, and date range. Read-only.",
    Category = "Audit",
    RequiredPermission = Permissions.System.ViewAuditLogs)]
public sealed record GetAuditLogsQuery(...) : IRequest<Result<PagedResult<AuditLogDto>>>;
```

### 7.2 `list_subscriptions`

`boilerplateBE/src/modules/Starter.Module.Billing/Application/Queries/GetAllSubscriptions/GetAllSubscriptionsQuery.cs`:

```csharp
[AiTool(
    Name = "list_subscriptions",
    Description = "List active and past tenant subscriptions, including plan name, status, and renewal date. Read-only.",
    Category = "Billing",
    RequiredPermission = BillingPermissions.View)]
```

### 7.3 `list_usage`

`boilerplateBE/src/modules/Starter.Module.Billing/Application/Queries/GetUsage/GetUsageQuery.cs`:

```csharp
[AiTool(
    Name = "list_usage",
    Description = "Report the current tenant's usage records (requests, storage, AI tokens) for a date range. Read-only.",
    Category = "Billing",
    RequiredPermission = BillingPermissions.View)]
```

### 7.4 Pre-flight on shape compliance

5c-1's discovery requires the request type to be `IBaseRequest` and the result to be JSON-serialisable. All three queries already return public DTOs that round-trip through controllers (`AuditLogDto`, `SubscriptionDto`, `UsageReportDto`) — safe. The `RequiredPermission` strings already exist (`Permissions.System.ViewAuditLogs`, `BillingPermissions.View`) — no new permissions added.

---

## 8. Startup install — single flag, all templates

The existing `InstallDemoTemplatesOnStartup` loop in `AIModule.SeedDataAsync` already iterates over **every** template in `IAiAgentTemplateRegistry.GetAll()`. Once the five 5e templates are registered by the existing per-module discovery scan, the existing flag installs them automatically — no new flag needed.

This is simpler than the two-flag design (no new config key, no new loop block, no slug-list filtering) and matches the natural shape of the existing code. A fresh dev tenant gets nine assistants installed (four 5c-2 demos + five 5e bundled); admins delete what they don't want post-install. Production default for `InstallDemoTemplatesOnStartup` remains `false`, so prod isn't affected.

### 8.1 No config changes

`appsettings.json` and `appsettings.Development.json` are not touched in 5e.

### 8.2 `AIModule.SeedDataAsync` order of operations

1. `AiRoleMetadataSeed.SeedAsync` (existing)
2. `ModelPricingSeed.SeedAsync` (existing)
3. `SafetyPresetProfileSeed.SeedAsync` (5d-2)
4. **`FlagshipPersonasBackfillSeed.SeedAsync` (NEW — 5e)** — must run before the install loop or `teacher_tutor` / `brand_content` will fail persona-target validation.
5. `AgentPrincipalBackfill.RunAsync` (5d-1)
6. Existing template-install loop (5c-2; gated by `InstallDemoTemplatesOnStartup`) — now installs all nine templates.

The persona backfill must run unconditionally (outside the flag-gated block) so that `TenantCreatedEvent`-driven flows that don't go through the demo-install path still benefit from the new personas.

---

## 9. Acid + unit tests

### 9.1 Plan 5e acid tests (`Plan5eAcidTests.cs`)

In `boilerplateBE/tests/Starter.Api.Tests/Ai/AcidTests/`. Use the existing `Plan5d2TestRuntimeBuilder` shape so fixtures are consistent.

| Test | Validates |
|---|---|
| `AllFiveTemplatesAreDiscovered` | `IAiAgentTemplateRegistry.GetAll()` exposes the five new slugs in addition to the four 5c-2 demo templates. |
| `TeacherTutorInstallsWithExplicitChildSafe` | `InstallTemplateCommand{ TemplateSlug = "teacher_tutor" }` → `AiAssistant.SafetyPresetOverride == SafetyPreset.ChildSafe`. |
| `BrandContentInstallsWithExplicitStandard` | `InstallTemplateCommand{ TemplateSlug = "brand_content" }` → `AiAssistant.SafetyPresetOverride == SafetyPreset.Standard`. |
| `PlatformInsightsInheritsFromDefaultPersona` | `InstallTemplateCommand{ TemplateSlug = "platform_insights_anthropic" }` → `SafetyPresetOverride == null`. Then the safety profile resolver returns `Standard` (the `default` persona's preset). |
| `Existing5c2TemplatesNowInheritFromPersona` | After installing each of `support_assistant_anthropic/openai` and `product_expert_anthropic/openai`, `SafetyPresetOverride == null` for all four. |
| `NewTenantGetsAllEightPersonas` | Raise `TenantCreatedEvent`; query `AiPersonas` for that tenant; assert exactly 8 rows with the expected slugs and safety presets. |
| `BackfillSeedAddsMissingPersonasToOldTenant` | Seed a tenant with only `anonymous` + `default`; run `FlagshipPersonasBackfillSeed.SeedAsync`; assert all 8 personas now exist; assert idempotency by running it twice. |
| `PlatformInsightsAgentCanCallAuditLogTool` | Install `platform_insights_anthropic`; submit a chat completion that elicits a `list_audit_logs` tool call; assert the dispatcher invokes the audit-log tool and the response surfaces results. (Uses the existing `AllowAllModeration` + fake-runtime fixture.) |
| `InstallFlagOff_No5eTemplatesAutoInstalled` | Set `AI:InstallDemoTemplatesOnStartup = false`; run `SeedDataAsync`; assert no 5e assistants exist (and no 5c-2 demos either — same flag). |
| `InstallFlagOn_FreshTenantHasAllNineAssistants` | Flag true; assert all nine boilerplate assistants (4 demos + 5 bundled) installed for every existing tenant. |

### 9.2 Unit tests

- `IAiAgentTemplateSafetyPresetOverrideTests` — covers the rename + serialization (registry handles both null and explicit values).
- `InstallTemplateCommandHandlerSafetyOverrideTests` — covers step 8.5: `null` template override → assistant override stays `null`; explicit value → `SetSafetyPreset` called with the value.
- `SeedTenantPersonasDomainEventHandlerExtensionTests` — covers the six-new-personas seed and idempotency.
- `FlagshipPersonasBackfillSeedTests` — covers the backfill across multiple tenants and idempotency.
- `AiToolDiscoveryNewToolsTests` — asserts the three new tool names are discovered by the existing `[AiTool]` assembly scan.

### 9.3 Existing tests to update

- `InstallDemoTemplatesSeedTests` — the existing 5c-2 templates' `SafetyPresetHint` became `SafetyPresetOverride` and changed value `Standard` → `null`. Any assertion on the field name needs updating.
- `AiAgentTemplateMappersTests` — DTO field rename if `AiAgentTemplateDto.SafetyPresetHint` exists.
- Any test that expected `AiAssistant.SafetyPresetOverride == Standard` after installing a 5c-2 demo template now expects `null`.

---

## 10. Files touched (summary)

### New files (≈14)

| Path | Purpose |
|---|---|
| `boilerplateBE/src/Starter.Application/Features/Ai/Templates/PlatformInsightsAnthropicTemplate.cs` | Template class |
| `boilerplateBE/src/Starter.Application/Features/Ai/Templates/PlatformInsightsOpenAiTemplate.cs` | Template class |
| `boilerplateBE/src/Starter.Application/Features/Ai/Templates/PlatformInsightsPrompts.cs` | Shared prompt + description |
| `boilerplateBE/src/Starter.Application/Features/Ai/Templates/SupportCopilotTemplate.cs` | Template class |
| `boilerplateBE/src/Starter.Application/Features/Ai/Templates/SupportCopilotPrompts.cs` | Prompt + description |
| `boilerplateBE/src/Starter.Application/Features/Ai/Templates/TeacherTutorTemplate.cs` | Template class |
| `boilerplateBE/src/Starter.Application/Features/Ai/Templates/TeacherTutorPrompts.cs` | Prompt + description |
| `boilerplateBE/src/Starter.Application/Features/Ai/Templates/BrandContentTemplate.cs` | Template class |
| `boilerplateBE/src/Starter.Application/Features/Ai/Templates/BrandContentPrompts.cs` | Prompt + description |
| `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Persistence/Seed/FlagshipPersonasBackfillSeed.cs` | Backfill seed |
| `boilerplateBE/tests/Starter.Api.Tests/Ai/AcidTests/Plan5eAcidTests.cs` | Acid tests |
| `boilerplateBE/tests/Starter.Api.Tests/Ai/Templates/InstallTemplateCommandHandlerSafetyOverrideTests.cs` | Handler unit tests |
| `boilerplateBE/tests/Starter.Api.Tests/Ai/Personas/SeedTenantPersonasDomainEventHandlerExtensionTests.cs` | Seed unit tests |
| `boilerplateBE/tests/Starter.Api.Tests/Ai/Personas/FlagshipPersonasBackfillSeedTests.cs` | Backfill unit tests |

### Modified files (≈12)

| Path | Change |
|---|---|
| `boilerplateBE/src/Starter.Abstractions/Capabilities/IAiAgentTemplate.cs` | Rename `SafetyPresetHint` → `SafetyPresetOverride` + doc-comment. |
| `boilerplateBE/src/modules/Starter.Module.AI/Application/Commands/InstallTemplate/InstallTemplateCommandHandler.cs` | Add step 8.5 — apply template safety override. |
| `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/EventHandlers/SeedTenantPersonasDomainEventHandler.cs` | Seed six new personas. |
| `boilerplateBE/src/modules/Starter.Module.AI/Domain/Entities/AiPersona.cs` | Six new factory methods + slug constants. |
| `boilerplateBE/src/Starter.Application/Features/Ai/Templates/SupportAssistantAnthropicTemplate.cs` | `Standard` → `null`; field rename. |
| `boilerplateBE/src/Starter.Application/Features/Ai/Templates/SupportAssistantOpenAiTemplate.cs` | Same. |
| `boilerplateBE/src/modules/Starter.Module.Products/Application/Templates/ProductExpertAnthropicTemplate.cs` | Same. |
| `boilerplateBE/src/modules/Starter.Module.Products/Application/Templates/ProductExpertOpenAiTemplate.cs` | Same. |
| `boilerplateBE/src/modules/Starter.Module.AI/AIModule.cs` | Wire backfill seed + new install loop. |
| `boilerplateBE/src/modules/Starter.Module.AI/Application/DTOs/AiAgentTemplateDto.cs` + mappers | Rename DTO field if present (e.g. `SafetyPresetHint` → `SafetyPresetOverride`). |
| `boilerplateBE/src/Starter.Application/Features/AuditLogs/Queries/GetAuditLogs/GetAuditLogsQuery.cs` | `[AiTool]` attribute. |
| `boilerplateBE/src/modules/Starter.Module.Billing/Application/Queries/GetAllSubscriptions/GetAllSubscriptionsQuery.cs` | `[AiTool]` attribute. |
| `boilerplateBE/src/modules/Starter.Module.Billing/Application/Queries/GetUsage/GetUsageQuery.cs` | `[AiTool]` attribute. |
| `docs/superpowers/specs/2026-04-25-ai-plan-5c-2-agent-templates-design.md` | "Superseded by 5e" pointer in §3.1. |

(Test-file updates per §9.3 not double-counted here.)

---

## 11. Acid tests against the vision

Vision row 251 — *"Teacher Tutor starter ready to fork per grade | Brand Content starter ready to fork per client"*.

Both pass once 5e ships:

- **Teacher Tutor "ready to fork per grade":** install `teacher_tutor` into a tenant → assistant exists with `student` persona target + `ChildSafe` override + Anthropic prompt. An admin clones via existing `UpdateAssistantCommand` (e.g. customising the system prompt to "Grade 5 Arabic Tutor"). Forking is plain template-then-update; nothing new in 5e.
- **Brand Content "ready to fork per client":** same flow with `brand_content` + `editor` persona. The `Standard` override is explicit per the 5d-2 commitment; persona-default is also `Standard`, so the runtime semantics are identical to inheriting — but the explicit value is the visible commitment in the template definition.

Both flagship acid tests slot neatly under existing tenants once `FlagshipPersonasBackfillSeed` has run.

---

## 12. Forward links

- **5f (Admin AI Settings backend)** — surfaces tenant-level safety preset defaults, brand/persona config, model selection per agent class. The personas seeded here are the data 5f's brand-config UI binds to.
- **6 (Chat Sidebar UI)** — renders the 5e assistants in the chat sidebar's assistant selector; `Platform Insights Agent` shows up for admins, `Teacher Tutor` for student-persona users.
- **7a (Admin Templates browser)** — lists all nine templates (4 from 5c-2 + 5 from 5e), grouped by `Category`, with one-click install.
- **8b (Inline AI)** — `Teacher Tutor` and `Brand Content` get their first `[AiTool]` commands ("generate quiz", "draft caption"). The flagship templates as-shipped here are chat-only; 8b adds tool calls.
- **8c (Operational agents)** — none of the 5e templates are scheduled / event-triggered. 8c adds `Student Progress Monitor` and `Content Scheduler` as separate operational-agent definitions.
- **5g (Gemini)** — the next provider lands as `platform_insights_gemini` template variant alongside the 5e ones, exercising the same install path.

---

## 13. Risks and mitigations

| Risk | Mitigation |
|---|---|
| Renaming `SafetyPresetHint` → `SafetyPresetOverride` breaks downstream consumers (DTOs, FE types). | Low blast radius — the field is only consumed inside the AI module and its tests. The grep for `SafetyPresetHint` returns only the four 5c-2 template files plus `IAiAgentTemplate.cs` and the mapper. The FE surface for templates lands in 7a. |
| Auto-install on startup creates noise (a fresh dev tenant gets 9 boilerplate assistants). | Single flag remains opt-in (`false` in prod, `true` in dev). Admins delete unwanted assistants post-install. The benefit of one flag outweighs the noise. |
| Backfill seed runs against every tenant on every startup. | Idempotent — query existing slugs first, skip tenants already complete. The query is per-tenant and indexed by `(TenantId, Slug)`. Cost is negligible at boilerplate scale. |
| Adding `[AiTool]` to `GetAllSubscriptionsQuery` exposes billing data to AI calls. | Already gated by `BillingPermissions.View`; the user dispatching the tool must hold the permission. Same model as the existing `list_users` tool. |
| `Teacher Tutor` chat with no tools is a thin acid-test demo. | This is intentional — the flagship-tool authoring is Plan 8b. The acid test is *"ready to fork per grade"*, which is a configuration story, not a tool story. |

---

## 14. Sequencing inside the implementation plan

(The implementation plan will detail this; capturing the natural order here for the writing-plans hand-off.)

1. **Foundation** — rename `SafetyPresetHint` → `SafetyPresetOverride` on the interface; update existing 5c-2 templates and their tests; wire step 8.5 in `InstallTemplateCommandHandler`. Build green.
2. **Personas** — extend `AiPersona` with six factory methods; extend `SeedTenantPersonasDomainEventHandler`; write `FlagshipPersonasBackfillSeed`; wire it into `AIModule.SeedDataAsync`. Build green; persona unit tests pass.
3. **Tools** — add three `[AiTool]` attributes; assert discovery via the existing scan tests.
4. **Templates** — write the five new template + prompt files; verify discovery test passes.
5. **Wire seed order** — insert `FlagshipPersonasBackfillSeed.SeedAsync` into the always-on block of `AIModule.SeedDataAsync` (before the existing template-install loop). No new flag.
6. **Acid tests** — write `Plan5eAcidTests.cs` last; require steps 1–5 to be in place.
7. **Documentation** — append "Superseded by 5e" pointer to 5c-2 design doc.

The plan is small enough (S) that all seven sub-steps land in a single PR. No staged rollout needed.
