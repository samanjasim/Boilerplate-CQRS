# Codebase Assessment - Enterprise Readiness Roadmap

**Snapshot date:** 2026-04-29
**Base:** `origin/main` at `beb94c80` (`Modularity Tier 2.5 - Theme 2`)
**Scope:** Backend (.NET 10 + EF Core + MediatR), React web, Flutter mobile, module generator, CI, local infra (Postgres, Redis, RabbitMQ, MinIO, Qdrant, Jaeger, Prometheus).
**Method:** Structural review of docs, recent merges, module catalog, CI workflows, architecture tests, frontend/mobile module boundaries, and the prior 2026-04-25 assessment. This is a roadmap-level assessment, not a line-by-line security audit.

---

## Executive Summary

The boilerplate is no longer "just a starter." It is becoming a serious full-stack application platform: modular monolith, source-mode module composition, transactional outbox, multi-tenancy, AI/RAG infrastructure, admin/product UI, and CI guardrails for generated apps.

Current position: **8.6 / 10 foundation, 7.4 / 10 enterprise production readiness**.

That split matters. The architecture is strong, but enterprise readiness is won in the less glamorous layers: tenant isolation defense-in-depth, migration safety, operational runbooks, deterministic client contracts, permission/codegen drift prevention, load baselines, and documented scaling thresholds.

The next goal should not be "add more features." The next goal should be:

> Make the starter trustworthy enough that a solo developer, product team, or enterprise group can start a real business system without first questioning the hidden reliability, security, modularity, and operations assumptions.

---

## What Changed Since The 2026-04-25 Assessment

The April 25 risk register is still useful, but it is now stale in important areas.

| Area | Status on 2026-04-25 | Current status on 2026-04-29 |
|---|---|---|
| Source-mode modularity | Catalog idea existed, but source composition was still fragile | Tier 2 landed catalog-driven source composition; React routes/nav now use module registry contributions |
| Catalog schema | Flat catalog metadata only | Tier 2.5 Theme 1 added `version`, `supportedPlatforms`, package-readiness placeholders, and schema tests |
| Architecture test gaps | Several silent drift modes unguarded | Theme 3 closed many gaps: module name uniqueness, permission format/uniqueness, catalog platform compatibility, mobile import guard |
| Generated-app CI | Human-discipline gated | Theme 2 added `.github/workflows/modularity.yml` killer tests for `None`, `All`, and dependency-chain module selections |
| Backend module dependencies | Runtime dependency missing could be silent/late | `ModuleLoader.ResolveOrder` now fails loud on duplicate names, missing hard dependencies, and cycles |
| Frontend optional module coupling | Core imported optional feature internals | Routes/sidebar have been moved behind module contribution APIs; ESLint restricts optional module imports from core |
| Mobile modularity | Mostly theoretical | Mobile has `ModuleRegistry`, dependency sorting, duplicate checks, empty-list boot, and import guard tests |

These changes raise confidence in the module-system direction. They do **not** remove the highest production-hardening risks around multi-tenancy, migrations, API contracts, and operations.

---

## Current Scores

| Dimension | Score | Direction | Notes |
|---|---:|---|---|
| Architecture and modularity | 9.0 | Up | Clean Architecture, CQRS-style handlers, optional modules, catalog v2, source-mode generated apps |
| Backend reliability | 8.3 | Stable | Outbox, health checks, tracing, result pattern, good architecture tests; handler/integration coverage still uneven |
| Multi-tenancy and security | 7.6 | Stable | Strong app-layer tenant patterns, but no Postgres RLS, no Redis JWT denylist, in-process rate limiting |
| Operations readiness | 6.5 | Needs work | Docker and CI exist; missing migration safety, runbooks, DR, secrets rotation, release strategy |
| Frontend reliability | 7.8 | Mixed | Strong UI architecture and module registry; API envelope handling and i18n drift remain high-friction |
| Mobile readiness | 6.8 | Up | Good modular shell, but only one real optional module and permissions still manual |
| Testing and quality gates | 7.6 | Up | Architecture and killer tests improved; no coverage gate, SLO/load baseline, bundle budget, or i18n parity CI |
| Developer experience | 9.0 | Stable | Strong docs, rename/generator path, conventions, specs/plans, CI guardrails |
| Documentation | 8.7 | Stable | Extensive docs; production operations docs are the missing class |

