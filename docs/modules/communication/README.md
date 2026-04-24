# Communication Module

Multi-channel notification dispatcher. Email, SMS, push, in-app. Template-driven with per-tenant channel configuration.

## Docs

- **[User Guide](user-guide.md)** — managing notification preferences, channels, templates.
- **[Developer Guide](developer-guide.md)** — integrating the dispatcher, adding channels, templates.
- **[Roadmap](roadmap.md)** — deferred items.

## Capability contract

`IMessageDispatcher` — consuming modules call `DispatchAsync(template, recipient, data)` without coupling to specific channels.
