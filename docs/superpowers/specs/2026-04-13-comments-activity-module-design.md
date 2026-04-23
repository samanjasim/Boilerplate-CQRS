# Comments & Activity Module: Design Specification

**Date:** 2026-04-13
**Status:** Draft
**Module:** Starter.Module.CommentsActivity
**Scope:** Composable collaboration + context layer for any entity in the system

---

## Context

The boilerplate's modular architecture supports composable modules via `IModule`, per-module DbContexts, capability contracts in `Starter.Abstractions`, and Null Object fallbacks. Four modules are already extracted (Billing, Webhooks, ImportExport, Products).

The Comments & Activity module is Wave 1 in the composable module catalog — a cross-domain engine that every vertical (SaaS, E-Commerce, Education, HR, ERP) needs. It transforms static CRUD records into collaborative, context-rich entities.

### Problem

Every business application eventually needs:
- Users discussing records (orders, contacts, tickets, products) without leaving the app
- Visibility into what happened to a record and when — beyond raw audit logs
- A way for systems (AI, workflows, automations) to read and write contextual information on entities

Without this module, every developer building on the boilerplate must build these patterns from scratch, per entity, with inconsistent UX.

### Vision

**Comments & Activity is a composable collaboration + context layer.** When enabled, any entity type in the system can independently gain:

- **Comments** — Rich threaded discussions with @mentions, file attachments, emoji reactions, and markdown
- **Activity** — Auto-generated timeline of what happened (CRUD captured automatically, custom events via registry)
- **Unified timeline** — Comments and activity merged chronologically with filter toggles

**Configuration is per-entity** — an Order might have comments + activity, a User record might have activity only, a Product might have comments only.

### Key Design Decisions

| Decision | Choice | Rationale |
|----------|--------|-----------|
| Threading | Single-level replies | Structured enough for conversations, simple enough for business users |
| Comment richness | Mentions, attachments, reactions, markdown | Full collaboration toolkit — positions this as a serious layer |
| Activity generation | Hybrid: auto CRUD + registry for custom | Broad coverage out-of-the-box, extensible per module |
| Feed UX | Unified timeline with filter toggle | Complete story by default, separable when needed |
| Integration surface | Full capability suite | AI-ready: read/write APIs for future AI, Workflow, Communication modules |
| Real-time | Live push via Ably | Feed updates in real-time, no presence/typing (added later via Enhanced Ably) |
| Watch system | Explicit watch/unwatch + auto-watch | User control + natural participation tracking |
| Standalone pages | Embedded only | Enhances entity detail pages without navigation clutter |
| EntityType field | String constants per module | Type safety at boundaries, module independence preserved |

### Target Users

- **End users** — Collaborate on records, see entity history, get notified about relevant changes
- **Developers using the boilerplate** — Enable comments/activity on their entities with minimal code
- **Future modules (AI, Workflow)** — Programmatically read/write comments and activity

---

## Data Model

### Database: `CommentsActivityDbContext`

Own migration history table: `__EFMigrationsHistory_CommentsActivity`

Global tenant query filter: `CurrentTenantId == null || entity.TenantId == CurrentTenantId`

### Entity: Comment

Extends `BaseAuditableEntity` (Id, CreatedAt, ModifiedAt, CreatedBy, ModifiedBy) + `ITenantEntity`.

| Property | Type | Constraints | Notes |
|----------|------|-------------|-------|
| TenantId | Guid? | | Global query filter |
| EntityType | string | MaxLength(100), Required | Polymorphic target ("Product", "Order") |
| EntityId | Guid | Required | Polymorphic target |
| ParentCommentId | Guid? | FK to Comment | null = top-level, set = reply |
| AuthorId | Guid | Required | User who authored the comment |
| Body | string | MaxLength(10000), Required | Markdown content |
| MentionsJson | string? | MaxLength(2000) | JSON: `[{userId, username, displayName}]` |
| IsDeleted | bool | Default: false | Soft delete flag |
| DeletedAt | DateTime? | | When soft-deleted |
| DeletedBy | Guid? | | Who soft-deleted |

**Navigation:** `ICollection<Comment> Replies` (inverse of ParentCommentId), `Comment? ParentComment`

**Domain logic:**
- Single-level threading enforced: a reply's parent must not itself have a parent
- Soft delete: `IsDeleted = true`, `Body` cleared (original lost for privacy/compliance), thread structure preserved
- Raises: `CommentCreatedEvent`, `CommentEditedEvent`, `CommentDeletedEvent`

