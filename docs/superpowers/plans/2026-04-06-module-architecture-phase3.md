# Phase 3: Extract 8 Remaining Features as Modules

**Goal:** Extract AuditLogs, Notifications, FeatureFlags, Reports, ApiKeys, Billing, Webhooks, and ImportExport as self-contained modules following the proven Files module pattern.

**Approach:** Each task extracts one feature. Build must succeed after each. Same pattern: create module project, move handlers/controller/EF config, update namespaces, move permissions to module, remove from core.

**Key rules (same as Phase 1&2):**
- Zero behavior changes — API responses, tenant isolation, permissions identical
- Core interfaces stay in `Starter.Application` — INotificationService, IFeatureFlagService, IBillingProvider, etc.
- Module handlers use `_context.Set<T>()` instead of named DbSets
- Permission strings stay identical (e.g., `System.ViewAuditLogs` stays `System.ViewAuditLogs`)

**Entity placement — based on "does core code reference it?":**

| Stays in `Starter.Domain` (core) | Why |
|---|---|
| AuditLog | Created by AuditableEntityInterceptor (core infrastructure) |
| Notification | Created by NotificationService (core, used by all features) |
| FeatureFlag, TenantFeatureFlag | Queried by FeatureFlagService (core, used everywhere) |
| ApiKey | Validated by ApiKeyAuthenticationHandler (core security middleware) |
| SubscriptionPlan, TenantSubscription | Used by RegisterTenantCommandHandler (core Auth — assigns plan on tenant registration) |

| Moves to module `Domain/Entities/` | Module |
|---|---|
| NotificationPreference | Notifications — no core references |
| ReportRequest | Reports — only feature-internal (UsageTracker switches to `Set<T>()`) |
| WebhookEndpoint, WebhookDelivery | Webhooks — WebhookPublisher + CleanupJob move with the module |
| ImportJob | ImportExport — ProcessImportConsumer moves with the module |
| PaymentRecord, PlanPriceHistory | Billing — only billing handlers reference these |

**New modules (future)** always own their entities in `module/Domain/Entities/` since no core code references them.

**Service placement:**
- Core service implementations stay in `Starter.Infrastructure` — NotificationService, FeatureFlagService
- Module-specific services move with the module — MockBillingProvider, WebhookPublisher, WebhookDeliveryCleanupJob, ImportExportRegistry

---

## Extraction Order (dependency-safe)

1. **AuditLogs** — 4 files, zero dependencies, easiest
2. **Notifications** — 19 files, event-driven, no module deps
3. **FeatureFlags** — 26 files, central hub (extract before ApiKeys/Billing that depend on it)
4. **Reports** — 16 files + MassTransit consumer, requires infra change for consumer discovery
5. **ApiKeys** — 19 files, uses IFeatureFlagService (core interface)
6. **Webhooks** — ~30 files, 5 domain event handlers + consumer + services, depends on core events
7. **ImportExport** — ~20 files, consumer + registry + definitions, depends on core services
8. **Billing** — ~46 files, largest, SyncPlanFeaturesHandler + MockBillingProvider (last — most complex)

---

## Task 1: Extract AuditLogs Module

**Move:** 3 application files + 1 controller = 4 files total
**No EF config** (convention-based)

**Create:**
- `src/modules/Starter.Module.AuditLogs/Starter.Module.AuditLogs.csproj`
- `AuditLogsModule.cs` — no deps, permission: `System.ViewAuditLogs` (keep string value)
- `Constants/AuditLogPermissions.cs` — `ViewAuditLogs = "System.ViewAuditLogs"`
- `Application/Queries/GetAuditLogs/` — GetAuditLogsQuery, GetAuditLogsQueryHandler, AuditLogDto
- `Controllers/AuditLogsController.cs`

**Remove from core:**
- `Starter.Application/Features/AuditLogs/` (delete directory)
- `Starter.Api/Controllers/AuditLogsController.cs`
- `System.ViewAuditLogs` from `Permissions.cs` + `GetAllWithMetadata()`
- `Permissions.System.ViewAuditLogs` from `Roles.cs` Admin mapping

**Handler change:** `context.AuditLogs` → `context.Set<AuditLog>()`

---

## Task 2: Extract Notifications Module

**Move:** 16 application files + 1 controller + 2 EF configs = 19 files total
**Entity moves to module:** `NotificationPreference` (no core references)
**Entity stays in core:** `Notification` (created by core NotificationService)

