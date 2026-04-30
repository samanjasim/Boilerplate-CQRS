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

### Core vs. Module vs. Shared

- **Core feature** — required by other features or cross-cutting (access control, auth, audit, notifications, files). Lives in `Starter.Domain` / `Starter.Application` / `Starter.Infrastructure`; uses `ApplicationDbContext`.
- **Module** (`src/modules/Starter.Module.*`) — optional vertical with its own bounded context, DbContext, migrations, and DI module. Modules may depend on core; core must **not** depend on a module.
- **Shared** (`Starter.Shared`) — constants, permissions, error codes, enums with no behavior. No EF entities, no services.

When in doubt: if more than one module needs it, it's core.

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

### `[AiTool]` vs Controller Authorization

When a query is exposed both as an HTTP controller endpoint and as an AI tool via `[AiTool]`, the two paths share one handler. The `[AiTool]` `RequiredPermission` is the gate that LLM dispatchers check before calling the handler — it must be **at least as restrictive** as every controller `[Authorize]` policy that dispatches the same query, *and* the handler must be tenant-scoped via `currentUser.TenantId` or EF query filters. Specifically:

- If the handler calls `IgnoreQueryFilters()` *and* never filters by `currentUser.TenantId`, it's a SuperAdmin admin-list — do **not** decorate it with `[AiTool]` under a `View`-level permission. Either tighten the permission to a SuperAdmin-only policy (e.g. `BillingPermissions.ManageTenantSubscriptions`) or expose a separate tenant-scoped query for agents instead.
- Server-trusted parameters (`TenantId`, `UserId`, role flags) on `[AiTool]`-decorated records must carry `[property: AiParameterIgnore]`. The `AiToolSchemaGenerator.EnforceTrustBoundary` check throws at startup if you forget.
- The acid test at `Plan5eAcidTests.Server_trusted_tool_parameters_carry_AiParameterIgnore` enumerates the explicit list — extend it whenever a new server-trusted field surfaces on an `[AiTool]` query.

### Integration Events & Messaging

Cross-module events are published via the **transactional outbox pattern**. Events are committed atomically with business data, then delivered asynchronously by MassTransit. Full reference: [docs/architecture/cross-module-communication.md § Pattern 2](docs/architecture/cross-module-communication.md).

**Publishing from a command handler — the only correct way:**

```csharp
internal sealed class RegisterTenantCommandHandler(
    IApplicationDbContext context,
    IIntegrationEventCollector eventCollector)  // ← NOT IPublishEndpoint
    : IRequestHandler<RegisterTenantCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(RegisterTenantCommand cmd, CancellationToken ct)
    {
        context.Tenants.Add(tenant);
        eventCollector.Schedule(new TenantRegisteredEvent(tenant.Id, ...));
        await context.SaveChangesAsync(ct);   // event row commits atomically
        return Result.Success(tenant.Id);
    }
}
```

**Never inject `IPublishEndpoint` in a MediatR handler.** With two `AddEntityFrameworkOutbox<T>` registrations, `IPublishEndpoint` resolves to the last-registered DbContext's provider — which isn't saved by the handler — and the event disappears silently. An architecture test (`MessagingArchitectureTests`) fails the build if anyone tries. Inside a MassTransit consumer, `IPublishEndpoint` is fine.

**Consumer rules:**

- Always implement a domain-uniqueness idempotency check at the top (`AnyAsync(e => e.TenantId == evt.TenantId)`) and `return` if the row already exists. At-least-once delivery is the guarantee.
- **Throw on transient failures** (DB unreachable, 5xx dependency). The default retry policy (3 attempts at 1 s / 5 s / 15 s) will fire, and exhausted messages go to the `_error` queue automatically.
- **Return quietly** on non-retryable business conditions (unknown tenant, feature off) and on idempotency hits.
- Events automatically carry a `ConversationId` derived from the originating HTTP request's `Activity.TraceId` — all events from one request share it for log/trace grouping.

**Event schema evolution:** additive-only. Renames or type changes → create `MyEventV2` alongside the original and migrate consumers gradually.

**Operational monitoring:** the `outbox-delivery-lag` check at `/health` reports `Degraded` (never `Unhealthy`) when the outbox backlog exceeds `Outbox:HealthCheck:MaxPendingRows` (default 1000) or `MaxOldestAge` (default 5 min). Liveness probes must not restart the pod on this signal.