**Indexes:** `(EntityType, EntityId)`, `(ParentCommentId)`, `(TenantId)`, `(AuthorId)`

### Entity: CommentAttachment

Extends `BaseEntity`.

| Property | Type | Constraints | Notes |
|----------|------|-------------|-------|
| CommentId | Guid | FK to Comment, Required | |
| FileMetadataId | Guid | Required | References `FileMetadata` in core by ID (no cross-context navigation) |
| SortOrder | int | Default: 0 | Display order within comment |

**Indexes:** `(CommentId)`

### Entity: CommentReaction

Extends `BaseEntity`.

| Property | Type | Constraints | Notes |
|----------|------|-------------|-------|
| CommentId | Guid | FK to Comment, Required | |
| UserId | Guid | Required | Who reacted |
| ReactionType | string | MaxLength(30), Required | Emoji code: "thumbsup", "heart", "rocket" |

**Unique constraint:** `(CommentId, UserId, ReactionType)` — one reaction per type per user per comment

**Toggle behavior:** Adding an existing reaction removes it; adding a new one creates it.

### Entity: ActivityEntry

Extends `BaseEntity` + `ITenantEntity`. Does NOT extend `AggregateRoot` — it IS the record of events, not a source of them.

| Property | Type | Constraints | Notes |
|----------|------|-------------|-------|
| TenantId | Guid? | | Global query filter |
| EntityType | string | MaxLength(100), Required | Polymorphic target |
| EntityId | Guid | Required | Polymorphic target |
| Action | string | MaxLength(100), Required | "created", "updated", "status_changed", custom |
| ActorId | Guid? | | null for system-generated activity |
| MetadataJson | string? | MaxLength(4000) | JSON bag: `{"oldStatus":"Draft","newStatus":"Active"}` |
| Description | string? | MaxLength(500) | Human-readable: "John changed price from $9.99 to $19.99" |

**Indexes:** `(EntityType, EntityId, CreatedAt DESC)`, `(TenantId)`, `(ActorId)`

### Entity: EntityWatcher

Extends `BaseEntity` + `ITenantEntity`.

| Property | Type | Constraints | Notes |
|----------|------|-------------|-------|
| TenantId | Guid? | | |
| EntityType | string | MaxLength(100), Required | |
| EntityId | Guid | Required | |
| UserId | Guid | Required | |
| Reason | WatchReason | Required | Explicit, Participated, Mentioned, Created |

**Unique constraint:** `(EntityType, EntityId, UserId)` — a user watches an entity once

**Indexes:** unique `(EntityType, EntityId, UserId)`, `(UserId)`, `(TenantId)`

### Not a DB Entity: CommentableEntityDefinition

In-memory registration record (same pattern as `EntityImportExportDefinition`):

```csharp
public sealed record CommentableEntityDefinition(
    string EntityType,              // "Product", "Order"
    string DisplayNameKey,          // i18n key for UI
    bool EnableComments,            // whether this entity supports comments
    bool EnableActivity,            // whether this entity has an activity feed
    string[] CustomActivityTypes,   // module-specific activity types beyond CRUD
    bool AutoWatchOnCreate,         // auto-add entity creator as watcher
    bool AutoWatchOnComment);       // auto-add commenter as watcher
```

---

## Capability Contracts

All interfaces defined in `Starter.Abstractions/Capabilities/`. Each gets a Null Object fallback in `Starter.Infrastructure/Capabilities/NullObjects/`.

### ICommentableEntityRegistry

```csharp
public interface ICommentableEntityRegistry : ICapability
{
    CommentableEntityDefinition? GetDefinition(string entityType);
    IReadOnlyList<CommentableEntityDefinition> GetAll();
    IReadOnlyList<string> GetCommentableTypes();
    IReadOnlyList<string> GetActivityTypes();
    bool IsCommentable(string entityType);
    bool HasActivity(string entityType);
}
```

**Registration pattern:** Modules register `ICommentableEntityRegistration` services in DI. The CommentsActivity module collects them all via `GetServices<ICommentableEntityRegistration>()` and populates the registry.

```csharp
public interface ICommentableEntityRegistration
{
    CommentableEntityDefinition Definition { get; }
}

public sealed record CommentableEntityRegistration(
    CommentableEntityDefinition Definition) : ICommentableEntityRegistration;
```

### ICommentService

