# Comments & Activity Module Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a composable Comments & Activity module that adds threaded comments, activity feeds, and real-time collaboration to any entity in the boilerplate system.

**Architecture:** New `Starter.Module.CommentsActivity` following the established module pattern (own DbContext, IModule, capability contracts in Abstractions, Null Object fallbacks). Frontend uses the slot system to embed an `<EntityTimeline>` component in entity detail pages. Real-time via Ably entity channels. Products module is the first integration target.

**Tech Stack:** .NET 10, EF Core (PostgreSQL), MediatR, FluentValidation, Ably, React 19, TypeScript, TanStack Query, shadcn/ui, react-markdown

**Spec:** `docs/superpowers/specs/2026-04-13-comments-activity-module-design.md`

---

## File Map

### New Files — Starter.Abstractions

| File | Responsibility |
|------|---------------|
| `src/Starter.Abstractions/Capabilities/ICommentableEntityRegistry.cs` | Registry contract for commentable entity definitions |
| `src/Starter.Abstractions/Capabilities/ICommentableEntityRegistration.cs` | Per-module registration interface |
| `src/Starter.Abstractions/Capabilities/CommentableEntityDefinition.cs` | Definition record |
| `src/Starter.Abstractions/Capabilities/ICommentService.cs` | Comment read/write capability |
| `src/Starter.Abstractions/Capabilities/IActivityService.cs` | Activity recording capability |
| `src/Starter.Abstractions/Capabilities/IEntityWatcherService.cs` | Watch/unwatch capability |
| `src/Starter.Abstractions/Capabilities/CommentSummary.cs` | Comment DTO for capability contract |
| `src/Starter.Abstractions/Capabilities/ActivitySummary.cs` | Activity DTO for capability contract |
| `src/Starter.Abstractions/Capabilities/WatchReason.cs` | Watch reason enum |

### New Files — Starter.Infrastructure (Null Objects)

| File | Responsibility |
|------|---------------|
| `src/Starter.Infrastructure/Capabilities/NullObjects/NullCommentableEntityRegistry.cs` | No-op registry fallback |
| `src/Starter.Infrastructure/Capabilities/NullObjects/NullCommentService.cs` | No-op comment fallback |
| `src/Starter.Infrastructure/Capabilities/NullObjects/NullActivityService.cs` | No-op activity fallback |
| `src/Starter.Infrastructure/Capabilities/NullObjects/NullEntityWatcherService.cs` | No-op watcher fallback |

### Modified Files — Core

| File | Change |
|------|--------|
| `src/Starter.Infrastructure/DependencyInjection.cs` | Register 4 null fallbacks in `AddCapabilities()` |
| `src/Starter.Application/Common/Interfaces/IRealtimeService.cs` | Add `PublishToChannelAsync` method |
| `src/Starter.Infrastructure/Services/AblyRealtimeService.cs` | Implement `PublishToChannelAsync` |
| `src/Starter.Infrastructure/Services/NoOpRealtimeService.cs` | Add no-op `PublishToChannelAsync` |
| `boilerplateBE/Starter.sln` | Add CommentsActivity project |

### New Files — Starter.Module.CommentsActivity

| File | Responsibility |
|------|---------------|
| `src/modules/Starter.Module.CommentsActivity/Starter.Module.CommentsActivity.csproj` | Project file |
| `src/modules/Starter.Module.CommentsActivity/CommentsActivityModule.cs` | IModule implementation |
| `src/modules/Starter.Module.CommentsActivity/Constants/CommentsActivityPermissions.cs` | Permission constants |
| **Domain** | |
| `Domain/Entities/Comment.cs` | Comment aggregate root |
| `Domain/Entities/CommentAttachment.cs` | File attachment link |
| `Domain/Entities/CommentReaction.cs` | Emoji reaction |
| `Domain/Entities/ActivityEntry.cs` | Activity entry entity |
| `Domain/Entities/EntityWatcher.cs` | Watcher entity |
| `Domain/Enums/WatchReason.cs` | Watch reason enum (module-local copy) |
| `Domain/Errors/CommentErrors.cs` | Error definitions |
| `Domain/Events/CommentCreatedEvent.cs` | Domain event |
| `Domain/Events/CommentEditedEvent.cs` | Domain event |
| `Domain/Events/CommentDeletedEvent.cs` | Domain event |
| `Domain/Events/ReactionToggledEvent.cs` | Domain event |
| **Infrastructure** | |
| `Infrastructure/Persistence/CommentsActivityDbContext.cs` | Module DbContext |
| `Infrastructure/Configurations/CommentConfiguration.cs` | EF config |
| `Infrastructure/Configurations/CommentAttachmentConfiguration.cs` | EF config |
| `Infrastructure/Configurations/CommentReactionConfiguration.cs` | EF config |
| `Infrastructure/Configurations/ActivityEntryConfiguration.cs` | EF config |
| `Infrastructure/Configurations/EntityWatcherConfiguration.cs` | EF config |
| `Infrastructure/Services/CommentableEntityRegistry.cs` | Registry implementation |
| `Infrastructure/Services/CommentService.cs` | ICommentService implementation |
| `Infrastructure/Services/ActivityService.cs` | IActivityService implementation |
| `Infrastructure/Services/EntityWatcherService.cs` | IEntityWatcherService implementation |
| **Application — Commands** | |
| `Application/Commands/AddComment/AddCommentCommand.cs` | Command + handler + validator |
| `Application/Commands/EditComment/EditCommentCommand.cs` | Command + handler + validator |
| `Application/Commands/DeleteComment/DeleteCommentCommand.cs` | Command + handler |
| `Application/Commands/ToggleReaction/ToggleReactionCommand.cs` | Command + handler |
| `Application/Commands/WatchEntity/WatchEntityCommand.cs` | Command + handler |
| `Application/Commands/UnwatchEntity/UnwatchEntityCommand.cs` | Command + handler |
| **Application — Queries** | |
| `Application/Queries/GetComments/GetCommentsQuery.cs` | Query + handler |
| `Application/Queries/GetTimeline/GetTimelineQuery.cs` | Query + handler |
| `Application/Queries/GetActivity/GetActivityQuery.cs` | Query + handler |
| `Application/Queries/GetWatchStatus/GetWatchStatusQuery.cs` | Query + handler |
| `Application/Queries/GetMentionableUsers/GetMentionableUsersQuery.cs` | Query + handler |
| **Application — DTOs** | |
| `Application/DTOs/CommentDto.cs` | Full comment DTO with replies |
| `Application/DTOs/ActivityEntryDto.cs` | Activity entry DTO |
| `Application/DTOs/TimelineItemDto.cs` | Unified timeline item |
| `Application/DTOs/WatchStatusDto.cs` | Watch status DTO |
| `Application/DTOs/ReactionSummaryDto.cs` | Reaction aggregation |
| `Application/DTOs/CommentAttachmentDto.cs` | Attachment DTO |
| `Application/DTOs/MentionableUserDto.cs` | User for @mention |
| **Application — Event Handlers** | |
| `Application/EventHandlers/NotifyWatchersOnCommentCreated.cs` | Notify watchers |
| `Application/EventHandlers/AutoWatchOnComment.cs` | Auto-add author as watcher |
| `Application/EventHandlers/AutoWatchOnMention.cs` | Auto-add mentioned users as watchers |
| `Application/EventHandlers/RecordCommentActivity.cs` | Record "comment_added" activity |
| `Application/EventHandlers/PublishCommentRealtimeEvent.cs` | Push to Ably entity channel |
| **Controller** | |
| `Controllers/CommentsActivityController.cs` | API endpoints |

### Modified Files — Products Module

| File | Change |
|------|--------|
| `src/modules/Starter.Module.Products/ProductsModule.cs` | Add ICommentableEntityRegistration |
| `src/modules/Starter.Module.Products/Application/EventHandlers/RecordProductActivity.cs` | New: record activity on product events |

### New Files — Frontend

