# Webhooks Module

Outbound webhook publishing with delivery log, retries, signature verification, and event-driven dispatch.

## Docs

- **[Developer Guide](developer-guide.md)** — event model, testing, secret rotation, delivery log.

## Capability contract

`IWebhookPublisher` — consuming modules call `PublishAsync(event, payload)` without coupling to the webhook infrastructure.