```csharp
public interface ICommentService : ICapability
{
    Task<Guid> AddCommentAsync(string entityType, Guid entityId, Guid? tenantId,
        Guid authorId, string body, string? mentionsJson,
        IReadOnlyList<Guid>? attachmentFileIds,
        Guid? parentCommentId = null, CancellationToken ct = default);

    Task EditCommentAsync(Guid commentId, string newBody, string? newMentionsJson,
        CancellationToken ct = default);

    Task DeleteCommentAsync(Guid commentId, Guid deletedBy,
        CancellationToken ct = default);

    Task<CommentSummary?> GetByIdAsync(Guid commentId, CancellationToken ct = default);

    Task<IReadOnlyList<CommentSummary>> GetCommentsAsync(string entityType, Guid entityId,
        int pageNumber = 1, int pageSize = 50, CancellationToken ct = default);
}
```

### IActivityService

```csharp
public interface IActivityService : ICapability
{
    Task RecordAsync(string entityType, Guid entityId, Guid? tenantId,
        string action, Guid? actorId, string? metadataJson = null,
        string? description = null, CancellationToken ct = default);

    Task<IReadOnlyList<ActivitySummary>> GetActivityAsync(string entityType, Guid entityId,
        int pageNumber = 1, int pageSize = 50, CancellationToken ct = default);
}
```

### IEntityWatcherService

```csharp
public interface IEntityWatcherService : ICapability
{
    Task WatchAsync(string entityType, Guid entityId, Guid? tenantId,
        Guid userId, WatchReason reason = WatchReason.Explicit,
        CancellationToken ct = default);

    Task UnwatchAsync(string entityType, Guid entityId, Guid userId,
        CancellationToken ct = default);

    Task<bool> IsWatchingAsync(string entityType, Guid entityId, Guid userId,
        CancellationToken ct = default);

    Task<IReadOnlyList<Guid>> GetWatcherUserIdsAsync(string entityType, Guid entityId,
        CancellationToken ct = default);
}
```

### Summary DTOs (in Abstractions)

```csharp
public sealed record CommentSummary(
    Guid Id, string EntityType, Guid EntityId,
    Guid AuthorId, string Body, string? MentionsJson,
    Guid? ParentCommentId, bool IsDeleted,
    DateTime CreatedAt, DateTime? ModifiedAt);

public sealed record ActivitySummary(
    Guid Id, string EntityType, Guid EntityId,
    string Action, Guid? ActorId, string? MetadataJson,
    string? Description, DateTime CreatedAt);
```

### WatchReason Enum (in Abstractions)

```csharp
public enum WatchReason { Explicit, Participated, Mentioned, Created }
```

### Null Object Fallbacks

| Interface | Null Behavior |
|-----------|--------------|
| NullCommentableEntityRegistry | Empty collections, `false` for all checks |
| NullCommentService | No-ops, returns empty lists, `Guid.Empty` for adds |
| NullActivityService | No-ops, returns empty lists |
| NullEntityWatcherService | No-ops, returns empty lists, `false` for IsWatching |

Registered in `DependencyInjection.AddCapabilities()` via `TryAdd*` — modules override by calling `AddScoped`/`AddSingleton` directly.

---

## Module Integration

### How Other Modules Register Entities

Each module that wants comments/activity registers a `ICommentableEntityRegistration` in its `ConfigureServices`:

```csharp
// In ProductsModule.ConfigureServices:
services.AddSingleton<ICommentableEntityRegistration>(
    new CommentableEntityRegistration(new CommentableEntityDefinition(
        EntityType: ProductEntityTypes.Product,  // string constant: "Product"
        DisplayNameKey: "commentsActivity.entityTypes.product",
        EnableComments: true,
        EnableActivity: true,
        CustomActivityTypes: ["PriceChanged", "Published", "Archived"],
        AutoWatchOnCreate: true,
        AutoWatchOnComment: true)));
```

When CommentsActivity module is NOT installed: the `ICommentableEntityRegistration` services are registered but never consumed. The Null Object fallbacks handle all capability calls as no-ops.

### How Other Modules Record Activity

Each module creates its own domain event handler that calls `IActivityService`:

```csharp
// In Starter.Module.Products/Application/EventHandlers/
internal sealed class RecordProductActivity(IActivityService activityService)
    : INotificationHandler<ProductCreatedEvent>
{
    public async Task Handle(ProductCreatedEvent e, CancellationToken ct)
        => await activityService.RecordAsync(
            ProductEntityTypes.Product, e.ProductId, e.TenantId,
            "created", actorId: null,
            metadataJson: JsonSerializer.Serialize(new { e.Name, e.Slug }),
            description: $"Product \"{e.Name}\" was created", ct);
}
```

