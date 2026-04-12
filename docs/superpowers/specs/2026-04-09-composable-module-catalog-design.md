# Composable Module Catalog: Feature Expansion & Use-Case Coverage

**Date:** 2026-04-09
**Status:** Approved
**Supersedes:** 2026-04-06-strategic-roadmap-design.md (replaced tier-based approach with composable modules)
**Scope:** Complete module catalog, implementation waves, preset combinations, per-module architecture

---

## Context

The boilerplate has adopted a modular architecture (see `2026-04-05-module-architecture-design.md` and `2026-04-07-true-modularity-refactor.md`) with:
- `Starter.Abstractions` — Pure contracts (IModule, Capabilities, Readers)
- Module-per-DbContext pattern — each module owns its own database context and migrations
- Capabilities pattern — interfaces in Abstractions with Null Object fallbacks in core
- Four extracted modules: Billing, Webhooks, ImportExport, Products (reference implementation)
- CLI scaffolding with `--modules` selection

### Vision

Instead of monolithic "starter kits," the boilerplate offers a **flat catalog of composable modules**. Each module is self-contained, follows the `IModule` pattern, and can be freely combined. "Starter presets" are just recommended module combinations for specific verticals — pre-selected checkboxes in the CLI, not separate codebases.

### Target Audiences

- **Solo devs / small agencies** — Buy the boilerplate, pick modules for their client project, ship fast
- **Mid-size dev shops / startups** — Use the engines as foundation, build domain logic on top
- **The boilerplate author** — Uses the boilerplate + modules to build standalone products (e.g., Education SaaS)

### Key Decisions

- **Real-time stays cloud-based** — Ably (or similar), no SignalR. Users of the boilerplate should also use cloud services for operational concerns.
- **LLM provider: platform-managed keys** — Platform owner configures API keys, tenants consume AI through the platform, billed via existing billing module.
- **RAG vector storage: Qdrant** — Dedicated vector DB in Docker Compose, purpose-built for similarity search.
- **AI function calling: via MediatR** — AI tools map to existing MediatR commands, reusing validation, authorization, and audit trails.
- **Modules are composable, not monolithic** — No "Education Starter" as one package. Instead: Core + Students + Courses + Scheduling + AI + Communication = Education preset.
- **Domain modules cross boundaries** — "Leave & Attendance" serves both HR and Education. "Products & Catalog" serves E-Commerce, ERP, and POS.

---

## Core (Always Present)

| Feature | Why Core |
|---------|----------|
| Auth (login, register, JWT, 2FA, sessions) | Every app needs authentication |
| Users (CRUD, activate/suspend) | Every app has users |
| Roles & Permissions | Every app needs authorization |
| Tenants | Multi-tenancy is the boilerplate's key differentiator |
| Settings (system settings per tenant) | Every app needs configuration |
| Dashboard (basic) | Landing page after login, module slots for cards |

---

## Existing Modules (Already Extracted)

| Module | Status | Description |
|--------|--------|-------------|
| Billing | Extracted | Subscription plans, payments, usage tracking, quota enforcement |
| Webhooks | Extracted | HMAC-signed delivery, retry with backoff, event subscriptions |
| ImportExport | Extracted | Registry-based CSV import/export, async processing |
| Products | Extracted | Reference e-commerce module, CRUD, quota checking |

---

## Module Catalog — New Modules by Implementation Wave

### Wave 1: Cross-Domain Engines (Foundations)

Everything else builds on these. Comments & AI can be built in parallel.

#### Module: Comments & Activity

**Purpose:** Threaded comments and auto-generated activity feeds on any entity. Every business app needs collaboration on records.

**Used By:** All verticals — CRM notes, ticket replies, approval comments, order notes, employee records

**Capability Exposed:** `ICommentService`, `IActivityService` in `Starter.Abstractions/Capabilities/`

**Entities (own DbContext: `CommentsDbContext`):**
- `Comment` — EntityType, EntityId, ParentCommentId (threads), AuthorId, Body (markdown), Mentions (JSON), CreatedAt, UpdatedAt
- `CommentAttachment` — CommentId, FileMetadataId (reuses Files module if present, or stores reference)
- `CommentReaction` — CommentId, UserId, ReactionType (emoji code)
- `ActivityEntry` — EntityType, EntityId, Action (created/updated/status_changed/comment_added/file_uploaded), ActorId, Metadata (JSON), Timestamp

