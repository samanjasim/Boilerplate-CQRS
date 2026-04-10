# Module Architecture — Phase 1 & 2 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Create the module infrastructure (IModule, ModuleLoader, convention-based tenant filters, multi-assembly scanning) and validate it by extracting the Files feature as the first module.

**Architecture:** Convention-based assembly scanning discovers `IModule` implementations at startup. Modules are single .csproj projects containing Domain/Application/Infrastructure/Controllers in folders. The host scans module assemblies for MediatR handlers, FluentValidation validators, and EF Core entity configurations automatically. A new `Starter.Module.Abstractions` project holds all contracts.

**Tech Stack:** .NET 10, EF Core, MediatR, FluentValidation, ASP.NET Core MVC (ApplicationPart)

**Spec:** `docs/superpowers/specs/2026-04-05-module-architecture-design.md`

---

## File Map

### New files

| File | Responsibility |
|------|---------------|
| `src/Starter.Module.Abstractions/Starter.Module.Abstractions.csproj` | Module contracts project (references Application) |
| `src/Starter.Module.Abstractions/IModule.cs` | Module descriptor interface |
| `src/Starter.Module.Abstractions/IModuleAssemblyProvider.cs` | Assembly provider interface + default impl |
| `src/Starter.Module.Abstractions/ModuleLoader.cs` | Discovery + topological sort |
| `src/Starter.Module.Abstractions/BaseApiController.cs` | Moved from Starter.Api — shared by all module controllers |
| `src/Starter.Domain/Common/ITenantEntity.cs` | Marker for tenant-filtered entities (in Domain, not Abstractions) |
| `src/modules/Starter.Module.Files/Starter.Module.Files.csproj` | Files module project |
| `src/modules/Starter.Module.Files/FilesModule.cs` | IModule implementation |
| `src/modules/Starter.Module.Files/Constants/FilePermissions.cs` | Permission constants |
| `src/modules/Starter.Module.Files/Application/Commands/...` | Moved from Application |
| `src/modules/Starter.Module.Files/Application/Queries/...` | Moved from Application |
| `src/modules/Starter.Module.Files/Application/DTOs/FileDto.cs` | Moved from Application |
| `src/modules/Starter.Module.Files/Application/Mappers/FileMapper.cs` | Moved from Application |
| `src/modules/Starter.Module.Files/Infrastructure/Configurations/FileMetadataConfiguration.cs` | Moved from Infrastructure |
| `src/modules/Starter.Module.Files/Controllers/FilesController.cs` | Moved from Api |

### Modified files

| File | Change |
|------|--------|
| `Starter.sln` | Add Starter.Module.Abstractions + Starter.Module.Files projects |
| `src/Starter.Api/Starter.Api.csproj` | Add references to Abstractions + Files module |
| `src/Starter.Api/Program.cs` | Add module discovery + registration |
| `src/Starter.Api/Controllers/BaseApiController.cs` | Replaced with thin subclass (real impl moved to Module.Abstractions) |
| `src/Starter.Application/DependencyInjection.cs` | Accept module assemblies parameter |
| `src/Starter.Application/Common/Interfaces/IApplicationDbContext.cs` | Remove Files DbSet, add `Set<T>()` |
| `src/Starter.Infrastructure/Persistence/ApplicationDbContext.cs` | Multi-assembly scanning + convention tenant filters |
| `src/Starter.Infrastructure/DependencyInjection.cs` | Pass module assemblies to DbContext |
| `src/Starter.Shared/Constants/Permissions.cs` | Remove Files permissions |
| `src/Starter.Shared/Constants/Roles.cs` | Remove Files permission mappings from roles |
| `src/Starter.Infrastructure/Persistence/Seeds/DataSeeder.cs` | Aggregate permissions from modules |

---

## Task 1: Create Starter.Module.Abstractions Project + ITenantEntity in Domain

**Files:**
- Create: `src/Starter.Module.Abstractions/Starter.Module.Abstractions.csproj`
- Create: `src/Starter.Module.Abstractions/IModule.cs`
- Create: `src/Starter.Module.Abstractions/IModuleAssemblyProvider.cs`
- Create: `src/Starter.Module.Abstractions/ModuleLoader.cs`
- Create: `src/Starter.Module.Abstractions/BaseApiController.cs` (moved from Api)
- Create: `src/Starter.Domain/Common/ITenantEntity.cs`
- Modify: `src/Starter.Api/GlobalUsings.cs` or controllers — resolve BaseApiController after move
- Modify: `Starter.sln`

**Dependency structure (no circular references):**
- `Domain` → `Shared` (existing)
- `Application` → `Domain`, `Shared` (existing)
- `Module.Abstractions` → `Application` (for BaseApiController, PaginatedList, IApplicationDbContext)
- `Module.Files` → `Module.Abstractions` (gets everything transitively)

`ITenantEntity` lives in `Starter.Domain.Common` (alongside `AggregateRoot`, `BaseEntity`, etc.) so domain entities can implement it without referencing Module.Abstractions.

- [ ] **Step 1: Create the .csproj file**

```xml
<!-- src/Starter.Module.Abstractions/Starter.Module.Abstractions.csproj -->
<Project Sdk="Microsoft.NET.Sdk">

  <ItemGroup>
    <FrameworkReference Include="Microsoft.AspNetCore.App" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="Asp.Versioning.Mvc" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\Starter.Application\Starter.Application.csproj" />
  </ItemGroup>

</Project>
```