This is the standard capability consumption pattern. No cross-module project references needed.

### Notification Flow (on new comment)

1. `CommentCreatedEvent` domain event raised
2. Event handlers fire in the CommentsActivity module:
   - **AutoWatchOnComment** — adds author as watcher (if `AutoWatchOnComment = true` in definition)
   - **AutoWatchOnMention** — adds each mentioned user as watcher
   - **NotifyWatchersOnCommentCreated** — gets all watcher user IDs, excludes author, creates in-app notifications via existing `INotificationService.CreateAsync`
   - **RecordCommentActivity** — records a `comment_added` activity entry
3. Ably push: `comment:created` event to entity channel + personal notifications via existing `user-{userId}` channel

### Ably Channel Strategy

**Entity channel naming:** `entity-{tenantId}-{entityType}-{entityId}`

Events published on the entity channel:
- `comment:created` — `{ commentId, authorId, authorName, bodyPreview }`
- `comment:updated` — `{ commentId }`
- `comment:deleted` — `{ commentId }`
- `activity:created` — `{ activityId, action, description }`
- `reaction:changed` — `{ commentId, reactionType, count }`

**Requires:** Adding `PublishToChannelAsync(channel, eventName, data)` to existing `IRealtimeService` interface and `AblyRealtimeService` implementation.

### AI / Workflow Consumption

Capability interfaces are directly consumable by future AI function calling:
- `ICommentService.GetCommentsAsync` → AI reads discussions for context/summarization
- `ICommentService.AddCommentAsync` → AI posts summaries, workflow adds approval notes
- `IActivityService.GetActivityAsync` → AI reads entity history for context building
- `IActivityService.RecordAsync` → workflow records state transitions

---

## Backend CQRS Structure

```
Starter.Module.CommentsActivity/
  CommentsActivityModule.cs                    -- IModule implementation
  Constants/
    CommentsActivityPermissions.cs             -- permission string constants
  Domain/
    Entities/
      Comment.cs
      CommentAttachment.cs
      CommentReaction.cs
      ActivityEntry.cs
      EntityWatcher.cs
    Enums/
      WatchReason.cs
    Errors/
      CommentErrors.cs
      ActivityErrors.cs
      WatcherErrors.cs
    Events/
      CommentCreatedEvent.cs
      CommentEditedEvent.cs
      CommentDeletedEvent.cs
      ReactionToggledEvent.cs
  Application/
    Commands/
      AddComment/
        AddCommentCommand.cs
        AddCommentCommandHandler.cs
        AddCommentCommandValidator.cs
      EditComment/
        EditCommentCommand.cs
        EditCommentCommandHandler.cs
        EditCommentCommandValidator.cs
      DeleteComment/
        DeleteCommentCommand.cs
        DeleteCommentCommandHandler.cs
      ToggleReaction/
        ToggleReactionCommand.cs
        ToggleReactionCommandHandler.cs
      WatchEntity/
        WatchEntityCommand.cs
        WatchEntityCommandHandler.cs
      UnwatchEntity/
        UnwatchEntityCommand.cs
        UnwatchEntityCommandHandler.cs
      RecordActivity/
        RecordActivityCommand.cs
        RecordActivityCommandHandler.cs
    Queries/
      GetComments/
        GetCommentsQuery.cs
        GetCommentsQueryHandler.cs
      GetTimeline/
        GetTimelineQuery.cs
        GetTimelineQueryHandler.cs
      GetActivity/
        GetActivityQuery.cs
        GetActivityQueryHandler.cs
      GetWatchStatus/
        GetWatchStatusQuery.cs
        GetWatchStatusQueryHandler.cs
      GetMentionableUsers/
        GetMentionableUsersQuery.cs
        GetMentionableUsersQueryHandler.cs
    DTOs/
      CommentDto.cs
      ActivityEntryDto.cs
      TimelineItemDto.cs
      WatchStatusDto.cs
      CommentAttachmentDto.cs
      ReactionSummaryDto.cs
      MentionableUserDto.cs
    EventHandlers/
      NotifyWatchersOnCommentCreated.cs
      AutoWatchOnComment.cs
      AutoWatchOnMention.cs
      RecordCommentActivity.cs
  Infrastructure/
    Persistence/
      CommentsActivityDbContext.cs
      Migrations/
    Configurations/
      CommentConfiguration.cs
      CommentAttachmentConfiguration.cs
      CommentReactionConfiguration.cs
      ActivityEntryConfiguration.cs
      EntityWatcherConfiguration.cs
    Services/
      CommentableEntityRegistry.cs             -- implements ICommentableEntityRegistry
      CommentService.cs                        -- implements ICommentService
      ActivityService.cs                       -- implements IActivityService
      EntityWatcherService.cs                  -- implements IEntityWatcherService
  Controllers/
    CommentsActivityController.cs
```