**Emails and other external side effects:** same rule as events — schedule `SendEmailRequestedEvent` (render the template synchronously in the handler, schedule the pre-rendered `EmailMessage`) instead of calling `IEmailService.SendAsync` inline after `SaveChangesAsync`. A transient SMTP failure must not leave a registered user without a verification email. `EmailDispatchConsumer` handles the SMTP call with full retry + DLQ.

**Log correlation in consumers:** `LogContextEnrichmentFilter` pushes `ConversationId`, `MessageId`, and `MessageType` into the `ILogger` scope for every consumer's lifetime. Grep logs by `ConversationId` to trace one HTTP request across every downstream consumer it triggered.

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

- **Never hardcode primary color shades** (`primary-600`, `primary-50`, etc.) in components. Use `bg-primary`, `text-primary`, or semantic tokens (`var(--active-bg)`, `var(--active-text)`, `var(--tinted-fg)`, `state-active`, `state-hover`).
- **Never add `dark:` overrides for primary colors.** The theme preset system handles dark mode automatically via `useThemePreset`. For text on faintly-tinted backgrounds (mono tags, code chips), use `text-[var(--tinted-fg)]` — the semantic token swaps light↔dark automatically.
- **Active preset lives in** `src/config/theme.config.ts` → `activePreset`. Changing it rebrands the entire app — including the J4 aurora, gradient buttons, and glow halos (all preset-aware via `color-mix()` + CSS vars).
- **Semantic tokens in CSS** (`--active-bg`, `--active-text`, `--active-border`, `--hover-bg`, `--gradient-from/to`, `--tinted-fg`, `--surface-glass`, `--border-strong`, `--aurora-1/2/3`, `--spectrum-text`, `--btn-primary-gradient`, `--glow-primary-{sm,md,lg}`) auto-derive from `--primary` using `color-mix()`. Components reference these, not raw shades.
- **J4 Spectrum companion scales** — `--color-violet-{50..950}` and `--color-amber-{50..950}` are registered in `@theme` and runtime-written by `useThemePreset` (with global fallbacks). Used for the spectrum gradient + status pills.
- All Tailwind semantic colors (`bg-card`, `bg-popover`, `bg-background`, `text-foreground`, etc.) are registered in the `@theme` block of `index.css` AND set at runtime by `useThemePreset.ts`. Both must stay in sync.

### Components & Patterns

- **MANDATORY: Use shared components** — `Pagination`, `PageHeader`, `EmptyState`, `UserAvatar`, `ConfirmDialog`, `ExportButton` from `@/components/common`. **NEVER create custom versions** of these. Every list page with a `<Table>` MUST include `<Pagination>` from the shared component. Every page MUST use `<PageHeader>`. Every empty data state MUST use `<EmptyState>`.
- **MANDATORY: No component duplication** — Before creating any new component, check if a shared component or pattern already exists in `@/components/common`, `@/components/ui`, `@/constants`, or `@/hooks`. Duplicate components are a code smell and must be avoided. When multiple features need the same UI pattern, extract it to `@/components/common`.
- **API Response Envelope** — All FE feature code uses the typed `api` namespace from `@/lib/api` — never raw `apiClient`. Helpers return `T` (or `{ items, pagination }` for paged endpoints) and throw `ApiError` on failure. Components and queries never see `ApiResponse<T>`; the `data.data` chain is forbidden by ESLint inside migrated features. Migration is per-feature; check the `migratedFeatureGlobs` array in `eslint.config.js` for the slice schedule. See [docs/superpowers/specs/2026-04-30-fe-api-envelope-cleanup-design.md](docs/superpowers/specs/2026-04-30-fe-api-envelope-cleanup-design.md).
- **API Errors** — `api.*` helpers throw `ApiError` (`status`, `code`, `validationErrors`, `cause`) on HTTP failure or envelope `success: false`. Use `e instanceof ApiError && e.code === '...'` for typed handlers. The error toast already fired (HTTP errors via the existing axios interceptor; envelope errors via the helper) — don't toast again in the catch.
- **DTO Sync** — When adding a field to a backend DTO record in `Starter.Abstractions`, ALWAYS add it to the matching TypeScript interface in `src/types/`. Missing fields silently render as `undefined`.
- **Entity Display Names** — Never show raw GUIDs to users. All entities in lists/cards must show human-readable names. Pass `entityDisplayName` when creating records that will be displayed.
- **Back navigation** — use `useBackNavigation(path, label)` hook in detail/edit pages. It renders in the header bar automatically and clears on unmount.
- **Page size persistence** — use `getPersistedPageSize()` from `@/components/common/Pagination` as the initial state for paginated lists. The `Pagination` component persists changes to localStorage.
- **Status badges** — use `STATUS_BADGE_VARIANT` from `@/constants/status` for mapping entity status to badge variants. Do NOT define local status-to-variant mappings in page components. The `Badge` component now also exposes J4 status variants (`healthy`, `pending`, `failed`, `info`) directly — use those for new statuses.
- **Tables** — the `Table` component includes its own `surface-glass` container with copper-tinted header + eyebrow column labels. Do NOT wrap it in an extra `<Card>`.
- **Cards** — `Card` accepts a `variant` prop: `solid` (default, backwards-compatible), `glass` (translucent over aurora), `elevated` (lift on hover). Reach for `glass` on landing/marketing surfaces and `elevated` for clickable list cards.
- **Empty states** — always use `<EmptyState>` component with an icon, title, and optional description/action. The icon now renders in a copper-tinted glass tile with a subtle glow.
- **J4 utilities** — `.aurora-canvas`, `.gradient-text`, `.surface-glass`, `.surface-glass-strong`, `.glow-primary-{sm,md,lg}`, `.btn-primary-gradient`, `.pulse-dot`, `.hero-pulse`, `.blueprint-line`, `.reveal-up`, `.reveal-stagger`, `.reveal-snap`, `.code-typing`, `.caret-blink`, `.feature-check`, `.brand-halo`, `.spark-shimmer`. Defined in `src/styles/index.css`. Live reference at `/styleguide` (dev-only).

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

