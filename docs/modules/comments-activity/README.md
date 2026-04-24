# Comments & Activity Module

Entity-scoped comments + activity feed. Any module can attach to any entity via a capability-based registration pattern.

## Docs

- **[User Guide](user-guide.md)** — commenting, @mentions, activity timeline.
- **[Developer Guide](developer-guide.md)** — registering entities, recording activity, capability contracts.
- **[Roadmap](roadmap.md)** — deferred items.

## Capability contracts

- `ICommentService` — add / edit / delete / read comments
- `IActivityService` — record and read activity-log entries
- `IEntityWatcherService` — subscribe users to notifications for an entity
- `ICommentableEntityRegistration` — opt an entity in via DI
