# Codebase Assessment — Production Readiness & Risk Register

**Snapshot date:** 2026-04-25
**Scope:** Backend (.NET 10 + EF Core + MediatR), Frontend (React 19 + TS), Mobile (Flutter), infra (RabbitMQ, Postgres, Qdrant, Redis, MinIO).
**Method:** structural read of CLAUDE.md, docs/, recent commits, and module layout. Not a line-by-line code audit — scores can move ±1 with deeper review.

---

## Overall: 8.5 / 10 — strong enterprise-grade foundation

| Dimension | Score | Notes |
|---|---|---|
| Architecture & design patterns | 9 | Clean Architecture + CQRS, modular monolith with strict dependency direction, architecture tests enforce rules |
| Code structure & organization | 9 | Feature-folder layout, one-handler-per-file, codified naming, mirrored across BE/FE/Mobile |
| Scalability | 8.5 | Multi-tenant, transactional outbox, MassTransit/RabbitMQ, per-module DbContext, AI module isolated with Qdrant |
| Performance | 7.5 | OTel + perf pipeline behavior + async-first; not yet measured against SLOs |
| Security | 8.5 | JWT + refresh rotation, TOTP/2FA, API keys with emergency revoke, rate limiting, CSV-injection sanitization, permission policies, audit log |
| Production readiness | 8 | Health checks (with `Degraded` semantics, not `Unhealthy` — mature signal), structured logging, distributed tracing, RAG eval harness with deterministic baselines |
| Developer experience | 9 | Excellent CLAUDE.md, rename script, post-feature testing workflow, demo tenants, Docker compose with all sidecars |
| Testing | 7 | Architecture tests + RAG eval harness exist; handler-level coverage unknown |
| Frontend | 8 | Theme preset system, mandatory shared components, RTL, TanStack Query + Zustand |
| Documentation | 9 | More thorough than most production codebases |

### What lifts it above 8

- **Outbox done correctly** — atomic event commit, retry/DLQ, conversation-id correlation, log enrichment. Most "enterprise" codebases get this wrong.
- **Architecture tests as guardrails** — build fails if someone injects `IPublishEndpoint` in a MediatR handler. Aspirational rules become enforced rules.
- **Multi-tenancy is woven through, not bolted on** — global filters, platform-admin escape hatch, tenant scoping in import/export.
- **RAG eval harness with deterministic baselines** — orphan-collection cleanup via v7 GUIDs, faithfulness streaming, cache warmup tool. Rare for an internal boilerplate.

---

## Risk Register — the case against

The strengths above are real. So are the following weaknesses. Each item is framed as **"how a critic would attack this"**, with severity (Low/Med/High/Critical) and a concrete mitigation path.

### A. Architectural concerns

#### A1. "Modular monolith is the worst of both worlds" — **Med**

**Argument:** 8 DbContexts in one binary is neither a clean monolith nor true microservices. You pay the complexity cost of distributed systems (outbox, eventual consistency, idempotency) without the deployment benefit (independent scaling). Cross-module queries are impossible — you have to either query each context or project via events.

**Mitigation:**
- Be explicit that this is a *deliberate* modular monolith targeting "extract-when-needed" — document which modules are extraction candidates (AI is the obvious one).
- Add a decision log entry justifying "monolith first, micro-services on demand."
- For genuinely cross-module reporting needs, define a read-model strategy (event-sourced projection or scheduled materialized view) instead of ad-hoc multi-context queries.

#### A2. "It's not really CQRS" — **Low** (cosmetic)

**Argument:** Reads and writes go through the same DbContext. True CQRS uses separate stores (write model in Postgres, read model in materialized views / Elasticsearch / etc.). What's here is "Commands and Queries as separate handlers" — useful, but calling it CQRS oversells it.

**Mitigation:** Rename the convention internally to "command/query separation" or be explicit in docs that this is *handler-level* CQRS, not store-level. Keep the door open to introducing real read models per feature when query patterns warrant it (e.g., dashboards, audit log search).

#### A3. "MediatR adds latency and obscures call graphs" — **Low**

**Argument:** Every CRUD goes through 4 pipeline behaviors (validation, logging, performance, tracing). For simple operations, that's hundreds of microseconds of overhead and a stack trace that goes through reflection. Recent .NET community sentiment has shifted toward "use MediatR only when you need its features, not for every endpoint."

