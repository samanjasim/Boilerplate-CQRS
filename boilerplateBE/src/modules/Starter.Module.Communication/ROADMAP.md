# Communication — Roadmap

Deliberately deferred improvements for module maintainers. Each entry names the trigger that flips it from "defer" to "do now" and points at the starting files so the next developer does not have to rediscover context.

Integrator-facing integration docs live in [`docs/developer-guide.md`](./docs/developer-guide.md). End-user docs live in [`docs/user-manual.md`](./docs/user-manual.md). This file is maintainer-facing.

---

### Transactional outbox on `CommunicationDbContext`

**What:** Bind the MassTransit EF outbox to `CommunicationDbContext` so dispatch messages (`DispatchMessageMessage`, `DispatchIntegrationMessage`) publish atomically with their Postgres writes. Today the outbox is bound to `ApplicationDbContext` only, and this module publishes via `IPublishEndpoint.Publish` — at-most-once semantics.

**Why deferred:** The UX-critical path (delivery log write + provider send) runs inside the consumer, so a lost dispatch message degrades only a single channel attempt; the dispatcher retries via `TryAddDeliveryAttempt`. The outbox upgrade buys crash-window durability but adds write-path latency with no caller pressure today.

**Pick this up when:** A compliance-grade consumer lands (e.g. audit-mandated delivery records, billed-by-sent-message metering) or we observe a crash-window event drop in production that mattered.

**Starting points:**
- Mirror the `AddEntityFrameworkOutbox<ApplicationDbContext>` block in [`boilerplateBE/src/Starter.Infrastructure/DependencyInjection.cs`](../../Starter.Infrastructure/DependencyInjection.cs) against `CommunicationDbContext` inside [`Infrastructure/DependencyInjection`](./Infrastructure) (register alongside the DbContext in `CommunicationModule.ConfigureServices`).
- Swap `IPublishEndpoint.Publish` in [`MessageDispatcher`](./Infrastructure/Services) and the trigger-rule dispatch path for `IBus.Publish` inside the same `SaveChangesAsync` transaction.
- Verify the purity test (`AbstractionsPurityTests`) still passes — abstractions must gain no MassTransit references.

---

### SMS channel provider (Twilio)

**What:** Real SMS sending via Twilio. Today `NotificationChannel.Sms` + `ChannelProvider.Twilio` are defined in [`Domain/Enums/NotificationChannel.cs`](./Domain/Enums/NotificationChannel.cs) and [`Domain/Enums/ChannelProvider.cs`](./Domain/Enums/ChannelProvider.cs), the UI exposes the channel in `ChannelSetupDialog`, but no `TwilioSmsProvider : IChannelProvider` implementation exists — dispatch falls through to `null` in `ChannelProviderFactory.GetDefaultProvider(NotificationChannel.Sms)`.

**Why deferred:** No tenant has asked for SMS, Twilio adds a paid dependency + credentials flow, and the email/Slack/Telegram/Discord/Teams coverage serves the current catalog.

**Pick this up when:** First tenant requests SMS, OR a Wave 3 domain module (e.g. Attendance, POS) has a workflow that needs SMS fallback.

**Starting points:**
- Add `TwilioSmsProvider` in [`Infrastructure/Providers`](./Infrastructure/Providers) implementing `IChannelProvider` with `Channel => NotificationChannel.Sms` and `ProviderType => ChannelProvider.Twilio`.
- Register via `services.AddScoped<IChannelProvider, TwilioSmsProvider>()` inside `CommunicationModule.ConfigureServices`.
- Add `Twilio` NuGet reference to [`Starter.Module.Communication.csproj`](./Starter.Module.Communication.csproj).
- Plumb `AccountSid` / `AuthToken` / `FromNumber` through `ChannelConfig.ProviderCredentials` (already encrypted by `ICredentialEncryptionService`).

---

### Push channel providers (FCM, APNs)

**What:** Real push notifications. `NotificationChannel.Push` + `ChannelProvider.Fcm` + `ChannelProvider.Apns` are declared but unimplemented.

**Why deferred:** Push requires device-token registration flows on the mobile client that don't exist yet (Flutter app has no notification-registration infrastructure today) and Apple-developer / Firebase-project setup per tenant.

**Pick this up when:** Mobile app adds device-token registration AND a tenant needs server-initiated push (e.g. realtime chat).

**Starting points:**
- Create `FcmPushProvider` + `ApnsPushProvider` in [`Infrastructure/Providers`](./Infrastructure/Providers).
- Add a `DeviceToken` entity (user-scoped, tenant-scoped) somewhere in core so recipients can be resolved to tokens. Consider whether this belongs in core or a new `Starter.Module.Devices`.
- Mobile: register device token on login in [`boilerplateMobile`](../../../../boilerplateMobile) and expose a `POST /api/v1/notifications/devices` endpoint in core.

---

### WhatsApp channel provider

