# Module Architecture Design

## Context

The boilerplate currently ships as a monolith where all 12 features are tightly coupled — removing one requires editing DbContext, DI registration, permissions, seeding, routes, and sidebar across multiple files. This is the #1 blocker for selling to small agencies/dev shops, who need to pick only the features their project requires and leave the rest out.

This spec designs a module system that makes features self-contained and removable, so agencies can start a new project with only what they need.

### Goals

1. **Plugin modularity** — features as self-contained modules that can be added/removed without touching core code
2. **Sellable boilerplate** — agencies buy the core, choose which modules to include per project
3. **Future-proof** — modules can later be distributed as NuGet packages (compiled DLLs) or extracted to microservices

### Non-Goals (deferred)

- NuGet packaging and distribution (future — once module boundaries exist)
- Code protection / obfuscation (future — depends on NuGet distribution)
- Frontend plugin framework (unnecessary — conditional rendering + folder deletion is sufficient)
- Runtime module loading/unloading (modules are determined at build time)

---

## Core vs. Module Split

### Core (always present, cannot be removed)

| Feature | Why it's core |
|---------|--------------|
| Auth (login, register, JWT, 2FA, sessions) | Every app needs authentication |
| Users (CRUD, activate/suspend) | Every app has users |
| Roles & Permissions | Every app needs authorization |
| Tenants | Multi-tenancy is the boilerplate's key differentiator |
| Settings (system settings per tenant) | Every app needs configuration |
| Dashboard (basic) | Landing page after login |

### Modules (optional, add/remove per project)

| Module | Contents |
|--------|----------|
| Files | Upload, download, signed URLs, MinIO/S3 integration |
| Reports | Async report generation, PDF/CSV export |
| Notifications | In-app notifications, email preferences, Ably real-time |
| Audit Logs | Entity change tracking, filtered viewer |
| API Keys | Tenant/platform key management |
| Feature Flags | Flag CRUD, tenant overrides, enforcement |
| Billing | Subscription plans, payments, usage tracking |

---

## Module Interface

A new project `Starter.Module.Abstractions` holds all module contracts. This is the only dependency a module needs on the core.

### IModule

```csharp
// src/Starter.Module.Abstractions/IModule.cs
namespace Starter.Module.Abstractions;

public interface IModule
{
    /// Unique module identifier, e.g. "Starter.Module.Files"
    string Name { get; }

    /// Human-readable name, e.g. "File Management"
    string DisplayName { get; }

    /// Semver string, e.g. "1.0.0"
    string Version { get; }

    /// Module names this depends on, e.g. ["Starter.Core"]. Used for ordering.
    IReadOnlyList<string> Dependencies { get; }

    /// Register module-specific services (storage providers, custom services, etc.)
    /// MediatR handlers, validators, and EF configs are auto-discovered from the assembly.
    IServiceCollection ConfigureServices(IServiceCollection services, IConfiguration configuration);

    /// Declare permissions this module contributes. Seeded on startup.
    IEnumerable<(string Name, string Description, string Module)> GetPermissions();

    /// Declare default role-permission mappings for this module's permissions.
    IEnumerable<(string Role, string[] Permissions)> GetDefaultRolePermissions();

    /// Seed module-specific data (feature flags, default settings, etc.)
    Task SeedDataAsync(IServiceProvider services);
}
```

### ITenantEntity

```csharp
// src/Starter.Module.Abstractions/ITenantEntity.cs
namespace Starter.Module.Abstractions;

/// Marker interface for entities that support tenant isolation.
/// Entities implementing this get automatic EF Core query filters.
public interface ITenantEntity
{
    Guid? TenantId { get; }
}
```

### BaseApiController

Moved from `Starter.Api` to `Starter.Module.Abstractions` so module controllers can inherit it without referencing the API project. Contains `HandleResult()`, `HandlePagedResult()`, and base route/versioning attributes.