**Mitigation:** Measure, don't assume. Add a perf benchmark for a representative handler (e.g., `GetUserByIdQuery`). If latency is a concern, allow trivial queries to bypass MediatR — but only with a clear rule documented in CLAUDE.md.

#### A4. "Result pattern duplicates exceptions" — **Low**

**Argument:** Every handler returns `Result<T>`. You still need exceptions for infrastructure failures. Now you have two error channels and the controller has to know which is which.

**Mitigation:** Already largely handled by `HandleResult()`. Document the rule clearly: `Result.Failure` for *expected* business outcomes, exceptions for *unexpected* failures. Add a one-pager at `docs/architecture/error-handling.md`.

---

### B. Multi-tenancy & security

#### B1. Multi-tenancy via EF query filters is a tenant-leak waiting to happen — **High**

**Argument:** Global query filters are easy to bypass. Anyone who writes `.IgnoreQueryFilters()` for a legitimate reason (uniqueness check, cross-tenant admin op) can accidentally leave it on. The platform-admin escape hatch (`TenantId == null sees all`) is a single conditional standing between a tenant and every other tenant's data. EF query filters are also silently dropped on raw SQL, FromSqlRaw, and certain projections.

**Mitigation:**
- **Add Postgres Row-Level Security** as defense-in-depth. Even if EF filters are bypassed, RLS catches it at the DB layer. This is the single highest-leverage hardening for a multi-tenant SaaS.
- Add a *test* that takes every entity with `TenantId` and runs a cross-tenant access attempt; assert no rows returned for any handler.
- Static analyzer (Roslyn) that flags any new `.IgnoreQueryFilters()` and requires a justification comment.
- Audit-log every query that runs as platform-admin against tenant data.

#### B2. JWT revocation is hard — **Med**

**Argument:** JWTs are stateless. Refresh-token rotation helps, but a stolen access token is valid until expiry. The Sessions table mitigates this only if every request checks it (which defeats the JWT performance benefit).

**Mitigation:**
- Document the access-token TTL choice and the trade-off.
- Add an "emergency revocation" path: a denylist in Redis checked on every request. Short TTL = small list. Cost is one Redis hit per request, which is cheap.
- Consider WebAuthn/passkeys for passwordless re-auth — TOTP is fine, but passkeys are now the modern standard and should be on the roadmap.

#### B3. Permission strings drift across BE / FE / Mobile — **Med**

**Argument:** `Permissions.Users.Create` is mirrored by hand in three codebases (`Permissions.cs`, `permissions.ts`, `permissions.dart`). Drift is inevitable; a missed mirror means a UI button that calls an endpoint the user can't use, or worse, a UI that hides an endpoint that's actually open.

**Mitigation:**
- Code-generate FE and Mobile permission constants from `Permissions.cs` at build time. Single source of truth.
- Add a test that asserts the three lists are identical (string-by-string).

#### B4. Permission policy attributes use magic strings — **Low**

**Argument:** `[Authorize(Policy = Permissions.Users.Create)]` is a string that the compiler can't verify points to a real registered policy. A typo passes review and silently 403s in prod.

**Mitigation:** Already partially safe because `Permissions.Users.Create` is a `const string` — typos in *that* are caught. The remaining risk is a permission added to the constant but never registered as a policy. Add a startup validation: at app boot, every `const` in `Permissions` must have a matching policy registered.

#### B5. Rate limiting is in-process — **Med**

**Argument:** "10/s, 100/m" is per-instance. In a 4-pod deployment, real limits are 4× what's documented. Distributed attacks across instances aren't blocked.

**Mitigation:** Move rate limiting to Redis-backed distributed counters. ASP.NET Core's `RateLimiter` supports this with a custom partitioner.

#### B6. File storage tenant isolation is unclear — **Med**

**Argument:** MinIO/S3 — are tenant files in separate buckets? Separate prefixes with IAM policies? Or in one bucket where any path is reachable if you guess the GUID? Signed URLs help, but the bucket policy itself matters.

**Mitigation:** Document the storage layout. If today everything is in one bucket scoped by GUID prefix, move toward `tenant-{id}/...` prefix with IAM policies that scope an app's credentials per request (or per-tenant bucket for high-value tenants).

#### B7. Audit log grows unbounded — **Med**

