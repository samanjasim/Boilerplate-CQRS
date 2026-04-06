# Strategic Roadmap: Boilerplate Feature Expansion & Use-Case Coverage

**Date:** 2026-04-06
**Status:** Approved
**Scope:** Feature roadmap, horizontal engines, vertical starter kits, infrastructure improvements

---

## Context

The boilerplate is a production-ready multi-tenant SaaS foundation with 15 backend features and 17 frontend modules. With the analytics dashboard completing the current feature set, the next strategic phase focuses on:

1. **Horizontal engines** — General-purpose features that unlock multiple business verticals
2. **Infrastructure improvements** — CI/CD, search, real-time deepening
3. **Vertical starter kits** — Pre-configured domain modules built on top of the engines

The goal is to maximize the boilerplate's value for two audiences:
- **Solo devs / small agencies** — Want speed-to-delivery and broad coverage
- **Mid-size dev shops / startups** — Want clean architecture and extensibility

The philosophy is **horizontal engines + vertical starters**: build powerful general-purpose features, then offer premium starter kits that demonstrate how to use them for specific domains (e-commerce, HR, education, ERP, SaaS).

---

## Current Feature Inventory (Already Built)

| Category | Features |
|----------|----------|
| **Auth & Identity** | JWT + refresh tokens, 2FA/TOTP, session management, invitations, password reset, email verification |
| **User Management** | CRUD, status lifecycle (active/suspended/deactivated/locked), profile editing |
| **Roles & Permissions** | Role CRUD, permission matrix, module-action model, policy-based auth |
| **Multi-Tenancy** | Global EF query filters, per-tenant settings, branding, subdomain resolution |
| **Billing** | 4-tier plans, subscriptions, usage metering, price grandfathering, payment abstraction |
| **Feature Flags** | Platform + tenant overrides, boolean/string/int/JSON, enforcement hooks, Redis caching |
| **File Storage** | S3/MinIO upload/download, signed URLs, categories, grid/list views |
| **Reports** | Async generation (CSV/PDF/Excel), MassTransit processing, download/delete |
| **Import/Export** | Registry-based CSV import/export, async processing, preview/validation, templates |
| **Notifications** | In-app + email, per-user preferences, real-time via Ably |
| **Audit Logs** | Automatic change tracking, JSON diff, device info, filterable viewer |
| **API Keys** | Tenant/platform scoped, permissions, expiration, emergency revoke |
| **Webhooks** | HMAC signing, retry with backoff, delivery log, 8 event types |
| **Settings** | Key-value per tenant, multiple field types, admin UI |
| **Analytics** | Dashboard with trend metrics, period selection, charts (spec complete) |

---

## Tier 1: Horizontal Engines (Build First)

These are the highest-impact additions — each unlocks multiple verticals.

### 1A. Workflow & Approvals Engine

**Problem:** Every business app has multi-step processes (order fulfillment, leave approvals, student enrollment, support escalation). Without a workflow engine, each developer builds this from scratch.

**Scope:**
- **Workflow Definitions** — Admin-configurable state machines: define states, transitions, conditions, and actions. Stored as JSON in DB, editable via visual UI
- **Approval Chains** — Sequential or parallel approvers, role-based or user-based, auto-escalation on timeout, delegation
- **Task Queue** — "My pending approvals" inbox per user, with filters and priority sorting
- **SLA Tracking** — Per-step time limits, overdue alerts, escalation triggers
- **Hooks** — On-enter/on-exit actions per state: send notification, trigger webhook, update field, call external API
- **Audit Trail** — Full history of transitions, approvals, rejections with comments and timestamps

**Domain Entities:**
- `WorkflowDefinition` — Name, description, states (JSON), transitions (JSON), tenant-scoped
- `WorkflowInstance` — Definition reference, entity type, entity ID, current state, started at, completed at
- `WorkflowStep` — Instance reference, from state, to state, actor, action (approve/reject/escalate), comment, timestamp
- `ApprovalTask` — Instance reference, step, assigned user/role, due date, status (pending/approved/rejected/escalated)
- `WorkflowTransitionLog` — Full audit of all transitions with metadata