**Key Design:**
- Polymorphic attachment: any entity becomes commentable via `(entityType, entityId)` pair
- Activity entries auto-created via domain event listeners
- Frontend: `<CommentThread entityType="order" entityId={id} />` and `<ActivityFeed entityType="order" entityId={id} />` reusable components
- Real-time: new comments pushed via Ably
- Other modules contribute activity types by publishing events

**Permissions:** Comments.View, Comments.Create, Comments.Delete, Comments.Manage
**Effort:** Small-Medium

---

#### Module: AI Integration

**Purpose:** Full AI platform — LLM abstraction, RAG pipeline, configurable assistants, function calling, usage tracking.

**Used By:** All verticals — smart search, chatbots, content generation, entity summarization, AI-powered actions

**Capability Exposed:** `IAiService` in `Starter.Abstractions/Capabilities/`

**Entities (own DbContext: `AiDbContext`):**
- `AiAssistant` — TenantId, Name, SystemPrompt, Model, Provider, Temperature, MaxTokens, KnowledgeBaseDocIds (JSON), EnabledToolNames (JSON)
- `AiConversation` — AssistantId, UserId, Title, MessageCount, CreatedAt, LastMessageAt
- `AiMessage` — ConversationId, Role (user/assistant/system/tool), Content, TokensUsed, ToolCalls (JSON), Timestamp
- `AiDocument` — TenantId, Name, FileRef, ChunkCount, EmbeddingStatus (Pending/Processing/Completed/Failed), ProcessedAt
- `AiDocumentChunk` — DocumentId, Content, ChunkIndex, QdrantPointId (UUID reference to Qdrant)
- `AiTool` — Name, Description, ParameterSchema (JSON), CommandType (fully qualified), RequiredPermission
- `AiUsageLog` — TenantId, UserId, Provider, Model, InputTokens, OutputTokens, EstimatedCost, Timestamp

**Key Design:**
- **Provider Abstraction:** Internal `IAiProvider` with OpenAI, Anthropic, Ollama implementations. Platform admin configures active provider + API key in system settings.
- **RAG Pipeline:** Upload → Chunk (512 tokens, 50 overlap) → Embed → Store in Qdrant. Query → Embed → Qdrant similarity → Top-K → Inject context → LLM response.
- **Qdrant:** Added to Docker Compose (ports 6333/6334). Each tenant gets a separate collection.
- **Function Calling:** AI tools map to MediatR commands via `IAiToolRegistry`. Other modules register tools in their `ConfigureServices`. Execution goes through full MediatR pipeline (validation, auth, audit).
- **Streaming:** SSE endpoint for streaming chat responses.
- **Usage:** Extends `IUsageTracker` with "ai_tokens" metric. `IQuotaChecker` enforces per-plan limits.
- **MassTransit Consumer:** Async document processing (chunking + embedding).

**API Endpoints:**
```
POST   /api/v1/ai/chat                — Send message (full response)
POST   /api/v1/ai/chat/stream         — Send message (SSE streaming)
GET    /api/v1/ai/conversations        — List conversations
GET    /api/v1/ai/conversations/{id}   — Get conversation with messages
DELETE /api/v1/ai/conversations/{id}   — Delete conversation
CRUD   /api/v1/ai/assistants           — Manage assistants
POST   /api/v1/ai/documents            — Upload document for RAG
GET    /api/v1/ai/documents            — List knowledge base
DELETE /api/v1/ai/documents/{id}       — Remove document
GET    /api/v1/ai/tools                — List available AI tools
POST   /api/v1/ai/search              — Semantic search
GET    /api/v1/ai/usage               — Usage stats
```

**Frontend Components:**
- `<AiChat assistantId={id} />` — Chat widget with streaming + markdown
- `<AiAssistantConfig />` — Admin UI for assistant configuration
- `<AiKnowledgeBase />` — Document upload + processing status
- `<AiToolManager />` — Enable/disable AI tools
- `<AiUsageDashboard />` — Token usage, cost breakdown
- Dashboard slot: AI usage stats card

**Permissions:** Ai.Chat, Ai.ManageAssistants, Ai.ManageDocuments, Ai.ManageTools, Ai.ViewUsage
**Effort:** Large

---

#### Module: Workflow & Approvals

**Purpose:** Configurable multi-step workflows with approval chains, status machines, SLA tracking. The single highest-value addition for process-driven applications.

**Used By:** HR (leave/expense approval), E-Commerce (order processing), Education (enrollment), SaaS (support tickets), ERP (purchase orders)

**Depends On:** Comments & Activity (for approval comments)