### ModuleLoader

```csharp
// src/Starter.Module.Abstractions/ModuleLoader.cs
namespace Starter.Module.Abstractions;

public static class ModuleLoader
{
    /// Scan all loaded assemblies for IModule implementations, instantiate them.
    public static IReadOnlyList<IModule> DiscoverModules();

    /// Topological sort based on Dependencies. Throws on circular dependency.
    public static IReadOnlyList<IModule> ResolveOrder(IReadOnlyList<IModule> modules);
}
```

---

## Module Project Structure

Each module is a single .csproj with internal folder structure mirroring the layers. One project = one assembly = one future NuGet package.

```
src/modules/Starter.Module.Files/
├── Starter.Module.Files.csproj     ← references Starter.Module.Abstractions
├── FilesModule.cs                  ← implements IModule
├── Domain/
│   ├── Entities/FileMetadata.cs
│   └── Errors/FileErrors.cs
├── Application/
│   ├── Commands/UploadFile/
│   │   ├── UploadFileCommand.cs
│   │   ├── UploadFileCommandHandler.cs
│   │   └── UploadFileCommandValidator.cs
│   ├── Queries/GetFiles/
│   │   ├── GetFilesQuery.cs
│   │   └── GetFilesQueryHandler.cs
│   └── DTOs/FileDto.cs
├── Infrastructure/
│   ├── Configurations/FileMetadataConfiguration.cs
│   └── Services/StorageService.cs
├── Controllers/
│   └── FilesController.cs
└── Constants/
    └── FilePermissions.cs
```

### Module .csproj dependencies

```xml
<ItemGroup>
  <ProjectReference Include="../../Starter.Module.Abstractions/Starter.Module.Abstractions.csproj" />
</ItemGroup>
<ItemGroup>
  <!-- Framework references for controllers, EF Core, MediatR, FluentValidation -->
  <FrameworkReference Include="Microsoft.AspNetCore.App" />
  <PackageReference Include="Microsoft.EntityFrameworkCore" />
  <PackageReference Include="MediatR" />
  <PackageReference Include="FluentValidation" />
</ItemGroup>
```

### Module implementation example

```csharp
// src/modules/Starter.Module.Files/FilesModule.cs
public class FilesModule : IModule
{
    public string Name => "Starter.Module.Files";
    public string DisplayName => "File Management";
    public string Version => "1.0.0";
    public IReadOnlyList<string> Dependencies => ["Starter.Core"];

    public IServiceCollection ConfigureServices(IServiceCollection services, IConfiguration config)
    {
        services.AddScoped<IStorageService, MinioStorageService>();
        return services;
    }

    public IEnumerable<(string Name, string Description, string Module)> GetPermissions()
    {
        yield return (FilePermissions.View, "View and download files", "Files");
        yield return (FilePermissions.Upload, "Upload new files", "Files");
        yield return (FilePermissions.Delete, "Delete files", "Files");
        yield return (FilePermissions.Manage, "Manage file metadata", "Files");
    }

    public IEnumerable<(string Role, string[] Permissions)> GetDefaultRolePermissions()
    {
        yield return ("SuperAdmin", [FilePermissions.View, FilePermissions.Upload,
                                     FilePermissions.Delete, FilePermissions.Manage]);
        yield return ("Admin", [FilePermissions.View, FilePermissions.Upload, FilePermissions.Delete]);
        yield return ("User", [FilePermissions.View]);
    }

    public async Task SeedDataAsync(IServiceProvider services)
    {
        // Seed feature flags, default settings for this module
    }
}
```

---

## DbContext Changes

### IApplicationDbContext — slim to core only

