# AI Module — Revised Vision and Roadmap (2026-04-23)

Supersedes `2026-04-21-ai-module-vision-and-roadmap.md`. All locked decisions in that document are inherited, updated, or explicitly replaced here. The original document remains as historical context.

---

## Purpose

Define the strategic target, architectural primitives, and plan sequence for the AI module — updated to reflect two flagship reference products and the new primitives they require.

---

## Target

**C — Hybrid: boilerplate + flagship reference products.**

The boilerplate ships horizontal AI infrastructure that any vertical can compose on top of. Two flagship reference products ship with the boilerplate and prove every primitive against real requirements:

- **School SaaS** — Teacher, Student, Parent personas; Tutor agent with ChildSafe moderation; Student Progress Monitor operational agent; curriculum RAG.
- **Social media SaaS** — Editor, Approver, Client personas; Brand Content Agent with per-tenant brand voice; Content Scheduler operational agent; approval workflows.

These are chosen because they stress different axes: school demands per-user-category agents, child safety, and long-context tutoring; social media demands brand/persona config, external tool integrations, and approval workflows. Together they validate generality. Neither is a toy.

---

## The Four Pillars

Inherited unchanged from the original vision.

### Pillar 1 — Tenant- and superadmin-authored agents
Tenants create, customise, and manage agents via the admin UI without developer help. Developers ship templates as a starting point.

### Pillar 2 — Domain-operational agents
Agents that have live access to system state, execute domain actions, respond to events, and run on schedules — acting as autonomous employees within a tenant's operational context.

### Pillar 3 — System-wide AI with permissions
AI surfaces (insights, actions, automations) embedded inline across domain pages, controlled by the same permission and persona system as the rest of the platform.

### Pillar 4 — AI-first products
The boilerplate enables downstream developers to build AI-first SaaS products on top of it with minimal integration effort.

---

## New Architectural Primitives

Four first-class concepts added in this revision. All downstream plans depend on these.

### Primitive 1 — Role × Persona (two-axis identity)

**Role** answers *what you can do* (CRUD permissions, unchanged from today).

**Persona** answers *what AI experience you get*. A named profile carrying:
- Display name + description
- Audience type: `Internal` (tenant staff), `EndCustomer` (parents, clients, students with accounts), `Anonymous` (public/guest, no auth required)
- Safety preset: `Standard`, `ChildSafe`, `ProfessionalModerated` — governs both input screening and output filtering for agents targeting this persona
- Permitted agent slugs (empty list = all agents permitted for this persona)

Tenants define their own personas. A user is assigned one or more personas. An agent targets one or more personas. Role and Persona are orthogonal — changing a persona never affects permissions and vice versa.

The `anonymous` persona is system-reserved and always available. Tenants configure it; they do not create it.

**School tenant example:**

| Persona | Audience type | Safety preset | Agents |
|---|---|---|---|
| Teacher | Internal | Standard | Lesson Planner, Grading Assistant, Admin Copilot |
| Student | Internal | ChildSafe | Tutor, Reading Coach |
| Parent | EndCustomer | Standard | Progress Reporter |

**Social media tenant example:**

| Persona | Audience type | Safety preset | Agents |
|---|---|---|---|
| Editor | Internal | Standard | Brand Content Agent, Copy Reviewer |
| Approver | Internal | Standard | Draft Reviewer |
| Client | EndCustomer | ProfessionalModerated | Campaign Previewer |

### Primitive 2 — Tiered agent authorship

Three creation paths sharing one `AiAgentDefinition` record shape and one runtime code path. Safety, cost, and persona enforcement applies at the record level regardless of origin.

```
Module code  → registers Template
                        ↓
Admin UI     → installs Template → optionally forks → Custom Agent
Admin UI     → creates from scratch             → Custom Agent
                (constrained to entitled tools / KBs / models)
```

