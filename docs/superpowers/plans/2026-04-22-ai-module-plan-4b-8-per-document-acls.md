# Plan 4b-8 — Per-document ACLs + unified file/access hub

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans. Steps use checkbox (`- [ ]`) syntax.

**Goal:** Add a generic `ResourceGrant` primitive, refactor the Files feature to own AI document storage, and enforce two-layer ACLs on AI retrieval with Qdrant payload push-down + Redis cache.

**Architecture:** Core-feature placement (matches `FileMetadata`/`Notification`). Shared ACL entity on `ApplicationDbContext`. AI module consumes via `IResourceAccessService`. Retrieval filter pushes stable ACL fields to Qdrant payload + denormalized `AiDocumentChunk` columns; volatile grants resolved via tiny indexed DB lookup cached per-user in Redis.

**Tech Stack:** .NET 10, EF Core (Postgres), MediatR, Qdrant gRPC, Redis (`ICacheService`), xUnit + FluentAssertions, React 19 + TanStack Query + shadcn/ui.

**Spec:** [`docs/superpowers/specs/2026-04-22-ai-module-plan-4b-8-per-document-acls-design.md`](../specs/2026-04-22-ai-module-plan-4b-8-per-document-acls-design.md)

---

## Conventions for this plan

- All file paths are relative to repo root (`Boilerplate-CQRS-ai-integration/`).
- Every task ends with **one commit**. Message format: `feat(access): …`, `feat(files): …`, `feat(ai): …`, `test(access): …`, `docs(access): …`. **Never** add `Co-Authored-By` lines.
- Tests use xUnit + FluentAssertions. In-memory EF (`UseInMemoryDatabase`). Match `MmrIntegrationTests` style.
- **Don't** create EF migrations — boilerplate never ships migrations; rename-apps regenerate.
- Run build before each commit: `dotnet build boilerplateBE/Starter.sln` (or targeted project build for FE changes skip this).
- Primary test run: `dotnet test boilerplateBE/tests/Starter.Api.Tests/Starter.Api.Tests.csproj --filter <category>`.
- Frontend build check: `cd boilerplateFE && npm run build`.

---

## File map (one-line responsibilities)

### Phase A — Shared ACL primitive (core)
- `boilerplateBE/src/Starter.Domain/Common/Access/ResourceGrant.cs` — aggregate root for a grant row
- `boilerplateBE/src/Starter.Domain/Common/Access/Enums/ResourceVisibility.cs` — `Private | TenantWide | Public`
- `boilerplateBE/src/Starter.Domain/Common/Access/Enums/GrantSubjectType.cs` — `User | Role`
- `boilerplateBE/src/Starter.Domain/Common/Access/Enums/AccessLevel.cs` — `Viewer | Editor | Manager`
- `boilerplateBE/src/Starter.Domain/Common/Access/Enums/AssistantAccessMode.cs` — `CallerPrincipal | AssistantPrincipal`
- `boilerplateBE/src/Starter.Domain/Common/Access/IShareable.cs` — marker interface
- `boilerplateBE/src/Starter.Domain/Common/Access/Errors/AccessErrors.cs`
- `boilerplateBE/src/Starter.Application/Common/Access/IResourceAccessService.cs`
- `boilerplateBE/src/Starter.Application/Common/Access/Contracts/AccessResolution.cs`
- `boilerplateBE/src/Starter.Application/Common/Access/Contracts/ResourceTypes.cs` — constants + registry
- `boilerplateBE/src/Starter.Application/Common/Access/DTOs/ResourceGrantDto.cs`
- `boilerplateBE/src/Starter.Application/Features/Access/Commands/GrantResourceAccess/*.cs`
- `boilerplateBE/src/Starter.Application/Features/Access/Commands/RevokeResourceAccess/*.cs`
- `boilerplateBE/src/Starter.Application/Features/Access/Commands/SetResourceVisibility/*.cs`
- `boilerplateBE/src/Starter.Application/Features/Access/Commands/TransferResourceOwnership/*.cs`
- `boilerplateBE/src/Starter.Application/Features/Access/Queries/ListResourceGrants/*.cs`
- `boilerplateBE/src/Starter.Infrastructure/Services/Access/ResourceAccessService.cs`
- `boilerplateBE/src/Starter.Infrastructure/Services/Access/AclCacheKeys.cs`
- `boilerplateBE/src/Starter.Infrastructure/Persistence/Configurations/ResourceGrantConfiguration.cs`
- `boilerplateBE/src/Starter.Infrastructure/Persistence/ApplicationDbContext.cs` — add `ResourceGrants` DbSet
- `boilerplateBE/src/Starter.Application/Common/Interfaces/IApplicationDbContext.cs` — add `ResourceGrants` DbSet
- `boilerplateBE/src/Starter.Infrastructure/DependencyInjection.cs` — register `IResourceAccessService`

### Phase B — Files as hub
- `boilerplateBE/src/Starter.Domain/Common/FileMetadata.cs` — replace `IsPublic` with `Visibility`; implement `IShareable`
- `boilerplateBE/src/Starter.Infrastructure/Persistence/Configurations/FileMetadataConfiguration.cs` — update column
- `boilerplateBE/src/Starter.Infrastructure/Services/FileService.cs` — add managed-file API; route via Visibility
- `boilerplateBE/src/Starter.Application/Common/Interfaces/IFileService.cs` — new contract methods
- `boilerplateBE/src/Starter.Application/Features/Files/Commands/UploadFile/*.cs` — update command shape
- `boilerplateBE/src/Starter.Application/Features/Files/Queries/GetFiles/*.cs` — view filter
- `boilerplateBE/src/Starter.Application/Features/Files/Queries/GetFileUrl/*.cs` — ACL check
- `boilerplateBE/src/Starter.Application/Features/Files/Queries/GetStorageSummary/*.cs` — **new**
- `boilerplateBE/src/Starter.Api/Controllers/FilesController.cs` — grants endpoints, visibility, ownership, storage-summary
- `boilerplateBE/src/Starter.Shared/Constants/Permissions.cs` — add `Files.ShareOwn`
- `boilerplateBE/src/Starter.Shared/Constants/Roles.cs` — grant `Files.ShareOwn` to User + Admin

### Phase C — AI wiring + retrieval filter
- `boilerplateBE/src/modules/Starter.Module.AI/Domain/Entities/AiDocument.cs` — `FileId` replaces `FileRef`
- `boilerplateBE/src/modules/Starter.Module.AI/Domain/Entities/AiDocumentChunk.cs` — denormalized ACL columns
- `boilerplateBE/src/modules/Starter.Module.AI/Domain/Entities/AiAssistant.cs` — `Visibility`, `AccessMode`, `CreatedByUserId`
- `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Persistence/Configurations/*.cs`
- `boilerplateBE/src/modules/Starter.Module.AI/Application/Commands/UploadDocument/UploadDocumentCommandHandler.cs` — route via FileService
- `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Ingestion/QdrantVectorStore.cs` — payload enrichment
- `boilerplateBE/src/modules/Starter.Module.AI/Application/Services/Ingestion/VectorPoint*.cs` — payload carrier additions
- `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Retrieval/RagRetrievalService.cs` — `acl-resolve` stage
- `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Retrieval/RagStages.cs` — add `AclResolve`
- `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Retrieval/PostgresKeywordSearchService.cs` — filter push-down
- `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Settings/AiRagSettings.cs` — two new keys
- `boilerplateBE/src/modules/Starter.Module.AI/Controllers/AiAssistantsController.cs` — grants/visibility endpoints, chat gate

### Phase D — Cascades, ownership, audit, storage summary
- `boilerplateBE/src/Starter.Application/Features/Users/Commands/DeleteUser/DeleteUserCommandHandler.cs` — revoke grants
- `boilerplateBE/src/Starter.Application/Features/Roles/Commands/DeleteRole/DeleteRoleCommandHandler.cs` — revoke grants
- `boilerplateBE/src/Starter.Application/Features/Tenants/Commands/DeleteTenant/DeleteTenantCommandHandler.cs` — revoke grants
- `boilerplateBE/src/Starter.Infrastructure/Services/NotificationService.cs` — add `ResourceShared` emission
- Audit events — call existing `AuditLog.Create` in each command handler

### Phase E — Tests + docs
- `boilerplateBE/tests/Starter.Application.Tests/Access/*.cs`
- `boilerplateBE/tests/Starter.Api.Tests/Ai/Retrieval/Acl/AclIntegrationTests.cs`
- `boilerplateBE/tests/Starter.Api.Tests/Ai/Fakes/FakeResourceAccessService.cs`
- `boilerplateBE/tests/Starter.Api.Tests/Files/AclApiRegressionTests.cs`
- `CLAUDE.md` — add Core/Module/Shared block

### Phase F — Frontend
- `boilerplateFE/src/features/access/api/access.api.ts` — generic ACL API
- `boilerplateFE/src/features/access/api/access.queries.ts`
- `boilerplateFE/src/features/access/types.ts`
- `boilerplateFE/src/features/files/api/files.api.ts` — visibility/ownership/storage
- `boilerplateFE/src/features/files/api/files.queries.ts`
- `boilerplateFE/src/types/file.types.ts` — `isPublic` → `visibility`
- `boilerplateFE/src/components/common/ResourceShareDialog.tsx`
- `boilerplateFE/src/components/common/OwnershipTransferDialog.tsx`
- `boilerplateFE/src/components/common/VisibilityBadge.tsx`
- `boilerplateFE/src/components/common/SubjectPicker.tsx`
- `boilerplateFE/src/components/common/SubjectStack.tsx`
- `boilerplateFE/src/features/files/pages/FilesPage.tsx` — view tabs + share wiring
- `boilerplateFE/src/features/files/components/StorageSummaryPanel.tsx`
- `boilerplateFE/src/constants/permissions.ts` — mirror `Files.ShareOwn`
- `boilerplateFE/src/config/api.config.ts` — new endpoint map
- `boilerplateFE/src/i18n/locales/en/translation.json` + `ar/translation.json` — new keys

---

## Phase A — Shared ACL primitive

### Task 1: Domain enums + marker interface

**Files:**
- Create: `boilerplateBE/src/Starter.Domain/Common/Access/Enums/ResourceVisibility.cs`
- Create: `boilerplateBE/src/Starter.Domain/Common/Access/Enums/GrantSubjectType.cs`
- Create: `boilerplateBE/src/Starter.Domain/Common/Access/Enums/AccessLevel.cs`
- Create: `boilerplateBE/src/Starter.Domain/Common/Access/Enums/AssistantAccessMode.cs`
- Create: `boilerplateBE/src/Starter.Domain/Common/Access/IShareable.cs`

- [ ] **Step 1: Write enums + marker**

```csharp
// ResourceVisibility.cs
namespace Starter.Domain.Common.Access.Enums;
public enum ResourceVisibility { Private = 0, TenantWide = 1, Public = 2 }
```

```csharp
// GrantSubjectType.cs
namespace Starter.Domain.Common.Access.Enums;
public enum GrantSubjectType { User = 0, Role = 1 }
```

```csharp
// AccessLevel.cs
namespace Starter.Domain.Common.Access.Enums;
public enum AccessLevel { Viewer = 0, Editor = 1, Manager = 2 }
```

```csharp
// AssistantAccessMode.cs
namespace Starter.Domain.Common.Access.Enums;
public enum AssistantAccessMode { CallerPrincipal = 0, AssistantPrincipal = 1 }
```

```csharp
// IShareable.cs
using Starter.Domain.Common.Access.Enums;
namespace Starter.Domain.Common.Access;
public interface IShareable { Guid Id { get; } ResourceVisibility Visibility { get; } }
```

- [ ] **Step 2: Build**

Run: `dotnet build boilerplateBE/src/Starter.Domain/Starter.Domain.csproj`
Expected: 0 errors.

- [ ] **Step 3: Commit**

```bash
git add boilerplateBE/src/Starter.Domain/Common/Access/
git commit -m "feat(access): add ACL domain enums and IShareable marker"
```

---

### Task 2: `ResourceGrant` entity + AccessErrors

**Files:**
- Create: `boilerplateBE/src/Starter.Domain/Common/Access/ResourceGrant.cs`
- Create: `boilerplateBE/src/Starter.Domain/Common/Access/Errors/AccessErrors.cs`
- Test: `boilerplateBE/tests/Starter.Application.Tests/Access/ResourceGrantTests.cs` (new folder `Access/`)

- [ ] **Step 1: Write failing test**

```csharp
// ResourceGrantTests.cs
using FluentAssertions;
using Starter.Domain.Common.Access;
using Starter.Domain.Common.Access.Enums;
using Xunit;

namespace Starter.Application.Tests.Access;

public sealed class ResourceGrantTests
{
    [Fact]
    public void Create_sets_all_fields_and_generates_id()
    {
        var tenantId = Guid.NewGuid();
        var resourceId = Guid.NewGuid();
        var subjectId = Guid.NewGuid();
        var grantedBy = Guid.NewGuid();

        var g = ResourceGrant.Create(
            tenantId, "File", resourceId,
            GrantSubjectType.User, subjectId,
            AccessLevel.Editor, grantedBy);

        g.Id.Should().NotBe(Guid.Empty);
        g.TenantId.Should().Be(tenantId);
        g.ResourceType.Should().Be("File");
        g.ResourceId.Should().Be(resourceId);
        g.SubjectType.Should().Be(GrantSubjectType.User);
        g.SubjectId.Should().Be(subjectId);
        g.Level.Should().Be(AccessLevel.Editor);
        g.GrantedByUserId.Should().Be(grantedBy);
        g.GrantedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void UpdateLevel_changes_level()
    {
        var g = ResourceGrant.Create(
            Guid.NewGuid(), "File", Guid.NewGuid(),
            GrantSubjectType.User, Guid.NewGuid(),
            AccessLevel.Viewer, Guid.NewGuid());

        g.UpdateLevel(AccessLevel.Manager);

        g.Level.Should().Be(AccessLevel.Manager);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test boilerplateBE/tests/Starter.Application.Tests --filter "FullyQualifiedName~ResourceGrantTests"`
Expected: FAIL — `ResourceGrant` type not found.

- [ ] **Step 3: Implement entity**

