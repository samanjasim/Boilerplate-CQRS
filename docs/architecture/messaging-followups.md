# Messaging: Production Follow-ups

Tracks remaining work identified during the April 2026 messaging review. Items are grouped by effort and impact so the next engineer can pick them up cleanly.

## Status legend

- **Queued** — scoped, agreed, not started
- **Sketched** — rough design below, needs fleshing out
- **Deferred** — agreed to be worth doing eventually; no immediate plan

---

## B1 — Dead-letter queue replay tooling — **Queued**

**Why it matters:** messages that exhaust retries land in RabbitMQ's `_error` queues. Today the only remediation path is `rabbitmqctl` from a sysadmin — painful, error-prone, not auditable. A permanently-failed `SendEmailRequestedEvent` means a user never got their verification code, and nobody at the business layer notices.

**Shape:**

- Superadmin-only API (`Ai.RunEval`-style permission) under `api/v1/admin/dead-letters`:
  - `GET /queues` — list `*_error` queues with message counts and oldest message age
  - `GET /queues/{name}/messages?take=50` — peek recent messages (type, enqueue time, fault reason, exception text)
  - `POST /queues/{name}/messages/{id}/replay` — re-enqueue onto the origin queue
  - `DELETE /queues/{name}/messages/{id}` — discard with audit trail
  - `POST /queues/{name}/replay-all` — bulk replay with a dry-run count first