**Capability Exposed:** `IWorkflowService` in `Starter.Abstractions/Capabilities/`

**Entities (own DbContext: `WorkflowDbContext`):**
- `WorkflowDefinition` — TenantId, Name, Description, EntityType, States (JSON), Transitions (JSON), IsActive, Version
- `WorkflowInstance` — DefinitionId, EntityType, EntityId, CurrentState, StartedAt, CompletedAt, StartedByUserId
- `WorkflowStep` — InstanceId, FromState, ToState, ActorUserId, Action (approve/reject/escalate/auto), Comment, Timestamp
- `ApprovalTask` — InstanceId, StepName, AssignedUserId or AssignedRoleId, DueDate, Status (pending/approved/rejected/escalated/delegated), CompletedAt
- `WorkflowTransitionLog` — InstanceId, all transition metadata for full audit

**Key Design:**
- **State Machine:** States and transitions stored as JSON in `WorkflowDefinition`. Transition rules include conditions (field values, role checks), actions (notify, webhook, update field).
- **Approval Chains:** Sequential (A then B then C) or parallel (A and B then C). Role-based or user-based assignment. Auto-escalation on timeout.
- **Task Inbox:** "My pending approvals" per user — a central view across all workflows.
- **SLA Tracking:** Per-step time limits, overdue detection via scheduled job, escalation triggers.
- **Hooks:** On-enter/on-exit per state: send notification (via Communication module if present), trigger webhook (via Webhooks module if present), update entity field, call AI summarization.
- **Integration:** Any entity becomes "workflowable" by implementing marker interface. `IWorkflowService.StartAsync(entityType, entityId, definitionId)` kicks off a workflow.

**Frontend Components:**
- Workflow designer — visual state/transition builder (admin)
- Task inbox — "My approvals" list with filters
- Approval dialog — approve/reject with comment
- Workflow history — timeline view per entity
- Dashboard slot: Pending approvals count

**Permissions:** Workflows.View, Workflows.Create, Workflows.Manage, Workflows.Approve
**Effort:** Large

---

#### Module: Multi-Channel Communication

**Purpose:** Unified messaging hub across all channels — email campaigns, SMS, WhatsApp, push, in-app. Every SaaS needs this beyond transactional emails.

**Used By:** All verticals — order confirmations, leave status, grade reports, appointment reminders, marketing campaigns

**Capability Exposed:** `IMessageDispatcher` in `Starter.Abstractions/Capabilities/`

**Entities (own DbContext: `CommunicationDbContext`):**
- `ChannelConfig` — TenantId, ChannelType (Email/SMS/Push/WhatsApp/InApp), Credentials (encrypted JSON), IsActive
- `MessageTemplate` — TenantId, ChannelType, Locale, Name, Subject, Body, Variables (JSON schema)
- `Campaign` — TenantId, Name, ChannelType, TemplateId, SegmentFilter (JSON), ScheduledAt, Status, SendStats (JSON)
- `CampaignRecipient` — CampaignId, UserId, Status (pending/sent/delivered/opened/clicked/bounced/failed), Timestamps
- `DeliveryLog` — TenantId, ChannelType, RecipientId, TemplateId, Status, SentAt, DeliveredAt, ErrorMessage

**Key Design:**
- **Channel Registry:** `IChannelProvider` interface — Email (SMTP/SendGrid/SES), SMS (Twilio), Push (FCM/APNs), WhatsApp (Twilio/Meta), InApp (Ably).
- **Template Engine:** Per-tenant, per-channel, per-locale templates with variable substitution. Visual HTML email builder.
- **Campaigns:** Bulk send with scheduling, recipient segmentation, delivery tracking.
- **Trigger Rules:** "When X happens, send Y via Z" — configurable automation tied to domain events.
- **Integration:** Workflow on-enter/on-exit actions can send messages. Scheduling reminders route through this module.

**Permissions:** Communication.View, Communication.Send, Communication.ManageTemplates, Communication.ManageCampaigns
**Effort:** Medium-Large

---

### Wave 2: Cross-Domain Utilities

Build after Wave 1 engines are functional.

#### Module: Scheduling & Calendar

**Purpose:** Events, recurring schedules, availability, booking, reminders. Every time-based business process needs this.

**Used By:** Education (timetables), HR (shifts, leave calendar), E-Commerce (delivery windows), Healthcare (appointments)

**Depends On:** Communication (for reminders)