---

## API Endpoints

Controller: `CommentsActivityController` at `api/v1/CommentsActivity/`

### Comments

| Method | Route | Body/Params | Permission | Description |
|--------|-------|-------------|------------|-------------|
| GET | `/comments` | `?entityType&entityId&pageNumber&pageSize` | Comments.View | List top-level comments for an entity, each with nested `replies[]` array, sorted CreatedAt ASC |
| POST | `/comments` | `{ entityType, entityId, body, mentionUserIds?, parentCommentId?, attachmentFileIds? }` | Comments.Create | Add a comment or reply |
| PUT | `/comments/{id}` | `{ body, mentionUserIds? }` | Comments.Edit | Edit own comment (or any with Comments.Manage) |
| DELETE | `/comments/{id}` | | Comments.Delete | Soft-delete own comment (or any with Comments.Manage) |

### Reactions

| Method | Route | Body | Permission | Description |
|--------|-------|------|------------|-------------|
| POST | `/comments/{id}/reactions` | `{ reactionType }` | Comments.Create | Add/toggle reaction on a comment |
| DELETE | `/comments/{id}/reactions/{reactionType}` | | Comments.Create | Remove reaction |

### Activity

| Method | Route | Params | Permission | Description |
|--------|-------|--------|------------|-------------|
| GET | `/activity` | `?entityType&entityId&pageNumber&pageSize` | Activity.View | List activity entries for an entity |

### Timeline

| Method | Route | Params | Permission | Description |
|--------|-------|--------|------------|-------------|
| GET | `/timeline` | `?entityType&entityId&pageNumber&pageSize&filter=all\|comments\|activity` | Comments.View or Activity.View | Merged chronological feed, sorted by CreatedAt ASC (oldest first, chat-style) |

### Watchers

| Method | Route | Body/Params | Permission | Description |
|--------|-------|-------------|------------|-------------|
| GET | `/watchers/status` | `?entityType&entityId` | Comments.View | Get watch status for current user + watcher count |
| POST | `/watchers` | `{ entityType, entityId }` | Comments.View | Watch an entity |
| DELETE | `/watchers` | `?entityType&entityId` | Comments.View | Unwatch an entity |

### Mentions

| Method | Route | Params | Permission | Description |
|--------|-------|--------|------------|-------------|
| GET | `/mentionable-users` | `?search&tenantId` | Comments.Create | Search users for @mention autocomplete |

---

## Permissions

### Backend Constants

```csharp
public static class CommentsActivityPermissions
{
    // Comments
    public const string ViewComments = "Comments.View";
    public const string CreateComments = "Comments.Create";
    public const string EditComments = "Comments.Edit";
    public const string DeleteComments = "Comments.Delete";
    public const string ManageComments = "Comments.Manage";

    // Activity
    public const string ViewActivity = "Activity.View";
}
```

### Default Role Mapping

| Permission | SuperAdmin | Admin | User |
|------------|-----------|-------|------|
| Comments.View | ✓ | ✓ | ✓ |
| Comments.Create | ✓ | ✓ | ✓ |
| Comments.Edit | ✓ | ✓ | ✓ |
| Comments.Delete | ✓ | ✓ | ✓ |
| Comments.Manage | ✓ | ✓ | |
| Activity.View | ✓ | ✓ | ✓ |

### Ownership Enforcement

- `Comments.Edit` and `Comments.Delete` enforce `comment.AuthorId == currentUser.Id` in the command handler
- `Comments.Manage` bypasses ownership check (moderation)

---

## Frontend Design

### Feature Module: `src/features/comments-activity/`

