# Project: Boilerplate CQRS

Full-stack boilerplate — .NET 10 backend (Clean Architecture + CQRS), React 19 frontend (TypeScript + Tailwind CSS 4 + shadcn/ui), and Flutter mobile client (Dart 3 + flutter_bloc + modular Clean Architecture).

## Build & Run

```bash
# Backend
cd boilerplateBE/src/Starter.Api && dotnet run --launch-profile http

# Frontend
cd boilerplateFE && npm run dev

# Build check
cd boilerplateFE && npm run build

# Mobile (Flutter)
cd boilerplateMobile && flutter pub get
dart run build_runner build --delete-conflicting-outputs
flutter run --flavor staging -t lib/main_staging.dart

# Docker services (from boilerplateBE/)
docker compose up -d
```

## Architecture Overview

```
API (Controllers) → Application (MediatR CQRS) → Domain (Entities) ← Infrastructure (EF Core, Services)
```

- **Clean Architecture** — Domain has zero dependencies. Application defines interfaces. Infrastructure implements them.
- **CQRS via MediatR** — Commands mutate state, Queries read. Each handler is a single file.
- **Multi-tenancy** — Global EF query filters on `ApplicationDbContext`. Platform admins (`TenantId=null`) see all data. Tenant users see only their tenant's data.
- **Auth** — JWT bearer tokens + API key auth (`X-Api-Key` header). Refresh token rotation. TOTP/2FA.
- **Result pattern** — All handlers return `Result<T>`. Controllers use `HandleResult()` / `HandlePagedResult()`.
- **Pipeline behaviors** — `ValidationBehavior`, `LoggingBehavior`, `PerformanceBehavior`, `TracingBehavior` (OpenTelemetry).

## Feature Inventory

### Backend (15 features, 16 controllers)

| Feature | Key Operations | Controller | Domain Entity |
|---------|---------------|------------|---------------|
| Auth | Login, Register, 2FA, Sessions, Invitations, Password Reset | AuthController | User, Session, LoginHistory, Invitation |
| Users | CRUD, Activate/Suspend/Deactivate/Unlock | UsersController | User, UserRole |
| Roles | CRUD, Permission matrix management | RolesController | Role, RolePermission, Permission |
| Tenants | CRUD, Status, Branding, Business Info | TenantsController | Tenant |
| Files | Upload, Download (signed URLs), Delete, List | FilesController | FileMetadata |
| Reports | Request (async), Download, Delete, List | ReportsController | ReportRequest |
| Notifications | List, Mark read, Preferences | NotificationsController | Notification, NotificationPreference |
| Settings | List, Update (per-tenant overrides) | SettingsController | SystemSetting |
| Audit Logs | List with filters (read-only) | AuditLogsController | AuditLog |
| Permissions | List (read-only) | PermissionsController | Permission |
| API Keys | Create, Update, Revoke, Emergency Revoke | ApiKeysController | ApiKey |
| Feature Flags | CRUD, Tenant Overrides, Opt-Out, Enforcement | FeatureFlagsController | FeatureFlag, TenantFeatureFlag |
| Billing | Plans CRUD, Subscriptions, Usage, Payments, Change Plan | BillingController | SubscriptionPlan, TenantSubscription, PaymentRecord |
| Webhooks | CRUD, Test, Delivery Log, Secret Regeneration, Event Handlers | WebhooksController | WebhookEndpoint, WebhookDelivery |
| Import/Export | Import CSV, Export CSV/PDF, Templates, Preview, Async Processing | ImportExportController | ImportJob |

### Frontend (17 feature modules)

| Feature | Pages | Key Hooks |
|---------|-------|-----------|
| auth | Login, Register, RegisterTenant, ForgotPassword, VerifyEmail, AcceptInvite | useLogin, useRegister |
| dashboard | DashboardPage | useUsers, useRoles, useAuditLogs |
| users | UsersListPage, UserDetailPage | useSearchUsers, useUsers |
| roles | RolesListPage, RoleDetailPage, RoleCreatePage, RoleEditPage | useRoles, useRole |
| tenants | TenantsListPage, TenantDetailPage | useTenants, useTenant |
| files | FilesPage | useFiles, useUploadFile |
| reports | ReportsPage | useReports, useRequestReport |
| notifications | NotificationsPage | useNotifications |
| settings | SettingsPage | useSettings, useUpdateSettings |
| audit-logs | AuditLogsPage | useAuditLogs |
| profile | ProfilePage | useProfile, useLoginHistory |
| api-keys | ApiKeysPage | useApiKeys |
| feature-flags | FeatureFlagsPage | useFeatureFlags, useFeatureFlag |
| landing | LandingPage | — |
| billing | BillingPage, BillingPlansPage, PricingPage, SubscriptionsPage, SubscriptionDetailPage | useSubscription, usePlans, useAllSubscriptions |
| webhooks | WebhooksPage | useWebhookEndpoints, useWebhookDeliveries |
| import-export | ImportExportPage | useImportJobs, useEntityTypes |