**What:** WhatsApp Business Platform sending. Enums define `ChannelProvider.TwilioWhatsApp` and `ChannelProvider.MetaWhatsApp`; no provider implementations.

**Why deferred:** Lowest-signal channel for the current product catalog. Meta's approval flow for WABA is heavy.

**Pick this up when:** A tenant in Wave 3 (e.g. Orders, Courses, POS) has a workflow that explicitly needs WhatsApp and is willing to fund WABA setup.

**Starting points:** Same pattern as SMS — Twilio-backed `TwilioWhatsAppProvider` is the easier first landing; Meta-direct can follow.

---

### Ably real-time push for In-App

**What:** Today [`InAppProvider`](./Infrastructure/Providers/InAppProvider.cs) writes a delivery log entry and stops — the frontend polls. Enhanced Ably support would push the delivery record to connected clients immediately.

**Why deferred:** The Enhanced Ably module ("Realtime") is planned for Wave 2 per [`docs/superpowers/specs/2026-04-09-composable-module-catalog-design.md`](../../../../docs/superpowers/specs/2026-04-09-composable-module-catalog-design.md). No point half-implementing here.

**Pick this up when:** The Realtime module lands. At that point `InAppProvider` should take an `IRealtimePublisher` capability and call it after the delivery log write.

**Starting points:**
- Define `IRealtimePublisher : ICapability` in [`Starter.Abstractions/Capabilities`](../../Starter.Abstractions/Capabilities) with a `PublishToUserAsync(Guid userId, string channel, object payload)` method.
- Register `NullRealtimePublisher` fallback in [`Starter.Infrastructure/Capabilities/NullObjects`](../../Starter.Infrastructure/Capabilities/NullObjects).
- Inject into `InAppProvider` — Null Object means this module keeps working when the Realtime module is absent.

---

### Provider-specific connection testing

**What:** [`TestChannelConfigCommandHandler.cs`](./Application/Commands/TestChannelConfig/TestChannelConfigCommandHandler.cs) currently performs structural validation only. The `// TODO: Phase 4 will add actual provider-specific connection testing` marker expects each provider to expose a `TestConnectionAsync` method that opens an SMTP handshake, pings the Slack webhook, hits Telegram `getMe`, etc.

**Why deferred:** Structural validation caught 90% of misconfigurations in the pre-merge testing. A real connection test requires provider-specific error taxonomy + timeout handling, which is significant work for a diagnostic feature.

**Pick this up when:** Support tickets from tenants misconfiguring credentials start to hit a threshold that justifies the diagnostic cost (informal rule: ≥3 tickets).

**Starting points:**
- Add `Task<ProviderTestResult> TestConnectionAsync(ChannelConfig config, CancellationToken ct)` to `IChannelProvider` and `IIntegrationProvider`.
- Implement per provider — start with SMTP (easiest: `SmtpClient.Connect` + `Noop`), then webhooks (HEAD/GET on the URL).
- Wire through `TestChannelConfigCommandHandler` by resolving `IChannelProviderFactory.GetProvider(...).TestConnectionAsync(...)`.

---

### SendGrid / Amazon SES email variants

**What:** `ChannelProvider.SendGrid` and `ChannelProvider.Ses` are defined but only `SmtpEmailProvider` is implemented. Tenants currently have to use an SMTP bridge to reach either service.

**Why deferred:** SMTP works against both SendGrid and SES via their relay servers, so there's no hard block. Native SDK integration adds DKIM/DMARC headers and webhook parsing niceties but isn't a blocker.

**Pick this up when:** A tenant hits SMTP-relay throughput limits or needs SendGrid/SES webhook-based bounce/complaint handling.

**Starting points:**
- New `SendGridEmailProvider` + `SesEmailProvider` in [`Infrastructure/Providers`](./Infrastructure/Providers) implementing `IChannelProvider` with `Channel => NotificationChannel.Email`.
- Add SDK references (`SendGrid`, `AWSSDK.SimpleEmail`).
- Add a per-provider webhook endpoint in [`Controllers`](./Controllers) for bounce/complaint handling, writing back to `DeliveryLog.Status`.

---

### Strongly-typed template variable schemas

**What:** Template variables today are `Dictionary<string, object>`. A schema registry (per-template, from the `ITemplateRegistrar` seeding flow) would let the UI render a form, catch typos at registration time, and let other modules publish type-safe "notification payloads".

**Why deferred:** The free-form dictionary works; the Stubble engine renders missing variables as empty strings (safe default). Schema rigour matters once ≥3 modules emit templates independently and drift appears.

**Pick this up when:** A typo-driven production incident occurs OR the template count exceeds ~30 and catalog navigation in the UI suffers.

**Starting points:**
- Extend `ITemplateRegistrar.RegisterTemplate` to accept `Dictionary<string, TemplateVariableMeta>` (description + required/optional + sample).
- Use these to drive the `TemplateEditorDialog` preview form and a JSON-schema dump for API consumers.
