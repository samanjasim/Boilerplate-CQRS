# Future Roadmap

Features planned for future implementation. These are not part of the current boilerplate scope but the architecture supports them.

## Tenant & Organization
- Tenant settings page (logo, custom branding, config)
- User invitation flow (tenant admin invites users by email with role pre-assignment)
- Subdomain-based tenant resolution (e.g., `acme.starter.com`)
- Per-tenant database isolation (connection string field exists on Tenant entity)
- Tenant-level feature flags

## Billing & Subscription
- Subscription plans with feature gating
- Payment integration (Stripe, Paddle)
- Usage metering and limits per tenant
- Invoice generation

## Notifications
- In-app notification center (bell icon, unread count)
- Ably real-time push for live notifications
- Email notification preferences per user
- Notification templates and channels

## Communication
- SMS implementation via Twilio (interface exists, toggle in config)
- Push notifications (FCM/APNs)
- Webhook system for external integrations

## Monitoring & Analytics
- User activity tracking / login history
- System settings management page
- Dashboard analytics with charts
- OpenTelemetry distributed tracing

## Data & Export
- Export audit logs to CSV/PDF
- Report generation
- Data import/export utilities
- File/blob storage service (S3, Azure Blob)

## Security
- Two-factor authentication (2FA/TOTP)
- API key management for service-to-service auth
- IP allowlisting per tenant
- Session management (view/revoke active sessions)