---

## Readiness Targets

The boilerplate should serve four user profiles:

| User | What they need from the starter |
|---|---|
| Independent developer | Fast setup, clear docs, safe defaults, one-command generation, no hidden architecture traps |
| Solo founder | Auth, tenants, billing, admin UI, deployment playbook, upgrade path, maintainable code |
| Product team | Module boundaries, test gates, CI, migration policy, observability, predictable client contracts |
| Enterprise organization | Tenant isolation defense-in-depth, auditability, DR, secrets rotation, scaling thresholds, extension/package strategy |

Enterprise readiness does not mean over-engineering every deployment into Kubernetes. It means the starter makes risk explicit, prevents common drift automatically, and documents how to operate and scale it.

---

## Roadmap Principles

1. **Guardrails before features.** Add automation that prevents known failure classes before adding new optional modules.
2. **Generated artifacts are acceptable if drift is gated.** FE/mobile permissions, module registries, and future package metadata should be generated and checked in, with CI failing on drift.
3. **Defense in depth for tenant data.** EF filters are necessary but not sufficient for an enterprise multi-tenant starter.
4. **Operational docs are product features.** A production starter without deploy, backup, migration, and incident runbooks is not production-ready.
5. **Measure before claiming.** Performance, coverage, bundle size, and SLO claims need numbers.
6. **One source of truth per contract.** Permissions, modules, API schemas, i18n keys, and module package metadata should not be hand-synchronized.

---

## Recommended Execution Tracks

### Track 1 - Finish Tier 2.5 Modularity

**Why:** Package-readiness should not start until source-mode module contracts are generated, drift-gated, and proven by at least two mobile modules.

**Work:**

- Ship Theme 4: cross-platform permission codegen.
- Ship Theme 5: generated module bootstrap for backend, web, and mobile.
- Ship Theme 6: second mobile module (`communication`) plus capability-contract pattern.
- Keep `.github/workflows/modularity.yml` as a required check.

**Exit criteria:**

- FE and mobile permissions are generated from BE constants.
- Backend does not rely on filesystem DLL scanning for production module startup.
- FE `modules.config.ts` and mobile `modules.config.dart` are generated from `modules.catalog.json`.
- At least two mobile modules validate removal, empty registry, and dependency-order behavior.
- CI fails if generated permission/module artifacts drift.

**Existing plans:**

- `docs/superpowers/plans/2026-04-29-modularity-tier-2-5-theme-4.md`
- `docs/superpowers/plans/2026-04-29-modularity-tier-2-5-theme-5.md`
- `docs/superpowers/plans/2026-04-29-modularity-tier-2-5-theme-6.md`

---

### Track 2 - Multi-Tenant Security Hardening

**Why:** This is the highest enterprise risk. App-layer filters protect the normal path; the database should protect the failure path.

**Work:**

- Add a design and pilot for Postgres Row-Level Security on core tenant-owned tables.
- Decide how the app sets tenant context per connection/transaction (`SET LOCAL app.tenant_id = ...` or equivalent).
- Add cross-tenant leak tests for every tenant-owned entity and selected handlers.
- Add a guard around `.IgnoreQueryFilters()` usage: analyzer/test plus required justification convention.
- Add Redis-backed JWT access-token denylist for emergency revocation.
- Replace in-memory rate limiting with distributed Redis counters.
- Document file-storage tenant layout and move toward tenant-prefixed object keys or stronger IAM isolation.
- Add audit retention and archival strategy.

**Exit criteria:**

- EF filter bypass does not expose tenant rows in the RLS-covered pilot area.
- New `.IgnoreQueryFilters()` usage fails review/CI unless explicitly justified.
- Session revocation can invalidate stolen access tokens before natural expiry.
- Rate limits behave consistently across multiple app instances.
- Storage and audit retention policies are documented.

**Candidate docs to add:**