1. **Check free ports** — `lsof -iTCP -sTCP:LISTEN -nP | awk '{print $9}' | grep -oE '[0-9]+$' | sort -un`. Use 5100/3100 or 5200/3200 or 5300/3300.
2. **Create test app** — `scripts/rename.ps1 -Name "_testFeatureName" -OutputDir "." -Modules "All" -IncludeMobile:$false`
3. **Reconfigure** — Fix seed email (remove `_` prefix from domain), fix bucket name, update ports in launchSettings.json, update CORS + FrontendUrl + BaseUrl to test port, create `.env` for frontend, increase rate limits 10x for testing.
4. **Generate ALL migrations** — There are currently 8 DbContexts (Core + 7 modules). Generate a migration for each: `dotnet ef migrations add Init --project <project> --startup-project <api> --context <context> --no-build`
5. **Build + Start** — `dotnet build` → `dotnet run` (backend), `npm install` → `npx vite --port <port>` (frontend)
6. **Setup Communication** — Create Acme tenant's SMTP email channel via API pointing to Mailpit (localhost:1025)
7. **Seed test data** — Create realistic test workflows/entities with proper display names (never use empty GUIDs)
8. **Live test via Playwright MCP** — Every feature must be visually verified in the browser. The user must SEE it working. API-only verification is not sufficient.
9. **Test ALL user roles** — Login as admin, regular user, and different users to verify scoping, permissions, delegation visibility
10. **Fix findings** — Fix in the worktree source. For FE-only changes, copy files to test app for hot reload. For BE changes, regenerate test app.
11. **Leave running** — Report URLs to user for manual QA. Do NOT clean up until user explicitly says so.

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

## Running the RAG eval harness

The AI module ships with an offline evaluation harness covering retrieval quality, stage latency, and faithfulness.

**When to run it:** Before merging any change to the retrieval pipeline (`RagRetrievalService`, chunking, embedding, reranker, vector store). A nightly Jenkins job also runs it against main.

### Prerequisites

- **Postgres** — Qdrant reads from Docker (`starter-qdrant`), but the eval fixture prefers a locally-installed Postgres. Set `STARTER_TEST_PG_CONN` to a connection string *without* a database name — the fixture creates and drops a per-run DB (`starter_test_<guid>`). Falls back to Testcontainers when unset.
- **Qdrant** — `docker compose up -d qdrant` (listens on `localhost:6333`).
- **Provider keys** — stored as dotnet-user-secrets under the Starter.Api `UserSecretsId` (`28025670-6752-4ada-a756-c41ce763661d`). The harness auto-loads them via `.AddUserSecrets<Starter.Api.Program>()`. Currently configured providers are OpenAI (embeddings) and Anthropic (chat + reranker). **No Ollama.**
  - Verify they're populated with `dotnet user-secrets list --project boilerplateBE/src/Starter.Api`.
  - The EvalCacheWarmup tool and `RagEvalFixture` both pull from this same store — no need to export env vars.