**Create:**
- `src/modules/Starter.Module.Notifications/Starter.Module.Notifications.csproj`
- `NotificationsModule.cs` — no permissions, no deps
- `Domain/Entities/NotificationPreference.cs` — moved from Starter.Domain
- `Application/Commands/` — MarkNotificationRead, MarkAllNotificationsRead, UpdateNotificationPreferences
- `Application/Queries/` — GetNotifications, GetNotificationPreferences, GetUnreadCount
- `Application/EventHandlers/` — UserCreatedNotificationHandler, PasswordChangedNotificationHandler
- `Application/DTOs/` — NotificationDto, NotificationPreferenceDto
- `Infrastructure/Configurations/` — NotificationConfiguration (stays, entity in core), NotificationPreferenceConfiguration (moves with entity)
- `Controllers/NotificationsController.cs`

**Stays in core:**
- `INotificationService` (interface) — used by Reports consumer
- `NotificationService` (implementation) — implements core interface
- `Notification` entity in Domain.Common — created by core service
- `NotificationConfiguration.cs` — EF config for core entity stays in Infrastructure

**Remove from core:**
- `Starter.Application/Features/Notifications/` (delete directory)
- `Starter.Api/Controllers/NotificationsController.cs`
- `NotificationPreferenceConfiguration.cs` from Infrastructure (moves to module)
- `NotificationPreference.cs` from Domain.Common (moves to module Domain/Entities/)
- `NotificationConfiguration.cs` stays in Infrastructure (Notification entity is core)
- No permission changes (Notifications has none)

**Handler changes:** `context.Notifications` → `context.Set<Notification>()`, `context.NotificationPreferences` → `context.Set<NotificationPreference>()`
**DbSet removal:** Remove `DbSet<NotificationPreference>` from IApplicationDbContext and ApplicationDbContext (entity moves to module)

---

## Task 3: Extract FeatureFlags Module

**Move:** 23 application files + 1 controller + 2 EF configs = 26 files total

**Create:**
- `src/modules/Starter.Module.FeatureFlags/Starter.Module.FeatureFlags.csproj`
- `FeatureFlagsModule.cs` — 6 permissions, `SeedDataAsync()` seeds feature flags (moved from DataSeeder)
- `Constants/FeatureFlagPermissions.cs` — View, Create, Update, Delete, ManageTenantOverrides, OptOut
- `Application/Commands/` — CreateFeatureFlag, UpdateFeatureFlag, DeleteFeatureFlag, SetTenantOverride, RemoveTenantOverride, OptOutFeatureFlag, RemoveOptOut
- `Application/Queries/` — GetFeatureFlags, GetFeatureFlagByKey
- `Application/DTOs/` — FeatureFlagDto, FeatureFlagMapper
- `Infrastructure/Configurations/` — FeatureFlagConfiguration, TenantFeatureFlagConfiguration
- `Controllers/FeatureFlagsController.cs`

**Stays in core:**
- `IFeatureFlagService` (interface) — used by ApiKeys, Billing, many features
- `FeatureFlagService` (implementation) — has caching layer, implements core interface
- FeatureFlag, TenantFeatureFlag entities in Domain.FeatureFlags

**Remove from core:**
- `Starter.Application/Features/FeatureFlags/` (delete directory)
- `Starter.Api/Controllers/FeatureFlagsController.cs`
- 2 EF configs from Infrastructure
- `Permissions.FeatureFlags` class + entries from `GetAllWithMetadata()`
- FeatureFlags references from `Roles.cs` (Admin: View+OptOut)
- `SeedFeatureFlagsAsync()` from `DataSeeder` + its call in `SeedAsync()`

**Handler changes:** `context.FeatureFlags` → `context.Set<FeatureFlag>()`, `context.TenantFeatureFlags` → `context.Set<TenantFeatureFlag>()`

**Seed migration:** Move `SeedFeatureFlagsAsync` logic into `FeatureFlagsModule.SeedDataAsync()`. Resolve `ApplicationDbContext` from `IServiceProvider`.

**Default role mappings:**
- SuperAdmin: all 6 permissions
- Admin: View, OptOut

---

## Task 4: Extract Reports Module + MassTransit Consumer Discovery

**Move:** 12 application files + 1 controller + 1 EF config + 1 consumer + 1 message = 16 files total
**Entity moves to module:** `ReportRequest` (only UsageTrackerService references it externally — switch to `Set<T>()`)

