# Webhooks — Design Specification

## Overview

Tenant-scoped webhook endpoints that receive HTTP POST notifications for selected event types. Each tenant admin registers URLs, subscribes to specific events, and gets HMAC-SHA256 signed payloads. Delivery uses MassTransit + RabbitMQ with exponential backoff retries. Feature-flag gated via `webhooks.enabled` and `webhooks.max_count`. Full delivery audit log with request/response bodies (7-day auto-purge).

## Decisions

| Decision | Choice | Rationale |
|----------|--------|-----------|
| Scope | Tenant-scoped | Standard SaaS pattern — each tenant registers their own endpoints for their own data |
| Retry strategy | MassTransit exponential backoff | Leverages existing RabbitMQ infrastructure, no custom retry logic |
| Event subscription | Per-endpoint configurable | Tenant chooses which event types each endpoint receives, reducing noise |
| Delivery log | Full payload + response (4KB cap) | Helps tenants debug integration issues; auto-purge after 7 days |
| Billing gate | Feature-flag gated | `webhooks.enabled` + `webhooks.max_count` per plan, same pattern as API keys |
| Signature | HMAC-SHA256 | Industry standard (Stripe, GitHub, Shopify pattern) |

## Entities

### WebhookEndpoint (AggregateRoot, TenantId)

| Field | Type | Notes |
|-------|------|-------|
| Id | Guid | PK |
| TenantId | Guid | FK → Tenant |
| Url | string | HTTPS endpoint URL (max 2000 chars) |
| Description | string? | Optional label ("Slack notifications", "CRM sync") |
| Secret | string | Auto-generated HMAC-SHA256 signing key (32 bytes, hex-encoded) |
| Events | string (jsonb) | Array of subscribed event type strings: `["user.created","file.uploaded"]` |
| IsActive | bool | Toggle on/off without deleting |
| CreatedAt | DateTime | |
| ModifiedAt | DateTime? | |

**Global query filter:** `TenantId == null || e.TenantId == TenantId`
**Unique constraint:** None (tenant can have multiple endpoints with same URL for different events)

### WebhookDelivery (BaseEntity, TenantId)

| Field | Type | Notes |
|-------|------|-------|
| Id | Guid | PK |
| TenantId | Guid | FK → Tenant |
| WebhookEndpointId | Guid | FK → WebhookEndpoint |
| EventType | string | e.g. "user.created" |
| RequestPayload | string | JSON body sent (full payload) |
| ResponseStatusCode | int? | HTTP status code from target |
| ResponseBody | string? | First 4KB of response body |
| Status | WebhookDeliveryStatus | Pending, Success, Failed |
| Duration | int? | Response time in milliseconds |
| AttemptCount | int | Number of delivery attempts |
| ErrorMessage | string? | Error description on failure |
| CreatedAt | DateTime | |

**Global query filter:** `TenantId == null || d.TenantId == TenantId`
**Index:** (WebhookEndpointId, CreatedAt desc) for delivery log queries
**Retention:** Background job purges records older than 7 days

### Enums

```csharp
public enum WebhookDeliveryStatus
{
    Pending = 0,
    Success = 1,
    Failed = 2
}
```

## Event Types

Core resource events that trigger webhook deliveries:

| Event Type | Trigger | Payload Includes |
|-----------|---------|-----------------|
| `user.created` | User registered or invited | userId, email, firstName, lastName, roles |
| `user.updated` | User profile or status changed | userId, email, changedFields |
| `user.deleted` | User deactivated/deleted | userId, email |
| `file.uploaded` | File uploaded | fileId, fileName, size, contentType |
| `file.deleted` | File deleted | fileId, fileName |
| `role.created` | Role created | roleId, name |
| `role.updated` | Role updated (name or permissions) | roleId, name |
| `role.deleted` | Role deleted | roleId, name |
| `invitation.accepted` | User accepted invite | userId, email, roleId |
| `tenant.updated` | Tenant profile/branding changed | tenantId, name, changedFields |
| `subscription.changed` | Plan changed | planId, planName, oldPlanId |

Each webhook endpoint subscribes to a subset of these. The `GET /api/v1/webhooks/events` endpoint returns the full list so the frontend can render checkboxes.

## Event Flow

```
1. Domain event fires (e.g., UserCreatedEvent after User.Create())
   → DomainEventDispatcherInterceptor publishes via MediatR

2. WebhookEventHandler (INotificationHandler<UserCreatedEvent>)
   → Checks if event type has any active webhook endpoints for this tenant
   → Builds WebhookPayload { eventType, tenantId, timestamp, data }
   → Publishes DeliverWebhookMessage to MassTransit (RabbitMQ)

3. DeliverWebhookConsumer (MassTransit consumer)
   → Loads active WebhookEndpoints for this tenant subscribed to this event type
   → For each matching endpoint:
     a. Compute HMAC-SHA256 signature
     b. HTTP POST to endpoint URL with payload + signature header
     c. Create WebhookDelivery record (Success or Failed)
   → MassTransit handles retries on failure

4. Retry policy (MassTransit built-in):
   → 1 min, 5 min, 30 min, 2 hr, 24 hr
   → After max retries → message goes to error queue
   → WebhookDelivery.AttemptCount incremented on each retry
```