```csharp
// src/Starter.Application/Common/Interfaces/IApplicationDbContext.cs
public interface IApplicationDbContext
{
    // Core entities — always present
    DbSet<User> Users { get; }
    DbSet<Role> Roles { get; }
    DbSet<Permission> Permissions { get; }
    DbSet<UserRole> UserRoles { get; }
    DbSet<RolePermission> RolePermissions { get; }
    DbSet<Tenant> Tenants { get; }
    DbSet<AuditLog> AuditLogs { get; }
    DbSet<Invitation> Invitations { get; }
    DbSet<Session> Sessions { get; }
    DbSet<LoginHistory> LoginHistory { get; }
    DbSet<SystemSetting> SystemSettings { get; }

    // Generic accessor — modules use this instead of named properties
    DbSet<TEntity> Set<TEntity>() where TEntity : class;

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    Task<T> ExecuteInTransactionAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        IsolationLevel isolationLevel,
        CancellationToken cancellationToken = default);
}
```

Module handlers use `_context.Set<FileMetadata>()` instead of `_context.FileMetadata`.

### ApplicationDbContext.OnModelCreating — multi-assembly scanning

```csharp
public sealed class ApplicationDbContext : DbContext, IApplicationDbContext
{
    private readonly ICurrentUserService? _currentUserService;
    private readonly IReadOnlyList<Assembly> _moduleAssemblies;

    public ApplicationDbContext(
        DbContextOptions<ApplicationDbContext> options,
        ICurrentUserService? currentUserService = null,
        IModuleAssemblyProvider? moduleAssemblyProvider = null)
        : base(options)
    {
        _currentUserService = currentUserService;
        _moduleAssemblies = moduleAssemblyProvider?.GetModuleAssemblies() ?? [];
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Core entity configurations
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());

        // Module entity configurations — auto-discovers IEntityTypeConfiguration<T>
        foreach (var assembly in _moduleAssemblies)
            modelBuilder.ApplyConfigurationsFromAssembly(assembly);

        // Convention-based tenant filters (replaces 12 hardcoded blocks)
        ApplyTenantFilters(modelBuilder);
    }

    private void ApplyTenantFilters(ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (!typeof(ITenantEntity).IsAssignableFrom(entityType.ClrType))
                continue;

            // Build expression: entity => TenantId == null || entity.TenantId == TenantId
            var parameter = Expression.Parameter(entityType.ClrType, "e");
            var tenantIdProp = Expression.Property(parameter, nameof(ITenantEntity.TenantId));
            var currentTenantId = Expression.Property(Expression.Constant(this), nameof(TenantId));
            var filter = Expression.Lambda(
                Expression.OrElse(
                    Expression.Equal(currentTenantId, Expression.Constant(null, typeof(Guid?))),
                    Expression.Equal(tenantIdProp, currentTenantId)),
                parameter);

            modelBuilder.Entity(entityType.ClrType).HasQueryFilter(filter);
        }
    }
}
```

### IModuleAssemblyProvider

```csharp
// src/Starter.Module.Abstractions/IModuleAssemblyProvider.cs
public interface IModuleAssemblyProvider
{
    IReadOnlyList<Assembly> GetModuleAssemblies();
}

// Simple implementation registered in Program.cs
public class ModuleAssemblyProvider(IReadOnlyList<Assembly> assemblies) : IModuleAssemblyProvider
{
    public IReadOnlyList<Assembly> GetModuleAssemblies() => assemblies;
}
```

### Migrations

Modules share the single `ApplicationDbContext`. When a module is added (via ProjectReference or NuGet PackageReference), its `IEntityTypeConfiguration<T>` classes become visible to EF Core. Running `dotnet ef migrations add AddFilesModule` generates the correct migration. No separate migration contexts needed.

### Special tenant filter patterns

Some entities need non-standard filter logic (e.g., Roles should be visible if `TenantId == null` OR matches current tenant). These entities declare their own query filter in their `IEntityTypeConfiguration`. The convention-based `ApplyTenantFilters` method must check whether the entity already has a query filter defined (via `entityType.GetQueryFilter() != null`) and skip it if so. EF Core applies the last `HasQueryFilter` call, so the convention must run *after* `ApplyConfigurationsFromAssembly` and must not overwrite existing filters.

