# Tenant Feature Flags — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add platform-defined feature flags with per-tenant value overrides, cached in Redis, with full CRUD API, frontend management UI, and enforcement checks in existing handlers as reference implementations.

**Architecture:** Two entities — `FeatureFlag` (platform-level, no TenantId, no global query filter) and `TenantFeatureFlag` (per-tenant override with standard tenant filter). Resolution: tenant override → platform default. Cached per-tenant in Redis with 5-min TTL, busted on write. `IFeatureFlagService` provides a clean API for checking flags from any handler. Enforcement pattern: boolean gate (`IsEnabledAsync`) for feature toggles, quota check (`GetValueAsync<int>`) for limits. Frontend: standalone `/feature-flags` route with platform admin view (CRUD + overrides) and tenant admin view (read-only + toggle non-system booleans).

**Tech Stack:** .NET 10, EF Core/PostgreSQL, Redis (ICacheService), MediatR, React 19, TanStack Query, shadcn/ui, i18next

### System Ownership Boundary (Feature Flags ↔ Billing)

**Feature Flags owns:**
- Flag definitions (`feature_flags` table) and per-tenant overrides (`tenant_feature_flags` table)
- Resolution logic (tenant override → platform default), Redis caching, invalidation
- `IFeatureFlagService` API consumed by all handlers
- Enforcement guard pattern in command handlers (boolean gates + quota checks)
- Seed data for all system-level flags

**Billing/Subscriptions will own (documented here as contract for the next feature):**
- Plans with entitlements (`SubscriptionPlan.Features` JSON mapping flag keys to values)
- `SubscriptionChangedEvent` handler that calls `SetTenantOverrideCommand` to sync plan entitlements to `tenant_feature_flags`
- An `OverrideSource` enum (`Manual`, `PlanSubscription`) added to `TenantFeatureFlag` via migration — plan changes only overwrite plan-sourced values, preserving manual tenant overrides
- Usage tracking counters for dashboard display (Redis atomic counters as upgrade from COUNT queries)
- "Upgrade your plan" prompts when limits are approached

**Race condition strategy:** Optimistic — accept small overages on concurrent requests, check on next request. Documented upgrade path to Redis atomic counters when Billing arrives.

---

## File Map

**Backend — New files:**
- `Starter.Domain/FeatureFlags/Entities/FeatureFlag.cs`
- `Starter.Domain/FeatureFlags/Entities/TenantFeatureFlag.cs`
- `Starter.Domain/FeatureFlags/Enums/FlagValueType.cs`
- `Starter.Domain/FeatureFlags/Errors/FeatureFlagErrors.cs`
- `Starter.Application/Common/Interfaces/IFeatureFlagService.cs`
- `Starter.Application/Features/FeatureFlags/DTOs/FeatureFlagDto.cs`
- `Starter.Application/Features/FeatureFlags/DTOs/FeatureFlagMapper.cs`
- `Starter.Application/Features/FeatureFlags/Commands/CreateFeatureFlag/CreateFeatureFlagCommand.cs`
- `Starter.Application/Features/FeatureFlags/Commands/CreateFeatureFlag/CreateFeatureFlagCommandHandler.cs`
- `Starter.Application/Features/FeatureFlags/Commands/CreateFeatureFlag/CreateFeatureFlagCommandValidator.cs`
- `Starter.Application/Features/FeatureFlags/Commands/UpdateFeatureFlag/UpdateFeatureFlagCommand.cs`
- `Starter.Application/Features/FeatureFlags/Commands/UpdateFeatureFlag/UpdateFeatureFlagCommandHandler.cs`
- `Starter.Application/Features/FeatureFlags/Commands/UpdateFeatureFlag/UpdateFeatureFlagCommandValidator.cs`
- `Starter.Application/Features/FeatureFlags/Commands/DeleteFeatureFlag/DeleteFeatureFlagCommand.cs`
- `Starter.Application/Features/FeatureFlags/Commands/DeleteFeatureFlag/DeleteFeatureFlagCommandHandler.cs`
- `Starter.Application/Features/FeatureFlags/Commands/SetTenantOverride/SetTenantOverrideCommand.cs`
- `Starter.Application/Features/FeatureFlags/Commands/SetTenantOverride/SetTenantOverrideCommandHandler.cs`
- `Starter.Application/Features/FeatureFlags/Commands/SetTenantOverride/SetTenantOverrideCommandValidator.cs`
- `Starter.Application/Features/FeatureFlags/Commands/RemoveTenantOverride/RemoveTenantOverrideCommand.cs`
- `Starter.Application/Features/FeatureFlags/Commands/RemoveTenantOverride/RemoveTenantOverrideCommandHandler.cs`
- `Starter.Application/Features/FeatureFlags/Queries/GetFeatureFlags/GetFeatureFlagsQuery.cs`
- `Starter.Application/Features/FeatureFlags/Queries/GetFeatureFlags/GetFeatureFlagsQueryHandler.cs`
- `Starter.Application/Features/FeatureFlags/Queries/GetFeatureFlagByKey/GetFeatureFlagByKeyQuery.cs`
- `Starter.Application/Features/FeatureFlags/Queries/GetFeatureFlagByKey/GetFeatureFlagByKeyQueryHandler.cs`
- `Starter.Infrastructure/Persistence/Configurations/FeatureFlagConfiguration.cs`
- `Starter.Infrastructure/Persistence/Configurations/TenantFeatureFlagConfiguration.cs`
- `Starter.Infrastructure/Services/FeatureFlagService.cs`
- `Starter.Api/Controllers/FeatureFlagsController.cs`

**Backend — Modify:**
- `Starter.Shared/Constants/Permissions.cs` — Add `FeatureFlags` permission class
- `Starter.Shared/Constants/Roles.cs` — Assign permissions to Admin role
- `Starter.Application/Common/Interfaces/IApplicationDbContext.cs` — Add 2 DbSets
- `Starter.Infrastructure/Persistence/ApplicationDbContext.cs` — Add 2 DbSets + query filter for TenantFeatureFlag only (FeatureFlag gets NO filter)
- `Starter.Infrastructure/DependencyInjection.cs` — Register `IFeatureFlagService`
- `Starter.Infrastructure/Persistence/Seeds/DataSeeder.cs` — Seed ALL system feature flags (11 flags)
- `Starter.Application/Features/Users/Commands/CreateUser/CreateUserCommandHandler.cs` — Add `users.max_count` quota enforcement
- `Starter.Application/Features/Files/Commands/UploadFile/UploadFileCommandHandler.cs` — Add `files.max_upload_size_mb` + `files.max_storage_mb` quota enforcement
- `Starter.Application/Features/ApiKeys/Commands/CreateApiKey/CreateApiKeyCommandHandler.cs` — Add `api_keys.max_count` quota + `api_keys.enabled` gate enforcement

**Documentation — New:**
- `boilerplateBE/docs/feature-flags.md` — Developer guide: adding flags, enforcement patterns, cache behavior, Billing integration contract

**Frontend — New files:**
- `boilerplateFE/src/features/feature-flags/api/feature-flags.api.ts`
- `boilerplateFE/src/features/feature-flags/api/feature-flags.queries.ts`
- `boilerplateFE/src/features/feature-flags/api/index.ts`
- `boilerplateFE/src/features/feature-flags/pages/FeatureFlagsPage.tsx`
- `boilerplateFE/src/features/feature-flags/components/FeatureFlagsList.tsx`
- `boilerplateFE/src/features/feature-flags/components/CreateFeatureFlagDialog.tsx`
- `boilerplateFE/src/features/feature-flags/components/EditFeatureFlagDialog.tsx`
- `boilerplateFE/src/features/feature-flags/components/TenantOverrideDialog.tsx`
- `boilerplateFE/src/hooks/useFeatureFlag.ts`

**Frontend — Modify:**
- `boilerplateFE/src/constants/permissions.ts` — Add FeatureFlags permissions
- `boilerplateFE/src/config/api.config.ts` — Add FEATURE_FLAGS endpoints
- `boilerplateFE/src/config/routes.config.ts` — Add FEATURE_FLAGS route
- `boilerplateFE/src/lib/query/keys.ts` — Add featureFlags query keys
- `boilerplateFE/src/routes/routes.tsx` — Add feature-flags route
- `boilerplateFE/src/components/layout/MainLayout/Sidebar.tsx` — Add nav item
- `boilerplateFE/src/i18n/locales/en/translation.json` — Add featureFlags keys
- `boilerplateFE/src/i18n/locales/ar/translation.json` — Add featureFlags keys
- `boilerplateFE/src/i18n/locales/ku/translation.json` — Add featureFlags keys

---

## Task 1: Domain Entities, Enum & Errors

**Files:**
- Create: `boilerplateBE/src/Starter.Domain/FeatureFlags/Enums/FlagValueType.cs`
- Create: `boilerplateBE/src/Starter.Domain/FeatureFlags/Entities/FeatureFlag.cs`
- Create: `boilerplateBE/src/Starter.Domain/FeatureFlags/Entities/TenantFeatureFlag.cs`
- Create: `boilerplateBE/src/Starter.Domain/FeatureFlags/Errors/FeatureFlagErrors.cs`

- [ ] **Step 1: Create the FlagValueType enum**

Create `boilerplateBE/src/Starter.Domain/FeatureFlags/Enums/FlagValueType.cs`:

```csharp
namespace Starter.Domain.FeatureFlags.Enums;

public enum FlagValueType
{
    Boolean = 0,
    String = 1,
    Integer = 2,
    Json = 3
}
```

- [ ] **Step 2: Create the FeatureFlag entity**

Create `boilerplateBE/src/Starter.Domain/FeatureFlags/Entities/FeatureFlag.cs`:

```csharp
using Starter.Domain.Common;
using Starter.Domain.FeatureFlags.Enums;

namespace Starter.Domain.FeatureFlags.Entities;

public sealed class FeatureFlag : AggregateRoot
{
    public string Key { get; private set; } = default!;
    public string Name { get; private set; } = default!;
    public string? Description { get; private set; }
    public string DefaultValue { get; private set; } = default!;
    public FlagValueType ValueType { get; private set; }
    public string? Category { get; private set; }
    public bool IsSystem { get; private set; }

    private readonly List<TenantFeatureFlag> _tenantOverrides = [];
    public IReadOnlyCollection<TenantFeatureFlag> TenantOverrides => _tenantOverrides.AsReadOnly();

    private FeatureFlag() { }

    private FeatureFlag(
        Guid id,
        string key,
        string name,
        string? description,
        string defaultValue,
        FlagValueType valueType,
        string? category,
        bool isSystem) : base(id)
    {
        Key = key;
        Name = name;
        Description = description;
        DefaultValue = defaultValue;
        ValueType = valueType;
        Category = category;
        IsSystem = isSystem;
    }

    public static FeatureFlag Create(
        string key,
        string name,
        string? description,
        string defaultValue,
        FlagValueType valueType,
        string? category,
        bool isSystem)
    {
        return new FeatureFlag(
            Guid.NewGuid(),
            key.Trim().ToLowerInvariant(),
            name.Trim(),
            description?.Trim(),
            defaultValue,
            valueType,
            category?.Trim(),
            isSystem);
    }

    public void Update(string name, string? description, string defaultValue, string? category)
    {
        Name = name.Trim();
        Description = description?.Trim();
        DefaultValue = defaultValue;
        Category = category?.Trim();
        ModifiedAt = DateTime.UtcNow;
    }
}
```

- [ ] **Step 3: Create the TenantFeatureFlag entity**

Create `boilerplateBE/src/Starter.Domain/FeatureFlags/Entities/TenantFeatureFlag.cs`:

```csharp
using Starter.Domain.Common;

namespace Starter.Domain.FeatureFlags.Entities;

public sealed class TenantFeatureFlag : BaseEntity
{
    public Guid TenantId { get; private set; }
    public Guid FeatureFlagId { get; private set; }
    public string Value { get; private set; } = default!;

    public FeatureFlag FeatureFlag { get; private set; } = default!;

    private TenantFeatureFlag() { }

    private TenantFeatureFlag(Guid id, Guid tenantId, Guid featureFlagId, string value) : base(id)
    {
        TenantId = tenantId;
        FeatureFlagId = featureFlagId;
        Value = value;
    }

    public static TenantFeatureFlag Create(Guid tenantId, Guid featureFlagId, string value)
    {
        return new TenantFeatureFlag(Guid.NewGuid(), tenantId, featureFlagId, value);
    }

    public void UpdateValue(string value)
    {
        Value = value;
        ModifiedAt = DateTime.UtcNow;
    }
}
```

- [ ] **Step 4: Create error constants**

Create `boilerplateBE/src/Starter.Domain/FeatureFlags/Errors/FeatureFlagErrors.cs`:

```csharp
using Starter.Shared.Results;

namespace Starter.Domain.FeatureFlags.Errors;

public static class FeatureFlagErrors
{
    public static readonly Error NotFound = Error.NotFound(
        "FeatureFlag.NotFound",
        "The specified feature flag was not found.");

    public static readonly Error KeyAlreadyExists = Error.Conflict(
        "FeatureFlag.KeyAlreadyExists",
        "A feature flag with this key already exists.");

    public static readonly Error CannotDeleteSystemFlag = Error.Validation(
        "FeatureFlag.CannotDeleteSystemFlag",
        "System feature flags cannot be deleted.");

    public static readonly Error OverrideNotFound = Error.NotFound(
        "FeatureFlag.OverrideNotFound",
        "No tenant override found for this feature flag.");

    public static readonly Error InvalidValueForType = Error.Validation(
        "FeatureFlag.InvalidValueForType",
        "The provided value is not valid for the flag's value type.");

    public static Error FeatureDisabled(string feature) => Error.Validation(
        "FeatureFlag.FeatureDisabled",
        $"The feature '{feature}' is not enabled for your tenant.");

    public static Error QuotaExceeded(string resource, int limit) => Error.Validation(
        "FeatureFlag.QuotaExceeded",
        $"Quota exceeded: maximum {limit} {resource} allowed for your tenant.");
}
```

- [ ] **Step 5: Verify build**

Run: `cd boilerplateBE && dotnet build --verbosity quiet`
Expected: 0 errors

- [ ] **Step 6: Commit**

```bash
git add boilerplateBE/src/Starter.Domain/FeatureFlags/
git commit -m "feat(feature-flags): add domain entities, enum, and errors"
```

---

## Task 2: Permissions & Roles

**Files:**
- Modify: `boilerplateBE/src/Starter.Shared/Constants/Permissions.cs`
- Modify: `boilerplateBE/src/Starter.Shared/Constants/Roles.cs`
- Modify: `boilerplateFE/src/constants/permissions.ts`

- [ ] **Step 1: Add FeatureFlags permission class to backend**

In `Permissions.cs`, add a new nested class alongside the existing `ApiKeys` class:

```csharp
public static class FeatureFlags
{
    public const string View = "FeatureFlags.View";
    public const string Create = "FeatureFlags.Create";
    public const string Update = "FeatureFlags.Update";
    public const string Delete = "FeatureFlags.Delete";
    public const string ManageTenantOverrides = "FeatureFlags.ManageTenantOverrides";
}
```

Also add these 5 entries to `GetAllWithMetadata()`:

```csharp
(FeatureFlags.View, "View feature flags", "FeatureFlags"),
(FeatureFlags.Create, "Create feature flags", "FeatureFlags"),
(FeatureFlags.Update, "Update feature flags", "FeatureFlags"),
(FeatureFlags.Delete, "Delete feature flags", "FeatureFlags"),
(FeatureFlags.ManageTenantOverrides, "Manage tenant feature flag overrides", "FeatureFlags"),
```

- [ ] **Step 2: Assign permissions to Admin role**

In `Roles.cs`, add to the Admin permission array:

```csharp
Permissions.FeatureFlags.View,
```

SuperAdmin already gets everything via `Permissions.GetAll()`. Admin should only view flags. Platform override management stays SuperAdmin-only.

- [ ] **Step 3: Add frontend permissions**

In `boilerplateFE/src/constants/permissions.ts`, add:

```typescript
FeatureFlags: {
  View: 'FeatureFlags.View',
  Create: 'FeatureFlags.Create',
  Update: 'FeatureFlags.Update',
  Delete: 'FeatureFlags.Delete',
  ManageTenantOverrides: 'FeatureFlags.ManageTenantOverrides',
},
```

- [ ] **Step 4: Verify build**

Run: `cd boilerplateBE && dotnet build --verbosity quiet`
Expected: 0 errors

- [ ] **Step 5: Commit**

```bash
git add boilerplateBE/src/Starter.Shared/Constants/Permissions.cs \
       boilerplateBE/src/Starter.Shared/Constants/Roles.cs \
       boilerplateFE/src/constants/permissions.ts
git commit -m "feat(feature-flags): add permissions and role assignments"
```

---

## Task 3: EF Core Configuration, DbContext & Migration

**Files:**
- Create: `boilerplateBE/src/Starter.Infrastructure/Persistence/Configurations/FeatureFlagConfiguration.cs`
- Create: `boilerplateBE/src/Starter.Infrastructure/Persistence/Configurations/TenantFeatureFlagConfiguration.cs`
- Modify: `boilerplateBE/src/Starter.Application/Common/Interfaces/IApplicationDbContext.cs`
- Modify: `boilerplateBE/src/Starter.Infrastructure/Persistence/ApplicationDbContext.cs`

- [ ] **Step 1: Create FeatureFlagConfiguration**

Create `boilerplateBE/src/Starter.Infrastructure/Persistence/Configurations/FeatureFlagConfiguration.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Starter.Domain.FeatureFlags.Entities;

namespace Starter.Infrastructure.Persistence.Configurations;

internal sealed class FeatureFlagConfiguration : IEntityTypeConfiguration<FeatureFlag>
{
    public void Configure(EntityTypeBuilder<FeatureFlag> builder)
    {
        builder.ToTable("feature_flags");

        builder.HasKey(f => f.Id);

        builder.Property(f => f.Key)
            .HasColumnName("key")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(f => f.Name)
            .HasColumnName("name")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(f => f.Description)
            .HasColumnName("description")
            .HasMaxLength(1000);

        builder.Property(f => f.DefaultValue)
            .HasColumnName("default_value")
            .HasMaxLength(4000)
            .IsRequired();

        builder.Property(f => f.ValueType)
            .HasColumnName("value_type")
            .IsRequired();

        builder.Property(f => f.Category)
            .HasColumnName("category")
            .HasMaxLength(100);

        builder.Property(f => f.IsSystem)
            .HasColumnName("is_system")
            .IsRequired();

        builder.HasIndex(f => f.Key)
            .IsUnique();

        builder.HasIndex(f => f.Category);

        builder.HasMany(f => f.TenantOverrides)
            .WithOne(t => t.FeatureFlag)
            .HasForeignKey(t => t.FeatureFlagId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
```