An `AiAgentDefinition` carries: `Slug`, `DisplayName`, `Persona[]`, `ModelConfig`, `SystemPrompt`, `Tools[]`, `KnowledgeBases[]`, `SafetyPreset`, `CostCap`, `TriggerConfig`, `TemplateSlug?` (null if from scratch).

### Primitive 3 — End-customer surface architecture (foundation now, UI later)

The architecture accommodates end-customer and public surfaces without shipping UI immediately.

**Built into the foundation:**
- `anonymous` persona is system-reserved in the persona model
- All agent-resolution and chat APIs accept a persona context, not just an authenticated user
- Public-scoped API endpoints (separate route prefix, no `[Authorize]`, rate-limited by origin + widget API key) designed in Plan 8f
- Per-widget API keys (issued by tenant admin, origin-pinned) designed in Plan 5f

**Deferred to dedicated phases:**
- Public frontend routes (`/:tenantSlug/parent`, `/:tenantSlug/client/:token`)
- Embeddable `<script>` widget
- Mobile persona-aware end-customer surfaces

### Primitive 4 — Operational Agent (named concept)

An **Operational Agent** is a named agent with:
- A service-account identity
- Access to tenant's live system state via the tool catalog
- A configured trigger (domain event / cron schedule / user-invoked)
- Multi-step reasoning capability (observe → reason → act → observe again)
- A full run-trace audit log of every step
- `[DangerousAction]` pause for human approval on destructive operations

Domain modules extend an operational agent's capabilities by registering MediatR commands as tools via `[AiTool]`. No separate tool-definition ceremony. Any module that ships MediatR commands can expose them to agents.

**School example:** `Student Progress Monitor` runs nightly, queries grade data, detects students below threshold, drafts a teacher notification, and logs the run trace. No human involved unless a grade change triggers `[DangerousAction]`.

**Social example:** `Content Scheduler` listens for a `ContentApproved` domain event, calls the social platform API tool, publishes the post, and records the outcome. Human approval pause fires if `RequirePublishApproval` is enabled on the account.

---

## Locked Architectural Decisions

### Decision 1 — Boilerplate scope: framework + bundled reference products
The boilerplate ships horizontal AI infrastructure. Domain modules ship vertical features. Two flagship reference products (school SaaS, social media SaaS) ship with the boilerplate and prove every primitive end-to-end. Downstream developers build their own verticals on the proven infrastructure.

### Decision 2 — Agent identity: hybrid service-account model
Agents act via dedicated service accounts with role assignments. They never impersonate human users. Cost caps, rate limits, and audit trails are attached to the service account, not the triggering user.

### Decision 3 — Extension model: Tools + Agent Templates (updated)
Domain modules extend agents by decorating MediatR commands and queries with `[AiTool]`. The DI container auto-discovers decorated handlers and registers them in the tool catalog, grouped by module. Agents access system operations exclusively through this catalog — no direct repository or DbContext access from agent code. No custom retrieval hooks or orchestration contracts beyond Tools + Templates.

### Decision 4 — Provider-native runtimes with explicit loop commitment (updated)
We delegate reasoning loops to provider-native runtimes (OpenAI Agents, Anthropic, Ollama). The runtime wrapper explicitly commits to multi-step agentic loop support: configurable `MaxSteps` cap, loop-break safety on repeated identical tool calls, and normalised step events for audit. Single-step chat completion is `MaxSteps=1`.

### Decision 5 — Embedded AI surfaces: Insights, Actions, Automations
The `<AiSurfaceSlot />` registry and slot-based placement system ships as designed. Domain modules register insights and actions onto pages without the AI module needing to know about them. All slots carry a `persona` filter — a slot registered for the `Teacher` persona only renders in teacher-context pages.

### Decision 6 — Target: C hybrid (boilerplate + flagship reference products)
This boilerplate is not purely developer-facing infrastructure, nor a standalone product. It ships horizontal AI primitives AND two flagship reference products that prove the primitives against real vertical requirements. Flagship requirements are the acid test for boilerplate scope decisions.