**Argument:** Every meaningful action writes a row to `AuditLog`. After 2 years and 500 tenants, this table is huge, unindexed-for-search queries are slow, and you can't easily comply with a "delete tenant" GDPR request.

**Mitigation:** Plan partitioning (monthly), archival to cold storage (e.g., S3 + Athena), and a tenant-scoped purge job. Document retention policy.

---

### C. Operational & deployment

#### C1. Multiple DbContexts = multiple migration histories — **High**

**Argument:** 8 migration histories means deploys must coordinate across modules. Online migrations (NOT NULL on a 50M-row table, etc.) are hard to do safely with EF Core's default strategy. There's no documented "online schema change" pattern.

**Mitigation:**
- Add `docs/architecture/migration-safety.md` with rules: never `NOT NULL` without a default, never rename columns, prefer additive changes, etc. Mirror what mature shops codify.
- Pre-deploy migration linter (e.g., custom EF analyzer or a "migration review" PR template).
- For very large tenants, consider tools like `pg_repack` for online table rewrites.

#### C2. Outbox adds latency and operational surface — **Low** (acceptable cost)

**Argument:** Polling-based outbox dispatch means events are delayed by the polling interval. Operationally, you now have to monitor outbox lag, DLQs, consumer health — three things that a sync `IPublishEndpoint` call wouldn't have. For modules that genuinely don't need atomic event commit, this is over-engineering.

**Mitigation:** This is the right trade-off for the current architecture, but document *which* events need outbox (state-changing cross-module) vs. which could be fire-and-forget (analytics pings, non-critical notifications). Right now everything goes through outbox — fine, but be intentional.

#### C3. The `IPublishEndpoint` footgun is symptomatic — **Low** (already mitigated)

**Argument:** The fact that there's a runtime architecture test to prevent silent data loss means the framework *allows* the bug in the first place. That's a design smell. A truly safe API would make the wrong thing impossible, not just detected.

**Mitigation:** Already addressed via `MessagingArchitectureTests`. To go further: hide `IPublishEndpoint` from the Application layer's DI container entirely so handlers can't inject it even if they tried. Only Infrastructure/Consumer layers see it.

#### C4. CI/CD and deployment strategy not documented — **Med**

**Argument:** There's no mention of build pipeline, environment promotion, blue/green or canary deploys, secret rotation, backup/DR runbook, or DB failover procedure. "Production-ready" without a runbook isn't.

**Mitigation:** Add `docs/operations/`:
- `deployment.md` — how a release flows from PR → prod
- `runbook.md` — common incidents and remediation
- `dr-recovery.md` — backup verification, RTO/RPO targets
- `secrets.md` — rotation cadence for JWT keys, DB passwords, third-party API keys

#### C5. Single-region assumptions — **Low** (until you grow)

**Argument:** No mention of read replicas, geo-replication, sharding, or CDN strategy. Fine today; a wall later.

**Mitigation:** Document the assumption. Add a "scaling thresholds" section to `system-design.md` — at what tenant count / RPS do we re-architect?

#### C6. Test coverage is asserted, not measured — **Med**

**Argument:** Architecture tests exist; the RAG eval harness is sophisticated. But CLAUDE.md doesn't mention unit/integration test coverage targets, what runs in CI, or whether PRs require tests. "Tests exist" ≠ "behaviors are covered."

**Mitigation:**
- Add a coverage threshold to CI (e.g., 70% on `Application/`, 50% overall).
- A PR template asking "what tests prove this works."
- Document the test pyramid: how many unit vs. integration vs. e2e.

---

### D. Frontend concerns

#### D1. ApiResponse envelope unwrap is admitted #1 bug source — **High** (and easily fixed)

**Argument:** CLAUDE.md itself says: *"Components must unwrap: `const item = response?.data ?? response`. This is the #1 source of 'data not showing' bugs."* The fact that the rule needs to be repeated by every consumer is a code smell. Fix it once at the API client layer and the bug class disappears.

**Mitigation:**
- Make the axios response interceptor unwrap `ApiResponse<T>` automatically. Components consume `T` directly.
- For error cases, throw a typed error from the interceptor.
- Delete every `?.data ?? response` in the codebase. Add a lint rule banning it.

This is the single highest-leverage frontend fix.

#### D2. Bundle size with 17 lazy feature modules — **Med**