- Implementation via the RabbitMQ Management HTTP API (talk directly; don't add a new MT pipeline)
- Audit log entry for every replay/discard
- Admin UI page listing queues + drill-down — follows the existing audit-log page shape

**Out of scope for this task:** advanced filters, scheduled auto-replay, partial body redaction in the peek view.

**Estimated effort:** medium. ~2 days including UI. RabbitMQ Management plugin must be installed (it is in docker-compose).

---

## B2 — Publish `Outbox:HealthCheck:*` defaults into `appsettings` — **Queued**

**Why it matters:** thresholds (`MaxPendingRows` = 1000, `MaxOldestAge` = 5 min) are hardcoded defaults in `OutboxHealthCheckOptions`. Ops can tune them via configuration but it's not discoverable — there's no appsettings entry showing the knob exists.

**Shape:** add to `boilerplateBE/src/Starter.Api/appsettings.json` and `appsettings.Production.json`:

```json
{
  "Outbox": {
    "HealthCheck": {
      "MaxPendingRows": 1000,
      "MaxOldestAge": "00:05:00"
    }
  }
}
```

Development can have more lenient values (say 5000 / 30 min) to avoid noise during demo flows. Add a one-line commentary in CLAUDE.md pointing to the knobs.

**Estimated effort:** 20 minutes.

---

## C1 — Migrate `DateTime.UtcNow` → `IDateTimeService` — **Sketched**

**Why it matters:** 13 Application handlers call `DateTime.UtcNow` directly. `IDateTimeService` exists for determinism + testability but is unused. Any test that asserts a timestamp can't pin the clock.

**Shape:** mechanical edit — inject `IDateTimeService` into each handler, replace `DateTime.UtcNow` with `dateTime.UtcNow`.

**Catch:** some call sites use `DateTime.UtcNow` inside `record` constructors during event scheduling (`new TenantRegisteredEvent(…, DateTime.UtcNow)`). Passing an `IDateTimeService`-generated value works fine but subtly changes the ordering (currently the record is built at the call site; if we time-stamp via service, the value is captured at that instant — same semantics, worth noting).

**Estimated effort:** 1 day including test updates.

**Files to check:**
```bash
grep -rn "DateTime\.UtcNow\|DateTime\.Now" boilerplateBE/src/Starter.Application/Features --include="*.cs"
```

---

## C2 — Split `RegisterTenantCommandHandler` by concern — **Deferred**

**Why it matters:** ~130 lines mixing slug generation, role resolution, event scheduling. Readable but would benefit from extraction if another tenant-creation path appears (e.g. admin-initiated provisioning).

**Shape:** extract `ISlugGenerator` (shared utility), `ITenantOwnerRoleResolver` (private service), leave event scheduling inline.

**Estimated effort:** 2-3 hours.

**Do this when:** a second path needs tenant creation. Premature otherwise.

---

## D1 — Roslyn analyzer for integration event contracts — **Deferred**

**Why it matters:** `MessagingArchitectureTests` blocks the reverse flow (`MassTransit` in Application). It doesn't block bad event shape — e.g. someone shipping an event that includes an EF entity (`TenantRegisteredEvent(Tenant Tenant, ...)`). That entity would be serialized into the outbox and fail deserialization at the consumer.

**Shape:** custom Roslyn analyzer registered in `Starter.Application.csproj`:

- Rule `STARTER001` — Types implementing `IDomainEvent` must be `record` (not `class`, not `struct`).
- Rule `STARTER002` — `IDomainEvent` properties must be primitives, `Guid`, `DateTime`, strings, enums, or records whose own properties satisfy the same rule. Reject `DbSet`, `IEntity`, types from `Starter.Domain.*.Entities` namespaces.
- Rule `STARTER003` — `IDomainEvent` types must live in `Starter.Application.Common.Events` namespace.

**Estimated effort:** 1 day. Custom analyzers have boilerplate (csproj packaging, registration, test infrastructure).

**Do this when:** we've shipped an event with a bad shape at least once, or the team grows past the point where PR review catches it.

---

## D2 — Shared `SlugGenerator` utility — **Deferred**

**Why it matters:** inline regex block in `RegisterTenantCommandHandler.GenerateSlug`. If another handler slugs, extract.

**Shape:** `Starter.Shared/Text/SlugGenerator.cs` with `Generate(string) → string`.

**Do this when:** second caller appears. One caller is not enough to justify the utility class.

---

## E1 — Migrate remaining `IPublishEndpoint` injections to `IIntegrationEventCollector` — **Critical**

**Why it matters:** the silent-drop bug PR #18 was meant to fix exists in **6 places** beyond `RegisterTenantCommandHandler`. PR #20 caught the remaining one (`UploadDocumentCommandHandler`) during integration testing because of the test app crash; the others were not exercised. Each is a ticking bug — it works in dev today (because in dev no second outbox replaces the first) but breaks the moment any future module adds its own `AddEntityFrameworkOutbox`.

Audited remaining offenders:

```
src/modules/Starter.Module.AI/Application/Commands/ReprocessDocument/ReprocessDocumentCommandHandler.cs
src/modules/Starter.Module.Communication/Application/Commands/ResendDelivery/ResendDeliveryCommandHandler.cs
src/modules/Starter.Module.Communication/Infrastructure/Services/TriggerRuleEvaluator.cs
src/modules/Starter.Module.Communication/Infrastructure/Services/MessageDispatcher.cs
src/Starter.Infrastructure/Services/MassTransitMessagePublisher.cs   ← internal core service used by other modules
```

**Shape:** the migration is mechanical, identical to what was done in this PR for the email-side handlers and `UploadDocumentCommandHandler`:

1. Replace `IPublishEndpoint bus` with `IIntegrationEventCollector eventCollector` in the constructor
2. Replace `await bus.Publish(new MyMessage(...), ct);` with `eventCollector.Schedule(new MyMessage(...));`
3. Ensure `await context.SaveChangesAsync(ct)` is called on `IApplicationDbContext` afterwards (for the interceptor to flush)
4. Verify the `MessagingArchitectureTests` ArchUnit test still passes — Application stays MassTransit-free

**Caveats:**

- `MassTransitMessagePublisher` implements `IMessagePublisher` from the Application layer. Its sole job is to wrap MT — it might be the right place to *change the implementation* to use the collector internally, leaving the abstraction intact.
- `TriggerRuleEvaluator` and `MessageDispatcher` are in the Communication module's Infrastructure layer (not Application). They publish from inside other consumer pipelines, where direct `IPublishEndpoint` is OK by design (MT's outbox is already in scope). **Verify case-by-case** before migrating these — they may not need to change.

**Estimated effort:** half a day plus a focused QA pass on each migration.

---

## Deferred / explicitly NOT on the radar

**Consumer-side shared idempotency helper** — the current domain-uniqueness-check pattern (`AnyAsync(e => e.TenantId == evt.TenantId)`) is simpler, more correct, and doesn't couple modules via a shared `ProcessedMessages` table. Adding a generic helper would duplicate MassTransit's `InboxState` and add cross-DbContext complexity without benefit. Keep the current convention.

**Kafka / event replay** — RabbitMQ doesn't retain event history. If the product grows to need replayable event streams (e.g. rebuilding a read model, seeding a new module with historical events), we'd introduce Kafka alongside MT rather than replace RabbitMQ. Not planned; monitor for signals.

**Exactly-once delivery** — at-least-once + idempotent consumers is the design. Exactly-once would require distributed transactions (2PC between DB and broker) which neither RabbitMQ nor MT supports out of the box. Not planned.