- [ ] **Step 2: Create TenantFeatureFlagConfiguration**

Create `boilerplateBE/src/Starter.Infrastructure/Persistence/Configurations/TenantFeatureFlagConfiguration.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Starter.Domain.FeatureFlags.Entities;

namespace Starter.Infrastructure.Persistence.Configurations;

internal sealed class TenantFeatureFlagConfiguration : IEntityTypeConfiguration<TenantFeatureFlag>
{
    public void Configure(EntityTypeBuilder<TenantFeatureFlag> builder)
    {
        builder.ToTable("tenant_feature_flags");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.TenantId)
            .HasColumnName("tenant_id")
            .IsRequired();

        builder.Property(t => t.FeatureFlagId)
            .HasColumnName("feature_flag_id")
            .IsRequired();

        builder.Property(t => t.Value)
            .HasColumnName("value")
            .HasMaxLength(4000)
            .IsRequired();

        builder.HasIndex(t => new { t.TenantId, t.FeatureFlagId })
            .IsUnique();

        builder.HasIndex(t => t.TenantId);
    }
}
```

- [ ] **Step 3: Add DbSets to IApplicationDbContext**

In `IApplicationDbContext.cs`, add:

```csharp
DbSet<FeatureFlag> FeatureFlags { get; }
DbSet<TenantFeatureFlag> TenantFeatureFlags { get; }
```

Add using: `using Starter.Domain.FeatureFlags.Entities;`

- [ ] **Step 4: Add DbSets and query filter to ApplicationDbContext**

In `ApplicationDbContext.cs`, add DbSet properties:

```csharp
public DbSet<FeatureFlag> FeatureFlags => Set<FeatureFlag>();
public DbSet<TenantFeatureFlag> TenantFeatureFlags => Set<TenantFeatureFlag>();
```

Add using: `using Starter.Domain.FeatureFlags.Entities;`

In `OnModelCreating()`, add query filter for `TenantFeatureFlag` ONLY (FeatureFlag is platform-level, NO filter):

```csharp
modelBuilder.Entity<TenantFeatureFlag>().HasQueryFilter(t =>
    TenantId == null || t.TenantId == TenantId);
```

**Do NOT add a query filter for FeatureFlag** — it is a platform-level entity visible to all tenants.

- [ ] **Step 5: Verify build** (migration created during post-feature testing only)

Run: `cd boilerplateBE && dotnet build --verbosity quiet`
Expected: 0 errors

- [ ] **Step 7: Commit**

```bash
git add boilerplateBE/src/Starter.Infrastructure/Persistence/Configurations/FeatureFlagConfiguration.cs \
       boilerplateBE/src/Starter.Infrastructure/Persistence/Configurations/TenantFeatureFlagConfiguration.cs \
       boilerplateBE/src/Starter.Application/Common/Interfaces/IApplicationDbContext.cs \
       boilerplateBE/src/Starter.Infrastructure/Persistence/ApplicationDbContext.cs \
       boilerplateBE/src/Starter.Infrastructure/Persistence/Migrations/
git commit -m "feat(feature-flags): add EF Core configuration, DbContext, and migration"
```

---

## Task 4: DTOs, Mapper & IFeatureFlagService

**Files:**
- Create: `boilerplateBE/src/Starter.Application/Features/FeatureFlags/DTOs/FeatureFlagDto.cs`
- Create: `boilerplateBE/src/Starter.Application/Features/FeatureFlags/DTOs/FeatureFlagMapper.cs`
- Create: `boilerplateBE/src/Starter.Application/Common/Interfaces/IFeatureFlagService.cs`

- [ ] **Step 1: Create the DTO**

Create `boilerplateBE/src/Starter.Application/Features/FeatureFlags/DTOs/FeatureFlagDto.cs`:

```csharp
using Starter.Domain.FeatureFlags.Enums;

namespace Starter.Application.Features.FeatureFlags.DTOs;

public sealed record FeatureFlagDto(
    Guid Id,
    string Key,
    string Name,
    string? Description,
    string DefaultValue,
    FlagValueType ValueType,
    string? Category,
    bool IsSystem,
    string? TenantOverrideValue,
    string ResolvedValue,
    DateTime CreatedAt,
    DateTime? ModifiedAt);
```

- [ ] **Step 2: Create the mapper**

Create `boilerplateBE/src/Starter.Application/Features/FeatureFlags/DTOs/FeatureFlagMapper.cs`:

```csharp
using Starter.Domain.FeatureFlags.Entities;

namespace Starter.Application.Features.FeatureFlags.DTOs;

public static class FeatureFlagMapper
{
    public static FeatureFlagDto ToDto(this FeatureFlag entity, string? tenantOverrideValue = null)
    {
        return new FeatureFlagDto(
            Id: entity.Id,
            Key: entity.Key,
            Name: entity.Name,
            Description: entity.Description,
            DefaultValue: entity.DefaultValue,
            ValueType: entity.ValueType,
            Category: entity.Category,
            IsSystem: entity.IsSystem,
            TenantOverrideValue: tenantOverrideValue,
            ResolvedValue: tenantOverrideValue ?? entity.DefaultValue,
            CreatedAt: entity.CreatedAt,
            ModifiedAt: entity.ModifiedAt);
    }
}
```

- [ ] **Step 3: Create IFeatureFlagService**

Create `boilerplateBE/src/Starter.Application/Common/Interfaces/IFeatureFlagService.cs`:

```csharp
namespace Starter.Application.Common.Interfaces;

public interface IFeatureFlagService
{
    Task<bool> IsEnabledAsync(string key, CancellationToken cancellationToken = default);
    Task<T> GetValueAsync<T>(string key, CancellationToken cancellationToken = default);
    Task<Dictionary<string, string>> GetAllResolvedAsync(CancellationToken cancellationToken = default);
    Task InvalidateCacheAsync(Guid? tenantId = null, CancellationToken cancellationToken = default);
}
```

- [ ] **Step 4: Verify build**

Run: `cd boilerplateBE && dotnet build --verbosity quiet`
Expected: 0 errors

- [ ] **Step 5: Commit**

```bash
git add boilerplateBE/src/Starter.Application/Features/FeatureFlags/DTOs/ \
       boilerplateBE/src/Starter.Application/Common/Interfaces/IFeatureFlagService.cs
git commit -m "feat(feature-flags): add DTOs, mapper, and IFeatureFlagService interface"
```

---

## Task 5: FeatureFlagService Implementation (Redis-Cached Resolution)

**Files:**
- Create: `boilerplateBE/src/Starter.Infrastructure/Services/FeatureFlagService.cs`
- Modify: `boilerplateBE/src/Starter.Infrastructure/DependencyInjection.cs`

- [ ] **Step 1: Implement FeatureFlagService**

Create `boilerplateBE/src/Starter.Infrastructure/Services/FeatureFlagService.cs`:

```csharp
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Starter.Application.Common.Interfaces;

namespace Starter.Infrastructure.Services;

internal sealed class FeatureFlagService(
    IApplicationDbContext context,
    ICacheService cache,
    ICurrentUserService currentUser) : IFeatureFlagService
{
    private const string CachePrefix = "ff";
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);

    public async Task<bool> IsEnabledAsync(string key, CancellationToken cancellationToken = default)
    {
        var value = await GetResolvedValueAsync(key, cancellationToken);
        return bool.TryParse(value, out var result) && result;
    }

    public async Task<T> GetValueAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        var value = await GetResolvedValueAsync(key, cancellationToken);
        return JsonSerializer.Deserialize<T>(value)!;
    }

    public async Task<Dictionary<string, string>> GetAllResolvedAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = currentUser.TenantId;
        var cacheKey = tenantId.HasValue ? $"{CachePrefix}:{tenantId}" : $"{CachePrefix}:platform";

        return await cache.GetOrSetAsync(
            cacheKey,
            async () => await BuildResolvedMapAsync(tenantId, cancellationToken),
            CacheTtl,
            cancellationToken);
    }

    public async Task InvalidateCacheAsync(Guid? tenantId = null, CancellationToken cancellationToken = default)
    {
        await cache.RemoveByPrefixAsync(CachePrefix, cancellationToken);
    }

    private async Task<string> GetResolvedValueAsync(string key, CancellationToken cancellationToken)
    {
        var allFlags = await GetAllResolvedAsync(cancellationToken);
        return allFlags.TryGetValue(key, out var value)
            ? value
            : throw new KeyNotFoundException($"Feature flag '{key}' not found.");
    }

    private async Task<Dictionary<string, string>> BuildResolvedMapAsync(
        Guid? tenantId, CancellationToken cancellationToken)
    {
        var flags = await context.FeatureFlags
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var overrides = tenantId.HasValue
            ? await context.TenantFeatureFlags
                .AsNoTracking()
                .Where(t => t.TenantId == tenantId.Value)
                .ToDictionaryAsync(t => t.FeatureFlagId, t => t.Value, cancellationToken)
            : new Dictionary<Guid, string>();

        return flags.ToDictionary(
            f => f.Key,
            f => overrides.TryGetValue(f.Id, out var ov) ? ov : f.DefaultValue);
    }
}
```

