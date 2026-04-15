# Comments & Activity — Developer Integration Guide

This module ships a reusable commenting + activity-feed capability that any other module (or the core host) can wire onto its own entities. This document is the integration contract: how to turn it on for a new entity, how to drive it programmatically, and how a future module (e.g. an `AI` module) can consume comment/activity events to run analysis on top.

---

## 1. Overview

**Loose coupling.** An entity is identified by a pair: `EntityType` (string, e.g. `"Product"`, `"Invoice"`) and `EntityId` (Guid). The module stores no foreign key to the target entity — any module can participate without creating a cross-schema dependency.

**Capability contracts.** Consumers depend only on `Starter.Abstractions`:

| Contract | Purpose |
|---|---|
| [IActivityService](../../Starter.Abstractions/Capabilities/IActivityService.cs) | Record and read activity-log entries |
| [ICommentService](../../Starter.Abstractions/Capabilities/ICommentService.cs) | Add / edit / delete / read comments |
| [IEntityWatcherService](../../Starter.Abstractions/Capabilities/IEntityWatcherService.cs) | Subscribe users to notifications for an entity |
| [INotificationServiceCapability](../../Starter.Abstractions/Capabilities/INotificationServiceCapability.cs) | Consumed by this module; module code compiles standalone |
| [ICommentableEntityRegistration](../../Starter.Abstractions/Capabilities/ICommentableEntityRegistration.cs) | Marker your module adds to DI to opt an entity in |

**Null Object fallback.** If this module is not installed, the host registers silent no-op implementations of `ICommentService`, `IActivityService`, and `IEntityWatcherService`. Callers don't need null-checks — calls simply become no-ops, and your module still compiles and runs.

**What you get out of the box.**
- Threaded comments with mentions, reactions, edit/delete, attachments
- Activity feed with built-in + custom action types
- Watchers + in-app and realtime notifications
- Multi-tenant isolation (read-side)
- Rich-text editor, timeline UI, filter tabs, i18n (EN/AR) on the frontend
- Ably realtime updates for live collaboration

**Feature flag.** The HTTP surface is gated by `comments.activity_enabled` (see `[RequireFeatureFlag]` on [`CommentsActivityController`](./Controllers/CommentsActivityController.cs)). Flip it off per-tenant to hide the feature.

---

## 2. Activating comments + activity on a new entity

Three steps in total. No base class, no interface to implement, no method to override. Just register a record.

### Step 1 — Register the entity definition

In your module's `IModule.ConfigureServices`, register an `ITenantResolver` and wire the entity with the fluent extension:

```csharp
services.AddScoped<InvoiceTenantResolver>();
services.AddCommentableEntity("Invoice", builder =>
{
    builder.CustomActivityTypes = ["Paid", "Voided", "Refunded"];
    builder.UseTenantResolver<InvoiceTenantResolver>();
});

internal sealed class InvoiceTenantResolver(InvoicesDbContext db) : ITenantResolver
{
    public Task<Guid?> ResolveTenantIdAsync(Guid entityId, CancellationToken ct) =>
        db.Invoices
            .IgnoreQueryFilters().AsNoTracking()
            .Where(i => i.Id == entityId)
            .Select(i => i.TenantId)
            .FirstOrDefaultAsync(ct);
}
```

Live reference: [`ProductsModule.cs`](../Starter.Module.Products/ProductsModule.cs) and [`ProductTenantResolver.cs`](../Starter.Module.Products/Infrastructure/Tenancy/ProductTenantResolver.cs).

**Builder defaults** (all optional — set only what differs):

| Property | Default |
|---|---|
| `DisplayNameKey` | `"commentsActivity.entityTypes.{entityType.ToLowerInvariant()}"` |
| `EnableComments` / `EnableActivity` | `true` / `true` |
| `CustomActivityTypes` | `[]` |
| `AutoWatchOnCreate` / `AutoWatchOnComment` | `true` / `true` |
| `ResolveTenantIdAsync` | `null` |

**Option: raw delegate instead of an `ITenantResolver`.** If you want inline tenant resolution without a separate class:

```csharp
services.AddCommentableEntity("Invoice", builder =>
{
    builder.ResolveTenantIdAsync = async (id, sp, ct) =>
    {
        await using var scope = sp.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<InvoicesDbContext>();
        return await db.Invoices.IgnoreQueryFilters().AsNoTracking()
            .Where(i => i.Id == id).Select(i => i.TenantId).FirstOrDefaultAsync(ct);
    };
});
```