**Prerequisite infra change:** Modify `Infrastructure.DependencyInjection.AddMessaging()` to auto-discover MassTransit consumers from module assemblies. Thread `moduleAssemblies` from `AddInfrastructure()` → `AddMessaging()`. Add: `foreach (var asm in moduleAssemblies) busConfigurator.AddConsumers(asm);`

**Create:**
- `src/modules/Starter.Module.Reports/Starter.Module.Reports.csproj` — needs `MassTransit` package ref
- `ReportsModule.cs` — permissions: `System.ExportData`, `System.ForceExport` (keep string values)
- `Constants/ReportPermissions.cs`
- `Domain/Entities/ReportRequest.cs` — moved from Starter.Domain/Common/
- `Application/Commands/` — RequestReport, DeleteReport
- `Application/Queries/` — GetReports, GetReportDownload
- `Application/DTOs/` — ReportDto, ReportMapper
- `Application/Messages/GenerateReportMessage.cs` (moved from Application/Common/Messages/)
- `Infrastructure/Consumers/GenerateReportConsumer.cs`
- `Infrastructure/Configurations/ReportRequestConfiguration.cs`
- `Controllers/ReportsController.cs`

**Stays in core:**
- `IExportService`, `INotificationService`, `IFileService`, `ISettingsService` — all core interfaces used by consumer

**Core fixup:** `UsageTrackerService` references `context.ReportRequests` — change to `context.Set<ReportRequest>()` with using for the module's entity namespace. Remove `DbSet<ReportRequest>` from IApplicationDbContext and ApplicationDbContext.

**Remove from core:**
- `Starter.Application/Features/Reports/` (delete directory)
- `Starter.Api/Controllers/ReportsController.cs`
- `Starter.Infrastructure/Consumers/GenerateReportConsumer.cs`
- `Starter.Application/Common/Messages/GenerateReportMessage.cs` (if it exists there)
- `ReportRequestConfiguration.cs` from Infrastructure
- `System.ExportData` and `System.ForceExport` from `Permissions.cs` + `GetAllWithMetadata()`
- Report permission references from `Roles.cs`
- Remove explicit `AddConsumer<GenerateReportConsumer>()` from `DependencyInjection.AddMessaging()`

**Default role mappings:**
- SuperAdmin: ExportData, ForceExport
- Admin: ExportData

---

## Task 5: Extract ApiKeys Module

**Move:** 17 application files + 1 controller + 1 EF config = 19 files total

**Create:**
- `src/modules/Starter.Module.ApiKeys/Starter.Module.ApiKeys.csproj`
- `ApiKeysModule.cs` — 9 permissions
- `Constants/ApiKeyPermissions.cs`
- `Application/Commands/` — CreateApiKey, UpdateApiKey, RevokeApiKey, EmergencyRevokeApiKey
- `Application/Queries/` — GetApiKeys, GetApiKeyById
- `Application/DTOs/` — ApiKeyDto, ApiKeyMapper, CreateApiKeyResponse
- `Infrastructure/Configurations/ApiKeyConfiguration.cs`
- `Controllers/ApiKeysController.cs`

**Stays in core:**
- ApiKey entity in Domain.ApiKeys
- `IFeatureFlagService`, `IUsageTracker`, `IPasswordService` — core interfaces used by handlers

**Remove from core:**
- `Starter.Application/Features/ApiKeys/` (delete directory)
- `Starter.Api/Controllers/ApiKeysController.cs`
- `ApiKeyConfiguration.cs` from Infrastructure
- `Permissions.ApiKeys` class + entries from `GetAllWithMetadata()`
- ApiKeys references from `Roles.cs` (Admin: View/Create/Update/Delete)

**Default role mappings:**
- SuperAdmin: all 9 permissions
- Admin: View, Create, Update, Delete (tenant-scoped only)

---

## Task 6: Extract Webhooks Module

**Move:** ~17 handler files + 5 event handlers + 1 controller + 2 EF configs + 2 services + 1 consumer + 1 message = ~30 files total