- [ ] **Step 2: Register in DI**

In `DependencyInjection.cs`, in the `AddServices()` method, add:

```csharp
services.AddScoped<IFeatureFlagService, FeatureFlagService>();
```

Add using: `using Starter.Infrastructure.Services;` (if not already present)

- [ ] **Step 3: Verify build**

Run: `cd boilerplateBE && dotnet build --verbosity quiet`
Expected: 0 errors

- [ ] **Step 4: Commit**

```bash
git add boilerplateBE/src/Starter.Infrastructure/Services/FeatureFlagService.cs \
       boilerplateBE/src/Starter.Infrastructure/DependencyInjection.cs
git commit -m "feat(feature-flags): implement FeatureFlagService with Redis caching"
```

---

## Task 6: CQRS Commands (Create, Update, Delete, SetOverride, RemoveOverride)

**Files:**
- Create: All files under `boilerplateBE/src/Starter.Application/Features/FeatureFlags/Commands/`

- [ ] **Step 1: CreateFeatureFlag command, validator, handler**

Create `CreateFeatureFlagCommand.cs`:
```csharp
using MediatR;
using Starter.Domain.FeatureFlags.Enums;
using Starter.Shared.Results;

namespace Starter.Application.Features.FeatureFlags.Commands.CreateFeatureFlag;

public sealed record CreateFeatureFlagCommand(
    string Key,
    string Name,
    string? Description,
    string DefaultValue,
    FlagValueType ValueType,
    string? Category,
    bool IsSystem) : IRequest<Result<Guid>>;
```

Create `CreateFeatureFlagCommandValidator.cs`:
```csharp
using FluentValidation;

namespace Starter.Application.Features.FeatureFlags.Commands.CreateFeatureFlag;

public sealed class CreateFeatureFlagCommandValidator : AbstractValidator<CreateFeatureFlagCommand>
{
    public CreateFeatureFlagCommandValidator()
    {
        RuleFor(x => x.Key)
            .NotEmpty().WithMessage("Key is required.")
            .MaximumLength(200).WithMessage("Key must not exceed 200 characters.")
            .Matches(@"^[a-z0-9_.]+$").WithMessage("Key must be lowercase alphanumeric with dots and underscores only.");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required.")
            .MaximumLength(200).WithMessage("Name must not exceed 200 characters.");

        RuleFor(x => x.Description)
            .MaximumLength(1000).WithMessage("Description must not exceed 1000 characters.");

        RuleFor(x => x.DefaultValue)
            .NotEmpty().WithMessage("Default value is required.")
            .MaximumLength(4000).WithMessage("Default value must not exceed 4000 characters.");

        RuleFor(x => x.Category)
            .MaximumLength(100).WithMessage("Category must not exceed 100 characters.");
    }
}
```

Create `CreateFeatureFlagCommandHandler.cs`:
```csharp
using MediatR;
using Microsoft.EntityFrameworkCore;
using Starter.Application.Common.Interfaces;
using Starter.Domain.FeatureFlags.Entities;
using Starter.Domain.FeatureFlags.Errors;
using Starter.Shared.Results;

namespace Starter.Application.Features.FeatureFlags.Commands.CreateFeatureFlag;

internal sealed class CreateFeatureFlagCommandHandler(
    IApplicationDbContext context) : IRequestHandler<CreateFeatureFlagCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(CreateFeatureFlagCommand request, CancellationToken cancellationToken)
    {
        var keyExists = await context.FeatureFlags
            .AnyAsync(f => f.Key == request.Key.Trim().ToLowerInvariant(), cancellationToken);

        if (keyExists)
            return Result.Failure<Guid>(FeatureFlagErrors.KeyAlreadyExists);

        var flag = FeatureFlag.Create(
            request.Key,
            request.Name,
            request.Description,
            request.DefaultValue,
            request.ValueType,
            request.Category,
            request.IsSystem);

        context.FeatureFlags.Add(flag);
        await context.SaveChangesAsync(cancellationToken);

        return Result.Success(flag.Id);
    }
}
```

- [ ] **Step 2: UpdateFeatureFlag command, validator, handler**

Create `UpdateFeatureFlagCommand.cs`:
```csharp
using MediatR;
using Starter.Shared.Results;

namespace Starter.Application.Features.FeatureFlags.Commands.UpdateFeatureFlag;

public sealed record UpdateFeatureFlagCommand(
    Guid Id,
    string Name,
    string? Description,
    string DefaultValue,
    string? Category) : IRequest<Result>;
```

Create `UpdateFeatureFlagCommandValidator.cs`:
```csharp
using FluentValidation;

namespace Starter.Application.Features.FeatureFlags.Commands.UpdateFeatureFlag;

public sealed class UpdateFeatureFlagCommandValidator : AbstractValidator<UpdateFeatureFlagCommand>
{
    public UpdateFeatureFlagCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(1000);
        RuleFor(x => x.DefaultValue).NotEmpty().MaximumLength(4000);
        RuleFor(x => x.Category).MaximumLength(100);
    }
}
```

Create `UpdateFeatureFlagCommandHandler.cs`:
```csharp
using MediatR;
using Microsoft.EntityFrameworkCore;
using Starter.Application.Common.Interfaces;
using Starter.Domain.FeatureFlags.Errors;
using Starter.Shared.Results;

namespace Starter.Application.Features.FeatureFlags.Commands.UpdateFeatureFlag;

internal sealed class UpdateFeatureFlagCommandHandler(
    IApplicationDbContext context,
    IFeatureFlagService featureFlagService) : IRequestHandler<UpdateFeatureFlagCommand, Result>
{
    public async Task<Result> Handle(UpdateFeatureFlagCommand request, CancellationToken cancellationToken)
    {
        var flag = await context.FeatureFlags
            .FirstOrDefaultAsync(f => f.Id == request.Id, cancellationToken);

        if (flag is null)
            return Result.Failure(FeatureFlagErrors.NotFound);

        flag.Update(request.Name, request.Description, request.DefaultValue, request.Category);
        await context.SaveChangesAsync(cancellationToken);
        await featureFlagService.InvalidateCacheAsync(cancellationToken: cancellationToken);

        return Result.Success();
    }
}
```

- [ ] **Step 3: DeleteFeatureFlag command and handler**

Create `DeleteFeatureFlagCommand.cs`:
```csharp
using MediatR;
using Starter.Shared.Results;

namespace Starter.Application.Features.FeatureFlags.Commands.DeleteFeatureFlag;

public sealed record DeleteFeatureFlagCommand(Guid Id) : IRequest<Result>;
```

Create `DeleteFeatureFlagCommandHandler.cs`:
```csharp
using MediatR;
using Microsoft.EntityFrameworkCore;
using Starter.Application.Common.Interfaces;
using Starter.Domain.FeatureFlags.Errors;
using Starter.Shared.Results;

namespace Starter.Application.Features.FeatureFlags.Commands.DeleteFeatureFlag;

internal sealed class DeleteFeatureFlagCommandHandler(
    IApplicationDbContext context,
    IFeatureFlagService featureFlagService) : IRequestHandler<DeleteFeatureFlagCommand, Result>
{
    public async Task<Result> Handle(DeleteFeatureFlagCommand request, CancellationToken cancellationToken)
    {
        var flag = await context.FeatureFlags
            .FirstOrDefaultAsync(f => f.Id == request.Id, cancellationToken);

        if (flag is null)
            return Result.Failure(FeatureFlagErrors.NotFound);

        if (flag.IsSystem)
            return Result.Failure(FeatureFlagErrors.CannotDeleteSystemFlag);

        context.FeatureFlags.Remove(flag);
        await context.SaveChangesAsync(cancellationToken);
        await featureFlagService.InvalidateCacheAsync(cancellationToken: cancellationToken);

        return Result.Success();
    }
}
```

- [ ] **Step 4: SetTenantOverride command, validator, handler**

Create `SetTenantOverrideCommand.cs`:
```csharp
using MediatR;
using Starter.Shared.Results;

namespace Starter.Application.Features.FeatureFlags.Commands.SetTenantOverride;

public sealed record SetTenantOverrideCommand(
    Guid FeatureFlagId,
    Guid TenantId,
    string Value) : IRequest<Result>;
```

Create `SetTenantOverrideCommandValidator.cs`:
```csharp
using FluentValidation;

namespace Starter.Application.Features.FeatureFlags.Commands.SetTenantOverride;

public sealed class SetTenantOverrideCommandValidator : AbstractValidator<SetTenantOverrideCommand>
{
    public SetTenantOverrideCommandValidator()
    {
        RuleFor(x => x.FeatureFlagId).NotEmpty();
        RuleFor(x => x.TenantId).NotEmpty();
        RuleFor(x => x.Value).NotEmpty().MaximumLength(4000);
    }
}
```