## Backend Development Patterns

### Adding a New Feature (End-to-End)

1. **Domain** — Create entity in `Starter.Domain/{Feature}/Entities/` extending `AggregateRoot`. Add `Errors/` and `Enums/` as needed.
2. **Application** — Create `Starter.Application/Features/{Feature}/`:
   - `Commands/{Action}/{Action}Command.cs` — sealed record implementing `IRequest<Result<T>>`
   - `Commands/{Action}/{Action}CommandHandler.cs` — sealed class with primary constructor
   - `Commands/{Action}/{Action}CommandValidator.cs` — `AbstractValidator<T>` (optional)
   - `Queries/{Query}/{Query}Query.cs` + `{Query}QueryHandler.cs`
   - `DTOs/{Feature}Dto.cs`
3. **Infrastructure** — Add `DbSet` to `IApplicationDbContext` + `ApplicationDbContext`. Add EF config in `Persistence/Configurations/`. Add global tenant filter if multi-tenant.
4. **API** — Create controller inheriting `BaseApiController(ISender)`. Route: `api/v{version}/[controller]`. Use `[Authorize(Policy = Permissions.{Module}.{Action})]`.
5. **Permissions** — Add module to `Starter.Shared/Constants/Permissions.cs`. Map to roles in `Constants/Roles.cs`. Mirror in `boilerplateFE/src/constants/permissions.ts`.
6. **Seed** — Update `DataSeeder` if initial data needed.
7. **Migration** — `dotnet ef migrations add {Name} --project src/Starter.Infrastructure --startup-project src/Starter.Api`

### Naming Conventions

| Type | Pattern | Example |
|------|---------|---------|
| Command | `{Action}{Entity}Command` | `CreateApiKeyCommand` |
| Handler | `{Action}{Entity}CommandHandler` | `CreateApiKeyCommandHandler` |
| Validator | `{Action}{Entity}CommandValidator` | `CreateApiKeyCommandValidator` |
| Query | `Get{Entity}ByIdQuery` / `Get{Entities}Query` | `GetApiKeyByIdQuery` |
| DTO | `{Entity}Dto` | `ApiKeyDto` |
| Errors | `{Entity}Errors` (static class) | `ApiKeyErrors.NotFound` |
| Controller | `{Entities}Controller` (plural) | `ApiKeysController` |

### Multi-Tenancy Rules

- Entities with `TenantId` get a global query filter in `ApplicationDbContext.OnModelCreating()`
- Platform admins (`TenantId=null`) see all data; tenant users see only their tenant
- Use `.IgnoreQueryFilters()` when cross-tenant access is needed (e.g., uniqueness checks)
- Never expose `TenantId` in API responses — it's an internal concern

## Frontend Development Patterns

### Adding a New Feature (End-to-End)

1. **Feature folder** — `src/features/{feature}/api/`, `pages/`, `components/`, `index.ts`
2. **API** — Add endpoints to `src/config/api.config.ts`. Create `{feature}.api.ts` (typed API calls) + `{feature}.queries.ts` (TanStack Query hooks with `queryKeys`).
3. **Types** — Add to `src/types/{feature}.types.ts`
4. **Routes** — Add to `src/config/routes.config.ts` (paths) + `src/routes/routes.tsx` (lazy components with PermissionGuard)
5. **Navigation** — Add to Sidebar in `src/components/layout/MainLayout/Sidebar.tsx`
6. **Permissions** — Add to `src/constants/permissions.ts` (mirror from BE)

### API Hook Pattern

