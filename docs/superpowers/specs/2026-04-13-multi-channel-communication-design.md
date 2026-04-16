# Multi-Channel Communication Module — Design Spec

**Date:** 2026-04-13
**Status:** Approved
**Wave:** 1 (Cross-Domain Engine)
**Dependencies:** None (fully independent)
**Scope:** Transactional messaging, template engine, trigger rules, notification channels, team integrations, delivery tracking, quota enforcement

---

## Context

The boilerplate's modular architecture (see `2026-04-05-module-architecture-design.md`) enables composable modules that follow the `IModule` pattern with own DbContext, capabilities via `Starter.Abstractions`, and Null Object fallbacks.

Every business application outgrows basic transactional emails. Workflow needs to notify approvers. Billing needs to send receipts. Scheduling needs to send reminders. Without a unified communication module, every module reinvents messaging — different email-sending patterns scattered across the codebase, no template management, no delivery tracking, no multi-channel support.

Multi-Channel Communication is one of four Wave 1 engines. It has no module dependencies and can be built in parallel with Comments & Activity, AI Integration, and Workflow & Approvals. It is the **outbound voice** of the entire platform — every other module publishes events, Communication handles dispatch.

---

## Vision

A unified messaging platform that gives every module a single API to send messages, gives tenants control over their channels, templates, and automation rules, and gives users control over how they receive notifications.

Two delivery models:
- **Notification Channels** — person-to-person messaging (Email, SMS, Push, WhatsApp, In-App)
- **Team Integrations** — group/team feeds (Slack, Telegram, Discord, Microsoft Teams)

---

## Key Decisions

| Decision | Choice | Rationale |
|----------|--------|-----------|
| V1 scope | Transactional + templates + triggers. Campaigns = V1.1 | Ship focused V1, campaigns add complexity without blocking other modules |
| Channels vs Integrations | Two separate sections with different UX | Person-to-person notifications and team feeds are fundamentally different targets |
| Template engine | Handlebars/Liquid-style with conditionals + loops | Simple `{{var}}` breaks down for invoices (line items), conditional sections |
| Credential model | Tenants bring their own (except In-App) | Email deliverability, SMS regulatory compliance, WhatsApp Business verification — all tenant-specific. Shared credentials = operational nightmare |
| In-App channel | Platform-managed via Ably | Internal to the app, no external provider needed, always available as final fallback |
| Fallback chains | Explicitly defined per trigger rule | Not every message makes sense on every channel. Tenants control cost. In-App is always implicit final fallback |
| Rate limiting | Per-tenant quotas via existing Billing module | Prevents abuse, ties into subscription plans, protects provider reputation |
| Template organization | Categorized by owning module | Auth templates, Billing templates, Workflow templates — each module registers its own |
| Social media channels | Separate "Integrations" model, not notification channels | Different target (group/channel vs person), different credential model (bot tokens/webhooks vs API keys), not in user preference matrix |

---

## Target Audiences

| Persona | Role | What They Care About |
|---------|------|---------------------|
| Platform Admin | Boilerplate deployer | Module works, dev tools available (Mailpit), defaults are sensible |
| Tenant Admin | Business owner / IT admin | Connecting channels, managing templates, setting up automations, monitoring delivery |
| Tenant User | Employee / staff | Receiving relevant notifications, controlling their preferences |
| End Customer | Tenant's customer (external) | Getting the right message, at the right time, on the right channel |

---

## Notification Channels

Person-to-person messaging. Target is a specific user. Appears in user notification preferences.

| Channel | Provider Options | Credential Owner | Notes |
|---------|-----------------|------------------|-------|
| Email | SMTP, SendGrid, Amazon SES | Tenant | Sender domain, SPF/DKIM, deliverability reputation |
| SMS | Twilio | Tenant | Phone number, A2P 10DLC registration, per-country compliance |
| Push Notifications | FCM (Android), APNs (iOS) | Tenant | Tied to tenant's mobile app bundle/Firebase project |
| WhatsApp | Twilio / Meta Business API | Tenant | Meta Business verification, template approval by Meta |
| In-App | Ably | Platform | Always available, no tenant config needed, final fallback |