```
comments-activity/
  api/
    comments-activity.api.ts          -- axios calls
    comments-activity.queries.ts      -- TanStack Query hooks
    index.ts
  components/
    EntityTimeline.tsx                -- main embedded component
    CommentComposer.tsx               -- markdown input + @mention + file upload
    CommentThread.tsx                 -- top-level comment + indented replies
    CommentItem.tsx                   -- single comment render
    ReactionPicker.tsx                -- emoji reaction selector
    ActivityItem.tsx                  -- single activity entry render
    WatchButton.tsx                   -- watch/unwatch toggle
    MentionAutocomplete.tsx           -- @mention user search dropdown
  hooks/
    useEntityChannel.ts              -- Ably entity channel subscription
  index.ts                           -- module + slot registration
```

### Types: `src/types/comments-activity.types.ts`

```typescript
export interface Comment {
  id: string;
  entityType: string;
  entityId: string;
  parentCommentId?: string;
  authorId: string;
  authorName: string;
  authorEmail: string;
  body: string;
  mentions?: MentionRef[];
  attachments?: CommentAttachmentDto[];
  reactions: ReactionSummary[];
  isDeleted: boolean;
  replies?: Comment[];
  createdAt: string;
  modifiedAt?: string;
}

export interface MentionRef {
  userId: string;
  username: string;
  displayName: string;
}

export interface CommentAttachmentDto {
  id: string;
  fileMetadataId: string;
  fileName: string;
  contentType: string;
  size: number;
  url?: string;
}

export interface ReactionSummary {
  reactionType: string;
  count: number;
  userReacted: boolean;
}

export interface ActivityEntry {
  id: string;
  entityType: string;
  entityId: string;
  action: string;
  actorId?: string;
  actorName?: string;
  metadata?: Record<string, unknown>;
  description?: string;
  createdAt: string;
}

export type TimelineItem =
  | { type: 'comment'; data: Comment }
  | { type: 'activity'; data: ActivityEntry };

export interface WatchStatus {
  isWatching: boolean;
  watcherCount: number;
}
```

### Component: EntityTimeline

The primary component, embedded in entity detail pages via the slot system.

**Layout:**

```
┌─────────────────────────────────────────────┐
│ 💬 Comments & Activity     [👁 Watch] [3]   │
│ ─────────────────────────────────────────── │
│ [All] [Comments] [Activity]                 │
│ ─────────────────────────────────────────── │
│                                             │
│ 🔵 Product "Widget" was created             │
│    Apr 10, 2026 • System                    │
│                                             │
│ 💬 John Doe                      Apr 11     │
│    Great product! @jane can you review      │
│    the pricing?                             │
│    📎 pricing-doc.pdf                       │
│    👍 2  ❤️ 1    [Reply]                    │
│    ├─ Jane Smith                 Apr 11     │
│    │  Looks good, approved.                 │
│    │  👍 1                                  │
│                                             │
│ 🔵 Price changed: $9.99 → $19.99           │
│    Apr 12, 2026 • Jane Smith                │
│                                             │
│ ─────────────────────────────────────────── │
│ [Write a comment...]          [@] [📎] [↵] │
└─────────────────────────────────────────────┘
```

**Props:** `{ entityType: string; entityId: string }`

**Behavior:**
- Fetches merged timeline from `/timeline?filter=all`
- Filter toggle buttons: All | Comments | Activity
- Renders `<CommentThread>` for comment items, `<ActivityItem>` for activity items
- `<CommentComposer>` at bottom for adding comments
- `<WatchButton>` in header
- Subscribes to entity Ably channel via `useEntityChannel` for live updates
- Pagination via "Load more" button or infinite scroll
- Internal check: if entity type not registered, renders nothing gracefully

### Component: CommentComposer

**Features:**
- Textarea with markdown preview toggle (lightweight, no rich text library dependency)
- `@` keystroke triggers `<MentionAutocomplete>` overlay — searches users via `/mentionable-users?search=`
- Mentions stored as `@[Display Name](userId)` in body text
- File attachment button — uses existing file upload flow (`/Files/upload-temp`), file IDs passed with comment
- Reply mode: "Replying to {authorName}" banner with cancel button
- Submit button with loading state
- Validates: body required, max 10000 chars

### Component: CommentThread

- Renders top-level comment via `<CommentItem>`
- Below it, indented, renders replies (also `<CommentItem>` with `isReply=true`)
- "Reply" button on top-level comments opens inline `<CommentComposer>` in reply mode
- Reply button NOT shown on replies (enforcing single-level threading in UI)

### Component: CommentItem

