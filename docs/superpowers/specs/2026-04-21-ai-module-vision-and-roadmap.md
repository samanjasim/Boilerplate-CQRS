# AI Module — Strategic Vision & Roadmap

**Date:** 2026-04-21
**Status:** Approved (supersedes the roadmap sections of `2026-04-13-ai-integration-module-design.md`; that document remains the reference for already-shipped Plans 1–4a mechanics).
**Author:** Saman Jasim

---

## Purpose

This document captures the strategic vision for the boilerplate's AI module, the architectural decisions that anchor it, and the updated roadmap derived from those decisions. It exists so future planning cycles have a single source of truth for *why* sub-plans exist and what they collectively build toward.

---

## The Four Pillars

The AI module serves four distinct use-case pillars, each of which every downstream project built on the boilerplate may use in isolation or combination.

### Pillar 1 — Tenant- and superadmin-authored agents

Tenant admins (and superadmins on behalf of tenants) create agents for their end-users. The canonical example is a customer-service agent that answers questions about the tenant's own product, pulls from tenant documentation, and may execute safe actions on behalf of the customer.

### Pillar 2 — Domain-operational agents

Agents that embody a business domain and operate within it: an HR agent that checks leave balances and submits requests; a Tutor agent that plans study material and evaluates student progress; a Teacher agent that drafts lesson plans and analyses attendance. Each such agent combines domain knowledge (prompt + KB) with domain actions (tools).

### Pillar 3 — System-wide AI with permissions

AI that can observe and operate the platform itself under explicit permissions: platform-insights agents answering "how many active tenants are above 80% of their quota?"; support copilots guiding an admin through tenant configuration; onboarding agents provisioning a new tenant. These agents act on the platform, not on a single domain.

### Pillar 4 — AI-first products

Products built on the boilerplate whose primary value is AI-generated output: social-media post generators, reel/video synthesisers, brand-voice copy engines. These pillars make heavy use of multi-modal generation (image, video, audio) and content-moderation safeguards.

---

## Locked Architectural Decisions

These decisions are durable. Every sub-plan after this doc must respect them.

### Decision 1 — Boilerplate scope: framework plus bundled demo agents

The boilerplate ships the **framework** (agent engine, tool registry, RAG pipeline, admin UI, multi-modal abstractions) and a **small number of bundled demo agents** that know the platform itself. Domain-specific agents (HR, Tutor, Social Media, etc.) ship as **separate modules** built on top.

**Bundled platform agents** (ship inside `Starter.Module.AI`):
- `AI Tools Demo` (already shipped) — illustrates function-calling.
- `Platform Insights Agent` — read-only Q&A over tenant data (users, audit log, billing usage).
- `Support Copilot` — answers "how do I configure X" questions about the boilerplate's own features.

Future-bundled agents follow the same pattern: small, platform-aware, primarily demonstrative.

### Decision 2 — Agent identity: hybrid service-account model

- **User-invoked agents** (interactive chat) run as the invoking user. Existing JWT and permission machinery apply unchanged.
- **Autonomous / scheduled agents** (cron triggers, event triggers, background tasks) run as a **dedicated service account** with an explicitly assigned role. Each bundled platform agent has its own superadmin-editable service account.
- **Tenant admins** configure which role autonomous agents created in their tenant run under. Principle of least privilege is the default.

This model gives a natural attribution ("Fatima's agent did this" vs "the nightly insights agent did this"), unifies audit across interactive and autonomous runs, and provides a clean anchor for cost caps and rate limits.

### Decision 3 — Extension model: Tools + Agent Templates

Domain modules extend the AI module through two primitives:

- **Tools** — MediatR commands auto-discovered by the existing tool registry, callable by any agent whose admin enables them.
- **Agent Templates** — a module-registered bundle of `(system prompt + curated tool list + seed KB documents + default settings)`. Tenant admins "install" a template and get a pre-configured assistant they can then customise. Templates are the "agent as a product" unit.

A **Full Domain Adapter** pattern (custom retrieval pipelines per-domain, UI slots, orchestration hooks) is documented as a future direction in the *Deferred* section below but not implemented until a concrete need surfaces.

### Decision 4 — Delegate reasoning loops to provider-native runtimes

The boilerplate does **not** reimplement agent reasoning loops, plan-mode behaviours, or built-in tool orchestration. Instead, it defines `IAiAgentRuntime` with provider-specific adapters:

- `OpenAiAgentRuntime` — delegates to OpenAI Responses API / Agents SDK.
- `AnthropicAgentRuntime` — delegates to Claude Agent SDK (the same open-source SDK that powers Claude Code; not the CLI itself).
- `OllamaAgentRuntime` — ships a minimal in-process loop (~100 lines) because Ollama has no native agent runtime.

**The boilerplate owns** (the shell around the runtime):
- Agent identity and permission enforcement.
- Persistence (tasks, step logs, audit trail).
- Cost caps, rate limits, and `[DangerousAction]` human-in-the-loop pauses.
- Triggers (cron, event-driven) and interruption (user injecting a message mid-run).
- Templates, tenant isolation, webhook emission.
- Streaming normalised step events to our UI.

**The provider runtime owns** (the inside of the loop):
- Deciding which tool to call next, in what order, in parallel or serially.
- Extended-thinking / reasoning-model capabilities.
- Built-in tools as they mature (web search, code execution, file search, computer use).
- Token budgeting for the reasoning stream.

**Rationale.** Every time a provider ships a new loop capability — reasoning models, plan-mode, built-in search — we absorb it by upgrading the SDK. If we'd built our own loop, each advance would require re-engineering. Across all four pillars this approach is strictly better or equal; the only measurable constraint is that OpenAI's server-side Responses-API loop does not let us inject code between steps. For agents that need fine-grained per-step interception, route them to the in-process Anthropic or Ollama runtime.

---

## Principles

1. **Wrap, don't rebuild.** Prefer provider-native capabilities behind a thin interface over re-implementations.
2. **Agents are products.** Templates are the unit of distribution; treat them with the same care as a shippable feature.
3. **Least privilege by default.** Service accounts start with minimal roles; admins escalate explicitly.
4. **Observe everything.** Every tool call, step, cost increment, and outcome is logged, metered, and webhook-broadcast.
5. **Bilingual from the start.** English and Arabic are first-class for every new capability (prompts, retrieval, moderation, UI).
6. **Fail degraded, not silent.** Timeouts and errors surface as typed degradations (already established in Plan 4b-4), never as empty results.

---

## Updated Roadmap

### Shipped (feature/ai-integration, as of HEAD 6e32921)

| Plan | Scope |
|---|---|
| 1 | Foundation + Provider Layer (OpenAI, Anthropic, Ollama; streaming; token counting) |
| 2 | Chat + Streaming (SSE, message persistence, conversation history) |
| 3 | Assistants CRUD + Tool Registry (function calling via MediatR) |
| 4a | RAG Ingestion (upload → extract → chunk → embed → Qdrant) |
| 4b | RAG Retrieval + Chat Injection (hybrid search, citations, `/ai/search`) |
| 4b-1 | RAG Hardening + Arabic Foundations (RRF, embedding cache, timeouts, normalisation) |
| 4b-2 | Query Intelligence (expansion, hybrid reranking, classifier, neighbour expansion) |
| 4b-3 | Structural Chunking (heading-aware, breadcrumbs, chunk typing) |
| 4b-4 | RAG Observability (per-stage metrics, webhooks, structured logs) |
| 4b-5 | Multi-turn Contextual Rewrite (follow-up resolution, bilingual) |

### Plan 4 family — completion

| Plan | Scope | Size |
|---|---|---|
| 4b-6 | Circuit breaker around Qdrant + FTS (Polly pipelines, graceful degradation using existing `DegradedStages` telemetry) | S |
| 4b-7 | MMR diversification (reduce near-duplicate chunks in retrieved set) | M |
| 4b-8 | Per-document ACLs (row-level access control on chunks beyond tenant isolation) | L |
| 4b-9 | RAG evaluation harness (offline eval set, recall@k / MRR / faithfulness scoring) | L |

Plan 4 is complete when 4b-9 ships.

### Plan 5 — Agent framework (expanded from the original single-plan design)