- `docs/architecture/tenant-isolation.md`
- `docs/architecture/rls-design.md`
- `docs/architecture/security-hardening.md`

---

### Track 3 - Migration And Operations Readiness

**Why:** Multiple DbContexts and optional modules are powerful, but production teams need safe schema-change and release rules.

**Work:**

- Add `docs/architecture/migration-safety.md`.
- Define additive-first migration rules: avoid destructive renames, avoid blocking table rewrites, backfill in batches, split deploy/backfill/constraint phases.
- Add a migration review checklist to PRs.
- Add `docs/operations/`:
  - `deployment.md`
  - `runbook.md`
  - `dr-recovery.md`
  - `secrets.md`
  - `backups.md`
- Define RTO/RPO defaults for starter deployments.
- Document blue/green or rolling deploy assumptions.
- Document outbox/DLQ operations and replay tooling requirements.

**Exit criteria:**

- Every production deployment has a documented path from PR to release.
- Migrations have a safety checklist and explicit rollback/forward-fix guidance.
- Backup verification is documented, not merely configured.
- Common incidents have runbook entries.

---

### Track 4 - API And Frontend Contract Reliability

**Why:** The FE still contains many `r.data.data`, `data?.data`, and envelope-normalization patterns. The April assessment called this the top frontend bug source, and it remains true.

**Work:**

- Centralize `ApiResponse<T>` and `PagedApiResponse<T>` unwrapping in the axios client layer.
- Introduce typed API helpers so feature clients return payloads, not envelopes, unless they deliberately need metadata.
- Add a lint rule or codemod guard banning direct `response.data.data` in feature code.
- Migrate high-traffic API clients first: auth, users, roles, tenants, billing, products.
- Add i18n key parity script for `en`, `ar`, and `ku`.
- Add bundle-size reporting and budget gate.

**Current measured i18n drift:**

- `ar` is missing 44 keys compared with `en`.
- `ku` is missing 115 keys compared with `en`.

**Exit criteria:**

- Components consume typed payloads rather than guessing envelope shape.
- CI fails when locale key sets drift.
- CI reports frontend bundle size and fails on agreed budget regression.

---

### Track 5 - Testing, Coverage, And SLO Baselines

**Why:** Existing architecture tests are good, but production readiness needs measured behavioral coverage and performance baselines.

**Work:**

- Define the test pyramid for this repo: unit, handler, integration, architecture, generated-app smoke, e2e.
- Add coverage reporting and thresholds. Start modestly and raise over time.
- Add load tests for critical flows:
  - login/refresh
  - tenant-scoped list query
  - file upload/download URL
  - outbox-producing command
  - AI/RAG search or chat path
- Define starter SLOs:
  - API p95 latency target
  - error-rate target
  - outbox lag target
  - worker processing target
- Run load tests locally first, then optionally scheduled CI.

**Exit criteria:**

- Coverage is measured in CI.
- At least five critical flows have repeatable load tests.
- SLOs are documented with known baseline numbers.
- Performance regressions have an owner and detection path.

---

### Track 6 - Enterprise Extension And Portability

**Why:** The starter should be honest about provider assumptions and ready for package-mode modules.

**Work:**

- Add `docs/architecture/portability.md`.
- Document provider boundaries:
  - Postgres
  - RabbitMQ/MassTransit
  - Redis
  - MinIO/S3
  - Qdrant
  - Ably
  - SMTP/SMS providers
  - AI providers
- Document domain events vs integration events clearly.
- Publish a webhook security spec with HMAC format, timestamp/replay protection, retries, and examples.
- Start Tier 3 only after Tier 2.5 exits cleanly.

**Exit criteria:**

- Buyers/evaluators can see which providers are swappable and which are currently assumptions.
- Webhook consumers have a public integration contract.
- Tier 3 package work starts from generated, drift-gated source-mode contracts.

---

## Recommended PR Sequence

This order balances risk reduction with the already-designed modularity roadmap.