**Architecture:**
- Any entity becomes "workflowable" by implementing `IWorkflowEntity` interface
- `IWorkflowService` orchestrates state transitions, validates rules, triggers hooks
- MassTransit consumers for async hook execution (notifications, webhooks, escalation checks)
- Frontend: Workflow designer (visual state builder), task inbox, approval dialogs

**Verticals Unlocked:** HR (leave/expense approval), E-commerce (order processing), Education (enrollment), SaaS (support tickets), ERP (purchase orders)

---

### 1B. Comments & Activity Engine

**Problem:** Almost every entity in a business app needs a comment thread and activity log — CRM notes, ticket replies, approval comments, order notes.

**Scope:**
- **Threaded Comments** — Polymorphic: attach to any entity type via (entityType, entityId). Supports replies (parent comment), @mentions, file attachments
- **Activity Feed** — Auto-generated timeline per entity: status changes, field updates, comments, file uploads
- **@Mentions** — Tag users in comments, triggers in-app + email notification via existing notification system
- **Rich Text** — Markdown support in comment body with inline image embedding
- **Reactions** — Lightweight emoji reactions on comments (optional, feature-flag gated)

**Domain Entities:**
- `Comment` — EntityType, EntityId, ParentCommentId (nullable for threads), AuthorId, Body (markdown), Mentions (JSON array of user IDs), CreatedAt, UpdatedAt
- `CommentAttachment` — CommentId, FileMetadataId (reuses existing file system)
- `CommentReaction` — CommentId, UserId, ReactionType (emoji code)
- `ActivityEntry` — EntityType, EntityId, Action (created/updated/status_changed/comment_added/file_uploaded), ActorId, Metadata (JSON — old/new values, details), Timestamp

**Architecture:**
- Generic services: `ICommentService`, `IActivityService` — any feature uses them without coupling
- Activity entries auto-created via domain event listeners (reuses existing audit infrastructure)
- Frontend: Reusable `<CommentThread entityType="order" entityId={id} />` and `<ActivityFeed entityType="order" entityId={id} />` components
- Real-time: New comments/activities pushed via Ably to viewers of that entity

**Verticals Unlocked:** All — every business app benefits from collaboration on records

---

### 1C. AI Integration Layer

**Problem:** Every modern SaaS needs AI capabilities. Building LLM integration from scratch (provider switching, token management, RAG pipeline, per-tenant quotas) is complex and repetitive.

**Scope:**
- **Provider Abstraction** — `IAiService` with implementations for OpenAI, Anthropic, and local models (Ollama). Per-tenant config for provider selection and API key storage
- **Chat Interface** — Reusable AI chat component with conversation history, streaming responses, markdown rendering
- **RAG Pipeline** — Document ingestion (PDF, CSV, text), vector storage via pgvector (PostgreSQL extension — no extra service), semantic search, context-aware responses
- **AI Assistants** — Per-tenant configurable assistants: system prompt, knowledge base (uploaded docs), available tools/functions
- **Usage & Quotas** — Token usage tracking per tenant (extends existing `IUsageTracker`), rate limiting per plan, cost estimation dashboard
- **Pre-built Capabilities** — Summarization, entity extraction, smart search, content generation templates

**Domain Entities:**
- `AiAssistant` — TenantId, Name, SystemPrompt, Model, Provider, MaxTokens, Temperature, KnowledgeBaseIds
- `AiConversation` — AssistantId, UserId, Title, CreatedAt, LastMessageAt
- `AiMessage` — ConversationId, Role (user/assistant/system), Content, TokensUsed, Timestamp
- `AiDocument` — TenantId, Name, FileMetadataId, ChunkCount, EmbeddingStatus, ProcessedAt
- `AiDocumentChunk` — DocumentId, Content, Embedding (vector), ChunkIndex
- `AiUsageLog` — TenantId, UserId, Provider, Model, InputTokens, OutputTokens, Cost, Timestamp