### Channel Lifecycle

```
Not Connected → Configuring (wizard) → Testing → Connected → Active
                                                              ↓
                                                          Disabled (by admin)
```

- **Not Connected:** Channel available but no credentials configured. Messages targeting this channel silently fall through to fallback.
- **Connected:** Credentials provided and test message succeeded.
- **Active:** Accepting message dispatch.
- **Disabled:** Admin manually turned off. Messages fall through to fallback.

---

## Team Integrations

Group/team feeds. Target is a channel, group, or bot conversation. Not in user notification preferences — configured at tenant level.

| Integration | Setup Model | Target |
|-------------|-------------|--------|
| Slack | OAuth app install → pick Slack channel | Slack channel (e.g., #orders, #alerts) |
| Telegram | Create bot via BotFather → paste token → pick group | Telegram group or channel |
| Discord | Paste webhook URL | Discord channel |
| Microsoft Teams | Install connector → pick Teams channel | Teams channel |

### Integration vs Channel: Key Differences

| Aspect | Notification Channels | Team Integrations |
|--------|----------------------|-------------------|
| Target | A specific person | A group/channel/bot conversation |
| Triggered by | System events | System events |
| Content | Personal, transactional | Team awareness, operational feeds |
| User preferences | Yes — users toggle per category | No — tenant admin configures |
| Fallback chain | Yes — explicit ordered fallback | No — independent delivery |
| Templates | Full template engine | Simplified format (plain text + optional rich embeds) |
| Credential model | API keys, sender identities | Bot tokens, webhook URLs, OAuth apps |

---

## Template System

### Architecture

Templates are organized by **owning module** (category). Each module registers its template definitions — the template name, available variables with descriptions, and system default content. Tenants can override any system default with their own version.

### Template Categories (Registered by Modules)

| Category | Registered By | Example Templates |
|----------|--------------|-------------------|
| Auth | Core | Welcome, Password Reset, Email Verification, 2FA Code, Account Locked, Login from New Device |
| Billing | Billing Module | Payment Confirmation, Invoice, Subscription Renewal, Payment Failed, Trial Expiring |
| Workflow | Workflow Module | Approval Needed, Approved, Rejected, Escalated, SLA Warning, Delegated |
| Communication | Self | Test Message, General Announcement |
| Notifications | Core | System Maintenance, Feature Update |

Additional modules register their own templates when installed (e.g., Orders registers "Order Confirmed," "Order Shipped," "Order Delivered").

### Template Structure

Each template has:
- **Name:** Unique identifier (e.g., `auth.welcome`, `billing.payment_confirmed`)
- **Category:** The owning module
- **Channel variants:** Different content per channel (Email gets HTML, SMS gets 160 chars, Push gets title + body)
- **Locale variants:** Per-language versions (English, Arabic, etc.)
- **Variables:** Declared schema with descriptions — what data is available for this template
- **System default:** Ships with the module, read-only for tenants
- **Tenant override:** Tenant's customized version, takes precedence over system default

### Template Engine

Handlebars/Liquid-style syntax supporting:

**Variables:**
```
Hi {{userName}}, your order {{orderNumber}} has been confirmed.
```

**Conditionals:**
```
{{#if hasPhysicalItems}}
Your order will be shipped to {{shippingAddress}}.
Estimated delivery: {{estimatedDelivery}}.
{{else}}
Your digital items are ready for download.
{{/if}}
```

**Loops:**
```
Order summary:
{{#each lineItems}}
  - {{this.name}} x{{this.quantity}}: {{this.price}}
{{/each}}

Total: {{orderTotal}}
```

**Variable reference panel:** The template editor shows all available variables for the selected template type with descriptions and sample values. Tenants never have to guess what's available.

**Preview:** Renders the template with sample data before saving. Shows exactly what the recipient will see.

### Template Lifecycle

```
Module Installed → System Defaults Auto-Registered → Tenant Overrides (optional)
                                                          ↓
Module Removed → Templates become "Orphaned" (not deleted, tenant may have customized them)
```

---

## Trigger Rules

Event-driven automation: "When X happens, send Y via Z."

### Trigger Rule Structure

| Field | Description |
|-------|-------------|
| Name | Human-readable name for the rule |
| Event | Domain event to listen for (dropdown of available events from installed modules) |
| Conditions | Optional field-level filters (e.g., "only if status = Approved", "only if amount > 1000") |
| Template | Which template to render |
| Recipient | How to resolve the target (from event context: the user, the customer, the manager, etc.) |
| Primary Channel | First channel to try (Email, SMS, Push, WhatsApp) |
| Fallback Chain | Ordered list of fallback channels if primary fails |
| Integration Targets | Optional: also post to Slack #channel, Telegram group, etc. |
| Active | Toggle on/off without deleting |

### Fallback Chain Behavior

Each trigger rule defines an explicit, ordered fallback chain:

```
Primary: Email
  → Fallback 1: SMS
  → Fallback 2: Push
  → Final (implicit, always): In-App
```

**Fallback triggers when:**
- Channel not connected for this tenant
- Delivery failed after all retries
- Recipient has disabled this channel in their preferences

**In-App is always the implicit final fallback.** Every message is guaranteed to at least appear in the user's in-app notification feed. This is not configurable — it's a system guarantee. Nothing is ever silently lost.

**Integration targets are independent** — they don't participate in the fallback chain. If a rule says "Email the customer AND post to #orders Slack," and the email fails, the Slack post still happens (and vice versa). They're parallel, not sequential.

### Event Registration

Each module registers its publishable events with metadata:

```
EventRegistration {
  EventName: "OrderStatusChanged",
  Module: "Orders",
  DisplayName: "Order Status Changed",
  AvailableVariables: [
    { Name: "orderNumber", Type: "string", Description: "The order number" },
    { Name: "newStatus", Type: "string", Description: "The new order status" },
    { Name: "customerName", Type: "string", Description: "Customer full name" },
    ...
  ],
  AvailableRecipients: [
    { Name: "customer", Description: "The customer who placed the order" },
    { Name: "assignedAgent", Description: "The agent handling the order" },
  ]
}
```

The trigger rule UI only shows events from installed modules — no dead options.

---

## User Notification Preferences

### Preference Matrix

Users manage their notification preferences in **Profile > Notification Preferences**.

Rows = notification categories (mapped from template categories).
Columns = notification channels (Email, SMS, Push, WhatsApp, In-App).

| Category | Email | SMS | Push | WhatsApp | In-App |
|----------|-------|-----|------|----------|--------|
| Security Alerts | Required | Off | On | Off | Always |
| Approvals | On | Off | On | Off | Always |
| Reports | On | Off | Off | Off | Always |
| System Updates | Off | Off | Off | Off | Always |

- **"Required"** = Tenant admin has marked this category+channel as mandatory. User cannot toggle it off.
- **"Always"** = In-App is always on. Cannot be toggled off.
- **Integrations (Slack, Telegram, etc.) do NOT appear here** — they're team-level, not personal.

### Preference Resolution

When dispatching a message, the system checks:

1. Is the channel connected for this tenant? No → skip to fallback.
2. Has the user disabled this channel for this category? Yes → skip to fallback.
3. Has the tenant admin marked this as "required"? Yes → send regardless of user preference.
4. Dispatch the message.

---

## Quota & Rate Limiting

Ties into the existing Billing module's `IUsageTracker` + `IQuotaChecker` capabilities.

### Usage Metrics

| Metric Key | Description |
|------------|-------------|
| `messages_email` | Emails sent this billing period |
| `messages_sms` | SMS sent this billing period |
| `messages_push` | Push notifications sent this billing period |
| `messages_whatsapp` | WhatsApp messages sent this billing period |
| `messages_total` | Total messages across all channels |

### Quota Tiers (Platform Admin Configures Per Plan)

| Plan | Email/mo | SMS/mo | WhatsApp/mo | Push/mo |
|------|----------|--------|-------------|---------|
| Free | 100 | 0 | 0 | 100 |
| Starter | 5,000 | 500 | 100 | 5,000 |
| Business | 50,000 | 5,000 | 1,000 | 50,000 |
| Enterprise | Unlimited | Unlimited | Unlimited | Unlimited |

### Quota Exceeded Behavior

1. Message dispatch returns quota exceeded error.
2. **In-App notifications are never rate-limited** — zero cost, always delivered.
3. Tenant Admin sees a quota warning in the Communication dashboard.
4. Integration messages (Slack, Telegram, etc.) are **not quota-limited** — they go through the tenant's own bot/webhook, not platform infrastructure.

---

## User Journeys

### Journey 1: Tenant Admin — First-Time Channel Setup

1. Navigates to **Settings > Communication > Channels**
2. Sees list of available channels, each showing "Not Connected"
3. Clicks **Email** > Setup wizard opens
4. Wizard: "Which email provider?" > SMTP / SendGrid / Amazon SES
5. Picks SendGrid > enters API key, sender email, sender name
6. Clicks **Test Connection** > test email sent to admin's own address
7. Confirms receipt > channel status = **Connected**
8. Repeats for SMS (Twilio), WhatsApp, etc.

**If skipped:** Module works — In-App always available. Other modules' messages deliver via In-App only. No errors, no broken flows.

### Journey 2: Tenant Admin — Managing Templates

1. Navigates to **Communication > Templates**
2. Sees templates organized by category (Auth, Billing, Workflow, etc.)
3. System defaults shown with "System" badge — read-only
4. Clicks a system template > **Customize** > creates tenant override
5. Template editor:
   - Channel selector (Email / SMS / Push / WhatsApp)
   - Locale selector (English, Arabic, etc.)
   - Subject line with variable autocomplete
   - Body editor with variables, conditionals, loops
   - Variable reference panel showing available variables with descriptions
6. Clicks **Preview** > sees rendered template with sample data
7. Saves > tenant's messages now use their custom template

### Journey 3: Tenant Admin — Setting Up Trigger Rules

1. Navigates to **Communication > Trigger Rules**
2. Clicks **Create Rule**
3. Configuration:
   - **Event:** dropdown of available domain events > selects "Leave Request Approved"
   - **Conditions:** optional field filter (e.g., "only if leave type = Annual")
   - **Template:** selects or creates template
   - **Recipient:** the employee who requested (auto-resolved from event context)
   - **Primary Channel:** SMS
   - **Fallback Chain:** Add fallback > Push > (In-App always at bottom)
   - **Integration Targets:** optionally also post to #hr-updates Slack channel
   - **Active:** toggle on
4. Saves > every future leave approval triggers SMS + Slack post

### Journey 4: Tenant Admin — Setting Up Integrations

1. Navigates to **Communication > Integrations**
2. Sees available integrations: Slack, Telegram, Discord, Teams
3. Clicks **Slack** > "Install Slack App" button > OAuth flow
4. After auth: sees list of tenant's Slack channels
5. Maps events to channels:
   - "New Order" > #orders
   - "Payment Failed" > #alerts
   - "Approval Needed" > #approvals
6. Saves > events now post to Slack channels automatically

### Journey 5: Tenant User — Managing Notification Preferences

1. Goes to **Profile > Notification Preferences**
2. Sees category x channel matrix
3. Toggles off Email for "Reports" > reports now In-App only
4. "Security Alerts" Email is marked "Required" > cannot toggle off
5. Saves preferences

### Journey 6: System — Cross-Module Message Dispatch

1. Workflow publishes: `OrderStatusChanged { OrderId, NewStatus: "Shipped", CustomerId }`
2. Communication event listener receives it
3. Checks trigger rules for this tenant > finds matching rule
4. Resolves template (tenant override > system default)
5. Checks recipient's channel preferences
6. Checks tenant's quota for the channel
7. Renders template with variables from event context
8. Dispatches via tenant's configured provider
9. Logs in `DeliveryLog` with status
10. If fails > retry with backoff > if all retries fail > next in fallback chain > In-App guaranteed

### Journey 7: Tenant Admin — Troubleshooting Delivery

1. Navigates to **Communication > Delivery Log**
2. Filters by recipient email or entity reference
3. Finds entry > status: "Bounced"
4. Sees: timestamp, channel, template used, provider response, retry attempts
5. Clicks **Resend** > message re-dispatched

---

## Module Navigation Structure

### Tenant Admin View

```
Communication
  ├── Channels              — Connect/manage notification providers
  │     └── Setup Wizard      per channel (Email, SMS, Push, WhatsApp)
  ├── Integrations          — Connect team feeds
  │     └── Setup Flow        per integration (Slack, Telegram, Discord, Teams)
  ├── Templates             — Organized by module category
  │     ├── System Defaults   (read-only, auto-registered by modules)
  │     └── Tenant Overrides  (customized versions)
  ├── Trigger Rules         — Event > Channel/Integration automation
  │     └── Fallback Chain    per rule
  ├── Delivery Log          — Full message history & status
  │     └── Resend action     for failed messages
  └── Usage & Quotas        — Current period usage vs plan limits
```

### User Profile Addition

```
Profile
  └── Notification Preferences — Per-category, per-channel toggles
```

### Dashboard Widget

- Messages sent (today / this week / this month)
- Delivery success rate (%)
- Channel breakdown (pie: email vs SMS vs push vs in-app)
- Failed deliveries requiring attention (count + link)
- Quota usage bar (current / limit)

---

## Capability Exposed

In `Starter.Abstractions/Capabilities/`:

```csharp
public interface IMessageDispatcher
{
    /// Send a transactional message to a specific user
    Task<Result<MessageDeliveryResult>> SendAsync(
        string templateName,
        Guid recipientUserId,
        Dictionary<string, object> variables,
        CancellationToken ct = default);

    /// Send a transactional message with explicit channel preference
    Task<Result<MessageDeliveryResult>> SendAsync(
        string templateName,
        Guid recipientUserId,
        Dictionary<string, object> variables,
        ChannelType preferredChannel,
        CancellationToken ct = default);

    /// Send to an external recipient (not a platform user)
    Task<Result<MessageDeliveryResult>> SendExternalAsync(
        string templateName,
        ExternalRecipient recipient,
        Dictionary<string, object> variables,
        CancellationToken ct = default);
}
```

Other modules call `IMessageDispatcher.SendAsync(...)` — they never need to know about SMTP, Twilio, or template rendering. If Communication module is not installed, the Null Object fallback is a no-op.

---

## Entities (Own DbContext: `CommunicationDbContext`)

| Entity | Purpose |
|--------|---------|
| `ChannelConfig` | Per-tenant channel provider credentials (encrypted) and status |
| `IntegrationConfig` | Per-tenant integration credentials (bot tokens, webhook URLs) and channel mappings |
| `MessageTemplate` | Template content per channel, per locale, with variable schema |
| `MessageTemplateOverride` | Tenant-specific override of a system default template |
| `TriggerRule` | Event > template > recipient > channel > fallback chain mapping |
| `TriggerRuleIntegrationTarget` | Integration targets attached to a trigger rule |
| `DeliveryLog` | Every message dispatched: recipient, channel, template, status, provider response, timestamps |
| `DeliveryAttempt` | Individual delivery attempts per log entry (retries) |
| `NotificationPreference` | Per-user, per-category channel toggles |
| `RequiredNotification` | Per-tenant categories marked as mandatory (cannot be toggled off by users) |
| `EventRegistration` | Metadata about available events from installed modules |

---

## Permissions

| Permission | Description |
|------------|-------------|
| `Communication.View` | View channels, templates, trigger rules, delivery log |
| `Communication.ManageChannels` | Connect/disconnect notification channels |
| `Communication.ManageIntegrations` | Connect/disconnect team integrations |
| `Communication.ManageTemplates` | Create/edit tenant template overrides |
| `Communication.ManageTriggerRules` | Create/edit/delete trigger rules |
| `Communication.ViewDeliveryLog` | View delivery history and status |
| `Communication.Resend` | Resend failed messages |
| `Communication.ManageQuotas` | View and manage usage quotas (platform admin) |

---

## Scope Boundaries

### V1 (This Spec)

- Notification channels: Email, SMS, Push, WhatsApp, In-App
- Team integrations: Slack, Telegram, Discord, Microsoft Teams
- Template engine with conditionals + loops
- Templates organized by module category with system defaults + tenant overrides
- Trigger rules with explicit fallback chains
- User notification preferences with required overrides
- Delivery log with retry and resend
- Quota enforcement via Billing module
- Dashboard widget

### V1.1 (Campaigns — Future)

- Recipient segments (dynamic groups based on filters)
- Campaign builder (pick segment, pick template, schedule delivery)
- Campaign analytics (open rates, click rates, bounce rates)
- Unsubscribe tracking
- A/B testing (test two subject lines, auto-pick winner)
- Drip sequences (multi-step campaigns triggered by user behavior)

### Out of Scope (Not Planned)

- Visual HTML email builder (tenants use their own tools or plain templates)
- Shared/platform-managed provider credentials (except In-App)
- VOIP / voice calls
- Live chat / real-time customer support

---

## Effort Estimate

**Medium-Large**

- Backend: Channel provider abstraction, template engine, trigger rule engine, delivery pipeline with retry/fallback, quota integration, event registration system
- Frontend: Channel setup wizards, integration setup flows, template editor with preview, trigger rule builder, delivery log, notification preferences, dashboard widget
- Infrastructure: No new Docker services (Ably already in stack). Provider SDKs: SendGrid, Twilio, FCM, Meta WhatsApp API, Slack SDK, Telegram Bot API, Discord webhook, Teams connector

---

## Cross-Module Integration Points

| Module | Integration |
|--------|-------------|
| **Core (Auth)** | Registers auth templates (welcome, password reset, 2FA, etc.) |
| **Billing** | Registers billing templates. Provides quota enforcement via `IUsageTracker` / `IQuotaChecker` |
| **Workflow** | Registers workflow templates. On-enter/on-exit state actions dispatch via `IMessageDispatcher` |
| **Scheduling** | Dispatches event reminders via `IMessageDispatcher` |
| **Comments** | Optional: notify mentioned users via `IMessageDispatcher` |
| **AI** | Optional: AI-generated message suggestions, smart template variable resolution |
| **Orders** | Registers order lifecycle templates (confirmed, shipped, delivered) |
| **Leave & Attendance** | Registers leave status templates (approved, rejected) |
| **Students & Enrollment** | Registers enrollment templates, absence alerts to guardians |
| **Payroll** | Dispatches pay slip notifications |
| **All modules** | Publish domain events that trigger rules can listen to |

---

## Next Steps

1. **Technical design** — Entity schemas, API endpoints, provider abstraction interfaces, template engine implementation
2. **Implementation** — Following the standard module pattern (Domain > Application > Infrastructure > API > Frontend)
3. **System default templates** — Define the initial set of templates that ship with Core
4. **Provider implementations** — Start with Email (SMTP/SendGrid) + In-App (Ably), add others incrementally