```csharp
// ResourceGrant.cs
using Starter.Domain.Common.Access.Enums;

namespace Starter.Domain.Common.Access;

public sealed class ResourceGrant : BaseEntity, ITenantEntity
{
    public Guid? TenantId { get; private set; }
    public string ResourceType { get; private set; } = default!;
    public Guid ResourceId { get; private set; }
    public GrantSubjectType SubjectType { get; private set; }
    public Guid SubjectId { get; private set; }
    public AccessLevel Level { get; private set; }
    public Guid GrantedByUserId { get; private set; }
    public DateTime GrantedAt { get; private set; }

    private ResourceGrant() { }
    private ResourceGrant(Guid id) : base(id) { }

    public static ResourceGrant Create(
        Guid? tenantId, string resourceType, Guid resourceId,
        GrantSubjectType subjectType, Guid subjectId,
        AccessLevel level, Guid grantedByUserId)
    {
        return new ResourceGrant(Guid.NewGuid())
        {
            TenantId = tenantId,
            ResourceType = resourceType,
            ResourceId = resourceId,
            SubjectType = subjectType,
            SubjectId = subjectId,
            Level = level,
            GrantedByUserId = grantedByUserId,
            GrantedAt = DateTime.UtcNow,
        };
    }

    public void UpdateLevel(AccessLevel level) => Level = level;
}
```

- [ ] **Step 4: Write `AccessErrors`**

```csharp
// AccessErrors.cs
using Starter.Shared.Results;
namespace Starter.Domain.Common.Access.Errors;

public static class AccessErrors
{
    public static readonly Error ResourceNotFound = Error.NotFound(
        "Access.ResourceNotFound", "Resource not found or inaccessible.");
    public static readonly Error GrantNotFound = Error.NotFound(
        "Access.GrantNotFound", "Grant not found.");
    public static readonly Error SubjectNotFound = Error.Validation(
        "Access.SubjectNotFound", "Grant target not found.");
    public static readonly Error SubjectInactive = Error.Validation(
        "Access.SubjectInactive", "Grant target is inactive.");
    public static readonly Error CrossTenantGrantBlocked = Error.Forbidden(
        "Access.CrossTenantGrantBlocked", "Cannot grant access across tenants.");
    public static readonly Error SelfGrantBlocked = Error.Conflict(
        "Access.SelfGrantBlocked", "Owners already have full access.");
    public static readonly Error InsufficientLevelToGrant = Error.Forbidden(
        "Access.InsufficientLevelToGrant", "You cannot grant a higher level than you have.");
    public static readonly Error VisibilityNotAllowedForResourceType = Error.Validation(
        "Access.VisibilityNotAllowedForResourceType", "This visibility is not allowed for this resource type.");
    public static readonly Error OnlyOwnerCanPerform = Error.Forbidden(
        "Access.OnlyOwnerCanPerform", "Only the owner can perform this action.");
    public static readonly Error OwnershipTargetNotInTenant = Error.Validation(
        "Access.OwnershipTargetNotInTenant", "New owner must be in the same tenant.");
    public static readonly Error OwnershipTargetInactive = Error.Validation(
        "Access.OwnershipTargetInactive", "New owner must be active.");
}
```

Note: if `Error.Forbidden` doesn't exist in `Starter.Shared.Results.Error`, use the closest factory (`Error.Failure` with a ForbiddenCode) — check `boilerplateBE/src/Starter.Shared/Results/Error.cs` and mirror existing patterns. Stick to the same factory style used by `AiErrors`.

- [ ] **Step 5: Run test to verify pass + commit**

Run: `dotnet test boilerplateBE/tests/Starter.Application.Tests --filter "FullyQualifiedName~ResourceGrantTests"`
Expected: PASS.

```bash
git add boilerplateBE/src/Starter.Domain/Common/Access/ \
        boilerplateBE/tests/Starter.Application.Tests/Access/
git commit -m "feat(access): add ResourceGrant entity and AccessErrors"
```

---

### Task 3: EF configuration + DbContext wiring

**Files:**
- Create: `boilerplateBE/src/Starter.Infrastructure/Persistence/Configurations/ResourceGrantConfiguration.cs`
- Modify: `boilerplateBE/src/Starter.Application/Common/Interfaces/IApplicationDbContext.cs`
- Modify: `boilerplateBE/src/Starter.Infrastructure/Persistence/ApplicationDbContext.cs`

- [ ] **Step 1: EF config**

```csharp
// ResourceGrantConfiguration.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Starter.Domain.Common.Access;
using Starter.Domain.Common.Access.Enums;

namespace Starter.Infrastructure.Persistence.Configurations;

public sealed class ResourceGrantConfiguration : IEntityTypeConfiguration<ResourceGrant>
{
    public void Configure(EntityTypeBuilder<ResourceGrant> b)
    {
        b.ToTable("resource_grants");
        b.HasKey(g => g.Id);
        b.Property(g => g.Id).HasColumnName("id").ValueGeneratedNever();
        b.Property(g => g.TenantId).HasColumnName("tenant_id");
        b.Property(g => g.ResourceType).HasColumnName("resource_type").HasMaxLength(100).IsRequired();
        b.Property(g => g.ResourceId).HasColumnName("resource_id").IsRequired();
        b.Property(g => g.SubjectType).HasColumnName("subject_type").HasConversion<int>().IsRequired();
        b.Property(g => g.SubjectId).HasColumnName("subject_id").IsRequired();
        b.Property(g => g.Level).HasColumnName("level").HasConversion<int>().IsRequired();
        b.Property(g => g.GrantedByUserId).HasColumnName("granted_by_user_id").IsRequired();
        b.Property(g => g.GrantedAt).HasColumnName("granted_at").IsRequired();
        b.Property(g => g.CreatedAt).HasColumnName("created_at").IsRequired();
        b.Property(g => g.ModifiedAt).HasColumnName("modified_at");

        b.HasIndex(g => new { g.TenantId, g.ResourceType, g.ResourceId, g.SubjectType, g.SubjectId })
            .IsUnique()
            .HasDatabaseName("ix_resource_grants_unique");
        b.HasIndex(g => new { g.TenantId, g.ResourceType, g.ResourceId })
            .HasDatabaseName("ix_resource_grants_by_resource");
        b.HasIndex(g => new { g.TenantId, g.SubjectType, g.SubjectId, g.ResourceType })
            .HasDatabaseName("ix_resource_grants_by_subject");
    }
}
```

- [ ] **Step 2: Add DbSet to interface + context**

`IApplicationDbContext.cs` — add after `Notifications`:
```csharp
DbSet<ResourceGrant> ResourceGrants { get; }
```
(plus `using Starter.Domain.Common.Access;`)

`ApplicationDbContext.cs` — mirror the new DbSet and add to the tenant-filter block for `ITenantEntity` (it will be auto-picked up by the convention loop at line 93-118; no manual filter needed).

- [ ] **Step 3: Build**

Run: `dotnet build boilerplateBE/Starter.sln`
Expected: 0 errors.

- [ ] **Step 4: Commit**

```bash
git add boilerplateBE/src/Starter.Infrastructure/Persistence/Configurations/ResourceGrantConfiguration.cs \
        boilerplateBE/src/Starter.Application/Common/Interfaces/IApplicationDbContext.cs \
        boilerplateBE/src/Starter.Infrastructure/Persistence/ApplicationDbContext.cs
git commit -m "feat(access): wire ResourceGrant into ApplicationDbContext"
```

---

### Task 4: `IResourceAccessService` contract + DTOs + ResourceTypes registry

**Files:**
- Create: `boilerplateBE/src/Starter.Application/Common/Access/IResourceAccessService.cs`
- Create: `boilerplateBE/src/Starter.Application/Common/Access/Contracts/AccessResolution.cs`
- Create: `boilerplateBE/src/Starter.Application/Common/Access/Contracts/ResourceTypes.cs`
- Create: `boilerplateBE/src/Starter.Application/Common/Access/DTOs/ResourceGrantDto.cs`

- [ ] **Step 1: Write contracts**

```csharp
// AccessResolution.cs
namespace Starter.Application.Common.Access.Contracts;

public sealed record AccessResolution(
    bool IsAdminBypass,
    IReadOnlyList<Guid> ExplicitGrantedResourceIds);
```

```csharp
// ResourceTypes.cs
using Starter.Domain.Common.Access.Enums;
namespace Starter.Application.Common.Access.Contracts;

public static class ResourceTypes
{
    public const string File = "File";
    public const string AiAssistant = "AiAssistant";

    public static ResourceVisibility MaxVisibility(string resourceType) => resourceType switch
    {
        File        => ResourceVisibility.Public,
        AiAssistant => ResourceVisibility.TenantWide,
        _           => ResourceVisibility.TenantWide,
    };

    public static bool IsKnown(string resourceType) =>
        resourceType is File or AiAssistant;
}
```

```csharp
// ResourceGrantDto.cs
using Starter.Domain.Common.Access.Enums;
namespace Starter.Application.Common.Access.DTOs;

public sealed record ResourceGrantDto(
    Guid Id,
    string ResourceType,
    Guid ResourceId,
    GrantSubjectType SubjectType,
    Guid SubjectId,
    string? SubjectDisplayName,
    AccessLevel Level,
    Guid GrantedByUserId,
    DateTime GrantedAt);
```

```csharp
// IResourceAccessService.cs
using Starter.Application.Common.Access.Contracts;
using Starter.Application.Common.Access.DTOs;
using Starter.Application.Common.Interfaces;
using Starter.Domain.Common.Access.Enums;

namespace Starter.Application.Common.Access;

public interface IResourceAccessService
{
    Task<Guid> GrantAsync(
        string resourceType, Guid resourceId,
        GrantSubjectType subjectType, Guid subjectId,
        AccessLevel level, CancellationToken ct);

    Task RevokeAsync(Guid grantId, CancellationToken ct);

    Task RevokeAllForResourceAsync(string resourceType, Guid resourceId, CancellationToken ct);

    Task<IReadOnlyList<ResourceGrantDto>> ListGrantsAsync(
        string resourceType, Guid resourceId, CancellationToken ct);

    Task<bool> CanAccessAsync(
        ICurrentUserService user, string resourceType, Guid resourceId,
        AccessLevel minLevel, CancellationToken ct);

    Task<AccessResolution> ResolveAccessibleResourcesAsync(
        ICurrentUserService user, string resourceType, CancellationToken ct);

    Task InvalidateUserAsync(Guid userId, CancellationToken ct);
    Task InvalidateRoleMembersAsync(Guid roleId, CancellationToken ct);
}
```

- [ ] **Step 2: Build**

Run: `dotnet build boilerplateBE/src/Starter.Application/Starter.Application.csproj`
Expected: 0 errors.

- [ ] **Step 3: Commit**

```bash
git add boilerplateBE/src/Starter.Application/Common/Access/
git commit -m "feat(access): add IResourceAccessService contract and DTOs"
```

---

### Task 5: `ResourceAccessService` implementation + cache keys + DI

**Files:**
- Create: `boilerplateBE/src/Starter.Infrastructure/Services/Access/AclCacheKeys.cs`
- Create: `boilerplateBE/src/Starter.Infrastructure/Services/Access/ResourceAccessService.cs`
- Modify: `boilerplateBE/src/Starter.Infrastructure/DependencyInjection.cs`
- Test: `boilerplateBE/tests/Starter.Application.Tests/Access/ResourceAccessServiceTests.cs`

- [ ] **Step 1: Cache keys**

```csharp
// AclCacheKeys.cs
namespace Starter.Infrastructure.Services.Access;
internal static class AclCacheKeys
{
    public static string UserVersion(Guid tenantId, Guid userId) => $"aclv:u:{tenantId:N}:{userId:N}";
    public static string AccessibleIds(Guid tenantId, Guid userId, long version, string resourceType) =>
        $"acl:{tenantId:N}:{userId:N}:v{version}:{resourceType}";
}
```

- [ ] **Step 2: Write failing test (cache-hit / resolver invoked once)**

```csharp
// ResourceAccessServiceTests.cs
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Starter.Application.Common.Access.Contracts;
using Starter.Domain.Common.Access;
using Starter.Domain.Common.Access.Enums;
using Starter.Infrastructure.Persistence;
using Starter.Infrastructure.Services.Access;
using Xunit;

namespace Starter.Application.Tests.Access;

public sealed class ResourceAccessServiceTests
{
    [Fact]
    public async Task Resolve_returns_explicit_grants_for_user()
    {
        var tenant = Guid.NewGuid();
        var user = Guid.NewGuid();
        var r1 = Guid.NewGuid();
        var r2 = Guid.NewGuid();

        await using var db = TestDbFactory.CreateApplicationDbContext(tenant);
        db.ResourceGrants.AddRange(
            ResourceGrant.Create(tenant, "File", r1, GrantSubjectType.User, user, AccessLevel.Viewer, Guid.NewGuid()),
            ResourceGrant.Create(tenant, "File", r2, GrantSubjectType.User, user, AccessLevel.Editor, Guid.NewGuid()));
        await db.SaveChangesAsync();

        var service = TestAccessServiceFactory.Create(db);
        var result = await service.ResolveAccessibleResourcesAsync(
            FakeCurrentUser.For(user, tenant, roles: Array.Empty<Guid>(), admin: false),
            ResourceTypes.File, CancellationToken.None);

        result.IsAdminBypass.Should().BeFalse();
        result.ExplicitGrantedResourceIds.Should().BeEquivalentTo(new[] { r1, r2 });
    }

    [Fact]
    public async Task Admin_bypass_when_user_has_Files_Manage()
    {
        var tenant = Guid.NewGuid();
        var user = Guid.NewGuid();
        await using var db = TestDbFactory.CreateApplicationDbContext(tenant);

        var service = TestAccessServiceFactory.Create(db);
        var result = await service.ResolveAccessibleResourcesAsync(
            FakeCurrentUser.For(user, tenant, permissions: new[] { "Files.Manage" }),
            ResourceTypes.File, CancellationToken.None);

        result.IsAdminBypass.Should().BeTrue();
    }
}
```

Also add lightweight test helpers in the same project (new files `TestDbFactory.cs`, `FakeCurrentUser.cs`, `TestAccessServiceFactory.cs`) that build:
- an `ApplicationDbContext` over `UseInMemoryDatabase`,
- a `FakeCurrentUserService` implementing `ICurrentUserService`,
- a `ResourceAccessService` using `NullCache` (implementing `ICacheService` with in-memory Dictionary) and the above DbContext.

Keep these helpers in `boilerplateBE/tests/Starter.Application.Tests/Access/_Helpers/` so other Access tests reuse them.

- [ ] **Step 3: Run test to verify it fails**