### Decision 7 — Two-axis identity: Role × Persona
Role answers *what you can do*. Persona answers *what AI experience you get*. These are orthogonal — a persona change never affects permissions. The `anonymous` persona is system-reserved. This model accommodates end-customer and guest surfaces without a second auth system.

### Decision 8 — Tiered agent authorship: template → fork → builder
All agents share one record shape and one runtime code path. Three creation origins are supported: module-registered template, fork of an installed template, and from-scratch via the admin builder. Safety, cost, and persona enforcement is at the record level — not at the creation path.

### Decision 9 — Flagship reference products: school SaaS + social media SaaS
Chosen because they stress orthogonal axes. School: per-user-category agents, child safety, long-context tutoring, curriculum RAG. Social media: brand/persona config per tenant, external tool integrations, approval workflows. Together they validate generality before any third vertical attempts to build on the boilerplate.

### Decision 10 — Foundation-first: multi-modal at Plan 10
Image, video, and audio generation are deferred to Plan 10. No feature enters the roadmap until the layer below it is solid. Social media flagship works text-first (copy, captions, briefs, scheduling) until the agent and surface foundation is proven.

### Decision 11 — End-customer surfaces: architecture-first, UI-deferred
The `anonymous` persona, public API scoping, and per-widget API key design are built into the foundation (Plans 5b and 5f). Public portals and embeddable widgets ship in dedicated phases when flagship needs surface them. No architectural rework later.

---

## Revised Roadmap

### Plan 4 family — completion

Plans 4a through 4b-8 are shipped. **4b-9 is current.**

| Plan | Description | Size |
|---|---|---|
| 4b-9 | RAG evaluation harness — offline eval set, recall@k, MRR, NDCG, faithfulness scoring, baseline regression gating. | L |

Plan 4 is complete when 4b-9 ships.

---

### Plan 5 family — Agent framework

| Plan | Scope | Size |
|---|---|---|
| 5a | Agent Runtime Abstraction + Provider Adapters. `IAiAgentRuntime`; `OpenAiAgentRuntime`, `AnthropicAgentRuntime`, `OllamaAgentRuntime`. Normalised step events. Multi-step agentic loop with configurable `MaxSteps` cap and loop-break safety on repeated tool calls. | L |
| **5b** | **Persona Primitive.** `IAiPersona` record, persona registry, user↔persona assignment table, `anonymous` system-reserved persona, persona-scoped agent resolution, safety preset and content filter tier per persona, persona-aware chat injection. | M |
| 5c | Agent Templates. Module-registered `(prompt + tools + KB + persona targets + safety preset)` bundles. `[AiTool]` attribute on MediatR commands — DI auto-discovery, tool catalog grouped by module. Install flow, fork-into-custom-agent, customisation-after-install. | M |
| 5d | Agent Identity + Safety + Content Moderation. Service-account creation, role assignment, cost caps, rate limits, `[DangerousAction]` human-approval pause. Input/output moderation pipeline with three presets: `Standard`, `ChildSafe`, `ProfessionalModerated`. Configurable per agent; inherited from persona default. | L |
| 5e | Bundled Platform Agents. `Platform Insights Agent` + `Support Copilot` as before. Adds: `Teacher Tutor` starter template (Student persona, ChildSafe) and `Brand Content Agent` starter template (Editor persona, Standard). | S |
| **5f** | **Admin AI Settings backend.** Per-tenant AI config API + data model: provider key management (platform-default fallback), model selection per agent class, tenant-level cost caps, default safety preset, brand/persona config (tone, name, avatar), per-widget API keys (origin-pinned, public quota bucket). No UI — surfaces in Plan 7b. | M |

---

### Frontends

| Plan | Scope | Size |
|---|---|---|
| 6 | Chat Sidebar UI. Global slide-in, assistant selector filtered by current user's persona, streaming markdown, citation hover, quota bar, keyboard toggle. | L |
| **7a** | **Core Admin Pages.** Assistants list, Knowledge Base manager, Tool Catalog browser (grouped by module), Persona Manager, Templates install/manage. | L |
| **7b** | **Advanced Admin Pages.** Agent Builder (model selector, prompt editor, KB attach, tool picker, persona targeting, safety preset, trigger config, cost cap). AI Settings UI (surfaces Plan 5f). Content Moderation Config. Usage Dashboard with per-agent cost and run counts. | L |