**Alternate: record-constructor form** (older code, still supported — no migration required):

```csharp
services.AddSingleton<ICommentableEntityRegistration>(
    new CommentableEntityRegistration(new CommentableEntityDefinition(
        EntityType: "Invoice",
        DisplayNameKey: "commentsActivity.entityTypes.invoice",
        EnableComments: true,
        EnableActivity: true,
        CustomActivityTypes: ["Paid", "Voided", "Refunded"],
        AutoWatchOnCreate: true,
        AutoWatchOnComment: true,
        ResolveTenantIdAsync: ResolveInvoiceTenantIdAsync)));
```

**What each field does:**

| Field | Effect |
|---|---|
| `EntityType` | Canonical string key. Pick something stable — it's written into every comment and activity row. |
| `DisplayNameKey` | i18n key rendered in the FE filter chips and entity-type selectors |
| `EnableComments` / `EnableActivity` | Toggle each surface independently |
| `CustomActivityTypes` | Hint for the UI's filter chips. The backend `IActivityService.RecordAsync` will accept **any** string. |
| `AutoWatchOnCreate` | Author of a new entity is auto-subscribed |
| `AutoWatchOnComment` | Any commenter is auto-subscribed to future activity on the entity |
| `ResolveTenantIdAsync` | See below |

### What `ResolveTenantIdAsync` unlocks (no extra wiring)

The single delegate tells the module how to find the owning tenant of an entity. Once registered, these features work automatically with **no further integrator code**:

- **Mention scoping.** The `@mention` picker calls [`GetMentionableUsersQueryHandler`](./Application/Queries/GetMentionableUsers/GetMentionableUsersQueryHandler.cs). For a tenant user, it scopes to `ICurrentUserService.TenantId` automatically. For a platform admin operating on the entity, the handler calls your `ResolveTenantIdAsync` to find the entity's tenant, then restricts mentionable users to `tenant users + platform users`. If you omit the delegate, tenant users still get correct scoping; only the platform-admin edge case degrades (mentions show only platform users).
- **Notifications, watchers, activity recording** consistently carry `tenantId`, which flows through the same resolver when needed.

You do **not** implement any interface, inherit from any base class, override any method, or duplicate tenant-resolution logic. One delegate, three lines of implementation, and the cross-cutting tenant behavior is correct.

### Step 2 — Grant permissions

Permissions used by the module (see [`CommentsActivityPermissions`](./Constants/CommentsActivityPermissions.cs)):

| Permission | Policy string |
|---|---|
| View comments + watch/unwatch + react | `Comments.View` |
| Post comments | `Comments.Create` |
| Edit own comments | `Comments.Edit` |
| Delete own comments | `Comments.Delete` |
| Manage all comments (moderator) | `Comments.Manage` |
| View activity feed | `Activity.View` |

Default role bindings (SuperAdmin, Admin, User) are seeded by [`CommentsActivityModule.GetDefaultRolePermissions`](./CommentsActivityModule.cs). If your entity's permissions policy differs, override in the host.

### Step 3 — Render the frontend slot

On your entity's detail page:

```tsx
<Slot
  id="entity-detail-timeline"
  props={{ entityType: 'Invoice', entityId: invoice.id, tenantId: invoice.tenantId }}
/>
```

The slot is registered once in [`boilerplateFE/src/features/comments-activity/index.ts`](../../../../boilerplateFE/src/features/comments-activity/index.ts) and picks up any entity page that renders the slot. The frontend enforces `Comments.View` at render time.