| File | Responsibility |
|------|---------------|
| `boilerplateFE/src/types/comments-activity.types.ts` | TypeScript types |
| `boilerplateFE/src/features/comments-activity/api/comments-activity.api.ts` | API calls |
| `boilerplateFE/src/features/comments-activity/api/comments-activity.queries.ts` | TanStack Query hooks |
| `boilerplateFE/src/features/comments-activity/api/index.ts` | Re-exports |
| `boilerplateFE/src/features/comments-activity/components/EntityTimeline.tsx` | Main timeline component |
| `boilerplateFE/src/features/comments-activity/components/CommentComposer.tsx` | Comment input with mentions + attachments |
| `boilerplateFE/src/features/comments-activity/components/CommentThread.tsx` | Top-level comment + replies |
| `boilerplateFE/src/features/comments-activity/components/CommentItem.tsx` | Single comment render |
| `boilerplateFE/src/features/comments-activity/components/ActivityItem.tsx` | Single activity entry |
| `boilerplateFE/src/features/comments-activity/components/ReactionPicker.tsx` | Emoji reaction selector |
| `boilerplateFE/src/features/comments-activity/components/WatchButton.tsx` | Watch/unwatch toggle |
| `boilerplateFE/src/features/comments-activity/components/MentionAutocomplete.tsx` | @mention dropdown |
| `boilerplateFE/src/features/comments-activity/hooks/useEntityChannel.ts` | Ably entity channel hook |
| `boilerplateFE/src/features/comments-activity/index.ts` | Module + slot registration |

### Modified Files — Frontend

| File | Change |
|------|--------|
| `boilerplateFE/src/config/api.config.ts` | Add COMMENTS_ACTIVITY endpoints |
| `boilerplateFE/src/config/modules.config.ts` | Register commentsActivity module |
| `boilerplateFE/src/lib/query/keys.ts` | Add commentsActivity query keys |
| `boilerplateFE/src/constants/permissions.ts` | Add Comments + Activity permissions |
| `boilerplateFE/src/lib/extensions/slot-map.ts` | Add `entity-detail-timeline` slot |
| `boilerplateFE/src/features/products/pages/ProductDetailPage.tsx` | Add Slot render for timeline |
| `boilerplateFE/src/i18n/locales/en/translation.json` | Add commentsActivity translations |
| `boilerplateFE/src/i18n/locales/ar/translation.json` | Add Arabic commentsActivity translations |
| `boilerplateFE/package.json` | Add react-markdown dependency |

---

## Phase 1: Abstractions & Null Objects

### Task 1: Capability Contracts in Starter.Abstractions

**Files:**
- Create: `boilerplateBE/src/Starter.Abstractions/Capabilities/WatchReason.cs`
- Create: `boilerplateBE/src/Starter.Abstractions/Capabilities/CommentableEntityDefinition.cs`
- Create: `boilerplateBE/src/Starter.Abstractions/Capabilities/CommentSummary.cs`
- Create: `boilerplateBE/src/Starter.Abstractions/Capabilities/ActivitySummary.cs`
- Create: `boilerplateBE/src/Starter.Abstractions/Capabilities/ICommentableEntityRegistration.cs`
- Create: `boilerplateBE/src/Starter.Abstractions/Capabilities/ICommentableEntityRegistry.cs`
- Create: `boilerplateBE/src/Starter.Abstractions/Capabilities/ICommentService.cs`
- Create: `boilerplateBE/src/Starter.Abstractions/Capabilities/IActivityService.cs`
- Create: `boilerplateBE/src/Starter.Abstractions/Capabilities/IEntityWatcherService.cs`

- [ ] **Step 1: Create WatchReason enum**

```csharp
// boilerplateBE/src/Starter.Abstractions/Capabilities/WatchReason.cs
namespace Starter.Abstractions.Capabilities;

public enum WatchReason
{
    Explicit,
    Participated,
    Mentioned,
    Created
}
```

- [ ] **Step 2: Create CommentableEntityDefinition record**

```csharp
// boilerplateBE/src/Starter.Abstractions/Capabilities/CommentableEntityDefinition.cs
namespace Starter.Abstractions.Capabilities;

public sealed record CommentableEntityDefinition(
    string EntityType,
    string DisplayNameKey,
    bool EnableComments,
    bool EnableActivity,
    string[] CustomActivityTypes,
    bool AutoWatchOnCreate,
    bool AutoWatchOnComment);
```

- [ ] **Step 3: Create summary DTOs**

```csharp
// boilerplateBE/src/Starter.Abstractions/Capabilities/CommentSummary.cs
namespace Starter.Abstractions.Capabilities;

public sealed record CommentSummary(
    Guid Id,
    string EntityType,
    Guid EntityId,
    Guid AuthorId,
    string Body,
    string? MentionsJson,
    Guid? ParentCommentId,
    bool IsDeleted,
    DateTime CreatedAt,
    DateTime? ModifiedAt);
```

```csharp
// boilerplateBE/src/Starter.Abstractions/Capabilities/ActivitySummary.cs
namespace Starter.Abstractions.Capabilities;

public sealed record ActivitySummary(
    Guid Id,
    string EntityType,
    Guid EntityId,
    string Action,
    Guid? ActorId,
    string? MetadataJson,
    string? Description,
    DateTime CreatedAt);
```

- [ ] **Step 4: Create registration interfaces**

```csharp
// boilerplateBE/src/Starter.Abstractions/Capabilities/ICommentableEntityRegistration.cs
namespace Starter.Abstractions.Capabilities;

public interface ICommentableEntityRegistration
{
    CommentableEntityDefinition Definition { get; }
}

public sealed record CommentableEntityRegistration(
    CommentableEntityDefinition Definition) : ICommentableEntityRegistration;
```

- [ ] **Step 5: Create ICommentableEntityRegistry**

```csharp
// boilerplateBE/src/Starter.Abstractions/Capabilities/ICommentableEntityRegistry.cs
namespace Starter.Abstractions.Capabilities;

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

- [ ] **Step 6: Create ICommentService**

```csharp
// boilerplateBE/src/Starter.Abstractions/Capabilities/ICommentService.cs
namespace Starter.Abstractions.Capabilities;

public interface ICommentService : ICapability
{
    Task<Guid> AddCommentAsync(
        string entityType, Guid entityId, Guid? tenantId,
        Guid authorId, string body, string? mentionsJson,
        IReadOnlyList<Guid>? attachmentFileIds,
        Guid? parentCommentId = null,
        CancellationToken ct = default);

    Task EditCommentAsync(
        Guid commentId, string newBody, string? newMentionsJson,
        CancellationToken ct = default);

    Task DeleteCommentAsync(
        Guid commentId, Guid deletedBy,
        CancellationToken ct = default);

    Task<CommentSummary?> GetByIdAsync(
        Guid commentId,
        CancellationToken ct = default);

    Task<IReadOnlyList<CommentSummary>> GetCommentsAsync(
        string entityType, Guid entityId,
        int pageNumber = 1, int pageSize = 50,
        CancellationToken ct = default);
}
```

- [ ] **Step 7: Create IActivityService**

```csharp
// boilerplateBE/src/Starter.Abstractions/Capabilities/IActivityService.cs
namespace Starter.Abstractions.Capabilities;

public interface IActivityService : ICapability
{
    Task RecordAsync(
        string entityType, Guid entityId, Guid? tenantId,
        string action, Guid? actorId,
        string? metadataJson = null,
        string? description = null,
        CancellationToken ct = default);

    Task<IReadOnlyList<ActivitySummary>> GetActivityAsync(
        string entityType, Guid entityId,
        int pageNumber = 1, int pageSize = 50,
        CancellationToken ct = default);
}
```

- [ ] **Step 8: Create IEntityWatcherService**

```csharp
// boilerplateBE/src/Starter.Abstractions/Capabilities/IEntityWatcherService.cs
namespace Starter.Abstractions.Capabilities;

public interface IEntityWatcherService : ICapability
{
    Task WatchAsync(
        string entityType, Guid entityId, Guid? tenantId,
        Guid userId, WatchReason reason = WatchReason.Explicit,
        CancellationToken ct = default);

    Task UnwatchAsync(
        string entityType, Guid entityId, Guid userId,
        CancellationToken ct = default);

    Task<bool> IsWatchingAsync(
        string entityType, Guid entityId, Guid userId,
        CancellationToken ct = default);