Entities that need the standard `TenantId == null || entity.TenantId == TenantId` filter simply implement `ITenantEntity` and do NOT define their own query filter in their configuration — the convention handles them.

---

## Permissions

### Core permissions stay static

`Starter.Shared/Constants/Permissions.cs` is slimmed down to core-only permissions: Users, Roles, System, Tenants. These remain compile-time constants for `[Authorize]` attributes.

### Module permissions

Each module declares its own static constants class and provides permissions via `IModule.GetPermissions()`:

```csharp
// src/modules/Starter.Module.Files/Constants/FilePermissions.cs
public static class FilePermissions
{
    public const string View = "Files.View";
    public const string Upload = "Files.Upload";
    public const string Delete = "Files.Delete";
    public const string Manage = "Files.Manage";
}
```

Used in module controllers: `[Authorize(Policy = FilePermissions.Upload)]`

### Seeding

`DataSeeder.SeedPermissionsAsync()` changes from calling `Permissions.GetAll()` to aggregating from core + all modules:

```csharp
var allPermissions = Permissions.GetAllWithMetadata()
    .Concat(modules.SelectMany(m => m.GetPermissions()));
```

Role-permission mappings aggregate similarly:

```csharp
var allRoleMappings = Roles.GetRolePermissions()
    .Concat(modules.SelectMany(m => m.GetDefaultRolePermissions()));
```

### Authorization handler

The existing `PermissionAuthorizationPolicyProvider` already handles any `"X.Y"` string dynamically — zero changes needed.

---

## Program.cs Composition Root

```csharp
// src/Starter.Api/Program.cs

// ... Serilog setup unchanged ...

builder.Services.AddHttpContextAccessor();

// 1. Discover and resolve modules
var modules = ModuleLoader.DiscoverModules();
var orderedModules = ModuleLoader.ResolveOrder(modules);
var moduleAssemblies = orderedModules.Select(m => m.GetType().Assembly).Distinct().ToList();

// 2. Register module assembly provider (used by DbContext)
builder.Services.AddSingleton<IModuleAssemblyProvider>(new ModuleAssemblyProvider(moduleAssemblies));

// 3. Core layers — now accept module assemblies
builder.Services.AddApplication(moduleAssemblies);
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddIdentityInfrastructure(builder.Configuration);

// 4. Module-specific services
foreach (var module in orderedModules)
    module.ConfigureServices(builder.Services, builder.Configuration);

// 5. Controllers — discover module controllers
var mvcBuilder = builder.Services.AddControllers();
foreach (var asm in moduleAssemblies)
    mvcBuilder.AddApplicationPart(asm);

// ... rest unchanged (Swagger, CORS, rate limiting, etc.) ...

// 6. Module seeding (after core seeding)
foreach (var module in orderedModules)
    await module.SeedDataAsync(app.Services);
```

### AddApplication changes

```csharp
// src/Starter.Application/DependencyInjection.cs
public static IServiceCollection AddApplication(
    this IServiceCollection services,
    IReadOnlyList<Assembly>? moduleAssemblies = null)
{
    var assemblies = new List<Assembly> { Assembly.GetExecutingAssembly() };
    if (moduleAssemblies != null)
        assemblies.AddRange(moduleAssemblies);

    services.AddMediatR(config =>
    {
        foreach (var assembly in assemblies)
            config.RegisterServicesFromAssembly(assembly);

        config.AddBehavior(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
        config.AddBehavior(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
        config.AddBehavior(typeof(IPipelineBehavior<,>), typeof(TracingBehavior<,>));
        config.AddBehavior(typeof(IPipelineBehavior<,>), typeof(PerformanceBehavior<,>));
    });

    foreach (var assembly in assemblies)
        services.AddValidatorsFromAssembly(assembly);

    return services;
}
```