```ts
// api/{feature}.api.ts — raw API calls
export const featureApi = {
  getAll: (params?) => apiClient.get(API_ENDPOINTS.FEATURE.LIST, { params }),
  create: (data) => apiClient.post(API_ENDPOINTS.FEATURE.CREATE, data),
};

// api/{feature}.queries.ts — TanStack Query hooks
export function useFeatures(params) {
  return useQuery({ queryKey: queryKeys.feature.list(params), queryFn: () => featureApi.getAll(params) });
}
```

## Adding Import/Export to an Entity

The import/export system uses a **registry pattern** — define fields, create a data provider (export) and row processor (import), register in DI. The frontend ImportWizard is reusable via a single prop.

### Backend — 3 files to create, 1 to modify

**1. Definition** — `Application/Features/ImportExport/Definitions/{Entity}ImportExportDefinition.cs`
```csharp
public static EntityImportExportDefinition Create() =>
    new(
        EntityType: "Tenants",              // Must match frontend entityType prop
        DisplayNameKey: "importExport.entityTypes.tenants",
        SupportsExport: true,
        SupportsImport: true,
        ConflictKeys: ["Name"],             // Fields used for duplicate detection
        Fields: [
            new FieldDefinition("Name", "Name", FieldType.String, Required: true, MaxLength: 200),
            new FieldDefinition("Email", "Email", FieldType.Email, Required: true),
            new FieldDefinition("Status", "Status", FieldType.Enum, ExportOnly: true, EnumOptions: ["Active", "Suspended"]),
        ],
        ExportDataProviderType: typeof(TenantExportDataProvider),
        ImportRowProcessorType: typeof(TenantImportRowProcessor));
```

**2. Export Provider** — `{Entity}ExportDataProvider.cs` implements `IExportDataProvider`
- Query with `.IgnoreQueryFilters().AsNoTracking()`
- Filter by tenantId + optional filters from JSON
- Sanitize all string values: prefix `=@+-\t\r` chars with `'` (CSV injection prevention)
- Return `ExportDataResult(headers, rows, totalCount)`

**3. Import Processor** — `{Entity}ImportRowProcessor.cs` implements `IImportRowProcessor`
- Validate required fields, formats, lengths
- Check for existing entity by ConflictKey (tenant-scoped)
- `ConflictMode.Skip` → return `Skipped`
- `ConflictMode.Upsert` → update existing, return `Updated`
- Create new entity, return `Created`

**4. Register** — In `Infrastructure/DependencyInjection.cs` → `AddImportExportServices()`:
```csharp
registry.Register(TenantImportExportDefinition.Create());
services.AddScoped<TenantExportDataProvider>();
services.AddScoped<TenantImportRowProcessor>();
```

### Frontend — 1 file to modify

Add import button to any list page:
```tsx
import { ImportWizard } from '@/features/import-export/components/ImportWizard';

const canImport = hasPermission(PERMISSIONS.System.ImportData);
const [importOpen, setImportOpen] = useState(false);

// In PageHeader actions:
{canImport && (
  <Button variant="outline" onClick={() => setImportOpen(true)}>
    <Upload className="mr-2 h-4 w-4" />
    {t('feature.import')}
  </Button>
)}

// At bottom of component:
<ImportWizard open={importOpen} onOpenChange={setImportOpen} entityType="Tenants" />
```

The `entityType` prop must match the `EntityType` string in the backend definition. When passed, the wizard pre-selects and locks the entity type selector. SuperAdmin sees an optional tenant dropdown; tenant admin imports into their own tenant automatically.

## Environment Setup

### Docker Services (run `docker compose up -d` from `boilerplateBE/`)

| Service | Ports | Purpose |
|---------|-------|---------|
| PostgreSQL | 5432 | Primary database |
| Redis | 6379 | Distributed cache |
| RabbitMQ | 5672, 15672 | Message broker + management UI |
| Mailpit | 1025, 8025 | Dev SMTP server + email viewer |
| MinIO | 9000, 9001 | S3-compatible file storage + console |
| Jaeger | 16686, 4317, 4318 | Distributed tracing UI + OTLP |
| Prometheus | 9090 | Metrics collection |

### Key URLs (development)

| URL | Service |
|-----|---------|
| http://localhost:5000/swagger | API Swagger UI |
| http://localhost:3000 | Frontend |
| http://localhost:8025 | Mailpit (email viewer) |
| http://localhost:9001 | MinIO Console |
| http://localhost:16686 | Jaeger UI |
| http://localhost:9090 | Prometheus |
| http://localhost:15672 | RabbitMQ Management |