    Task<IReadOnlyList<Guid>> GetWatcherUserIdsAsync(
        string entityType, Guid entityId,
        CancellationToken ct = default);
}
```

- [ ] **Step 9: Verify build**

Run: `cd boilerplateBE && dotnet build src/Starter.Abstractions/Starter.Abstractions.csproj`
Expected: Build succeeded with zero errors. No new project references added.

- [ ] **Step 10: Commit**

```bash
cd boilerplateBE
git add src/Starter.Abstractions/Capabilities/
git commit -m "feat(abstractions): add Comments & Activity capability contracts"
```

---

### Task 2: Null Object Fallbacks + DI Registration

**Files:**
- Create: `boilerplateBE/src/Starter.Infrastructure/Capabilities/NullObjects/NullCommentableEntityRegistry.cs`
- Create: `boilerplateBE/src/Starter.Infrastructure/Capabilities/NullObjects/NullCommentService.cs`
- Create: `boilerplateBE/src/Starter.Infrastructure/Capabilities/NullObjects/NullActivityService.cs`
- Create: `boilerplateBE/src/Starter.Infrastructure/Capabilities/NullObjects/NullEntityWatcherService.cs`
- Modify: `boilerplateBE/src/Starter.Infrastructure/DependencyInjection.cs`

- [ ] **Step 1: Create NullCommentableEntityRegistry**

```csharp
// boilerplateBE/src/Starter.Infrastructure/Capabilities/NullObjects/NullCommentableEntityRegistry.cs
using Starter.Abstractions.Capabilities;

namespace Starter.Infrastructure.Capabilities.NullObjects;

public sealed class NullCommentableEntityRegistry : ICommentableEntityRegistry
{
    public CommentableEntityDefinition? GetDefinition(string entityType) => null;
    public IReadOnlyList<CommentableEntityDefinition> GetAll() => [];
    public IReadOnlyList<string> GetCommentableTypes() => [];
    public IReadOnlyList<string> GetActivityTypes() => [];
    public bool IsCommentable(string entityType) => false;
    public bool HasActivity(string entityType) => false;
}
```

- [ ] **Step 2: Create NullCommentService**

```csharp
// boilerplateBE/src/Starter.Infrastructure/Capabilities/NullObjects/NullCommentService.cs
using Microsoft.Extensions.Logging;
using Starter.Abstractions.Capabilities;

namespace Starter.Infrastructure.Capabilities.NullObjects;

public sealed class NullCommentService(ILogger<NullCommentService> logger) : ICommentService
{
    public Task<Guid> AddCommentAsync(
        string entityType, Guid entityId, Guid? tenantId,
        Guid authorId, string body, string? mentionsJson,
        IReadOnlyList<Guid>? attachmentFileIds,
        Guid? parentCommentId = null,
        CancellationToken ct = default)
    {
        logger.LogDebug("Comment add skipped — CommentsActivity module not installed");
        return Task.FromResult(Guid.Empty);
    }

    public Task EditCommentAsync(Guid commentId, string newBody, string? newMentionsJson, CancellationToken ct = default)
        => Task.CompletedTask;

    public Task DeleteCommentAsync(Guid commentId, Guid deletedBy, CancellationToken ct = default)
        => Task.CompletedTask;

    public Task<CommentSummary?> GetByIdAsync(Guid commentId, CancellationToken ct = default)
        => Task.FromResult<CommentSummary?>(null);