---

### Plan 8 — Embedded AI Surfaces

| Plan | Scope | Size |
|---|---|---|
| 8a | Insight primitive + slot system. `RegisterInsight` backend helper, hybrid cadence scheduler, typed output schemas, `<AiSurfaceSlot />` frontend registry, default card renderers. Persona-filtered slot registration. | L |
| 8b | Action primitive. One-click execute, preview/confirm, `[DangerousAction]` pause integration, inline action slot registration. Persona-aware. | M |
| 8c | Automation primitive. Domain events → automations, cron triggers, outcome-summary emission, webhook extensions. | M |
| 8d | Activity Views. Admin run-trace dashboard (trace + tokens + degradations) + end-user outcome inbox. Both backed by the same run store. | M |
| 8e | Embedded AI admin UI. Per-slot enable/disable/reorder, prompt/tool editor for insights and actions, automation trigger editor, tenant-level builder. | L |
| **8f** | **End-customer API Foundation.** Public-scoped API endpoints (separate route prefix, no `[Authorize]`), anonymous persona resolution, per-widget API key authentication, origin pinning, public rate-limit tier, public quota bucket. Backend only — no portal UI or widget. | M |

---

### New dimensions

| Plan | Scope | Size |
|---|---|---|
| 9 | Mobile chat client (Flutter). Persona-aware — agents surface per the authenticated user's persona. | L |
| 10 | Multi-modal. `IAiImageGenerator`, `IAiVideoGenerator`, `IAiAudioGenerator` abstractions; provider adapters; content moderation on generated assets; MinIO storage. | L |
| 11 | Marketplace + Cost Governance. Superadmin template publishing across tenants, per-tenant budgets, billing integration, opt-in/opt-out, enforcement. | M |

---

### Flagship acid tests

| Plan | School SaaS | Social SaaS |
|---|---|---|
| 5b | Teacher / Student / Parent personas created; Student gets ChildSafe filter | Editor / Approver / Client personas created |
| 5c | Tutor template installs targeting Student persona | Brand Content template installs targeting Editor persona |
| 5d | Student-targeted agent refuses inappropriate prompt; ChildSafe blocks output | ProfessionalModerated filters client-facing agent output |
| 5e | Teacher Tutor starter ready to fork per grade | Brand Content starter ready to fork per client |
| 5f | Admin sets ChildSafe as tenant default safety preset | Admin sets brand tone + avatar per tenant |
| 6 | Student sees Tutor; Teacher sees Lesson Planner — same sidebar, different persona | Editor sees Brand Content; Approver sees Draft Reviewer |
| 7a | Admin manages personas, installs templates | Same |
| 7b | Admin forks "Grade 5 Arabic Tutor" from Tutor template | Admin builds "ClientXYZ Brand Agent" from scratch |
| 8a | "3 students below grade threshold" insight on teacher dashboard | "Top post this week" insight on content dashboard |
| 8b | Teacher clicks "Generate quiz from this lesson" inline | Editor clicks "Draft caption for this product" inline |
| 8c | Student Progress Monitor runs nightly | Content Scheduler fires on `ContentApproved` event |
| 8f | Parent portal API foundation ready (no UI yet) | Client portal API foundation ready (no UI yet) |

---

## What the boilerplate looks like when complete

**AI foundation.**
Multi-tenant chat with streaming, inline citations, English + Arabic, quota tracking. RAG with hybrid retrieval, reranking, multi-turn follow-ups, structural chunking, and an offline evaluation harness (recall@k, MRR, NDCG, faithfulness).