Run: `dotnet test boilerplateBE/tests/Starter.Application.Tests --filter "FullyQualifiedName~ResourceAccessServiceTests"`
Expected: FAIL — service type missing.

- [ ] **Step 4: Implement service**

```csharp
// ResourceAccessService.cs
using Microsoft.EntityFrameworkCore;
using Starter.Application.Common.Access;
using Starter.Application.Common.Access.Contracts;
using Starter.Application.Common.Access.DTOs;
using Starter.Application.Common.Interfaces;
using Starter.Domain.Common.Access;
using Starter.Domain.Common.Access.Enums;
using Starter.Domain.Common.Access.Errors;
using Starter.Domain.Exceptions;
using Starter.Shared.Constants;

namespace Starter.Infrastructure.Services.Access;

public sealed class ResourceAccessService(
    IApplicationDbContext db,
    ICacheService cache,
    ICurrentUserService currentUser) : IResourceAccessService
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(60);

    public async Task<Guid> GrantAsync(
        string resourceType, Guid resourceId,
        GrantSubjectType subjectType, Guid subjectId,
        AccessLevel level, CancellationToken ct)
    {
        if (!ResourceTypes.IsKnown(resourceType))
            throw new DomainException(AccessErrors.ResourceNotFound.Description, AccessErrors.ResourceNotFound.Code);

        var tenantId = currentUser.TenantId;

        // Upsert: existing row? update level, else create.
        var existing = await db.ResourceGrants.FirstOrDefaultAsync(
            g => g.TenantId == tenantId
                 && g.ResourceType == resourceType
                 && g.ResourceId == resourceId
                 && g.SubjectType == subjectType
                 && g.SubjectId == subjectId,
            ct);

        Guid grantId;
        if (existing is not null)
        {
            existing.UpdateLevel(level);
            grantId = existing.Id;
        }
        else
        {
            var grant = ResourceGrant.Create(
                tenantId, resourceType, resourceId,
                subjectType, subjectId, level,
                currentUser.UserId ?? Guid.Empty);
            db.ResourceGrants.Add(grant);
            grantId = grant.Id;
        }

        await db.SaveChangesAsync(ct);
        await InvalidateForSubjectAsync(subjectType, subjectId, ct);
        return grantId;
    }

    public async Task RevokeAsync(Guid grantId, CancellationToken ct)
    {
        var grant = await db.ResourceGrants.FirstOrDefaultAsync(g => g.Id == grantId, ct)
            ?? throw new DomainException(AccessErrors.GrantNotFound.Description, AccessErrors.GrantNotFound.Code);
        db.ResourceGrants.Remove(grant);
        await db.SaveChangesAsync(ct);
        await InvalidateForSubjectAsync(grant.SubjectType, grant.SubjectId, ct);
    }

    public async Task RevokeAllForResourceAsync(string resourceType, Guid resourceId, CancellationToken ct)
    {
        var tenantId = currentUser.TenantId;
        var grants = await db.ResourceGrants
            .Where(g => g.TenantId == tenantId && g.ResourceType == resourceType && g.ResourceId == resourceId)
            .ToListAsync(ct);

        if (grants.Count == 0) return;

        var affected = grants.Select(g => (g.SubjectType, g.SubjectId)).Distinct().ToList();
        db.ResourceGrants.RemoveRange(grants);
        await db.SaveChangesAsync(ct);

        foreach (var (st, sid) in affected)
            await InvalidateForSubjectAsync(st, sid, ct);
    }

    public async Task<IReadOnlyList<ResourceGrantDto>> ListGrantsAsync(
        string resourceType, Guid resourceId, CancellationToken ct)
    {
        var tenantId = currentUser.TenantId;
        var rows = await db.ResourceGrants
            .Where(g => g.TenantId == tenantId && g.ResourceType == resourceType && g.ResourceId == resourceId)
            .AsNoTracking()
            .ToListAsync(ct);

        // Names lookup (users + roles)
        var userIds = rows.Where(r => r.SubjectType == GrantSubjectType.User).Select(r => r.SubjectId).Distinct().ToList();
        var roleIds = rows.Where(r => r.SubjectType == GrantSubjectType.Role).Select(r => r.SubjectId).Distinct().ToList();

        var users = await db.Users.Where(u => userIds.Contains(u.Id)).AsNoTracking()
            .Select(u => new { u.Id, Name = (u.FirstName + " " + u.LastName).Trim() }).ToDictionaryAsync(u => u.Id, u => u.Name, ct);
        var roles = await db.Roles.Where(r => roleIds.Contains(r.Id)).AsNoTracking()
            .Select(r => new { r.Id, r.Name }).ToDictionaryAsync(r => r.Id, r => r.Name, ct);

        string? LookupName(GrantSubjectType st, Guid id) =>
            st == GrantSubjectType.User ? users.GetValueOrDefault(id) : roles.GetValueOrDefault(id);

        return rows.Select(g => new ResourceGrantDto(
            g.Id, g.ResourceType, g.ResourceId, g.SubjectType, g.SubjectId,
            LookupName(g.SubjectType, g.SubjectId), g.Level, g.GrantedByUserId, g.GrantedAt)).ToList();
    }

    public async Task<bool> CanAccessAsync(
        ICurrentUserService user, string resourceType, Guid resourceId,
        AccessLevel minLevel, CancellationToken ct)
    {
        if (user.HasPermission(Permissions.Files.Manage)) return true; // admin bypass for files
        if (resourceType == ResourceTypes.AiAssistant && user.IsInRole(Roles.Admin)) return true;

        if (user.UserId is not Guid uid) return false;
        var tenantId = user.TenantId;

        // Check direct user grant
        var userGrant = await db.ResourceGrants.AsNoTracking().FirstOrDefaultAsync(
            g => g.TenantId == tenantId
                 && g.ResourceType == resourceType
                 && g.ResourceId == resourceId
                 && g.SubjectType == GrantSubjectType.User
                 && g.SubjectId == uid,
            ct);
        if (userGrant is not null && userGrant.Level >= minLevel) return true;

        // Check role grants
        var roleIds = await db.UserRoles.Where(ur => ur.UserId == uid).Select(ur => ur.RoleId).ToListAsync(ct);
        var roleGrant = await db.ResourceGrants.AsNoTracking().AnyAsync(
            g => g.TenantId == tenantId
                 && g.ResourceType == resourceType
                 && g.ResourceId == resourceId
                 && g.SubjectType == GrantSubjectType.Role
                 && roleIds.Contains(g.SubjectId)
                 && g.Level >= minLevel,
            ct);
        return roleGrant;
    }

    public async Task<AccessResolution> ResolveAccessibleResourcesAsync(
        ICurrentUserService user, string resourceType, CancellationToken ct)
    {
        if (user.HasPermission(Permissions.Files.Manage) && resourceType == ResourceTypes.File)
            return new AccessResolution(true, Array.Empty<Guid>());
        if (resourceType == ResourceTypes.AiAssistant && user.IsInRole(Roles.Admin))
            return new AccessResolution(true, Array.Empty<Guid>());
        if (user.UserId is not Guid uid || user.TenantId is not Guid tid)
            return new AccessResolution(false, Array.Empty<Guid>());

        // per-user version
        var versionKey = AclCacheKeys.UserVersion(tid, uid);
        var version = await cache.GetAsync<long?>(versionKey, ct);
        if (version is null)
        {
            version = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            await cache.SetAsync(versionKey, version.Value, CacheTtl, ct);
        }

        var cacheKey = AclCacheKeys.AccessibleIds(tid, uid, version.Value, resourceType);
        var cached = await cache.GetAsync<List<Guid>>(cacheKey, ct);
        if (cached is not null)
            return new AccessResolution(false, cached);

        var roleIds = await db.UserRoles.Where(ur => ur.UserId == uid).Select(ur => ur.RoleId).ToListAsync(ct);
        var ids = await db.ResourceGrants.AsNoTracking()
            .Where(g => g.TenantId == tid
                        && g.ResourceType == resourceType
                        && ((g.SubjectType == GrantSubjectType.User && g.SubjectId == uid)
                            || (g.SubjectType == GrantSubjectType.Role && roleIds.Contains(g.SubjectId))))
            .Select(g => g.ResourceId)
            .Distinct()
            .ToListAsync(ct);

        await cache.SetAsync(cacheKey, ids, CacheTtl, ct);
        return new AccessResolution(false, ids);
    }

    public async Task InvalidateUserAsync(Guid userId, CancellationToken ct)
    {
        if (currentUser.TenantId is not Guid tid) return;
        await cache.RemoveAsync(AclCacheKeys.UserVersion(tid, userId), ct);
    }

    public async Task InvalidateRoleMembersAsync(Guid roleId, CancellationToken ct)
    {
        if (currentUser.TenantId is not Guid tid) return;
        var userIds = await db.UserRoles.Where(ur => ur.RoleId == roleId).Select(ur => ur.UserId).ToListAsync(ct);
        foreach (var uid in userIds)
            await cache.RemoveAsync(AclCacheKeys.UserVersion(tid, uid), ct);
    }

    private Task InvalidateForSubjectAsync(GrantSubjectType st, Guid sid, CancellationToken ct) =>
        st == GrantSubjectType.User ? InvalidateUserAsync(sid, ct) : InvalidateRoleMembersAsync(sid, ct);
}
```

- [ ] **Step 5: DI registration**

`Starter.Infrastructure/DependencyInjection.cs` — in `AddInfrastructure(...)`, add:

```csharp
services.AddScoped<IResourceAccessService, ResourceAccessService>();
```

- [ ] **Step 6: Run tests + commit**

Run: `dotnet test boilerplateBE/tests/Starter.Application.Tests --filter "FullyQualifiedName~ResourceAccessServiceTests"`
Expected: PASS (both test cases).

```bash
git add boilerplateBE/src/Starter.Infrastructure/Services/Access/ \
        boilerplateBE/src/Starter.Infrastructure/DependencyInjection.cs \
        boilerplateBE/tests/Starter.Application.Tests/Access/
git commit -m "feat(access): implement ResourceAccessService with Redis-backed caching"
```

---

### Task 6: Access CQRS feature (Grant/Revoke/SetVisibility/TransferOwnership/ListGrants)

**Files (all new):**
- `boilerplateBE/src/Starter.Application/Features/Access/Commands/GrantResourceAccess/GrantResourceAccessCommand.cs`
- `…/GrantResourceAccessCommandHandler.cs`
- `…/GrantResourceAccessCommandValidator.cs`
- Same pattern for `RevokeResourceAccess`, `SetResourceVisibility`, `TransferResourceOwnership`
- `boilerplateBE/src/Starter.Application/Features/Access/Queries/ListResourceGrants/ListResourceGrantsQuery.cs` + handler

These commands are **thin orchestration** over `IResourceAccessService` + the resource-specific entity updates. Since `SetResourceVisibility` and `TransferResourceOwnership` touch resource-type-specific entities, they dispatch by `resourceType`.

- [ ] **Step 1: GrantResourceAccess command + handler**

```csharp
// GrantResourceAccessCommand.cs
using MediatR;
using Starter.Domain.Common.Access.Enums;
using Starter.Shared.Results;

namespace Starter.Application.Features.Access.Commands.GrantResourceAccess;

public sealed record GrantResourceAccessCommand(
    string ResourceType, Guid ResourceId,
    GrantSubjectType SubjectType, Guid SubjectId,
    AccessLevel Level) : IRequest<Result<Guid>>;
```

```csharp
// GrantResourceAccessCommandHandler.cs
using MediatR;
using Starter.Application.Common.Access;
using Starter.Application.Common.Access.Contracts;
using Starter.Application.Common.Interfaces;
using Starter.Domain.Common.Access.Enums;
using Starter.Domain.Common.Access.Errors;
using Starter.Shared.Results;

namespace Starter.Application.Features.Access.Commands.GrantResourceAccess;

internal sealed class GrantResourceAccessCommandHandler(
    IResourceAccessService access,
    IResourceOwnershipProbe probe,
    ICurrentUserService currentUser)
    : IRequestHandler<GrantResourceAccessCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(GrantResourceAccessCommand request, CancellationToken ct)
    {
        if (!ResourceTypes.IsKnown(request.ResourceType))
            return Result.Failure<Guid>(AccessErrors.ResourceNotFound);

        var ownerCheck = await probe.EnsureCallerCanShareAsync(request.ResourceType, request.ResourceId, ct);
        if (ownerCheck.IsFailure) return Result.Failure<Guid>(ownerCheck.Error);

        if (request.SubjectType == GrantSubjectType.User
            && request.SubjectId == currentUser.UserId)
            return Result.Failure<Guid>(AccessErrors.SelfGrantBlocked);

        var subjectCheck = await probe.EnsureSubjectValidAsync(request.SubjectType, request.SubjectId, ct);
        if (subjectCheck.IsFailure) return Result.Failure<Guid>(subjectCheck.Error);

        var id = await access.GrantAsync(
            request.ResourceType, request.ResourceId,
            request.SubjectType, request.SubjectId, request.Level, ct);

        return Result.Success(id);
    }
}
```

The `IResourceOwnershipProbe` is introduced to avoid the handler dispatching on `resourceType`. Define in `boilerplateBE/src/Starter.Application/Common/Access/IResourceOwnershipProbe.cs`:

```csharp
using Starter.Domain.Common.Access.Enums;
using Starter.Shared.Results;
namespace Starter.Application.Common.Access;

public interface IResourceOwnershipProbe
{
    Task<Result> EnsureCallerCanShareAsync(string resourceType, Guid resourceId, CancellationToken ct);
    Task<Result> EnsureSubjectValidAsync(GrantSubjectType subjectType, Guid subjectId, CancellationToken ct);
    Task<Result<Guid>> GetOwnerAsync(string resourceType, Guid resourceId, CancellationToken ct);
    Task<Result> SetVisibilityAsync(string resourceType, Guid resourceId, ResourceVisibility visibility, CancellationToken ct);
    Task<Result> TransferOwnershipAsync(string resourceType, Guid resourceId, Guid newOwnerId, CancellationToken ct);
}
```

Implementation lives in `boilerplateBE/src/Starter.Infrastructure/Services/Access/ResourceOwnershipProbe.cs` and dispatches to per-type resolvers:

```csharp
// ResourceOwnershipProbe.cs — core (File) handler lives here. AI module adds AiAssistant via registration.
using Microsoft.EntityFrameworkCore;
using Starter.Application.Common.Access;
using Starter.Application.Common.Access.Contracts;
using Starter.Application.Common.Interfaces;
using Starter.Domain.Common;
using Starter.Domain.Common.Access.Enums;
using Starter.Domain.Common.Access.Errors;
using Starter.Domain.Identity.Entities;
using Starter.Shared.Constants;
using Starter.Shared.Results;

namespace Starter.Infrastructure.Services.Access;

public interface IResourceOwnershipHandler
{
    string ResourceType { get; }
    Task<Result<Guid>> GetOwnerAsync(Guid resourceId, CancellationToken ct);
    Task<Result> SetVisibilityAsync(Guid resourceId, ResourceVisibility visibility, CancellationToken ct);
    Task<Result> TransferOwnershipAsync(Guid resourceId, Guid newOwnerId, CancellationToken ct);
}

public sealed class ResourceOwnershipProbe(
    IApplicationDbContext db,
    ICurrentUserService currentUser,
    IEnumerable<IResourceOwnershipHandler> handlers) : IResourceOwnershipProbe
{
    private readonly Dictionary<string, IResourceOwnershipHandler> _handlers = handlers.ToDictionary(h => h.ResourceType);

    public async Task<Result> EnsureCallerCanShareAsync(string resourceType, Guid resourceId, CancellationToken ct)
    {
        if (currentUser.UserId is not Guid uid) return Result.Failure(AccessErrors.OnlyOwnerCanPerform);

        if (resourceType == ResourceTypes.File && currentUser.HasPermission(Permissions.Files.Manage))
            return Result.Success();
        if (resourceType == ResourceTypes.AiAssistant && currentUser.IsInRole(Roles.Admin))
            return Result.Success();

        var owner = await GetOwnerAsync(resourceType, resourceId, ct);
        if (owner.IsFailure) return Result.Failure(owner.Error);
        return owner.Value == uid ? Result.Success() : Result.Failure(AccessErrors.OnlyOwnerCanPerform);
    }

    public async Task<Result> EnsureSubjectValidAsync(GrantSubjectType subjectType, Guid subjectId, CancellationToken ct)
    {
        var tenantId = currentUser.TenantId;
        if (subjectType == GrantSubjectType.User)
        {
            var user = await db.Users.FirstOrDefaultAsync(u => u.Id == subjectId, ct);
            if (user is null) return Result.Failure(AccessErrors.SubjectNotFound);
            if (user.TenantId != tenantId) return Result.Failure(AccessErrors.CrossTenantGrantBlocked);
            if (user.Status != UserStatus.Active) return Result.Failure(AccessErrors.SubjectInactive);
            return Result.Success();
        }
        else
        {
            var role = await db.Roles.FirstOrDefaultAsync(r => r.Id == subjectId, ct);
            if (role is null) return Result.Failure(AccessErrors.SubjectNotFound);
            if (role.TenantId != tenantId) return Result.Failure(AccessErrors.CrossTenantGrantBlocked);
            return Result.Success();
        }
    }

    public Task<Result<Guid>> GetOwnerAsync(string resourceType, Guid resourceId, CancellationToken ct) =>
        _handlers.TryGetValue(resourceType, out var h)
            ? h.GetOwnerAsync(resourceId, ct)
            : Task.FromResult(Result.Failure<Guid>(AccessErrors.ResourceNotFound));

    public Task<Result> SetVisibilityAsync(string resourceType, Guid resourceId, ResourceVisibility visibility, CancellationToken ct) =>
        _handlers.TryGetValue(resourceType, out var h)
            ? h.SetVisibilityAsync(resourceId, visibility, ct)
            : Task.FromResult(Result.Failure(AccessErrors.ResourceNotFound));

    public Task<Result> TransferOwnershipAsync(string resourceType, Guid resourceId, Guid newOwnerId, CancellationToken ct) =>
        _handlers.TryGetValue(resourceType, out var h)
            ? h.TransferOwnershipAsync(resourceId, newOwnerId, ct)
            : Task.FromResult(Result.Failure(AccessErrors.ResourceNotFound));
}
```

A concrete `FileOwnershipHandler` lives in `boilerplateBE/src/Starter.Infrastructure/Services/Access/FileOwnershipHandler.cs` and the AI module ships `AiAssistantOwnershipHandler` in Task 11.

```csharp
// FileOwnershipHandler.cs
using Microsoft.EntityFrameworkCore;
using Starter.Application.Common.Access.Contracts;
using Starter.Application.Common.Access;
using Starter.Application.Common.Interfaces;
using Starter.Domain.Common;
using Starter.Domain.Common.Access.Enums;
using Starter.Domain.Common.Access.Errors;
using Starter.Domain.Identity.Entities;
using Starter.Shared.Results;

namespace Starter.Infrastructure.Services.Access;

public sealed class FileOwnershipHandler(
    IApplicationDbContext db,
    IResourceAccessService access,
    ICurrentUserService currentUser) : IResourceOwnershipHandler
{
    public string ResourceType => ResourceTypes.File;

    public async Task<Result<Guid>> GetOwnerAsync(Guid resourceId, CancellationToken ct)
    {
        var file = await db.Set<FileMetadata>().FirstOrDefaultAsync(f => f.Id == resourceId, ct);
        return file is null ? Result.Failure<Guid>(AccessErrors.ResourceNotFound) : Result.Success(file.UploadedBy);
    }

    public async Task<Result> SetVisibilityAsync(Guid resourceId, ResourceVisibility visibility, CancellationToken ct)
    {
        var file = await db.Set<FileMetadata>().FirstOrDefaultAsync(f => f.Id == resourceId, ct);
        if (file is null) return Result.Failure(AccessErrors.ResourceNotFound);
        if ((int)visibility > (int)ResourceTypes.MaxVisibility(ResourceTypes.File))
            return Result.Failure(AccessErrors.VisibilityNotAllowedForResourceType);

        file.SetVisibility(visibility);
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result> TransferOwnershipAsync(Guid resourceId, Guid newOwnerId, CancellationToken ct)
    {
        var file = await db.Set<FileMetadata>().FirstOrDefaultAsync(f => f.Id == resourceId, ct);
        if (file is null) return Result.Failure(AccessErrors.ResourceNotFound);

        var newOwner = await db.Users.FirstOrDefaultAsync(u => u.Id == newOwnerId, ct);
        if (newOwner is null) return Result.Failure(AccessErrors.OwnershipTargetNotInTenant);
        if (newOwner.TenantId != file.TenantId) return Result.Failure(AccessErrors.OwnershipTargetNotInTenant);
        if (newOwner.Status != UserStatus.Active) return Result.Failure(AccessErrors.OwnershipTargetInactive);

        var oldOwner = file.UploadedBy;
        file.TransferOwnership(newOwnerId);
        await db.SaveChangesAsync(ct);

        // Demote old owner to Manager grant (idempotent UPSERT)
        await access.GrantAsync(
            ResourceTypes.File, resourceId,
            GrantSubjectType.User, oldOwner,
            AccessLevel.Manager, ct);

        return Result.Success();
    }
}
```

- [ ] **Step 2: Other commands (thin wrappers)**

Write `RevokeResourceAccessCommand(Guid GrantId)` → calls `access.RevokeAsync`. Caller must be owner of the underlying resource (use `probe.EnsureCallerCanShareAsync`).

Write `SetResourceVisibilityCommand(string ResourceType, Guid ResourceId, ResourceVisibility Visibility)` → owner or admin (probe). Additional guard: `Public` requires `Files.Manage` (files) or rejects for `AiAssistant`.

Write `TransferResourceOwnershipCommand(string ResourceType, Guid ResourceId, Guid NewOwnerId)` → calls `probe.TransferOwnershipAsync`.

`ListResourceGrantsQuery(string ResourceType, Guid ResourceId)` → calls `access.ListGrantsAsync`.

Each file mirrors the `Grant` handler pattern above: `Result<X>`, owner probe, delegate to service.

- [ ] **Step 3: Register owner handlers**

`Starter.Infrastructure/DependencyInjection.cs`:
```csharp
services.AddScoped<IResourceOwnershipProbe, ResourceOwnershipProbe>();
services.AddScoped<IResourceOwnershipHandler, FileOwnershipHandler>();
```

AI module equivalent lives in `Starter.Module.AI/AIModule.cs` (Task 11).

- [ ] **Step 4: Build + commit**

```bash
dotnet build boilerplateBE/Starter.sln
git add boilerplateBE/src/Starter.Application/Features/Access/ \
        boilerplateBE/src/Starter.Application/Common/Access/IResourceOwnershipProbe.cs \
        boilerplateBE/src/Starter.Infrastructure/Services/Access/ \
        boilerplateBE/src/Starter.Infrastructure/DependencyInjection.cs
git commit -m "feat(access): add grant/revoke/visibility/transfer commands + ownership probe"
```

---

## Phase B — Files as hub

### Task 7: Migrate `FileMetadata` from `IsPublic` to `Visibility`; add `IShareable`

**Files:**
- Modify: `boilerplateBE/src/Starter.Domain/Common/FileMetadata.cs`
- Modify: `boilerplateBE/src/Starter.Infrastructure/Persistence/Configurations/FileMetadataConfiguration.cs`
- Modify: `boilerplateBE/src/Starter.Infrastructure/Services/FileService.cs` — map visibility → `GetPublicUrl`/`GetSignedUrl`
- Modify: `boilerplateBE/src/Starter.Application/Features/Files/Commands/UploadFile/UploadFileCommand.cs` + handler — replace `IsPublic` with `Visibility` (default `Private`)
- Modify: `boilerplateBE/src/Starter.Application/Features/Files/Queries/...` and `DTOs/FileDto.cs`

- [ ] **Step 1: Update entity**

Replace the `IsPublic` property + its usage in `Create(...)` with `Visibility` (default `Private`). Add:

```csharp
public ResourceVisibility Visibility { get; private set; }

public void SetVisibility(ResourceVisibility visibility) { Visibility = visibility; }
public void TransferOwnership(Guid newOwnerUserId) { UploadedBy = newOwnerUserId; }
```

Implement `IShareable`. The old `IsPublic` boolean is removed entirely — no backwards-compat shim.

- [ ] **Step 2: Update EF configuration**

Replace the `IsPublic` column with:

```csharp
builder.Property(f => f.Visibility)
    .HasColumnName("visibility")
    .HasConversion<int>()
    .HasDefaultValue(ResourceVisibility.Private)
    .IsRequired();
```

- [ ] **Step 3: Update `FileService.GetUrlAsync` + `UploadAsync`**

`FileService.GetUrlAsync`:
```csharp
return metadata.Visibility == ResourceVisibility.Public
    ? await storageService.GetPublicUrlAsync(metadata.StorageKey, ct)
    : await storageService.GetSignedUrlAsync(metadata.StorageKey, TimeSpan.FromMinutes(_settings.SignedUrlExpirationMinutes), ct);
```

`FileService.UploadAsync` — replace `bool isPublic = false` param with `ResourceVisibility visibility = ResourceVisibility.Private`, and pass to `FileMetadata.Create`.

- [ ] **Step 4: Update `IFileService` contract and all call-sites**

Change every caller that passed `isPublic: true` to `visibility: ResourceVisibility.Public`. Grep `IsPublic` and `isPublic` across `boilerplateBE/src/` and `boilerplateFE/src/` and fix each. Fix `FileDto` to expose `Visibility` instead of `IsPublic`. Update `UploadFileCommand` and validator.

- [ ] **Step 5: Build + test**

Run: `dotnet build boilerplateBE/Starter.sln`
Expected: 0 errors. (Any broken call-site must be fixed — no placeholder shims.)

- [ ] **Step 6: Commit**

```bash
git add boilerplateBE/src/
git commit -m "feat(files): replace IsPublic with Visibility (Private|TenantWide|Public) on FileMetadata"
```

---

### Task 8: `IFileService` managed-file API + `FilesController` ACL endpoints

**Files:**
- Modify: `boilerplateBE/src/Starter.Application/Common/Interfaces/IFileService.cs`
- Modify: `boilerplateBE/src/Starter.Infrastructure/Services/FileService.cs`
- Modify: `boilerplateBE/src/Starter.Api/Controllers/FilesController.cs`
- Modify: `boilerplateBE/src/Starter.Shared/Constants/Permissions.cs` — add `Files.ShareOwn`
- Modify: `boilerplateBE/src/Starter.Shared/Constants/Roles.cs` — grant `Files.ShareOwn` to `User` + `Admin`
- Modify: `boilerplateBE/src/Starter.Application/Features/Files/Queries/GetFileUrl/GetFileUrlQueryHandler.cs` — ACL check via `CanAccessAsync`
- Modify: `boilerplateBE/src/Starter.Application/Features/Files/Queries/GetFiles/GetFilesQueryHandler.cs` — visibility+grants filter + `?view=` support

- [ ] **Step 1: Extend `IFileService` + `FileService`**

Add methods:
```csharp
Task<FileMetadata> CreateManagedFileAsync(ManagedFileUpload upload, CancellationToken ct);
Task DeleteManagedFileAsync(Guid fileId, CancellationToken ct);
Task<FileDownloadResult?> ResolveDownloadAsync(Guid fileId, CancellationToken ct);
```

`ManagedFileUpload` record:
```csharp
public sealed record ManagedFileUpload(
    Stream Stream, string FileName, string ContentType, long Size,
    FileCategory Category, ResourceVisibility Visibility,
    string? EntityType = null, Guid? EntityId = null,
    FileOrigin Origin = FileOrigin.UserUpload);

public sealed record FileDownloadResult(Stream Stream, string ContentType, string FileName);
```

Implementation uses the same `folder/{category}/{Guid.NewGuid()}/{safe}` storage-key layout already used by `FileService.UploadAsync` but centralized into a helper `BuildStorageKey(tenantId, category, fileId, safeName)`.

- [ ] **Step 2: `FilesController` endpoints**

Add (all `ApiVersion 1`, routes under `api/v1/Files`):
```
GET    /{id}/grants                          Policy: Files.View
POST   /{id}/grants                          Policy: Files.ShareOwn (+ owner-or-admin inside handler)
DELETE /{id}/grants/{grantId}                Policy: Files.ShareOwn
PUT    /{id}/visibility                      Policy: Files.ShareOwn (+ Public needs Files.Manage)
POST   /{id}/transfer-ownership              Policy: Files.ShareOwn
GET    /storage-summary                      Policy: Files.View (platform-admin toggles cross-tenant)
```