**Entities (own DbContext: `SchedulingDbContext`):**
- `CalendarEvent` — TenantId, Title, Description, Location, StartUtc, EndUtc, RecurrenceRule (RRULE), OrganizerUserId, EntityType/EntityId (optional polymorphic link)
- `EventAttendee` — EventId, UserId, Status (invited/accepted/declined/tentative)
- `Availability` — TenantId, UserId or ResourceId, DayOfWeek, StartTime, EndTime, EffectiveFrom, EffectiveTo
- `Booking` — EventId, BookedByUserId, BookedForUserId, Status (confirmed/cancelled/no-show), Notes
- `Reminder` — EventId, ChannelType, MinutesBefore, SentAt

**Key Design:**
- RRULE-based recurrence with expansion to date lists
- Conflict detection against availability windows
- Timezone: stored UTC, displayed per user/tenant preference
- iCal export (.ics) and calendar feed URL
- Reminders dispatched via Communication module
- Frontend: Calendar component (day/week/month views), booking flow, availability editor

**Permissions:** Scheduling.View, Scheduling.Create, Scheduling.Manage, Scheduling.Book
**Effort:** Medium

---

#### Module: Reporting & Dashboards

**Purpose:** Tenant-configurable reports and dashboards. Every business needs custom KPIs and charts beyond the admin analytics.

**Used By:** All verticals — sales reports, grade distributions, attendance analytics, financial summaries

**Entities (own DbContext: `ReportingDbContext`):**
- `ReportDefinition` — TenantId, Name, DataSource, Fields (JSON), Filters (JSON), GroupBy, Aggregation, ChartType, CreatedByUserId
- `DashboardLayout` — TenantId, Name, IsDefault, Widgets (JSON — position, size, report ref)
- `DashboardWidget` — DashboardId, Type (kpi/chart/table/counter), ReportDefinitionId, Config (JSON)
- `ScheduledReport` — ReportDefinitionId, CronExpression, Recipients (JSON), Format, LastRunAt, NextRunAt

**Key Design:**
- Dynamic EF Core query builder translates report config to SQL, respecting tenant filters
- Data sources: pre-defined per entity type with field metadata. Modules register their own data sources via capability.
- Scheduled reports delivered via Communication module
- Extends existing export infrastructure for PDF/CSV
- Frontend: Report builder (field picker, filter builder, chart preview), dashboard layout editor (grid drag-and-drop)

**Permissions:** Reporting.View, Reporting.Create, Reporting.Manage, Reporting.Schedule
**Effort:** Medium-Large

---

#### Module: Payments & Invoicing

**Purpose:** Extends existing Billing module with invoice generation, payment processing, and financial tracking. Required for any transactional application.

**Used By:** E-Commerce (order payments), Education (tuition), HR (payroll disbursement), ERP (AP/AR)

**Depends On:** Billing (extends subscription billing with transactional payments)

**Entities (own DbContext: `PaymentsDbContext`):**
- `Invoice` — TenantId, Number (auto-generated), CustomerRef (entityType/entityId), LineItems (JSON), Subtotal, Tax, Total, Status, DueDate, PaidAt
- `InvoiceLine` — InvoiceId, Description, Quantity, UnitPrice, TaxRate, Total
- `PaymentTransaction` — TenantId, InvoiceId (optional), Amount, Provider (Stripe/PayPal/manual), ProviderRef, Status, ProcessedAt
- `TaxConfig` — TenantId, Name, Rate, AppliesTo (JSON)

**Key Design:**
- Invoice generation from any entity (orders, enrollments, subscriptions)
- PDF invoice generation (extends existing PDF export)
- Payment provider abstraction: Stripe, PayPal, manual entry
- Webhook receivers for payment provider callbacks
- Overdue invoice detection + reminders via Communication

**Permissions:** Payments.View, Payments.Create, Payments.Manage, Payments.Refund
**Effort:** Medium

---

#### Module: Advanced Search (Meilisearch)

**Purpose:** Full-text search with typo tolerance, faceted filtering, and instant results. Replaces basic EF LIKE queries.

**Used By:** All verticals — every app with significant data benefits from fast, typo-tolerant search

**Entities:** No own DbContext needed — Meilisearch is the data store. Module only has configuration.

**Key Design:**
- `ISearchService` abstraction in Abstractions with Meilisearch implementation
- Auto-indexing via domain event listeners: entity create/update/delete syncs to Meilisearch
- Modules register their searchable entity types and fields
- Global search bar — federated results across all entity types
- Per-entity enhanced search on list pages (feature-flag gated)
- Tenant isolation: separate indices per tenant or tenant-scoped filter keys
- Meilisearch added to Docker Compose