**Create:**
- `src/modules/Starter.Module.Webhooks/Starter.Module.Webhooks.csproj` — needs `MassTransit` package ref
- `WebhooksModule.cs` — 5 permissions, `ConfigureServices()` registers WebhookPublisher + WebhookDeliveryCleanupJob
- `Constants/WebhookPermissions.cs` — View, Create, Update, Delete, ViewPlatform
- `Application/Commands/` — CreateWebhookEndpoint, UpdateWebhookEndpoint, DeleteWebhookEndpoint, RegenerateWebhookSecret, TestWebhookEndpoint
- `Application/Queries/` — GetWebhookEndpoints, GetWebhookEndpointById, GetAllWebhookEndpoints (admin), GetWebhookDeliveries, GetWebhookDeliveriesAdmin, GetWebhookAdminStats, GetWebhookEventTypes
- `Application/EventHandlers/` — WebhookBillingEventHandler, WebhookFileEventHandler, WebhookInvitationEventHandler, WebhookRoleEventHandler, WebhookUserEventHandler
- `Application/DTOs/` — all Webhook DTOs
- `Application/Messages/DeliverWebhookMessage.cs`
- `Infrastructure/Consumers/DeliverWebhookConsumer.cs`
- `Infrastructure/Services/WebhookPublisher.cs`
- `Infrastructure/Services/WebhookDeliveryCleanupJob.cs`
- `Infrastructure/Configurations/` — WebhookEndpointConfiguration, WebhookDeliveryConfiguration
- `Controllers/WebhooksController.cs`

**Entity moves to module:** `WebhookEndpoint`, `WebhookDelivery` — WebhookPublisher + CleanupJob both move to the module, no remaining core references

**Stays in core:**
- `IWebhookPublisher` (interface) — core contract, other features may trigger webhooks via this interface

**Core fixup:** Remove `DbSet<WebhookEndpoint>` and `DbSet<WebhookDelivery>` from IApplicationDbContext and ApplicationDbContext. Remove explicit query filters for these entities from `ApplyTenantFilters()` — they'll use the convention-based ITenantEntity filter from the module. Remove Domain/Webhooks/ directory from Starter.Domain.

**Remove from core:**
- `Starter.Application/Features/Webhooks/` (delete directory)
- `Starter.Api/Controllers/WebhooksController.cs`
- `Starter.Infrastructure/Services/WebhookPublisher.cs`
- `Starter.Infrastructure/Services/WebhookDeliveryCleanupJob.cs`
- `Starter.Infrastructure/Consumers/DeliverWebhookConsumer.cs`
- 2 EF configs from Infrastructure
- `Permissions.Webhooks` class + entries from `GetAllWithMetadata()`
- Webhooks references from `Roles.cs` (Admin: View/Create/Update/Delete, User: View)
- Remove `services.AddScoped<IWebhookPublisher, WebhookPublisher>()` from `DependencyInjection.AddServices()`
- Remove `AddConsumer<DeliverWebhookConsumer>()` from `AddMessaging()` (auto-discovered from module assembly)
- Remove WebhookDeliveryCleanupJob registration from `AddServices()`

**ConfigureServices:**
```csharp
services.AddScoped<IWebhookPublisher, WebhookPublisher>();
services.AddHostedService<WebhookDeliveryCleanupJob>();
```

**Default role mappings:**
- SuperAdmin: all 5 permissions
- Admin: View, Create, Update, Delete
- User: View

---

## Task 7: Extract ImportExport Module

**Move:** ~8 handler files + 1 controller + 1 EF config + 1 consumer + 1 message + definitions + registry = ~20 files total

**Create:**
- `src/modules/Starter.Module.ImportExport/Starter.Module.ImportExport.csproj` — needs `MassTransit` package ref
- `ImportExportModule.cs` — permissions: `System.ImportData`, `System.ExportData` (shared with Reports for ExportData)
- `Constants/ImportExportPermissions.cs` — ImportData (ExportData is owned by Reports module)
- `Application/Commands/` — StartImport, DeleteImportJob
- `Application/Queries/` — GetEntityTypes, GetImportErrorReport, GetImportJobById, GetImportJobs, GetImportTemplate, PreviewImport
- `Application/DTOs/` — EntityTypeDto, ImportJobDto, ImportPreviewDto, ImportJobMapper
- `Application/Definitions/` — UserImportExportDefinition, UserImportRowProcessor, UserExportDataProvider, RoleImportExportDefinition, RoleImportRowProcessor, RoleExportDataProvider
- `Application/Messages/ProcessImportMessage.cs`
- `Infrastructure/Consumers/ProcessImportConsumer.cs`
- `Infrastructure/Services/ImportExportRegistry.cs`
- `Infrastructure/Configurations/ImportJobConfiguration.cs`
- `Controllers/ImportExportController.cs`

