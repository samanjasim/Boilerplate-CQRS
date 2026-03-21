# Future Roadmap

Features planned for future implementation. The architecture supports all of these — they can be added per project as needed.

## Data & Operations
- Export audit logs / user lists to CSV/PDF
- Report generation with charts
- Data import/export utilities
- File/blob storage service (S3, Azure Blob, local disk) with IStorageService interface
- System settings management page (key-value config per tenant)

## Tenant & Organization
- Tenant settings page (logo, custom branding, config)
- Subdomain-based tenant resolution (e.g., `acme.starter.com`)
- Per-tenant database isolation (connection string field exists on Tenant entity)
- Tenant-level feature flags

## Integration & Extensibility
- Webhook system for external integrations (register URLs, HMAC signatures, retry)
- API key management for service-to-service auth (X-Api-Key header)
- Push notifications (FCM/APNs)

## Billing & Subscription
- Subscription plans with feature gating
- Payment integration (Stripe, Paddle)
- Usage metering and limits per tenant
- Invoice generation

## Monitoring & Analytics
- Dashboard analytics with charts (chart.js or recharts)
- OpenTelemetry distributed tracing
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
- ~~User invitation flow~~ — Implemented with email + token + role pre-assignment
- ~~SMS via Twilio~~ — Implemented with toggle in config
- ~~User profile page~~ — Implemented with edit, change password, 2FA setup