Create `SetTenantOverrideCommandHandler.cs`:
```csharp
using MediatR;
using Microsoft.EntityFrameworkCore;
using Starter.Application.Common.Interfaces;
using Starter.Domain.FeatureFlags.Entities;
using Starter.Domain.FeatureFlags.Errors;
using Starter.Shared.Results;

namespace Starter.Application.Features.FeatureFlags.Commands.SetTenantOverride;

internal sealed class SetTenantOverrideCommandHandler(
    IApplicationDbContext context,
    IFeatureFlagService featureFlagService) : IRequestHandler<SetTenantOverrideCommand, Result>
{
    public async Task<Result> Handle(SetTenantOverrideCommand request, CancellationToken cancellationToken)
    {
        var flagExists = await context.FeatureFlags
            .AnyAsync(f => f.Id == request.FeatureFlagId, cancellationToken);

        if (!flagExists)
            return Result.Failure(FeatureFlagErrors.NotFound);

        var existing = await context.TenantFeatureFlags
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(t =>
                t.FeatureFlagId == request.FeatureFlagId && t.TenantId == request.TenantId,
                cancellationToken);

        if (existing is not null)
        {
            existing.UpdateValue(request.Value);
        }
        else
        {
            var tenantOverride = TenantFeatureFlag.Create(
                request.TenantId, request.FeatureFlagId, request.Value);
            context.TenantFeatureFlags.Add(tenantOverride);
        }

        await context.SaveChangesAsync(cancellationToken);
        await featureFlagService.InvalidateCacheAsync(request.TenantId, cancellationToken);

        return Result.Success();
    }
}
```

- [ ] **Step 5: RemoveTenantOverride command and handler**

Create `RemoveTenantOverrideCommand.cs`:
```csharp
using MediatR;
using Starter.Shared.Results;

namespace Starter.Application.Features.FeatureFlags.Commands.RemoveTenantOverride;

public sealed record RemoveTenantOverrideCommand(
    Guid FeatureFlagId,
    Guid TenantId) : IRequest<Result>;
```

Create `RemoveTenantOverrideCommandHandler.cs`:
```csharp
using MediatR;
using Microsoft.EntityFrameworkCore;
using Starter.Application.Common.Interfaces;
using Starter.Domain.FeatureFlags.Errors;
using Starter.Shared.Results;

namespace Starter.Application.Features.FeatureFlags.Commands.RemoveTenantOverride;

internal sealed class RemoveTenantOverrideCommandHandler(
    IApplicationDbContext context,
    IFeatureFlagService featureFlagService) : IRequestHandler<RemoveTenantOverrideCommand, Result>
{
    public async Task<Result> Handle(RemoveTenantOverrideCommand request, CancellationToken cancellationToken)
    {
        var existing = await context.TenantFeatureFlags
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(t =>
                t.FeatureFlagId == request.FeatureFlagId && t.TenantId == request.TenantId,
                cancellationToken);

        if (existing is null)
            return Result.Failure(FeatureFlagErrors.OverrideNotFound);

        context.TenantFeatureFlags.Remove(existing);
        await context.SaveChangesAsync(cancellationToken);
        await featureFlagService.InvalidateCacheAsync(request.TenantId, cancellationToken);

        return Result.Success();
    }
}
```

- [ ] **Step 6: Verify build**

Run: `cd boilerplateBE && dotnet build --verbosity quiet`
Expected: 0 errors

- [ ] **Step 7: Commit**

```bash
git add boilerplateBE/src/Starter.Application/Features/FeatureFlags/Commands/
git commit -m "feat(feature-flags): add CQRS commands (create, update, delete, set/remove override)"
```

---

## Task 7: CQRS Queries (GetAll, GetByKey)

**Files:**
- Create: All files under `boilerplateBE/src/Starter.Application/Features/FeatureFlags/Queries/`

- [ ] **Step 1: GetFeatureFlags query and handler**

Create `GetFeatureFlagsQuery.cs`:
```csharp
using MediatR;
using Starter.Application.Features.FeatureFlags.DTOs;
using Starter.Shared.Pagination;
using Starter.Shared.Results;

namespace Starter.Application.Features.FeatureFlags.Queries.GetFeatureFlags;

public sealed record GetFeatureFlagsQuery(
    int PageNumber = 1,
    int PageSize = 50,
    string? Category = null,
    string? Search = null) : IRequest<Result<PaginatedList<FeatureFlagDto>>>;
```

Create `GetFeatureFlagsQueryHandler.cs`:
```csharp
using MediatR;
using Microsoft.EntityFrameworkCore;
using Starter.Application.Common.Interfaces;
using Starter.Application.Features.FeatureFlags.DTOs;
using Starter.Shared.Pagination;
using Starter.Shared.Results;

namespace Starter.Application.Features.FeatureFlags.Queries.GetFeatureFlags;

internal sealed class GetFeatureFlagsQueryHandler(
    IApplicationDbContext context,
    ICurrentUserService currentUser) : IRequestHandler<GetFeatureFlagsQuery, Result<PaginatedList<FeatureFlagDto>>>
{
    public async Task<Result<PaginatedList<FeatureFlagDto>>> Handle(
        GetFeatureFlagsQuery request, CancellationToken cancellationToken)
    {
        var tenantId = currentUser.TenantId;

        var query = context.FeatureFlags.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.Category))
            query = query.Where(f => f.Category == request.Category);

        if (!string.IsNullOrWhiteSpace(request.Search))
            query = query.Where(f =>
                f.Key.Contains(request.Search) ||
                f.Name.Contains(request.Search));

        query = query.OrderBy(f => f.Category).ThenBy(f => f.Key);

        var totalCount = await query.CountAsync(cancellationToken);

        var flags = await query
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        // Load tenant overrides if we have a tenant context
        var overrideMap = new Dictionary<Guid, string>();
        if (tenantId.HasValue && flags.Count > 0)
        {
            var flagIds = flags.Select(f => f.Id).ToList();
            overrideMap = await context.TenantFeatureFlags
                .AsNoTracking()
                .Where(t => t.TenantId == tenantId.Value && flagIds.Contains(t.FeatureFlagId))
                .ToDictionaryAsync(t => t.FeatureFlagId, t => t.Value, cancellationToken);
        }

        var dtos = flags.Select(f =>
            f.ToDto(overrideMap.GetValueOrDefault(f.Id))).ToList();

        var paginatedList = new PaginatedList<FeatureFlagDto>(
            dtos, totalCount, request.PageNumber, request.PageSize);

        return Result.Success(paginatedList);
    }
}
```

- [ ] **Step 2: GetFeatureFlagByKey query and handler**

Create `GetFeatureFlagByKeyQuery.cs`:
```csharp
using MediatR;
using Starter.Application.Features.FeatureFlags.DTOs;
using Starter.Shared.Results;

namespace Starter.Application.Features.FeatureFlags.Queries.GetFeatureFlagByKey;

public sealed record GetFeatureFlagByKeyQuery(string Key) : IRequest<Result<FeatureFlagDto>>;
```

Create `GetFeatureFlagByKeyQueryHandler.cs`:
```csharp
using MediatR;
using Microsoft.EntityFrameworkCore;
using Starter.Application.Common.Interfaces;
using Starter.Application.Features.FeatureFlags.DTOs;
using Starter.Domain.FeatureFlags.Errors;
using Starter.Shared.Results;

namespace Starter.Application.Features.FeatureFlags.Queries.GetFeatureFlagByKey;

internal sealed class GetFeatureFlagByKeyQueryHandler(
    IApplicationDbContext context,
    ICurrentUserService currentUser) : IRequestHandler<GetFeatureFlagByKeyQuery, Result<FeatureFlagDto>>
{
    public async Task<Result<FeatureFlagDto>> Handle(
        GetFeatureFlagByKeyQuery request, CancellationToken cancellationToken)
    {
        var flag = await context.FeatureFlags
            .AsNoTracking()
            .FirstOrDefaultAsync(f => f.Key == request.Key.Trim().ToLowerInvariant(), cancellationToken);

        if (flag is null)
            return Result.Failure<FeatureFlagDto>(FeatureFlagErrors.NotFound);

        string? overrideValue = null;
        var tenantId = currentUser.TenantId;
        if (tenantId.HasValue)
        {
            overrideValue = await context.TenantFeatureFlags
                .AsNoTracking()
                .Where(t => t.TenantId == tenantId.Value && t.FeatureFlagId == flag.Id)
                .Select(t => t.Value)
                .FirstOrDefaultAsync(cancellationToken);
        }

        return Result.Success(flag.ToDto(overrideValue));
    }
}
```

- [ ] **Step 3: Verify build**

Run: `cd boilerplateBE && dotnet build --verbosity quiet`
Expected: 0 errors

- [ ] **Step 4: Commit**

```bash
git add boilerplateBE/src/Starter.Application/Features/FeatureFlags/Queries/
git commit -m "feat(feature-flags): add CQRS queries (list with pagination, get by key)"
```

---

## Task 8: API Controller & Seed Data

**Files:**
- Create: `boilerplateBE/src/Starter.Api/Controllers/FeatureFlagsController.cs`
- Modify: `boilerplateBE/src/Starter.Infrastructure/Persistence/Seeds/DataSeeder.cs`

- [ ] **Step 1: Create the controller**

Create `boilerplateBE/src/Starter.Api/Controllers/FeatureFlagsController.cs`:

```csharp
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Starter.Application.Features.FeatureFlags.Commands.CreateFeatureFlag;
using Starter.Application.Features.FeatureFlags.Commands.DeleteFeatureFlag;
using Starter.Application.Features.FeatureFlags.Commands.RemoveTenantOverride;
using Starter.Application.Features.FeatureFlags.Commands.SetTenantOverride;
using Starter.Application.Features.FeatureFlags.Commands.UpdateFeatureFlag;
using Starter.Application.Features.FeatureFlags.Queries.GetFeatureFlagByKey;
using Starter.Application.Features.FeatureFlags.Queries.GetFeatureFlags;
using Starter.Shared.Constants;

namespace Starter.Api.Controllers;

public sealed class FeatureFlagsController(ISender mediator) : BaseApiController(mediator)
{
    [HttpGet]
    [Authorize(Policy = Permissions.FeatureFlags.View)]
    public async Task<IActionResult> GetAll(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 50,
        [FromQuery] string? category = null,
        [FromQuery] string? search = null,
        CancellationToken ct = default)
    {
        var result = await Mediator.Send(
            new GetFeatureFlagsQuery(pageNumber, pageSize, category, search), ct);
        return HandlePagedResult(result);
    }

    [HttpGet("{key}")]
    [Authorize(Policy = Permissions.FeatureFlags.View)]
    public async Task<IActionResult> GetByKey(string key, CancellationToken ct = default)
    {
        var result = await Mediator.Send(new GetFeatureFlagByKeyQuery(key), ct);
        return HandleResult(result);
    }

    [HttpPost]
    [Authorize(Policy = Permissions.FeatureFlags.Create)]
    public async Task<IActionResult> Create(
        CreateFeatureFlagCommand command, CancellationToken ct = default)
    {
        var result = await Mediator.Send(command, ct);
        return HandleCreatedResult(result, nameof(GetByKey));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = Permissions.FeatureFlags.Update)]
    public async Task<IActionResult> Update(
        Guid id, UpdateFeatureFlagCommand command, CancellationToken ct = default)
    {
        if (id != command.Id)
            return BadRequest();

        var result = await Mediator.Send(command, ct);
        return HandleResult(result);
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = Permissions.FeatureFlags.Delete)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct = default)
    {
        var result = await Mediator.Send(new DeleteFeatureFlagCommand(id), ct);
        return HandleResult(result);
    }

    [HttpPut("{id:guid}/tenants/{tenantId:guid}")]
    [Authorize(Policy = Permissions.FeatureFlags.ManageTenantOverrides)]
    public async Task<IActionResult> SetTenantOverride(
        Guid id, Guid tenantId, [FromBody] SetTenantOverrideRequest request, CancellationToken ct = default)
    {
        var result = await Mediator.Send(
            new SetTenantOverrideCommand(id, tenantId, request.Value), ct);
        return HandleResult(result);
    }

    [HttpDelete("{id:guid}/tenants/{tenantId:guid}")]
    [Authorize(Policy = Permissions.FeatureFlags.ManageTenantOverrides)]
    public async Task<IActionResult> RemoveTenantOverride(
        Guid id, Guid tenantId, CancellationToken ct = default)
    {
        var result = await Mediator.Send(
            new RemoveTenantOverrideCommand(id, tenantId), ct);
        return HandleResult(result);
    }
}

public sealed record SetTenantOverrideRequest(string Value);
```

- [ ] **Step 2: Add seed data for default feature flags**

In `DataSeeder.cs`, add a call to `SeedFeatureFlagsAsync(context, logger)` after the existing seed methods, and add the method:

```csharp
private static async Task SeedFeatureFlagsAsync(ApplicationDbContext context, ILogger logger)
{
    if (await context.FeatureFlags.AnyAsync())
    {
        logger.LogInformation("Feature flags already seeded");
        return;
    }

    var flags = new[]
    {
        // User limits
        FeatureFlag.Create("users.max_count", "Max Users", "Maximum number of users per tenant", "100", FlagValueType.Integer, "users", false),
        FeatureFlag.Create("users.invitations_enabled", "Invitations Enabled", "Allow sending user invitations", "true", FlagValueType.Boolean, "users", false),
        // File limits
        FeatureFlag.Create("files.max_upload_size_mb", "Max Upload Size (MB)", "Maximum single file upload size in megabytes", "50", FlagValueType.Integer, "files", false),
        FeatureFlag.Create("files.max_storage_mb", "Max Storage (MB)", "Maximum total storage per tenant in megabytes", "5120", FlagValueType.Integer, "files", false),
        // Report limits
        FeatureFlag.Create("reports.enabled", "Reports Enabled", "Enable report generation", "true", FlagValueType.Boolean, "reports", false),
        FeatureFlag.Create("reports.max_concurrent", "Max Concurrent Reports", "Maximum concurrent report generation jobs", "3", FlagValueType.Integer, "reports", false),
        FeatureFlag.Create("reports.pdf_export", "PDF Export", "Enable PDF export for reports", "true", FlagValueType.Boolean, "reports", false),
        // API key limits
        FeatureFlag.Create("api_keys.enabled", "API Keys Enabled", "Enable API key management", "true", FlagValueType.Boolean, "api_keys", false),
        FeatureFlag.Create("api_keys.max_count", "Max API Keys", "Maximum number of API keys per tenant", "10", FlagValueType.Integer, "api_keys", false),
        // System flags (platform admin only, IsSystem=true)
        FeatureFlag.Create("ui.maintenance_mode", "Maintenance Mode", "Show maintenance page to non-admin users", "false", FlagValueType.Boolean, "system", true),
        FeatureFlag.Create("billing.enabled", "Billing Enabled", "Enable billing and subscription features", "false", FlagValueType.Boolean, "billing", true),
    };

    context.FeatureFlags.AddRange(flags);
    await context.SaveChangesAsync();
    logger.LogInformation("Seeded {Count} default feature flags", flags.Length);
}
```

Add using: `using Starter.Domain.FeatureFlags.Entities;` and `using Starter.Domain.FeatureFlags.Enums;`

- [ ] **Step 3: Verify build**

Run: `cd boilerplateBE && dotnet build --verbosity quiet`
Expected: 0 errors

- [ ] **Step 4: Commit**

```bash
git add boilerplateBE/src/Starter.Api/Controllers/FeatureFlagsController.cs \
       boilerplateBE/src/Starter.Infrastructure/Persistence/Seeds/DataSeeder.cs
git commit -m "feat(feature-flags): add API controller and seed data"
```

---

## Task 9: Frontend API Layer & Config

**Files:**
- Modify: `boilerplateFE/src/config/api.config.ts`
- Modify: `boilerplateFE/src/config/routes.config.ts`
- Modify: `boilerplateFE/src/lib/query/keys.ts`
- Create: `boilerplateFE/src/features/feature-flags/api/feature-flags.api.ts`
- Create: `boilerplateFE/src/features/feature-flags/api/feature-flags.queries.ts`
- Create: `boilerplateFE/src/features/feature-flags/api/index.ts`

- [ ] **Step 1: Add API endpoints config**

In `api.config.ts`, add to `API_ENDPOINTS`:

```typescript
FEATURE_FLAGS: {
  LIST: '/FeatureFlags',
  BY_KEY: (key: string) => `/FeatureFlags/${key}`,
  DETAIL: (id: string) => `/FeatureFlags/${id}`,
  TENANT_OVERRIDE: (id: string, tenantId: string) => `/FeatureFlags/${id}/tenants/${tenantId}`,
},
```

- [ ] **Step 2: Add route config**

In `routes.config.ts`, add to `ROUTES`:

```typescript
FEATURE_FLAGS: {
  LIST: '/feature-flags',
},
```

- [ ] **Step 3: Add query keys**

In `keys.ts`, add to `queryKeys`:

```typescript
featureFlags: {
  all: ['featureFlags'] as const,
  lists: () => [...queryKeys.featureFlags.all, 'list'] as const,
  list: (filters?: object) => [...queryKeys.featureFlags.lists(), filters ?? {}] as const,
  details: () => [...queryKeys.featureFlags.all, 'detail'] as const,
  detail: (key: string) => [...queryKeys.featureFlags.details(), key] as const,
},
```

- [ ] **Step 4: Create API functions and types**

Create `boilerplateFE/src/features/feature-flags/api/feature-flags.api.ts`:

```typescript
import { apiClient } from '@/lib/api/client';
import { API_ENDPOINTS } from '@/config/api.config';

export interface FeatureFlagDto {
  id: string;
  key: string;
  name: string;
  description: string | null;
  defaultValue: string;
  valueType: 'Boolean' | 'String' | 'Integer' | 'Json';
  category: string | null;
  isSystem: boolean;
  tenantOverrideValue: string | null;
  resolvedValue: string;
  createdAt: string;
  modifiedAt: string | null;
}

export interface CreateFeatureFlagData {
  key: string;
  name: string;
  description?: string | null;
  defaultValue: string;
  valueType: number;
  category?: string | null;
  isSystem: boolean;
}

export interface UpdateFeatureFlagData {
  id: string;
  name: string;
  description?: string | null;
  defaultValue: string;
  category?: string | null;
}

export interface SetTenantOverrideData {
  value: string;
}

export const featureFlagsApi = {
  getAll: (params?: Record<string, unknown>) =>
    apiClient.get(API_ENDPOINTS.FEATURE_FLAGS.LIST, { params }).then(r => r.data),

  getByKey: (key: string) =>
    apiClient.get<{ data: FeatureFlagDto }>(API_ENDPOINTS.FEATURE_FLAGS.BY_KEY(key)).then(r => r.data.data),

  create: (data: CreateFeatureFlagData) =>
    apiClient.post(API_ENDPOINTS.FEATURE_FLAGS.LIST, data).then(r => r.data),

  update: (data: UpdateFeatureFlagData) =>
    apiClient.put(API_ENDPOINTS.FEATURE_FLAGS.DETAIL(data.id), data).then(r => r.data),

  delete: (id: string) =>
    apiClient.delete(API_ENDPOINTS.FEATURE_FLAGS.DETAIL(id)).then(r => r.data),

  setTenantOverride: (flagId: string, tenantId: string, data: SetTenantOverrideData) =>
    apiClient.put(API_ENDPOINTS.FEATURE_FLAGS.TENANT_OVERRIDE(flagId, tenantId), data).then(r => r.data),

  removeTenantOverride: (flagId: string, tenantId: string) =>
    apiClient.delete(API_ENDPOINTS.FEATURE_FLAGS.TENANT_OVERRIDE(flagId, tenantId)).then(r => r.data),
};
```