Each endpoint dispatches to the matching `Features/Access` or `Features/Files/Queries/GetStorageSummary` MediatR request with `ResourceType = ResourceTypes.File`.

- [ ] **Step 3: Permissions**

`Permissions.cs`:
```csharp
public static class Files
{
    public const string View = "Files.View";
    public const string Upload = "Files.Upload";
    public const string Delete = "Files.Delete";
    public const string Manage = "Files.Manage";
    public const string ShareOwn = "Files.ShareOwn";   // NEW
}
```

`GetAllWithMetadata`:
```csharp
yield return (Files.ShareOwn, "Share files the user owns", "Files");
```

`Roles.cs` — add `Files.ShareOwn` to `User` and `Admin` role permission arrays.

- [ ] **Step 4: `GetFilesQueryHandler` view filter**

Accept `view` parameter (`all | mine | shared | public`) and compose EF query:

```csharp
var resolution = await _access.ResolveAccessibleResourcesAsync(_currentUser, ResourceTypes.File, ct);
IQueryable<FileMetadata> q = _db.Set<FileMetadata>();

if (!resolution.IsAdminBypass)
{
    var uid = _currentUser.UserId!.Value;
    q = q.Where(f => f.Visibility == ResourceVisibility.TenantWide
                     || f.Visibility == ResourceVisibility.Public
                     || f.UploadedBy == uid
                     || resolution.ExplicitGrantedResourceIds.Contains(f.Id));
}

q = view switch
{
    "mine"   => q.Where(f => f.UploadedBy == _currentUser.UserId),
    "shared" => q.Where(f => f.UploadedBy != _currentUser.UserId
                              && (resolution.ExplicitGrantedResourceIds.Contains(f.Id)
                                  || f.Visibility == ResourceVisibility.TenantWide)),
    "public" => q.Where(f => f.Visibility == ResourceVisibility.Public),
    _        => q,
};
```

- [ ] **Step 5: `GetFileUrlQueryHandler` ACL check**

Replace existing `IsPublic` check with:
```csharp
var file = await _db.Set<FileMetadata>().FirstOrDefaultAsync(f => f.Id == request.Id, ct);
if (file is null) return Result.Failure<string>(FileErrors.NotFound);

if (file.Visibility == ResourceVisibility.Public) /* pass */
else if (file.Visibility == ResourceVisibility.TenantWide && file.TenantId == _currentUser.TenantId) /* pass */
else if (file.UploadedBy == _currentUser.UserId) /* pass */
else if (!await _access.CanAccessAsync(_currentUser, ResourceTypes.File, file.Id, AccessLevel.Viewer, ct))
    return Result.Failure<string>(AccessErrors.ResourceNotFound);
```

- [ ] **Step 6: Build + run existing file tests**

