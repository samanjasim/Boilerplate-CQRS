# Future Roadmap

Features planned for future implementation. The architecture supports all of these — they can be added per project as needed.

## Priority Order (Active Roadmap)

| # | Feature | Status | Dependencies | Spec |
|---|---------|--------|-------------|------|
| 1 | ~~Tenant Feature Flags~~ | ✅ Done | — | — |
| 2 | ~~Invitation System Overhaul~~ | ✅ Done | Feature Flags | — |
| 3 | ~~Billing & Subscriptions~~ | ✅ Done | Feature Flags | `docs/superpowers/specs/2026-03-31-billing-subscriptions-design.md` |
| 4 | **Tenant Self-Service Portal** | 📋 Spec Ready | Billing, Feature Flags | `docs/superpowers/specs/2026-03-31-tenant-self-service-design.md` |
| 5 | ~~Webhooks~~ | ✅ Done | — | `docs/superpowers/specs/2026-04-02-webhooks-design.md` |
| 6 | Analytics Dashboard | ⬜ Planned | — | — |
| 7 | ~~Import/Export~~ | ✅ Done | — | `docs/superpowers/specs/2026-04-03-import-export-design.md` |

---

## Tenant Self-Service Portal (Next Up)

Dedicated tenant admin experience — onboarding wizard, organization page, usage visibility with upgrade prompts, tenant-scoped dashboard and audit logs. See full spec.

**6 phases:** Sidebar + Organization route → Usage tab → Tenant-scoped dashboard → Onboarding wizard → Activity tab → Usage threshold email notifications

---

## Data & Operations
- Export audit logs / user lists to CSV/PDF
- Report generation with charts
- Data import/export utilities
- ~~File/blob storage service~~ — **Implemented** (S3/MinIO with IStorageService, IFileService, file manager UI)
- ~~System settings management page~~ — **Implemented** (key-value config per tenant with admin UI)

## Tenant & Organization
- ~~Tenant settings page (logo, custom branding, config)~~ — **Implemented**
- ~~Subdomain-based tenant resolution~~ — **Implemented** (e.g., `acme.starter.com` with SSO)
- Per-tenant database isolation (connection string field exists on Tenant entity)
- ~~Tenant-level feature flags~~ — **Implemented** (platform definitions, tenant overrides, enforcement hooks, caching)
- Tenant self-service portal — **Spec ready** (onboarding wizard, organization page, usage + limits, tenant-scoped dashboard)

## Integration & Extensibility
- ~~Webhook system for external integrations~~ — **Implemented** (register URLs, HMAC-SHA256 signing, MassTransit delivery with retry, delivery log, event handlers, admin overview, feature-flag gated)
- ~~API key management for service-to-service auth~~ — **Implemented** (tenant + platform keys, scoped permissions, emergency revoke)
- ~~Push notifications~~ — **Dropped** (not needed for SaaS boilerplate)

## Billing & Subscription
- ~~Subscription plans with feature gating~~ — **Implemented** (4-tier plans, feature flag presets, auto-assign on registration)
- ~~Usage metering and limits per tenant~~ — **Implemented** (Redis atomic counters via IUsageTracker)
- ~~Payment integration abstraction~~ — **Implemented** (IBillingProvider + MockBillingProvider, Stripe-ready interface)
- ~~Price grandfathering + history~~ — **Implemented** (locked prices on subscription, PlanPriceHistory audit trail)
- ~~Plan localization~~ — **Implemented** (translations JSON per plan, multi-language pricing page)
- Invoice generation (future — wire with real billing provider)

## Monitoring & Analytics
- Dashboard analytics with charts (chart.js or recharts)
- ~~OpenTelemetry distributed tracing~~ — **Implemented** (Jaeger + Prometheus via OTLP)
- IP allowlisting per tenant

---

## Already Implemented (for reference)

These were on the roadmap and are now part of the boilerplate:

- ~~Two-factor authentication (2FA/TOTP)~~ — Implemented with backup codes
- ~~Session management (view/revoke active sessions)~~ — Implemented with device detection
- ~~User activity tracking / login history~~ — Implemented with success/failure tracking
- ~~In-app notification center~~ — Implemented with bell icon, unread count
- ~~Ably real-time push for live notifications~~ — Implemented with channel per user
- ~~Email notification preferences per user~~ — Implemented with per-type toggles
- ~~User invitation flow~~ — Implemented with email + token + role pre-assignment + multi-tenant targeting
- ~~SMS via Twilio~~ — Implemented with toggle in config
- ~~User profile page~~ — Implemented with edit, change password, 2FA setup
- ~~System settings management~~ — Implemented with per-tenant overrides and admin UI
- ~~Subdomain-based tenant resolution~~ — Implemented with SSO and shared cookie auth
- ~~API key management~~ — Implemented with tenant/platform scopes, expiration, emergency revoke
- ~~OpenTelemetry distributed tracing~~ — Implemented with Jaeger UI and Prometheus metrics
- ~~Tenant-level feature flags~~ — Implemented with tenant overrides, enforcement, opt-out, and caching
- ~~Billing & Subscriptions~~ — Implemented with 4-tier plans, feature flag presets, Redis usage tracking, price grandfathering, mock billing provider
- ~~Invitation system overhaul~~ — Implemented with multi-tenant targeting, permission hierarchy, default registration roles, feature-flag-gated custom roles
- ~~Registration security~~ — Public /register disabled, /register-tenant auto-assigns Free plan, tenant owner role configurable
- ~~Webhooks~~ — Implemented with CRUD, HMAC-SHA256 payload signing, MassTransit delivery with retry (1m/5m/30m/2h/24h), delivery log with status tracking, 8 event types, admin overview with 24h stats, feature-flag gated per plan, secret regeneration, cleanup job
- ~~Import/Export~~ — Implemented with registry-based architecture, CSV import with preview/validation/progress tracking, CSV/PDF export, async MassTransit processing with batch commits, tenant-targeted imports for SuperAdmin, feature-flag gated per plan, reusable ImportWizard component