- [ ] **Step 5: Create React Query hooks**

Create `boilerplateFE/src/features/feature-flags/api/feature-flags.queries.ts`:

```typescript
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { queryKeys } from '@/lib/query/keys';
import { featureFlagsApi } from './feature-flags.api';
import type { CreateFeatureFlagData, UpdateFeatureFlagData, SetTenantOverrideData } from './feature-flags.api';
import { toast } from 'sonner';
import i18n from '@/i18n';

export function useFeatureFlags(params?: Record<string, unknown>) {
  return useQuery({
    queryKey: queryKeys.featureFlags.list(params),
    queryFn: () => featureFlagsApi.getAll(params),
  });
}

export function useFeatureFlagByKey(key: string) {
  return useQuery({
    queryKey: queryKeys.featureFlags.detail(key),
    queryFn: () => featureFlagsApi.getByKey(key),
    enabled: !!key,
  });
}

export function useCreateFeatureFlag() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (data: CreateFeatureFlagData) => featureFlagsApi.create(data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: queryKeys.featureFlags.all });
      toast.success(i18n.t('featureFlags.createdSuccess'));
    },
  });
}

export function useUpdateFeatureFlag() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (data: UpdateFeatureFlagData) => featureFlagsApi.update(data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: queryKeys.featureFlags.all });
      toast.success(i18n.t('featureFlags.updatedSuccess'));
    },
  });
}

export function useDeleteFeatureFlag() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => featureFlagsApi.delete(id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: queryKeys.featureFlags.all });
      toast.success(i18n.t('featureFlags.deletedSuccess'));
    },
  });
}

export function useSetTenantOverride() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ flagId, tenantId, data }: { flagId: string; tenantId: string; data: SetTenantOverrideData }) =>
      featureFlagsApi.setTenantOverride(flagId, tenantId, data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: queryKeys.featureFlags.all });
      toast.success(i18n.t('featureFlags.overrideSet'));
    },
  });
}

export function useRemoveTenantOverride() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ flagId, tenantId }: { flagId: string; tenantId: string }) =>
      featureFlagsApi.removeTenantOverride(flagId, tenantId),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: queryKeys.featureFlags.all });
      toast.success(i18n.t('featureFlags.overrideRemoved'));
    },
  });
}
```

- [ ] **Step 6: Create barrel export**

Create `boilerplateFE/src/features/feature-flags/api/index.ts`:

```typescript
export * from './feature-flags.api';
export * from './feature-flags.queries';
```

- [ ] **Step 7: Verify frontend build**

Run: `cd boilerplateFE && npm run build`
Expected: Build succeeds (pages not wired yet, but API layer should compile)

- [ ] **Step 8: Commit**

```bash
git add boilerplateFE/src/config/api.config.ts \
       boilerplateFE/src/config/routes.config.ts \
       boilerplateFE/src/lib/query/keys.ts \
       boilerplateFE/src/features/feature-flags/api/
git commit -m "feat(feature-flags): add frontend API layer, query hooks, and config"
```

---

## Task 10: Frontend Page, Components & Routing

**Files:**
- Create: `boilerplateFE/src/features/feature-flags/pages/FeatureFlagsPage.tsx`
- Create: `boilerplateFE/src/features/feature-flags/components/FeatureFlagsList.tsx`
- Create: `boilerplateFE/src/features/feature-flags/components/CreateFeatureFlagDialog.tsx`
- Create: `boilerplateFE/src/features/feature-flags/components/EditFeatureFlagDialog.tsx`
- Create: `boilerplateFE/src/features/feature-flags/components/TenantOverrideDialog.tsx`
- Modify: `boilerplateFE/src/routes/routes.tsx`
- Modify: `boilerplateFE/src/components/layout/MainLayout/Sidebar.tsx`

- [ ] **Step 1: Create components**

Create all 5 frontend component files. Follow the exact patterns from the api-keys feature:

- **FeatureFlagsPage.tsx** — Page wrapper with `PageHeader`, permission-based create button, `FeatureFlagsList` child. Use `useFeatureFlags` hook with pagination params.
- **FeatureFlagsList.tsx** — Table component with columns: Key, Name, Type badge, Category, Default Value, Resolved Value (with override indicator), Actions (edit, delete, set override). Use `Table` from common components (no extra Card wrapper). Boolean flags show inline toggle for quick default value changes. Category grouping via optional filter dropdown.
- **CreateFeatureFlagDialog.tsx** — Dialog with form: key (text, dot-notation), name, description, valueType (select), defaultValue (text/toggle for boolean), category, isSystem checkbox. Uses `useCreateFeatureFlag` mutation.
- **EditFeatureFlagDialog.tsx** — Dialog for updating name, description, defaultValue, category. Pre-fills from selected flag. Uses `useUpdateFeatureFlag` mutation.
- **TenantOverrideDialog.tsx** — Dialog showing current default, tenant selector (if platform admin), value input. Uses `useSetTenantOverride` mutation. Remove override button uses `useRemoveTenantOverride`.

Follow these existing patterns from the api-keys feature:
- `PageHeader` from `@/components/common`
- `Table` component with no extra card wrapper
- `ConfirmDialog` for delete confirmation
- `EmptyState` when no flags exist
- `Pagination` with `getPersistedPageSize()` for initial state
- `useBackNavigation` is NOT needed (this is a list page, not a detail)
- Permission checks via `usePermissions()` hook
- All text via `useTranslation()` / `t()` calls

- [ ] **Step 2: Add route**

In `routes.tsx`, add the feature flags route alongside the existing API keys route:

```tsx
// Feature Flags
{
  element: <PermissionGuard permission={PERMISSIONS.FeatureFlags.View} />,
  children: [
    { path: ROUTES.FEATURE_FLAGS.LIST, element: <FeatureFlagsPage /> },
  ],
},
```

Add the lazy import at the top:
```tsx
const FeatureFlagsPage = lazy(() => import('@/features/feature-flags/pages/FeatureFlagsPage'));
```

- [ ] **Step 3: Add sidebar nav item**

In `Sidebar.tsx`, add the nav item after API Keys:

```tsx
...(hasPermission(PERMISSIONS.FeatureFlags.View)
  ? [{ label: t('nav.featureFlags'), icon: ToggleRight, path: ROUTES.FEATURE_FLAGS.LIST }]
  : []),
```

Import `ToggleRight` from `lucide-react`.

- [ ] **Step 4: Verify frontend build**

Run: `cd boilerplateFE && npm run build`
Expected: Build succeeds

- [ ] **Step 5: Commit**

```bash
git add boilerplateFE/src/features/feature-flags/ \
       boilerplateFE/src/routes/routes.tsx \
       boilerplateFE/src/components/layout/MainLayout/Sidebar.tsx
git commit -m "feat(feature-flags): add frontend page, components, and routing"
```

---

## Task 11: i18n Translations & useFeatureFlag Hook

**Files:**
- Modify: `boilerplateFE/src/i18n/locales/en/translation.json`
- Modify: `boilerplateFE/src/i18n/locales/ar/translation.json`
- Modify: `boilerplateFE/src/i18n/locales/ku/translation.json`
- Create: `boilerplateFE/src/hooks/useFeatureFlag.ts`

- [ ] **Step 1: Add English translations**

In `en/translation.json`, add under a new `"featureFlags"` key:

```json
"featureFlags": {
  "title": "Feature Flags",
  "description": "Manage platform feature flags and tenant overrides",
  "create": "Create Flag",
  "edit": "Edit Flag",
  "delete": "Delete Flag",
  "setOverride": "Set Override",
  "removeOverride": "Remove Override",
  "key": "Key",
  "name": "Name",
  "defaultValue": "Default Value",
  "resolvedValue": "Resolved Value",
  "valueType": "Type",
  "category": "Category",
  "isSystem": "System Flag",
  "tenantOverride": "Tenant Override",
  "overrideValue": "Override Value",
  "selectTenant": "Select Tenant",
  "createdSuccess": "Feature flag created successfully",
  "updatedSuccess": "Feature flag updated successfully",
  "deletedSuccess": "Feature flag deleted successfully",
  "overrideSet": "Tenant override set successfully",
  "overrideRemoved": "Tenant override removed successfully",
  "confirmDelete": "Are you sure you want to delete this feature flag? This cannot be undone.",
  "cannotDeleteSystem": "System flags cannot be deleted",
  "noFlags": "No feature flags found",
  "noFlagsDescription": "Create your first feature flag to get started",
  "keyHelp": "Use dot notation (e.g., billing.enabled)",
  "boolean": "Boolean",
  "string": "String",
  "integer": "Integer",
  "json": "JSON",
  "overridden": "Overridden"
}
```