**Entity moves to module:** `ImportJob` — ProcessImportConsumer moves with the module, no remaining core references

**Stays in core:**
- `IImportExportRegistry` (interface) — core contract
- `IExportService` (interface) — core contract

**Core fixup:** Remove `DbSet<ImportJob>` from IApplicationDbContext and ApplicationDbContext. Remove explicit query filter for ImportJob from `ApplyTenantFilters()`. Remove Domain/ImportExport/ directory from Starter.Domain.

**Remove from core:**
- `Starter.Application/Features/ImportExport/` (delete directory)
- `Starter.Api/Controllers/ImportExportController.cs`
- `Starter.Infrastructure/Services/ImportExportRegistry.cs`
- `Starter.Infrastructure/Consumers/ProcessImportConsumer.cs`
- `ImportJobConfiguration.cs` from Infrastructure
- `System.ImportData` from `Permissions.cs` + `GetAllWithMetadata()`
- ImportData references from `Roles.cs` (Admin: ImportData)
- Remove `AddConsumer<ProcessImportConsumer>()` from `AddMessaging()`
- Remove ImportExportRegistry registration from `AddImportExportServices()`

**ConfigureServices:**
```csharp
services.AddScoped<IImportExportRegistry, ImportExportRegistry>();
// Register import/export definitions
services.AddTransient<UserImportExportDefinition>();
services.AddTransient<RoleImportExportDefinition>();
```

**Note on shared permission:** `System.ExportData` is used by BOTH Reports and ImportExport. Since Reports module (Task 4) already owns it, ImportExport module only declares `System.ImportData`. The export functionality uses the Reports module's permission.

**Default role mappings:**
- SuperAdmin: ImportData
- Admin: ImportData

---

## Task 8: Extract Billing Module

**Move:** ~40 application files + 1 controller + 4 EF configs + 1 service = ~46 files total (LARGEST)
**Entities move to module:** `PaymentRecord`, `PlanPriceHistory` — only billing handlers reference these
**Entities stay in core:** `SubscriptionPlan`, `TenantSubscription` — referenced by RegisterTenantCommandHandler (core Auth)

**Create:**
- `src/modules/Starter.Module.Billing/Starter.Module.Billing.csproj`
- `BillingModule.cs` — 5 permissions, `ConfigureServices()` registers MockBillingProvider, `SeedDataAsync()` seeds subscription plans
- `Constants/BillingPermissions.cs`
- `Domain/Entities/PaymentRecord.cs` — moved from Starter.Domain/Billing/
- `Domain/Entities/PlanPriceHistory.cs` — moved from Starter.Domain/Billing/
- `Application/Commands/` — CreatePlan, UpdatePlan, DeactivatePlan, ChangePlan, CancelSubscription, ResyncPlanTenants
- `Application/Queries/` — GetPlans, GetPlanById, GetPlanOptions, GetSubscription, GetAllSubscriptions, GetPayments, GetUsage
- `Application/EventHandlers/SyncPlanFeaturesHandler.cs`
- `Application/DTOs/` — 8 DTO files
- `Infrastructure/Configurations/` — PaymentRecordConfiguration, PlanPriceHistoryConfiguration (move with entities), SubscriptionPlanConfiguration, TenantSubscriptionConfiguration (move — EF config for core entity, discovered via module assembly scan)
- `Infrastructure/Services/MockBillingProvider.cs`
- `Controllers/BillingController.cs`

**Stays in core:**
- `IBillingProvider` (interface) — core contract
- `IUsageTracker`, `IFeatureFlagService` — core interfaces
- `SubscriptionPlan`, `TenantSubscription` entities in Domain.Billing — referenced by core Auth

**Core fixup:** Remove `DbSet<PaymentRecord>` and `DbSet<PlanPriceHistory>` from IApplicationDbContext and ApplicationDbContext. Remove explicit query filters for PaymentRecord from `ApplyTenantFilters()`. Keep SubscriptionPlan and TenantSubscription DbSets and filters (core entities).