- UserAvatar + author name + relative timestamp
- Markdown-rendered body (lightweight markdown renderer — e.g., `react-markdown` or simple regex-based)
- Attachment thumbnails/links (clickable, opens/downloads via file service)
- Reaction bar: existing reactions with counts + "+" button opening `<ReactionPicker>`
- Edit/Delete actions: shown for author OR users with `Comments.Manage`
- Soft-deleted comments: `[This comment has been deleted]` with dimmed styling
- Edit mode: inline `<CommentComposer>` replacing the body

### Component: WatchButton

- Eye icon toggle: "Watch" / "Watching" states
- Shows watcher count
- Calls POST/DELETE `/watchers` on toggle

### Component: ReactionPicker

- Preset emoji grid (thumbsup, thumbsdown, heart, rocket, eyes, party)
- Click toggles the reaction via POST/DELETE reaction endpoints
- Shown as a popover/dropdown

### Component: MentionAutocomplete

- Positioned overlay triggered by `@` keystroke in CommentComposer
- Searches users via `/mentionable-users?search={query}`
- Shows UserAvatar + name + email
- Selection inserts `@[Display Name](userId)` into the body

### Slot Integration

New slot in `slot-map.ts`:

```typescript
'entity-detail-timeline': {
  entityType: string;
  entityId: string;
}
```

Module registration in `comments-activity/index.ts`:

```typescript
export const commentsActivityModule = {
  name: 'commentsActivity',
  register(): void {
    registerSlot('entity-detail-timeline', {
      id: 'commentsActivity.entity-timeline',
      module: 'commentsActivity',
      order: 10,
      permission: 'Comments.View',
      component: lazy(() => import('./components/EntityTimeline')),
    });
  },
};
```

Entity detail pages add one line:

```tsx
<Slot id="entity-detail-timeline" props={{ entityType: "Product", entityId: id }} />
```

When the module is not installed, the slot renders nothing.

### Query Keys

```typescript
commentsActivity: {
  all: ['commentsActivity'] as const,
  comments: {
    all: ['commentsActivity', 'comments'] as const,
    list: (entityType: string, entityId: string, params?: Record<string, unknown>) =>
      ['commentsActivity', 'comments', entityType, entityId, params] as const,
  },
  activity: {
    all: ['commentsActivity', 'activity'] as const,
    list: (entityType: string, entityId: string, params?: Record<string, unknown>) =>
      ['commentsActivity', 'activity', entityType, entityId, params] as const,
  },
  timeline: {
    list: (entityType: string, entityId: string, params?: Record<string, unknown>) =>
      ['commentsActivity', 'timeline', entityType, entityId, params] as const,
  },
  watchers: {
    status: (entityType: string, entityId: string) =>
      ['commentsActivity', 'watchers', 'status', entityType, entityId] as const,
  },
},
```

### Real-time Hook: useEntityChannel

```typescript
function useEntityChannel(tenantId: string, entityType: string, entityId: string) {
  // Subscribes to Ably channel: entity-{tenantId}-{entityType}-{entityId}
  // On comment:created/updated/deleted → invalidate comments + timeline queries
  // On activity:created → invalidate activity + timeline queries
  // On reaction:changed → invalidate comments query
  // Cleans up subscription on unmount
}
```

### Permissions (Frontend)

```typescript
// src/constants/permissions.ts
Comments: {
  View: 'Comments.View',
  Create: 'Comments.Create',
  Edit: 'Comments.Edit',
  Delete: 'Comments.Delete',
  Manage: 'Comments.Manage',
},
Activity: {
  View: 'Activity.View',
},
```

### i18n Keys

Namespace: `commentsActivity` in `src/i18n/locales/{lang}/`

Key areas: titles, actions (add, edit, delete, reply), placeholders, empty states, watch/unwatch labels, filter labels, reaction tooltips, error messages, timestamp formats.

---

## Edge Cases

### Deleted Entities

When a parent entity (e.g., Product) is deleted, comments and activity entries become orphaned. Since the CommentsActivity module does NOT verify entity existence (no cross-context query), the data is harmless — the entity detail page is gone, so the timeline is never rendered. No cleanup needed for soft-deleted entities.

### Soft-Deleted Comments

- `IsDeleted = true`, `Body` cleared (original lost for privacy/compliance)
- `DeletedAt` and `DeletedBy` recorded
- Thread structure preserved — replies to deleted comments remain visible
- Frontend renders: "[This comment has been deleted]" with dimmed styling
- If all replies to a deleted comment are also deleted, the frontend can optionally hide the entire thread

### Tenant Isolation