**Identity + safety.**
Two-axis identity: Role (permissions) × Persona (AI experience). Tenants define audiences — Teacher, Student, Editor, Client — and agents target them. The `anonymous` persona is system-reserved for public surfaces. Content moderation pipeline with `Standard`, `ChildSafe`, and `ProfessionalModerated` presets, applied per agent and inherited from persona defaults. Agent service accounts with role assignment, cost caps, rate limits, and `[DangerousAction]` human-approval pause.

**Operational agents.**
Multi-step agentic runtime (OpenAI / Anthropic / Ollama) with configurable `MaxSteps` and loop-break safety. Domain modules expose operations as agent tools via `[AiTool]` on MediatR commands — no separate ceremony. Operational agents get service-account identity, live system access via the tool catalog, event and cron triggers, and a full run-trace audit log.

**Embedded AI surfaces.**
Insight cards inline on domain pages, one-click actions bound to records, event/cron automations — all persona-aware and slot-registered without touching the AI module. Admin run-trace dashboard and end-user outcome inbox.

**Admin UX.**
Per-tenant AI Settings: provider keys, model selection, cost caps, safety presets, brand/persona config. Persona Manager. Agent Builder: install templates, fork, or build from scratch — constrained to entitled tools, KBs, and models. Usage dashboard and content moderation config. Chat sidebar filters agents by the current user's persona.

**End-customer foundation.**
Public API endpoints with anonymous persona resolution, per-widget API keys, and origin pinning. Portal routes and embeddable widget ship in dedicated phases.

**Extensibility.**
Any domain module ships its own agent templates, insights, actions, and automations without modifying the AI module. `[AiTool]` on a MediatR command is all it takes to put a system operation in an agent's hands.

**Flagship reference products.**
School SaaS and social media SaaS ship with the boilerplate as living proof. Mobile chat in Flutter (persona-aware). Image/video/audio generation at Plan 10 with content moderation on generated assets. Superadmin marketplace for template publishing with per-tenant budgets and billing integration.

**What a product developer does to build a new vertical:**
Configure provider keys → define personas for your user categories → decorate domain commands with `[AiTool]` → register Agent Templates (or let admins build from scratch) → set content moderation presets → ship.

---

## Pillar → Plan mapping

| Pillar | Primary plans | Supporting plans |
|---|---|---|
| 1. Tenant/superadmin-authored agents | 5b, 5c, 7a, 7b, 8e | 4b family (RAG), 5f (settings), 6, 11 |
| 2. Domain-operational agents | 5a, 5c, 5d, 8a–8d | `[AiTool]` registration in domain modules; 5b (persona targeting) |
| 3. System-wide AI with permissions | 5a, 5d, 5e, 8c, 8d | 5b (persona), 5f (settings), 7b (admin UI) |
| 4. AI-first products | 10, 11 | 5a, 5b, 5c, 8b; flagship modules (school, social); future verticals |

---

## Deferred (documented, not scheduled)

- **End-customer portal UI and embeddable widget.** Architecture is ready (Plan 8f); UI delivery deferred to dedicated phases driven by flagship needs.
- **Long-term agent memory beyond RAG** (per-user preferences, past-interaction summaries). Revisit after Plan 7b ships and real usage patterns are visible.
- **Voice I/O** (speech-to-text + text-to-speech). Relevant for school tutoring and accessibility. Revisit after Plan 8 as a dedicated plan.
- **Semantic Kernel as base abstraction.** Revisit when SK Agents stabilises.
- **Fine-tuned embeddings / private models.** Revisit when tenant demand for domain-tuned retrieval surfaces.
- **Multi-agent orchestration / agent-to-agent protocols.** Revisit when single-agent pattern proves insufficient.
- **Real-time / live voice chat.** Revisit after multi-modal (Plan 10).

---

## Change control

Any future sub-plan that contradicts a locked decision must either (a) update this document first or (b) explicitly call out the deviation and justify it. Silent drift is a maintenance risk.

This document supersedes `2026-04-21-ai-module-vision-and-roadmap.md` for all forward-looking decisions. The original document remains for historical context on Plans 1–4b-8.