**Argument:** Even with lazy loading, the initial bundle includes shadcn/ui, TanStack Query, Zustand, react-hook-form, i18n, axios, etc. Mobile users on 3G will feel it.

**Mitigation:** Run `vite-bundle-visualizer` and document the budget. Set a PR-blocking budget (e.g., initial bundle ≤ 250KB gzipped).

#### D3. State split: Zustand + TanStack Query — **Low**

**Argument:** Two state libraries means two mental models. New devs need to learn the boundary rule (server state = TQ, client state = Zustand) and will sometimes get it wrong.

**Mitigation:** Document the rule with examples: "auth user → Zustand, list of users from API → TanStack." Add a lint rule that prevents server-y state in Zustand stores.

#### D4. i18n drift — **Med**

**Argument:** Translation key sync, missing keys, fallback strategies aren't called out. With Arabic + English and RTL, missing keys are painfully visible.

**Mitigation:** Run a script in CI that diffs `en.json` and `ar.json` keys; fail on mismatch. Document fallback behavior.

---

### E. Cross-cutting / strategic

#### E1. Domain events vs. integration events distinction is unclear — **Low**

**Argument:** Mature event-driven systems separate *domain events* (in-process, same-aggregate) from *integration events* (cross-module, async). CLAUDE.md only describes integration events. If domain events exist, they're not documented; if they don't, you're missing a useful pattern.

**Mitigation:** Document the chosen approach explicitly — even "we don't use domain events; everything is integration via outbox" is a fine answer.

#### E2. Webhook security spec — **Med**

**Argument:** Webhooks list "Secret Regeneration" but the spec doesn't say: HMAC algorithm, signature header format, replay-protection (timestamp window), retry semantics on 5xx. Consumers integrating need this.

**Mitigation:** Publish a public-facing webhook spec under `docs/modules/webhooks/spec.md` with example signatures and verification code in 3 languages.

#### E3. Mobile is uneven with web/BE — **Low** (known)

**Argument:** "Full stack" is the pitch, but mobile is at Phase 3 (Auth) per current memory. Permissions and theme sync are manual. Anyone evaluating the boilerplate as "BE + FE + Mobile parity" finds the gap.

**Mitigation:** Already on the roadmap. Add a clear status banner to mobile docs: "Mobile is in active development — Phases 0-2 complete, Phase 3 (Auth) in progress."

#### E4. No load-test / SLO baseline — **Med**

**Argument:** "Performance" claims need numbers. Without a baseline, regressions are invisible until prod.

**Mitigation:**
- Add a k6 or NBomber test for the 5 most critical endpoints.
- Define SLOs: p95 latency, error rate, availability.
- Run weekly in CI; alert on regression.

#### E5. Provider lock-in is undocumented — **Low**

**Argument:** Postgres-only, RabbitMQ-only via MassTransit (which abstracts somewhat), MinIO/S3 (abstracted). Anyone evaluating "can we move to Azure Service Bus / Azure SQL / Azure Blob?" needs to know the answer up front.

**Mitigation:** Add `docs/architecture/portability.md` listing every provider boundary and the abstraction in front of it (or lack thereof).

---

## Severity rollup

**High (fix or document explicitly before scaling beyond ~50 tenants):**
- B1 — Postgres RLS as defense-in-depth for multi-tenancy
- C1 — Migration safety patterns
- D1 — Centralize ApiResponse envelope unwrap

**Medium (track and revisit each quarter):**
- B2 (JWT revocation), B3 (permission codegen), B5 (distributed rate limit), B6 (file storage isolation), B7 (audit log retention)
- C4 (deploy runbook), C6 (test coverage targets)
- D2 (bundle budget), D4 (i18n key parity)
- E2 (webhook spec), E4 (SLO baseline)

**Low (acceptable, just be honest in docs):**
- A1 (modular monolith), A2 (CQRS naming), A3 (MediatR overhead), A4 (Result pattern)
- B4 (policy attribute strings), C2 (outbox cost), C3 (IPublishEndpoint footgun), C5 (single region)
- D3 (state library split)
- E1 (domain vs. integration events), E3 (mobile parity), E5 (provider portability)

---

## Suggested next reads

- [System Design](system-design.md)
- [Cross-Module Communication](cross-module-communication.md)
- [Messaging Follow-ups](messaging-followups.md)