**Permissions:** Search.Use, Search.ManageIndices
**Effort:** Medium

---

### Wave 3: Domain Modules (Most Reusable First)

These serve specific verticals but many cross boundaries.

#### Module: Contacts & CRM

**Purpose:** Customer/client/contact management. The most universally needed domain module — every B2B and B2C app tracks people.

**Used By:** SaaS (client management), E-Commerce (customers), Education (parents/guardians), HR (candidates)

**Entities (own DbContext: `ContactsDbContext`):**
- `Contact` — TenantId, FirstName, LastName, Email, Phone, Company, Title, Tags (JSON), CustomFields (JSON), Source, Status
- `ContactNote` — ContactId, AuthorId, Content (uses Comments capability if available)
- `ContactActivity` — ContactId, Type (email/call/meeting/task), Description, Timestamp
- `ContactGroup` — TenantId, Name, Description, Filter (JSON — dynamic segment)

**Key Design:**
- Custom fields per tenant via JSON column
- Contact segmentation for Communication module campaigns
- Import/export via ImportExport module
- AI integration: contact enrichment, smart search
- Workflow: lead lifecycle (New → Qualified → Converted)

**Permissions:** Contacts.View, Contacts.Create, Contacts.Update, Contacts.Delete, Contacts.Import, Contacts.Export
**Effort:** Medium

---

#### Module: Employees & Org Structure

**Purpose:** Employee profiles, departments, positions, organizational hierarchy.

**Used By:** HR (primary), Education (staff management), ERP (workforce)

**Entities (own DbContext: `EmployeesDbContext`):**
- `Employee` — TenantId, UserId (links to core User), EmployeeNumber, Department, Position, ManagerId (self-ref), HireDate, Status, Salary (encrypted)
- `Department` — TenantId, Name, ParentDepartmentId (hierarchy), ManagerEmployeeId
- `Position` — TenantId, Title, Department, Level, SalaryRange

**Key Design:**
- Links to core User entity via `IUserReader`
- Org chart visualization (tree structure from manager relationships)
- Department hierarchy (nested departments)
- Workflow: onboarding checklist, status changes require approval

**Permissions:** Employees.View, Employees.Create, Employees.Update, Employees.Manage, Employees.ViewSalary
**Effort:** Medium

---

#### Module: Orders & Cart

**Purpose:** Shopping cart, order lifecycle, order management.

**Used By:** E-Commerce (primary), POS, ERP (purchase orders adapted)

**Depends On:** Products (items to order), Workflow (order status lifecycle)

**Entities (own DbContext: `OrdersDbContext`):**
- `Cart` — TenantId, UserId, Status (active/abandoned/converted), ExpiresAt
- `CartItem` — CartId, ProductId, VariantId, Quantity, UnitPrice
- `Order` — TenantId, OrderNumber, UserId, Status, ShippingAddress (JSON), BillingAddress (JSON), Subtotal, Tax, Shipping, Total, PlacedAt
- `OrderItem` — OrderId, ProductId, VariantId, Quantity, UnitPrice, Total
- `OrderStatusHistory` — OrderId, FromStatus, ToStatus, ChangedByUserId, Timestamp, Note

**Key Design:**
- Cart persistence with expiration
- Order lifecycle via Workflow module: Placed → Paid → Processing → Shipped → Delivered
- Communication triggers: order confirmation, shipping update, delivery notification
- Payments integration for checkout
- Returns/refunds sub-workflow
- Products module provides catalog data via reader

**Permissions:** Orders.View, Orders.Create, Orders.Manage, Orders.Refund
**Effort:** Medium-Large

---

#### Module: Leave & Attendance

**Purpose:** Leave requests, attendance tracking, shift management.

**Used By:** HR (primary), Education (student/teacher attendance)