**Architecture:**
- pgvector for embeddings — no extra infrastructure service needed
- Provider adapters behind `IAiService` interface, configured per tenant in settings
- MassTransit consumer for async document ingestion and chunking
- Frontend: Chat widget, document upload for knowledge base, assistant config UI, usage dashboard
- Feature-flag gated: AI features enabled per billing plan tier

**Verticals Unlocked:** SaaS (smart search, chatbots), Education (AI tutors, auto-grading assistance), E-commerce (product recommendations, support), HR (resume parsing)

---

## Tier 2: High-Value Feature Modules

### 2A. Multi-Channel Communication Hub

**Problem:** The boilerplate only has transactional email and basic in-app notifications. Real business apps need campaign emails, SMS, WhatsApp, push — all managed from one place.

**Scope:**
- **Channel Registry** — Pluggable channel system: Email (SMTP/SendGrid/SES), SMS (Twilio), Push (FCM/APNs), WhatsApp (Twilio/Meta API), In-App (existing Ably)
- **Message Templates** — Per-tenant, per-channel, per-locale templates with variable substitution. Visual HTML email builder
- **Campaigns** — Bulk messaging: recipient lists/segments, scheduling, A/B testing, delivery tracking, unsubscribe management
- **Contact Preferences** — Per-user channel opt-in/opt-out (extends existing `NotificationPreference`)
- **Delivery Tracking** — Unified log across all channels: sent, delivered, opened, clicked, bounced, failed
- **Trigger Rules** — "When X happens, send Y via Z channel" — configurable automation tied to domain events and workflows

**Domain Entities:**
- `ChannelConfig` — TenantId, ChannelType, Credentials (encrypted), IsActive
- `MessageTemplate` — TenantId, ChannelType, Locale, Name, Subject, Body, Variables (JSON)
- `Campaign` — TenantId, Name, ChannelType, TemplateId, SegmentFilter (JSON), ScheduledAt, Status, Stats (sent/delivered/opened/clicked)
- `CampaignRecipient` — CampaignId, UserId, Status, DeliveredAt, OpenedAt, ClickedAt
- `DeliveryLog` — TenantId, ChannelType, RecipientId, TemplateId, Status, SentAt, DeliveredAt, ErrorMessage

**Architecture:**
- `IChannelProvider` interface per channel type, `IMessageDispatcher` orchestrates multi-channel sends
- MassTransit consumers for async delivery (extends existing pattern)
- Hooks into Workflow Engine (workflow steps can trigger messages)
- Frontend: Template builder, campaign wizard, delivery analytics dashboard, channel config admin

---

### 2B. Scheduling & Calendar Engine

**Problem:** Appointments, shifts, classes, delivery windows, meetings — every vertical needs time-based scheduling.

**Scope:**
- **Events & Schedules** — One-time or recurring events (RRULE standard), with attendees, location, description
- **Availability** — Define availability windows per user/resource, conflict detection before booking
- **Booking** — Book time slots: appointment (1-on-1), class enrollment (many-to-one), resource reservation (rooms, equipment)
- **Reminders** — Automated reminders via Communication Hub at configurable intervals before events
- **Calendar Views** — Day, week, month views in frontend. Filterable by resource/user/type
- **Timezone Handling** — All times stored UTC, displayed in user's or tenant's timezone
- **iCal Integration** — Export to .ics files, subscribe to calendar feed URL

**Domain Entities:**
- `CalendarEvent` — TenantId, Title, Description, Location, StartUtc, EndUtc, RecurrenceRule, OrganizerUserId, EntityType (nullable), EntityId (nullable)
- `EventAttendee` — EventId, UserId, Status (invited/accepted/declined/tentative)
- `Availability` — TenantId, UserId or ResourceId, DayOfWeek, StartTime, EndTime, EffectiveFrom, EffectiveTo
- `Booking` — EventId, BookedByUserId, BookedForUserId, Status (confirmed/cancelled/no-show), Notes
- `Reminder` — EventId, ChannelType, MinutesBefore, SentAt