Also add to `"nav"`:
```json
"featureFlags": "Feature Flags"
```

- [ ] **Step 2: Add Arabic translations**

Add equivalent keys in `ar/translation.json` with Arabic text.

- [ ] **Step 3: Add Kurdish translations**

Add equivalent keys in `ku/translation.json` with Kurdish text.

- [ ] **Step 4: Create useFeatureFlag hook**

Create `boilerplateFE/src/hooks/useFeatureFlag.ts`:

```typescript
import { useQuery } from '@tanstack/react-query';
import { queryKeys } from '@/lib/query/keys';
import { featureFlagsApi } from '@/features/feature-flags/api';

export function useFeatureFlag(key: string) {
  const { data, isLoading } = useQuery({
    queryKey: queryKeys.featureFlags.detail(key),
    queryFn: () => featureFlagsApi.getByKey(key),
    enabled: !!key,
    staleTime: 5 * 60 * 1000, // 5 minutes, matching server cache TTL
  });

  return {
    value: data?.resolvedValue ?? null,
    isEnabled: data?.resolvedValue === 'true',
    isLoading,
    flag: data ?? null,
  };
}
```

- [ ] **Step 5: Verify frontend build**

Run: `cd boilerplateFE && npm run build`
Expected: Build succeeds

- [ ] **Step 6: Commit**

```bash
git add boilerplateFE/src/i18n/locales/ \
       boilerplateFE/src/hooks/useFeatureFlag.ts
git commit -m "feat(feature-flags): add i18n translations and useFeatureFlag hook"
```

---

## Task 12: Enforcement in Existing Handlers (3 Reference Implementations)

**Files:**
- Modify: `boilerplateBE/src/Starter.Application/Features/Users/Commands/CreateUser/CreateUserCommandHandler.cs`
- Modify: `boilerplateBE/src/Starter.Application/Features/Files/Commands/UploadFile/UploadFileCommandHandler.cs`
- Modify: `boilerplateBE/src/Starter.Application/Features/ApiKeys/Commands/CreateApiKey/CreateApiKeyCommandHandler.cs`

- [ ] **Step 1: Add quota enforcement to CreateUserCommandHandler**

Inject `IFeatureFlagService flags` into the handler's primary constructor. Add this check at the start of the `Handle` method, before any existing logic:

```csharp
var maxUsers = await flags.GetValueAsync<int>("users.max_count", cancellationToken);
var currentCount = await context.Users.CountAsync(cancellationToken);
if (currentCount >= maxUsers)
    return Result.Failure<Guid>(FeatureFlagErrors.QuotaExceeded("users", maxUsers));
```

Add using: `using Starter.Domain.FeatureFlags.Errors;`

- [ ] **Step 2: Add quota enforcement to UploadFileCommandHandler**

Inject `IFeatureFlagService flags`. Add at the start of `Handle`:

```csharp
// Check single file size limit
var maxSizeMb = await flags.GetValueAsync<int>("files.max_upload_size_mb", cancellationToken);
var fileSizeMb = (int)(request.FileSize / (1024 * 1024));
if (fileSizeMb > maxSizeMb)
    return Result.Failure<Guid>(FeatureFlagErrors.QuotaExceeded($"MB per file (max {maxSizeMb}MB)", maxSizeMb));

// Check total storage limit
var maxStorageMb = await flags.GetValueAsync<int>("files.max_storage_mb", cancellationToken);
var usedBytes = await context.FileMetadata.SumAsync(f => f.Size, cancellationToken);
var usedMb = (int)(usedBytes / (1024 * 1024));
if (usedMb + fileSizeMb > maxStorageMb)
    return Result.Failure<Guid>(FeatureFlagErrors.QuotaExceeded($"MB storage (max {maxStorageMb}MB)", maxStorageMb));
```

- [ ] **Step 3: Add quota + gate enforcement to CreateApiKeyCommandHandler**

Inject `IFeatureFlagService flags`. Add at the start of `Handle`:

```csharp
if (!await flags.IsEnabledAsync("api_keys.enabled", cancellationToken))
    return Result.Failure<CreateApiKeyResponse>(FeatureFlagErrors.FeatureDisabled("API Keys"));

var maxKeys = await flags.GetValueAsync<int>("api_keys.max_count", cancellationToken);
var currentCount = await context.ApiKeys.CountAsync(k => !k.IsRevoked, cancellationToken);
if (currentCount >= maxKeys)
    return Result.Failure<CreateApiKeyResponse>(FeatureFlagErrors.QuotaExceeded("API keys", maxKeys));
```

- [ ] **Step 4: Verify build**

Run: `cd boilerplateBE && dotnet build --verbosity quiet`
Expected: 0 errors

- [ ] **Step 5: Commit**

```bash
git add boilerplateBE/src/Starter.Application/Features/Users/Commands/CreateUser/ \
       boilerplateBE/src/Starter.Application/Features/Files/Commands/UploadFile/ \
       boilerplateBE/src/Starter.Application/Features/ApiKeys/Commands/CreateApiKey/
git commit -m "feat(feature-flags): add enforcement to CreateUser, UploadFile, CreateApiKey handlers"
```

---

## Task 13: Developer Documentation

**Files:**
- Create: `boilerplateBE/docs/feature-flags.md`

- [ ] **Step 1: Create the developer guide**

Create `boilerplateBE/docs/feature-flags.md` with these sections:

1. **Overview** — What feature flags are, resolution chain (tenant override → platform default), caching behavior (Redis, 5-min TTL, prefix-based invalidation)

2. **Adding a New Feature Flag** — Step-by-step:
   - Add seed data to `DataSeeder.SeedFeatureFlagsAsync()` with key, name, description, default value, value type, category, isSystem
   - Run migration if changing schema (usually not needed — just adding a row)
   - Add enforcement check in the relevant command handler

3. **Enforcement Patterns** — Two patterns with code examples:
   - **Boolean gate:** `if (!await flags.IsEnabledAsync("feature.key")) return FeatureDisabled(...)`
   - **Quota check:** `var limit = await flags.GetValueAsync<int>("resource.max_count"); var current = await context.Entity.CountAsync(); if (current >= limit) return QuotaExceeded(...)`

4. **Existing Enforced Flags** — Table of all 11 flags with enforcement status:
   - `users.max_count` — ✅ Enforced in CreateUserCommandHandler
   - `files.max_upload_size_mb` — ✅ Enforced in UploadFileCommandHandler
   - `files.max_storage_mb` — ✅ Enforced in UploadFileCommandHandler
   - `api_keys.enabled` — ✅ Enforced in CreateApiKeyCommandHandler
   - `api_keys.max_count` — ✅ Enforced in CreateApiKeyCommandHandler
   - `users.invitations_enabled` — ⬜ TODO: enforce in CreateInvitationCommandHandler
   - `reports.enabled` — ⬜ TODO: enforce in RequestReportCommandHandler
   - `reports.max_concurrent` — ⬜ TODO: enforce in RequestReportCommandHandler
   - `reports.pdf_export` — ⬜ TODO: enforce in RequestReportCommandHandler (format check)
   - `ui.maintenance_mode` — ⬜ TODO: enforce in middleware (block non-admin requests)
   - `billing.enabled` — ⬜ Placeholder for Billing feature

5. **Cache Behavior** — Redis key format `ff:{tenantId}`, 5-min TTL, `InvalidateCacheAsync()` called on any write operation, `RemoveByPrefixAsync("ff")` clears all tenants

6. **Race Conditions & Performance** — Optimistic strategy: small overages possible on concurrent requests. Upgrade path: Redis atomic counters (`INCR`/`DECR`) when Billing arrives for real-time usage tracking

7. **Billing Integration Contract** — What the Billing feature MUST implement:
   - Add `OverrideSource` enum (`Manual = 0`, `PlanSubscription = 1`) to `TenantFeatureFlag` entity via migration
   - `SubscriptionChangedEvent` domain event on plan change
   - `SyncPlanFeaturesHandler` listens to event, reads `SubscriptionPlan.Features` JSON, calls `SetTenantOverrideCommand` for each flag, sets source = `PlanSubscription`
   - Plan changes only overwrite `PlanSubscription`-sourced overrides, preserving `Manual` overrides
   - Usage counters: Redis `usage:{tenantId}:{resource}` keys with atomic INCR/DECR, synced periodically with DB counts

8. **API Key Access** — API key authenticated requests automatically resolve feature flags via `ICurrentUserService.TenantId` which `ApiKeyAuthenticationHandler` sets. No special configuration needed.

- [ ] **Step 2: Commit**

```bash
git add boilerplateBE/docs/feature-flags.md
git commit -m "docs(feature-flags): add developer guide with enforcement patterns and billing contract"
```

---

## Task 14: Backend & Frontend Build Verification

- [ ] **Step 1: Full backend build**

Run: `cd boilerplateBE && dotnet build --verbosity quiet`
Expected: 0 errors, 0 warnings (or only pre-existing warnings)

- [ ] **Step 2: Full frontend build**

Run: `cd boilerplateFE && npm run build`
Expected: Build succeeds with no errors

- [ ] **Step 3: Final commit if any fixes needed**

If any build issues were found and fixed:
```bash
git add -A
git commit -m "fix(feature-flags): resolve build issues"
```