### Default Credentials

- **App SuperAdmin**: `superadmin@starter.com` / `Admin@123456`
- **MinIO**: `minioadmin` / `minioadmin`
- **RabbitMQ**: `guest` / `guest`
- **PostgreSQL**: `postgres` / `123456`

**Demo tenants** (all seeded active + email-confirmed; password `Admin@123456`):

| Tenant | Slug | Admin | Users |
|---|---|---|---|
| Acme Corporation | `acme` | `acme.admin@acme.com` | `acme.alice@acme.com`, `acme.bob@acme.com` |
| Globex Industries | `globex` | `globex.admin@globex.com` | `globex.hank@globex.com`, `globex.ivy@globex.com` |
| Initech Systems | `initech` | `initech.admin@initech.com` | `initech.milton@initech.com`, `initech.samir@initech.com` |

Usernames are the email local-part (e.g. `acme.admin`). Admin users have the `Admin` role; others have `User`.

## Frontend Rules — Must Always Follow

### Theme System

- **Never hardcode primary color shades** (`primary-600`, `primary-50`, etc.) in components. Use `bg-primary`, `text-primary`, or semantic tokens (`var(--active-bg)`, `var(--active-text)`, `state-active`, `state-hover`).
- **Never add `dark:` overrides for primary colors.** The theme preset system handles dark mode automatically via `useThemePreset`.
- **Active preset lives in** `src/config/theme.config.ts` → `activePreset`. Changing it rebrands the entire app.
- **Semantic tokens in CSS** (`--active-bg`, `--active-text`, `--active-border`, `--hover-bg`, `--gradient-from/to`) auto-derive from `--primary` using `color-mix()`. Components reference these, not raw shades.
- All Tailwind semantic colors (`bg-card`, `bg-popover`, `bg-background`, `text-foreground`, etc.) are registered in the `@theme` block of `index.css` AND set at runtime by `useThemePreset.ts`. Both must stay in sync.

### Components & Patterns

- **Use shared components** — `Pagination`, `PageHeader`, `EmptyState`, `UserAvatar`, `ConfirmDialog`, `ExportButton` from `@/components/common`.
- **Back navigation** — use `useBackNavigation(path, label)` hook in detail/edit pages. It renders in the header bar automatically and clears on unmount.
- **Page size persistence** — use `getPersistedPageSize()` from `@/components/common/Pagination` as the initial state for paginated lists. The `Pagination` component persists changes to localStorage.
- **Status badges** — use `STATUS_BADGE_VARIANT` from `@/constants/status` for mapping entity status to badge variants.
- **Tables** — the `Table` component includes its own `rounded-2xl bg-card shadow-card` container. Do NOT wrap it in an extra `<Card>`.
- **Empty states** — always use `<EmptyState>` component with an icon, title, and optional description/action.

### Styling Rules

- **Font** — IBM Plex Sans (loaded via Google Fonts in `index.html`). RTL uses IBM Plex Sans Arabic.
- **Radius convention** — sm=8px, md=12px, lg=16px. Cards use `rounded-2xl`, inputs/buttons `rounded-xl`, nav items `rounded-lg`.
- **No global `color` on typography tags** — the `p`, `h1`, etc. rules in `index.css` set size/weight only, not color. Use Tailwind classes.
- **RTL** — use `text-start` not `text-left`, `ltr:/rtl:` prefixes for directional borders/margins, `rtl:rotate-180` on arrow icons.
- **Buttons** — `variant="default"` is the primary action (copper fill), `variant="outline"` shows primary text, `variant="ghost"` shows primary tint on hover.

### Type Safety

- **No `as unknown as` casts** — extend the proper interface instead.
- **Shared types** live in `src/types/`. Extend them when the API returns new fields.

### Architecture

- **Feature-based structure** — each feature in `src/features/` has `api/`, `pages/`, `components/` subdirs.
- **State** — Zustand for client state (`src/stores/`), TanStack Query for server state.
- **Constants** — shared mappings (permissions, status variants, audit actions) go in `src/constants/`.
- **Hooks** — reusable logic in `src/hooks/` (permissions, back nav, theme preset, tenant branding, etc.).

## Mobile Development Patterns

### Architecture