**Architecture:**
- `ISchedulingService` with conflict detection, recurrence expansion (RRULE → date list), timezone conversion
- Reminders route through Communication Hub
- Workflow Engine can create/cancel bookings as workflow actions
- Frontend: Calendar component, booking flow, availability editor

---

### 2C. Reporting & Dashboard Engine

**Problem:** Every business needs custom reports. The current analytics dashboard is admin-only and fixed. This engine lets tenants build their own reports and dashboards.

**Scope:**
- **Report Builder** — Tenant admins configure reports: select entity/data source, choose fields, add filters, pick chart type, set grouping/aggregation
- **Dashboard Builder** — Drag-and-drop dashboard with configurable widgets: KPI cards, charts, tables, counters
- **Data Sources** — Pre-defined data sources per entity type with tenant-scoped access and field metadata
- **Scheduled Reports** — Auto-generate and email reports on a schedule via Communication Hub
- **Export Formats** — PDF, CSV, Excel (extends existing export infrastructure)
- **Saved Reports** — Per-user and per-tenant saved configurations, shareable within tenant

**Domain Entities:**
- `ReportDefinition` — TenantId, Name, DataSource, Fields (JSON), Filters (JSON), GroupBy, AggregationType, ChartType, CreatedByUserId
- `DashboardLayout` — TenantId, Name, IsDefault, Widgets (JSON — position, size, report reference)
- `DashboardWidget` — DashboardId, Type (kpi/chart/table/counter), ReportDefinitionId, Config (JSON)
- `ScheduledReport` — ReportDefinitionId, CronExpression, Recipients (JSON), Format, LastRunAt, NextRunAt

**Architecture:**
- Query builder translates report config to EF Core dynamic queries, respecting tenant filters
- Extends existing export infrastructure for PDF/CSV generation
- Communication Hub integration for scheduled report delivery
- Frontend: Report builder UI (field picker, filter builder, chart preview), dashboard layout editor (grid-based drag-and-drop)

---

## Tier 3: Infrastructure & Developer Experience

### 3A. CI/CD Pipelines & Deployment Templates

**Scope:**
- **GitHub Actions Workflows:**
  - `build-and-test.yml` — Build backend + frontend, run linters, type checks on every PR
  - `deploy-staging.yml` — Auto-deploy to staging on merge to `develop`
  - `deploy-production.yml` — Manual trigger or tag-based deploy to production
  - `database-migration.yml` — Safe EF migration runner with rollback support
- **Deployment Templates:**
  - Docker Compose (production) — Hardened compose with SSL termination (Traefik/Caddy), health checks, restart policies, log rotation
  - Kubernetes — Helm chart with configurable replicas, resource limits, HPA, ingress
  - Azure — App Service + Azure SQL + Blob Storage ARM/Bicep template
  - AWS — ECS + RDS + S3 CloudFormation template
- **Environment Management** — `.env.example` files per environment, secrets management guide, environment-specific appsettings templates

### 3B. Advanced Search (Meilisearch)

**Why Meilisearch:** Lightweight, easy to self-host, great search UX out of the box, fast setup, cloud offering available. Matches the boilerplate's developer-friendly philosophy.

**Scope:**
- `ISearchService` abstraction with Meilisearch implementation
- Domain event listeners auto-sync entities to search index on create/update/delete
- Global search bar — federated results across all entity types
- Per-entity search — replace EF `LIKE` queries with Meilisearch for list pages (optional, feature-flag gated)
- Faceted filtering — filter by category, status, date range, tenant
- Tenant isolation — separate indices per tenant or tenant-scoped filter keys

### 3C. Enhanced Real-time (Ably Deepening)

Keep Ably as the cloud-based real-time provider. Deepen the integration:
- **Presence** — Show who's viewing the same entity (collaboration awareness)
- **Typing Indicators** — Show when someone is composing a comment
- **Push Notifications** — Ably push for mobile clients
- **Channel Architecture** — Well-defined naming: `tenant:{id}:entity:{type}:{id}` for granular subscriptions
- **Connection Management** — Reconnection handling, offline queue, connection state UI indicator

