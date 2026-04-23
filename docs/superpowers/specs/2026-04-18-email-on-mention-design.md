# Email-on-Mention via Communication Module

**Date:** 2026-04-18
**Status:** Approved design
**Depends on:** Comments & Activity module (merged), Communication module (merged, optional at runtime)

## Problem

When a user is @mentioned in a comment, they receive an in-app notification (bell icon). There is no email notification — the user only knows they were mentioned if they happen to check the app. High-signal events like mentions should optionally reach users via email, but only when the Communication module is installed and the user has opted in.

## Solution

Add an email dispatch path to the existing mention notification flow. The email is **additive** — the in-app notification continues to work unchanged via the existing `NotifyMentionedUsersOnCommentCreatedHandler`. A new, separate MediatR handler calls `IMessageDispatcher.SendAsync` for each mentioned user. When Communication is absent, the `NullMessageDispatcher` silently returns `Guid.Empty` — no error, no log noise beyond debug level.

Users control whether they receive mention emails via a toggle in their existing Profile → Notification Preferences panel. When Communication is not installed, the email toggle renders as disabled with an explanatory tooltip.

## Architecture

### Handler: `EmailMentionedUsersOnCommentCreatedHandler`

**Location:** `Starter.Module.CommentsActivity/Application/EventHandlers/EmailMentionedUsersOnCommentCreatedHandler.cs`

**Pattern:** Third `INotificationHandler<CommentCreatedEvent>` handler, alongside:
- `NotifyMentionedUsersOnCommentCreatedHandler` — in-app notification (existing, unchanged)
- `NotifyWatchersOnCommentCreatedHandler` — watcher notifications (existing, unchanged)

**Constructor dependencies** (all from `Starter.Abstractions`):
- `IMessageDispatcher` — the Communication capability; Null Object when module absent
- `IUserReader` — resolve mentioned user IDs to `UserReaderDto` (for display name + tenant)
- `INotificationPreferenceReader` — check if recipient has email enabled for `CommentMention` type
- `ILogger<EmailMentionedUsersOnCommentCreatedHandler>`

**Flow:**
1. Parse `MentionsJson` → `List<Guid>` (bail on parse failure, same pattern as existing handler).
2. Exclude self-mentions (`authorId`).
3. Resolve recipients via `IUserReader.GetManyAsync`.
4. For each recipient:
   a. Check `INotificationPreferenceReader.IsEmailEnabledAsync(recipientId, "CommentMention")` — skip if false.
   b. Call `IMessageDispatcher.SendAsync("notification.comment-mention", recipientId, variables, recipient.TenantId)`.
   c. Catch exceptions per-recipient, log as warning — a failed email must never block the loop or affect the in-app handler.

**Template variables passed to `SendAsync`:**
| Variable | Type | Example |
|---|---|---|
| `mentionerName` | string | `"Saman Jasim"` |
| `entityType` | string | `"Product"` |
| `entityId` | string | `"3fa85f64-5717-4562-b3fc-2c963f66afa6"` |
| `commentBody` | string | First 200 chars of the comment body |
| `appUrl` | string | From `AppSettings:BaseUrl` configuration |

### Notification Preference Reader

**New abstraction:** `INotificationPreferenceReader` in `Starter.Abstractions/Capabilities/`

```csharp
public interface INotificationPreferenceReader : ICapability
{
    Task<bool> IsEmailEnabledAsync(Guid userId, string notificationType, CancellationToken ct = default);
}
```

**Why a new reader interface instead of using the existing `NotificationPreference` entity directly?**
The Comments module cannot reference `Starter.Domain` entities through queries (modules don't inject `IApplicationDbContext`). Following the existing `IUserReader` / `ITenantReader` pattern, a reader capability provides a clean seam. The core infrastructure implements it by querying the existing `NotificationPreference` table.

**Implementation:** `Starter.Infrastructure/Capabilities/NotificationPreferenceReaderService.cs` — queries `ApplicationDbContext.NotificationPreferences` for the user + type combination. Returns `true` (email enabled) as the default when no preference row exists — mentions are high-signal enough to warrant opt-out semantics, not opt-in.

**Null Object:** `NullNotificationPreferenceReader` — returns `true` (default to enabled). This ensures the handler's logic flows correctly even if the reader isn't wired, though in practice the core infrastructure always provides the real implementation.

### Template Registration

**Where:** `CommentsActivityModule.SeedDataAsync` — call `ITemplateRegistrar.RegisterTemplateAsync(...)` to register the `notification.comment-mention` template. When Communication is absent, `NullTemplateRegistrar` silently no-ops — no error, template simply doesn't exist (and `IMessageDispatcher` is also null, so it's never needed).