**Depends On:** Employees (who's tracking), Scheduling (shifts), Workflow (leave approval)

**Entities (own DbContext: `AttendanceDbContext`):**
- `LeaveType` — TenantId, Name, DaysAllowed, CarryOverAllowed, RequiresApproval
- `LeaveRequest` — TenantId, EmployeeId, LeaveTypeId, StartDate, EndDate, Status, Reason
- `LeaveBalance` — EmployeeId, LeaveTypeId, Year, Entitled, Used, Remaining
- `AttendanceRecord` — TenantId, EmployeeId, Date, CheckIn, CheckOut, Status (present/absent/late/half-day), Source (manual/biometric/geo)
- `ShiftDefinition` — TenantId, Name, StartTime, EndTime, BreakMinutes

**Key Design:**
- Leave approval via Workflow module (employee → manager → HR)
- Calendar integration via Scheduling module (leave calendar, shift calendar)
- Notifications via Communication (leave status updates)
- Reporting: attendance percentage, leave balance reports
- Supports both employee (HR) and student (Education) attendance patterns

**Permissions:** Attendance.View, Attendance.Record, Attendance.ManageLeave, Attendance.ApproveLeave
**Effort:** Medium

---

#### Module: Inventory & Warehousing

**Purpose:** Stock tracking, warehouse management, inventory transactions.

**Used By:** ERP (primary), E-Commerce (stock levels), POS (store inventory)

**Depends On:** Products (what's being tracked)

**Entities (own DbContext: `InventoryDbContext`):**
- `Warehouse` — TenantId, Name, Location, IsDefault
- `InventoryItem` — WarehouseId, ProductId, Quantity, ReorderLevel, ReorderQuantity
- `InventoryTransaction` — ItemId, Type (receive/ship/adjust/transfer), Quantity, Reference (PO/Order ID), Timestamp, UserId
- `StockTransfer` — FromWarehouseId, ToWarehouseId, Status, Items (JSON), RequestedByUserId

**Key Design:**
- Real-time stock level tracking with transaction history
- Low-stock alerts via Communication module
- Stock transfer workflow between warehouses
- Integration with Orders (auto-deduct on ship, auto-add on receive)
- Reporting: inventory valuation, stock movement, reorder suggestions

**Permissions:** Inventory.View, Inventory.Manage, Inventory.Transfer, Inventory.Adjust
**Effort:** Medium

---

### Wave 4: Specialized Domain Modules

#### Module: Students & Enrollment

**Purpose:** Student management, enrollment processes, guardian relationships.

**Used By:** Education (primary), Training platforms

**Depends On:** Workflow (enrollment approval), Communication (notifications to parents)

**Entities (own DbContext: `StudentsDbContext`):**
- `Student` — TenantId, UserId, StudentNumber, Grade/Level, Section, EnrollmentDate, Status, GuardianIds
- `Guardian` — TenantId, UserId, Relationship, Students (many-to-many)
- `Enrollment` — StudentId, AcademicYear, Term, Status (applied/approved/enrolled/withdrawn), AppliedAt
- `AcademicYear` — TenantId, Name, StartDate, EndDate, IsCurrent
- `Term` — AcademicYearId, Name, StartDate, EndDate

**Key Design:**
- Enrollment workflow: Apply → Review → Approve → Enrolled
- Guardian portal access (separate role with limited permissions)
- Academic year/term structure for temporal data
- Communication: enrollment status, report cards, absence alerts
- Scheduling: class timetable per section

**Permissions:** Students.View, Students.Create, Students.Manage, Students.Enroll, Students.ViewGuardian
**Effort:** Medium

---

#### Module: Courses & Grading

**Purpose:** Course management, assignments, grading, gradebook.

**Used By:** Education (primary), Corporate training/LMS

**Depends On:** Students (who's enrolled), Scheduling (timetable), AI (tutoring, auto-grading suggestions)

**Entities (own DbContext: `CoursesDbContext`):**
- `Course` — TenantId, Name, Code, Description, Credits, Department, TeacherUserId
- `Section` — CourseId, Term, Schedule (JSON — linked to Scheduling), MaxCapacity, CurrentEnrollment
- `Assignment` — SectionId, Title, Description, DueDate, MaxPoints, Type (homework/quiz/exam/project)
- `Grade` — AssignmentId, StudentId, Points, Feedback, GradedByUserId, GradedAt
- `GradebookEntry` — SectionId, StudentId, FinalGrade, GPA, Status (in-progress/passed/failed)

**Key Design:**
- Gradebook with weighted categories
- Assignment submission + grading workflow
- AI: tutoring assistant per course, auto-grading suggestions for MCQ
- Scheduling: section timetable linked to Calendar events
- Communication: assignment due reminders, grade notifications
- Reporting: grade distribution, class performance, student transcript

**Permissions:** Courses.View, Courses.Create, Courses.Manage, Courses.Grade, Courses.ViewGradebook
**Effort:** Medium

---

#### Module: Payroll

**Purpose:** Salary calculation, pay periods, pay slip generation.

**Used By:** HR (primary)

**Depends On:** Employees (salary data), Attendance (hours worked), Payments (disbursement), Communication (pay slips)

**Entities (own DbContext: `PayrollDbContext`):**
- `PayrollPeriod` — TenantId, Month, Year, Status (draft/processing/finalized/paid), ProcessedAt
- `PaySlip` — PayrollPeriodId, EmployeeId, BasicSalary, Components (JSON — allowances, deductions), GrossPay, NetPay, GeneratedAt
- `SalaryComponent` — TenantId, Name, Type (earning/deduction), CalculationType (fixed/percentage), Value, AppliesTo (JSON)
- `PayrollAdjustment` — PaySlipId, Reason, Amount, Type (bonus/deduction/correction), ApprovedByUserId

**Key Design:**
- Monthly payroll cycle: Draft → Calculate → Review → Approve → Disburse
- Configurable salary components per tenant
- Integration with Attendance for hours/overtime
- Pay slip PDF generation + email delivery via Communication
- Approval workflow for payroll finalization
- Reporting: payroll summary, tax reports, year-end statements

**Permissions:** Payroll.View, Payroll.Process, Payroll.Approve, Payroll.ViewPayslips
**Effort:** Large

---

#### Module: POS (Point of Sale)

**Purpose:** Retail checkout, receipt generation, daily reconciliation.

**Used By:** Retail businesses, restaurants

**Depends On:** Products (catalog), Payments (transactions), Inventory (stock deduction)

**Entities (own DbContext: `PosDbContext`):**
- `PosTerminal` — TenantId, Name, Location, Status, AssignedUserId
- `PosTransaction` — TerminalId, Items (JSON), Subtotal, Tax, Discount, Total, PaymentMethod, Timestamp
- `DailyReconciliation` — TerminalId, Date, ExpectedTotal, ActualTotal, Variance, Status, ClosedByUserId

**Key Design:**
- Lightweight checkout flow optimized for speed
- Barcode/SKU lookup against Products
- Receipt generation (thermal printer format + PDF)
- Daily cash reconciliation workflow
- Inventory auto-deduction on sale
- Reporting: daily sales, top products, hourly trends

**Permissions:** Pos.Sell, Pos.Void, Pos.Reconcile, Pos.Manage
**Effort:** Medium

---

### Wave 5: Infrastructure (Parallel with Any Wave)

#### CI/CD Pipelines

**Scope:**
- GitHub Actions: build-and-test.yml, deploy-staging.yml, deploy-production.yml, database-migration.yml
- Docker Compose (production): SSL termination via Traefik/Caddy, health checks, restart policies
- Kubernetes: Helm chart with HPA, ingress, resource limits
- Cloud templates: Azure (App Service + SQL + Blob), AWS (ECS + RDS + S3)
- Environment management: .env.example files, secrets guide

**Effort:** Small

---

#### Enhanced Ably Integration

**Scope:**
- Presence: show who's viewing the same entity
- Typing indicators for comment composition
- Push notifications for mobile
- Channel naming convention: `tenant:{id}:entity:{type}:{id}`
- Connection management: reconnection, offline queue, state indicator

**Effort:** Small

---

## Docker Compose Additions

| Service | Port | Added By |
|---------|------|----------|
| Qdrant | 6333, 6334 | AI Integration module |
| Meilisearch | 7700 | Advanced Search module |

All existing services remain: PostgreSQL, Redis, RabbitMQ, Mailpit, MinIO, Jaeger, Prometheus.

---

## Preset Combinations (CLI)

```bash
# Interactive (shows all modules as checkboxes):
starter new MyApp

# Preset-based (pre-selects recommended modules):
starter new MyApp --preset saas
starter new MyApp --preset ecommerce
starter new MyApp --preset education
starter new MyApp --preset hr
starter new MyApp --preset erp

# Manual selection:
starter new MyApp --modules Workflow,AI,Scheduling,Communication

# Core only:
starter new MyApp --modules None
```

### Preset Definitions

| Preset | Modules Included |
|--------|-----------------|
| **saas** | Core + Comments + Workflow + AI + Communication + Reporting + Search + Contacts |
| **ecommerce** | Core + Products + Orders + Payments + Inventory + Communication + Search + Reporting + Workflow |
| **education** | Core + Students + Courses + Scheduling + Communication + AI + Reporting + Attendance + Workflow + Comments |
| **hr** | Core + Employees + Leave + Attendance + Scheduling + Communication + Workflow + Payroll + Reporting |
| **erp** | Core + Products + Inventory + Orders + Payments + Employees + Workflow + Reporting + Communication |
| **full** | All modules |

---

## Module Dependency Graph

```
Independent (no module dependencies):
  Comments & Activity
  AI Integration
  Multi-Channel Communication
  Reporting & Dashboards
  Advanced Search (Meilisearch)
  Contacts & CRM
  Enhanced Ably

Depends on 1 module:
  Workflow & Approvals → Comments (approval comments)
  Scheduling & Calendar → Communication (reminders)
  Payments & Invoicing → Billing (extends subscription billing)
  Employees & Org → (none, but enriches with Workflow)
  Inventory & Warehousing → Products (what to track)

Depends on 2+ modules:
  Orders & Cart → Products + Workflow
  Leave & Attendance → Employees + Scheduling + Workflow
  Students & Enrollment → Workflow + Communication
  Courses & Grading → Students + Scheduling + AI
  Payroll → Employees + Attendance + Payments + Communication
  POS → Products + Payments + Inventory
```

Modules gracefully degrade when dependencies are absent (Null Object pattern). For example:
- Workflow without Communication: workflows work, but no email notifications on transitions
- Orders without Workflow: orders work with simple status field, no configurable approval chains
- Courses without AI: courses work, no tutoring assistant or auto-grading

---

## Use-Case Coverage Matrix

| Capability | SaaS | E-Commerce | Education | HR | ERP | POS |
|-----------|------|-----------|-----------|-----|-----|-----|
| Auth & Roles | Core | Core | Core | Core | Core | Core |
| Multi-tenancy | Core | Core | Core | Core | Core | Core |
| Billing | Existing | Existing | Existing | Existing | Existing | Existing |
| Webhooks | Existing | Existing | Existing | Existing | Existing | — |
| ImportExport | Existing | Existing | Existing | Existing | Existing | — |
| Products | — | Existing | — | — | Existing | Existing |
| **Comments** | W1 | W1 | W1 | W1 | W1 | — |
| **AI** | W1 | W1 | W1 | — | — | — |
| **Workflow** | W1 | W1 | W1 | W1 | W1 | — |
| **Communication** | W1 | W1 | W1 | W1 | W1 | — |
| **Scheduling** | — | W2 | W2 | W2 | — | — |
| **Reporting** | W2 | W2 | W2 | W2 | W2 | W2 |
| **Payments** | — | W2 | W2 | W2 | W2 | W2 |
| **Search** | W2 | W2 | W2 | — | W2 | — |
| **Contacts** | W3 | W3 | W3 | W3 | — | — |
| **Employees** | — | — | W3 | W3 | W3 | — |
| **Orders** | — | W3 | — | — | W3 | — |
| **Attendance** | — | — | W3 | W3 | — | — |
| **Inventory** | — | W3 | — | — | W3 | W3 |
| **Students** | — | — | W4 | — | — | — |
| **Courses** | — | — | W4 | — | — | — |
| **Payroll** | — | — | — | W4 | — | — |
| **POS** | — | — | — | — | — | W4 |

**W1 = Wave 1, W2 = Wave 2, W3 = Wave 3, W4 = Wave 4**

After Wave 2 completes: ~80% coverage across all verticals.
After Wave 3: ~90%+ with domain entities.
After Wave 4: ~95%+ with specialized modules.

---

## Pricing Model (Suggested)

| Tier | What's Included | Target Buyer |
|------|----------------|-------------|
| **Core** (free/low price) | Auth, Users, Roles, Tenants, Settings, Dashboard | Open-source community, attract developers |
| **Professional** | Core + Billing + Webhooks + ImportExport + Products + Comments + Workflow | Solo devs, small agencies |
| **Enterprise** | Professional + All Wave 1-2 modules (AI, Communication, Scheduling, Reporting, Payments, Search) | Mid-size teams, startups |
| **Domain Modules** | Individual add-ons: Contacts, Employees, Orders, Attendance, Inventory, Students, Courses, Payroll, POS | Sold separately per domain |
| **Presets** | Bundled discount: SaaS, E-Commerce, Education, HR, ERP preset packages | Vertical-specific buyers |

---

## Next Steps

1. **Continue module architecture implementation** — Complete Phase 2-4 from the module architecture spec (extract remaining existing features as modules)
2. **Wave 1 implementation** — Spec and build Comments & Activity + AI Integration in parallel, then Workflow, then Communication
3. **CLI enhancement** — Add `--preset` support and module dependency resolution to the CLI tool
4. **Wave 2-4** — Build domain modules following the validated module pattern