    public Task<IReadOnlyList<CommentSummary>> GetCommentsAsync(
        string entityType, Guid entityId, int pageNumber = 1, int pageSize = 50, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<CommentSummary>>([]);
}
```

- [ ] **Step 3: Create NullActivityService**

```csharp
// boilerplateBE/src/Starter.Infrastructure/Capabilities/NullObjects/NullActivityService.cs
using Microsoft.Extensions.Logging;
using Starter.Abstractions.Capabilities;

namespace Starter.Infrastructure.Capabilities.NullObjects;

public sealed class NullActivityService(ILogger<NullActivityService> logger) : IActivityService
{
    public Task RecordAsync(
        string entityType, Guid entityId, Guid? tenantId,
        string action, Guid? actorId,
        string? metadataJson = null, string? description = null,
        CancellationToken ct = default)
    {
        logger.LogDebug("Activity record skipped — CommentsActivity module not installed");
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<ActivitySummary>> GetActivityAsync(
        string entityType, Guid entityId, int pageNumber = 1, int pageSize = 50, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<ActivitySummary>>([]);
}
```

- [ ] **Step 4: Create NullEntityWatcherService**

```csharp
// boilerplateBE/src/Starter.Infrastructure/Capabilities/NullObjects/NullEntityWatcherService.cs
using Microsoft.Extensions.Logging;
using Starter.Abstractions.Capabilities;

namespace Starter.Infrastructure.Capabilities.NullObjects;

public sealed class NullEntityWatcherService(ILogger<NullEntityWatcherService> logger) : IEntityWatcherService
{
    public Task WatchAsync(string entityType, Guid entityId, Guid? tenantId, Guid userId,
        WatchReason reason = WatchReason.Explicit, CancellationToken ct = default)
        => Task.CompletedTask;

    public Task UnwatchAsync(string entityType, Guid entityId, Guid userId, CancellationToken ct = default)
        => Task.CompletedTask;

    public Task<bool> IsWatchingAsync(string entityType, Guid entityId, Guid userId, CancellationToken ct = default)
        => Task.FromResult(false);

    public Task<IReadOnlyList<Guid>> GetWatcherUserIdsAsync(string entityType, Guid entityId, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<Guid>>([]);
}
```

- [ ] **Step 5: Register null fallbacks in DependencyInjection.cs**

In `boilerplateBE/src/Starter.Infrastructure/DependencyInjection.cs`, inside the `AddCapabilities` method, after the existing `TryAdd*` lines for webhooks/billing/import-export, add:

```csharp
// Comments & Activity — Null Object fallbacks
services.TryAddSingleton<ICommentableEntityRegistry, NullCommentableEntityRegistry>();
services.TryAddScoped<ICommentService, NullCommentService>();
services.TryAddScoped<IActivityService, NullActivityService>();
services.TryAddScoped<IEntityWatcherService, NullEntityWatcherService>();
```

Add the required usings at the top of the file.

- [ ] **Step 6: Verify build**

Run: `cd boilerplateBE && dotnet build src/Starter.Infrastructure/Starter.Infrastructure.csproj`
Expected: Build succeeded.

- [ ] **Step 7: Commit**

```bash
cd boilerplateBE
git add src/Starter.Infrastructure/Capabilities/NullObjects/Null*Service.cs src/Starter.Infrastructure/Capabilities/NullObjects/NullCommentableEntityRegistry.cs src/Starter.Infrastructure/DependencyInjection.cs
git commit -m "feat(infrastructure): add null object fallbacks for Comments & Activity capabilities"
```

---

### Task 3: Extend IRealtimeService with PublishToChannelAsync

**Files:**
- Modify: `boilerplateBE/src/Starter.Application/Common/Interfaces/IRealtimeService.cs`
- Modify: `boilerplateBE/src/Starter.Infrastructure/Services/AblyRealtimeService.cs`
- Modify: `boilerplateBE/src/Starter.Infrastructure/Services/NoOpRealtimeService.cs` (if it exists, otherwise check for the no-op implementation)

- [ ] **Step 1: Add method to IRealtimeService interface**

In `boilerplateBE/src/Starter.Application/Common/Interfaces/IRealtimeService.cs`, add below the existing `PublishToUserAsync` method:

```csharp
Task PublishToChannelAsync(string channel, string eventName, object data, CancellationToken ct = default);
```

- [ ] **Step 2: Implement in AblyRealtimeService**

In `boilerplateBE/src/Starter.Infrastructure/Services/AblyRealtimeService.cs`, add:

```csharp
public async Task PublishToChannelAsync(string channel, string eventName, object data, CancellationToken ct = default)
{
    try
    {
        var url = $"https://rest.ably.io/channels/{Uri.EscapeDataString(channel)}/messages";

        var payload = new
        {
            name = eventName,
            data = JsonSerializer.Serialize(data)
        };

        var content = new StringContent(
            JsonSerializer.Serialize(payload),
            Encoding.UTF8,
            "application/json");

        var response = await _httpClient.PostAsync(url, content, ct);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            _logger.LogWarning(
                "Ably publish failed for channel {Channel}: {StatusCode} - {Body}",
                channel, response.StatusCode, body);
        }
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Failed to publish realtime event to channel {Channel}", channel);
    }
}
```

- [ ] **Step 3: Add no-op implementation**

Find the no-op realtime service (likely `NoOpRealtimeService.cs`) and add:

```csharp
public Task PublishToChannelAsync(string channel, string eventName, object data, CancellationToken ct = default)
    => Task.CompletedTask;
```

- [ ] **Step 4: Verify build**

Run: `cd boilerplateBE && dotnet build`
Expected: Full solution builds with zero errors.

- [ ] **Step 5: Commit**

```bash
cd boilerplateBE
git add src/Starter.Application/Common/Interfaces/IRealtimeService.cs src/Starter.Infrastructure/Services/AblyRealtimeService.cs src/Starter.Infrastructure/Services/NoOpRealtimeService.cs
git commit -m "feat(realtime): add PublishToChannelAsync to IRealtimeService for entity-scoped channels"
```

---

## Phase 2: Module Project & Domain Layer

### Task 4: Create Module Project Skeleton

**Files:**
- Create: `boilerplateBE/src/modules/Starter.Module.CommentsActivity/Starter.Module.CommentsActivity.csproj`
- Create: `boilerplateBE/src/modules/Starter.Module.CommentsActivity/Constants/CommentsActivityPermissions.cs`
- Modify: `boilerplateBE/Starter.sln`

- [ ] **Step 1: Create .csproj file**

```xml
<!-- boilerplateBE/src/modules/Starter.Module.CommentsActivity/Starter.Module.CommentsActivity.csproj -->
<Project Sdk="Microsoft.NET.Sdk">
  <ItemGroup>
    <FrameworkReference Include="Microsoft.AspNetCore.App" />
  </ItemGroup>
  <ItemGroup>
    <PackageReference Include="MassTransit" />
    <PackageReference Include="MassTransit.EntityFrameworkCore" />
    <PackageReference Include="Microsoft.EntityFrameworkCore.Relational" />
    <PackageReference Include="Npgsql.EntityFrameworkCore.PostgreSQL" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\..\Starter.Abstractions.Web\Starter.Abstractions.Web.csproj" />
  </ItemGroup>
</Project>
```

- [ ] **Step 2: Create permission constants**

```csharp
// boilerplateBE/src/modules/Starter.Module.CommentsActivity/Constants/CommentsActivityPermissions.cs
namespace Starter.Module.CommentsActivity.Constants;

public static class CommentsActivityPermissions
{
    public const string ViewComments = "Comments.View";
    public const string CreateComments = "Comments.Create";
    public const string EditComments = "Comments.Edit";
    public const string DeleteComments = "Comments.Delete";
    public const string ManageComments = "Comments.Manage";
    public const string ViewActivity = "Activity.View";
}
```

- [ ] **Step 3: Add project to solution**

Run: `cd boilerplateBE && dotnet sln add src/modules/Starter.Module.CommentsActivity/Starter.Module.CommentsActivity.csproj --solution-folder src/modules`

- [ ] **Step 4: Add project reference from Api to module**

Check how the existing modules are referenced. If the Api project references modules directly, add:

Run: `cd boilerplateBE && dotnet add src/Starter.Api/Starter.Api.csproj reference src/modules/Starter.Module.CommentsActivity/Starter.Module.CommentsActivity.csproj`

- [ ] **Step 5: Verify build**

Run: `cd boilerplateBE && dotnet build`
Expected: Build succeeded.

- [ ] **Step 6: Commit**

```bash
cd boilerplateBE
git add Starter.sln src/modules/Starter.Module.CommentsActivity/ src/Starter.Api/Starter.Api.csproj
git commit -m "feat(comments-activity): scaffold module project with permissions"
```

---

### Task 5: Domain Entities

**Files:**
- Create: `boilerplateBE/src/modules/Starter.Module.CommentsActivity/Domain/Entities/Comment.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.CommentsActivity/Domain/Entities/CommentAttachment.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.CommentsActivity/Domain/Entities/CommentReaction.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.CommentsActivity/Domain/Entities/ActivityEntry.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.CommentsActivity/Domain/Entities/EntityWatcher.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.CommentsActivity/Domain/Enums/WatchReason.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.CommentsActivity/Domain/Errors/CommentErrors.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.CommentsActivity/Domain/Events/CommentCreatedEvent.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.CommentsActivity/Domain/Events/CommentEditedEvent.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.CommentsActivity/Domain/Events/CommentDeletedEvent.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.CommentsActivity/Domain/Events/ReactionToggledEvent.cs`

- [ ] **Step 1: Create Comment entity**

Follow the Product entity pattern exactly. Comment is an AggregateRoot with domain events. Use factory method `Create()` and mutation methods `Edit()`, `SoftDelete()`. Enforce single-level threading in `Create()`: if `parentCommentId` is provided, the caller must validate the parent has no parent itself (validation is in the command handler, not the entity, since the entity doesn't have access to the repository).

Key properties: TenantId, EntityType, EntityId, ParentCommentId, AuthorId, Body, MentionsJson, IsDeleted, DeletedAt, DeletedBy. Navigation: Replies collection, ParentComment.

- [ ] **Step 2: Create CommentAttachment entity**

Simple BaseEntity with CommentId, FileMetadataId, SortOrder.

- [ ] **Step 3: Create CommentReaction entity**

Simple BaseEntity with CommentId, UserId, ReactionType.

- [ ] **Step 4: Create ActivityEntry entity**

BaseEntity + ITenantEntity. NOT an AggregateRoot. Properties: TenantId, EntityType, EntityId, Action, ActorId, MetadataJson, Description.

- [ ] **Step 5: Create EntityWatcher entity**

BaseEntity + ITenantEntity. Properties: TenantId, EntityType, EntityId, UserId, Reason (WatchReason enum).

- [ ] **Step 6: Create WatchReason enum (module-local)**

```csharp
// Domain/Enums/WatchReason.cs
namespace Starter.Module.CommentsActivity.Domain.Enums;

public enum WatchReason
{
    Explicit,
    Participated,
    Mentioned,
    Created
}
```

- [ ] **Step 7: Create error definitions**

Follow the pattern from Product errors. Define: `CommentErrors.NotFound(id)`, `CommentErrors.NotCommentable(entityType)`, `CommentErrors.CannotReplyToReply`, `CommentErrors.NotAuthor`, `CommentErrors.AlreadyDeleted`.

- [ ] **Step 8: Create domain events**

```csharp
// Domain/Events/CommentCreatedEvent.cs
public sealed record CommentCreatedEvent(
    Guid CommentId, string EntityType, Guid EntityId,
    Guid? TenantId, Guid AuthorId,
    string? MentionsJson, Guid? ParentCommentId) : DomainEventBase;

// Domain/Events/CommentEditedEvent.cs
public sealed record CommentEditedEvent(
    Guid CommentId, string EntityType, Guid EntityId,
    Guid? TenantId) : DomainEventBase;

// Domain/Events/CommentDeletedEvent.cs
public sealed record CommentDeletedEvent(
    Guid CommentId, string EntityType, Guid EntityId,
    Guid? TenantId, Guid DeletedBy) : DomainEventBase;

// Domain/Events/ReactionToggledEvent.cs
public sealed record ReactionToggledEvent(
    Guid CommentId, string EntityType, Guid EntityId,
    Guid? TenantId, string ReactionType,
    bool Added) : DomainEventBase;
```

- [ ] **Step 9: Verify build**

Run: `cd boilerplateBE && dotnet build src/modules/Starter.Module.CommentsActivity/Starter.Module.CommentsActivity.csproj`
Expected: Build succeeded.

- [ ] **Step 10: Commit**

```bash
cd boilerplateBE
git add src/modules/Starter.Module.CommentsActivity/Domain/
git commit -m "feat(comments-activity): add domain entities, events, and errors"
```

---

### Task 6: EF Core Configurations & DbContext

**Files:**
- Create: `boilerplateBE/src/modules/Starter.Module.CommentsActivity/Infrastructure/Persistence/CommentsActivityDbContext.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.CommentsActivity/Infrastructure/Configurations/CommentConfiguration.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.CommentsActivity/Infrastructure/Configurations/CommentAttachmentConfiguration.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.CommentsActivity/Infrastructure/Configurations/CommentReactionConfiguration.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.CommentsActivity/Infrastructure/Configurations/ActivityEntryConfiguration.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.CommentsActivity/Infrastructure/Configurations/EntityWatcherConfiguration.cs`

- [ ] **Step 1: Create CommentsActivityDbContext**

Follow `ProductsDbContext` exactly: inject `ICurrentUserService`, apply global tenant query filters for Comment, ActivityEntry, and EntityWatcher. Migration history table: `__EFMigrationsHistory_CommentsActivity`. Apply configurations from assembly.

DbSets: Comments, CommentAttachments, CommentReactions, ActivityEntries, EntityWatchers.

- [ ] **Step 2: Create CommentConfiguration**

Follow `ProductConfiguration` pattern: snake_case columns, table name `comments`. Configure all properties with column names, max lengths, required flags. Self-referencing FK for ParentCommentId. HasMany(Replies) with FK(ParentCommentId). Indexes on (EntityType, EntityId), (ParentCommentId), (TenantId), (AuthorId).

- [ ] **Step 3: Create CommentAttachmentConfiguration**

Table `comment_attachments`. FK to comments. Index on (CommentId).

- [ ] **Step 4: Create CommentReactionConfiguration**

Table `comment_reactions`. FK to comments. Unique index on (CommentId, UserId, ReactionType).

- [ ] **Step 5: Create ActivityEntryConfiguration**

Table `activity_entries`. Indexes on (EntityType, EntityId, CreatedAt desc), (TenantId), (ActorId).

- [ ] **Step 6: Create EntityWatcherConfiguration**

Table `entity_watchers`. Unique index on (EntityType, EntityId, UserId). Index on (UserId), (TenantId). WatchReason as string conversion.

- [ ] **Step 7: Create initial migration**

Run: `cd boilerplateBE && dotnet ef migrations add InitialCommentsActivity --project src/modules/Starter.Module.CommentsActivity --startup-project src/Starter.Api --context CommentsActivityDbContext --output-dir Infrastructure/Persistence/Migrations`
Expected: Migration files created in the module's Migrations folder.

- [ ] **Step 8: Verify build**

Run: `cd boilerplateBE && dotnet build`
Expected: Build succeeded.

- [ ] **Step 9: Commit**

```bash
cd boilerplateBE
git add src/modules/Starter.Module.CommentsActivity/Infrastructure/
git commit -m "feat(comments-activity): add DbContext, EF configurations, and initial migration"
```

---

## Phase 3: Application Layer (CQRS)

### Task 7: DTOs

**Files:**
- Create all files in `boilerplateBE/src/modules/Starter.Module.CommentsActivity/Application/DTOs/`

- [ ] **Step 1: Create all DTOs**

```csharp
// Application/DTOs/ReactionSummaryDto.cs
public sealed record ReactionSummaryDto(string ReactionType, int Count, bool UserReacted);

// Application/DTOs/CommentAttachmentDto.cs
public sealed record CommentAttachmentDto(
    Guid Id, Guid FileMetadataId, string FileName,
    string ContentType, long Size, string? Url);

// Application/DTOs/CommentDto.cs
public sealed record CommentDto(
    Guid Id, string EntityType, Guid EntityId,
    Guid? ParentCommentId, Guid AuthorId,
    string AuthorName, string AuthorEmail,
    string Body, List<MentionRefDto>? Mentions,
    List<CommentAttachmentDto> Attachments,
    List<ReactionSummaryDto> Reactions,
    bool IsDeleted, List<CommentDto>? Replies,
    DateTime CreatedAt, DateTime? ModifiedAt);

public sealed record MentionRefDto(Guid UserId, string Username, string DisplayName);

// Application/DTOs/ActivityEntryDto.cs
public sealed record ActivityEntryDto(
    Guid Id, string EntityType, Guid EntityId,
    string Action, Guid? ActorId, string? ActorName,
    string? MetadataJson, string? Description,
    DateTime CreatedAt);

// Application/DTOs/TimelineItemDto.cs
public sealed record TimelineItemDto(
    string Type, // "comment" or "activity"
    CommentDto? Comment,
    ActivityEntryDto? Activity,
    DateTime Timestamp);

// Application/DTOs/WatchStatusDto.cs
public sealed record WatchStatusDto(bool IsWatching, int WatcherCount);

// Application/DTOs/MentionableUserDto.cs
public sealed record MentionableUserDto(Guid Id, string Username, string DisplayName, string Email);
```

- [ ] **Step 2: Verify build**

Run: `cd boilerplateBE && dotnet build src/modules/Starter.Module.CommentsActivity/Starter.Module.CommentsActivity.csproj`

- [ ] **Step 3: Commit**

```bash
cd boilerplateBE
git add src/modules/Starter.Module.CommentsActivity/Application/DTOs/
git commit -m "feat(comments-activity): add application DTOs"
```

---

### Task 8: Commands — AddComment, EditComment, DeleteComment

**Files:**
- Create: `Application/Commands/AddComment/AddCommentCommand.cs` (command + handler + validator in one file for brevity, or separate — follow the existing pattern of separate files per Products module)

- [ ] **Step 1: Create AddCommentCommand + Handler + Validator**

Command: `sealed record AddCommentCommand(string EntityType, Guid EntityId, string Body, List<Guid>? MentionUserIds, Guid? ParentCommentId, List<Guid>? AttachmentFileIds) : IRequest<Result<Guid>>`

Handler: Validates entity type is commentable via `ICommentableEntityRegistry`. If `ParentCommentId` set, loads parent and checks it has no parent itself (single-level enforcement). Creates Comment entity, adds attachments, saves to `CommentsActivityDbContext`. Returns the new comment ID.

Validator: Body NotEmpty, MaxLength(10000). EntityType NotEmpty, MaxLength(100). EntityId NotEmpty.

- [ ] **Step 2: Create EditCommentCommand + Handler + Validator**

Command: `sealed record EditCommentCommand(Guid Id, string Body, List<Guid>? MentionUserIds) : IRequest<Result>`

Handler: Loads comment, checks ownership (AuthorId == current user OR Comments.Manage permission), calls `comment.Edit()`, saves.

Validator: Body NotEmpty, MaxLength(10000).

- [ ] **Step 3: Create DeleteCommentCommand + Handler**

Command: `sealed record DeleteCommentCommand(Guid Id) : IRequest<Result>`

Handler: Loads comment, checks ownership, calls `comment.SoftDelete(currentUserId)`, saves.

- [ ] **Step 4: Verify build**

Run: `cd boilerplateBE && dotnet build src/modules/Starter.Module.CommentsActivity/Starter.Module.CommentsActivity.csproj`

- [ ] **Step 5: Commit**

```bash
cd boilerplateBE
git add src/modules/Starter.Module.CommentsActivity/Application/Commands/
git commit -m "feat(comments-activity): add comment CRUD commands with validation"
```

---

### Task 9: Commands — ToggleReaction, WatchEntity, UnwatchEntity

**Files:**
- Create: `Application/Commands/ToggleReaction/ToggleReactionCommand.cs`
- Create: `Application/Commands/WatchEntity/WatchEntityCommand.cs`
- Create: `Application/Commands/UnwatchEntity/UnwatchEntityCommand.cs`

- [ ] **Step 1: Create ToggleReactionCommand + Handler**

Command: `sealed record ToggleReactionCommand(Guid CommentId, string ReactionType) : IRequest<Result>`

Handler: Check if reaction exists for (CommentId, UserId, ReactionType). If exists → remove. If not → create. Raise `ReactionToggledEvent`. Save.

- [ ] **Step 2: Create WatchEntityCommand + Handler**

Command: `sealed record WatchEntityCommand(string EntityType, Guid EntityId) : IRequest<Result>`

Handler: Check if already watching. If not, create EntityWatcher with Reason=Explicit. Save.

- [ ] **Step 3: Create UnwatchEntityCommand + Handler**

Command: `sealed record UnwatchEntityCommand(string EntityType, Guid EntityId) : IRequest<Result>`

Handler: Find and remove EntityWatcher for current user. Save.

- [ ] **Step 4: Verify build and commit**

```bash
cd boilerplateBE && dotnet build src/modules/Starter.Module.CommentsActivity/Starter.Module.CommentsActivity.csproj
git add src/modules/Starter.Module.CommentsActivity/Application/Commands/
git commit -m "feat(comments-activity): add reaction toggle and watch/unwatch commands"
```

---

### Task 10: Queries — GetComments, GetTimeline, GetActivity, GetWatchStatus, GetMentionableUsers

**Files:**
- Create all query files under `Application/Queries/`

- [ ] **Step 1: Create GetCommentsQuery + Handler**

Query: `sealed record GetCommentsQuery(string EntityType, Guid EntityId, int PageNumber = 1, int PageSize = 50) : IRequest<Result<PaginatedList<CommentDto>>>`

Handler: Load top-level comments (ParentCommentId == null) for the entity, ordered by CreatedAt ASC. Include replies (ordered by CreatedAt ASC). Map to CommentDto using `IUserReader.GetManyAsync()` to resolve author names. Aggregate reactions per comment. Build attachment DTOs.

- [ ] **Step 2: Create GetActivityQuery + Handler**

Query: `sealed record GetActivityQuery(string EntityType, Guid EntityId, int PageNumber = 1, int PageSize = 50) : IRequest<Result<PaginatedList<ActivityEntryDto>>>`

Handler: Load activity entries ordered by CreatedAt ASC. Resolve actor names via `IUserReader`.

- [ ] **Step 3: Create GetTimelineQuery + Handler**

Query: `sealed record GetTimelineQuery(string EntityType, Guid EntityId, string Filter = "all", int PageNumber = 1, int PageSize = 50) : IRequest<Result<PaginatedList<TimelineItemDto>>>`

Handler: Based on filter, load comments and/or activity entries. Merge into a single list sorted by timestamp ASC. Map to `TimelineItemDto` (discriminated by Type field).

- [ ] **Step 4: Create GetWatchStatusQuery + Handler**

Query: `sealed record GetWatchStatusQuery(string EntityType, Guid EntityId) : IRequest<Result<WatchStatusDto>>`

Handler: Check if current user is watching. Count total watchers. Return `WatchStatusDto`.

- [ ] **Step 5: Create GetMentionableUsersQuery + Handler**

Query: `sealed record GetMentionableUsersQuery(string? Search, int PageSize = 10) : IRequest<Result<List<MentionableUserDto>>>`

Handler: Use `IUserReader` to search users in the current tenant by name/email. Return top N matches.

- [ ] **Step 6: Verify build and commit**

```bash
cd boilerplateBE && dotnet build src/modules/Starter.Module.CommentsActivity/Starter.Module.CommentsActivity.csproj
git add src/modules/Starter.Module.CommentsActivity/Application/Queries/
git commit -m "feat(comments-activity): add queries for comments, timeline, activity, watchers, and mentions"
```

---

### Task 11: Event Handlers

**Files:**
- Create: `Application/EventHandlers/AutoWatchOnComment.cs`
- Create: `Application/EventHandlers/AutoWatchOnMention.cs`
- Create: `Application/EventHandlers/NotifyWatchersOnCommentCreated.cs`
- Create: `Application/EventHandlers/RecordCommentActivity.cs`
- Create: `Application/EventHandlers/PublishCommentRealtimeEvent.cs`

- [ ] **Step 1: Create AutoWatchOnComment**

Handles `CommentCreatedEvent`. Checks `ICommentableEntityRegistry` for `AutoWatchOnComment` flag. If true, adds author as watcher with Reason=Participated via `CommentsActivityDbContext` directly (not via capability — this runs inside the module).

- [ ] **Step 2: Create AutoWatchOnMention**

Handles `CommentCreatedEvent`. Parses `MentionsJson` to extract user IDs. Adds each as watcher with Reason=Mentioned.

- [ ] **Step 3: Create NotifyWatchersOnCommentCreated**

Handles `CommentCreatedEvent`. Gets all watcher user IDs for the entity. Excludes the author. Creates notifications via `INotificationService.CreateAsync()` for each watcher. Uses notification type `"CommentCreated"`.

- [ ] **Step 4: Create RecordCommentActivity**

Handles `CommentCreatedEvent`. Records a `comment_added` activity entry via the `CommentsActivityDbContext` directly.

- [ ] **Step 5: Create PublishCommentRealtimeEvent**

Handles `CommentCreatedEvent`, `CommentEditedEvent`, `CommentDeletedEvent`, `ReactionToggledEvent`. Publishes to entity Ably channel `entity-{tenantId}-{entityType}-{entityId}` via `IRealtimeService.PublishToChannelAsync()`. Event names: `comment:created`, `comment:updated`, `comment:deleted`, `reaction:changed`.

- [ ] **Step 6: Verify build and commit**

```bash
cd boilerplateBE && dotnet build src/modules/Starter.Module.CommentsActivity/Starter.Module.CommentsActivity.csproj
git add src/modules/Starter.Module.CommentsActivity/Application/EventHandlers/
git commit -m "feat(comments-activity): add event handlers for notifications, auto-watch, activity recording, and real-time"
```

---

### Task 12: Capability Service Implementations

**Files:**
- Create: `Infrastructure/Services/CommentableEntityRegistry.cs`
- Create: `Infrastructure/Services/CommentService.cs`
- Create: `Infrastructure/Services/ActivityService.cs`
- Create: `Infrastructure/Services/EntityWatcherService.cs`

- [ ] **Step 1: Create CommentableEntityRegistry**

Collects `ICommentableEntityRegistration` services from DI. Stores definitions in a dictionary keyed by EntityType. Implements all `ICommentableEntityRegistry` methods.

- [ ] **Step 2: Create CommentService**

Implements `ICommentService`. Delegates to `CommentsActivityDbContext` for all operations. Maps entities to `CommentSummary` DTOs for the capability contract.

- [ ] **Step 3: Create ActivityService**

Implements `IActivityService`. Creates `ActivityEntry` entities in `CommentsActivityDbContext`. Maps to `ActivitySummary` DTOs.

- [ ] **Step 4: Create EntityWatcherService**

Implements `IEntityWatcherService`. CRUD on `EntityWatcher` entities.

- [ ] **Step 5: Verify build and commit**

```bash
cd boilerplateBE && dotnet build src/modules/Starter.Module.CommentsActivity/Starter.Module.CommentsActivity.csproj
git add src/modules/Starter.Module.CommentsActivity/Infrastructure/Services/
git commit -m "feat(comments-activity): add capability service implementations"
```

---

### Task 13: Controller

**Files:**
- Create: `boilerplateBE/src/modules/Starter.Module.CommentsActivity/Controllers/CommentsActivityController.cs`

- [ ] **Step 1: Create CommentsActivityController**

Follow `ProductsController` pattern exactly. Inherits `Starter.Abstractions.Web.BaseApiController(ISender)`.

Endpoints:
- `GET /comments` — GetComments query, `[Authorize(Policy = CommentsActivityPermissions.ViewComments)]`
- `POST /comments` — AddComment command, `[Authorize(Policy = CommentsActivityPermissions.CreateComments)]`
- `PUT /comments/{id}` — EditComment command, `[Authorize(Policy = CommentsActivityPermissions.EditComments)]`
- `DELETE /comments/{id}` — DeleteComment command, `[Authorize(Policy = CommentsActivityPermissions.DeleteComments)]`
- `POST /comments/{id}/reactions` — ToggleReaction command, `[Authorize(Policy = CommentsActivityPermissions.CreateComments)]`
- `DELETE /comments/{id}/reactions/{reactionType}` — ToggleReaction (remove), same permission
- `GET /activity` — GetActivity query, `[Authorize(Policy = CommentsActivityPermissions.ViewActivity)]`
- `GET /timeline` — GetTimeline query, `[Authorize(Policy = CommentsActivityPermissions.ViewComments)]`
- `GET /watchers/status` — GetWatchStatus query, `[Authorize(Policy = CommentsActivityPermissions.ViewComments)]`
- `POST /watchers` — WatchEntity command, `[Authorize(Policy = CommentsActivityPermissions.ViewComments)]`
- `DELETE /watchers` — UnwatchEntity command, `[Authorize(Policy = CommentsActivityPermissions.ViewComments)]`
- `GET /mentionable-users` — GetMentionableUsers query, `[Authorize(Policy = CommentsActivityPermissions.CreateComments)]`

- [ ] **Step 2: Verify build and commit**

```bash
cd boilerplateBE && dotnet build src/modules/Starter.Module.CommentsActivity/Starter.Module.CommentsActivity.csproj
git add src/modules/Starter.Module.CommentsActivity/Controllers/
git commit -m "feat(comments-activity): add API controller with all endpoints"
```

---

### Task 14: Module Registration (IModule)

**Files:**
- Create: `boilerplateBE/src/modules/Starter.Module.CommentsActivity/CommentsActivityModule.cs`

- [ ] **Step 1: Create CommentsActivityModule**

Follow `ProductsModule` exactly. Register:
- `CommentsActivityDbContext` with Npgsql, migration history table `__EFMigrationsHistory_CommentsActivity`
- `ICommentableEntityRegistry` as singleton (collecting `ICommentableEntityRegistration` services)
- `ICommentService` as scoped `CommentService`
- `IActivityService` as scoped `ActivityService`
- `IEntityWatcherService` as scoped `EntityWatcherService`

Implement `GetPermissions()`: yield all 6 permissions from `CommentsActivityPermissions`.

Implement `GetDefaultRolePermissions()`:
- SuperAdmin: all permissions
- Admin: all permissions
- User: View, Create, Edit, Delete (own), ViewActivity

Implement `MigrateAsync()`: migrate `CommentsActivityDbContext`.

- [ ] **Step 2: Verify the full backend builds and the module loads**

Run: `cd boilerplateBE && dotnet build`
Expected: Build succeeded.

- [ ] **Step 3: Commit**

```bash
cd boilerplateBE
git add src/modules/Starter.Module.CommentsActivity/CommentsActivityModule.cs
git commit -m "feat(comments-activity): add IModule implementation with DI registration"
```

---

## Phase 4: Products Module Integration

### Task 15: Register Products as Commentable + Activity Event Handler

**Files:**
- Modify: `boilerplateBE/src/modules/Starter.Module.Products/ProductsModule.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.Products/Application/EventHandlers/RecordProductActivity.cs`

- [ ] **Step 1: Add ICommentableEntityRegistration to ProductsModule.ConfigureServices**

After the existing `services.AddScoped<IUsageMetricCalculator, ProductsUsageMetricCalculator>();` line, add:

```csharp
services.AddSingleton<ICommentableEntityRegistration>(
    new CommentableEntityRegistration(new CommentableEntityDefinition(
        EntityType: "Product",
        DisplayNameKey: "commentsActivity.entityTypes.product",
        EnableComments: true,
        EnableActivity: true,
        CustomActivityTypes: ["PriceChanged", "Published", "Archived"],
        AutoWatchOnCreate: true,
        AutoWatchOnComment: true)));
```

Add the required using for `Starter.Abstractions.Capabilities`.

- [ ] **Step 2: Create RecordProductActivity event handler**

```csharp
// boilerplateBE/src/modules/Starter.Module.Products/Application/EventHandlers/RecordProductActivity.cs
using System.Text.Json;
using MediatR;
using Starter.Abstractions.Capabilities;
using Starter.Module.Products.Domain.Events;

namespace Starter.Module.Products.Application.EventHandlers;

internal sealed class RecordProductActivity(IActivityService activityService)
    : INotificationHandler<ProductCreatedEvent>
{
    public async Task Handle(ProductCreatedEvent notification, CancellationToken cancellationToken)
    {
        await activityService.RecordAsync(
            "Product",
            notification.ProductId,
            notification.TenantId,
            "created",
            actorId: null,
            metadataJson: JsonSerializer.Serialize(new { notification.Name, notification.Slug }),
            description: $"Product \"{notification.Name}\" was created",
            cancellationToken);
    }
}
```

- [ ] **Step 3: Verify build and commit**

```bash
cd boilerplateBE && dotnet build
git add src/modules/Starter.Module.Products/
git commit -m "feat(products): register as commentable entity with activity recording"
```

---

## Phase 5: Backend Smoke Test

### Task 16: Run Backend and Verify Module Loads

- [ ] **Step 1: Start Docker services**

Run: `cd boilerplateBE && docker compose up -d`

- [ ] **Step 2: Run the backend**

Run: `cd boilerplateBE/src/Starter.Api && dotnet run --launch-profile http`

Expected: Application starts, CommentsActivity module is discovered and loaded, migration runs, tables are created in PostgreSQL.

- [ ] **Step 3: Verify API endpoints exist in Swagger**

Navigate to `http://localhost:5000/swagger`. Verify that `CommentsActivity` controller endpoints appear.

- [ ] **Step 4: Test basic comment creation via curl or Swagger**

Test the POST `/api/v1/CommentsActivity/comments` endpoint with a valid entityType ("Product") and entityId. Verify 201 response.

- [ ] **Step 5: Stop backend, commit any fixes if needed**

---

## Phase 6: Frontend — Foundation

### Task 17: Install react-markdown + Types + Config

**Files:**
- Modify: `boilerplateFE/package.json` (install react-markdown)
- Create: `boilerplateFE/src/types/comments-activity.types.ts`
- Modify: `boilerplateFE/src/config/api.config.ts`
- Modify: `boilerplateFE/src/lib/query/keys.ts`
- Modify: `boilerplateFE/src/constants/permissions.ts`

- [ ] **Step 1: Install react-markdown**

Run: `cd boilerplateFE && npm install react-markdown`

- [ ] **Step 2: Create TypeScript types**

Create `boilerplateFE/src/types/comments-activity.types.ts` with all interfaces from the spec: Comment, MentionRef, CommentAttachmentDto, ReactionSummary, ActivityEntry, TimelineItem, WatchStatus, CreateCommentData, EditCommentData, ToggleReactionData.

- [ ] **Step 3: Add API endpoints to api.config.ts**

Add `COMMENTS_ACTIVITY` section following the PRODUCTS pattern.

- [ ] **Step 4: Add query keys**

Add `commentsActivity` section to `queryKeys` in `keys.ts` with comments, activity, timeline, and watchers sub-keys.

- [ ] **Step 5: Add permissions**

Add to `permissions.ts`:
```typescript
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

- [ ] **Step 6: Verify build**

Run: `cd boilerplateFE && npm run build`
Expected: Build succeeded.

- [ ] **Step 7: Commit**

```bash
cd boilerplateFE
git add src/types/comments-activity.types.ts src/config/api.config.ts src/lib/query/keys.ts src/constants/permissions.ts package.json package-lock.json
git commit -m "feat(fe): add comments-activity types, API config, query keys, and permissions"
```

---

### Task 18: API Layer + Query Hooks

**Files:**
- Create: `boilerplateFE/src/features/comments-activity/api/comments-activity.api.ts`
- Create: `boilerplateFE/src/features/comments-activity/api/comments-activity.queries.ts`
- Create: `boilerplateFE/src/features/comments-activity/api/index.ts`

- [ ] **Step 1: Create API calls**

Follow `products.api.ts` pattern. Create `commentsActivityApi` object with methods: getTimeline, getComments, addComment, editComment, deleteComment, toggleReaction, removeReaction, getWatchStatus, watch, unwatch, getMentionableUsers.

- [ ] **Step 2: Create query hooks**

Follow `products.queries.ts` pattern. Create: useTimeline, useComments, useWatchStatus, useMentionableUsers (queries), useAddComment, useEditComment, useDeleteComment, useToggleReaction, useWatch, useUnwatch (mutations). Mutations invalidate relevant query keys on success and show toast messages.

- [ ] **Step 3: Create index.ts re-export**

- [ ] **Step 4: Verify build and commit**

```bash
cd boilerplateFE && npm run build
git add src/features/comments-activity/api/
git commit -m "feat(fe): add comments-activity API layer and query hooks"
```

---

### Task 19: Ably Entity Channel Hook

**Files:**
- Create: `boilerplateFE/src/features/comments-activity/hooks/useEntityChannel.ts`

- [ ] **Step 1: Create useEntityChannel hook**

Follow `useAblyNotifications.ts` pattern. Subscribe to channel `entity-${tenantId}-${entityType}-${entityId}`. Listen for: `comment:created`, `comment:updated`, `comment:deleted`, `activity:created`, `reaction:changed`. On each event, invalidate the relevant commentsActivity query keys. Return `{ connected }` state. Clean up on unmount.

- [ ] **Step 2: Verify build and commit**

```bash
cd boilerplateFE && npm run build
git add src/features/comments-activity/hooks/
git commit -m "feat(fe): add Ably entity channel hook for real-time updates"
```

---

## Phase 7: Frontend — Components

### Task 20: ActivityItem Component

**Files:**
- Create: `boilerplateFE/src/features/comments-activity/components/ActivityItem.tsx`

- [ ] **Step 1: Create ActivityItem**

Renders a single activity entry. Shows: colored dot (by action type), description text, actor name, relative timestamp. Use existing `UserAvatar` if actor is available. Use `date-fns` for relative time formatting. Action types map to icons/colors (created=green, updated=blue, deleted=red, comment_added=gray).

- [ ] **Step 2: Verify build and commit**

---

### Task 21: ReactionPicker + MentionAutocomplete Components

**Files:**
- Create: `boilerplateFE/src/features/comments-activity/components/ReactionPicker.tsx`
- Create: `boilerplateFE/src/features/comments-activity/components/MentionAutocomplete.tsx`

- [ ] **Step 1: Create ReactionPicker**

A popover with a grid of preset emoji buttons: thumbsup, thumbsdown, heart, rocket, eyes, party_popper. Each button calls `useToggleReaction` mutation. Show existing reactions as small pills below the comment with count and whether the current user reacted.

- [ ] **Step 2: Create MentionAutocomplete**

A positioned overlay triggered by `@` in the CommentComposer textarea. Uses `useMentionableUsers` query with debounced search. Shows list of matching users with UserAvatar + name + email. On selection, inserts `@[Display Name](userId)` into the textarea at cursor position.

- [ ] **Step 3: Verify build and commit**

---

### Task 22: CommentItem Component

**Files:**
- Create: `boilerplateFE/src/features/comments-activity/components/CommentItem.tsx`

- [ ] **Step 1: Create CommentItem**

Props: `comment: Comment`, `isReply?: boolean`, `onReply?: (commentId: string) => void`.

Renders: UserAvatar + author name + relative timestamp. Markdown body via `react-markdown`. Attachment list (file name + size, clickable). Reaction bar (existing reactions with counts, "+" button opens ReactionPicker). Edit/Delete dropdown menu (shown if current user is author OR has Comments.Manage). Reply button (only on top-level, not replies). Soft-deleted state: show `[This comment has been deleted]` dimmed.

Edit mode: inline CommentComposer replacing the body, with save/cancel.

- [ ] **Step 2: Verify build and commit**

---

### Task 23: CommentComposer Component

**Files:**
- Create: `boilerplateFE/src/features/comments-activity/components/CommentComposer.tsx`

- [ ] **Step 1: Create CommentComposer**

Props: `entityType: string`, `entityId: string`, `parentCommentId?: string`, `onCancel?: () => void`, `editMode?: { commentId: string, initialBody: string }`.

Features:
- Textarea with placeholder "Write a comment..."
- `@` keystroke triggers MentionAutocomplete
- File attachment button using existing file upload pattern (upload temp, collect IDs)
- Reply mode: "Replying to {name}" banner with cancel button
- Edit mode: pre-filled body, save/cancel buttons
- Submit calls `useAddComment` or `useEditComment` mutation
- Zod validation: body required, max 10000 chars
- Markdown preview toggle button

- [ ] **Step 2: Verify build and commit**

---

### Task 24: CommentThread Component

**Files:**
- Create: `boilerplateFE/src/features/comments-activity/components/CommentThread.tsx`

- [ ] **Step 1: Create CommentThread**

Props: `comment: Comment` (a top-level comment with replies array).

Renders the top-level comment via `<CommentItem>`. Below it, indented with left border, renders each reply via `<CommentItem isReply>`. When the Reply button is clicked on the top-level comment, shows an inline `<CommentComposer>` with `parentCommentId` set.

- [ ] **Step 2: Verify build and commit**

---

### Task 25: WatchButton Component

**Files:**
- Create: `boilerplateFE/src/features/comments-activity/components/WatchButton.tsx`

- [ ] **Step 1: Create WatchButton**

Props: `entityType: string`, `entityId: string`.

Uses `useWatchStatus` query. Shows Eye icon + "Watch" or "Watching" text + watcher count badge. Click toggles via `useWatch` / `useUnwatch` mutations.

- [ ] **Step 2: Verify build and commit**

---

### Task 26: EntityTimeline — Main Component

**Files:**
- Create: `boilerplateFE/src/features/comments-activity/components/EntityTimeline.tsx`

- [ ] **Step 1: Create EntityTimeline**

Props: `entityType: string`, `entityId: string`.

This is the main component rendered via the slot system. Layout:
1. Header: title "Comments & Activity" + `<WatchButton>` on the right
2. Filter toggle: three buttons — All | Comments | Activity (state stored locally)
3. Timeline: uses `useTimeline(entityType, entityId, { filter })`. Maps each item to either `<CommentThread>` or `<ActivityItem>` based on `item.type`.
4. Pagination: "Load more" button at the bottom
5. CommentComposer: at the bottom for adding new comments
6. Real-time: calls `useEntityChannel` hook with current tenant, entityType, entityId
7. Empty state: `<EmptyState>` when no items, with appropriate icon and message
8. Permission check: conditionally show CommentComposer only if `hasPermission(Comments.Create)`

- [ ] **Step 2: Verify build and commit**

---

### Task 27: Module Registration + Slot Integration

**Files:**
- Create: `boilerplateFE/src/features/comments-activity/index.ts`
- Modify: `boilerplateFE/src/lib/extensions/slot-map.ts`
- Modify: `boilerplateFE/src/config/modules.config.ts`
- Modify: `boilerplateFE/src/features/products/pages/ProductDetailPage.tsx`

- [ ] **Step 1: Add slot to slot-map.ts**

Add to the `SlotMap` interface:

```typescript
'entity-detail-timeline': {
  entityType: string;
  entityId: string;
};
```

- [ ] **Step 2: Create module index.ts**

```typescript
// boilerplateFE/src/features/comments-activity/index.ts
import { lazy } from 'react';
import { registerSlot } from '@/lib/extensions';

const EntityTimeline = lazy(() =>
  import('./components/EntityTimeline').then((m) => ({ default: m.EntityTimeline })),
);

export const commentsActivityModule = {
  name: 'commentsActivity',
  register(): void {
    registerSlot('entity-detail-timeline', {
      id: 'commentsActivity.entity-timeline',
      module: 'commentsActivity',
      order: 10,
      permission: 'Comments.View',
      component: EntityTimeline,
    });
  },
};
```

- [ ] **Step 3: Register module in modules.config.ts**

Add `commentsActivity: true` to `activeModules`. Import `commentsActivityModule` and add to `enabledModules` array.

- [ ] **Step 4: Add Slot render to ProductDetailPage**

Find the appropriate location in `ProductDetailPage.tsx` (after the main content, or as a tab) and add:

```tsx
import { Slot } from '@/lib/extensions';

// In the JSX, after the product detail content:
<Slot id="entity-detail-timeline" props={{ entityType: "Product", entityId: id }} />
```

- [ ] **Step 5: Verify build**

Run: `cd boilerplateFE && npm run build`

- [ ] **Step 6: Commit**

```bash
cd boilerplateFE
git add src/features/comments-activity/index.ts src/lib/extensions/slot-map.ts src/config/modules.config.ts src/features/products/pages/ProductDetailPage.tsx
git commit -m "feat(fe): register comments-activity module with slot integration in ProductDetailPage"
```

---

### Task 28: i18n Translations

**Files:**
- Modify: `boilerplateFE/src/i18n/locales/en/translation.json`
- Modify: `boilerplateFE/src/i18n/locales/ar/translation.json`

- [ ] **Step 1: Add English translations**

Add `commentsActivity` section with keys for: title, comments, activity, all, writeComment, reply, edit, delete, deleteConfirm, deleted, watch, watching, watcherCount, addReaction, noComments, noActivity, noTimeline, loadMore, edited, mentionPlaceholder, attachFile, preview, entityTypes (product, etc.).

- [ ] **Step 2: Add Arabic translations**

Mirror all English keys with Arabic translations.

- [ ] **Step 3: Verify build and commit**

```bash
cd boilerplateFE && npm run build
git add src/i18n/
git commit -m "feat(fe): add i18n translations for comments-activity module"
```

---

## Phase 8: End-to-End Verification

### Task 29: Full Stack Smoke Test

- [ ] **Step 1: Start all services**

```bash
cd boilerplateBE && docker compose up -d
cd boilerplateBE/src/Starter.Api && dotnet run --launch-profile http &
cd boilerplateFE && npm run dev &
```

- [ ] **Step 2: Navigate to a Product detail page**

Login as `superadmin@starter.com` / `Admin@123456`. Navigate to Products, select a product. Verify the EntityTimeline component appears below the product details.

- [ ] **Step 3: Test comment CRUD**

- Add a comment → appears in timeline
- Reply to the comment → appears indented
- Edit the comment → body updates
- Delete the comment → shows "[deleted]" placeholder
- Toggle a reaction → reaction appears with count

- [ ] **Step 4: Test activity feed**

- Create a new product → "created" activity appears in product timeline
- Update the product → verify activity appears
- Toggle filter buttons: All, Comments, Activity — verify filtering works

- [ ] **Step 5: Test watch/unwatch**

- Click Watch → button changes to "Watching"
- Unwatch → reverts
- Add a comment → auto-watch should activate

- [ ] **Step 6: Test real-time (if Ably configured)**

Open same product in two browser tabs. Add a comment in one → should appear in the other.

- [ ] **Step 7: Test permissions**

Login as a regular User role. Verify they can view and create comments but cannot see Manage (edit/delete other users' comments).

- [ ] **Step 8: Fix any issues found, commit**

---

### Task 30: Final Build Verification

- [ ] **Step 1: Backend full build**

Run: `cd boilerplateBE && dotnet build`
Expected: Zero errors, zero warnings related to CommentsActivity.

- [ ] **Step 2: Frontend full build**

Run: `cd boilerplateFE && npm run build`
Expected: Zero errors.

- [ ] **Step 3: Final commit with any remaining fixes**
