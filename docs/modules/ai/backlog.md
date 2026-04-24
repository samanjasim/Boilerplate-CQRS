# AI Module — Deferred Work Backlog

Items deliberately left out of completed plans, tracked here so future plans can pick them up explicitly. Each item names the plan that deferred it, the rationale, and the likely landing spot.

When an item ships, **delete it from this file** in the same PR — the file should always reflect *currently outstanding* deferrals, not historical ones (git history retains the audit trail).

Add new items at the bottom of the relevant section as plans ship.

---

## From Plan 5c-2 — Agent Templates

Deferred 2026-04-25. Spec: [`docs/superpowers/specs/2026-04-25-ai-plan-5c-2-agent-templates-design.md`](../../superpowers/specs/2026-04-25-ai-plan-5c-2-agent-templates-design.md).

### KB bundling in templates
**What:** Templates today carry no `KnowledgeBaseDocIds`. A template author cannot ship "this preset comes with these documents."
**Why deferred:** KB-as-part-of-template needs a document seed format (slug-match, name-match, or embedded-content), an install-time materialisation path (creating `AiDocument` rows + running ingest), and failure handling for partial-ingest mid-install. Out of scope for the template primitive itself.
**Likely landing:** Plan 5c-3 (or whichever plan first has a flagship RAG agent driving real requirements).
**Pickup hooks already in place:** None — `IAiAgentTemplate` adds the property when needed.

### Auto-install on tenant creation
**What:** Mark a template as "install-on-tenant-create" so every new tenant gets it without admin action.
**Why deferred:** YAGNI for 5c-2 — every demo template install is admin-initiated (or driven by the dev-only `AI:InstallDemoTemplatesOnStartup` flag).
**Likely landing:** Pulled in when a flagship plan needs a baseline assistant on every new tenant (e.g., a default Support Bot in the social SaaS reference app).
**Pickup hooks already in place:** `SeedTenantPersonasDomainEventHandler` provides the pattern — a sibling `InstallTenantTemplatesOnTenantCreatedEventHandler` would consume a new `IAiAgentTemplate.AutoInstallOnTenantCreate` boolean.

### Inline overrides at install time
**What:** `InstallTemplateCommand` today copies the template verbatim. Admin must edit afterward to customise. Future option: `InstallTemplateCommand(slug, overrides: { Name?, Description?, PersonaTargetSlugs?, EnabledToolNames?, ... })`.
**Why deferred:** Validation edge cases multiply (override persona that doesn't exist? override tool not in registry?). Edit-after-install via the existing `UpdateAssistantCommand` covers the same ground without a wider surface.
**Likely landing:** When a UX wizard (Plan 7a or later) genuinely needs single-shot install-with-customisation.

### Multiple installs of same template into same tenant
**What:** Today, a second install of the same template into the same tenant fails with `AlreadyInstalled` (slug collision on `AiAssistant`).
**Why deferred:** Use case is unclear — most templates are "one preset per tenant." If admin needs a second variant, they can install once + edit + clone via existing assistant tools.
**Likely landing:** Add `AssistantSlug?` override param to `InstallTemplateCommand` if/when a real use case surfaces.

### Template versioning + "update available" indicator
**What:** `AiAssistant.TemplateSourceVersion` is reserved-nullable today; never written. Future feature: bump a `Version` field on `IAiAgentTemplate`, record it on install, surface "update available" when the live template's version is newer than the installed version.
**Why deferred:** Wholly separate UX problem (diff visualisation, merge semantics, partial-update flow). No demand yet.
**Likely landing:** Plan 5c-3 or later.
**Pickup hooks already in place:** `TemplateSourceVersion` column already exists in 5c-2; populating it is a one-line change in `InstallTemplateCommandHandler`.

### Reset-to-template-defaults
**What:** Endpoint that reverts an installed assistant back to its source template's values. Requires looking up the template by `TemplateSourceSlug`, overwriting fields, and deciding what to do with admin-edited customisations.
**Why deferred:** No use case yet. Provenance column (`TemplateSourceSlug`) is in place to enable it later.
**Likely landing:** When the template-update feature lands and users need a "discard local changes" escape hatch.

### Uninstall endpoint
**What:** Dedicated `DELETE /api/v1/ai/templates/{slug}/uninstall` that finds and deletes the assistant installed from this template.
**Why deferred:** Existing `DeleteAssistantCommand` already does this — the assistant is just an `AiAssistant` row with `TemplateSourceSlug` set. No need for a duplicate verb.
**Likely landing:** Probably never. Keep `DeleteAssistantCommand` as the single deletion path.

### Assistant-level safety preset enforcement
**What:** `IAiAgentTemplate.SafetyPresetHint` is documentation-level today. The runtime still uses persona-level safety. When safety hoists onto `AiAssistant`, install can write the hint to a real `AiAssistant.SafetyPreset` field.
**Why deferred:** This is Plan 5d's whole job — agent-level safety, cost caps, dangerous-action pause, content moderation.
**Likely landing:** Plan 5d.
**Pickup hooks already in place:** `SafetyPresetHint` flows through the install handler without enforcement; 5d wires it.

### Flagship acid-test templates
**What:** Vision doc names two flagship acid tests for Plan 5c — School Tutor (targeting Student persona) and Social Brand Content (targeting Editor persona).
**Why deferred:** The flagship modules themselves (`Starter.Module.School`, `Starter.Module.Social`) don't exist yet. 5c-2 ships boilerplate demo templates (Support Assistant, Product Expert) to prove the abstraction; flagship acid tests re-run when those product plans land.
**Likely landing:** Whichever plans build out the school SaaS and social SaaS reference products (post-5g per the current roadmap).

### Frontend admin UI for templates
**What:** React pages to browse the template catalog and install with one click.
**Why deferred:** 5c-2 is backend-only by design. Frontend admin work is consolidated in Plan 7a.
**Likely landing:** Plan 7a.
**Pickup hooks already in place:** API surface (`GET /ai/templates`, `POST /ai/templates/{slug}/install`) and `AiAgentTemplateDto` are in place.