| PR | Theme | Why first/next |
|---:|---|---|
| 1 | Update this roadmap and add a short tracking issue/list | Aligns the team before implementation |
| 2 | Tier 2.5 Theme 4 - permission codegen | Removes a known cross-platform drift class |
| 3 | Frontend API envelope cleanup design + first migration slice | Fixes the highest-leverage FE reliability bug |
| 4 | Migration safety docs + PR checklist | Low-risk, high-value operational guardrail |
| 5 | Tier 2.5 Theme 5 - generated module bootstrap | Heavier modularity change, now backed by Theme 4 and killer CI |
| 6 | Tenant isolation/RLS design and pilot | Highest security hardening item; should be deliberate |
| 7 | Operations docs pack | Deployment, DR, secrets, backups, runbook |
| 8 | Tier 2.5 Theme 6 - second mobile module | Validates mobile modularity after generation stabilizes |
| 9 | i18n parity + bundle budget gates | Frontend quality gate |
| 10 | Load/SLO baseline | Turns performance from a claim into a measured contract |

If you want maximum modularity momentum, swap PRs 3/4 with Theme 5. If you want maximum production-readiness momentum, keep the order above.

---

## Live Risk Register

| ID | Risk | Severity | Current status | Roadmap track |
|---|---|---|---|---|
| B1 | EF query filters can be bypassed; no DB-level tenant isolation | High | Open | Track 2 |
| C1 | Multi-DbContext migrations lack production safety rules | High | Open | Track 3 |
| D1 | FE API envelope unwrapping remains inconsistent | High | Open | Track 4 |
| B3 | Permissions drift across BE/FE/mobile | Medium | Planned in Theme 4 | Track 1 |
| B5 | Rate limiting is in-process | Medium | Open | Track 2 |
| B6 | File storage tenant isolation is not documented enough | Medium | Open | Track 2 |
| B7 | Audit retention/archival strategy missing | Medium | Open | Track 2 |
| C4 | Deployment/DR/secrets runbooks missing | Medium | Open | Track 3 |
| C6 | Coverage targets and test pyramid not formalized | Medium | Open | Track 5 |
| D2 | Bundle size budget missing | Medium | Open | Track 4 |
| D4 | i18n key drift exists (`ar`: 44 missing, `ku`: 115 missing) | Medium | Open | Track 4 |
| E2 | Public webhook security contract incomplete | Medium | Open | Track 6 |
| E4 | Load/SLO baseline missing | Medium | Open | Track 5 |
| A1 | Modular monolith trade-off needs explicit ADR | Low | Accepted, document | Track 6 |
| A2 | "CQRS" is handler-level, not store-level | Low | Accepted, clarify docs | Track 6 |
| E5 | Provider lock-in/portability assumptions need documentation | Low | Open | Track 6 |

---

## What Not To Do Next

- Do not start Tier 3 package distribution before Tier 2.5 Themes 4 and 5 are done.
- Do not add more optional modules until permission/module generation and API-envelope cleanup are under control.
- Do not attempt a full all-table RLS migration in one PR. Start with a pilot and prove connection-scoped tenant context first.
- Do not treat docs/operations as "later." For an enterprise starter, operational docs are part of the product.
- Do not chase microservices yet. The modular monolith is the right default; extraction should be a documented option, not the starting point.

---

## Immediate Next Decision

Choose the next implementation lane:

1. **Modularity-first:** Theme 4 -> Theme 5 -> Theme 6. Best if the main strategic goal is package-ready modules.
2. **Production-hardening-first:** API envelope cleanup -> migration safety -> RLS pilot -> ops docs. Best if the main goal is using this starter in a serious business project soon.
3. **Balanced path (recommended):** Theme 4 -> API envelope cleanup -> migration safety -> Theme 5 -> RLS pilot -> ops docs -> Theme 6.

The balanced path removes the most visible drift bug, fixes a daily frontend pain point, adds production migration discipline, then returns to the heavier module bootstrap work with better guardrails.

---

## Suggested Next Reads

- [System Design](system-design.md)
- [Cross-Module Communication](cross-module-communication.md)
- [Messaging Follow-ups](messaging-followups.md)
- [Module Development](module-development.md)
- [Tier 2.5 Hardening Spec](../superpowers/specs/2026-04-29-modularity-tier-2-5-hardening.md)