```
Presentation (Cubit/Bloc) → Domain (UseCases + Repository ifaces) → Data (DTOs + Dio + RepoImpl) → Core (Dio, Hive, DI)
```

- **Cubit-first** — use `Cubit` for simple state, `Bloc` only when event streams help.
- **Result<T>** — all repos/use cases return `Result<T>` (sealed `Success`/`Err`). Never throw from domain/data layers.
- **One UseCase per action** — keeps cubits thin, business logic testable.

### Module System

- Optional modules live in `lib/modules/` and implement `AppModule` (DI, nav items, slots, permissions).
- `lib/app/modules.config.dart` is the single source of truth — edited by `rename.ps1` via markers.
- Modules provide `pageBuilder` in nav items so the shell never imports module code directly.
- Cross-module communication uses capability contracts + Null Object fallbacks.

### Adding a Mobile Feature

1. **Domain** — Create entity + repository interface + use cases in `lib/core/features/{name}/domain/`
2. **Data** — Create freezed DTOs, remote datasource (Dio), repository impl in `lib/core/features/{name}/data/`
3. **Presentation** — Create freezed state, Cubit, pages in `lib/core/features/{name}/presentation/`
4. **DI** — Register datasource, repository, use cases, cubit in `lib/core/di/injection.dart`
5. **Shell** — Add page rendering in `lib/app/shell/main_shell_page.dart`'s `_buildTab` switch
6. **Codegen** — Run `dart run build_runner build --delete-conflicting-outputs`

### Key Sync Points with BE/FE

- Permission strings: `lib/core/permissions/permissions.dart` must mirror `Starter.Shared/Constants/Permissions.cs`
- Theme: `lib/app/theme/app_colors.dart` mirrors FE active preset (manual sync)
- API response envelope: `lib/core/network/api_response.dart` matches BE `ApiResponse<T>`

## Post-Feature Testing Workflow

After completing any feature (backend + frontend builds pass), **always** run the testing workflow before requesting user review:

1. **Create test app** — Run `scripts/rename.ps1 -Name "_testFeatureName" -OutputDir "."` to create an isolated test instance in `_testFeatureName/` (gitignored)
2. **Drop old test DB** — `psql -U postgres -c "DROP DATABASE IF EXISTS _testfeaturenamedb;"`
3. **Reconfigure ports** — Backend → `5100`, Frontend → `3100`, update CORS + `.env` accordingly
4. **Install & build** — `dotnet build` (backend), `npm install` (frontend)
5. **Run** — `dotnet run` (backend), `npm run dev` (frontend). Services (mailpit, redis, minio) come from existing Docker containers
6. **Playwright tests** — Feature test (all CRUD for the new feature) + regression test (nav, users, roles, files, settings)
7. **Fix findings** — Fix in the worktree source, regenerate test app, re-test
8. **Leave running** — Report URLs to user for manual QA. Wait for confirmation before pushing

See `.claude/skills/post-feature-testing.md` for full details.

**Credentials:** Seed email becomes `superadmin@_testfeaturename.com` after rename, but Zod `.email()` rejects domains starting with `_`. Fix: update the test app's seed email to `superadmin@testfeaturename.com` (no underscore prefix) in `appsettings.Development.json` before first run, or login via Playwright with `keyboard.type()` + `requestSubmit()`.
**Ports:** Dev=5000/3000, Test=5100/3100

## Backend Notes

- .NET 10, PostgreSQL, Redis, RabbitMQ
- EF Core migrations in `Starter.Infrastructure/Persistence/Migrations/`
- Seed data applied on startup when `DatabaseSettings.SeedDataOnStartup = true`
- Default credentials: `superadmin@starter.com` / `Admin@123456`
- **OpenTelemetry** — Enabled via `OpenTelemetry:Enabled` in appsettings. Traces to Jaeger via OTLP at port 4318.
- **Serilog** — Structured logging to console + daily rolling file (`logs/`). Enriched with environment, machine, thread ID.
- **Rate limiting** — Global: 10/s, 100/m. Login: 5/m. Register: 10/h. Returns 429.
- **Health checks** — `/health` endpoint for all external services.
- **API versioning** — URL path (`/api/v1/`). Swagger per version.
- **CORS** — Explicit origin whitelist in `appsettings.Development.json` → `Cors:AllowedOrigins`.