**Remove from core:**
- `Starter.Application/Features/Billing/` (delete directory)
- `Starter.Api/Controllers/BillingController.cs`
- `Starter.Infrastructure/Services/MockBillingProvider.cs`
- 4 EF configs from Infrastructure
- `Permissions.Billing` class + entries from `GetAllWithMetadata()`
- Billing references from `Roles.cs` (Admin: View+Manage, User: View)
- `services.AddScoped<IBillingProvider, MockBillingProvider>()` from `DependencyInjection.AddServices()`
- `SeedSubscriptionPlansAsync()` from `DataSeeder` + its call in `SeedAsync()`

**ConfigureServices:**
```csharp
services.AddScoped<IBillingProvider, MockBillingProvider>();
```

**Seed migration:** Move `SeedSubscriptionPlansAsync` logic into `BillingModule.SeedDataAsync()`.

**Default role mappings:**
- SuperAdmin: all 5 permissions
- Admin: View, Manage
- User: View

---

## Task 9: Core Cleanup + Final Verification

1. **Permissions.cs** — verify only Users (6), Roles (6), System (ViewDashboard, ManageSettings), Tenants (5) remain. System group should have only 2 permissions left.
2. **Roles.cs** — verify GetRolePermissions() only references remaining core permissions; SuperAdmin still uses `Permissions.GetAll()` (auto-includes only core). Admin and User should only have core permission refs.
3. **IApplicationDbContext** — verify DbSets removed for entities that moved to modules: NotificationPreference, ReportRequest, WebhookEndpoint, WebhookDelivery, ImportJob, PaymentRecord, PlanPriceHistory. Remaining DbSets: User, Role, Permission, UserRole, RolePermission, Tenant, AuditLog, Invitation, Session, LoginHistory, Notification, FileMetadata, SystemSetting, ApiKey, FeatureFlag, TenantFeatureFlag, SubscriptionPlan, TenantSubscription + `Set<T>()`.
4. **ApplicationDbContext.ApplyTenantFilters()** — verify explicit filters removed for entities that moved to modules. Those entities now use convention-based ITenantEntity filter from module assembly. Keep explicit filters for core entities.
5. **DataSeeder** — verify SeedFeatureFlagsAsync and SeedSubscriptionPlansAsync removed; SeedPermissionsAsync and SeedRolePermissionsAsync still aggregate from modules correctly.
6. **Infrastructure DependencyInjection.cs** — verify: no removed consumer references in AddMessaging(), no MockBillingProvider in AddServices(), no WebhookPublisher/DeliveryCleanup, no ImportExportRegistry. Verify AddMessaging() auto-discovers consumers from module assemblies.
7. **Starter.Domain** — verify Domain/Webhooks/ and Domain/ImportExport/ directories removed (entities moved to modules). Domain/Billing/ still has SubscriptionPlan + TenantSubscription (core entities) but PaymentRecord and PlanPriceHistory removed.
8. **Build full solution** — `dotnet build` with all 9 modules referenced
9. **Build without modules** — comment out all 9 module references in Api.csproj, verify `dotnet build` succeeds (core standalone)
10. **Swagger check** — run the API, verify all endpoints from all 9 modules appear
11. **Module count** — verify ModuleLoader discovers 9 modules at startup (Files + 8 new)

---

## Files Modified Per Task (summary)

| Task | Files Moved | New Files | Core Files Modified |
|------|-------------|-----------|-------------------|
| 1. AuditLogs | 4 | 3 (module/csproj/permissions) | Permissions.cs, Roles.cs |
| 2. Notifications | 19 | 2 (module/csproj) | — |
| 3. FeatureFlags | 26 | 3 (module/csproj/permissions) | Permissions.cs, Roles.cs, DataSeeder.cs |
| 4. Reports | 16 | 4 (module/csproj/permissions + MassTransit pkg) | Permissions.cs, Roles.cs, Infra DI (MassTransit) |
| 5. ApiKeys | 19 | 3 (module/csproj/permissions) | Permissions.cs, Roles.cs |
| 6. Webhooks | 30 | 3 (module/csproj/permissions) | Permissions.cs, Roles.cs, Infra DI |
| 7. ImportExport | 20 | 3 (module/csproj/permissions) | Permissions.cs, Roles.cs, Infra DI |
| 8. Billing | 46 | 3 (module/csproj/permissions) | Permissions.cs, Roles.cs, Infra DI, DataSeeder.cs |
| 9. Cleanup | 0 | 0 | Verify all above |

**Total:** ~180 files moved, 24 new files created, ~6 core files surgically edited across tasks