## Signature Format

```
X-Webhook-Signature-256: t=1680000000,v1=abc123def456...
```

**How it's computed:**
```csharp
var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
var signedPayload = $"{timestamp}.{jsonPayload}";
var signature = HMACSHA256(endpoint.Secret, signedPayload);
// Header: t={timestamp},v1={hex(signature)}
```

**Consumer verification:**
1. Extract `t` and `v1` from header
2. Recompute: `HMACSHA256(secret, "{t}.{rawBody}")`
3. Compare with `v1`
4. Optionally reject if `t` is older than 5 minutes (replay protection)

## Webhook Payload Structure

```json
{
  "id": "evt_abc123",
  "type": "user.created",
  "tenantId": "tenant-guid",
  "timestamp": "2026-04-02T12:00:00Z",
  "data": {
    "userId": "user-guid",
    "email": "john@acme.com",
    "firstName": "John",
    "lastName": "Doe",
    "roles": ["Admin"]
  }
}
```

The `data` field varies by event type. Each event handler builds its own data object with the relevant fields.

## API Endpoints

| Method | Path | Permission | Purpose |
|--------|------|-----------|---------|
| GET | `/api/v1/webhooks` | Webhooks.View | List tenant's webhook endpoints |
| POST | `/api/v1/webhooks` | Webhooks.Create | Register new endpoint |
| GET | `/api/v1/webhooks/{id}` | Webhooks.View | Get endpoint details |
| PUT | `/api/v1/webhooks/{id}` | Webhooks.Update | Update endpoint (URL, events, active) |
| DELETE | `/api/v1/webhooks/{id}` | Webhooks.Delete | Delete endpoint + delivery log |
| GET | `/api/v1/webhooks/{id}/deliveries` | Webhooks.View | Delivery log (paginated) |
| POST | `/api/v1/webhooks/{id}/test` | Webhooks.Create | Send test ping event |
| GET | `/api/v1/webhooks/events` | Webhooks.View | List available event types |

### Create Endpoint Request

```json
{
  "url": "https://hooks.example.com/my-webhook",
  "description": "Slack notifications",
  "events": ["user.created", "user.updated", "file.uploaded"],
  "isActive": true
}
```

**Response** includes auto-generated `secret` (shown once, must be copied).

### Test Endpoint

Sends a `webhook.test` event with a dummy payload to verify connectivity. Creates a WebhookDelivery record so the tenant can see the result.

## Permissions

```csharp
public static class Webhooks
{
    public const string View = "Webhooks.View";
    public const string Create = "Webhooks.Create";
    public const string Update = "Webhooks.Update";
    public const string Delete = "Webhooks.Delete";
}
```

**Role mapping:**
- Admin: All webhook permissions (View, Create, Update, Delete)
- User: Webhooks.View (read-only)
- SuperAdmin: All (automatic)

## Feature Flags

| Flag | Type | Free | Starter | Pro | Enterprise |
|------|------|------|---------|-----|------------|
| `webhooks.enabled` | Boolean | false | true | true | true |
| `webhooks.max_count` | Integer | 0 | 3 | 10 | 25 |

**Enforcement:** `CreateWebhookEndpointCommandHandler` checks `webhooks.enabled` and counts existing endpoints against `webhooks.max_count` (via IUsageTracker with metric `webhooks`).

## MassTransit Integration

### Message

```csharp
public sealed record DeliverWebhookMessage(
    Guid TenantId,
    string EventType,
    string Payload,  // JSON string of the full webhook payload
    DateTime OccurredAt);
```

### Consumer

```csharp
public sealed class DeliverWebhookConsumer : IConsumer<DeliverWebhookMessage>
```

Registered in `DependencyInjection.cs` alongside existing `GenerateReportConsumer`.

### Retry Policy

Configured via MassTransit's `UseMessageRetry`:
```csharp
cfg.UseMessageRetry(r => r.Intervals(
    TimeSpan.FromMinutes(1),
    TimeSpan.FromMinutes(5),
    TimeSpan.FromMinutes(30),
    TimeSpan.FromHours(2),
    TimeSpan.FromHours(24)));
```

After 5 failed attempts, the message moves to the error queue. The WebhookDelivery record's `AttemptCount` is updated on each attempt.

## Frontend

### Sidebar

Add "Webhooks" nav item for tenant users (gated by `Webhooks.View`), placed after API Keys:

```typescript
...(hasPermission(PERMISSIONS.Webhooks.View)
  ? [{ label: t('nav.webhooks'), icon: Webhook, path: ROUTES.WEBHOOKS }]
  : []),
```

### Pages

**WebhooksPage** (`/webhooks`) — Tenant admin view:
- Endpoint list table: URL, description, events (badge chips), active toggle, last delivery status, created date
- "Create Webhook" button (gated by `Webhooks.Create`, checks feature flag)
- Per-endpoint actions: Edit, Delete, Test, View Deliveries
- Empty state when no endpoints

**Create/Edit Webhook Dialog:**
- URL input (required, HTTPS validation)
- Description input (optional)
- Event type checkboxes (loaded from `GET /events` endpoint, grouped by resource)
- Active toggle
- On create: show secret once with copy button + warning "This secret won't be shown again"

**Delivery Log Modal:**
- Table: timestamp, event type, status badge (Success/Failed/Pending), response code, duration
- Expandable rows: request payload (formatted JSON), response body, error message
- Pagination (20 per page)
- Filter by status

### i18n Keys (en/ar/ku)

```
nav.webhooks
webhooks.title, webhooks.subtitle
webhooks.createWebhook, webhooks.editWebhook, webhooks.deleteWebhook
webhooks.url, webhooks.description, webhooks.events, webhooks.secret
webhooks.active, webhooks.inactive
webhooks.testWebhook, webhooks.testSent
webhooks.deliveries, webhooks.noDeliveries
webhooks.secretWarning (shown once on create)
webhooks.created, webhooks.updated, webhooks.deleted
webhooks.eventTypes (section header for checkbox group)
```

## Background Jobs

### Delivery Log Cleanup Job

Runs daily — deletes WebhookDelivery records older than 7 days.

```csharp
public sealed class WebhookDeliveryCleanupJob : BackgroundService
```

Registered as `AddHostedService<WebhookDeliveryCleanupJob>()`. Interval configurable via system setting `webhooks.delivery_retention_days` (default: 7).

## Seed Data

### Feature Flags

Add to DataSeeder alongside existing flags:
```
webhooks.enabled (Boolean, default: false, category: System)
webhooks.max_count (Integer, default: 0, category: System)
```

### Plan Features

Update each plan's Features JSON:
- Free: `webhooks.enabled=false, webhooks.max_count=0`
- Starter: `webhooks.enabled=true, webhooks.max_count=3`
- Pro: `webhooks.enabled=true, webhooks.max_count=10`
- Enterprise: `webhooks.enabled=true, webhooks.max_count=25`

## Domain Events → Webhook Event Mapping

For each domain event that should trigger webhooks, create a MediatR notification handler:

| Domain Event | Webhook Event Type | Handler |
|-------------|-------------------|---------|
| UserCreatedEvent (new) | `user.created` | WebhookUserEventHandler |
| UserUpdatedEvent (new) | `user.updated` | WebhookUserEventHandler |
| UserDeletedEvent (new) | `user.deleted` | WebhookUserEventHandler |
| FileUploadedEvent (new) | `file.uploaded` | WebhookFileEventHandler |
| FileDeletedEvent (new) | `file.deleted` | WebhookFileEventHandler |
| RoleCreatedEvent (new) | `role.created` | WebhookRoleEventHandler |
| RoleUpdatedEvent (new) | `role.updated` | WebhookRoleEventHandler |
| InvitationAcceptedEvent (new) | `invitation.accepted` | WebhookInvitationEventHandler |
| SubscriptionChangedEvent (existing) | `subscription.changed` | WebhookBillingEventHandler |

**Note:** Most domain events listed above don't exist yet — they need to be added to the relevant entities (User, FileMetadata, Role). The existing pattern is: entity calls `RaiseDomainEvent(new XxxEvent(...))` in its mutation methods, and the `DomainEventDispatcherInterceptor` auto-publishes them via MediatR on `SaveChangesAsync`.

## Testing Checklist

- [ ] Create webhook endpoint with URL + selected events
- [ ] Secret displayed once on creation, not retrievable later
- [ ] Endpoint active toggle works (inactive endpoints don't receive deliveries)
- [ ] Create a user → webhook delivery sent to subscribed endpoints
- [ ] Upload a file → webhook delivery for `file.uploaded`
- [ ] Delivery log shows request payload, response code, duration
- [ ] Failed delivery shows error message
- [ ] Test button sends ping event and records delivery
- [ ] Feature flag gate: Free plan can't create webhooks
- [ ] Quota enforcement: Starter plan limited to 3 endpoints
- [ ] HMAC signature verification with test consumer
- [ ] Delivery cleanup job purges records > 7 days
- [ ] Tenant isolation: tenant A can't see tenant B's endpoints/deliveries