| Plan | Scope | Size |
|---|---|---|
| 5a | Agent Runtime Abstraction + Provider Adapters. Defines `IAiAgentRuntime`; implements `OpenAiAgentRuntime`, `AnthropicAgentRuntime`, `OllamaAgentRuntime`. Normalises step events. | L |
| 5b | Agent Templates. Module-registered `(prompt + tools + KB + settings)` bundles; tenant-admin "install" flow; customisation-after-install. | M |
| 5c | Agent Identity & Safety. Service-account creation, role assignment, cost caps, rate limits, `[DangerousAction]` attribute with human-approval pause, interruption injection. | M |
| 5d | Bundled Platform Agents. `Platform Insights Agent` + `Support Copilot` shipped as templates with dedicated service accounts. | S |
| — | `docs/ai-friendly-modules.md`. Lightweight authoring guide for future domain modules (how to name tools, structure errors, design idempotent commands, etc.). | S |

### Frontends

| Plan | Scope | Size |
|---|---|---|
| 6 | Chat Sidebar UI (global slide-in, assistant selector, streaming markdown, citation hover, quota bar, keyboard toggle). | L |
| 7 | Admin Pages (Assistants, Knowledge Base, AI Tools, Agent Triggers, Usage Dashboard, Templates install/manage). | L |

### New dimensions

| Plan | Scope | Size |
|---|---|---|
| 8 | Mobile chat client (Flutter). | L |
| 9 | Multi-modal: image/video/audio generation with `IAiImageGenerator` / `IAiVideoGenerator` / `IAiAudioGenerator` abstractions; provider adapters; content moderation; asset storage via existing MinIO. | L |
| 10 | Marketplace & Cost Governance (superadmin publishing templates across tenants, per-tenant budgets, billing integration, opt-in/opt-out, enforcement). | M |

---

## Deferred (documented, not scheduled)

Items deliberately out of scope until a concrete need surfaces. These belong in this section so the intent is preserved and revisited on demand.

- **Full Domain Adapter contract** (richer Option C from Decision 3 — custom retrieval hooks, UI slots, orchestration). Revisit when a domain module proves unable to express its behaviour via Tools + Templates alone.
- **Long-term agent memory beyond RAG** (per-user preferences, past-interaction summaries persisted across sessions). Revisit after Plan 7 ships and we have real usage telemetry.
- **Semantic Kernel as base abstraction** (instead of direct SDK wrapping). Revisit when SK Agents stabilises and/or when we need cross-provider behaviours our direct adapters can't cleanly express.
- **Fine-tuned embeddings / private models.** Revisit when tenant demand for domain-tuned retrieval surfaces.
- **Multi-agent orchestration / agent-to-agent protocols.** Revisit when a single-agent pattern proves insufficient for a real use case.

---

## What the boilerplate looks like when complete

A developer generating a new project from the boilerplate receives, out of the box:

- Multi-tenant AI chat with streaming and inline citations, English + Arabic.
- Upload-your-documents RAG with hybrid retrieval, reranking, multi-turn follow-ups, and structural chunking.
- An admin UI to create assistants, upload knowledge, enable tools, schedule agent triggers, and install/manage templates.
- Autonomous agents with cron and event triggers, human-in-the-loop for dangerous actions, per-agent cost caps, and a full audit trail.
- A chat sidebar that end-users can open from any page.
- Mobile chat in Flutter.
- Image, video, and audio generation with content moderation and asset storage.
- Superadmin publishing of agent templates across tenants with per-tenant budgets and billing integration.
- Extension points for downstream modules to ship their own AI agents (HR, Tutor, Social Media, etc.) as templates — without modifying the AI module.

A product developer's effort to build an AI-enabled SaaS on top of this boilerplate collapses to: configure provider keys, write a few domain tools as MediatR commands, register one or two Agent Templates, customise prompts, ship.

---

## Pillar → Plan mapping (cross-reference)

| Pillar | Primary plans | Supporting plans |
|---|---|---|
| 1. Tenant/superadmin-authored agents | 3, 5b, 7 | 4a–4b family (RAG), 6, 10 |
| 2. Domain operational agents | 5a, 5b | Future domain modules (HR, Tutor, …) each ship their own Template(s) |
| 3. System-wide AI with permissions | 5a, 5c, 5d | `docs/ai-friendly-modules.md`, Plan 7 |
| 4. AI-first products | 9 | 5a, 5b, 10; future product modules (Social Media, …) |

---

## Change control

Any future sub-plan that contradicts a locked decision in this document must either (a) update this document first or (b) explicitly call out the deviation and justify it. Silent drift is a maintenance risk.
