# System Design

**Audience:** Anyone working on this boilerplate. New to the codebase? Start here, then read [module-development-guide.md](./module-development-guide.md) when you need to add or extend a feature.

This document is the map of the codebase: which projects exist, what each one is for, what it may and may not depend on, and how the major patterns (CQRS, capabilities, slots, outbox events) flow through them.

> **History:** the system was reshaped by the true-modularity refactor in early 2026 (Phases 0–4 + B1c + D1). The pre-refactor state had 9 features pretending to be modules and zero actual removability. If you find a doc, comment, or commit message referencing the old state, it predates this rewrite. The spec lives at [`docs/superpowers/specs/2026-04-07-true-modularity-refactor.md`](../superpowers/specs/2026-04-07-true-modularity-refactor.md).

---

## Table of Contents

- [1. Solution overview](#1-solution-overview)
- [2. Backend project graph](#2-backend-project-graph)
- [3. Backend folder layout](#3-backend-folder-layout)
- [4. Frontend folder layout](#4-frontend-folder-layout)
- [5. Where things live — quick reference](#5-where-things-live--quick-reference)
- [6. Key patterns](#6-key-patterns)
- [7. Request lifecycle (HTTP → DB)](#7-request-lifecycle-http--db)
- [8. Event lifecycle (cross-module side effects)](#8-event-lifecycle-cross-module-side-effects)
- [9. Module loading at startup](#9-module-loading-at-startup)
- [10. Architecture rules and how they're enforced](#10-architecture-rules-and-how-theyre-enforced)

---

## 1. Solution overview

The repository is a full-stack starter kit:

```
Boilerplate/
├── boilerplateBE/         .NET 10 backend (Clean Architecture + CQRS + EF Core)
├── boilerplateFE/         React 19 + TypeScript + Vite + Tailwind frontend
├── docs/                  Architecture, specs, plans
└── scripts/               rename.ps1 (project scaffolder), modules.json
```

The backend follows a layered Clean Architecture with **6 always-present core features** and **3 truly optional modules** that can be excluded at scaffold time. Modules can be added/removed without touching core code — that's the architectural goal the refactor delivered. The 3 current optional modules (Billing, Webhooks, ImportExport) live under `boilerplateBE/src/modules/`. Domain modules a downstream developer adds (e-commerce, POS, CRM, etc.) follow the same pattern.

The frontend is a feature-folder React app where the 3 optional modules each have a feature folder under `boilerplateFE/src/features/{billing,webhooks,import-export}/` plus an `index.ts` that registers their UI contributions into a typed slot registry. Core pages render `<Slot id="..." />` and have zero direct imports from any module folder.

---

## 2. Backend project graph

```
┌─────────────────────────────────────────────────────────────────────┐
│                          Starter.Api                                │
│  Controllers, Program.cs, middleware, DI composition root           │
└─────────────────────────┬───────────────────────────────────────────┘
                          │
        ┌─────────────────┼──────────────────┬────────────────────┐
        ▼                 ▼                  ▼                    ▼
┌──────────────┐  ┌──────────────┐   ┌──────────────────┐  ┌─────────────┐
│Infrastructure│  │   modules/   │   │ Infrastructure   │  │Abstractions │
│              │  │   {3 .csproj}│   │ .Identity        │  │    .Web     │
│  EF Core,    │  │  Billing     │   │ JWT, password,   │  │ BaseApi-    │
│  MassTransit,│  │  Webhooks    │   │ TOTP, refresh    │  │ Controller  │
│  S3, Email,  │  │  ImportExport│   │ token rotation   │  │             │
│  Redis,      │  │              │   └──────────────────┘  └──────┬──────┘
│  Null Objects│  │  Each module │                                │
│  Readers     │  │  has its own │                                │
└──────┬───────┘  │  DbContext   │                                │
       │          └──────┬───────┘                                │
       │                 │                                        │
       │                 └────────────────┐                       │
       ▼                                  ▼                       ▼
┌──────────────────────────────────────────────────────────────────────┐
│                       Starter.Application                            │
│  CQRS handlers, MediatR pipeline behaviors, Common/Interfaces        │
│  (IFileService, INotificationService, IFeatureFlagService, ...)      │
│  Common/Events (TenantRegisteredEvent, UserRegisteredEvent, ...)     │
└──────────────────────────────┬───────────────────────────────────────┘
                               │
       ┌───────────────────────┼─────────────────────┐
       ▼                       ▼                     ▼
┌──────────────┐      ┌────────────────┐    ┌─────────────────┐
│Starter.Domain│      │Starter.        │    │ Starter.Shared  │
│              │      │Abstractions    │    │                 │
│ Core         │      │                │    │ Result<T>,      │
│ entities     │      │ ICapability,   │    │ ApiResponse,    │
│ (User, Role, │      │ IModule,       │    │ Permissions,    │
│ Tenant,      │      │ Readers,       │    │ Roles, Errors   │
│ FileMetadata │      │ Capability     │    │                 │
│ ApiKey, ...) │      │ contracts      │    │                 │
│ ZERO module  │      │ ZERO project   │    │                 │
│ types        │      │ references     │    │                 │
└──────────────┘      └────────────────┘    └─────────────────┘
```

Note: `Starter.Abstractions` has ZERO project references (as of the module type relocation refactor). Contract-adjacent value types like `BillingInterval`, `FieldType`, `FieldDefinition`, and `EntityImportExportDefinition` live directly in `Starter.Abstractions.Capabilities`. Module-owned entities live inside each module's own project under `Domain/`. `Starter.Domain/` contains only core entities that every build of the boilerplate ships with.

### Project responsibilities

| Project | Purpose | May reference | Real packages |
|---|---|---|---|
| **Starter.Domain** | Pure domain — entities, value objects, enums, domain events, errors. Zero infrastructure concerns. | _nothing_ | _none_ |
| **Starter.Shared** | Cross-cutting primitives that don't belong in Domain: `Result<T>`, error types, permission constants, API response wrappers. | _nothing_ | _none_ |
| **Starter.Abstractions** | Pure contracts: `IModule`, `IModuleDbContext`, capability interfaces (`IBillingProvider`, `IWebhookPublisher`, `IImportExportRegistry`, `IQuotaChecker`, `IUsageMetricCalculator`), `ICapability` marker, reader services (`ITenantReader`, `IUserReader`, `IRoleReader`), reader DTOs (`TenantSummary`, etc.), contract-adjacent value types (`BillingInterval`, `FieldType`, `FieldDefinition`, `EntityImportExportDefinition`). | **zero project references** | `Microsoft.Extensions.Configuration.Abstractions`, `Microsoft.Extensions.DependencyInjection.Abstractions` |
| **Starter.Abstractions.Web** | Web-layer helpers shared by core and module controllers — `BaseApiController`. Separated from `Starter.Abstractions` so the pure contracts project stays free of ASP.NET. | `Starter.Abstractions`, `Starter.Application` | `Asp.Versioning.Mvc`, `Microsoft.AspNetCore.App` (FrameworkReference) |
| **Starter.Application** | CQRS handlers (commands, queries, validators, DTOs, mappers) for core features. Defines core-provided infrastructure contracts in `Common/Interfaces/` (`IFileService`, `INotificationService`, etc.). Defines cross-module domain events in `Common/Events/`. MediatR pipeline behaviors. | `Starter.Domain`, `Starter.Shared` | `MediatR`, `MassTransit`, `FluentValidation`, `Riok.Mapperly`, `Microsoft.EntityFrameworkCore` (interfaces only) |
| **Starter.Infrastructure** | Implementations of everything Application defines: `ApplicationDbContext`, EF migrations + interceptors, S3 file service, email/SMS, Redis cache, JWT, MassTransit transactional outbox, reader services, Null Object capability fallbacks. The composition root for the data layer. | `Starter.Application`, `Starter.Abstractions` | `Npgsql.EntityFrameworkCore.PostgreSQL`, `MassTransit.RabbitMQ`, `MassTransit.EntityFrameworkCore`, `AWSSDK.S3`, `StackExchange.Redis`, `BCrypt.Net-Next`, `QuestPDF` |
| **Starter.Infrastructure.Identity** | Authentication and authorization: JWT issuance/validation, password hashing, TOTP/2FA, refresh token rotation, permission policy provider. Separated so it can be replaced wholesale (e.g. with an external identity provider). | `Starter.Application`, `Starter.Domain`, `Starter.Shared` | `Microsoft.AspNetCore.Authentication.JwtBearer`, `System.IdentityModel.Tokens.Jwt`, `BCrypt.Net-Next` |
| **Starter.Api** | The web host. Controllers for all core features, `Program.cs` (DI composition + middleware pipeline + module discovery), Swagger, rate limiting, OpenTelemetry, Serilog, exception middleware. | All projects above + the 3 optional modules | `Asp.Versioning.Mvc`, `Swashbuckle`, `AspNetCoreRateLimit`, `Serilog.AspNetCore`, `OpenTelemetry.*` |
| **Starter.Module.{Billing,Webhooks,ImportExport}** | Optional modules. Each has its own `DbContext`, controllers, handlers, EF configurations, services, permissions. Each registers itself via `IModule`. | `Starter.Abstractions.Web` (which transitively pulls Abstractions, Application, Domain, Shared) | Module-specific (e.g. Billing: `MassTransit.EntityFrameworkCore`, `Npgsql.EntityFrameworkCore.PostgreSQL`) |
| **tests/Starter.Api.Tests** | Integration + architecture tests. Currently contains `AbstractionsPurityTests` which enforces the dependency rules for `Starter.Abstractions` via reflection. | `Starter.Api` | `xUnit`, `Moq`, `FluentAssertions`, `Microsoft.EntityFrameworkCore.InMemory` |

### What "Abstractions" means in this codebase

Two projects use the word, with deliberately different scopes:

- **`Starter.Abstractions`** — pure contracts. **Zero project references.** Only two `Microsoft.Extensions.*.Abstractions` NuGet packages. All types used in contract signatures — including former domain enums like `BillingInterval` and `FieldType` — live inside this project itself (under `Capabilities/`). This is the project modules implement against. When a new capability contract needs a value type, define it here rather than adding a project reference.
- **`Starter.Abstractions.Web`** — adds the ASP.NET-flavoured `BaseApiController` and pulls in `Starter.Application` (for `Result<T>`, `PaginatedList<T>`, `PagedResponse<T>`). Modules whose controllers inherit from `BaseApiController` reference this project; through the transitive graph they get everything they need without `Starter.Application` or `Starter.Infrastructure` showing up as a direct dependency.

The split is enforced by `tests/Starter.Api.Tests/Architecture/AbstractionsPurityTests.cs`. Adding any project reference to `Starter.Abstractions.csproj` will fail CI.

---

## 3. Backend folder layout

```
boilerplateBE/
├── Directory.Packages.props        Central package versions (CPM)
├── Starter.sln
├── docker-compose.yml              PostgreSQL, Redis, RabbitMQ, Mailpit, MinIO, Jaeger, Prometheus
└── src/
    ├── Starter.Domain/             ← core entities ONLY — zero module-owned types
    │   ├── Common/                 Cross-cutting: AuditLog, FileMetadata, Notification, ReportRequest, ITenantEntity, IAuditableEntity, ...
    │   ├── Identity/               User, Role, Permission, Session, Invitation, value objects (Email, FullName, ...)
    │   ├── Tenants/                Tenant, TenantStatus
    │   ├── ApiKeys/                ApiKey
    │   ├── FeatureFlags/           FeatureFlag, TenantFeatureFlag
    │   ├── Exceptions/             DomainException, BusinessRuleException
    │   └── Primitives/             AggregateRoot<TId>, Entity<TId>, Enumeration<T>, ValueObject
    │
    ├── Starter.Shared/
    │   ├── Constants/              Permissions.cs, Roles.cs (centralized for the seeder)
    │   ├── Models/                 ApiResponse, PagedApiResponse, PagedResponse
    │   └── Results/                Result, Result<T>, Error, ErrorType
    │
    ├── Starter.Abstractions/       ← ZERO project references; pure contracts
    │   ├── Capabilities/           ICapability, IBillingProvider, IWebhookPublisher, IImportExportRegistry, IQuotaChecker, IUsageMetricCalculator, CapabilityNotInstalledException
    │   │                           + contract-adjacent value types: BillingInterval, FieldType, FieldDefinition, EntityImportExportDefinition
    │   ├── Modularity/             IModule, IModuleDbContext, ModuleLoader
    │   └── Readers/                ITenantReader, IUserReader, IRoleReader (+ Summary records)
    │
    ├── Starter.Abstractions.Web/
    │   └── BaseApiController.cs    Mediator-aware base controller used by core + module controllers
    │
    ├── Starter.Application/
    │   ├── Common/
    │   │   ├── Behaviors/          MediatR pipeline: Validation, Logging, Performance, Tracing
    │   │   ├── Constants/          Application-layer constants (e.g. ValidationConstants)
    │   │   ├── Events/             Cross-module domain events (TenantRegisteredEvent, UserRegisteredEvent, RoleCreatedEvent, FileUploadedEvent, IDomainEvent)
    │   │   ├── Interfaces/         Core-provided contracts: IApplicationDbContext, ICurrentUserService, IFileService, INotificationService, IFeatureFlagService, IUsageTracker, ICacheService, IEmailService, ISmsService, IOtpService, IExportService, ISettingsService, ...
    │   │   ├── Models/             PaginatedList, dashboard models
    │   │   └── Messages/           MassTransit message types (e.g. GenerateReportMessage)
    │   └── Features/               One folder per core feature
    │       ├── Auth/
    │       ├── Users/
    │       ├── Roles/
    │       ├── Tenants/
    │       ├── Files/
    │       ├── Notifications/
    │       ├── FeatureFlags/
    │       ├── ApiKeys/
    │       ├── AuditLogs/
    │       ├── Reports/
    │       └── ...                 Each folder has Commands/, Queries/, DTOs/, EventHandlers/
    │
    ├── Starter.Infrastructure/
    │   ├── Capabilities/
    │   │   ├── MetricCalculators/  UsersMetricCalculator, ApiKeysMetricCalculator, StorageBytesMetricCalculator, ReportsActiveMetricCalculator
    │   │   └── NullObjects/        NullBillingProvider, NullWebhookPublisher, NullImportExportRegistry, NullQuotaChecker
    │   ├── Consumers/              MassTransit consumers (e.g. GenerateReportConsumer)
    │   ├── Email/                  EmailService, EmailTemplateService, SMTP wiring
    │   ├── Persistence/
    │   │   ├── ApplicationDbContext.cs  ← the only outbox-aware DbContext
    │   │   ├── Configurations/     IEntityTypeConfiguration<T> for every core entity
    │   │   ├── Interceptors/       AuditableEntityInterceptor, DomainEventDispatcherInterceptor
    │   │   ├── Migrations/         (empty in the boilerplate; populated by `dotnet ef migrations add` per-project)
    │   │   └── Seeds/              DataSeeder.cs (orchestrates core + module migrate + seed)
    │   ├── Readers/                TenantReader, UserReader, RoleReader (real implementations)
    │   ├── Services/               FileService (S3), CacheService (Redis), DateTimeService, FeatureFlagService, NotificationService, PermissionHierarchyService, UsageTrackerService, ExportService, OtpService, AblyRealtimeService, ...
    │   ├── Settings/               Strongly typed settings: SmtpSettings, StorageSettings, TwilioSettings, ...
    │   └── DependencyInjection.cs  AddInfrastructure: persistence + caching + messaging + capabilities + email + sms + realtime + storage + export + healthchecks
    │
    ├── Starter.Infrastructure.Identity/
    │   ├── Authentication/         JWT bearer setup, refresh-token handler
    │   ├── Authorization/          Permission policy provider, ApiKey auth handler
    │   ├── Models/                 Token result types
    │   └── Services/               PasswordService (BCrypt), TokenService, TotpService
    │
    ├── Starter.Api/
    │   ├── Controllers/            One controller per core feature + a shim BaseApiController
    │   ├── Configurations/         Swagger, CORS, ApiVersioning, RateLimiting, OpenTelemetry setup extensions
    │   ├── Middleware/             ExceptionHandlingMiddleware, RequestLoggingMiddleware, TenantResolutionMiddleware, ...
    │   └── Program.cs              Module discovery → AddApplication → AddInfrastructure → AddIdentity → module ConfigureServices → middleware pipeline → DataSeeder.SeedAsync → app.Run
    │
    ├── modules/
    │   ├── Starter.Module.Billing/
    │   │   ├── Application/        Commands/Queries/EventHandlers/DTOs (uses IApplicationDbContext + BillingDbContext via Rule B)
    │   │   ├── Constants/          BillingPermissions
    │   │   ├── Controllers/        BillingController
    │   │   ├── Domain/             ← module-owned entities, enums, errors, internal events
    │   │   │   ├── Entities/       SubscriptionPlan, TenantSubscription, PaymentRecord, PlanPriceHistory
    │   │   │   ├── Enums/          SubscriptionStatus, PaymentStatus (BillingInterval is in Abstractions)
    │   │   │   ├── Errors/         BillingErrors
    │   │   │   └── Events/         SubscriptionChangedEvent, SubscriptionCanceledEvent (intra-module only)
    │   │   ├── Infrastructure/
    │   │   │   ├── Configurations/ EF configs for the billing entities
    │   │   │   ├── Persistence/    BillingDbContext (own __EFMigrationsHistory_Billing)
    │   │   │   └── Services/       MockBillingProvider (real IBillingProvider implementation)
    │   │   └── BillingModule.cs    : IModule
    │   ├── Starter.Module.Webhooks/
    │   │   ├── Application/, Constants/, Controllers/, Infrastructure/, WebhooksModule.cs
    │   │   └── Domain/             Entities (WebhookEndpoint, WebhookDelivery), Enums (WebhookDeliveryStatus), Errors (WebhookErrors)
    │   └── Starter.Module.ImportExport/
    │       ├── Application/, Constants/, Controllers/, Infrastructure/, ImportExportModule.cs
    │       │   └── Application/Abstractions/  IImportRowProcessor (module-internal contract)
    │       └── Domain/             Entities (ImportJob), Enums (ConflictMode, ImportJobStatus, ImportRowStatus — FieldType is in Abstractions), Errors, ImportRowResult
    │
    └── tests/
        └── Starter.Api.Tests/
            ├── Architecture/
            │   └── AbstractionsPurityTests.cs   ← reflection-based dependency rules
            └── Starter.Api.Tests.csproj
```

---

## 4. Frontend folder layout

```
boilerplateFE/
├── eslint.config.js                Flat config; no-restricted-imports rule blocks core→module imports
├── tailwind.config.js              Theme tokens via CSS variables
├── vite.config.ts
└── src/
    ├── app/
    │   ├── main.tsx                Calls registerAllModules() before ReactDOM.createRoot
    │   └── providers/              QueryClient, ThemeProvider, AuthProvider, RouterProvider
    │
    ├── components/
    │   ├── common/                 Shared: Pagination, PageHeader, EmptyState, ConfirmDialog, ExportButton, FileUpload, UserAvatar, ...
    │   ├── guards/                 PermissionGuard, AuthGuard
    │   ├── layout/
    │   │   ├── AuthLayout/         Login/register/etc shell
    │   │   ├── PublicLayout/       Landing/pricing shell
    │   │   └── MainLayout/         Authenticated shell + Sidebar.tsx
    │   └── ui/                     shadcn/ui primitives (button, card, dialog, table, ...)
    │
    ├── config/
    │   ├── api.config.ts           API_ENDPOINTS table
    │   ├── theme.config.ts         Active theme preset
    │   ├── routes.config.ts        Route path constants
    │   └── modules.config.ts       activeModules flags + enabledModules array + registerAllModules() bootstrap
    │
    ├── constants/                  Permissions table, status badge variants, audit action labels
    │
    ├── features/                   ONE FOLDER PER FEATURE
    │   ├── auth/                   Login, Register, RegisterTenant, ForgotPassword, VerifyEmail, AcceptInvite
    │   ├── dashboard/
    │   ├── users/
    │   ├── roles/
    │   ├── tenants/
    │   ├── files/                  Core (always present)
    │   ├── notifications/          Core
    │   ├── feature-flags/          Core
    │   ├── api-keys/               Core
    │   ├── audit-logs/             Core
    │   ├── reports/                Core
    │   ├── settings/
    │   ├── profile/
    │   ├── landing/
    │   ├── billing/                ← optional MODULE
    │   │   ├── api/, components/, constants/, pages/, utils/
    │   │   └── index.ts            Exports billingModule with register() — registers TenantSubscriptionTab into 'tenant-detail-tabs' slot
    │   ├── webhooks/               ← optional MODULE
    │   │   └── index.ts            Exports webhooksModule (no slot contributions yet)
    │   └── import-export/          ← optional MODULE
    │       └── index.ts            Exports importExportModule — registers UsersImportButton into 'users-list-toolbar' slot
    │
    ├── hooks/                      usePermissions, useBackNavigation, useThemePreset, useTenantBranding, ...
    │
    ├── i18n/
    │   ├── index.ts
    │   └── locales/{en,ar,ku}/translation.json
    │
    ├── lib/
    │   ├── axios/                  Client + auth/refresh/error interceptors
    │   ├── extensions/             ← THE SLOT REGISTRY
    │   │   ├── slot-map.ts         Typed SlotMap interface — one entry per slot id with its props shape
    │   │   ├── slots.ts            registerSlot, getSlotEntries, hasSlotEntries
    │   │   ├── Slot.tsx            <Slot id="..." props={...}/> component with permission gating + Suspense
    │   │   ├── capabilities.ts     registerCapability, getCapability (parallel registry for hook/service capabilities)
    │   │   └── index.ts            Barrel export
    │   ├── query/                  TanStack Query client + queryKeys
    │   ├── utils.ts                cn() etc.
    │   └── validation/             Zod schemas
    │
    ├── routes/
    │   ├── routes.tsx              All app routes; module pages use `activeModules.X ? lazy(...) : NullPage` pattern
    │   ├── NotFoundPage.tsx        Stub used as the "absent module page" target
    │   └── index.tsx               Route tree composition
    │
    ├── stores/                     Zustand stores: useAuthStore, useThemeStore, ...
    │
    ├── types/                      Cross-feature TypeScript types
    │
    └── utils/                      format, validation helpers
```

---

## 5. Where things live — quick reference

### Backend

| If you need to... | Look in |
|---|---|
| Define a new core entity | `src/Starter.Domain/{Feature}/Entities/` |
| Define a domain event published by core | `src/Starter.Application/Common/Events/` |
| Define a contract a module will implement | `src/Starter.Abstractions/Capabilities/` |
| Define a contract core implements (e.g. another file service) | `src/Starter.Application/Common/Interfaces/` |
| Implement a core service (Redis cache, S3, SMTP) | `src/Starter.Infrastructure/Services/` |
| Add a Null Object fallback for a capability | `src/Starter.Infrastructure/Capabilities/NullObjects/` |
| Add a CQRS handler for a core feature | `src/Starter.Application/Features/{Feature}/Commands\|Queries/` |
| Add an EF entity configuration | `src/Starter.Infrastructure/Persistence/Configurations/` |
| Add seed data | `src/Starter.Infrastructure/Persistence/Seeds/DataSeeder.cs` |
| Add a controller for a core feature | `src/Starter.Api/Controllers/` |
| Add a controller for a module | `src/modules/Starter.Module.{Name}/Controllers/` |
| Add a permission constant | `src/Starter.Shared/Constants/Permissions.cs` (core) or `src/modules/Starter.Module.{Name}/Constants/{Name}Permissions.cs` (module) |
| Add a default role-permission mapping | `src/Starter.Shared/Constants/Roles.cs` (core) or the module's `GetDefaultRolePermissions()` |
| Add an architecture rule test | `tests/Starter.Api.Tests/Architecture/` |
| Wire up DI for infrastructure | `src/Starter.Infrastructure/DependencyInjection.cs` |
| Wire up DI for a module | the module's `{Name}Module.ConfigureServices` |
| Read core data from a module without injecting `IApplicationDbContext` | `ITenantReader`, `IUserReader`, `IRoleReader` from `Starter.Abstractions.Readers` |

### Frontend

| If you need to... | Look in |
|---|---|
| Add a new core feature | `src/features/{name}/{api,components,pages,hooks}/` |
| Add a new module | `src/features/{name}/` + `src/features/{name}/index.ts` exporting the module object |
| Define a new slot | `src/lib/extensions/slot-map.ts` (add a typed entry to `SlotMap`) |
| Render a slot from core | `<Slot id="..." props={...} />` from `@/lib/extensions` |
| Conditionally show UI when at least one slot entry exists | `hasSlotEntries('slot-id')` from `@/lib/extensions` |
| Register a slot entry from a module | `registerSlot('slot-id', { id, module, order, label?, icon?, permission?, component })` inside the module's `index.ts` `register()` |
| Add a route | `src/routes/routes.tsx` (use `activeModules.X ? lazy(...) : NullPage` pattern for module routes) |
| Add a sidebar entry | `src/components/layout/MainLayout/Sidebar.tsx` |
| Add a permission constant | `src/constants/permissions.ts` (mirror from backend) |
| Add an API hook | `src/features/{name}/api/{name}.queries.ts` (TanStack Query) |
| Toggle a module on/off in this build | `src/config/modules.config.ts` (`activeModules.X` flag + `enabledModules` array entry + import) |

---

## 6. Key patterns

### 6.1 Clean Architecture + CQRS via MediatR

Standard layered separation: **Domain has zero dependencies. Application defines interfaces. Infrastructure implements them.** Every operation is either a `Command` (mutates state, returns `Result<T>`) or a `Query` (reads state, returns `Result<T>`). Each handler is one file.

The MediatR pipeline runs four behaviors in order:

1. `ValidationBehavior` — runs FluentValidation `AbstractValidator<T>` if one is registered
2. `LoggingBehavior` — structured request/response logging
3. `PerformanceBehavior` — warns on slow handlers
4. `TracingBehavior` — OpenTelemetry span per request

All handlers return `Result<T>` (never throw). Controllers use `HandleResult()` / `HandlePagedResult()` from `BaseApiController` to convert `Result` → `IActionResult`.

### 6.2 Multi-tenancy via global query filters

Entities with a `TenantId` property implement `Starter.Domain.Common.ITenantEntity`. `ApplicationDbContext.OnModelCreating` walks every entity type, sees the marker, and applies a query filter:

```csharp
TenantId == null || e.TenantId == TenantId
```

(`TenantId` here is the **current user's** tenant id, resolved from `ICurrentUserService` and exposed as a context property.) Platform admins (`TenantId == null`) see all rows. Tenant users see only their own. A small number of entities have explicit non-standard filters defined inline (e.g. `Role`/`Invitation`/`SystemSetting` use the "tenant users see global + their own" pattern). Module DbContexts apply their own tenant filters in their own `OnModelCreating`.

### 6.3 Capability contracts + Null Object fallbacks

Core code unconditionally injects capabilities like `IBillingProvider` or `IWebhookPublisher`. The DI container resolves them via the rule "last registration wins":

1. `AddInfrastructure().AddCapabilities()` registers a Null Object via `TryAddScoped`/`TryAddSingleton` — these are no-ops or "throw on write" stubs
2. Each module's `ConfigureServices` runs **after** `AddInfrastructure` and registers its real implementation via `AddScoped` — replacing the null object
3. If the module isn't installed, the null object stays in place

The capability contracts and their Null Objects:

| Contract | Null Object behavior | Real implementation |
|---|---|---|
| `IBillingProvider` | Throws `CapabilityNotInstalledException` on writes (mapped to HTTP 501 by `ExceptionHandlingMiddleware`) | `MockBillingProvider` in `Starter.Module.Billing` |
| `IWebhookPublisher` | Silent no-op (logs at Debug) | `WebhookPublisher` in `Starter.Module.Webhooks` |
| `IImportExportRegistry` | Returns empty collections | `ImportExportRegistry` in `Starter.Module.ImportExport` |
| `IQuotaChecker` | Returns `Unlimited()` | _no real implementation yet — Billing module is the natural future provider_ |
| `IUsageMetricCalculator` | Not registered for unknown metrics → `UsageTrackerService.GetAsync` returns 0 | Per-metric: 4 core calculators in `Starter.Infrastructure/Capabilities/MetricCalculators/` + `WebhookUsageMetricCalculator` in `Starter.Module.Webhooks` |

`IUsageMetricCalculator` uses a slightly different pattern from the others — it's injected as `IEnumerable<IUsageMetricCalculator>` into `UsageTrackerService`, which builds a dictionary keyed by `Metric` name and dispatches. This lets each module own its own metric without core ever seeing module entities.

The lifetime of each Null Object **matches** the real implementation, so swapping doesn't shift lifetimes (Billing/Webhooks Null Objects are Scoped; ImportExport/QuotaChecker are Singleton).

### 6.4 Reader services for cross-context data

Modules with their own DbContext cannot do EF joins against `ApplicationDbContext`. When a module needs core data (the tenant name, the user email, a role), it injects a **reader service** that returns a flat DTO:

```csharp
public sealed record TenantSummary(Guid Id, string Name, string? Slug, string Status);
public sealed record UserSummary(Guid Id, Guid? TenantId, string Username, string Email, string DisplayName, string Status);
public sealed record RoleSummary(Guid Id, string Name, Guid? TenantId, bool IsSystemRole);
```

Real implementations live in `Starter.Infrastructure/Readers/` and use `IApplicationDbContext`, `IgnoreQueryFilters().AsNoTracking()`, and `Select(...)` projections — no entity tracking, no navigation. Modules see only the DTOs.

`WebhookUserEventHandler` is the first production consumer of `IUserReader`. Future modules should prefer reader services over `IApplicationDbContext` injection wherever the use case fits.

### 6.5 MassTransit transactional outbox for cross-module events

Cross-module side effects flow through domain events. The flow:

1. A core handler (e.g. `RegisterTenantCommandHandler`) does its business write AND publishes a `TenantRegisteredEvent` via `IPublishEndpoint` — both inside `SaveChangesAsync`
2. MassTransit's EF Core outbox writes the event into the `OutboxMessage` table **in the same transaction** as the business data
3. A background dispatcher polls `OutboxMessage`, sends each event to any registered consumer, and marks it delivered in `OutboxState`
4. `InboxState` deduplicates retries

Key facts:
- The outbox is registered against **`ApplicationDbContext` only**. Module DbContexts deliberately don't have outbox tables — all events flow through the single core outbox. This keeps retry/dedup bookkeeping in one place.
- A module can publish events too: it injects `IPublishEndpoint` like core does.
- A module subscribes by implementing `IConsumer<TEvent>` — MassTransit's assembly scanning picks it up automatically.
- If no consumer is registered for an event (because the handling module is absent), the event fires into the void with no error.

The Billing module's `CreateFreeTierSubscriptionOnTenantRegistered` is a reference implementation: it consumes `TenantRegisteredEvent`, checks idempotency manually (`TenantSubscriptions.AnyAsync(s => s.TenantId == evt.TenantId)`), and provisions a free-tier subscription using its own `BillingDbContext`.

### 6.6 Per-module DbContext with isolated migration history

Each module that owns persisted data has its own `DbContext` implementing `IModuleDbContext`:

- `BillingDbContext` → tables `subscription_plans`, `tenant_subscriptions`, `payment_records`, `plan_price_histories`; migration history table `__EFMigrationsHistory_Billing`
- `WebhooksDbContext` → tables `webhook_endpoints`, `webhook_deliveries`; `__EFMigrationsHistory_Webhooks`
- `ImportExportDbContext` → table `import_jobs`; `__EFMigrationsHistory_ImportExport`

All four contexts (core + 3 modules) **point at the same physical database**. They differ only in which entities they know about and which migration history table they bookkeep. This is the same pattern ABP Commercial / OrchardCore / Shopware use — it gives true module isolation without the operational overhead of multi-database deployments.

A handler may inject **two** contexts when it crosses the boundary. The Billing handlers that need `FeatureFlag` (a core entity) inject both `BillingDbContext` AND `IApplicationDbContext`. This is the "Rule B" pattern documented in the module guide; both contexts share the same physical connection so writes remain cohesive.

### 6.7 Frontend slot registry

`<Slot id="..." props={...} />` is the only way core renders module-contributed UI. The flow:

1. `slot-map.ts` declares each slot id and its prop type (e.g. `'tenant-detail-tabs': { tenantId: string; tenantName: string }`)
2. A module's `index.ts` calls `registerSlot('tenant-detail-tabs', { id, module, order, permission, component: lazy(() => import('./MyTab')) })` inside its `register()` function
3. `main.tsx` calls `registerAllModules()` (from `config/modules.config.ts`) BEFORE React mounts, so by the time any component renders, the slot registry is fully populated
4. A core page renders `<Slot id="tenant-detail-tabs" props={{ tenantId, tenantName }} />` — the component filters entries by permission, sorts by `order`, and renders each one wrapped in `<Suspense>`
5. `hasSlotEntries('tenant-detail-tabs')` returns whether anything is registered, which a core page can use to hide a tab button entirely when no module contributes

Core has zero `import` statements pointing at module folders. The ESLint rule `no-restricted-imports` blocks future regressions, including `import type`.

### 6.8 Capability registry (frontend)

A parallel mechanism for runtime hooks/services that aren't visual contributions. `registerCapability('myCap', impl)` from a module, `getCapability<F>('myCap')` from core. Core falls back to a sensible default when no implementation is registered. See the JSDoc example in `src/lib/extensions/capabilities.ts` for the realistic future use case (a `Payments` module exposing a `processPayment` hook).

### 6.9 Cross-module communication — three patterns, one rule

When a module needs something to happen outside its own boundaries, there are exactly three allowed patterns:

1. **Capability contract calls (Pattern 1, default)** — inject a capability like `IWebhookPublisher` from `Starter.Abstractions.Capabilities` and call it unconditionally. Single-consumer, synchronous, Null Object fallback when the provider is absent. Example: Billing's `ChangePlanCommandHandler` calls `webhookPublisher.PublishAsync("subscription.changed", ...)` after `SaveChangesAsync`.

2. **Integration events via `IPublishEndpoint` (Pattern 2, for fan-out)** — define the event type in `Starter.Application/Common/Events/`, publish via `IPublishEndpoint`, consume via `IConsumer<T>`. 0..N consumers, asynchronous, transactional-outbox reliability. Example: core publishes `TenantRegisteredEvent`; Billing module's `CreateFreeTierSubscriptionOnTenantRegistered` consumes it.

3. **Reader services (Pattern 3, for cross-module reads)** — inject `ITenantReader` / `IUserReader` / `IRoleReader` from `Starter.Abstractions.Readers` and call `GetAsync(id)`. One query, flat DTO, no entity tracking. Example: Webhooks module's `WebhookUserEventHandler` uses `IUserReader` to look up a user's email for a delivery.

The forbidden fourth pattern is **cross-module `INotificationHandler<T>` consuming another module's domain event**. This created a hidden compile-time coupling (the consuming module had `using` for the other module's event type). `WebhookBillingEventHandler` was the last example; it was deleted during the module type relocation refactor and replaced by Pattern 1.

**Full decision tree, real examples, and anti-patterns** are in [cross-module-communication.md](./cross-module-communication.md).

---

## 7. Request lifecycle (HTTP → DB)

A typical authenticated POST request to a core feature:

```
1. HTTP request arrives at Kestrel
   → ASP.NET routing matches /api/v1/Users (e.g. UsersController.Create)

2. Middleware pipeline (in Program.cs):
   a. ExceptionHandlingMiddleware (catches everything below)
   b. Serilog request logging
   c. CORS
   d. Rate limiting (10/s, 100/m default)
   e. Authentication (JWT bearer OR X-Api-Key, depending on the request)
   f. Authorization (Permission policy provider checks the [Authorize(Policy = ...)])
   g. TenantResolutionMiddleware (sets ICurrentUserService.TenantId from the JWT claims)
   h. RouteToController

3. Controller action (UsersController.Create) calls Mediator.Send(command)

4. MediatR pipeline:
   a. ValidationBehavior runs CreateUserCommandValidator
   b. LoggingBehavior logs the command name + correlation id
   c. PerformanceBehavior starts a stopwatch
   d. TracingBehavior starts an OTel span
   e. CreateUserCommandHandler runs

5. Handler logic:
   - Reads/writes via IApplicationDbContext (which is the scoped ApplicationDbContext)
   - May call core services (IFileService, IEmailService, etc.) — all injected
   - May publish a domain event via IPublishEndpoint (lands in outbox transactionally)
   - Returns Result<UserDto>

6. ApplicationDbContext.SaveChangesAsync triggers two interceptors:
   a. AuditableEntityInterceptor stamps CreatedAt/CreatedBy/ModifiedAt/ModifiedBy
   b. DomainEventDispatcherInterceptor collects any AggregateRoot.DomainEvents and dispatches them via MediatR INotificationHandler<T> AFTER the save completes

7. Controller receives Result<UserDto>, calls HandleResult(result):
   - Success → 200 with ApiResponse<UserDto>
   - NotFound → 404
   - Validation failure → 400 with the FluentValidation error dictionary
   - Forbidden → 403
   - CapabilityNotInstalledException (e.g. someone called IBillingProvider on a no-Billing build) → 501

8. Response goes back through the middleware in reverse order
   - OTel span closes, performance behavior logs duration, exception middleware no-ops
```

---

## 8. Event lifecycle (cross-module side effects)

A `TenantRegisteredEvent` flowing from `RegisterTenantCommandHandler` to the Billing module's `CreateFreeTierSubscriptionOnTenantRegistered` consumer:

```
[RegisterTenantCommandHandler — runs in core, inside the request scope]
   1. Validate inputs, check email uniqueness, generate slug
   2. Tenant.Create(...)  ──── added to ApplicationDbContext.Tenants
   3. User.Create(...)    ──── added to ApplicationDbContext.Users
   4. publishEndpoint.Publish(new TenantRegisteredEvent(tenant.Id, ...))
   5. context.SaveChangesAsync(ct)
        │
        ├─ EF Core writes:
        │     INSERT INTO tenants ...
        │     INSERT INTO users ...
        │     INSERT INTO "OutboxMessage" (Body, MessageType, ...)   ← outbox row
        │     COMMIT
        │
        └─ Returns Result.Success(tenant.Id) to the controller

[MassTransit background delivery service — runs out of band]
   6. Polls "OutboxState" for the next pending message (FOR UPDATE SKIP LOCKED)
   7. Reads the OutboxMessage row, deserializes the event
   8. Routes the event to every registered IConsumer<TenantRegisteredEvent>
        │
        └─ The Billing module registered CreateFreeTierSubscriptionOnTenantRegistered
           (auto-discovered via AddConsumers(billingAssembly) at startup)
        │
[CreateFreeTierSubscriptionOnTenantRegistered — runs in a background scope]
   9. Manual idempotency check:
        if (await billingDb.TenantSubscriptions.AnyAsync(s => s.TenantId == evt.TenantId))
            return;
  10. Look up the active free plan in BillingDbContext
  11. TenantSubscription.Create(...) → billingDb.TenantSubscriptions.Add(...)
  12. billingDb.SaveChangesAsync()                    ← writes via Billing's __EFMigrationsHistory_Billing schema
  13. usageTracker.SetAsync(evt.TenantId, "users", 1) ← seeds initial seat count
  14. MassTransit marks the OutboxMessage delivered → updates InboxState

If the consumer throws:
  - MassTransit retries with backoff (default: 5 attempts)
  - After exhaustion, the message moves to a dead-letter queue
  - The original tenant + user remain — only the billing side effect is lost
```

If the Billing module is **not installed**, step 6 still finds the OutboxMessage row but step 8 finds zero registered consumers. MassTransit marks the message delivered with no work done, and the tenant ends up with no subscription — exactly the desired behavior for a no-Billing build.

---

## 9. Module loading at startup

`Program.cs` orchestrates module discovery and registration in this order:

```
1. var modules = ModuleLoader.DiscoverModules();
     // Walks the bin/ directory looking for *.Module.*.dll, loads each into the AppDomain,
     // scans for IModule implementations, returns the instances.

2. var orderedModules = ModuleLoader.ResolveOrder(modules);
     // Topological sort by IModule.Dependencies. None of the current 3 modules have deps.

3. var moduleAssemblies = orderedModules.Select(m => m.GetType().Assembly).Distinct().ToList();

4. builder.Services.AddSingleton<IReadOnlyList<IModule>>(orderedModules);
     // DataSeeder pulls this for permission aggregation + module migrate/seed orchestration.

5. builder.Services.AddApplication(moduleAssemblies);
     // MediatR scans core + module assemblies for handlers, validators, behaviors.

6. builder.Services.AddInfrastructure(builder.Configuration, moduleAssemblies);
     // Persistence, caching, MassTransit (with module assembly scanning for consumers),
     // capability null fallbacks (TryAddScoped/TryAddSingleton — modules will replace), readers,
     // email/sms/realtime/storage/health.

7. builder.Services.AddIdentityInfrastructure(builder.Configuration);
     // JWT, password hashing, TOTP, refresh tokens.

8. foreach (var module in orderedModules)
       module.ConfigureServices(builder.Services, builder.Configuration);
     // Each module registers its own DbContext, capability implementations (overriding the
     // null fallbacks because module ConfigureServices runs AFTER AddInfrastructure),
     // and any module-private services.

9. var mvcBuilder = builder.Services.AddControllers();
   foreach (var asm in moduleAssemblies)
       mvcBuilder.AddApplicationPart(asm);
     // Tells MVC to discover controllers from each module's assembly.

10. ... rest of pipeline (Swagger, CORS, rate limiting, OpenTelemetry, exception middleware) ...

11. var app = builder.Build();

12. if (configuration["DatabaseSettings:SeedDataOnStartup"] == "true")
        await DataSeeder.SeedAsync(app.Services);

      // DataSeeder.SeedAsync orchestrates:
      //   a. ApplicationDbContext.Database.MigrateAsync()
      //   b. SeedPermissionsAsync (core + modules' GetPermissions())
      //   c. SeedRolesAsync
      //   d. SeedRolePermissionsAsync
      //   e. SeedDefaultTenantAsync
      //   f. SeedSuperAdminUserAsync
      //   g. SeedDefaultSettingsAsync
      //   h. SeedFeatureFlagsAsync
      //   i. foreach (module) → module.MigrateAsync(serviceProvider)   ← module DbContext migrations
      //   j. foreach (module) → module.SeedDataAsync(serviceProvider)  ← module-owned seed data

13. app.Run();
```

The whole sequence is gated by `SeedDataOnStartup`. In production, you set the flag to `false`, run migrations out of band, and seed manually.

---

## 10. Architecture rules and how they're enforced

The rules in [module-development-guide.md Section E](./module-development-guide.md#e-the-dont-rules--architectural-discipline) are enforced at three levels:

### Compile-time (project references)

The csproj graph itself prevents most violations:
- `Starter.Domain` has zero references — it cannot depend on anything
- `Starter.Abstractions` has **zero project references** too — only two `Microsoft.Extensions.*.Abstractions` NuGet packages. Every type in its contract signatures lives inside the project itself.
- `Starter.Application` doesn't reference `Starter.Infrastructure` — handlers cannot call EF Core implementations directly, only the abstractions they own
- `Starter.Application` doesn't reference `Starter.Module.*` — core handlers cannot import module types
- Module projects only reference `Starter.Abstractions.Web` — they cannot reach into `Starter.Infrastructure` or each other
- `Starter.Domain/` contains only core entities — module-owned entities (`SubscriptionPlan`, `WebhookEndpoint`, `ImportJob`, etc.) live inside each module's own `Domain/` folder

### Test-time (`AbstractionsPurityTests`)

`tests/Starter.Api.Tests/Architecture/AbstractionsPurityTests.cs` runs on every `dotnet test` and asserts via reflection that `Starter.Abstractions.dll`'s referenced assemblies are all on an explicit allowlist. The allowlist is `{ Starter.Abstractions, System, netstandard, Microsoft.Extensions.Configuration.Abstractions, Microsoft.Extensions.DependencyInjection.Abstractions, Microsoft.Extensions.Primitives }` — anything else fails CI. The forbidden list covers every other `Starter.*` project (including `Domain`, `Application`, `Shared`, `Abstractions.Web`) and every framework package prefix (`Microsoft.AspNetCore`, `Microsoft.EntityFrameworkCore`, `MassTransit`).

Adding a new dependency is a deliberate, visible change — you'd have to edit both the allowlist and the forbidden list, which a code reviewer will spot. Add new architecture tests in the same folder when you formalize a new rule.

### Lint-time (frontend ESLint `no-restricted-imports`)

`boilerplateFE/eslint.config.js` blocks core files from importing `@/features/billing/*`, `@/features/webhooks/*`, or `@/features/import-export/*`. The rule includes `import type`. The allowlist permits the modules themselves, `src/config/modules.config.ts`, `src/app/main.tsx`, and `src/routes/routes.tsx`.

### Manual (the killer test)

The ultimate enforcement is generating a fresh app with `pwsh ./scripts/rename.ps1 -Modules None` and confirming it builds. This catches anything the static analysis missed: a transitive type leak, a missed `using`, a dead `lazy()` import. Run this before every module-touching merge.

---

**Got a question this doc doesn't answer?** Add it. The doc is the source of truth — keep it accurate.