---

## Frontend

### No plugin framework — conditional rendering + folder deletion

Frontend features in `src/features/` are already self-contained. The module system uses two mechanisms:

### Build-time: modules.ts registry

```typescript
// src/modules.ts
export const activeModules = {
  files: true,
  reports: true,
  notifications: true,
  auditLogs: true,
  apiKeys: true,
  featureFlags: true,
  billing: false,
} as const;

export type ModuleName = keyof typeof activeModules;
```

### Routes use the registry

```typescript
// src/routes/routes.tsx
import { activeModules } from '@/modules';

const routes = [
  ...coreRoutes,
  ...(activeModules.files ? fileRoutes : []),
  ...(activeModules.reports ? reportRoutes : []),
  ...(activeModules.billing ? billingRoutes : []),
  // etc.
];
```

### Sidebar uses the registry

```typescript
{activeModules.files && <SidebarItem icon={FileIcon} label="Files" path="/files" />}
{activeModules.billing && <SidebarItem icon={CreditCardIcon} label="Billing" path="/billing" />}
```

### Runtime: feature flags (orthogonal)

`modules.ts` answers: "is this feature in the codebase?" (build-time, per project).
`useFeatureFlag('billing.enabled')` answers: "is this feature enabled for this tenant?" (runtime, per tenant).

Both coexist. A project with billing included uses feature flags to control which tenants get it.

### Module permissions on frontend

Each feature folder exports its own permissions:

```typescript
// src/features/files/constants/permissions.ts
export const FilePermissions = {
  View: 'Files.View',
  Upload: 'Files.Upload',
  Delete: 'Files.Delete',
  Manage: 'Files.Manage',
} as const;
```

---

## CLI Tool — `starter`

A .NET CLI tool (`dotnet tool`) that serves as the single entry point for all boilerplate operations. Distributed via NuGet, uses Spectre.Console for rich interactive UI.

### Installation

```bash
dotnet tool install --global Starter.Cli
```

### Commands

```bash
starter new <name> [--output <dir>] [--modules <list>]   # Create new project
starter module add <name> [--source <path>]               # Add module to existing project
starter module remove <name>                              # Remove module from project
starter module list                                       # Show available/installed modules
starter info                                              # Show project info (name, active modules, versions)
```

### `starter new` — Create project

Replaces the current `rename.ps1`. Same logic (copy, rename, clean artifacts) but with interactive module selection via Spectre.Console multi-select checkboxes.

```bash
# Interactive (default) — shows checkbox UI for module selection:
starter new MyApp

# Explicit — skip interactive prompt:
starter new MyApp --modules Files,Billing

# Core only:
starter new MyApp --modules None

# Custom output directory:
starter new MyApp --output C:\Projects
```

**Interactive flow:**

```
Creating project 'MyApp'...

Select modules to include:
  [x] File Management      — Upload, download, S3/MinIO storage
  [x] Reports              — Async CSV/PDF report generation
  [ ] Notifications        — In-app notifications, email preferences, Ably real-time
  [x] Audit Logs           — Entity change tracking, filtered viewer
  [ ] API Keys             — Tenant/platform API key management
  [ ] Feature Flags        — Flag CRUD, tenant overrides, enforcement
  [ ] Billing              — Subscription plans, payments, usage tracking

  Press <space> to toggle, <enter> to confirm

✓ Project 'MyApp' created at C:\Projects\MyApp
  Included modules: Files, Reports, AuditLogs

Next steps:
  cd MyApp/MyApp-BE && dotnet build
  cd MyApp/MyApp-FE && npm install && npm run dev
```

**Internally, `starter new` does:**