---

## Tier 4: Vertical Starter Kits (Premium Modules)

Each starter kit is a pre-configured package built ON TOP of the horizontal engines. It includes: domain entities, seed data, sample pages, pre-configured workflows, and documentation. Gives buyers a 30-60 minute head start on their specific domain.

### 4A. SaaS Platform Starter (Project Management)

**Entities:** Project, Task, Board, Sprint, Label, Priority
**Sample Pages:** Project board (Kanban), task detail, sprint planning, project settings
**Pre-configured:**
- Workflow: Task lifecycle (Backlog → In Progress → Review → Done)
- Comments: Task discussions with @mentions
- Activity: Project timeline
- Scheduling: Sprint date ranges, milestones

### 4B. E-Commerce Starter

**Entities:** Product, Category, ProductVariant, Cart, CartItem, Order, OrderItem, Coupon, Review, ShippingMethod
**Sample Pages:** Product catalog, product detail, cart, checkout, order history, admin order management
**Pre-configured:**
- Workflow: Order processing (Placed → Paid → Shipped → Delivered), return/refund flow
- Communication: Order confirmations, shipping updates, abandoned cart emails
- Scheduling: Delivery windows
- Reporting: Sales by period, top products, revenue dashboard

### 4C. Education / School Management Starter

**Entities:** Student, Teacher, Course, Section, Enrollment, Grade, Assignment, Attendance, Guardian, AcademicYear, Term
**Sample Pages:** Student roster, course catalog, grade book, attendance tracker, parent portal, timetable
**Pre-configured:**
- Workflow: Enrollment approval, grade submission & review
- Scheduling: Class timetables, exam dates, academic calendar
- Communication: Parent notifications, grade reports, absence alerts
- AI: Tutoring assistant, auto-grading suggestions
- Reporting: Grade distribution, attendance analytics, enrollment trends

### 4D. HR & Payroll Starter

**Entities:** Employee, Department, Position, LeaveType, LeaveRequest, Attendance, PayrollPeriod, SalaryComponent, ExpenseReport
**Sample Pages:** Employee directory, org chart, leave calendar, attendance dashboard, payroll summary, expense tracker
**Pre-configured:**
- Workflow: Leave approval (employee → manager → HR), expense approval, onboarding checklist
- Scheduling: Shift management, leave calendar, payroll periods
- Communication: Policy updates, pay slip delivery, leave status notifications
- Reporting: Headcount, attendance rates, leave balances, payroll summaries

### 4E. ERP Lite Starter

**Entities:** Supplier, PurchaseOrder, PurchaseOrderItem, Inventory, InventoryTransaction, Invoice, InvoiceLine, Account, Warehouse
**Sample Pages:** Supplier list, PO management, inventory dashboard, invoice generator, basic ledger
**Pre-configured:**
- Workflow: PO approval (requester → manager → finance), invoice approval, stock transfer
- Communication: PO email to supplier, invoice delivery, low-stock alerts
- Reporting: Inventory valuation, accounts receivable/payable, purchase analytics

---

## Use-Case Coverage Matrix