### Run the harness

```bash
AI_EVAL_ENABLED=1 \
STARTER_TEST_PG_CONN="Host=localhost;Port=5432;Username=<you>" \
  dotnet test boilerplateBE/Starter.sln \
  --filter "FullyQualifiedName~RagEvalHarnessTests"
```

Without `AI_EVAL_ENABLED=1` the test emits a skip reason via `ITestOutputHelper` and returns early.

### Update the baseline

Intentional improvements to the pipeline (new reranker, better chunking, etc.) should bump the baseline:

```bash
AI_EVAL_ENABLED=1 UPDATE_EVAL_BASELINE=1 \
STARTER_TEST_PG_CONN="Host=localhost;Port=5432;Username=<you>" \
  dotnet test boilerplateBE/Starter.sln \
  --filter "FullyQualifiedName~RagEvalHarnessTests"
```

This writes to the copy under `bin/Debug/net10.0/Ai/Eval/fixtures/rag-eval-baseline.json`. Copy it back to the source tree before committing:

```bash
cp boilerplateBE/tests/Starter.Api.Tests/bin/Debug/net10.0/Ai/Eval/fixtures/rag-eval-baseline.json \
   boilerplateBE/tests/Starter.Api.Tests/Ai/Eval/fixtures/rag-eval-baseline.json
```

### Regenerate the rerank cache blobs

Required when adding new questions to a fixture or when provider model IDs change. The blobs let offline eval runs hit a warm cache rather than calling the reranker live, which is what makes the harness deterministic.

```bash
# Create a throwaway Postgres DB (schema is built via EnsureCreated)
createdb starter_eval_warmup

# EN
ConnectionStrings__DefaultConnection="Host=localhost;Port=5432;Database=starter_eval_warmup;Username=<you>" \
  dotnet run --project boilerplateBE/tools/EvalCacheWarmup -- \
    --fixture boilerplateBE/tests/Starter.Api.Tests/Ai/Eval/fixtures/rag-eval-dataset-en.json \
    --out     boilerplateBE/tests/Starter.Api.Tests/Ai/Eval/fixtures/eval-rerank-cache-en.json

# AR (drop + recreate first so each run starts clean)
dropdb starter_eval_warmup && createdb starter_eval_warmup
ConnectionStrings__DefaultConnection="Host=localhost;Port=5432;Database=starter_eval_warmup;Username=<you>" \
  dotnet run --project boilerplateBE/tools/EvalCacheWarmup -- \
    --fixture boilerplateBE/tests/Starter.Api.Tests/Ai/Eval/fixtures/rag-eval-dataset-ar.json \
    --out     boilerplateBE/tests/Starter.Api.Tests/Ai/Eval/fixtures/eval-rerank-cache-ar.json

dropdb starter_eval_warmup
```

Commit the updated blobs alongside the fixture/model change.

### Faithfulness spot-check (superadmin)

Single-shot JSON response (fine for small datasets):

```bash
curl -H "Authorization: Bearer <superadmin-token>" \
  -F datasetName=en -F assistantId=<assistant-guid> \
  http://localhost:5000/api/v1/ai/eval/faithfulness
```

SSE streaming (recommended for >20-question datasets):

```bash
curl -N -H "Authorization: Bearer <superadmin-token>" \
  -F datasetName=en -F assistantId=<assistant-guid> \
  http://localhost:5000/api/v1/ai/eval/faithfulness/stream
```

Emits `run_started`, one `question_completed` per question, then a terminal `run_completed` with the full `FaithfulnessReport`. Only superadmins (`Ai.RunEval`) can invoke either endpoint.

### Orphan Qdrant collections

The harness names its synthetic tenants with v7 (time-ordered) GUIDs, so `RagEvalFixture.InitializeAsync` can safely reap any `tenant_{v7-guid}` collection older than 24 h without touching real tenants (which use v4 GUIDs). If a run crashes, the next eval-test invocation will clean up after it.