1. Copy boilerplate source to target directory
2. Clean build artifacts (bin, obj, node_modules, etc.)
3. Run rename logic (replace `Starter` → project name in all files, rename files/dirs)
4. For each excluded module: run the module removal logic (same as `starter module remove`)
5. Copy `scripts/modules.json` into the generated project (tracks what's available)
6. Verify no leftover `Starter` references

### `starter module add` — Add module post-creation

```bash
# From inside an existing project directory:
starter module add Billing

# If boilerplate source isn't auto-detected:
starter module add Billing --source C:\Systems\ForMe\Boilerplate
```

**Source resolution order:** The CLI finds the boilerplate source by checking:
1. `--source` parameter (explicit path)
2. `starter.json` in the project root (written by `starter new` with the source path baked in)
3. Environment variable `STARTER_BOILERPLATE_PATH`
4. Prompts the user if none found

**What it does:**

1. Reads `modules.json` to find the module's source paths
2. Detects the project name from the .sln file in the current directory
3. Copies the module's backend project from boilerplate source into `src/modules/`
4. Renames all `Starter` references to the project name (shared rename logic)
5. Adds `<ProjectReference>` to the API .csproj
6. Copies the frontend feature folder into `src/features/`
7. Updates `modules.ts` — sets the module to `true`
8. Prints: "Run `dotnet ef migrations add AddBillingModule` and `dotnet run` to apply"

### `starter module remove` — Remove module

```bash
starter module remove Billing
```

**What it does:**

1. Deletes the module backend project folder (e.g., `src/modules/MyApp.Module.Billing/`)
2. Removes the `<ProjectReference>` from API .csproj
3. Deletes the frontend feature folder (e.g., `src/features/billing/`)
4. Sets the module to `false` in `src/modules.ts`
5. Prints: "Run `dotnet ef migrations add RemoveBillingModule` to clean up tables (optional)"

No other changes needed — auto-discovery handles the rest:
- MediatR: assembly not present = handlers not scanned
- DbContext: EF configs not found = entities not registered
- Controllers: no ApplicationPart = no routes
- Permissions: no IModule = no permissions contributed
- Sidebar/routes: conditional on `modules.ts`

### `starter module list` — Show module status

```
Available modules for MyApp:

  Module           Status      Description
  ─────────────────────────────────────────────────────────────
  Files            Installed   Upload, download, S3/MinIO storage
  Reports          Installed   Async CSV/PDF report generation
  Notifications    Available   In-app notifications, Ably real-time
  Audit Logs       Installed   Entity change tracking
  API Keys         Available   Tenant/platform API key management
  Feature Flags    Available   Flag CRUD, tenant overrides
  Billing          Available   Subscription plans, payments
```

### `starter info` — Project summary

```
Project: MyApp
Location: C:\Projects\MyApp
Modules: Files, Reports, AuditLogs (3 of 7)
Backend: MyApp-BE (.NET 10)
Frontend: MyApp-FE (React 19)
```

### Module manifest

```jsonc
// scripts/modules.json — shipped with every generated project
{
  "files": {
    "displayName": "File Management",
    "backend": "src/modules/Starter.Module.Files",
    "frontend": "src/features/files",
    "description": "Upload, download, S3/MinIO storage"
  },
  "reports": {
    "displayName": "Reports",
    "backend": "src/modules/Starter.Module.Reports",
    "frontend": "src/features/reports",
    "description": "Async CSV/PDF report generation"
  },
  "notifications": {
    "displayName": "Notifications",
    "backend": "src/modules/Starter.Module.Notifications",
    "frontend": "src/features/notifications",
    "description": "In-app notifications, email preferences, Ably real-time"
  },
  "auditLogs": {
    "displayName": "Audit Logs",
    "backend": "src/modules/Starter.Module.AuditLogs",
    "frontend": "src/features/audit-logs",
    "description": "Entity change tracking, filtered viewer"
  },
  "apiKeys": {
    "displayName": "API Keys",
    "backend": "src/modules/Starter.Module.ApiKeys",
    "frontend": "src/features/api-keys",
    "description": "Tenant/platform API key management"
  },
  "featureFlags": {
    "displayName": "Feature Flags",
    "backend": "src/modules/Starter.Module.FeatureFlags",
    "frontend": "src/features/feature-flags",
    "description": "Flag CRUD, tenant overrides, enforcement"
  },
  "billing": {
    "displayName": "Billing",
    "backend": "src/modules/Starter.Module.Billing",
    "frontend": "src/features/billing",
    "description": "Subscription plans, payments, usage tracking"
  }
}
```

### CLI project structure

```
src/Starter.Cli/
├── Starter.Cli.csproj          ← dotnet tool, references Spectre.Console
├── Program.cs                  ← command dispatcher
├── Commands/
│   ├── NewCommand.cs           ← starter new
│   ├── ModuleAddCommand.cs     ← starter module add
│   ├── ModuleRemoveCommand.cs  ← starter module remove
│   ├── ModuleListCommand.cs    ← starter module list
│   └── InfoCommand.cs          ← starter info
└── Services/
    ├── ProjectRenamer.cs       ← shared rename logic (extracted from rename.ps1)
    ├── ModuleManager.cs        ← shared add/remove logic
    └── ProjectDetector.cs      ← find .sln, detect project name, locate modules.json
```

### Relationship to existing rename.ps1

`rename.ps1` is kept temporarily for backward compatibility and for the post-feature testing workflow (which uses it). The CLI tool subsumes its functionality. Once the CLI is stable, `rename.ps1` becomes a thin wrapper that calls `starter new` or is deprecated.

---

## Migration Phases

### Phase 1: Foundation (non-breaking)

Create the module infrastructure without moving any existing features yet.

1. Create `Starter.Module.Abstractions` project with `IModule`, `ITenantEntity`, `IModuleAssemblyProvider`, `ModuleLoader`, `BaseApiController` (moved from Starter.Api)
2. Modify `AddApplication()` to accept optional module assemblies (backward-compatible default parameter)
3. Modify `ApplicationDbContext.OnModelCreating` to scan module assemblies + convention-based tenant filters
4. Slim `IApplicationDbContext` — add `Set<T>()` method (keep existing DbSets for now)
5. Update `Program.cs` composition root (works with zero modules)
6. Update `DataSeeder` to aggregate permissions from modules

### Phase 2: First module extraction (Files)

Extract the Files feature as the pilot module to validate the pattern.

1. Create `src/modules/Starter.Module.Files/` with the full module structure
2. Move domain entities, handlers, controller, EF config, DTOs from core projects
3. Implement `FilesModule : IModule`
4. Remove Files DbSet from `IApplicationDbContext`, update handlers to use `Set<FileMetadata>()`
5. Remove Files permissions from core `Permissions.cs`
6. Verify: build, run, test with module present. Remove reference, verify clean removal.

### Phase 3: Remaining module extractions

Extract one module at a time in this order (least coupled first):
1. Reports
2. Notifications
3. Audit Logs
4. API Keys
5. Feature Flags
6. Billing

### Phase 4: CLI tool + frontend

1. Create `src/Starter.Cli/` project with Spectre.Console
2. Implement `starter new` (port rename.ps1 logic to C#)
3. Implement `starter module add/remove/list`
4. Implement `starter info`
5. Create `scripts/modules.json` manifest
6. Create `src/modules.ts` on frontend
7. Update routes and sidebar to use module registry
8. Keep `rename.ps1` as backward-compatible wrapper

---

## Verification

After each phase:

1. `dotnet build` — all projects compile
2. `dotnet run` — API starts, Swagger shows correct endpoints
3. Add a module reference → endpoints/permissions appear
4. Remove a module reference → endpoints/permissions disappear, no compile errors
5. Run rename script with `-Modules "Files,Billing"` → only those modules present in output
6. Run rename script with `-Modules "None"` → core only, builds and runs clean
7. Frontend: `npm run build` succeeds with any combination of active modules