Run: `dotnet test boilerplateBE/tests/Starter.Api.Tests --filter FullyQualifiedName~Files`
Expected: PASS or clearly scoped failures (we'll add new regression tests in Task 18).

- [ ] **Step 7: Commit**

```bash
git add boilerplateBE/src/
git commit -m "feat(files): add ACL endpoints, managed-file API, and view filter"
```

---

### Task 9: `GetStorageSummaryQuery` + endpoint

**Files:**
- Create: `boilerplateBE/src/Starter.Application/Features/Files/Queries/GetStorageSummary/{Query,Handler,Dto}.cs`

- [ ] **Step 1: DTO + handler**

```csharp
public sealed record StorageSummaryDto(
    long TotalBytes,
    IReadOnlyList<CategoryBytes> ByCategory,
    IReadOnlyList<EntityTypeBytes> ByEntityType,
    IReadOnlyList<UploaderBytes> TopUploaders);

public sealed record CategoryBytes(string Category, long Bytes, int FileCount);
public sealed record EntityTypeBytes(string? EntityType, long Bytes, int FileCount);
public sealed record UploaderBytes(Guid UserId, string? UserName, long Bytes, int FileCount);
```

```csharp
public sealed record GetStorageSummaryQuery(bool AllTenants = false) : IRequest<Result<StorageSummaryDto>>;

internal sealed class GetStorageSummaryQueryHandler(
    IApplicationDbContext db,
    ICurrentUserService currentUser)
    : IRequestHandler<GetStorageSummaryQuery, Result<StorageSummaryDto>>
{
    public async Task<Result<StorageSummaryDto>> Handle(GetStorageSummaryQuery q, CancellationToken ct)
    {
        var query = db.Set<FileMetadata>().AsNoTracking();
        if (!(q.AllTenants && currentUser.TenantId is null))
            query = query.Where(f => f.TenantId == currentUser.TenantId);

        var byCat = await query.GroupBy(f => f.Category)
            .Select(g => new CategoryBytes(g.Key.ToString(), g.Sum(f => f.Size), g.Count()))
            .ToListAsync(ct);

        var byEt = await query.GroupBy(f => f.EntityType)
            .Select(g => new EntityTypeBytes(g.Key, g.Sum(f => f.Size), g.Count()))
            .ToListAsync(ct);

        var top = await query.GroupBy(f => f.UploadedBy)
            .Select(g => new { UserId = g.Key, Bytes = g.Sum(f => f.Size), Count = g.Count() })
            .OrderByDescending(x => x.Bytes).Take(10).ToListAsync(ct);
        var names = await db.Users.Where(u => top.Select(t => t.UserId).Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => (u.FirstName + " " + u.LastName).Trim(), ct);
        var topOut = top.Select(x => new UploaderBytes(x.UserId, names.GetValueOrDefault(x.UserId), x.Bytes, x.Count)).ToList();

        var total = byCat.Sum(c => c.Bytes);
        return Result.Success(new StorageSummaryDto(total, byCat, byEt, topOut));
    }
}
```

- [ ] **Step 2: Wire endpoint + commit**

`FilesController` — `GET /storage-summary` dispatches `GetStorageSummaryQuery`.

```bash
git add boilerplateBE/src/Starter.Application/Features/Files/Queries/GetStorageSummary/ \
        boilerplateBE/src/Starter.Api/Controllers/FilesController.cs
git commit -m "feat(files): add storage-summary endpoint"
```

---

## Phase C — AI module integration

### Task 10: `AiDocument.FileId` replaces `FileRef`; `UploadDocumentCommandHandler` routes via `FileService`

**Files:**
- Modify: `boilerplateBE/src/modules/Starter.Module.AI/Domain/Entities/AiDocument.cs`
- Modify: `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Persistence/Configurations/AiDocumentConfiguration.cs`
- Modify: `boilerplateBE/src/modules/Starter.Module.AI/Application/Commands/UploadDocument/UploadDocumentCommandHandler.cs`
- Modify all readers that read `FileRef` — replace with `FileId` lookup against `FileMetadata`

- [ ] **Step 1: Entity change**

Replace `FileRef` with `FileId`:
```csharp
public Guid FileId { get; private set; }

// In factory: take `fileId` instead of `fileRef`.
```

Drop `.FileRef` everywhere in the AI module. No shim.

- [ ] **Step 2: EF config**

Rename column `file_ref` → `file_id` with `uuid` type + foreign key to `files(id)` (no cascade: we cascade manually so we can delete Qdrant points). Add index on `file_id`.

- [ ] **Step 3: Handler refactor**

```csharp
public async Task<Result<AiDocumentDto>> Handle(UploadDocumentCommand request, CancellationToken ct)
{
    if (currentUser.UserId is not Guid userId)
        return Result.Failure<AiDocumentDto>(AiErrors.NotAuthenticated);

    var file = request.File;
    await using var stream = file.OpenReadStream();

    var managed = await fileService.CreateManagedFileAsync(new ManagedFileUpload(
        Stream: stream,
        FileName: file.FileName,
        ContentType: file.ContentType,
        Size: file.Length,
        Category: FileCategory.AiDocument,
        Visibility: ResourceVisibility.Private,
        EntityType: "AiDocument",
        Origin: FileOrigin.UserUpload), ct);

    var contentHash = /* compute SHA256 of stream content — stream is already consumed, re-open via storage */;

    var doc = AiDocument.Create(
        tenantId: currentUser.TenantId,
        name: string.IsNullOrWhiteSpace(request.Name) ? managed.FileName : request.Name!,
        fileName: managed.FileName,
        fileId: managed.Id,
        contentType: managed.ContentType,
        sizeBytes: managed.Size,
        uploadedByUserId: userId);
    doc.SetContentHash(contentHash);

    db.AiDocuments.Add(doc);
    await db.SaveChangesAsync(ct);

    // Link FileMetadata.EntityId to AiDocument.Id
    await fileService.AttachToEntityAsync(managed.Id, doc.Id, "AiDocument", ct);

    await bus.Publish(new ProcessDocumentMessage(doc.Id, doc.TenantId, userId), ct);
    await appDb.SaveChangesAsync(ct);
    return Result.Success(doc.ToDto());
}
```

The hash needs a second pass over the stored file — read the `storageKey` via `IStorageService.GetStreamAsync` if available; otherwise wrap the original `IFormFile` stream in a `CryptoStream` during upload (preferred, saves a round-trip). Use whichever pattern already exists in `S3StorageService`.

Add `FileCategory.AiDocument` to `Starter.Domain/Common/Enums/FileCategory.cs` if not present.

- [ ] **Step 4: Update readers**

Anywhere the AI module reads `FileRef` (grep `FileRef` within `modules/Starter.Module.AI/`), replace with:
```csharp
var file = await appDb.Set<FileMetadata>().FirstOrDefaultAsync(f => f.Id == doc.FileId, ct);
var storageKey = file!.StorageKey;
```

- [ ] **Step 5: Build + commit**

```bash
dotnet build boilerplateBE/Starter.sln
git add boilerplateBE/src/
git commit -m "feat(ai): route AI uploads through FileService; replace AiDocument.FileRef with FileId"
```

---

### Task 11: `AiAssistant` — Visibility / AccessMode / CreatedByUserId + controller endpoints + chat gate + handler registration

**Files:**
- Modify: `boilerplateBE/src/modules/Starter.Module.AI/Domain/Entities/AiAssistant.cs`
- Modify: `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Persistence/Configurations/AiAssistantConfiguration.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Access/AiAssistantOwnershipHandler.cs`
- Modify: `boilerplateBE/src/modules/Starter.Module.AI/Controllers/AiAssistantsController.cs` — grant/visibility/access-mode endpoints + chat pre-gate
- Modify: `boilerplateBE/src/modules/Starter.Module.AI/AIModule.cs` — DI registration of owner handler

- [ ] **Step 1: Entity fields + methods**

```csharp
public ResourceVisibility Visibility { get; private set; } = ResourceVisibility.Private;
public AssistantAccessMode AccessMode { get; private set; } = AssistantAccessMode.CallerPrincipal;
public Guid CreatedByUserId { get; private set; }

public void SetVisibility(ResourceVisibility v) {
    if ((int)v > (int)ResourceVisibility.TenantWide)
        throw new DomainException(AccessErrors.VisibilityNotAllowedForResourceType.Description,
                                  AccessErrors.VisibilityNotAllowedForResourceType.Code);
    Visibility = v;
    ModifiedAt = DateTime.UtcNow;
}
public void SetAccessMode(AssistantAccessMode m) { AccessMode = m; ModifiedAt = DateTime.UtcNow; }
public void TransferOwnership(Guid newOwner) { CreatedByUserId = newOwner; ModifiedAt = DateTime.UtcNow; }
```

Factory `Create(...)` takes a new `Guid createdByUserId` parameter. `IShareable` implemented.

- [ ] **Step 2: EF config — new columns + defaults**

```csharp
b.Property(a => a.Visibility).HasColumnName("visibility").HasConversion<int>().HasDefaultValue(ResourceVisibility.Private);
b.Property(a => a.AccessMode).HasColumnName("access_mode").HasConversion<int>().HasDefaultValue(AssistantAccessMode.CallerPrincipal);
b.Property(a => a.CreatedByUserId).HasColumnName("created_by_user_id").IsRequired();
```

- [ ] **Step 3: Owner handler + DI**

```csharp
// AiAssistantOwnershipHandler.cs
using Microsoft.EntityFrameworkCore;
using Starter.Application.Common.Access.Contracts;
using Starter.Application.Common.Interfaces;
using Starter.Domain.Common.Access.Enums;
using Starter.Domain.Common.Access.Errors;
using Starter.Infrastructure.Services.Access;
using Starter.Module.AI.Infrastructure.Persistence;
using Starter.Shared.Results;

namespace Starter.Module.AI.Infrastructure.Access;

public sealed class AiAssistantOwnershipHandler(AiDbContext db) : IResourceOwnershipHandler
{
    public string ResourceType => ResourceTypes.AiAssistant;

    public async Task<Result<Guid>> GetOwnerAsync(Guid id, CancellationToken ct)
    {
        var a = await db.AiAssistants.FirstOrDefaultAsync(x => x.Id == id, ct);
        return a is null ? Result.Failure<Guid>(AccessErrors.ResourceNotFound) : Result.Success(a.CreatedByUserId);
    }

    public async Task<Result> SetVisibilityAsync(Guid id, ResourceVisibility v, CancellationToken ct)
    {
        var a = await db.AiAssistants.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (a is null) return Result.Failure(AccessErrors.ResourceNotFound);
        if ((int)v > (int)ResourceTypes.MaxVisibility(ResourceTypes.AiAssistant))
            return Result.Failure(AccessErrors.VisibilityNotAllowedForResourceType);
        a.SetVisibility(v);
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result> TransferOwnershipAsync(Guid id, Guid newOwnerId, CancellationToken ct)
    {
        var a = await db.AiAssistants.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (a is null) return Result.Failure(AccessErrors.ResourceNotFound);
        a.TransferOwnership(newOwnerId);
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }
}
```

Register in `AIModule.ConfigureServices`:
```csharp
services.AddScoped<IResourceOwnershipHandler, AiAssistantOwnershipHandler>();
```

- [ ] **Step 4: Controller endpoints + chat gate**

`AiAssistantsController` — add:
```
GET    /ai/assistants/{id}/grants             Policy: Ai.Chat
POST   /ai/assistants/{id}/grants             Policy: Ai.Chat    (+ owner/admin via handler)
DELETE /ai/assistants/{id}/grants/{grantId}   Policy: Ai.Chat
PUT    /ai/assistants/{id}/visibility         Policy: Ai.ManageAssistants
PUT    /ai/assistants/{id}/access-mode        Policy: Ai.ManageAssistants   (+ require admin role inside handler)
POST   /ai/assistants/{id}/transfer-ownership Policy: Ai.ManageAssistants
```

Chat endpoint (`POST /ai/conversations/{id}/messages` or equivalent chat entry) gains a pre-check:
```csharp
if (!await _access.CanAccessAsync(currentUser, ResourceTypes.AiAssistant, assistantId, AccessLevel.Viewer, ct))
    return Forbid();
```

- [ ] **Step 5: List filter**

`GET /ai/assistants` — apply `_access.ResolveAccessibleResourcesAsync` + `Visibility=TenantWide` + `CreatedByUserId=me`:

```csharp
var r = await _access.ResolveAccessibleResourcesAsync(currentUser, ResourceTypes.AiAssistant, ct);
var q = _db.AiAssistants.AsNoTracking();
if (!r.IsAdminBypass)
    q = q.Where(a => a.Visibility == ResourceVisibility.TenantWide
                  || a.CreatedByUserId == currentUser.UserId
                  || r.ExplicitGrantedResourceIds.Contains(a.Id));
```

- [ ] **Step 6: Seed update**

Wherever `SeedSampleAssistant` creates the default assistant (grep for it), set:
```csharp
Visibility = ResourceVisibility.TenantWide,
AccessMode = AssistantAccessMode.CallerPrincipal,
CreatedByUserId = superAdminUserId,
```

- [ ] **Step 7: Build + commit**

```bash
git add boilerplateBE/src/modules/Starter.Module.AI/
git commit -m "feat(ai): add Visibility/AccessMode to AiAssistant + ACL endpoints + chat gate"
```

---

### Task 12: Qdrant payload enrichment (stable ACL fields)

**Files:**
- Modify: `boilerplateBE/src/modules/Starter.Module.AI/Application/Services/Retrieval/VectorPoint.cs` — add fields to `VectorPayload`
- Modify: `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Ingestion/QdrantVectorStore.cs` — emit new keys on upsert + on `SearchAsync` filter

- [ ] **Step 1: Extend `VectorPayload` record**

Add properties (locate the record — likely alongside `VectorSearchHit`):
```csharp
public Guid FileId { get; init; }
public ResourceVisibility Visibility { get; init; }
public Guid UploadedByUserId { get; init; }
```

- [ ] **Step 2: Emit new keys in `UpsertAsync`**

```csharp
Payload =
{
    // existing...
    ["file_id"]               = p.Payload.FileId.ToString(),
    ["visibility"]            = (int)p.Payload.Visibility,
    ["uploaded_by_user_id"]   = p.Payload.UploadedByUserId.ToString(),
}
```

- [ ] **Step 3: Accept filter contract on `SearchAsync`**

New optional parameter:
```csharp
Task<IReadOnlyList<VectorSearchHit>> SearchAsync(
    Guid tenantId,
    float[] queryVector,
    IReadOnlyCollection<Guid>? documentFilter,
    AclPayloadFilter? aclFilter,      // NEW
    int limit,
    CancellationToken ct);
```

`AclPayloadFilter` record:
```csharp
public sealed record AclPayloadFilter(
    Guid UserId,
    ResourceVisibility MinVisibilityTenantWide,    // always TenantWide for now
    IReadOnlyCollection<Guid> GrantedFileIds);
```

Build Qdrant condition:
```csharp
var shouldConds = new List<Condition>
{
    new() { Field = new FieldCondition { Key = "visibility", Match = new Match { Integer = (long)ResourceVisibility.TenantWide } } },
    new() { Field = new FieldCondition { Key = "visibility", Match = new Match { Integer = (long)ResourceVisibility.Public } } },
    new() { Field = new FieldCondition { Key = "uploaded_by_user_id", Match = new Match { Keyword = aclFilter.UserId.ToString() } } },
};
if (aclFilter.GrantedFileIds.Count > 0)
{
    var kws = new RepeatedStrings();
    foreach (var id in aclFilter.GrantedFileIds) kws.Strings.Add(id.ToString());
    shouldConds.Add(new() { Field = new FieldCondition { Key = "file_id", Match = new Match { Keywords = kws } } });
}
filter.Should.AddRange(shouldConds);
filter.MinShould = new MinShould { Conditions = { shouldConds }, MinCount = 1 };
```

(Actual Qdrant .NET SDK types: check `Qdrant.Client.Grpc.Filter.Should` + `MinShould`. If `MinShould` not available in the linked SDK version, wrap the `Should` group in a nested `Must { HasShould }` alternative — use whichever the SDK supports; treat Qdrant SDK version as authoritative.)

- [ ] **Step 4: Build + commit**

```bash
git add boilerplateBE/src/modules/Starter.Module.AI/
git commit -m "feat(ai): stamp ACL fields on Qdrant payload and support ACL-filtered search"
```

---

### Task 13: Denormalize ACL fields on `AiDocumentChunk`; keyword-search push-down

**Files:**
- Modify: `boilerplateBE/src/modules/Starter.Module.AI/Domain/Entities/AiDocumentChunk.cs` — add `FileId`, `Visibility`, `UploadedByUserId` columns
- Modify: `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Persistence/Configurations/AiDocumentChunkConfiguration.cs` — map columns + index
- Modify: `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Retrieval/PostgresKeywordSearchService.cs` — accept `AclPayloadFilter` and inject WHERE clauses
- Modify: document-ingest handler (where chunks are created) to stamp these fields from the parent `AiDocument` + `FileMetadata`

- [ ] **Step 1: Entity fields + factory**

```csharp
public Guid FileId { get; private set; }
public ResourceVisibility Visibility { get; private set; }
public Guid UploadedByUserId { get; private set; }
```

Extend `AiDocumentChunk.Create(...)` with the three new params.

- [ ] **Step 2: EF config**

```csharp
b.Property(c => c.FileId).HasColumnName("file_id").IsRequired();
b.Property(c => c.Visibility).HasColumnName("visibility").HasConversion<int>().IsRequired();
b.Property(c => c.UploadedByUserId).HasColumnName("uploaded_by_user_id").IsRequired();
b.HasIndex(c => new { c.FileId, c.ChunkLevel });
```

- [ ] **Step 3: Keyword search filter**

`PostgresKeywordSearchService.SearchAsync` — extend SQL:
```sql
AND (
    c.visibility = 1                                       -- TenantWide
    OR c.visibility = 2                                    -- Public
    OR c.uploaded_by_user_id = {userId}
    OR c.file_id = ANY({grantedFileIds}::uuid[])
)
```

- [ ] **Step 4: Ingest-handler update**

Locate the chunk-creation site (grep `AiDocumentChunk.Create`). Pass the parent `AiDocument`'s `FileId`, and look up `FileMetadata.Visibility`/`UploadedBy` once for the whole document.

- [ ] **Step 5: Commit**

```bash
git add boilerplateBE/src/modules/Starter.Module.AI/
git commit -m "feat(ai): denormalize ACL fields onto AiDocumentChunk and push-down keyword filter"
```

---

### Task 14: `acl-resolve` stage in `RagRetrievalService`

**Files:**
- Modify: `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Retrieval/RagStages.cs`
- Modify: `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Settings/AiRagSettings.cs` — add `StageTimeoutAclResolveMs` + `AclCacheTtlSeconds`
- Modify: `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Retrieval/RagRetrievalService.cs` — inject `IResourceAccessService`; run `acl-resolve` between query-planning and search; thread `AclPayloadFilter` through to vector + keyword calls; fail-closed

- [ ] **Step 1: Add stage name**

`RagStages.cs`:
```csharp
public const string AclResolve = "acl-resolve";
```

- [ ] **Step 2: Settings**

`AiRagSettings.cs`:
```csharp
public int StageTimeoutAclResolveMs { get; set; } = 1500;
public int AclCacheTtlSeconds { get; set; } = 60;
```

Also add corresponding keys (disabled / default) in `boilerplateBE/src/Starter.Api/appsettings.json` + `appsettings.Development.json` under `AI:Rag`.

- [ ] **Step 3: `RagRetrievalService` wiring**

Inject `IResourceAccessService` and `ICurrentUserService`. In `RetrieveForTurnAsync`/`RetrieveForQueryAsync`, after `EmbedQuery`:

```csharp
AclPayloadFilter? aclFilter = null;
if (assistant.AccessMode == AssistantAccessMode.CallerPrincipal)
{
    var resolution = await WithTimeoutAsync<AccessResolution>(
        tok => _access.ResolveAccessibleResourcesAsync(_currentUser, ResourceTypes.File, tok),
        settings.StageTimeoutAclResolveMs,
        RagStages.AclResolve,
        degraded,
        ct);

    if (resolution is null)
    {
        // fail-closed: empty context + surface degraded stage
        return new RetrievedContext(
            Children: Array.Empty<RetrievedChunk>(),
            Parents: Array.Empty<RetrievedChunk>(),
            UsedTokens: 0, Truncated: false, DegradedStages: degraded,
            Siblings: Array.Empty<RetrievedChunk>(),
            FusedCandidatesCount: 0, DetectedLang: null);
    }
    if (!resolution.IsAdminBypass)
    {
        aclFilter = new AclPayloadFilter(
            UserId: _currentUser.UserId!.Value,
            MinVisibilityTenantWide: ResourceVisibility.TenantWide,
            GrantedFileIds: resolution.ExplicitGrantedResourceIds);
    }
}
// AssistantPrincipal or admin-bypass → aclFilter stays null (tenant-scoped only)
```

Pass `aclFilter` into `_vectorStore.SearchAsync(..., aclFilter, limit, ct)` and `_keywordSearch.SearchAsync(..., aclFilter, ct)`.

- [ ] **Step 4: Commit**

```bash
git add boilerplateBE/src/modules/Starter.Module.AI/ \
        boilerplateBE/src/Starter.Api/appsettings.*.json
git commit -m "feat(ai): add acl-resolve stage with fail-closed degradation"
```

---

## Phase D — Cascades, ownership, audit, notifications

### Task 15: Cascade grant revocation on resource/user/role/tenant delete

**Files:**
- Modify: `boilerplateBE/src/Starter.Application/Features/Files/Commands/DeleteFile/DeleteFileCommandHandler.cs`
- Modify: `boilerplateBE/src/modules/Starter.Module.AI/Application/Commands/DeleteDocument/...` (if exists)
- Modify: `boilerplateBE/src/modules/Starter.Module.AI/Application/Commands/DeleteAssistant/...` (if exists)
- Modify: `boilerplateBE/src/Starter.Application/Features/Users/Commands/DeleteUser/DeleteUserCommandHandler.cs`
- Modify: `boilerplateBE/src/Starter.Application/Features/Roles/Commands/DeleteRole/DeleteRoleCommandHandler.cs`
- Modify: `boilerplateBE/src/Starter.Application/Features/Tenants/Commands/DeleteTenant/DeleteTenantCommandHandler.cs`

For each handler, after the existing delete operation, add:

**File delete:**
```csharp
await _access.RevokeAllForResourceAsync(ResourceTypes.File, file.Id, ct);
// Existing MinIO delete via IFileService.DeleteManagedFileAsync already runs.
```

**AI document delete:** chunks + Qdrant points (existing) + `await _fileService.DeleteManagedFileAsync(doc.FileId, ct)` (which itself cascades the file-grant revoke in the file-delete handler).

**AI assistant delete:** `await _access.RevokeAllForResourceAsync(ResourceTypes.AiAssistant, assistant.Id, ct)` + existing session cleanup.

**User delete:**
```csharp
var toDelete = await _db.ResourceGrants
    .Where(g => g.SubjectType == GrantSubjectType.User && g.SubjectId == userId)
    .ToListAsync(ct);
_db.ResourceGrants.RemoveRange(toDelete);
// Plus: invalidate that user's cache key (remove aclv:u:*:userId).
```

**Role delete:** same pattern filtering on `SubjectType = Role && SubjectId = roleId`, plus invalidate all members' cache.

**Tenant delete:**
```csharp
var toDelete = await _db.ResourceGrants
    .IgnoreQueryFilters()
    .Where(g => g.TenantId == tenantId)
    .ToListAsync(ct);
_db.ResourceGrants.RemoveRange(toDelete);
```

- [ ] **Step 1: Implement + build + commit**

```bash
dotnet build boilerplateBE/Starter.sln
git add boilerplateBE/src/
git commit -m "feat(access): cascade grant revocation on resource/user/role/tenant delete"
```

---

### Task 16: Audit events for all grant/visibility/ownership/access-mode changes

**Files:**
- Modify: every `Access` command handler (`GrantResourceAccess`, `RevokeResourceAccess`, `SetResourceVisibility`, `TransferResourceOwnership`, and `SetAssistantAccessMode` inside AI module)

Each handler writes an `AuditLog` row before returning success. Pattern (match existing handler audit style — likely via `_db.AuditLogs.Add` or `IAuditLogService`):

```csharp
_db.AuditLogs.Add(AuditLog.Create(
    entityType: request.ResourceType,
    entityId: request.ResourceId,
    action: AuditAction.Updated,
    changes: JsonSerializer.Serialize(new {
        Event = "ResourceGrantCreated",
        request.SubjectType, request.SubjectId, request.Level
    }),
    performedBy: currentUser.UserId,
    tenantId: currentUser.TenantId));
```

Specific event names:
- `ResourceGrantCreated` / `Updated` / `Revoked`
- `ResourceVisibilityChanged`
- `ResourceVisibilityMadePublic` (**second** audit row in addition to the `Changed` row when visibility → `Public`)
- `ResourceOwnershipTransferred`
- `AssistantAccessModeChanged`

- [ ] **Step 1: Implement + commit**

```bash
git add boilerplateBE/src/
git commit -m "feat(access): emit audit events for all grant/visibility/ownership/access-mode changes"
```

---

### Task 17: `ResourceShared` notification on grant to a user

**Files:**
- Modify: `GrantResourceAccessCommandHandler` — after success, if `SubjectType=User`, call `INotificationService.CreateAsync(subjectId, tenantId, "ResourceShared", title, message, data)` with resource name looked up via the owner handler + `probe.GetResourceDisplayNameAsync` (add a display-name method to `IResourceOwnershipHandler`).

Add to `IResourceOwnershipHandler`:
```csharp
Task<Result<string>> GetDisplayNameAsync(Guid resourceId, CancellationToken ct);
```

Implement in `FileOwnershipHandler` → `file.FileName`, and `AiAssistantOwnershipHandler` → `assistant.Name`.

In the grant handler:
```csharp
if (request.SubjectType == GrantSubjectType.User)
{
    var name = await probe.GetResourceDisplayNameAsync(request.ResourceType, request.ResourceId, ct);
    var resourceName = name.IsSuccess ? name.Value : request.ResourceType;
    var data = JsonSerializer.Serialize(new { request.ResourceType, request.ResourceId, request.Level });
    await notifications.CreateAsync(
        userId: request.SubjectId,
        tenantId: currentUser.TenantId,
        type: "ResourceShared",
        title: $"Shared with you: {resourceName}",
        message: $"{currentUser.Email} gave you {request.Level} access to {resourceName}.",
        data: data,
        ct: ct);
}
```

- [ ] **Step 1: Implement + commit**

```bash
git add boilerplateBE/src/
git commit -m "feat(access): notify grantee when resource is shared with them"
```

---

## Phase E — Tests + docs

### Task 18: Integration tests — `AclIntegrationTests`

**Files:**
- Create: `boilerplateBE/tests/Starter.Api.Tests/Ai/Fakes/FakeResourceAccessService.cs`
- Create: `boilerplateBE/tests/Starter.Api.Tests/Ai/Retrieval/Acl/AclIntegrationTests.cs`

Follow `MmrIntegrationTests` pattern (ctor-less `sealed` class; `CreateDb()` factory; `BuildService(...)` with fakes; named `[Fact]` per scenario).

`FakeResourceAccessService` stores grants in an in-memory dictionary, exposes a `CallCount`, and can be toggled to `ForceAdminBypass = true`.

Test scenarios (one `[Fact]` each; copy the MMR test style of seed-then-assert):

- [ ] **Step 1: Test shells**

```csharp
[Fact] public async Task CallerPrincipal_AllTenant_noGrants_returnsOnly_TenantWide()
[Fact] public async Task CallerPrincipal_AllTenant_withGrant_returnsGrantedPrivatePlusTenantWide()
[Fact] public async Task CallerPrincipal_SelectedDocs_intersects_with_KnowledgeBase()
[Fact] public async Task AssistantPrincipal_AllTenant_ignoresCallerAcl()
[Fact] public async Task AssistantPrincipal_SelectedDocs_usesKnowledgeBaseOnly()
[Fact] public async Task AdminBypass_callsVectorSearchWithNullAclFilter()
[Fact] public async Task EmptyAccessibleSet_shortCircuitsPipeline()   // vectorStore.CallCount == 0
[Fact] public async Task AclResolveDegraded_populatesDegradedStages_andReturnsEmptyContext()
[Fact] public async Task WarmCache_secondCall_doesNotHitResolver()   // CallCount == 1
[Fact] public async Task GrantChangeEvent_bumpsVersion_andNextCallHitsResolver()
```

- [ ] **Step 2: Implement each test**

Seed chunks + matching `FileMetadata` rows (use `TestDbFactory` extended to set up both `AiDbContext` and `ApplicationDbContext` with shared in-memory DB name). Configure `FakeVectorStore.HitsToReturn` to mirror the accessible + non-accessible doc IDs. After retrieval, assert on returned children.

For `AclResolveDegraded`: configure `FakeResourceAccessService.ThrowOnResolve = true`; assert `RetrievedContext.DegradedStages` contains `"acl-resolve"` and `Children` is empty.

- [ ] **Step 3: Run + commit**

```bash
dotnet test boilerplateBE/tests/Starter.Api.Tests --filter FullyQualifiedName~AclIntegrationTests
git add boilerplateBE/tests/
git commit -m "test(ai): integration coverage for acl-resolve stage and filter composition"
```

---

### Task 19: API / security regression tests — `AclApiRegressionTests`

**Files:**
- Create: `boilerplateBE/tests/Starter.Api.Tests/Files/AclApiRegressionTests.cs`
- Create: `boilerplateBE/tests/Starter.Api.Tests/Ai/Assistants/AssistantAclApiTests.cs`

Use whatever WebApplicationFactory the repo has (see `boilerplateBE/tests/Starter.Api.Tests/_Infrastructure/` if present; else extend `TestApiFactory`).

One `[Fact]` per row of the §7.3 table:
- Tenant A user chatting with Tenant B assistant → 404
- User without assistant grant → 403 before vector/keyword (assert `FakeVectorStore.CallCount == 0`)
- Private file download without grant → 403
- `PUT /files/{id}/visibility Public` by non-admin owner → 403
- `PUT /ai/assistants/{id}/access-mode AssistantPrincipal` by owner non-admin → 403
- Grant/revoke endpoints write expected audit rows (assert via `context.AuditLogs` count + event name in `Changes` JSON)
- `Public` on `AiAssistant` → 400 with error code `Access.VisibilityNotAllowedForResourceType`

- [ ] **Step 1: Implement + commit**

```bash
dotnet test boilerplateBE/tests/Starter.Api.Tests --filter "FullyQualifiedName~AclApiRegressionTests|FullyQualifiedName~AssistantAclApiTests"
git add boilerplateBE/tests/
git commit -m "test(access): API security regression for ACL gates"
```

---

### Task 20: Performance-guard tests

**Files:**
- Create: `boilerplateBE/tests/Starter.Api.Tests/Access/AclPerformanceTests.cs`

Three `[Fact]`s matching spec §7.4:
- `acl_resolve_warmCache_p95_under_5ms` — seed cache, loop 100 calls, compute p95 from a `Stopwatch.ElapsedTicks` histogram, assert `< 5ms`.
- `acl_resolve_coldResolver_p95_under_25ms` — clear cache each iteration.
- `visibility_change_payload_update_batches_1000chunks_within_two_roundtrips` — use a counting Qdrant double that records `set_payload` call count.

- [ ] **Step 1: Implement + commit**

```bash
dotnet test boilerplateBE/tests/Starter.Api.Tests --filter FullyQualifiedName~AclPerformanceTests
git add boilerplateBE/tests/
git commit -m "test(access): perf guards for acl-resolve latency and payload-update batching"
```

---

### Task 21: CLAUDE.md — Core/Module/Shared block

**Files:**
- Modify: `CLAUDE.md` — add a subsection under `## Architecture Overview`

- [ ] **Step 1: Edit**

Insert between existing "Clean Architecture" bullets and "Multi-tenancy":

```markdown
### Core vs. Module vs. Shared

- **Core feature** — required by other features or cross-cutting (access control, auth, audit, notifications, files). Lives in `Starter.Domain` / `Starter.Application` / `Starter.Infrastructure`; uses `ApplicationDbContext`.
- **Module** (`src/modules/Starter.Module.*`) — optional vertical with its own bounded context, DbContext, migrations, and DI module. Modules may depend on core; core must **not** depend on a module.
- **Shared** (`Starter.Shared`) — constants, permissions, error codes, enums with no behavior. No EF entities, no services.

When in doubt: if more than one module needs it, it's core.
```

- [ ] **Step 2: Commit**

```bash
git add CLAUDE.md
git commit -m "docs: document Core vs. Module vs. Shared layering"
```

---

## Phase F — Frontend

### Task 22: Types + generic access API hooks + permissions mirror

**Files:**
- Modify: `boilerplateFE/src/types/file.types.ts` — `isPublic: boolean` → `visibility: 'Private' | 'TenantWide' | 'Public'`
- Create: `boilerplateFE/src/features/access/types.ts`
- Create: `boilerplateFE/src/features/access/api/access.api.ts`
- Create: `boilerplateFE/src/features/access/api/access.queries.ts`
- Create: `boilerplateFE/src/features/access/index.ts`
- Modify: `boilerplateFE/src/config/api.config.ts` — add Access + Files extensions
- Modify: `boilerplateFE/src/constants/permissions.ts` — add `Files.ShareOwn`

- [ ] **Step 1: Types**

```ts
// features/access/types.ts
export type ResourceType = 'File' | 'AiAssistant';
export type ResourceVisibility = 'Private' | 'TenantWide' | 'Public';
export type GrantSubjectType = 'User' | 'Role';
export type AccessLevel = 'Viewer' | 'Editor' | 'Manager';
export type AssistantAccessMode = 'CallerPrincipal' | 'AssistantPrincipal';

export interface ResourceGrant {
  id: string;
  resourceType: ResourceType;
  resourceId: string;
  subjectType: GrantSubjectType;
  subjectId: string;
  subjectDisplayName: string | null;
  level: AccessLevel;
  grantedByUserId: string;
  grantedAt: string;
}
```

```ts
// types/file.types.ts (patch)
export type FileVisibility = 'Private' | 'TenantWide' | 'Public';

export interface FileMetadata {
  // ...existing fields (remove isPublic)
  visibility: FileVisibility;
}
```

- [ ] **Step 2: API endpoint map**

`api.config.ts` — add:
```ts
ACCESS: {
  GRANTS: (resourceType: string, resourceId: string) => `/${resourceType}s/${resourceId}/grants`,
  GRANT: (resourceType: string, resourceId: string, grantId: string) =>
    `/${resourceType}s/${resourceId}/grants/${grantId}`,
  VISIBILITY: (resourceType: string, resourceId: string) => `/${resourceType}s/${resourceId}/visibility`,
  TRANSFER_OWNERSHIP: (resourceType: string, resourceId: string) =>
    `/${resourceType}s/${resourceId}/transfer-ownership`,
},
FILES: {
  // existing...
  STORAGE_SUMMARY: '/Files/storage-summary',
  GRANTS: (id: string) => `/Files/${id}/grants`,
  GRANT: (id: string, grantId: string) => `/Files/${id}/grants/${grantId}`,
  VISIBILITY: (id: string) => `/Files/${id}/visibility`,
  TRANSFER_OWNERSHIP: (id: string) => `/Files/${id}/transfer-ownership`,
},
```

- [ ] **Step 3: Generic access API + hooks**

```ts
// access.api.ts
import { apiClient } from '@/lib/axios';
import { API_ENDPOINTS } from '@/config';
import type { ResourceGrant, ResourceType, ResourceVisibility, GrantSubjectType, AccessLevel } from '../types';

export const accessApi = {
  list: (resourceType: ResourceType, resourceId: string): Promise<ResourceGrant[]> =>
    apiClient.get(API_ENDPOINTS.ACCESS.GRANTS(resourceType, resourceId)).then(r => r.data.data),
  grant: (resourceType: ResourceType, resourceId: string, body: { subjectType: GrantSubjectType; subjectId: string; level: AccessLevel }) =>
    apiClient.post(API_ENDPOINTS.ACCESS.GRANTS(resourceType, resourceId), body),
  revoke: (resourceType: ResourceType, resourceId: string, grantId: string) =>
    apiClient.delete(API_ENDPOINTS.ACCESS.GRANT(resourceType, resourceId, grantId)),
  setVisibility: (resourceType: ResourceType, resourceId: string, visibility: ResourceVisibility) =>
    apiClient.put(API_ENDPOINTS.ACCESS.VISIBILITY(resourceType, resourceId), { visibility }),
  transferOwnership: (resourceType: ResourceType, resourceId: string, newOwnerId: string) =>
    apiClient.post(API_ENDPOINTS.ACCESS.TRANSFER_OWNERSHIP(resourceType, resourceId), { newOwnerId }),
};
```

```ts
// access.queries.ts
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { toast } from 'sonner';
import i18n from '@/i18n';
import { accessApi } from './access.api';
import type { ResourceType, ResourceVisibility, GrantSubjectType, AccessLevel } from '../types';

const qk = {
  grants: (t: ResourceType, id: string) => ['access', 'grants', t, id] as const,
};

export function useResourceGrants(resourceType: ResourceType, resourceId: string) {
  return useQuery({
    queryKey: qk.grants(resourceType, resourceId),
    queryFn: () => accessApi.list(resourceType, resourceId),
    enabled: !!resourceId,
  });
}

export function useGrantResourceAccess(resourceType: ResourceType, resourceId: string) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (body: { subjectType: GrantSubjectType; subjectId: string; level: AccessLevel }) =>
      accessApi.grant(resourceType, resourceId, body),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: qk.grants(resourceType, resourceId) });
      toast.success(i18n.t('access.grantAdded'));
    },
  });
}

export function useRevokeResourceGrant(resourceType: ResourceType, resourceId: string) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (grantId: string) => accessApi.revoke(resourceType, resourceId, grantId),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: qk.grants(resourceType, resourceId) });
      toast.success(i18n.t('access.grantRevoked'));
    },
  });
}

export function useSetResourceVisibility(resourceType: ResourceType, resourceId: string) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (visibility: ResourceVisibility) => accessApi.setVisibility(resourceType, resourceId, visibility),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['files'] });
      qc.invalidateQueries({ queryKey: qk.grants(resourceType, resourceId) });
      toast.success(i18n.t('access.visibilityUpdated'));
    },
  });
}

export function useTransferResourceOwnership(resourceType: ResourceType, resourceId: string) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (newOwnerId: string) => accessApi.transferOwnership(resourceType, resourceId, newOwnerId),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['files'] });
      toast.success(i18n.t('access.ownershipTransferred'));
    },
  });
}
```

- [ ] **Step 4: Permissions mirror**

```ts
// permissions.ts
Files: {
  View: 'Files.View',
  Upload: 'Files.Upload',
  Delete: 'Files.Delete',
  Manage: 'Files.Manage',
  ShareOwn: 'Files.ShareOwn',   // NEW
},
```

- [ ] **Step 5: Commit**

```bash
git add boilerplateFE/src/
git commit -m "feat(fe): add generic access API + visibility type + Files.ShareOwn mirror"
```

---

### Task 23: Shared components — Badge, SubjectPicker, SubjectStack, OwnershipTransferDialog, ResourceShareDialog

**Files:**
- Create: `boilerplateFE/src/components/common/VisibilityBadge.tsx`
- Create: `boilerplateFE/src/components/common/SubjectPicker.tsx`
- Create: `boilerplateFE/src/components/common/SubjectStack.tsx`
- Create: `boilerplateFE/src/components/common/OwnershipTransferDialog.tsx`
- Create: `boilerplateFE/src/components/common/ResourceShareDialog.tsx`
- Modify: `boilerplateFE/src/components/common/index.ts` — export each

- [ ] **Step 1: `VisibilityBadge`**

```tsx
// VisibilityBadge.tsx
import { useTranslation } from 'react-i18next';
import { Lock, Building2, Globe2 } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import type { ResourceVisibility } from '@/features/access/types';

export function VisibilityBadge({ visibility }: { visibility: ResourceVisibility }) {
  const { t } = useTranslation();
  const map = {
    Private:     { icon: Lock,    variant: 'outline'   as const, key: 'access.visibility.private' },
    TenantWide:  { icon: Building2, variant: 'secondary' as const, key: 'access.visibility.tenantWide' },
    Public:      { icon: Globe2,  variant: 'default'   as const, key: 'access.visibility.public' },
  };
  const { icon: Icon, variant, key } = map[visibility];
  return (
    <Badge variant={variant} className="gap-1">
      <Icon className="h-3 w-3" />
      {t(key)}
    </Badge>
  );
}
```

- [ ] **Step 2: `SubjectPicker`**

Combines two search hooks (`useSearchUsers`, `useRoles`) behind tabbed tabs `Users | Roles`. Emits `{ type: 'User'|'Role', id: string, name: string }` on select. ~100 lines.

- [ ] **Step 3: `SubjectStack`**

Avatar stack for users (reusing `UserAvatar`), chip for roles, +N overflow. Accepts `subjects: { type; id; name }[]`. ~60 lines.

- [ ] **Step 4: `OwnershipTransferDialog`**

```tsx
// OwnershipTransferDialog.tsx
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogFooter, DialogDescription } from '@/components/ui/dialog';
import { Button } from '@/components/ui/button';
import { SubjectPicker } from './SubjectPicker';
import { useTransferResourceOwnership } from '@/features/access/api/access.queries';
import type { ResourceType } from '@/features/access/types';

type Props = {
  open: boolean;
  onOpenChange: (v: boolean) => void;
  resourceType: ResourceType;
  resourceId: string;
  resourceName: string;
  currentOwnerId: string;
};

export function OwnershipTransferDialog({ open, onOpenChange, resourceType, resourceId, resourceName, currentOwnerId }: Props) {
  const { t } = useTranslation();
  const [newOwnerId, setNewOwnerId] = useState<string | null>(null);
  const transfer = useTransferResourceOwnership(resourceType, resourceId);

  const handleConfirm = async () => {
    if (!newOwnerId) return;
    await transfer.mutateAsync(newOwnerId);
    onOpenChange(false);
  };

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>{t('access.transferOwnership.title', { name: resourceName })}</DialogTitle>
          <DialogDescription>{t('access.transferOwnership.description')}</DialogDescription>
        </DialogHeader>
        <SubjectPicker mode="user-only" excludeIds={[currentOwnerId]} onSelect={s => setNewOwnerId(s.id)} />
        <DialogFooter>
          <Button variant="outline" onClick={() => onOpenChange(false)}>{t('common.cancel')}</Button>
          <Button onClick={handleConfirm} disabled={!newOwnerId || transfer.isPending}>
            {t('access.transferOwnership.confirm')}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
```

- [ ] **Step 5: `ResourceShareDialog`**

Core component. ~250 lines. Sections:
1. Header (resource name).
2. Visibility segmented control — `Private | Tenant | Public`. `Public` disabled if `MaxVisibility(resourceType) < Public` **or** if `!hasPermission(Files.Manage)`. Changing to `Public` opens a confirm with "I understand" checkbox.
3. Copy-link button — visible only when visibility === `Public` (reuses `fileApi.getFileUrl` for files).
4. `SubjectPicker` + level select → calls `useGrantResourceAccess`.
5. Grants list via `useResourceGrants`: renders `SubjectStack` rows with level dropdown (`useGrantResourceAccess` with same subjectId upserts) + inline revoke button (`useRevokeResourceGrant` + inline confirm).
6. Footer: Close button.

Key `MaxVisibility` util:
```ts
const MAX_VISIBILITY: Record<ResourceType, ResourceVisibility> = {
  File: 'Public',
  AiAssistant: 'TenantWide',
};
```

- [ ] **Step 6: Commit**

```bash
git add boilerplateFE/src/components/common/
git commit -m "feat(fe): add ResourceShareDialog and supporting shared components"
```

---

### Task 24: `FilesPage` — view tabs, visibility column, shared-with, row actions

**Files:**
- Modify: `boilerplateFE/src/features/files/pages/FilesPage.tsx`
- Modify: `boilerplateFE/src/features/files/api/files.queries.ts` — expose `view` param

- [ ] **Step 1: View tabs**

Add URL-synced tabs (use `useSearchParams`) above the table:
```tsx
<Tabs value={view} onValueChange={setView}>
  <TabsList>
    <TabsTrigger value="all">{t('files.views.all')}</TabsTrigger>
    <TabsTrigger value="mine">{t('files.views.mine')}</TabsTrigger>
    <TabsTrigger value="shared">{t('files.views.shared')}</TabsTrigger>
    <TabsTrigger value="public">{t('files.views.public')}</TabsTrigger>
  </TabsList>
</Tabs>
```

Pass `view` into `useFiles({ view, ...otherFilters })`. Query key becomes `queryKeys.files.list({ view, ... })` (the helper should be stable — TanStack Query serializes the object).

- [ ] **Step 2: Visibility column**

Replace the existing public/private checkbox display with:
```tsx
<TableCell><VisibilityBadge visibility={file.visibility} /></TableCell>
```

- [ ] **Step 3: Shared-with column**

```tsx
<TableCell>
  <SharedWithCell fileId={file.id} />
</TableCell>
```

`SharedWithCell` internally calls `useResourceGrants('File', fileId)` and renders `SubjectStack`. Use `enabled: inView` via `react-intersection-observer` if perf concerns arise — fine to skip for MVP.

- [ ] **Step 4: Row actions menu**

```tsx
<DropdownMenu>
  <DropdownMenuTrigger asChild><Button variant="ghost" size="icon"><MoreVertical /></Button></DropdownMenuTrigger>
  <DropdownMenuContent>
    {canShare && <DropdownMenuItem onClick={() => openShare(file)}>{t('access.share')}</DropdownMenuItem>}
    {isOwner && <DropdownMenuItem onClick={() => openTransfer(file)}>{t('access.transferOwnership.action')}</DropdownMenuItem>}
    <DropdownMenuItem onClick={() => handleDownload(file)}>{t('files.download')}</DropdownMenuItem>
    {canDelete && <DropdownMenuItem className="text-destructive" onClick={() => handleDelete(file)}>{t('common.delete')}</DropdownMenuItem>}
  </DropdownMenuContent>
</DropdownMenu>
```

`canShare = user.id === file.uploadedBy || hasPermission('Files.ShareOwn') || hasPermission('Files.Manage')`.

- [ ] **Step 5: Commit**

```bash
git add boilerplateFE/src/features/files/
git commit -m "feat(fe): FilesPage view tabs, visibility column, share and ownership actions"
```

---

### Task 25: `StorageSummaryPanel`

**Files:**
- Create: `boilerplateFE/src/features/files/components/StorageSummaryPanel.tsx`
- Modify: `FilesPage.tsx` — header button opens the panel (use shadcn `Sheet`)

- [ ] **Step 1: Hook + panel**

```ts
// in features/files/api/files.queries.ts
export function useStorageSummary(allTenants = false) {
  return useQuery({
    queryKey: ['files', 'storageSummary', allTenants],
    queryFn: () => filesApi.getStorageSummary({ allTenants }),
  });
}
```

`StorageSummaryPanel` renders (inside a `<Sheet>`):
- Total bytes formatted with existing `formatBytes`
- `byCategory` → horizontal bar chart (reuse whichever chart library the FE already has; plain flex bars OK if none).
- `byEntityType` → table
- `topUploaders` → table with `UserAvatar` + bytes
- Platform-admin toggle (`TenantId === null`) that sets `allTenants=true`.

- [ ] **Step 2: Commit**

```bash
git add boilerplateFE/src/features/files/
git commit -m "feat(fe): add StorageSummaryPanel"
```

---

### Task 26: Notification type `ResourceShared` consumer wiring

**Files:**
- Modify: `boilerplateFE/src/components/common/NotificationBell.tsx` (or wherever notifications render)
- Modify: `boilerplateFE/src/types/notification.types.ts` — extend `NotificationType`

- [ ] **Step 1: Add type + icon mapping**

```ts
// notification.types.ts
export type NotificationType = 'FileUploaded' | 'DocumentProcessed' | 'ResourceShared' | (string & {});
```

Add icon + color for `ResourceShared` in whatever `NOTIFICATION_ICON_MAP` exists; if none, inline per row.

On click of a `ResourceShared` notification, navigate to `/files?view=shared` (or to the specific resource detail if `data.resourceType === 'AiAssistant'`).

- [ ] **Step 2: Commit**

```bash
git add boilerplateFE/src/
git commit -m "feat(fe): handle ResourceShared notification type in notifications consumer"
```

---

### Task 27: i18n keys (EN + AR)

**Files:**
- Modify: `boilerplateFE/src/i18n/locales/en/translation.json`
- Modify: `boilerplateFE/src/i18n/locales/ar/translation.json`

- [ ] **Step 1: Add keys**

Add top-level `access` block (EN):
```json
"access": {
  "share": "Share",
  "shareTitle": "Share \"{{name}}\"",
  "addPeopleOrRoles": "Add people or roles",
  "hasAccess": "Has access",
  "owner": "Owner",
  "visibility": {
    "private": "Private",
    "tenantWide": "Tenant",
    "public": "Public"
  },
  "visibilityUpdated": "Visibility updated",
  "makePublicConfirmTitle": "Make this public?",
  "makePublicConfirmBody": "Anyone with the link will be able to access this resource.",
  "makePublicConsent": "I understand",
  "grantAdded": "Access granted",
  "grantRevoked": "Access revoked",
  "levels": { "viewer": "Viewer", "editor": "Editor", "manager": "Manager" },
  "transferOwnership": {
    "action": "Transfer ownership",
    "title": "Transfer ownership of \"{{name}}\"",
    "description": "The current owner will be demoted to Manager and retain access.",
    "confirm": "Transfer"
  },
  "copyLink": "Copy link",
  "linkCopied": "Link copied",
  "ownershipTransferred": "Ownership transferred",
  "errors": {
    "resourceNotFound": "Resource not found.",
    "grantNotFound": "Grant not found.",
    "subjectInactive": "This user or role is inactive.",
    "crossTenantBlocked": "Cannot share across tenants.",
    "selfGrantBlocked": "Owners already have access.",
    "insufficientLevel": "You cannot grant a higher level than you have.",
    "visibilityNotAllowed": "This visibility is not allowed for this resource.",
    "onlyOwner": "Only the owner can do this.",
    "ownerNotInTenant": "New owner must be in the same tenant.",
    "ownerInactive": "New owner must be active."
  }
},
"files": {
  "views": { "all": "All", "mine": "My files", "shared": "Shared with me", "public": "Public" },
  "storageSummary": "Storage summary",
  "byCategory": "By category",
  "byEntityType": "By type",
  "topUploaders": "Top uploaders"
}
```

Mirror in AR with appropriate translations. Preserve existing nested keys.

- [ ] **Step 2: Build check + commit**

```bash
cd boilerplateFE && npm run build && cd -
git add boilerplateFE/src/i18n/
git commit -m "feat(fe): i18n keys for ACL + file views + storage summary"
```

---

### Task 28: Playwright scenarios — rename-app verification (§5.9)

**Files:**
- Create: `boilerplateBE/tests/playwright/acl-files.spec.ts` (or follow the repo's existing Playwright layout — check `scripts/rename.ps1` output location)

Per project convention (see `CLAUDE.md` post-feature-testing section), this happens in a renamed test app (`_testAcl`). The spec produces a Playwright file that exercises all 7 UX flows from §5.9. One `test(...)` per flow.

- [ ] **Step 1: Implement + run in rename-app**

Run via project-standard command (see `CLAUDE.md` post-feature-testing workflow). Do not ship to main repo until passing. Commit tests under the path the repo already uses for FE E2E (grep `playwright` across `boilerplateFE/` first).

```bash
git add boilerplateFE/  # or appropriate path
git commit -m "test(fe): Playwright coverage for ACL file UX flows"
```

---

### Task 29: Final verification — full build + test suite

- [ ] **Step 1: Full build**

Run: `dotnet build boilerplateBE/Starter.sln` → 0 errors.

- [ ] **Step 2: Full tests**

Run: `dotnet test boilerplateBE/Starter.sln`
Expected: all green. Existing RAG tests (MMR, reranking, circuit-breaker) unaffected (only new `aclFilter` parameter, default `null`).

- [ ] **Step 3: FE build**

```bash
cd boilerplateFE && npm run build
```
Expected: 0 errors.

- [ ] **Step 4: post-feature-testing workflow (see CLAUDE.md §Post-Feature Testing Workflow)**

Create `_testAcl` rename-app → fresh DB → run Playwright scenarios (Task 28) + regression → report URLs. No commit needed here; the verification step just confirms feature correctness.

---

## Self-review notes (completed during plan writing)

**Spec coverage (§3 D1-D11 + §5.1-§5.9):**
- D1 visibility model — Tasks 1, 7
- D2 access levels — Task 1
- D3 owner distinct — Task 6 (probe.GetOwnerAsync), Task 7 (UploadedBy), Task 11 (CreatedByUserId)
- D4 two-layer defence — Task 11 (chat gate) + Task 14 (retrieval filter)
- D5 AccessMode — Tasks 11, 14
- D6 Files as hub — Tasks 8, 10
- D7 core placement — Tasks 1-6
- D8 payload enrichment — Task 12
- D9 explicit grants only — Task 5
- D10 Redis version counter — Task 5
- D11 cascades — Task 15
- §5.6 ownership transfer — Task 6 (FileOwnershipHandler) + Task 11 (AiAssistant)
- §5.8 audit — Task 16
- §5.9 frontend — Tasks 22-28

**Placeholder scan:** Every "TODO"-adjacent phrase has either real code or an explicit "grep and fix each" instruction pointing at concrete tokens (e.g. `FileRef`, `IsPublic`). No "add validation" without a code example.

**Type consistency:** `IResourceAccessService` methods referenced in Tasks 5/6/8/11/14 all match the Task 4 contract. `IResourceOwnershipHandler` / `IResourceOwnershipProbe` naming is consistent across Tasks 6, 7, 11. `AclPayloadFilter` defined Task 12 + consumed Tasks 13/14. `ResourceTypes.File` / `ResourceTypes.AiAssistant` used consistently.

---

**Plan complete.** Save path: `docs/superpowers/plans/2026-04-22-ai-module-plan-4b-8-per-document-acls.md`.