- All entities have `TenantId` with global EF query filters
- Platform admins (`TenantId=null`) see all comments/activity across tenants
- Tenant users see only their tenant's data
- Comments created by platform admins have `TenantId=null` — visible to platform admins only (consistent with existing behavior)

### Permission Scoping

- Edit/Delete endpoints enforce `comment.AuthorId == currentUser.Id` in the handler
- `Comments.Manage` bypasses ownership check
- Return `403 Forbidden` if not owner and not Manage permission

### Entity Type Validation

- `AddCommentCommand` validates that `entityType` is registered in `ICommentableEntityRegistry`
- `AddCommentCommand` validates that `EnableComments = true` for the entity type
- `RecordActivityCommand` validates that `EnableActivity = true` for the entity type
- Returns `400 Bad Request` with clear error if entity type is not commentable/trackable

### Reply Threading Enforcement

- `AddCommentCommand` validates: if `parentCommentId` is set, the parent comment must not itself have a `ParentCommentId` (single-level only)
- Returns `400 Bad Request` if attempting to create a nested reply

---

## Verification Plan

### Backend

1. **Unit tests:** Command/query handlers, validators, domain entity logic
2. **Integration tests:** Full CQRS flow through controller → handler → DbContext
3. **Architecture tests:** Verify Abstractions has no project references, module isolation
4. **Multi-tenancy tests:** Tenant isolation on all queries

### Frontend

1. **Build check:** `npm run build` passes with no type errors
2. **Component testing:** EntityTimeline renders correctly with mock data
3. **Slot integration:** ProductDetailPage shows timeline when module enabled, nothing when disabled

### End-to-End

1. Run backend + frontend in test app
2. Navigate to a Product detail page → see empty timeline
3. Add a comment → appears in timeline, watcher auto-added
4. Reply to the comment → appears indented under parent
5. @mention another user → that user gets a notification
6. Add a reaction → reaction count updates
7. Edit a comment → body updates, "edited" indicator shown
8. Delete a comment → shows "[deleted]" placeholder
9. Watch/unwatch entity → toggle works
10. Create/update/delete a Product → activity entries auto-appear in timeline
11. Open same entity in two browser tabs → real-time updates appear in both
12. Filter toggles → correctly show All / Comments only / Activity only

---

## Dependencies on Existing Code (Files to Modify)

### Starter.Abstractions
- Add: `Capabilities/ICommentableEntityRegistry.cs`
- Add: `Capabilities/ICommentableEntityRegistration.cs`
- Add: `Capabilities/ICommentService.cs`
- Add: `Capabilities/IActivityService.cs`
- Add: `Capabilities/IEntityWatcherService.cs`
- Add: `Capabilities/WatchReason.cs`
- Add: `Capabilities/CommentSummary.cs`
- Add: `Capabilities/ActivitySummary.cs`
- Add: `Capabilities/CommentableEntityDefinition.cs`

### Starter.Infrastructure
- Modify: `DependencyInjection.cs` — register null fallbacks in `AddCapabilities()`
- Modify: `Services/AblyRealtimeService.cs` — add `PublishToChannelAsync` method
- Modify: `Services/IRealtimeService.cs` — add `PublishToChannelAsync` to interface
- Add: `Capabilities/NullObjects/NullCommentableEntityRegistry.cs`
- Add: `Capabilities/NullObjects/NullCommentService.cs`
- Add: `Capabilities/NullObjects/NullActivityService.cs`
- Add: `Capabilities/NullObjects/NullEntityWatcherService.cs`

### Starter.Module.Products (first integration)
- Add: `ICommentableEntityRegistration` in `ProductsModule.ConfigureServices`
- Add: `Application/EventHandlers/RecordProductActivity.cs`

### Frontend
- Modify: `src/config/api.config.ts` — add COMMENTS_ACTIVITY endpoints
- Modify: `src/config/modules.config.ts` — register commentsActivity module
- Modify: `src/config/routes.config.ts` — (no standalone routes needed)
- Modify: `src/constants/permissions.ts` — add Comments + Activity permissions
- Modify: `src/lib/query/keys.ts` — add commentsActivity query keys
- Modify: `src/lib/extensions/slot-map.ts` — add `entity-detail-timeline` slot
- Modify: `src/features/products/pages/ProductDetailPage.tsx` — add Slot render
- Add: `src/features/comments-activity/` — entire feature module
- Add: `src/types/comments-activity.types.ts`
- Add: `src/i18n/locales/en/commentsActivity.json` (+ ar translation)
