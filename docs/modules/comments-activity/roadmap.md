# Comments & Activity — Roadmap

Deliberately deferred improvements for module maintainers. Each entry names the trigger that flips it from "defer" to "do now" and points at the starting files so the next developer does not have to rediscover context.

Integrator-facing integration docs live in [`developer-guide/modules/comments-activity.md`](developer-guide.md). This file is maintainer-facing.

---

### Transactional outbox on `CommentsActivityDbContext`

**What:** Bind the MassTransit EF outbox to `CommentsActivityDbContext` so integration events (`CommentCreatedIntegrationEvent`, `CommentEditedIntegrationEvent`, `CommentDeletedIntegrationEvent`, `ReactionToggledIntegrationEvent`, `ActivityRecordedIntegrationEvent`) publish atomically with their Postgres writes. Today the outbox is bound to `ApplicationDbContext` only and this module publishes via `IMessagePublisher.PublishAsync` — at-most-once semantics.

**Why deferred:** No current consumer requires at-least-once. UX-critical paths (in-app notifications, realtime, activity feed) run as MediatR handlers inside the save pipeline, so a lost integration event degrades cross-module analytics only. Enabling the outbox now would add write-path latency without a caller to justify it.

**Pick this up when:** The first consumer requiring delivery guarantees lands (AI sentiment scoring that must not silently drop comments, audit-compliance consumer, billing-driven analytics, etc.), or when we observe a crash-window event drop in production that matters.

**Starting points:**
- Mirror the `AddEntityFrameworkOutbox<ApplicationDbContext>` block in `boilerplateBE/src/Starter.Infrastructure/DependencyInjection.cs` against `CommentsActivityDbContext` inside `boilerplateBE/src/modules/Starter.Module.CommentsActivity/Infrastructure/DependencyInjection.cs`.
- Swap `IMessagePublisher.PublishAsync` in `ActivityService.RecordAsync` and the four `Publish*IntegrationEvent` handlers for `IBus.Publish` running inside the same `SaveChangesAsync` transaction.
- Verify the purity test (`AbstractionsPurityTests`) still passes — the abstractions should gain no MassTransit references.

---

### `MetadataType` column on `ActivityEntry`

**What:** Persist the CLR type name alongside `metadataJson` so `GetActivityAsync<TMetadata>` can deserialize strongly-typed metadata on reads. Pairs with the `RecordAsync<TMetadata>` extension added in this bundle (which serializes but does not record the type).

**Why deferred:** Current readers treat `metadataJson` as an opaque string — the FE renders it via templates keyed off `action`, and there is no typed-read consumer today. Adding the column requires a migration in the CommentsActivity DbContext and threading type info through every write path, which is non-trivial with no caller pressure.

**Pick this up when:** An integrator explicitly asks for typed reads (e.g. "give me the list of `InvoicePaidMetadata` entries for this invoice"), or when a second consumer re-implements the same action-keyed switch and duplication becomes the right thing to remove.

**Starting points:**
- Add `MetadataType string? null` to `ActivityEntry` in `boilerplateBE/src/modules/Starter.Module.CommentsActivity/Domain/Entities/ActivityEntry.cs`.
- Create an EF migration in `boilerplateBE/src/modules/Starter.Module.CommentsActivity/Infrastructure/Persistence/Migrations/` (the module owns its own migration history table).
- Extend `RecordAsync<TMetadata>` in `boilerplateBE/src/Starter.Abstractions/Capabilities/ActivityServiceExtensions.cs` to pass `typeof(TMetadata).AssemblyQualifiedName` through a new `IActivityService.RecordAsync(..., string? metadataType, ...)` overload.
- Add a read-side `GetActivityAsync<TMetadata>` that filters by type and deserializes.

---

### Source-generator for `[CommentableEntity("Invoice")]` attribute

**What:** A Roslyn incremental source generator that reads `[CommentableEntity("Invoice", CustomActivityTypes = ["Paid"])]` on the entity class and emits the equivalent `AddCommentableEntity(...)` registration. Eliminates string-typing of `EntityType` at every call site and the duplicated registration boilerplate.

**Why deferred:** The boilerplate today is ~10 lines in a single `ConfigureServices` method per module — the fluent DI extension added in this bundle already removed the worst of it. A source-gen adds build-time complexity (new project, CI integration, debugging overhead) that is not justified for the current two-entity baseline (Product, and future Invoice as a worked example).