**Template definition:**
```
Name:            notification.comment-mention
ModuleSource:    CommentsActivity
Category:        comments
Subject:         {{mentionerName}} mentioned you in a comment
Body:            Hi,\n\n{{mentionerName}} mentioned you in a comment on {{entityType}}.\n\n"{{commentBody}}"\n\nView it in the app: {{appUrl}}
DefaultChannel:  Email
AvailableChannels: [Email, InApp]
Variables:       mentionerName (string, required), entityType (string, required),
                 entityId (string, required), commentBody (string, required),
                 appUrl (string, required)
```

### Frontend: Notification Preference Toggle

**File:** `boilerplateFE/src/features/profile/components/NotificationPreferences.tsx`

**Change:** Add a `CommentMention` entry to the notification types list. The existing component renders Email + InApp toggles per type. For the Email toggle on this row:

- When `isModuleActive('communication')` is `true`: normal toggle, enabled by default.
- When `isModuleActive('communication')` is `false`: toggle is disabled, with a tooltip: *"Email notifications are available when the Communication module is enabled."*

The InApp toggle is always active (it controls the existing in-app notification, which is independent of Communication).

**No new components, no new pages, no new API endpoints for preferences** — the existing `useNotificationPreferences` / `useUpdateNotificationPreferences` hooks and their backend endpoints already support arbitrary notification type strings.

## Error Handling

| Scenario | Behavior |
|---|---|
| Communication module absent | `NullMessageDispatcher.SendAsync` returns `Guid.Empty`, debug log. In-app notification unaffected. |
| `MentionsJson` unparseable | Handler bails early with warning log. Same pattern as existing handler. |
| `IMessageDispatcher.SendAsync` throws for one recipient | Caught, logged as warning, loop continues to next recipient. |
| User has email disabled for `CommentMention` | Handler skips `SendAsync` for that user. In-app notification still fires (separate handler). |
| Template `notification.comment-mention` doesn't exist in Communication | `MessageDispatcher` logs a warning and skips — no crash. This happens when Communication is freshly installed but hasn't re-seeded. |

## Testing

### Unit Tests

1. **`EmailMentionedUsersOnCommentCreatedHandler`:**
   - Happy path: mock `IMessageDispatcher`, verify `SendAsync` called once per non-self-mentioned recipient with correct template name and variables.
   - Self-mention excluded: author in mentions list → no `SendAsync` call for that user.
   - Preference opt-out: mock `INotificationPreferenceReader.IsEmailEnabledAsync` returning `false` → `SendAsync` not called for that user.
   - Parse failure: invalid `MentionsJson` → handler returns without calling `SendAsync`.
   - Per-recipient error isolation: first `SendAsync` throws → second recipient still gets their call.

2. **Null-Object path:** inject `NullMessageDispatcher` → handler completes, returns `Guid.Empty` results, no exceptions.

3. **`NotificationPreferenceReaderService`:** query returns correct default (true) when no row exists; returns persisted value when row exists.

### Isolation Test

`rename.ps1 -Modules commentsActivity` → `dotnet build` succeeds. The handler compiles and runs, `NullMessageDispatcher` and `NullTemplateRegistrar` handle absence silently.

## Files Changed

| File | Change |
|---|---|
| `Starter.Abstractions/Capabilities/INotificationPreferenceReader.cs` | New interface |
| `Starter.Infrastructure/Capabilities/NotificationPreferenceReaderService.cs` | New implementation |
| `Starter.Infrastructure/Capabilities/NullObjects/NullNotificationPreferenceReader.cs` | New Null Object |
| `Starter.Infrastructure/DependencyInjection.cs` | Register `NullNotificationPreferenceReader` via `TryAddScoped` |
| `Starter.Module.CommentsActivity/Application/EventHandlers/EmailMentionedUsersOnCommentCreatedHandler.cs` | New handler |
| `Starter.Module.CommentsActivity/CommentsActivityModule.cs` | Seed `notification.comment-mention` template via `ITemplateRegistrar` |
| `boilerplateFE/src/features/profile/components/NotificationPreferences.tsx` | Add `CommentMention` row with Communication-awareness |
| `tests/Starter.Api.Tests/CommentsActivity/Application/EmailMentionedUsersTests.cs` | New test file |
| `tests/Starter.Api.Tests/Capabilities/NotificationPreferenceReaderTests.cs` | New test file |

## Non-Goals

- No changes to the mention autocomplete UI — the tagger doesn't need to know about channels.
- No admin-level "force email on mention" — Communication's trigger rules already cover that.
- No SMS/Push/WhatsApp — only `SendAsync` default-channel path (Communication handles channel selection).
- No new navigation items or pages.
- No changes to existing in-app mention notification handler.