| Capability | SaaS | E-commerce | Education | HR | ERP |
|-----------|------|-----------|-----------|-----|-----|
| Auth & Roles | ✅ built | ✅ built | ✅ built | ✅ built | ✅ built |
| Multi-tenancy | ✅ built | ✅ built | ✅ built | ✅ built | ✅ built |
| Billing & Subscriptions | ✅ built | ✅ built | ✅ built | ✅ built | ✅ built |
| File Storage | ✅ built | ✅ built | ✅ built | ✅ built | ✅ built |
| Webhooks | ✅ built | ✅ built | ✅ built | ✅ built | ✅ built |
| Audit Trail | ✅ built | ✅ built | ✅ built | ✅ built | ✅ built |
| Import/Export | ✅ built | ✅ built | ✅ built | ✅ built | ✅ built |
| Feature Flags | ✅ built | ✅ built | ✅ built | ✅ built | ✅ built |
| API Keys | ✅ built | ✅ built | ✅ built | ✅ built | ✅ built |
| Analytics Dashboard | ✅ built | ✅ built | ✅ built | ✅ built | ✅ built |
| **Workflow & Approvals** | ✅ T1 | ✅ T1 | ✅ T1 | ✅ T1 | ✅ T1 |
| **Comments & Activity** | ✅ T1 | ✅ T1 | ✅ T1 | ✅ T1 | ✅ T1 |
| **AI Integration** | ✅ T1 | ✅ T1 | ✅ T1 | ⚠️ optional | ⚠️ optional |
| **Communication Hub** | ✅ T2 | ✅ T2 | ✅ T2 | ✅ T2 | ✅ T2 |
| **Scheduling & Calendar** | ⚠️ optional | ✅ T2 | ✅ T2 | ✅ T2 | ⚠️ optional |
| **Reporting Engine** | ✅ T2 | ✅ T2 | ✅ T2 | ✅ T2 | ✅ T2 |
| **Advanced Search** | ✅ T3 | ✅ T3 | ✅ T3 | ⚠️ optional | ✅ T3 |
| **CI/CD & Deploy** | ✅ T3 | ✅ T3 | ✅ T3 | ✅ T3 | ✅ T3 |
| **Domain Entities** | ✅ T4-A | ✅ T4-B | ✅ T4-C | ✅ T4-D | ✅ T4-E |

**Coverage after Tier 1+2:** ~80-90% of common needs across all five verticals
**Coverage after Tier 4:** ~95%+ with domain-specific entities and pre-configured workflows

---

## Implementation Sequence (Recommended)

### Phase 1: Engines Foundation
1. **Comments & Activity Engine** — Simplest engine, high impact, foundation for others
2. **Workflow & Approvals Engine** — Most complex but highest value, needed by starter kits
3. **AI Integration Layer** — Market differentiator, independent of other engines

### Phase 2: Communication & Time
4. **Multi-Channel Communication Hub** — Extends existing notification system, needed by workflows
5. **Scheduling & Calendar Engine** — Independent, uses Communication Hub for reminders

### Phase 3: Data & Search
6. **Reporting & Dashboard Engine** — Builds on all entities, high perceived value
7. **Advanced Search (Meilisearch)** — Enhances all list views, independent

### Phase 4: Infrastructure
8. **CI/CD Pipelines** — Can be done in parallel with any phase
9. **Enhanced Ably Integration** — Deepens existing real-time capabilities

### Phase 5: Vertical Starter Kits
10. **SaaS Starter** — Closest to current boilerplate DNA
11. **E-Commerce Starter** — High market demand
12. **Education Starter** — Strong niche demand
13. **HR Starter** — Enterprise market
14. **ERP Lite Starter** — Most complex, builds on all engines

---

## Pricing Model (Suggested)

| Tier | What's Included | Target |
|------|----------------|--------|
| **Core (Free/Base)** | Auth, Users, Roles, Tenants, Files, Settings, Audit, Notifications | Open-source or low price to attract developers |
| **Professional** | Core + Billing, Feature Flags, API Keys, Webhooks, Import/Export, Analytics | Solo devs, small agencies |
| **Enterprise** | Professional + All Tier 1-3 engines (Workflow, AI, Comments, Communication, Scheduling, Reporting, Search) | Mid-size teams, startups |
| **Starter Kits** | Individual premium add-ons (E-Commerce, Education, HR, ERP, SaaS) | Per-vertical pricing, sold separately |

---

## Next Steps

This roadmap feeds into two follow-up specs:
1. **Modularization & CLI** — How to make features optional, build the CLI for add/remove modules
2. **Individual feature specs** — Each Tier 1-3 feature gets its own detailed design spec before implementation

The modularization spec should come next, as it defines the module boundaries that all future features will follow.