**Pick this up when:** We have ≥4 commentable entities **and** at least one production bug has shipped from an `EntityType` string drift (e.g. registered as `"Product"`, queried as `"product"`, or a typo in the FE filter chips).

**Starting points:**
- Look for existing Roslyn source-gen usage in the repo (`grep -r IIncrementalGenerator`). The repo does not ship one today, so the first generator sets the project template.
- Target the analyzer at the Entity class level (`[CommentableEntity]` on `Product : AggregateRoot`), not the module class.
- Emit a partial `IModule.ConfigureServices` helper that the module's real method calls.

---

### Cross-entity analytics capability `IActivityAnalyticsService`

**What:** A capability contract in `Starter.Abstractions` that lets consumers query activity entries across entity types with filters — e.g. "top 10 most-commented Products in Q1", "all `Paid` actions across Invoices and Subscriptions by actor X". Complements the current per-entity `IActivityService.GetActivityAsync`.

**Why deferred:** No reporting consumer exists today. Building a query-shape surface without a real caller means designing for imagined requirements, and the existing `IActivityService.GetActivityAsync` is sufficient for per-entity timeline reads (the only live consumer).

**Pick this up when:** The first analytics/reporting consumer lands and needs aggregation across entity types — typically a dashboard card ("activity by module this week") or an export job.

**Starting points:**
- Model the paging shape after `IReportingService` in `boilerplateBE/src/Starter.Abstractions/Capabilities/IReportingService.cs` — same `PagedResult<T>` plus a filter record.
- Implement against `CommentsActivityDbContext.ActivityEntries` with `.AsNoTracking()` and multi-tenant filter respected.
- Consider exposing it through the `CommentsActivityController` only if there is a UI caller; otherwise keep it capability-only for cross-module use.

---

### FE entity-scoped slot configuration

**What:** Per-entity overrides on the `entity-detail-timeline` slot — custom filter chips, empty-state copy, default filter tab, attachment policy. Today every entity renders the same timeline UI with the same chip set from `CustomActivityTypes`.

**Why deferred:** Only Product renders the slot today, so there is nothing to differentiate. Adding a config surface for hypothetical needs invites over-engineering (picking config keys we never validate against real use).

**Pick this up when:** A second entity (Invoice, Subscription, etc.) needs copy or chips distinct from Product — typically surfaced as a design-review ask ("the Invoice timeline should default to the Activity tab, not All").

**Starting points:**
- Extend `registerSlot` in `boilerplateFE/src/features/comments-activity/index.ts` to accept an optional `entityOverrides: Record<EntityType, Overrides>`.
- Thread the override into `EntityTimelineSlot.tsx` and consume it in `TimelineFilters.tsx` + `EmptyState.tsx`.
- Expose the BE `CustomActivityTypes` to the FE through `/api/v1/CommentsActivity/entity-types` (new endpoint) so the chips stay in sync with the server definition.

---

### Enrichment pipeline capability `ICommentEnrichmentPipeline`

**What:** A composable pipeline contract that runs ordered enrichment steps on each new comment (sentiment → entity-extraction → translation → embedding). Converts the three "pick exactly one" integration patterns in DEVELOPER_GUIDE.md §6 into a composition model where each enricher plugs in without owning the full `CommentCreatedEvent` path.

**Why deferred:** No enrichment consumer exists today. Pattern B (integration events + `IConsumer<T>`) is the default for cross-module work and is sufficient for independent enrichers. Pattern ordering matters only when there are ≥2 enrichers whose outputs feed each other.

**Pick this up when:** ≥2 AI/enrichment consumers exist **and** ordering or intermediate-result chaining becomes a real requirement — e.g. "translation must run before embedding so the vector space is consistent".

**Starting points:**
- Capability interface in `boilerplateBE/src/Starter.Abstractions/Capabilities/ICommentEnrichmentPipeline.cs` with a `Register(IEnricher step, int order)` surface.
- Host fan-out in a new MediatR handler on `CommentCreatedEvent` inside the CommentsActivity module.
- Pattern to mirror: the three `ICapability` examples in `DEVELOPER_GUIDE.md` §6 — this converts Pattern C into a composable pipeline.