Live reference: [`ProductDetailPage.tsx:280`](../../../../boilerplateFE/src/features/products/pages/ProductDetailPage.tsx#L280).

---

## 3. Recording activity from your module

Inject [`IActivityService`](../../Starter.Abstractions/Capabilities/IActivityService.cs) into an `INotificationHandler<TYourDomainEvent>` and call `RecordAsync` from there. Your domain event publishes on commit via MediatR.

```csharp
internal sealed class RecordInvoicePaidActivity(IActivityService activity)
    : INotificationHandler<InvoicePaidEvent>
{
    public Task Handle(InvoicePaidEvent e, CancellationToken ct) =>
        activity.RecordAsync(
            entityType: "Invoice",
            entityId: e.InvoiceId,
            tenantId: e.TenantId,
            action: "Paid",                         // from your CustomActivityTypes or a built-in
            actorId: e.PaidByUserId,
            metadataJson: JsonSerializer.Serialize(new { e.Amount, e.Currency }),
            description: $"Invoice paid — {e.Amount} {e.Currency}",
            ct);
}
```

Live reference: [`RecordProductActivity.cs`](../Starter.Module.Products/Application/EventHandlers/RecordProductActivity.cs).

**Typed-metadata overload.** To avoid inlining `JsonSerializer.Serialize(...)` at every call site, use the generic extension from [`ActivityServiceExtensions`](../../Starter.Abstractions/Capabilities/ActivityServiceExtensions.cs):

```csharp
public sealed record InvoicePaidMetadata(decimal Amount, string Currency);

await activity.RecordAsync(
    entityType: "Invoice",
    entityId: e.InvoiceId,
    tenantId: e.TenantId,
    action: "Paid",
    actorId: e.PaidByUserId,
    metadata: new InvoicePaidMetadata(e.Amount, e.Currency),
    description: $"Invoice paid — {e.Amount} {e.Currency}",
    ct);
```

The overload serializes with `JsonSerializerDefaults.Web` (camelCase) and stores the result as `metadataJson`. The persisted column and reads are unchanged — this is purely ergonomic.

**Notes:**
- Built-in action values are free-form strings; prefer the ones you declared in `CustomActivityTypes` plus the conventional `"created"` / `"updated"` / `"deleted"`.
- `metadataJson` is opaque to this module — store whatever you want to render later.
- If the module is not installed, calls are swallowed silently by the Null Object.

---

## 4. Reading comments, activity, and managing watchers programmatically

These are the capability signatures, lifted verbatim from the contracts. All methods accept `CancellationToken ct = default`.

### Comments ([`ICommentService`](../../Starter.Abstractions/Capabilities/ICommentService.cs))

```csharp
Task<Guid> AddCommentAsync(string entityType, Guid entityId, Guid? tenantId,
    Guid authorId, string body, string? mentionsJson,
    IReadOnlyList<Guid>? attachmentFileIds, Guid? parentCommentId = null, …);

Task EditCommentAsync(Guid commentId, string newBody, string? newMentionsJson, …);
Task DeleteCommentAsync(Guid commentId, Guid deletedBy, …);

Task<CommentSummary?> GetByIdAsync(Guid commentId, …);
Task<IReadOnlyList<CommentSummary>> GetCommentsAsync(
    string entityType, Guid entityId, int pageNumber = 1, int pageSize = 50, …);
```

Use `AddCommentAsync` when authoring system comments from a bot / integration account (e.g. "Invoice re-sent to customer" posted by a workflow runner).

### Activity ([`IActivityService`](../../Starter.Abstractions/Capabilities/IActivityService.cs))

```csharp
Task RecordAsync(string entityType, Guid entityId, Guid? tenantId,
    string action, Guid? actorId,
    string? metadataJson = null, string? description = null, …);

Task<IReadOnlyList<ActivitySummary>> GetActivityAsync(
    string entityType, Guid entityId, int pageNumber = 1, int pageSize = 50, …);
```

### Watchers ([`IEntityWatcherService`](../../Starter.Abstractions/Capabilities/IEntityWatcherService.cs))

```csharp
Task WatchAsync(string entityType, Guid entityId, Guid? tenantId,
    Guid userId, WatchReason reason = WatchReason.Explicit, …);
Task UnwatchAsync(string entityType, Guid entityId, Guid userId, …);
Task<bool> IsWatchingAsync(string entityType, Guid entityId, Guid userId, …);
Task<IReadOnlyList<Guid>> GetWatcherUserIdsAsync(string entityType, Guid entityId, …);
```

`WatchReason` distinguishes explicit subscriptions from auto-watch (so "unwatch" works correctly without nuking auto-subscriptions).

---

## 5. HTTP API surface

Base route: `/api/v1/CommentsActivity/…` — set by [`BaseApiController`](../../Starter.Abstractions.Web/BaseApiController.cs).

| Method | Route | Purpose | Permission |
|---|---|---|---|
| GET  | `/comments?entityType&entityId&pageNumber&pageSize` | List comments on an entity | `Comments.View` |
| POST | `/comments` | Add a comment | `Comments.Create` |
| PUT  | `/comments/{id}` | Edit a comment | `Comments.Edit` |
| DELETE | `/comments/{id}` | Delete a comment | `Comments.Delete` |
| POST | `/comments/{id}/reactions` | Toggle a reaction | `Comments.View` |
| DELETE | `/comments/{id}/reactions/{reactionType}` | Remove a reaction | `Comments.View` |
| GET  | `/activity?entityType&entityId&pageNumber&pageSize` | List activity entries | `Activity.View` |
| GET  | `/timeline?entityType&entityId&filter&pageNumber&pageSize` | Merged comments + activity (filter = `all` / `comments` / `activity`) | `Comments.View` |
| GET  | `/watchers/status?entityType&entityId` | Is current user watching? | `Comments.View` |
| POST | `/watchers` | Subscribe | `Comments.View` |
| DELETE | `/watchers?entityType&entityId` | Unsubscribe | `Comments.View` |
| GET  | `/mentionable-users?search&pageSize&entityType&entityId` | Mention autocomplete | `Comments.Create` |

Source of truth: [`CommentsActivityController`](./Controllers/CommentsActivityController.cs).

---

## 6. Consuming from another module — worked example for a hypothetical AI module

Suppose a `Starter.Module.AI` wants to score each new comment for sentiment, extract entities, or embed it for semantic search. Three integration patterns, in order of coupling — **pick exactly one**:

### Pattern A — In-process MediatR handler (tight, same-transaction)

[`CommentCreatedEvent`](./Domain/Events/CommentCreatedEvent.cs) publishes on commit through MediatR `INotification`. The AI module can handle it directly:

```csharp
internal sealed class AnalyzeNewComment(ISemanticAnalyzer analyzer)
    : INotificationHandler<CommentCreatedEvent>
{
    public async Task Handle(CommentCreatedEvent e, CancellationToken ct)
    {
        var comment = /* fetch via ICommentService.GetByIdAsync */;
        await analyzer.ScoreAsync(comment.Body, ct);
    }
}
```

**Pick this when:** the AI work is fast and cheap (e.g. a local classifier), you're OK coupling the AI module to this module's domain-event type, and you want analysis to block the commit on failure.

**Caveats:** `CommentCreatedEvent` lives under `Domain/Events/` and is intended for internal use; depending on it couples your module to this module's assembly. `CommentEditedEvent`, `CommentDeletedEvent`, and `ReactionToggledEvent` are available in the same folder.

### Pattern B — MassTransit integration event (loose, eventual, recommended)

For slow/expensive analysis (LLM calls, embeddings, vector-DB writes) consume the public integration events this module publishes. The contracts live in [`Starter.Abstractions/Events/CommentsActivity/`](../../Starter.Abstractions/Events/CommentsActivity/):

| Event | Published when |
|---|---|
| `CommentCreatedIntegrationEvent` | A comment is added (including replies) |
| `CommentEditedIntegrationEvent` | A comment body or mention set is edited |
| `CommentDeletedIntegrationEvent` | A comment is deleted |
| `ReactionToggledIntegrationEvent` | A reaction is added or removed |
| `ActivityRecordedIntegrationEvent` | Any call to `IActivityService.RecordAsync` |

Consume from the AI module (MassTransit auto-wires any `IConsumer<T>` in an assembly registered via `AddConsumers`):

```csharp
internal sealed class ScoreCommentSentiment(ISemanticAnalyzer analyzer)
    : IConsumer<CommentCreatedIntegrationEvent>
{
    public Task Consume(ConsumeContext<CommentCreatedIntegrationEvent> ctx) =>
        analyzer.ScoreAsync(ctx.Message.CommentId, ctx.Message.Body, ctx.CancellationToken);
}
```

**Pick this when:** analysis is slow, can retry on failure, must not block commits, or needs delivery guarantees across service/process boundaries.

**Delivery semantics — at-most-once (today).** The MassTransit outbox is currently bound to `ApplicationDbContext` only; this module owns `CommentsActivityDbContext` and publishes via `IMessagePublisher.PublishAsync`. A crash between the Postgres commit and the broker publish drops the integration event. In practice UX is unaffected because internal MediatR handlers (in-app notifications, realtime, activity) always fire — only cross-module consumers may miss an event. Upgrade to at-least-once by binding the outbox to the module's DbContext when a consumer requires it; tracked in [`ROADMAP.md`](./ROADMAP.md) under *Transactional outbox on `CommentsActivityDbContext`*.

### Pattern C — Capability + Null Object (consumer-driven, loosest deployment)

The AI module **defines** a capability in `Starter.Abstractions`:

```csharp
public interface ICommentAnalysisCapability : ICapability
{
    Task<CommentAnalysis> AnalyzeAsync(
        Guid commentId, string body, CancellationToken ct);
}
```

The core registers a `NullCommentAnalysisCapability` fallback (no-op or returns empty). The AI module registers its real implementation when installed. **This module** (`Starter.Module.CommentsActivity`) injects `ICommentAnalysisCapability` and calls it from its own handler on `CommentCreatedEvent`, storing the result in comment metadata or a side table.

**Reference for the pattern:** [`INotificationServiceCapability`](../../Starter.Abstractions/Capabilities/INotificationServiceCapability.cs) + [`NullNotificationServiceCapability`](../../Starter.Infrastructure/Capabilities/NullObjects/NullNotificationServiceCapability.cs) — exactly how this module consumes notifications today.

**Pick this when:** the AI result is intrinsic to the comment (you want it visible on the comment object itself), you want the module to keep working with or without AI installed, and the call is inline-acceptable.

### Quick picker

| Scenario | Pattern |
|---|---|
| Fast local analysis, tightly bound, fail-closed | A |
| Slow external call, retries, eventual, cross-process | B |
| Result is part of the comment, optional deployment | C |

---

## 7. Multi-tenancy notes

- Read queries respect global EF filters — tenant users only see their own tenant's data automatically.
- Write paths accept an explicit `tenantId`. For foreground requests from tenant users, this is always `ICurrentUserService.TenantId`. For **background jobs, webhooks, and platform-admin flows**, the module calls your `ResolveTenantIdAsync` to determine the owning tenant of the entity.
- Watchers are tenant-scoped. Mentions only resolve within the tenant of the caller (or the tenant of the entity, for platform admins) — no integrator code required to enforce this. See §2 Step 1.
- **Server-enforced tenant on capability writes (create paths).** `ICommentService.AddCommentAsync`, `IActivityService.RecordAsync`, and `IEntityWatcherService.WatchAsync` now reconcile the caller-supplied `tenantId` against the registered `ResolveTenantIdAsync` for the entity. If they disagree, the resolved value wins and a warning is logged. Callers that register a resolver (or an `ITenantResolver`) get cross-tenant-write protection for free.
- **Edit/delete paths still trust persisted state.** `EditCommentAsync` / `DeleteCommentAsync` operate on the comment's existing `TenantId`; a cross-tenant edit API call is still possible if the caller has the raw comment id. Tracked in [`ROADMAP.md`](./ROADMAP.md) under *Write-side tenant hardening for edit/delete*.
- **No resolver registered = legacy behavior.** If an entity omits `ResolveTenantIdAsync`, the caller-supplied `tenantId` is accepted as-is. This preserves back-compat for non-tenant-scoped entities.

---

## 8. Frontend slot registration

The slot is registered once, in this repo, at `boilerplateFE/src/features/comments-activity/index.ts`:

```ts
registerSlot('entity-detail-timeline', {
  id: 'commentsActivity.entity-timeline',
  module: 'commentsActivity',
  order: 10,
  permission: 'Comments.View',
  component: EntityTimelineSlot,
});
```

Any entity page that renders `<Slot id="entity-detail-timeline" props={{…}} />` picks up the timeline. No per-entity frontend code is needed beyond the three props.

---

## 9. Gotchas / FAQ

- **Domain events under `Domain/Events/` are internal.** Prefer Pattern B or C over Pattern A for cross-module integration.
- **`CustomActivityTypes` is a declarative hint only.** `IActivityService.RecordAsync` accepts any `action` string at runtime. Align the string exactly with what the UI filter expects.
- **`AutoWatchOnComment: true`** turns every commenter into a watcher on first comment. Disable for system-authored comments by calling `AddCommentAsync` with an `authorId` that you don't want subscribed.
- **Reactions use `Comments.View` (not `Comments.Create`).** Read-only viewers can react.
- **Feature flag.** The module is behind `comments.activity_enabled`. Flip it for incremental rollouts per tenant.
- **No installation = no crashes.** The host registers silent Null Objects for all three capability interfaces, so any module calling `IActivityService.RecordAsync` etc. keeps compiling and running without this module installed.

---

## 10. Roadmap

Planned improvements — outbox upgrade, typed-metadata reads, source-gen attribute, cross-entity analytics, FE slot config, enrichment pipeline, edit/delete tenant hardening — are tracked in [`ROADMAP.md`](./ROADMAP.md) with explicit "pick this up when" triggers. Consult it before opening a capability-change RFC.