Note: MediatR, FluentValidation, and EF Core come transitively via Application → Domain → Shared. Only `Asp.Versioning.Mvc` is needed explicitly (for BaseApiController's `[ApiVersion]` attribute).

- [ ] **Step 2: Create IModule interface**

```csharp
// src/Starter.Module.Abstractions/IModule.cs
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Starter.Module.Abstractions;

public interface IModule
{
    string Name { get; }
    string DisplayName { get; }
    string Version { get; }
    IReadOnlyList<string> Dependencies { get; }
    IServiceCollection ConfigureServices(IServiceCollection services, IConfiguration configuration);
    IEnumerable<(string Name, string Description, string Module)> GetPermissions();
    IEnumerable<(string Role, string[] Permissions)> GetDefaultRolePermissions();
    Task SeedDataAsync(IServiceProvider services);
}
```

- [ ] **Step 3: Create ITenantEntity in Domain**

```csharp
// src/Starter.Domain/Common/ITenantEntity.cs
namespace Starter.Domain.Common;

public interface ITenantEntity
{
    Guid? TenantId { get; }
}
```

- [ ] **Step 4: Move BaseApiController from Starter.Api to Module.Abstractions**

Copy `src/Starter.Api/Controllers/BaseApiController.cs` to `src/Starter.Module.Abstractions/BaseApiController.cs`.

Update the namespace in the new file:

```csharp
// src/Starter.Module.Abstractions/BaseApiController.cs
using Asp.Versioning;
using Starter.Application.Common.Models;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Starter.Shared.Models;
using Starter.Shared.Results;

namespace Starter.Module.Abstractions;

// ... rest of class unchanged ...
```

Then replace the original file in `src/Starter.Api/Controllers/BaseApiController.cs` with a re-export so existing core controllers don't break:

```csharp
// src/Starter.Api/Controllers/BaseApiController.cs
// Re-export from Module.Abstractions for backward compatibility
using Starter.Module.Abstractions;

namespace Starter.Api.Controllers;

public abstract class BaseApiController(ISender mediator) : Starter.Module.Abstractions.BaseApiController(mediator);
```

This thin subclass preserves the `Starter.Api.Controllers` namespace for existing core controllers while the real implementation lives in Module.Abstractions.

- [ ] **Step 5: Create IModuleAssemblyProvider and ModuleLoader**

Combine these into one step since they're small and related.

```csharp
// src/Starter.Module.Abstractions/IModuleAssemblyProvider.cs
using System.Reflection;

namespace Starter.Module.Abstractions;

public interface IModuleAssemblyProvider
{
    IReadOnlyList<Assembly> GetModuleAssemblies();
}

public sealed class ModuleAssemblyProvider(IReadOnlyList<Assembly> assemblies) : IModuleAssemblyProvider
{
    public IReadOnlyList<Assembly> GetModuleAssemblies() => assemblies;
}
```

- [ ] **Step 5: Create ModuleLoader**

```csharp
// src/Starter.Module.Abstractions/ModuleLoader.cs
using System.Reflection;

namespace Starter.Module.Abstractions;

public static class ModuleLoader
{
    public static IReadOnlyList<IModule> DiscoverModules()
    {
        var moduleType = typeof(IModule);
        var modules = new List<IModule>();

        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            try
            {
                var types = assembly.GetTypes()
                    .Where(t => moduleType.IsAssignableFrom(t) && t is { IsAbstract: false, IsInterface: false });

                foreach (var type in types)
                {
                    if (Activator.CreateInstance(type) is IModule module)
                        modules.Add(module);
                }
            }
            catch (ReflectionTypeLoadException)
            {
                // Skip assemblies that can't be loaded
            }
        }

        return modules;
    }

    public static IReadOnlyList<IModule> ResolveOrder(IReadOnlyList<IModule> modules)
    {
        var moduleMap = modules.ToDictionary(m => m.Name);
        var sorted = new List<IModule>();
        var visited = new HashSet<string>();
        var visiting = new HashSet<string>();

        foreach (var module in modules)
            Visit(module, moduleMap, sorted, visited, visiting);

        return sorted;
    }

    private static void Visit(
        IModule module,
        Dictionary<string, IModule> moduleMap,
        List<IModule> sorted,
        HashSet<string> visited,
        HashSet<string> visiting)
    {
        if (visited.Contains(module.Name)) return;

        if (!visiting.Add(module.Name))
            throw new InvalidOperationException(
                $"Circular module dependency detected: {module.Name}");

        foreach (var dep in module.Dependencies)
        {
            if (moduleMap.TryGetValue(dep, out var depModule))
                Visit(depModule, moduleMap, sorted, visited, visiting);
        }

        visiting.Remove(module.Name);
        visited.Add(module.Name);
        sorted.Add(module);
    }
}
```

- [ ] **Step 6: Move BaseApiController to Module.Abstractions**

Move `src/Starter.Api/Controllers/BaseApiController.cs` to `src/Starter.Module.Abstractions/BaseApiController.cs`. Update namespace from `Starter.Api.Controllers` to `Starter.Module.Abstractions`.

Then in `src/Starter.Api/Controllers/`, create a one-line re-export so existing core controllers don't break:

```csharp
// src/Starter.Api/Controllers/BaseApiController.cs
// Re-export for backward compatibility — core controllers use this namespace
global using Starter.Module.Abstractions;
```

Actually, simpler: just add `using Starter.Module.Abstractions;` to each core controller's usings, or add a `GlobalUsings.cs` in `Starter.Api`:

```csharp
// src/Starter.Api/GlobalUsings.cs — add this line
global using Starter.Module.Abstractions;
```

This way existing controllers resolve `BaseApiController` without any changes to their code.

- [ ] **Step 6: Add project to solution**

```bash
cd boilerplateBE
dotnet sln add src/Starter.Module.Abstractions/Starter.Module.Abstractions.csproj
```

- [ ] **Step 7: Build to verify**

```bash
cd boilerplateBE
dotnet build
```

Expected: BUILD SUCCEEDED — Module.Abstractions compiles, and core controllers still resolve BaseApiController via the thin subclass in Starter.Api.

- [ ] **Step 8: Commit**

```bash
git add -A
git commit -m "feat: add Starter.Module.Abstractions with IModule, ModuleLoader, BaseApiController, ITenantEntity"
```

---

## Task 2: Wire Module Discovery into Program.cs and DI

**Files:**
- Modify: `src/Starter.Api/Starter.Api.csproj` — add Abstractions reference
- Modify: `src/Starter.Api/Program.cs` — add module discovery + registration
- Modify: `src/Starter.Application/DependencyInjection.cs` — accept module assemblies
- Modify: `src/Starter.Infrastructure/DependencyInjection.cs` — accept module assemblies parameter (backward-compatible)

- [ ] **Step 1: Add Abstractions reference to Api.csproj**

Add to `src/Starter.Api/Starter.Api.csproj` inside the existing `<ItemGroup>` with ProjectReferences:

```xml
    <ProjectReference Include="..\Starter.Module.Abstractions\Starter.Module.Abstractions.csproj" />
```

- [ ] **Step 2: Modify AddApplication() to accept module assemblies**

In `src/Starter.Application/DependencyInjection.cs`, change the method signature and body:

```csharp
using System.Reflection;
using Starter.Application.Common.Behaviors;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace Starter.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(
        this IServiceCollection services,
        IReadOnlyList<Assembly>? moduleAssemblies = null)
    {
        var assemblies = new List<Assembly> { Assembly.GetExecutingAssembly() };
        if (moduleAssemblies is not null)
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
}
```

- [ ] **Step 3: Modify AddInfrastructure() signature (backward-compatible)**

In `src/Starter.Infrastructure/DependencyInjection.cs`, change the `AddInfrastructure` method signature to accept module assemblies and pass them down to `AddPersistence`:

```csharp
public static IServiceCollection AddInfrastructure(
    this IServiceCollection services,
    IConfiguration configuration,
    IReadOnlyList<System.Reflection.Assembly>? moduleAssemblies = null)
{
    QuestPDF.Settings.License = LicenseType.Community;

    services
        .AddPersistence(configuration)
        .AddCaching(configuration)
        .AddMessaging(configuration)
        .AddServices()
        .AddEmailServices(configuration)
        .AddSmsServices(configuration)
        .AddRealtimeServices(configuration)
        .AddStorageServices(configuration)
        .AddExportServices()
        .AddImportExportServices()
        .AddHealthChecks(configuration);

    return services;
}
```

Note: Module assemblies are NOT passed to `AddPersistence` directly. Instead, `ApplicationDbContext` receives them via the `IModuleAssemblyProvider` service registered in `Program.cs`. No changes to `AddPersistence` needed.

- [ ] **Step 4: Update Program.cs with module discovery**

In `src/Starter.Api/Program.cs`, add module discovery after the Serilog setup and before `AddApplication()`. Replace lines 34-43 with:

```csharp
// Add services
builder.Services.AddHttpContextAccessor();

// Discover and resolve modules
var modules = Starter.Module.Abstractions.ModuleLoader.DiscoverModules();
var orderedModules = Starter.Module.Abstractions.ModuleLoader.ResolveOrder(modules);
var moduleAssemblies = orderedModules.Select(m => m.GetType().Assembly).Distinct().ToList();

// Register module assembly provider (used by ApplicationDbContext)
builder.Services.AddSingleton<Starter.Module.Abstractions.IModuleAssemblyProvider>(
    new Starter.Module.Abstractions.ModuleAssemblyProvider(moduleAssemblies));

// Add layers
builder.Services.AddApplication(moduleAssemblies);
builder.Services.AddInfrastructure(builder.Configuration, moduleAssemblies);
builder.Services.AddIdentityInfrastructure(builder.Configuration);

// Module-specific services
foreach (var module in orderedModules)
    module.ConfigureServices(builder.Services, builder.Configuration);

// API Configuration
var mvcBuilder = builder.Services.AddControllers();
foreach (var asm in moduleAssemblies)
    mvcBuilder.AddApplicationPart(asm);

builder.Services.AddEndpointsApiExplorer();
```

And after the core database seeding section (around line 88), add module seeding:

```csharp
// Module seeding
foreach (var module in orderedModules)
    await module.SeedDataAsync(app.Services);
```

- [ ] **Step 5: Build to verify no regressions**

```bash
cd boilerplateBE
dotnet build
```

Expected: BUILD SUCCEEDED — no modules exist yet so module discovery returns empty list, all existing code works unchanged.

- [ ] **Step 6: Commit**

```bash
git add -A
git commit -m "feat: wire module discovery into Program.cs, DI, and MediatR registration"
```

---

## Task 3: Modify ApplicationDbContext for Multi-Assembly Scanning and Convention Tenant Filters

**Files:**
- Modify: `src/Starter.Infrastructure/Persistence/ApplicationDbContext.cs`
- Modify: `src/Starter.Application/Common/Interfaces/IApplicationDbContext.cs`

- [ ] **Step 1: Add Set\<T\>() to IApplicationDbContext**

In `src/Starter.Application/Common/Interfaces/IApplicationDbContext.cs`, add the generic accessor. Keep ALL existing DbSets for now (they'll be removed one at a time as modules are extracted). Add after the last DbSet property (line 35):

```csharp
    DbSet<TEntity> Set<TEntity>() where TEntity : class;
```

Note: `DbContext` already implements `Set<T>()`, so `ApplicationDbContext` inherits it automatically.

- [ ] **Step 2: Update ApplicationDbContext constructor and OnModelCreating**

Replace the full `ApplicationDbContext.cs` content. Key changes:
- Add `IModuleAssemblyProvider` to constructor
- Scan module assemblies in `OnModelCreating`
- Replace 12 hardcoded tenant query filters with convention-based `ApplyTenantFilters`

```csharp
using System.Data;
using System.Linq.Expressions;
using System.Reflection;
using Starter.Application.Common.Interfaces;
using Starter.Domain.ApiKeys.Entities;
using Starter.Domain.Billing.Entities;
using Starter.Domain.Common;
using Starter.Domain.FeatureFlags.Entities;
using Starter.Domain.Identity.Entities;
using Starter.Domain.Tenants.Entities;
using Starter.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace Starter.Infrastructure.Persistence;

public sealed class ApplicationDbContext : DbContext, IApplicationDbContext
{
    private readonly ICurrentUserService? _currentUserService;
    private readonly IReadOnlyList<Assembly> _moduleAssemblies;

    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<Invitation> Invitations => Set<Invitation>();
    public DbSet<Session> Sessions => Set<Session>();
    public DbSet<LoginHistory> LoginHistory => Set<LoginHistory>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<NotificationPreference> NotificationPreferences => Set<NotificationPreference>();
    public DbSet<FileMetadata> FileMetadata => Set<FileMetadata>();
    public DbSet<ReportRequest> ReportRequests => Set<ReportRequest>();
    public DbSet<SystemSetting> SystemSettings => Set<SystemSetting>();
    public DbSet<ApiKey> ApiKeys => Set<ApiKey>();
    public DbSet<FeatureFlag> FeatureFlags => Set<FeatureFlag>();
    public DbSet<TenantFeatureFlag> TenantFeatureFlags => Set<TenantFeatureFlag>();
    public DbSet<SubscriptionPlan> SubscriptionPlans => Set<SubscriptionPlan>();
    public DbSet<TenantSubscription> TenantSubscriptions => Set<TenantSubscription>();
    public DbSet<PaymentRecord> PaymentRecords => Set<PaymentRecord>();
    public DbSet<PlanPriceHistory> PlanPriceHistories => Set<PlanPriceHistory>();

    private Guid? TenantId => _currentUserService?.TenantId;

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

        // Module entity configurations
        foreach (var assembly in _moduleAssemblies)
            modelBuilder.ApplyConfigurationsFromAssembly(assembly);

        // Convention-based tenant query filters
        ApplyTenantFilters(modelBuilder);
    }

    private void ApplyTenantFilters(ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (!typeof(ITenantEntity).IsAssignableFrom(entityType.ClrType))
                continue;

            // Skip entities that already have a query filter defined by their IEntityTypeConfiguration
            if (entityType.GetQueryFilter() is not null)
                continue;

            // Build: e => TenantId == null || e.TenantId == TenantId
            var parameter = Expression.Parameter(entityType.ClrType, "e");
            var tenantIdProp = Expression.Property(parameter, nameof(ITenantEntity.TenantId));
            var currentTenantId = Expression.Property(
                Expression.Constant(this),
                typeof(ApplicationDbContext).GetProperty(nameof(TenantId),
                    BindingFlags.NonPublic | BindingFlags.Instance)!);

            var filter = Expression.Lambda(
                Expression.OrElse(
                    Expression.Equal(currentTenantId,
                        Expression.Constant(null, typeof(Guid?))),
                    Expression.Equal(tenantIdProp, currentTenantId)),
                parameter);

            modelBuilder.Entity(entityType.ClrType).HasQueryFilter(filter);
        }
    }

    public async Task<T> ExecuteInTransactionAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        IsolationLevel isolationLevel,
        CancellationToken cancellationToken = default)
    {
        var strategy = Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async ct =>
        {
            await using var transaction = await Database.BeginTransactionAsync(isolationLevel, ct);
            var result = await operation(ct);
            await transaction.CommitAsync(ct);
            return result;
        }, cancellationToken);
    }
}
```

- [ ] **Step 3: Add ITenantEntity to existing domain entities**

The convention-based tenant filters require entities to implement `ITenantEntity`. These entities already have a `Guid? TenantId` property — they just need the interface added.

Entities that use the standard filter (`TenantId == null || entity.TenantId == TenantId`):
- `User` — `src/Starter.Domain/Identity/Entities/User.cs`
- `AuditLog` — `src/Starter.Domain/Common/AuditLog.cs`
- `Notification` — `src/Starter.Domain/Common/Notification.cs`
- `FileMetadata` — `src/Starter.Domain/Common/FileMetadata.cs`
- `ReportRequest` — `src/Starter.Domain/Common/ReportRequest.cs`
- `ApiKey` — `src/Starter.Domain/ApiKeys/Entities/ApiKey.cs`
- `TenantFeatureFlag` — `src/Starter.Domain/FeatureFlags/Entities/TenantFeatureFlag.cs`
- `TenantSubscription` — `src/Starter.Domain/Billing/Entities/TenantSubscription.cs`
- `PaymentRecord` — `src/Starter.Domain/Billing/Entities/PaymentRecord.cs`

For each, add `: ITenantEntity` to the class declaration. For example, in `User.cs`:

```csharp
// Before:
public sealed class User : AggregateRoot
// After:
public sealed class User : AggregateRoot, ITenantEntity
```

Add `using Starter.Domain.Common;` to each file (if not already present — most Domain entity files already have this namespace).

Entities with NON-standard filters (keep their custom filter in their `IEntityTypeConfiguration`):
- `Role` — sees global roles (TenantId=null) + own tenant's roles. Filter: `TenantId == null || r.TenantId == null || r.TenantId == TenantId`
- `Invitation` — sees platform invitations + own tenant's. Same pattern as Role.
- `SystemSetting` — sees global settings + own tenant's. Same pattern.
- `Tenant` — uses `t.Id == TenantId` not `t.TenantId`. Does NOT implement ITenantEntity.

For Role, Invitation, SystemSetting: do NOT add `ITenantEntity`. Keep their custom query filters in their `IEntityTypeConfiguration` files. They already have custom filters in the EF configs.

**Wait** — actually, the current code puts ALL query filters in `ApplicationDbContext.OnModelCreating`, not in `IEntityTypeConfiguration` files. We need to move the non-standard filters to their EF configs before removing them from DbContext.

Move these three non-standard filters to their respective configuration files:

In `src/Starter.Infrastructure/Persistence/Configurations/RoleConfiguration.cs`, add inside `Configure()`:

```csharp
// In the existing Configure method, add:
builder.HasQueryFilter(r =>
    EF.Property<Guid?>(r, "_tenantId") == null ||
    r.TenantId == null ||
    r.TenantId == EF.Property<Guid?>(r, "_tenantId"));
```

**Actually**, the EF query filter approach using `_currentUserService` from DbContext is specific to how the current code works — the filter expression references `this.TenantId` which is a property on the DbContext. Moving filters to `IEntityTypeConfiguration` won't work because configs don't have access to the DbContext instance.

**Better approach**: For entities with non-standard filters, still implement `ITenantEntity` but also register them in a `HashSet<Type>` of types to skip in the convention loop. The non-standard filters remain in `ApplyTenantFilters` as explicit cases:

```csharp
private void ApplyTenantFilters(ModelBuilder modelBuilder)
{
    // Entities with custom filter logic — handled explicitly
    modelBuilder.Entity<Role>().HasQueryFilter(r =>
        TenantId == null || r.TenantId == null || r.TenantId == TenantId);
    modelBuilder.Entity<Invitation>().HasQueryFilter(i =>
        TenantId == null || i.TenantId == null || i.TenantId == TenantId);
    modelBuilder.Entity<SystemSetting>().HasQueryFilter(s =>
        TenantId == null || s.TenantId == null || s.TenantId == TenantId);
    modelBuilder.Entity<Tenant>().HasQueryFilter(t =>
        TenantId == null || t.Id == TenantId);

    // Convention-based: standard filter for all ITenantEntity that don't already have one
    foreach (var entityType in modelBuilder.Model.GetEntityTypes())
    {
        if (!typeof(ITenantEntity).IsAssignableFrom(entityType.ClrType))
            continue;

        if (entityType.GetQueryFilter() is not null)
            continue;

        var parameter = Expression.Parameter(entityType.ClrType, "e");
        var tenantIdProp = Expression.Property(parameter, nameof(ITenantEntity.TenantId));
        var currentTenantId = Expression.Property(
            Expression.Constant(this),
            typeof(ApplicationDbContext).GetProperty(nameof(TenantId),
                BindingFlags.NonPublic | BindingFlags.Instance)!);

        var filter = Expression.Lambda(
            Expression.OrElse(
                Expression.Equal(currentTenantId,
                    Expression.Constant(null, typeof(Guid?))),
                Expression.Equal(tenantIdProp, currentTenantId)),
            parameter);

        modelBuilder.Entity(entityType.ClrType).HasQueryFilter(filter);
    }
}
```

Update the `ApplicationDbContext` code from Step 2 accordingly — replace the `ApplyTenantFilters` method with this version.

- [ ] **Step 4: Build and verify**

```bash
cd boilerplateBE
dotnet build
```

Expected: BUILD SUCCEEDED. All existing query filters still work — the explicit ones fire first, then the convention loop skips entities that already have filters.

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "feat: add multi-assembly scanning and convention-based tenant filters to DbContext"
```

---

## Task 4: Update DataSeeder to Aggregate Module Permissions

**Files:**
- Modify: `src/Starter.Infrastructure/Persistence/Seeds/DataSeeder.cs`

- [ ] **Step 1: Modify DataSeeder.SeedAsync to accept modules**

Change the method to discover modules and pass them to permission/role seeding. In `src/Starter.Infrastructure/Persistence/Seeds/DataSeeder.cs`:

Replace the `SeedAsync` method signature and add module discovery at the top:

```csharp
public static async Task SeedAsync(IServiceProvider serviceProvider)
{
    using var scope = serviceProvider.CreateScope();
    var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<ApplicationDbContext>>();

    // Discover modules for permission aggregation
    var modules = Starter.Module.Abstractions.ModuleLoader.DiscoverModules();

    try
    {
        var resetDatabase = configuration.GetValue<bool>("DatabaseSettings:ResetDatabase");
        if (resetDatabase)
        {
            logger.LogWarning("ResetDatabase is enabled — dropping and recreating database");
            await context.Database.EnsureDeletedAsync();
        }

        await context.Database.MigrateAsync();

        await SeedPermissionsAsync(context, logger, modules);
        await SeedRolesAsync(context, logger);
        await SeedRolePermissionsAsync(context, logger, modules);
        await SeedDefaultTenantAsync(context, logger);
        await SeedSuperAdminUserAsync(context, configuration, logger);
        await SeedDefaultSettingsAsync(context, logger);
        await SeedFeatureFlagsAsync(context, logger);
        await SeedSubscriptionPlansAsync(context, logger);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "An error occurred while seeding the database");
        throw;
    }
}
```

- [ ] **Step 2: Update SeedPermissionsAsync to aggregate from modules**

Change the method signature and permission source:

```csharp
private static async Task SeedPermissionsAsync(
    ApplicationDbContext context,
    ILogger logger,
    IReadOnlyList<Starter.Module.Abstractions.IModule> modules)
{
    // Aggregate permissions from core + all modules
    var allPermissionMetadata = Permissions.GetAllWithMetadata()
        .Concat(modules.SelectMany(m => m.GetPermissions()))
        .ToList();

    var allPermissions = allPermissionMetadata.Select(p => p.Name).ToList();
    var existingPermissions = await context.Permissions
        .Select(p => p.Name)
        .ToListAsync();

    var newPermissions = allPermissions
        .Where(p => !existingPermissions.Contains(p))
        .ToList();

    if (newPermissions.Count == 0) return;

    var metadataLookup = allPermissionMetadata.ToDictionary(p => p.Name);

    foreach (var permissionName in newPermissions)
    {
        var meta = metadataLookup[permissionName];
        var permission = Permission.Create(
            meta.Name,
            meta.Description,
            meta.Module);

        context.Permissions.Add(permission);
    }

    await context.SaveChangesAsync();
    logger.LogInformation("Seeded {Count} permissions", newPermissions.Count);
}
```

- [ ] **Step 3: Update SeedRolePermissionsAsync to aggregate from modules**

Change the method signature to accept modules and merge role-permission mappings:

```csharp
private static async Task SeedRolePermissionsAsync(
    ApplicationDbContext context,
    ILogger logger,
    IReadOnlyList<Starter.Module.Abstractions.IModule> modules)
{
    // Aggregate role-permission mappings from core + modules
    var coreRoleMappings = RoleNames.GetRolePermissions();
    var moduleRoleMappings = modules.SelectMany(m => m.GetDefaultRolePermissions());
    var allMappings = coreRoleMappings.Concat(moduleRoleMappings).ToList();

    var roles = await context.Roles
        .Include(r => r.RolePermissions)
        .ToListAsync();
    var permissions = await context.Permissions.ToListAsync();

    var seededCount = 0;

    foreach (var (roleName, permissionNames) in allMappings)
    {
        var role = roles.FirstOrDefault(r => r.Name == roleName);
        if (role is null) continue;

        foreach (var permissionName in permissionNames)
        {
            var permission = permissions.FirstOrDefault(p => p.Name == permissionName);
            if (permission is null) continue;

            var exists = role.RolePermissions.Any(rp => rp.PermissionId == permission.Id);
            if (exists) continue;

            role.AddPermission(permission);
            seededCount++;
        }
    }

    if (seededCount > 0)
    {
        await context.SaveChangesAsync();
        logger.LogInformation("Seeded {Count} role-permission mappings", seededCount);
    }
}
```

- [ ] **Step 4: Build and verify**

```bash
cd boilerplateBE
dotnet build
```

Expected: BUILD SUCCEEDED

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "feat: DataSeeder aggregates permissions from core + discovered modules"
```

---

## Task 5: Extract Files Feature as First Module

This is the validation task — we move the Files feature out of the core projects into `src/modules/Starter.Module.Files/`.

**Important context**: `IFileService` and `IStorageService` interfaces and their implementations stay in core — they're used by Tenants, Reports, ImportExport, and other features. The Files *module* contains only the CRUD handlers (upload/download/delete commands/queries), the controller, the DTO, the mapper, the entity, and the EF configuration.

**Files:**
- Create: `src/modules/Starter.Module.Files/Starter.Module.Files.csproj`
- Create: `src/modules/Starter.Module.Files/FilesModule.cs`
- Create: `src/modules/Starter.Module.Files/Constants/FilePermissions.cs`
- Move: `FileMetadata.cs` entity → module Domain/Entities/
- Move: `FileCategory.cs` enum → module Domain/Enums/
- Move: Files commands/queries/DTOs → module Application/
- Move: `FileMetadataConfiguration.cs` → module Infrastructure/Configurations/
- Move: `FilesController.cs` → module Controllers/
- Modify: `IApplicationDbContext.cs` — remove FileMetadata DbSet
- Modify: `Permissions.cs` — remove Files permissions
- Modify: `Roles.cs` — remove Files permission mappings
- Modify: `Starter.Api/Starter.Api.csproj` — add module reference

- [ ] **Step 1: Create module .csproj**

```xml
<!-- src/modules/Starter.Module.Files/Starter.Module.Files.csproj -->
<Project Sdk="Microsoft.NET.Sdk">

  <ItemGroup>
    <FrameworkReference Include="Microsoft.AspNetCore.App" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="Riok.Mapperly" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\Starter.Module.Abstractions\Starter.Module.Abstractions.csproj" />
  </ItemGroup>

</Project>
```

Note: The module references only `Starter.Module.Abstractions`, which transitively provides Application, Domain, Shared, MediatR, FluentValidation, and EF Core. Only `Riok.Mapperly` (source-gen mapper) is needed explicitly since it's not a transitive dependency.

- [ ] **Step 2: Create FilePermissions constants**

```csharp
// src/modules/Starter.Module.Files/Constants/FilePermissions.cs
namespace Starter.Module.Files.Constants;

public static class FilePermissions
{
    public const string View = "Files.View";
    public const string Upload = "Files.Upload";
    public const string Delete = "Files.Delete";
    public const string Manage = "Files.Manage";
}
```

- [ ] **Step 3: Create FilesModule.cs**

```csharp
// src/modules/Starter.Module.Files/FilesModule.cs
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Starter.Module.Abstractions;
using Starter.Module.Files.Constants;

namespace Starter.Module.Files;

public sealed class FilesModule : IModule
{
    public string Name => "Starter.Module.Files";
    public string DisplayName => "File Management";
    public string Version => "1.0.0";
    public IReadOnlyList<string> Dependencies => [];

    public IServiceCollection ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        // IStorageService and IFileService are registered by core Infrastructure
        // No module-specific services needed
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

    public Task SeedDataAsync(IServiceProvider services)
    {
        // File-specific feature flags are seeded by core DataSeeder for now
        return Task.CompletedTask;
    }
}
```

- [ ] **Step 4: Move files from core to module**

This step physically moves files. Use the shell to create directories and move files:

```bash
cd boilerplateBE

# Create module directory structure
mkdir -p src/modules/Starter.Module.Files/Domain/Entities
mkdir -p src/modules/Starter.Module.Files/Domain/Enums
mkdir -p src/modules/Starter.Module.Files/Application/Commands/UploadFile
mkdir -p src/modules/Starter.Module.Files/Application/Commands/DeleteFile
mkdir -p src/modules/Starter.Module.Files/Application/Commands/UpdateFileMetadata
mkdir -p src/modules/Starter.Module.Files/Application/Queries/GetFiles
mkdir -p src/modules/Starter.Module.Files/Application/Queries/GetFileById
mkdir -p src/modules/Starter.Module.Files/Application/Queries/GetFileUrl
mkdir -p src/modules/Starter.Module.Files/Application/DTOs
mkdir -p src/modules/Starter.Module.Files/Application/Mappers
mkdir -p src/modules/Starter.Module.Files/Infrastructure/Configurations
mkdir -p src/modules/Starter.Module.Files/Controllers
mkdir -p src/modules/Starter.Module.Files/Constants

# Move domain entity
cp src/Starter.Domain/Common/FileMetadata.cs src/modules/Starter.Module.Files/Domain/Entities/
cp src/Starter.Domain/Common/Enums/FileCategory.cs src/modules/Starter.Module.Files/Domain/Enums/

# Move application commands/queries
cp src/Starter.Application/Features/Files/Commands/UploadFile/* src/modules/Starter.Module.Files/Application/Commands/UploadFile/
cp src/Starter.Application/Features/Files/Commands/DeleteFile/* src/modules/Starter.Module.Files/Application/Commands/DeleteFile/
cp src/Starter.Application/Features/Files/Commands/UpdateFileMetadata/* src/modules/Starter.Module.Files/Application/Commands/UpdateFileMetadata/
cp src/Starter.Application/Features/Files/Queries/GetFiles/* src/modules/Starter.Module.Files/Application/Queries/GetFiles/
cp src/Starter.Application/Features/Files/Queries/GetFileById/* src/modules/Starter.Module.Files/Application/Queries/GetFileById/
cp src/Starter.Application/Features/Files/Queries/GetFileUrl/* src/modules/Starter.Module.Files/Application/Queries/GetFileUrl/

# Move DTOs and mapper
cp src/Starter.Application/Features/Files/FileDto.cs src/modules/Starter.Module.Files/Application/DTOs/
cp src/Starter.Application/Features/Files/FileMapper.cs src/modules/Starter.Module.Files/Application/Mappers/

# Move EF configuration
cp src/Starter.Infrastructure/Persistence/Configurations/FileMetadataConfiguration.cs src/modules/Starter.Module.Files/Infrastructure/Configurations/

# Move controller
cp src/Starter.Api/Controllers/FilesController.cs src/modules/Starter.Module.Files/Controllers/
```

- [ ] **Step 5: Update namespaces in all moved files**

Every moved file needs its namespace updated from `Starter.Application.Features.Files.*` / `Starter.Domain.Common` / `Starter.Api.Controllers` / `Starter.Infrastructure.Persistence.Configurations` to `Starter.Module.Files.*`.

For each moved file, update the namespace and using statements. The key namespace changes:

| Old namespace | New namespace |
|------|------|
| `Starter.Domain.Common` (FileMetadata) | `Starter.Module.Files.Domain.Entities` |
| `Starter.Domain.Common.Enums` (FileCategory) | `Starter.Module.Files.Domain.Enums` |
| `Starter.Application.Features.Files.Commands.*` | `Starter.Module.Files.Application.Commands.*` |
| `Starter.Application.Features.Files.Queries.*` | `Starter.Module.Files.Application.Queries.*` |
| `Starter.Application.Features.Files` (FileDto, FileMapper) | `Starter.Module.Files.Application.DTOs` / `Starter.Module.Files.Application.Mappers` |
| `Starter.Infrastructure.Persistence.Configurations` (FileMetadataConfiguration) | `Starter.Module.Files.Infrastructure.Configurations` |
| `Starter.Api.Controllers` (FilesController) | `Starter.Module.Files.Controllers` |

Also update `using` statements:
- `using Starter.Shared.Constants` → `using Starter.Module.Files.Constants` (for permission attributes)
- Handlers that reference `_context.FileMetadata` → `_context.Set<FileMetadata>()`

The `using Starter.Application.Common.Interfaces` stays (for IApplicationDbContext, ICurrentUserService, IFileService, etc.).

The controller needs to change `[Authorize(Policy = Permissions.Files.View)]` to `[Authorize(Policy = FilePermissions.View)]` with `using Starter.Module.Files.Constants;`.

**Important**: `FileMetadata` entity is referenced by `IFileService` (core interface) and `FileService` (core implementation). Those core files need a `using Starter.Module.Files.Domain.Entities;` added since `FileMetadata` moved namespaces. Similarly, `FileCategory` enum references in core need updating.

**Actually** — this reveals a coupling issue. `FileMetadata` and `FileCategory` are used by `IFileService`, which is a core interface used by Tenants, Reports, ImportExport. Moving these types to the module would mean core depends on a module — that's backwards.

**Resolution**: Keep `FileMetadata` entity and `FileCategory` enum in `Starter.Domain`. The module references them from Domain. Only the *handlers*, *controller*, *DTO*, *mapper*, and *EF configuration* move to the module. This is clean because:
- Domain entities are shared types — they can live in Domain even if their CRUD logic is in a module
- `IFileService` continues to reference `Starter.Domain.Common.FileMetadata`
- The module's handlers reference the same entity
- The module's `IEntityTypeConfiguration<FileMetadata>` configures the table

Update the copy commands from Step 4 — do NOT copy `FileMetadata.cs` or `FileCategory.cs`. Remove those two lines. Delete the empty Domain folders in the module.

- [ ] **Step 6: Delete original files from core (after namespace updates are done)**

```bash
cd boilerplateBE

# Delete moved application files
rm -rf src/Starter.Application/Features/Files/

# Delete moved controller
rm src/Starter.Api/Controllers/FilesController.cs

# Delete moved EF configuration
rm src/Starter.Infrastructure/Persistence/Configurations/FileMetadataConfiguration.cs

# Remove empty module Domain folders (entity stays in core)
rm -rf src/modules/Starter.Module.Files/Domain/
```

- [ ] **Step 7: Remove FileMetadata DbSet from IApplicationDbContext**

In `src/Starter.Application/Common/Interfaces/IApplicationDbContext.cs`, remove:

```csharp
    DbSet<FileMetadata> FileMetadata { get; }
```

And in `src/Starter.Infrastructure/Persistence/ApplicationDbContext.cs`, remove:

```csharp
    public DbSet<FileMetadata> FileMetadata => Set<FileMetadata>();
```

Any core code that referenced `_context.FileMetadata` (e.g., `FileService.cs`) should use `_context.Set<FileMetadata>()` instead. Search and replace.

- [ ] **Step 8: Remove Files permissions from core Permissions.cs**

In `src/Starter.Shared/Constants/Permissions.cs`:
- Remove the `Files` inner class (lines 76-82)
- Remove the Files entries from `GetAllWithMetadata()` (lines 179-183)

In `src/Starter.Shared/Constants/Roles.cs`:
- Remove Files permission mappings from `GetRolePermissions()` — these are now provided by `FilesModule.GetDefaultRolePermissions()`

- [ ] **Step 9: Add module reference to Api.csproj and solution**

```bash
cd boilerplateBE
dotnet sln add src/modules/Starter.Module.Files/Starter.Module.Files.csproj
```

In `src/Starter.Api/Starter.Api.csproj`, add:

```xml
    <ProjectReference Include="..\modules\Starter.Module.Files\Starter.Module.Files.csproj" />
```

- [ ] **Step 10: Build and fix compilation errors**

```bash
cd boilerplateBE
dotnet build 2>&1
```

Fix any remaining namespace issues, missing usings, or broken references. Common fixes:
- Core `FileService.cs` needs `using Starter.Domain.Common;` (FileMetadata stayed in Domain)
- Module handlers need correct usings for `IApplicationDbContext`, `ICurrentUserService`, `Result`, etc.
- Module controller needs `using Starter.Module.Files.Constants;` for `FilePermissions`
- Any `_context.FileMetadata` in module handlers → `_context.Set<FileMetadata>()`

- [ ] **Step 11: Commit**

```bash
git add -A
git commit -m "feat: extract Files feature as first module (Starter.Module.Files)"
```

---

## Task 6: Verify Module Add/Remove Works

This task validates that the module system works correctly — the app should function with the module present and compile cleanly without it.

**Files:** No new files — verification only.

- [ ] **Step 1: Verify with module present**

```bash
cd boilerplateBE
dotnet build
dotnet run --project src/Starter.Api --launch-profile http &
```

Expected: API starts. Check Swagger at http://localhost:5000/swagger — Files endpoints should appear.

- [ ] **Step 2: Verify module removal compiles**

Temporarily comment out the Files module ProjectReference in `src/Starter.Api/Starter.Api.csproj`:

```xml
    <!-- <ProjectReference Include="..\modules\Starter.Module.Files\Starter.Module.Files.csproj" /> -->
```

```bash
cd boilerplateBE
dotnet build
```

Expected: BUILD SUCCEEDED. No compilation errors — core code doesn't reference the module.

- [ ] **Step 3: Restore module reference**

Uncomment the ProjectReference in `src/Starter.Api/Starter.Api.csproj`.

```bash
cd boilerplateBE
dotnet build
```

Expected: BUILD SUCCEEDED with module restored.

- [ ] **Step 4: Commit verification notes**

No code changes to commit — this was a verification-only task.

---

## Task 7: Create EF Core Migration for Module Architecture

**Files:**
- Create: new migration file in `src/Starter.Infrastructure/Persistence/Migrations/`

- [ ] **Step 1: Check if schema changed**

The module extraction should NOT change the database schema — same entities, same tables, same configurations. But the convention-based tenant filters may produce slightly different filter expressions. Generate a migration to verify:

```bash
cd boilerplateBE
dotnet ef migrations add ModuleArchitectureRefactor --project src/Starter.Infrastructure --startup-project src/Starter.Api
```

- [ ] **Step 2: Inspect the migration**

If the migration is empty (only `Up()` and `Down()` with no operations), that's ideal — delete it:

```bash
cd boilerplateBE
dotnet ef migrations remove --project src/Starter.Infrastructure --startup-project src/Starter.Api
```

If it has changes, review them carefully. The only expected changes would be query filter expression differences (which don't affect schema). If schema is unchanged, remove the migration.

- [ ] **Step 3: Commit if migration was needed**

Only if a non-empty migration was generated:

```bash
git add -A
git commit -m "chore: migration for module architecture tenant filter refactor"
```

---

## Verification Checklist

After all tasks are complete:

1. `dotnet build` — all projects compile with module present
2. Comment out Files module reference → `dotnet build` succeeds without it
3. `dotnet run` — API starts, Swagger shows Files endpoints
4. Files CRUD works: upload, list, download URL, delete
5. Permissions seeded correctly — Files permissions appear in database
6. Tenant isolation works — tenant users see only their files
7. No leftover `Starter.Application.Features.Files` namespace references in core projects
