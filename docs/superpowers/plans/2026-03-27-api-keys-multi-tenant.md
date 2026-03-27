# API Keys Multi-Tenant Redesign — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Redesign API keys to support tenant-scoped keys (managed by tenants) and platform keys (managed by platform admins), with emergency revoke capability.

**Architecture:** Extend existing CQRS handlers with dual-scope authorization logic. Add composite authorization policies to the controller layer. Enhance the auth handler to resolve tenant context from `X-Tenant-Id` header for platform keys. Split frontend into tabbed view for platform admins.

**Tech Stack:** .NET 10, EF Core, MediatR, React 19, TanStack Query, shadcn/ui, i18next

---

## File Map

**Backend changes (modify):**
- `Starter.Shared/Constants/Permissions.cs` — Add 5 new platform/emergency permissions
- `Starter.Shared/Constants/Roles.cs` — Assign new permissions to SuperAdmin and Admin roles
- `Starter.Domain/ApiKeys/Entities/ApiKey.cs` — Add `IsPlatformKey` computed property
- `Starter.Domain/ApiKeys/Errors/ApiKeyErrors.cs` — Add new error constants
- `Starter.Domain/Common/Enums/AuditAction.cs` — Add `EmergencyRevoked` action
- `Starter.Domain/Common/Enums/AuditEntityType.cs` — Add `ApiKey` entity type
- `Starter.Infrastructure.Identity/Authorization/PermissionRequirement.cs` — Support multiple permissions (any-of)
- `Starter.Infrastructure.Identity/Authorization/PermissionAuthorizationPolicyProvider.cs` — Handle pipe-delimited composite policies
- `Starter.Infrastructure.Identity/Authorization/PermissionAuthorizationHandler.cs` — Check any-of permissions
- `Starter.Infrastructure.Identity/Authentication/ApiKeyAuthenticationHandler.cs` — X-Tenant-Id handling, tenant validation, `is_platform_key` claim
- `Starter.Api/Controllers/ApiKeysController.cs` — Composite policies, `keyType` param, emergency-revoke endpoint
- `Starter.Application/Features/ApiKeys/DTOs/ApiKeyDto.cs` — Add `IsPlatformKey`, `TenantId`, `TenantName`
- `Starter.Application/Features/ApiKeys/DTOs/ApiKeyMapper.cs` — Map new fields
- `Starter.Application/Features/ApiKeys/Commands/CreateApiKey/CreateApiKeyCommand.cs` — Add `IsPlatformKey`
- `Starter.Application/Features/ApiKeys/Commands/CreateApiKey/CreateApiKeyCommandValidator.cs` — Validate new field
- `Starter.Application/Features/ApiKeys/Commands/CreateApiKey/CreateApiKeyCommandHandler.cs` — Dual-scope creation logic
- `Starter.Application/Features/ApiKeys/Commands/RevokeApiKey/RevokeApiKeyCommandHandler.cs` — Ownership checks
- `Starter.Application/Features/ApiKeys/Commands/UpdateApiKey/UpdateApiKeyCommandHandler.cs` — Ownership checks
- `Starter.Application/Features/ApiKeys/Queries/GetApiKeys/GetApiKeysQuery.cs` — Add `KeyType`, `TenantId` params
- `Starter.Application/Features/ApiKeys/Queries/GetApiKeys/GetApiKeysQueryHandler.cs` — KeyType filtering + tenant join
- `Starter.Application/Features/ApiKeys/Queries/GetApiKeyById/GetApiKeyByIdQueryHandler.cs` — Platform admin: IgnoreQueryFilters + tenant name

**Backend changes (new):**
- `Starter.Application/Features/ApiKeys/Commands/EmergencyRevokeApiKey/EmergencyRevokeApiKeyCommand.cs`
- `Starter.Application/Features/ApiKeys/Commands/EmergencyRevokeApiKey/EmergencyRevokeApiKeyCommandHandler.cs`

**Frontend changes (modify):**
- `constants/permissions.ts` — Add 5 platform/emergency permissions
- `config/api.config.ts` — Add emergency-revoke endpoint
- `lib/query/keys.ts` — keyType in cache key
- `features/api-keys/api/api-keys.api.ts` — keyType, isPlatformKey, emergencyRevoke
- `features/api-keys/api/api-keys.queries.ts` — New hooks
- `features/api-keys/pages/ApiKeysPage.tsx` — Dual view with tabs
- `features/api-keys/components/CreateApiKeyDialog.tsx` — isPlatformKey flag
- `i18n/locales/{en,ar,ku}/translation.json` — New keys

**Frontend changes (new):**
- `features/api-keys/components/PlatformKeysTab.tsx`
- `features/api-keys/components/TenantKeysTab.tsx`
- `features/api-keys/components/EmergencyRevokeDialog.tsx`

---

## Task 1: Permissions, Roles & Domain Enums

**Files:**
- Modify: `boilerplateBE/src/Starter.Shared/Constants/Permissions.cs`
- Modify: `boilerplateBE/src/Starter.Shared/Constants/Roles.cs`
- Modify: `boilerplateBE/src/Starter.Domain/Common/Enums/AuditAction.cs`
- Modify: `boilerplateBE/src/Starter.Domain/Common/Enums/AuditEntityType.cs`
- Modify: `boilerplateBE/src/Starter.Domain/ApiKeys/Entities/ApiKey.cs`
- Modify: `boilerplateBE/src/Starter.Domain/ApiKeys/Errors/ApiKeyErrors.cs`

- [ ] **Step 1: Add platform + emergency permissions to `Permissions.cs`**

In the `ApiKeys` inner class (currently lines 84-89), add 5 new constants:

```csharp
public static class ApiKeys
{
    public const string View = "ApiKeys.View";
    public const string Create = "ApiKeys.Create";
    public const string Update = "ApiKeys.Update";
    public const string Delete = "ApiKeys.Delete";
    public const string ViewPlatform = "ApiKeys.ViewPlatform";
    public const string CreatePlatform = "ApiKeys.CreatePlatform";
    public const string UpdatePlatform = "ApiKeys.UpdatePlatform";
    public const string DeletePlatform = "ApiKeys.DeletePlatform";
    public const string EmergencyRevoke = "ApiKeys.EmergencyRevoke";
}
```

In the `GetAllWithMetadata()` method (currently around line 158-162), add the new tuples:

```csharp
// ─── API Keys ───
yield return (ApiKeys.View, "View API keys", "ApiKeys");
yield return (ApiKeys.Create, "Create API keys", "ApiKeys");
yield return (ApiKeys.Update, "Update API keys", "ApiKeys");
yield return (ApiKeys.Delete, "Delete (revoke) API keys", "ApiKeys");
yield return (ApiKeys.ViewPlatform, "View platform API keys and all tenant keys (read-only)", "ApiKeys");
yield return (ApiKeys.CreatePlatform, "Create platform-scoped API keys", "ApiKeys");
yield return (ApiKeys.UpdatePlatform, "Update platform API keys", "ApiKeys");
yield return (ApiKeys.DeletePlatform, "Revoke platform API keys", "ApiKeys");
yield return (ApiKeys.EmergencyRevoke, "Emergency revoke any tenant API key", "ApiKeys");
```

Update the permission matrix table in the XML doc comment to include the new columns.

- [ ] **Step 2: Assign new permissions to roles in `Roles.cs`**

In `GetRolePermissions()`, the `SuperAdmin` section (which uses `Permissions.GetAll()`) already gets all permissions automatically since `GetAll()` yields everything from `GetAllWithMetadata()`. No change needed for SuperAdmin.

For the `Admin` role, add only tenant-level API key permissions (currently around lines 44-48):

```csharp
// API Keys (tenant-level only)
Permissions.ApiKeys.View,
Permissions.ApiKeys.Create,
Permissions.ApiKeys.Update,
Permissions.ApiKeys.Delete,
```

This is already present — verify it does NOT include `ViewPlatform`, `CreatePlatform`, `UpdatePlatform`, `DeletePlatform`, or `EmergencyRevoke`.

- [ ] **Step 3: Add `EmergencyRevoked` to `AuditAction` enum and `ApiKey` to `AuditEntityType`**

In `AuditAction.cs`:

```csharp
public enum AuditAction
{
    Created = 1,
    Updated = 2,
    Deleted = 3,
    EmergencyRevoked = 4
}
```

In `AuditEntityType.cs`:

```csharp
public enum AuditEntityType
{
    User = 1,
    Role = 2,
    Permission = 3,
    Tenant = 4,
    File = 5,
    ApiKey = 6
}
```

- [ ] **Step 4: Add `IsPlatformKey` to `ApiKey.cs` entity**

Add after the `IsValid` property (line 18):

```csharp
public bool IsPlatformKey => TenantId == null;
```

- [ ] **Step 5: Add new error constants to `ApiKeyErrors.cs`**

```csharp
public static class ApiKeyErrors
{
    public static readonly Error NotFound = Error.NotFound(
        "ApiKey.NotFound", "API key not found.");
    public static readonly Error AlreadyRevoked = Error.Conflict(
        "ApiKey.AlreadyRevoked", "API key is already revoked.");
    public static readonly Error TenantCannotCreatePlatformKey = Error.Validation(
        "ApiKey.TenantCannotCreatePlatformKey", "Tenant users cannot create platform keys.");
    public static readonly Error PlatformAdminMustBeExplicit = Error.Validation(
        "ApiKey.PlatformAdminMustBeExplicit", "Platform admins must explicitly create platform keys. Set isPlatformKey to true.");
    public static readonly Error CannotModifyTenantKey = Error.Forbidden(
        "ApiKey.CannotModifyTenantKey", "Cannot modify tenant API keys. Tenant keys are managed by their owning tenant.");
    public static readonly Error UseTenantEmergencyRevoke = Error.Forbidden(
        "ApiKey.UseTenantEmergencyRevoke", "Use the emergency-revoke endpoint to revoke tenant keys.");
}
```

Note: The `Error` class may not have a `Forbidden` factory. Check the `Error` class — if missing, use `Error.Failure` with a custom code, and have the controller map it to 403 based on the error code prefix.

- [ ] **Step 6: Build backend to verify compilation**

Run: `cd boilerplateBE && dotnet build --verbosity quiet`
Expected: Build succeeded with 0 errors.

- [ ] **Step 7: Commit**

```bash
git add boilerplateBE/src/Starter.Shared/Constants/Permissions.cs \
       boilerplateBE/src/Starter.Shared/Constants/Roles.cs \
       boilerplateBE/src/Starter.Domain/Common/Enums/AuditAction.cs \
       boilerplateBE/src/Starter.Domain/Common/Enums/AuditEntityType.cs \
       boilerplateBE/src/Starter.Domain/ApiKeys/Entities/ApiKey.cs \
       boilerplateBE/src/Starter.Domain/ApiKeys/Errors/ApiKeyErrors.cs
git commit -m "feat(api-keys): add platform permissions, domain enums, error constants"
```

---

## Task 2: Composite Authorization Policy (Any-Of)

**Files:**
- Modify: `boilerplateBE/src/Starter.Infrastructure.Identity/Authorization/PermissionRequirement.cs`
- Modify: `boilerplateBE/src/Starter.Infrastructure.Identity/Authorization/PermissionAuthorizationPolicyProvider.cs`
- Modify: `boilerplateBE/src/Starter.Infrastructure.Identity/Authorization/PermissionAuthorizationHandler.cs`

The current system creates one `PermissionRequirement` per policy, checking for an exact match. We need to support `[Authorize(Policy = "ApiKeys.View|ApiKeys.ViewPlatform")]` — pipe-delimited permissions where any match succeeds.

- [ ] **Step 1: Modify `PermissionRequirement.cs` to hold multiple permissions**

Replace the full file:

```csharp
using Microsoft.AspNetCore.Authorization;

namespace Starter.Infrastructure.Identity.Authorization;

/// <summary>
/// Requirement for permission-based authorization.
/// Supports a single permission OR multiple pipe-delimited permissions (any must match).
/// </summary>
public class PermissionRequirement : IAuthorizationRequirement
{
    public IReadOnlyList<string> Permissions { get; }

    public PermissionRequirement(string permission)
    {
        // Support pipe-delimited: "ApiKeys.View|ApiKeys.ViewPlatform"
        Permissions = permission.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }
}
```

- [ ] **Step 2: Update `PermissionAuthorizationPolicyProvider.cs`**

The provider already checks for a dot (`policyName.Contains('.')`) to identify permission policies. Pipe-delimited strings like `"ApiKeys.View|ApiKeys.ViewPlatform"` still contain dots, so no change needed to the detection logic. The `PermissionRequirement` constructor handles parsing.

Verify this — no code change should be needed. Read the file to confirm.

- [ ] **Step 3: Update `PermissionAuthorizationHandler.cs` to check any-of**

Replace the handler logic:

```csharp
using Microsoft.AspNetCore.Authorization;

namespace Starter.Infrastructure.Identity.Authorization;

/// <summary>
/// Authorization handler for permission-based authorization.
/// Succeeds if the user has ANY of the required permissions.
/// </summary>
public class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        if (context.User?.Identity?.IsAuthenticated != true)
        {
            return Task.CompletedTask;
        }

        var userPermissions = context.User.Claims
            .Where(c => c.Type == "permission")
            .Select(c => c.Value)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Succeed if user has ANY of the required permissions
        if (requirement.Permissions.Any(p => userPermissions.Contains(p)))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
```

- [ ] **Step 4: Build and verify**

Run: `cd boilerplateBE && dotnet build --verbosity quiet`
Expected: Build succeeded.

- [ ] **Step 5: Commit**

```bash
git add boilerplateBE/src/Starter.Infrastructure.Identity/Authorization/
git commit -m "feat(auth): support composite any-of permission policies via pipe delimiter"
```

---

## Task 3: Auth Handler — X-Tenant-Id & Platform Key Claims

**Files:**
- Modify: `boilerplateBE/src/Starter.Infrastructure.Identity/Authentication/ApiKeyAuthenticationHandler.cs`

- [ ] **Step 1: Read the current handler**

Read `boilerplateBE/src/Starter.Infrastructure.Identity/Authentication/ApiKeyAuthenticationHandler.cs` to confirm current structure.

- [ ] **Step 2: Update the handler**

Replace the full `HandleAuthenticateAsync` method. Key changes:
1. After key validation, add `is_platform_key` claim.
2. For platform keys (`TenantId == null`), check `X-Tenant-Id` header.
3. Validate the tenant exists and is Active via a query on the `Tenants` DbSet.

```csharp
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Starter.Application.Common.Interfaces;
using Starter.Domain.Tenants.Enums;

namespace Starter.Infrastructure.Identity.Authentication;

public sealed class ApiKeyAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IApplicationDbContext dbContext)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "ApiKey";
    public const string HeaderName = "X-Api-Key";
    public const string TenantHeaderName = "X-Tenant-Id";

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(HeaderName, out var apiKeyValue))
            return AuthenticateResult.NoResult();

        var providedKey = apiKeyValue.ToString();
        if (string.IsNullOrWhiteSpace(providedKey))
            return AuthenticateResult.NoResult();

        if (providedKey.Length < 16)
            return AuthenticateResult.Fail("Invalid API key format.");

        var prefix = providedKey[..16];

        var apiKey = await dbContext.ApiKeys
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(k => k.KeyPrefix == prefix);

        if (apiKey is null)
            return AuthenticateResult.Fail("Invalid API key.");

        if (!apiKey.IsValid)
            return AuthenticateResult.Fail("API key is revoked or expired.");

        if (!BCrypt.Net.BCrypt.Verify(providedKey, apiKey.KeyHash))
            return AuthenticateResult.Fail("Invalid API key.");

        // Update last used (fire-and-forget)
        apiKey.UpdateLastUsed();
        try { await dbContext.SaveChangesAsync(); }
        catch { /* non-critical */ }

        var claims = new List<Claim>
        {
            new("api_key_id", apiKey.Id.ToString()),
            new("auth_method", "api_key"),
            new("is_platform_key", apiKey.IsPlatformKey.ToString().ToLowerInvariant())
        };

        if (apiKey.TenantId.HasValue)
        {
            // Tenant key: locked to its tenant, ignore X-Tenant-Id header
            claims.Add(new Claim("tenant_id", apiKey.TenantId.Value.ToString()));
        }
        else
        {
            // Platform key: check for X-Tenant-Id header
            if (Request.Headers.TryGetValue(TenantHeaderName, out var tenantIdHeader))
            {
                var tenantIdStr = tenantIdHeader.ToString();
                if (Guid.TryParse(tenantIdStr, out var requestedTenantId))
                {
                    // Validate tenant exists and is Active
                    var tenant = await dbContext.Tenants
                        .IgnoreQueryFilters()
                        .Where(t => t.Id == requestedTenantId)
                        .Select(t => new { t.Id, t.Status })
                        .FirstOrDefaultAsync();

                    if (tenant is null)
                        return AuthenticateResult.Fail("Invalid tenant ID.");

                    if (tenant.Status != TenantStatus.Active)
                        return AuthenticateResult.Fail("Tenant is not active.");

                    claims.Add(new Claim("tenant_id", requestedTenantId.ToString()));
                }
                else
                {
                    return AuthenticateResult.Fail("Invalid X-Tenant-Id header format.");
                }
            }
            // No X-Tenant-Id header: platform-wide access (no tenant_id claim)
        }

        // Add scopes as permission claims
        foreach (var scope in apiKey.Scopes)
            claims.Add(new Claim("permission", scope));

        var identity = new ClaimsIdentity(claims, SchemeName);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, SchemeName);

        return AuthenticateResult.Success(ticket);
    }
}
```

- [ ] **Step 3: Verify `Tenants` DbSet is on `IApplicationDbContext`**

Check `boilerplateBE/src/Starter.Application/Common/Interfaces/IApplicationDbContext.cs` for `DbSet<Tenant> Tenants`. It should already be there.

- [ ] **Step 4: Build and verify**

Run: `cd boilerplateBE && dotnet build --verbosity quiet`
Expected: Build succeeded.

- [ ] **Step 5: Commit**

```bash
git add boilerplateBE/src/Starter.Infrastructure.Identity/Authentication/ApiKeyAuthenticationHandler.cs
git commit -m "feat(auth): platform key X-Tenant-Id resolution with tenant status validation"
```

---

## Task 4: DTOs, Commands & Query Models

**Files:**
- Modify: `boilerplateBE/src/Starter.Application/Features/ApiKeys/DTOs/ApiKeyDto.cs`
- Modify: `boilerplateBE/src/Starter.Application/Features/ApiKeys/DTOs/ApiKeyMapper.cs`
- Modify: `boilerplateBE/src/Starter.Application/Features/ApiKeys/Commands/CreateApiKey/CreateApiKeyCommand.cs`
- Modify: `boilerplateBE/src/Starter.Application/Features/ApiKeys/Commands/CreateApiKey/CreateApiKeyCommandValidator.cs`
- Modify: `boilerplateBE/src/Starter.Application/Features/ApiKeys/Queries/GetApiKeys/GetApiKeysQuery.cs`
- Create: `boilerplateBE/src/Starter.Application/Features/ApiKeys/Commands/EmergencyRevokeApiKey/EmergencyRevokeApiKeyCommand.cs`

- [ ] **Step 1: Update `ApiKeyDto.cs`**

Replace the record:

```csharp
namespace Starter.Application.Features.ApiKeys.DTOs;

public sealed record ApiKeyDto(
    Guid Id,
    string Name,
    string KeyPrefix,
    List<string> Scopes,
    DateTime? ExpiresAt,
    DateTime? LastUsedAt,
    bool IsRevoked,
    bool IsExpired,
    bool IsPlatformKey,
    Guid? TenantId,
    string? TenantName,
    DateTime CreatedAt,
    Guid? CreatedBy);
```

- [ ] **Step 2: Update `ApiKeyMapper.cs`**

The mapper extension method needs to map the new fields. Replace:

```csharp
using Starter.Domain.ApiKeys.Entities;

namespace Starter.Application.Features.ApiKeys.DTOs;

public static class ApiKeyMapper
{
    public static ApiKeyDto ToDto(this ApiKey entity, string? tenantName = null)
    {
        return new ApiKeyDto(
            entity.Id,
            entity.Name,
            entity.KeyPrefix,
            entity.Scopes,
            entity.ExpiresAt,
            entity.LastUsedAt,
            entity.IsRevoked,
            entity.IsExpired,
            entity.IsPlatformKey,
            entity.TenantId,
            tenantName,
            entity.CreatedAt,
            entity.CreatedBy);
    }
}
```

- [ ] **Step 3: Update `CreateApiKeyCommand.cs`**

Add `IsPlatformKey` field:

```csharp
using MediatR;
using Starter.Application.Features.ApiKeys.DTOs;
using Starter.Shared.Results;

namespace Starter.Application.Features.ApiKeys.Commands.CreateApiKey;

public sealed record CreateApiKeyCommand(
    string Name,
    List<string> Scopes,
    DateTime? ExpiresAt,
    bool IsPlatformKey = false) : IRequest<Result<CreateApiKeyResponse>>;
```

- [ ] **Step 4: Update `CreateApiKeyCommandValidator.cs`**

No changes needed — the validator validates `Name` and `Scopes`. `IsPlatformKey` is a boolean with a default, no validation needed.

Read the file to confirm existing validation rules are sufficient.

- [ ] **Step 5: Update `GetApiKeysQuery.cs`**

Add `KeyType` and `TenantId` filter parameters:

```csharp
using MediatR;
using Starter.Application.Common.Models;
using Starter.Application.Features.ApiKeys.DTOs;
using Starter.Shared.Results;

namespace Starter.Application.Features.ApiKeys.Queries.GetApiKeys;

public sealed record GetApiKeysQuery(
    int PageNumber = 1,
    int PageSize = 10,
    string? KeyType = null,
    Guid? TenantId = null) : IRequest<Result<PaginatedList<ApiKeyDto>>>;
```

- [ ] **Step 6: Create `EmergencyRevokeApiKeyCommand.cs`**

Create directory and file at `boilerplateBE/src/Starter.Application/Features/ApiKeys/Commands/EmergencyRevokeApiKey/EmergencyRevokeApiKeyCommand.cs`:

```csharp
using MediatR;
using Starter.Shared.Results;

namespace Starter.Application.Features.ApiKeys.Commands.EmergencyRevokeApiKey;

public sealed record EmergencyRevokeApiKeyCommand(
    Guid Id,
    string? Reason = null) : IRequest<Result>;
```

- [ ] **Step 7: Build and verify**

Run: `cd boilerplateBE && dotnet build --verbosity quiet`
Expected: Build succeeded.

- [ ] **Step 8: Commit**

```bash
git add boilerplateBE/src/Starter.Application/Features/ApiKeys/
git commit -m "feat(api-keys): update DTOs, commands, query models for dual-scope"
```

---

## Task 5: Command Handlers — Create, Revoke, Update, EmergencyRevoke

**Files:**
- Modify: `boilerplateBE/src/Starter.Application/Features/ApiKeys/Commands/CreateApiKey/CreateApiKeyCommandHandler.cs`
- Modify: `boilerplateBE/src/Starter.Application/Features/ApiKeys/Commands/RevokeApiKey/RevokeApiKeyCommandHandler.cs`
- Modify: `boilerplateBE/src/Starter.Application/Features/ApiKeys/Commands/UpdateApiKey/UpdateApiKeyCommandHandler.cs`
- Create: `boilerplateBE/src/Starter.Application/Features/ApiKeys/Commands/EmergencyRevokeApiKey/EmergencyRevokeApiKeyCommandHandler.cs`

- [ ] **Step 1: Update `CreateApiKeyCommandHandler.cs`**

Add dual-scope logic. Key changes:
- Tenant user: Always sets `TenantId` to their tenant. Rejects `IsPlatformKey=true`.
- Platform admin: Requires `IsPlatformKey=true`. Sets `TenantId=null`.

```csharp
using System.Security.Cryptography;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Starter.Application.Common.Interfaces;
using Starter.Application.Features.ApiKeys.DTOs;
using Starter.Domain.ApiKeys.Entities;
using Starter.Domain.ApiKeys.Errors;
using Starter.Shared.Results;

namespace Starter.Application.Features.ApiKeys.Commands.CreateApiKey;

public sealed class CreateApiKeyCommandHandler(
    IApplicationDbContext dbContext,
    ICurrentUserService currentUserService,
    IPasswordService passwordService)
    : IRequestHandler<CreateApiKeyCommand, Result<CreateApiKeyResponse>>
{
    public async Task<Result<CreateApiKeyResponse>> Handle(
        CreateApiKeyCommand request,
        CancellationToken cancellationToken)
    {
        // Determine tenant scope
        Guid? tenantId;
        if (currentUserService.TenantId.HasValue)
        {
            // Tenant user — always scoped to their tenant
            if (request.IsPlatformKey)
                return Result.Failure<CreateApiKeyResponse>(ApiKeyErrors.TenantCannotCreatePlatformKey);

            tenantId = currentUserService.TenantId.Value;
        }
        else
        {
            // Platform admin — must explicitly create platform key
            if (!request.IsPlatformKey)
                return Result.Failure<CreateApiKeyResponse>(ApiKeyErrors.PlatformAdminMustBeExplicit);

            tenantId = null;
        }

        // Generate key
        var randomBytes = RandomNumberGenerator.GetBytes(32);
        var randomPart = Convert.ToBase64String(randomBytes)
            .Replace("+", "").Replace("/", "").Replace("=", "");
        if (randomPart.Length > 32) randomPart = randomPart[..32];

        var fullKey = $"sk_live_{randomPart}";
        var keyPrefix = $"sk_live_{randomPart[..8]}";

        var prefixExists = await dbContext.ApiKeys
            .IgnoreQueryFilters()
            .AnyAsync(k => k.KeyPrefix == keyPrefix, cancellationToken);

        if (prefixExists)
            return Result.Failure<CreateApiKeyResponse>(
                Error.Conflict("ApiKey.PrefixCollision", "Key generation collision. Please try again."));

        var keyHash = await passwordService.HashPasswordAsync(fullKey);

        var apiKey = ApiKey.Create(
            tenantId,
            request.Name,
            keyPrefix,
            keyHash,
            request.Scopes,
            request.ExpiresAt,
            currentUserService.UserId);

        dbContext.ApiKeys.Add(apiKey);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success(new CreateApiKeyResponse(
            apiKey.Id, apiKey.Name, apiKey.KeyPrefix, fullKey,
            apiKey.Scopes, apiKey.ExpiresAt, apiKey.CreatedAt));
    }
}
```

- [ ] **Step 2: Update `RevokeApiKeyCommandHandler.cs`**

Read the current handler first, then replace with ownership-aware logic:

```csharp
using MediatR;
using Microsoft.EntityFrameworkCore;
using Starter.Application.Common.Interfaces;
using Starter.Domain.ApiKeys.Errors;
using Starter.Shared.Constants;
using Starter.Shared.Results;

namespace Starter.Application.Features.ApiKeys.Commands.RevokeApiKey;

public sealed class RevokeApiKeyCommandHandler(
    IApplicationDbContext dbContext,
    ICurrentUserService currentUserService)
    : IRequestHandler<RevokeApiKeyCommand, Result>
{
    public async Task<Result> Handle(RevokeApiKeyCommand request, CancellationToken cancellationToken)
    {
        var apiKey = await dbContext.ApiKeys
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(k => k.Id == request.Id, cancellationToken);

        if (apiKey is null)
            return Result.Failure(ApiKeyErrors.NotFound);

        if (apiKey.IsRevoked)
            return Result.Failure(ApiKeyErrors.AlreadyRevoked);

        if (apiKey.IsPlatformKey)
        {
            // Platform key: only platform admin with DeletePlatform can revoke
            if (currentUserService.TenantId.HasValue)
                return Result.Failure(ApiKeyErrors.NotFound); // Hide from tenant users

            if (!currentUserService.HasPermission(Permissions.ApiKeys.DeletePlatform))
                return Result.Failure(ApiKeyErrors.NotFound);
        }
        else
        {
            // Tenant key
            if (currentUserService.TenantId.HasValue)
            {
                // Tenant user: can only revoke own tenant's keys
                if (apiKey.TenantId != currentUserService.TenantId)
                    return Result.Failure(ApiKeyErrors.NotFound);
            }
            else
            {
                // Platform admin: cannot revoke tenant keys via normal endpoint
                return Result.Failure(ApiKeyErrors.UseTenantEmergencyRevoke);
            }
        }

        apiKey.Revoke();
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
```

- [ ] **Step 3: Update `UpdateApiKeyCommandHandler.cs`**

Read the current handler, then replace with ownership checks:

```csharp
using MediatR;
using Microsoft.EntityFrameworkCore;
using Starter.Application.Common.Interfaces;
using Starter.Application.Features.ApiKeys.DTOs;
using Starter.Domain.ApiKeys.Errors;
using Starter.Shared.Constants;
using Starter.Shared.Results;

namespace Starter.Application.Features.ApiKeys.Commands.UpdateApiKey;

public sealed class UpdateApiKeyCommandHandler(
    IApplicationDbContext dbContext,
    ICurrentUserService currentUserService)
    : IRequestHandler<UpdateApiKeyCommand, Result<ApiKeyDto>>
{
    public async Task<Result<ApiKeyDto>> Handle(UpdateApiKeyCommand request, CancellationToken cancellationToken)
    {
        var apiKey = await dbContext.ApiKeys
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(k => k.Id == request.Id, cancellationToken);

        if (apiKey is null)
            return Result.Failure<ApiKeyDto>(ApiKeyErrors.NotFound);

        if (apiKey.IsPlatformKey)
        {
            // Platform key: only platform admin with UpdatePlatform
            if (currentUserService.TenantId.HasValue)
                return Result.Failure<ApiKeyDto>(ApiKeyErrors.NotFound);

            if (!currentUserService.HasPermission(Permissions.ApiKeys.UpdatePlatform))
                return Result.Failure<ApiKeyDto>(ApiKeyErrors.NotFound);
        }
        else
        {
            // Tenant key
            if (currentUserService.TenantId.HasValue)
            {
                if (apiKey.TenantId != currentUserService.TenantId)
                    return Result.Failure<ApiKeyDto>(ApiKeyErrors.NotFound);
            }
            else
            {
                // Platform admin cannot modify tenant keys
                return Result.Failure<ApiKeyDto>(ApiKeyErrors.CannotModifyTenantKey);
            }
        }

        apiKey.UpdateDetails(request.Name, request.Scopes);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success(apiKey.ToDto());
    }
}
```

- [ ] **Step 4: Create `EmergencyRevokeApiKeyCommandHandler.cs`**

Create at `boilerplateBE/src/Starter.Application/Features/ApiKeys/Commands/EmergencyRevokeApiKey/EmergencyRevokeApiKeyCommandHandler.cs`:

```csharp
using MediatR;
using Microsoft.EntityFrameworkCore;
using Starter.Application.Common.Interfaces;
using Starter.Domain.ApiKeys.Errors;
using Starter.Domain.Common;
using Starter.Domain.Common.Enums;
using Starter.Shared.Results;

namespace Starter.Application.Features.ApiKeys.Commands.EmergencyRevokeApiKey;

public sealed class EmergencyRevokeApiKeyCommandHandler(
    IApplicationDbContext dbContext,
    ICurrentUserService currentUserService)
    : IRequestHandler<EmergencyRevokeApiKeyCommand, Result>
{
    public async Task<Result> Handle(EmergencyRevokeApiKeyCommand request, CancellationToken cancellationToken)
    {
        var apiKey = await dbContext.ApiKeys
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(k => k.Id == request.Id, cancellationToken);

        if (apiKey is null)
            return Result.Failure(ApiKeyErrors.NotFound);

        if (apiKey.IsRevoked)
            return Result.Failure(ApiKeyErrors.AlreadyRevoked);

        apiKey.Revoke();

        // Get tenant name for audit
        string? tenantName = null;
        if (apiKey.TenantId.HasValue)
        {
            tenantName = await dbContext.Tenants
                .IgnoreQueryFilters()
                .Where(t => t.Id == apiKey.TenantId.Value)
                .Select(t => t.Name)
                .FirstOrDefaultAsync(cancellationToken);
        }

        // Create explicit audit log for emergency action
        var auditLog = new AuditLog
        {
            Id = Guid.NewGuid(),
            EntityType = AuditEntityType.ApiKey,
            EntityId = apiKey.Id,
            Action = AuditAction.EmergencyRevoked,
            Changes = System.Text.Json.JsonSerializer.Serialize(new
            {
                keyName = apiKey.Name,
                tenantName,
                tenantId = apiKey.TenantId,
                reason = request.Reason,
                isPlatformKey = apiKey.IsPlatformKey
            }),
            PerformedBy = currentUserService.UserId,
            PerformedByName = currentUserService.Email,
            PerformedAt = DateTime.UtcNow,
            TenantId = apiKey.TenantId
        };

        dbContext.AuditLogs.Add(auditLog);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
```

Note: Check that `dbContext.AuditLogs` exists on `IApplicationDbContext`. If not, add `DbSet<AuditLog> AuditLogs { get; }` to the interface and the `ApplicationDbContext` class.

- [ ] **Step 5: Build and verify**

Run: `cd boilerplateBE && dotnet build --verbosity quiet`
Expected: Build succeeded.

- [ ] **Step 6: Commit**

```bash
git add boilerplateBE/src/Starter.Application/Features/ApiKeys/Commands/
git commit -m "feat(api-keys): dual-scope create, ownership-aware revoke/update, emergency revoke"
```

---

## Task 6: Query Handlers — GetApiKeys & GetApiKeyById

**Files:**
- Modify: `boilerplateBE/src/Starter.Application/Features/ApiKeys/Queries/GetApiKeys/GetApiKeysQueryHandler.cs`
- Modify: `boilerplateBE/src/Starter.Application/Features/ApiKeys/Queries/GetApiKeyById/GetApiKeyByIdQueryHandler.cs`

- [ ] **Step 1: Update `GetApiKeysQueryHandler.cs`**

Replace with keyType filtering and tenant join:

```csharp
using MediatR;
using Microsoft.EntityFrameworkCore;
using Starter.Application.Common.Interfaces;
using Starter.Application.Common.Models;
using Starter.Application.Features.ApiKeys.DTOs;
using Starter.Shared.Results;

namespace Starter.Application.Features.ApiKeys.Queries.GetApiKeys;

public sealed class GetApiKeysQueryHandler(
    IApplicationDbContext dbContext,
    ICurrentUserService currentUserService)
    : IRequestHandler<GetApiKeysQuery, Result<PaginatedList<ApiKeyDto>>>
{
    public async Task<Result<PaginatedList<ApiKeyDto>>> Handle(
        GetApiKeysQuery request,
        CancellationToken cancellationToken)
    {
        var isPlatformAdmin = !currentUserService.TenantId.HasValue;

        IQueryable<ApiKeyDto> query;

        if (isPlatformAdmin)
        {
            // Platform admin: use IgnoreQueryFilters, filter by keyType
            var baseQuery = dbContext.ApiKeys
                .IgnoreQueryFilters()
                .AsNoTracking();

            // Apply keyType filter
            var keyType = request.KeyType?.ToLowerInvariant();
            if (keyType == "platform")
                baseQuery = baseQuery.Where(k => k.TenantId == null);
            else if (keyType == "tenant")
                baseQuery = baseQuery.Where(k => k.TenantId != null);
            // "all" or null: no additional filter (default to platform for safety)
            else if (keyType is null or "")
                baseQuery = baseQuery.Where(k => k.TenantId == null);

            // Optional tenant filter
            if (request.TenantId.HasValue)
                baseQuery = baseQuery.Where(k => k.TenantId == request.TenantId.Value);

            // Left-join tenants for TenantName
            query = from k in baseQuery
                    join t in dbContext.Tenants.IgnoreQueryFilters() on k.TenantId equals t.Id into tj
                    from tenant in tj.DefaultIfEmpty()
                    orderby k.CreatedAt descending
                    select new ApiKeyDto(
                        k.Id, k.Name, k.KeyPrefix, k.Scopes,
                        k.ExpiresAt, k.LastUsedAt, k.IsRevoked, k.IsExpired,
                        k.TenantId == null, k.TenantId,
                        tenant != null ? tenant.Name : null,
                        k.CreatedAt, k.CreatedBy);
        }
        else
        {
            // Tenant user: global filter applies, no join needed
            query = dbContext.ApiKeys
                .AsNoTracking()
                .OrderByDescending(k => k.CreatedAt)
                .Select(k => new ApiKeyDto(
                    k.Id, k.Name, k.KeyPrefix, k.Scopes,
                    k.ExpiresAt, k.LastUsedAt, k.IsRevoked, k.IsExpired,
                    false, k.TenantId, null,
                    k.CreatedAt, k.CreatedBy));
        }

        var result = await PaginatedList<ApiKeyDto>.CreateAsync(
            query, request.PageNumber, request.PageSize);

        return Result.Success(result);
    }
}
```

- [ ] **Step 2: Update `GetApiKeyByIdQueryHandler.cs`**

Read the current handler, then update to support platform admin view with tenant name:

```csharp
using MediatR;
using Microsoft.EntityFrameworkCore;
using Starter.Application.Common.Interfaces;
using Starter.Application.Features.ApiKeys.DTOs;
using Starter.Domain.ApiKeys.Errors;
using Starter.Shared.Results;

namespace Starter.Application.Features.ApiKeys.Queries.GetApiKeyById;

public sealed class GetApiKeyByIdQueryHandler(
    IApplicationDbContext dbContext,
    ICurrentUserService currentUserService)
    : IRequestHandler<GetApiKeyByIdQuery, Result<ApiKeyDto>>
{
    public async Task<Result<ApiKeyDto>> Handle(GetApiKeyByIdQuery request, CancellationToken cancellationToken)
    {
        var isPlatformAdmin = !currentUserService.TenantId.HasValue;

        if (isPlatformAdmin)
        {
            // Platform admin: see all keys, populate TenantName
            var result = await (
                from k in dbContext.ApiKeys.IgnoreQueryFilters().AsNoTracking()
                join t in dbContext.Tenants.IgnoreQueryFilters() on k.TenantId equals t.Id into tj
                from tenant in tj.DefaultIfEmpty()
                where k.Id == request.Id
                select new ApiKeyDto(
                    k.Id, k.Name, k.KeyPrefix, k.Scopes,
                    k.ExpiresAt, k.LastUsedAt, k.IsRevoked, k.IsExpired,
                    k.TenantId == null, k.TenantId,
                    tenant != null ? tenant.Name : null,
                    k.CreatedAt, k.CreatedBy)
            ).FirstOrDefaultAsync(cancellationToken);

            return result is not null
                ? Result.Success(result)
                : Result.Failure<ApiKeyDto>(ApiKeyErrors.NotFound);
        }
        else
        {
            // Tenant user: global filter applies
            var apiKey = await dbContext.ApiKeys
                .AsNoTracking()
                .FirstOrDefaultAsync(k => k.Id == request.Id, cancellationToken);

            return apiKey is not null
                ? Result.Success(apiKey.ToDto())
                : Result.Failure<ApiKeyDto>(ApiKeyErrors.NotFound);
        }
    }
}
```

- [ ] **Step 3: Build and verify**

Run: `cd boilerplateBE && dotnet build --verbosity quiet`
Expected: Build succeeded.

- [ ] **Step 4: Commit**

```bash
git add boilerplateBE/src/Starter.Application/Features/ApiKeys/Queries/
git commit -m "feat(api-keys): keyType filtering, tenant name join for platform admin queries"
```

---

## Task 7: Controller — Composite Policies & Emergency Revoke Endpoint

**Files:**
- Modify: `boilerplateBE/src/Starter.Api/Controllers/ApiKeysController.cs`

- [ ] **Step 1: Replace the controller**

Key changes:
- All `[Authorize(Policy = "...")]` become pipe-delimited composite policies.
- Add `EmergencyRevoke` endpoint.
- Accept `keyType` query param.

```csharp
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Starter.Application.Features.ApiKeys.Commands.CreateApiKey;
using Starter.Application.Features.ApiKeys.Commands.EmergencyRevokeApiKey;
using Starter.Application.Features.ApiKeys.Commands.RevokeApiKey;
using Starter.Application.Features.ApiKeys.Commands.UpdateApiKey;
using Starter.Application.Features.ApiKeys.Queries.GetApiKeyById;
using Starter.Application.Features.ApiKeys.Queries.GetApiKeys;
using Starter.Shared.Constants;

namespace Starter.Api.Controllers;

/// <summary>
/// API key management endpoints.
/// </summary>
public sealed class ApiKeysController(ISender mediator) : BaseApiController(mediator)
{
    /// <summary>
    /// Create a new API key (tenant-scoped or platform).
    /// </summary>
    [HttpPost]
    [Authorize(Policy = $"{Permissions.ApiKeys.Create}|{Permissions.ApiKeys.CreatePlatform}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateApiKeyCommand command, CancellationToken ct)
    {
        var result = await Mediator.Send(command, ct);
        return HandleResult(result);
    }

    /// <summary>
    /// Get paginated list of API keys. Platform admins use ?keyType=platform|tenant|all.
    /// </summary>
    [HttpGet]
    [Authorize(Policy = $"{Permissions.ApiKeys.View}|{Permissions.ApiKeys.ViewPlatform}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll([FromQuery] GetApiKeysQuery query)
    {
        var result = await Mediator.Send(query);
        return HandlePagedResult(result);
    }

    /// <summary>
    /// Get API key details by ID.
    /// </summary>
    [HttpGet("{id:guid}")]
    [Authorize(Policy = $"{Permissions.ApiKeys.View}|{Permissions.ApiKeys.ViewPlatform}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id)
    {
        var result = await Mediator.Send(new GetApiKeyByIdQuery(id));
        return HandleResult(result);
    }

    /// <summary>
    /// Update API key name or scopes.
    /// </summary>
    [HttpPatch("{id:guid}")]
    [Authorize(Policy = $"{Permissions.ApiKeys.Update}|{Permissions.ApiKeys.UpdatePlatform}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateApiKeyRequest request, CancellationToken ct)
    {
        var command = new UpdateApiKeyCommand(id, request.Name, request.Scopes);
        var result = await Mediator.Send(command, ct);
        return HandleResult(result);
    }

    /// <summary>
    /// Revoke an API key. Tenant users can revoke their own keys. Platform admins can revoke platform keys.
    /// For tenant keys, platform admins must use the emergency-revoke endpoint.
    /// </summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Policy = $"{Permissions.ApiKeys.Delete}|{Permissions.ApiKeys.DeletePlatform}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Revoke(Guid id, CancellationToken ct)
    {
        var result = await Mediator.Send(new RevokeApiKeyCommand(id), ct);
        return HandleResult(result);
    }

    /// <summary>
    /// Emergency revoke any API key. Platform admins only.
    /// </summary>
    [HttpDelete("{id:guid}/emergency-revoke")]
    [Authorize(Policy = Permissions.ApiKeys.EmergencyRevoke)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> EmergencyRevoke(Guid id, [FromBody] EmergencyRevokeRequest? request, CancellationToken ct)
    {
        var result = await Mediator.Send(new EmergencyRevokeApiKeyCommand(id, request?.Reason), ct);
        return HandleResult(result);
    }
}

/// <summary>
/// Request body for updating an API key.
/// </summary>
public sealed record UpdateApiKeyRequest(string? Name, List<string>? Scopes);

/// <summary>
/// Request body for emergency revoking an API key.
/// </summary>
public sealed record EmergencyRevokeRequest(string? Reason);
```

- [ ] **Step 2: Build and verify**

Run: `cd boilerplateBE && dotnet build --verbosity quiet`
Expected: Build succeeded.

- [ ] **Step 3: Full backend build + verify no regressions**

Run: `cd boilerplateBE && dotnet build`
Expected: Build succeeded with 0 errors.

- [ ] **Step 4: Commit**

```bash
git add boilerplateBE/src/Starter.Api/Controllers/ApiKeysController.cs
git commit -m "feat(api-keys): composite auth policies, emergency-revoke endpoint"
```

---

## Task 8: Frontend — Permissions, API Layer, Query Keys

**Files:**
- Modify: `boilerplateFE/src/constants/permissions.ts`
- Modify: `boilerplateFE/src/config/api.config.ts`
- Modify: `boilerplateFE/src/lib/query/keys.ts`
- Modify: `boilerplateFE/src/features/api-keys/api/api-keys.api.ts`
- Modify: `boilerplateFE/src/features/api-keys/api/api-keys.queries.ts`

- [ ] **Step 1: Update `permissions.ts`**

Add platform and emergency permissions to the `ApiKeys` section:

```typescript
ApiKeys: {
  View: 'ApiKeys.View',
  Create: 'ApiKeys.Create',
  Update: 'ApiKeys.Update',
  Delete: 'ApiKeys.Delete',
  ViewPlatform: 'ApiKeys.ViewPlatform',
  CreatePlatform: 'ApiKeys.CreatePlatform',
  UpdatePlatform: 'ApiKeys.UpdatePlatform',
  DeletePlatform: 'ApiKeys.DeletePlatform',
  EmergencyRevoke: 'ApiKeys.EmergencyRevoke',
},
```

- [ ] **Step 2: Update `api.config.ts`**

Add emergency-revoke endpoint to the `API_KEYS` section:

```typescript
API_KEYS: {
  LIST: '/ApiKeys',
  DETAIL: (id: string) => `/ApiKeys/${id}`,
  EMERGENCY_REVOKE: (id: string) => `/ApiKeys/${id}/emergency-revoke`,
},
```

- [ ] **Step 3: Update `api-keys.api.ts`**

Replace the full file to add `keyType` param, `isPlatformKey`, and `emergencyRevoke`:

```typescript
import { apiClient } from '@/lib/axios';
import { API_ENDPOINTS } from '@/config';

export interface ApiKeyDto {
  id: string;
  name: string;
  keyPrefix: string;
  scopes: string[];
  expiresAt: string | null;
  lastUsedAt: string | null;
  isRevoked: boolean;
  isExpired: boolean;
  isPlatformKey: boolean;
  tenantId: string | null;
  tenantName: string | null;
  createdAt: string;
  createdBy: string | null;
}

export interface CreateApiKeyResponse {
  id: string;
  name: string;
  keyPrefix: string;
  fullKey: string;
  scopes: string[];
  expiresAt: string | null;
  createdAt: string;
}

export interface CreateApiKeyData {
  name: string;
  scopes: string[];
  expiresAt?: string | null;
  isPlatformKey?: boolean;
}

export interface UpdateApiKeyData {
  name?: string;
  scopes?: string[];
}

export const apiKeysApi = {
  getApiKeys: (params?: Record<string, unknown>) =>
    apiClient.get(API_ENDPOINTS.API_KEYS.LIST, { params }).then(r => r.data),

  getApiKeyById: async (id: string): Promise<ApiKeyDto> => {
    const response = await apiClient.get<{ data: ApiKeyDto }>(API_ENDPOINTS.API_KEYS.DETAIL(id));
    return response.data.data;
  },

  createApiKey: async (data: CreateApiKeyData): Promise<CreateApiKeyResponse> => {
    const response = await apiClient.post<{ data: CreateApiKeyResponse }>(
      API_ENDPOINTS.API_KEYS.LIST,
      data
    );
    return response.data.data;
  },

  updateApiKey: async (id: string, data: UpdateApiKeyData): Promise<ApiKeyDto> => {
    const response = await apiClient.patch<{ data: ApiKeyDto }>(
      API_ENDPOINTS.API_KEYS.DETAIL(id),
      data
    );
    return response.data.data;
  },

  revokeApiKey: async (id: string): Promise<void> => {
    await apiClient.delete(API_ENDPOINTS.API_KEYS.DETAIL(id));
  },

  emergencyRevokeApiKey: async (id: string, reason?: string): Promise<void> => {
    await apiClient.delete(API_ENDPOINTS.API_KEYS.EMERGENCY_REVOKE(id), {
      data: reason ? { reason } : undefined,
    });
  },
};
```

- [ ] **Step 4: Update `api-keys.queries.ts`**

Add hooks for platform queries and emergency revoke:

```typescript
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { toast } from 'sonner';
import { apiKeysApi } from './api-keys.api';
import { queryKeys } from '@/lib/query/keys';
import i18n from '@/i18n';
import type { CreateApiKeyData, UpdateApiKeyData } from './api-keys.api';

export function useApiKeys(params?: Record<string, unknown>) {
  return useQuery({
    queryKey: queryKeys.apiKeys.list(params),
    queryFn: () => apiKeysApi.getApiKeys(params),
  });
}

export function useApiKey(id: string) {
  return useQuery({
    queryKey: queryKeys.apiKeys.detail(id),
    queryFn: () => apiKeysApi.getApiKeyById(id),
    enabled: !!id,
  });
}

export function useCreateApiKey() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (data: CreateApiKeyData) => apiKeysApi.createApiKey(data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: queryKeys.apiKeys.all });
      toast.success(i18n.t('apiKeys.createdSuccess'));
    },
  });
}

export function useUpdateApiKey() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ id, data }: { id: string; data: UpdateApiKeyData }) =>
      apiKeysApi.updateApiKey(id, data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: queryKeys.apiKeys.all });
      toast.success(i18n.t('apiKeys.updatedSuccess'));
    },
  });
}

export function useRevokeApiKey() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => apiKeysApi.revokeApiKey(id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: queryKeys.apiKeys.all });
      toast.success(i18n.t('apiKeys.revokedSuccess'));
    },
  });
}

export function useEmergencyRevokeApiKey() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ id, reason }: { id: string; reason?: string }) =>
      apiKeysApi.emergencyRevokeApiKey(id, reason),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: queryKeys.apiKeys.all });
      toast.success(i18n.t('apiKeys.emergencyRevokedSuccess'));
    },
  });
}
```

- [ ] **Step 5: Query keys are already correct**

The existing `queryKeys.apiKeys.list(filters)` structure includes `filters` in the key. When the platform admin passes `{ keyType: 'platform' }` vs `{ keyType: 'tenant' }`, they get separate cache entries. No change needed.

- [ ] **Step 6: Build frontend**

Run: `cd boilerplateFE && npm run build`
Expected: Build succeeds.

- [ ] **Step 7: Commit**

```bash
git add boilerplateFE/src/constants/permissions.ts \
       boilerplateFE/src/config/api.config.ts \
       boilerplateFE/src/features/api-keys/api/
git commit -m "feat(api-keys): frontend API layer for dual-scope, emergency revoke"
```

---

## Task 9: Frontend — EmergencyRevokeDialog Component

**Files:**
- Create: `boilerplateFE/src/features/api-keys/components/EmergencyRevokeDialog.tsx`

- [ ] **Step 1: Create `EmergencyRevokeDialog.tsx`**

```tsx
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { AlertTriangle } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { useEmergencyRevokeApiKey } from '../api';
import type { ApiKeyDto } from '../api';

interface EmergencyRevokeDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  apiKey: ApiKeyDto | null;
}

export function EmergencyRevokeDialog({ open, onOpenChange, apiKey }: EmergencyRevokeDialogProps) {
  const { t } = useTranslation();
  const emergencyRevoke = useEmergencyRevokeApiKey();
  const [confirmName, setConfirmName] = useState('');
  const [reason, setReason] = useState('');

  const isConfirmed = confirmName === apiKey?.name;

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!apiKey || !isConfirmed) return;
    await emergencyRevoke.mutateAsync({ id: apiKey.id, reason: reason || undefined });
    setConfirmName('');
    setReason('');
    onOpenChange(false);
  };

  const handleClose = () => {
    setConfirmName('');
    setReason('');
    onOpenChange(false);
  };

  return (
    <Dialog open={open} onOpenChange={handleClose}>
      <DialogContent className="max-w-md">
        <DialogHeader>
          <DialogTitle>{t('apiKeys.emergencyRevokeTitle')}</DialogTitle>
          <DialogDescription>{t('apiKeys.emergencyRevokeDescription')}</DialogDescription>
        </DialogHeader>

        <form onSubmit={handleSubmit} className="space-y-4">
          <div className="flex items-start gap-2 rounded-xl border border-destructive/30 bg-destructive/5 p-3">
            <AlertTriangle className="mt-0.5 h-4 w-4 shrink-0 text-destructive" />
            <p className="text-sm text-destructive">
              {t('apiKeys.emergencyRevokeWarning', { tenant: apiKey?.tenantName ?? t('common.unknown') })}
            </p>
          </div>

          <div className="space-y-2">
            <Label>{t('apiKeys.emergencyRevokeConfirmLabel', { name: apiKey?.name })}</Label>
            <Input
              value={confirmName}
              onChange={e => setConfirmName(e.target.value)}
              placeholder={apiKey?.name ?? ''}
            />
          </div>

          <div className="space-y-2">
            <Label>{t('apiKeys.emergencyRevokeReason')}</Label>
            <Textarea
              value={reason}
              onChange={e => setReason(e.target.value)}
              placeholder={t('apiKeys.emergencyRevokeReasonPlaceholder')}
              rows={2}
            />
          </div>

          <DialogFooter>
            <Button type="button" variant="outline" onClick={handleClose}>
              {t('common.cancel')}
            </Button>
            <Button
              type="submit"
              variant="destructive"
              disabled={!isConfirmed || emergencyRevoke.isPending}
            >
              {emergencyRevoke.isPending ? t('common.loading') : t('apiKeys.emergencyRevokeConfirm')}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}
```

- [ ] **Step 2: Commit**

```bash
git add boilerplateFE/src/features/api-keys/components/EmergencyRevokeDialog.tsx
git commit -m "feat(api-keys): emergency revoke dialog with name confirmation"
```

---

## Task 10: Frontend — PlatformKeysTab & TenantKeysTab Components

**Files:**
- Create: `boilerplateFE/src/features/api-keys/components/PlatformKeysTab.tsx`
- Create: `boilerplateFE/src/features/api-keys/components/TenantKeysTab.tsx`

- [ ] **Step 1: Create `PlatformKeysTab.tsx`**

This is essentially the current table view but queries with `keyType=platform`:

```tsx
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { formatDateTime } from '@/utils/format';
import { Key, Plus, Trash2, Pencil } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import {
  Table, TableBody, TableCell, TableHead, TableHeader, TableRow,
} from '@/components/ui/table';
import { EmptyState, ConfirmDialog, Pagination, getPersistedPageSize } from '@/components/common';
import { useApiKeys, useRevokeApiKey } from '../api';
import { usePermissions } from '@/hooks';
import { PERMISSIONS } from '@/constants';
import type { ApiKeyDto, CreateApiKeyResponse } from '../api';
import { CreateApiKeyDialog } from './CreateApiKeyDialog';
import { ApiKeySecretDisplay } from './ApiKeySecretDisplay';

export function PlatformKeysTab() {
  const { t } = useTranslation();
  const { hasPermission } = usePermissions();
  const [pageNumber, setPageNumber] = useState(1);
  const [pageSize, setPageSize] = useState(getPersistedPageSize);
  const { data, isLoading } = useApiKeys({ pageNumber, pageSize, keyType: 'platform' });
  const revokeMutation = useRevokeApiKey();

  const [showCreate, setShowCreate] = useState(false);
  const [createdKey, setCreatedKey] = useState<CreateApiKeyResponse | null>(null);
  const [revokeTarget, setRevokeTarget] = useState<ApiKeyDto | null>(null);

  const apiKeys: ApiKeyDto[] = data?.data ?? [];
  const pagination = data?.pagination;

  const handleCreated = (response: CreateApiKeyResponse) => {
    setShowCreate(false);
    setCreatedKey(response);
  };

  const handleRevoke = async () => {
    if (!revokeTarget) return;
    await revokeMutation.mutateAsync(revokeTarget.id);
    setRevokeTarget(null);
  };

  if (apiKeys.length === 0 && !isLoading) {
    return (
      <>
        <EmptyState
          icon={Key}
          title={t('apiKeys.emptyPlatformTitle')}
          description={t('apiKeys.emptyPlatformDescription')}
          action={
            hasPermission(PERMISSIONS.ApiKeys.CreatePlatform)
              ? { label: t('apiKeys.createPlatformKey'), onClick: () => setShowCreate(true) }
              : undefined
          }
        />
        <CreateApiKeyDialog open={showCreate} onOpenChange={setShowCreate} onCreated={handleCreated} isPlatform />
        {createdKey && (
          <ApiKeySecretDisplay open={!!createdKey} onOpenChange={() => setCreatedKey(null)} response={createdKey} />
        )}
      </>
    );
  }

  return (
    <>
      {hasPermission(PERMISSIONS.ApiKeys.CreatePlatform) && (
        <div className="flex justify-end">
          <Button onClick={() => setShowCreate(true)}>
            <Plus className="mr-2 h-4 w-4" />
            {t('apiKeys.createPlatformKey')}
          </Button>
        </div>
      )}

      <Table>
        <TableHeader>
          <TableRow>
            <TableHead>{t('apiKeys.name')}</TableHead>
            <TableHead>{t('apiKeys.prefix')}</TableHead>
            <TableHead>{t('apiKeys.scopes')}</TableHead>
            <TableHead>{t('apiKeys.status')}</TableHead>
            <TableHead>{t('apiKeys.lastUsed')}</TableHead>
            <TableHead>{t('apiKeys.expires')}</TableHead>
            <TableHead>{t('apiKeys.created')}</TableHead>
            <TableHead className="text-right">{t('common.actions')}</TableHead>
          </TableRow>
        </TableHeader>
        <TableBody>
          {apiKeys.map((key) => (
            <TableRow key={key.id}>
              <TableCell className="font-medium text-foreground">{key.name}</TableCell>
              <TableCell>
                <code className="rounded-md bg-secondary px-2 py-1 text-xs text-muted-foreground">
                  {key.keyPrefix}...
                </code>
              </TableCell>
              <TableCell>
                <div className="flex flex-wrap gap-1">
                  {key.scopes.map((scope) => (
                    <Badge key={scope} variant="outline" className="text-xs">{scope}</Badge>
                  ))}
                </div>
              </TableCell>
              <TableCell>
                {key.isRevoked
                  ? <Badge variant="destructive">{t('apiKeys.statusRevoked')}</Badge>
                  : key.isExpired
                    ? <Badge variant="secondary">{t('apiKeys.statusExpired')}</Badge>
                    : <Badge variant="default">{t('apiKeys.statusActive')}</Badge>}
              </TableCell>
              <TableCell className="text-muted-foreground">
                {key.lastUsedAt ? formatDateTime(key.lastUsedAt) : t('apiKeys.never')}
              </TableCell>
              <TableCell className="text-muted-foreground">
                {key.expiresAt ? formatDateTime(key.expiresAt) : t('apiKeys.noExpiry')}
              </TableCell>
              <TableCell className="text-muted-foreground">{formatDateTime(key.createdAt)}</TableCell>
              <TableCell className="text-right">
                {!key.isRevoked && hasPermission(PERMISSIONS.ApiKeys.DeletePlatform) && (
                  <Button variant="ghost" size="icon" onClick={() => setRevokeTarget(key)}>
                    <Trash2 className="h-4 w-4 text-destructive" />
                  </Button>
                )}
              </TableCell>
            </TableRow>
          ))}
        </TableBody>
      </Table>

      {pagination && (
        <Pagination
          pagination={pagination}
          onPageChange={setPageNumber}
          onPageSizeChange={(size) => { setPageSize(size); setPageNumber(1); }}
        />
      )}

      <CreateApiKeyDialog open={showCreate} onOpenChange={setShowCreate} onCreated={handleCreated} isPlatform />
      {createdKey && (
        <ApiKeySecretDisplay open={!!createdKey} onOpenChange={() => setCreatedKey(null)} response={createdKey} />
      )}
      <ConfirmDialog
        isOpen={!!revokeTarget}
        onClose={() => setRevokeTarget(null)}
        title={t('apiKeys.revokeTitle')}
        description={t('apiKeys.revokeDescription', { name: revokeTarget?.name })}
        confirmLabel={t('apiKeys.revokeConfirm')}
        onConfirm={handleRevoke}
        isLoading={revokeMutation.isPending}
        variant="danger"
      />
    </>
  );
}
```

- [ ] **Step 2: Create `TenantKeysTab.tsx`**

Read-only table with tenant column and emergency revoke:

```tsx
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { formatDateTime } from '@/utils/format';
import { Key, AlertTriangle } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import {
  Table, TableBody, TableCell, TableHead, TableHeader, TableRow,
} from '@/components/ui/table';
import { EmptyState, Pagination, getPersistedPageSize } from '@/components/common';
import { useApiKeys } from '../api';
import { usePermissions } from '@/hooks';
import { PERMISSIONS } from '@/constants';
import type { ApiKeyDto } from '../api';
import { EmergencyRevokeDialog } from './EmergencyRevokeDialog';

export function TenantKeysTab() {
  const { t } = useTranslation();
  const { hasPermission } = usePermissions();
  const [pageNumber, setPageNumber] = useState(1);
  const [pageSize, setPageSize] = useState(getPersistedPageSize);
  const { data, isLoading } = useApiKeys({ pageNumber, pageSize, keyType: 'tenant' });

  const [emergencyTarget, setEmergencyTarget] = useState<ApiKeyDto | null>(null);

  const apiKeys: ApiKeyDto[] = data?.data ?? [];
  const pagination = data?.pagination;

  if (apiKeys.length === 0 && !isLoading) {
    return (
      <EmptyState
        icon={Key}
        title={t('apiKeys.emptyTenantTitle')}
        description={t('apiKeys.emptyTenantDescription')}
      />
    );
  }

  return (
    <>
      <Table>
        <TableHeader>
          <TableRow>
            <TableHead>{t('apiKeys.tenant')}</TableHead>
            <TableHead>{t('apiKeys.name')}</TableHead>
            <TableHead>{t('apiKeys.prefix')}</TableHead>
            <TableHead>{t('apiKeys.scopes')}</TableHead>
            <TableHead>{t('apiKeys.status')}</TableHead>
            <TableHead>{t('apiKeys.lastUsed')}</TableHead>
            <TableHead>{t('apiKeys.created')}</TableHead>
            {hasPermission(PERMISSIONS.ApiKeys.EmergencyRevoke) && (
              <TableHead className="text-right">{t('common.actions')}</TableHead>
            )}
          </TableRow>
        </TableHeader>
        <TableBody>
          {apiKeys.map((key) => (
            <TableRow key={key.id}>
              <TableCell className="font-medium text-foreground">
                {key.tenantName ?? '-'}
              </TableCell>
              <TableCell className="text-foreground">{key.name}</TableCell>
              <TableCell>
                <code className="rounded-md bg-secondary px-2 py-1 text-xs text-muted-foreground">
                  {key.keyPrefix}...
                </code>
              </TableCell>
              <TableCell>
                <div className="flex flex-wrap gap-1">
                  {key.scopes.map((scope) => (
                    <Badge key={scope} variant="outline" className="text-xs">{scope}</Badge>
                  ))}
                </div>
              </TableCell>
              <TableCell>
                {key.isRevoked
                  ? <Badge variant="destructive">{t('apiKeys.statusRevoked')}</Badge>
                  : key.isExpired
                    ? <Badge variant="secondary">{t('apiKeys.statusExpired')}</Badge>
                    : <Badge variant="default">{t('apiKeys.statusActive')}</Badge>}
              </TableCell>
              <TableCell className="text-muted-foreground">
                {key.lastUsedAt ? formatDateTime(key.lastUsedAt) : t('apiKeys.never')}
              </TableCell>
              <TableCell className="text-muted-foreground">{formatDateTime(key.createdAt)}</TableCell>
              {hasPermission(PERMISSIONS.ApiKeys.EmergencyRevoke) && (
                <TableCell className="text-right">
                  {!key.isRevoked && (
                    <Button
                      variant="ghost"
                      size="sm"
                      className="text-destructive hover:text-destructive"
                      onClick={() => setEmergencyTarget(key)}
                    >
                      <AlertTriangle className="mr-1 h-4 w-4" />
                      {t('apiKeys.emergencyRevoke')}
                    </Button>
                  )}
                </TableCell>
              )}
            </TableRow>
          ))}
        </TableBody>
      </Table>

      {pagination && (
        <Pagination
          pagination={pagination}
          onPageChange={setPageNumber}
          onPageSizeChange={(size) => { setPageSize(size); setPageNumber(1); }}
        />
      )}

      <EmergencyRevokeDialog
        open={!!emergencyTarget}
        onOpenChange={() => setEmergencyTarget(null)}
        apiKey={emergencyTarget}
      />
    </>
  );
}
```

- [ ] **Step 3: Commit**

```bash
git add boilerplateFE/src/features/api-keys/components/PlatformKeysTab.tsx \
       boilerplateFE/src/features/api-keys/components/TenantKeysTab.tsx
git commit -m "feat(api-keys): platform keys tab, tenant keys tab with emergency revoke"
```

---

## Task 11: Frontend — ApiKeysPage Dual View & CreateDialog Update

**Files:**
- Modify: `boilerplateFE/src/features/api-keys/pages/ApiKeysPage.tsx`
- Modify: `boilerplateFE/src/features/api-keys/components/CreateApiKeyDialog.tsx`

- [ ] **Step 1: Update `CreateApiKeyDialog.tsx` to accept `isPlatform` prop**

Add an `isPlatform` prop. When true, send `isPlatformKey: true` in the API call. The dialog UI is the same either way.

In the props interface, add:

```typescript
interface CreateApiKeyDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  onCreated: (response: CreateApiKeyResponse) => void;
  isPlatform?: boolean;  // NEW
}
```

In the `handleSubmit` function, include `isPlatformKey`:

```typescript
const result = await createMutation.mutateAsync({
  name,
  scopes,
  expiresAt: expiresAt || null,
  isPlatformKey: isPlatform ?? false,
});
```

- [ ] **Step 2: Rewrite `ApiKeysPage.tsx` for dual view**

```tsx
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Key, Plus } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Spinner } from '@/components/ui/spinner';
import {
  Table, TableBody, TableCell, TableHead, TableHeader, TableRow,
} from '@/components/ui/table';
import { PageHeader, EmptyState, ConfirmDialog, Pagination, getPersistedPageSize } from '@/components/common';
import { useApiKeys, useRevokeApiKey } from '../api';
import { CreateApiKeyDialog } from '../components/CreateApiKeyDialog';
import { ApiKeySecretDisplay } from '../components/ApiKeySecretDisplay';
import { PlatformKeysTab } from '../components/PlatformKeysTab';
import { TenantKeysTab } from '../components/TenantKeysTab';
import { usePermissions } from '@/hooks';
import { PERMISSIONS } from '@/constants';
import { formatDateTime } from '@/utils/format';
import type { ApiKeyDto, CreateApiKeyResponse } from '../api';

export default function ApiKeysPage() {
  const { t } = useTranslation();
  const { hasPermission } = usePermissions();

  const isPlatformAdmin = hasPermission(PERMISSIONS.ApiKeys.ViewPlatform);

  if (isPlatformAdmin) {
    return <PlatformAdminView />;
  }

  return <TenantUserView />;
}

/** Platform admin sees tabs: Platform Keys | Tenant Keys */
function PlatformAdminView() {
  const { t } = useTranslation();
  const [activeTab, setActiveTab] = useState<'platform' | 'tenant'>('platform');

  return (
    <div className="space-y-6">
      <PageHeader
        title={t('apiKeys.title')}
        subtitle={t('apiKeys.description')}
      />

      {/* Tabs */}
      <div className="flex gap-1 border-b border-border">
        <button
          onClick={() => setActiveTab('platform')}
          className={`px-4 py-2 text-sm font-medium transition-colors border-b-2 -mb-px ${
            activeTab === 'platform'
              ? 'border-primary [color:var(--active-text)]'
              : 'border-transparent text-muted-foreground hover:text-foreground'
          }`}
        >
          {t('apiKeys.platformKeys')}
        </button>
        <button
          onClick={() => setActiveTab('tenant')}
          className={`px-4 py-2 text-sm font-medium transition-colors border-b-2 -mb-px ${
            activeTab === 'tenant'
              ? 'border-primary [color:var(--active-text)]'
              : 'border-transparent text-muted-foreground hover:text-foreground'
          }`}
        >
          {t('apiKeys.tenantKeys')}
        </button>
      </div>

      {activeTab === 'platform' ? <PlatformKeysTab /> : <TenantKeysTab />}
    </div>
  );
}

/** Tenant user sees simple CRUD table for their keys */
function TenantUserView() {
  const { t } = useTranslation();
  const { hasPermission } = usePermissions();
  const [pageNumber, setPageNumber] = useState(1);
  const [pageSize, setPageSize] = useState(getPersistedPageSize);
  const { data, isLoading, isError } = useApiKeys({ pageNumber, pageSize });
  const revokeMutation = useRevokeApiKey();

  const [showCreate, setShowCreate] = useState(false);
  const [createdKey, setCreatedKey] = useState<CreateApiKeyResponse | null>(null);
  const [revokeTarget, setRevokeTarget] = useState<ApiKeyDto | null>(null);

  const apiKeys: ApiKeyDto[] = data?.data ?? [];
  const pagination = data?.pagination;

  const handleCreated = (response: CreateApiKeyResponse) => {
    setShowCreate(false);
    setCreatedKey(response);
  };

  const handleRevoke = async () => {
    if (!revokeTarget) return;
    await revokeMutation.mutateAsync(revokeTarget.id);
    setRevokeTarget(null);
  };

  if (isError) {
    return (
      <div className="space-y-6">
        <PageHeader title={t('apiKeys.title')} />
        <EmptyState icon={Key} title={t('common.errorOccurred')} description={t('common.tryAgain')} />
      </div>
    );
  }

  if (isLoading && !data) {
    return (
      <div className="flex justify-center py-12">
        <Spinner size="lg" />
      </div>
    );
  }

  return (
    <div className="space-y-6">
      <PageHeader
        title={t('apiKeys.title')}
        subtitle={t('apiKeys.description')}
        actions={
          hasPermission(PERMISSIONS.ApiKeys.Create) ? (
            <Button onClick={() => setShowCreate(true)}>
              <Plus className="mr-2 h-4 w-4" />
              {t('apiKeys.create')}
            </Button>
          ) : undefined
        }
      />

      {apiKeys.length === 0 ? (
        <EmptyState
          icon={Key}
          title={t('apiKeys.emptyTitle')}
          description={t('apiKeys.emptyDescription')}
          action={
            hasPermission(PERMISSIONS.ApiKeys.Create)
              ? { label: t('apiKeys.create'), onClick: () => setShowCreate(true) }
              : undefined
          }
        />
      ) : (
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead>{t('apiKeys.name')}</TableHead>
              <TableHead>{t('apiKeys.prefix')}</TableHead>
              <TableHead>{t('apiKeys.scopes')}</TableHead>
              <TableHead>{t('apiKeys.status')}</TableHead>
              <TableHead>{t('apiKeys.lastUsed')}</TableHead>
              <TableHead>{t('apiKeys.expires')}</TableHead>
              <TableHead>{t('apiKeys.created')}</TableHead>
              <TableHead className="text-right">{t('common.actions')}</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {apiKeys.map((key) => (
              <TableRow key={key.id}>
                <TableCell className="font-medium text-foreground">{key.name}</TableCell>
                <TableCell>
                  <code className="rounded-md bg-secondary px-2 py-1 text-xs text-muted-foreground">
                    {key.keyPrefix}...
                  </code>
                </TableCell>
                <TableCell>
                  <div className="flex flex-wrap gap-1">
                    {key.scopes.map((scope) => (
                      <Badge key={scope} variant="outline" className="text-xs">{scope}</Badge>
                    ))}
                  </div>
                </TableCell>
                <TableCell>
                  {key.isRevoked
                    ? <Badge variant="destructive">{t('apiKeys.statusRevoked')}</Badge>
                    : key.isExpired
                      ? <Badge variant="secondary">{t('apiKeys.statusExpired')}</Badge>
                      : <Badge variant="default">{t('apiKeys.statusActive')}</Badge>}
                </TableCell>
                <TableCell className="text-muted-foreground">
                  {key.lastUsedAt ? formatDateTime(key.lastUsedAt) : t('apiKeys.never')}
                </TableCell>
                <TableCell className="text-muted-foreground">
                  {key.expiresAt ? formatDateTime(key.expiresAt) : t('apiKeys.noExpiry')}
                </TableCell>
                <TableCell className="text-muted-foreground">{formatDateTime(key.createdAt)}</TableCell>
                <TableCell className="text-right">
                  {!key.isRevoked && hasPermission(PERMISSIONS.ApiKeys.Delete) && (
                    <Button variant="ghost" size="icon" onClick={() => setRevokeTarget(key)}>
                      <Trash2 className="h-4 w-4 text-destructive" />
                    </Button>
                  )}
                </TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      )}

      {pagination && (
        <Pagination
          pagination={pagination}
          onPageChange={setPageNumber}
          onPageSizeChange={(size) => { setPageSize(size); setPageNumber(1); }}
        />
      )}

      <CreateApiKeyDialog open={showCreate} onOpenChange={setShowCreate} onCreated={handleCreated} />
      {createdKey && (
        <ApiKeySecretDisplay open={!!createdKey} onOpenChange={() => setCreatedKey(null)} response={createdKey} />
      )}
      <ConfirmDialog
        isOpen={!!revokeTarget}
        onClose={() => setRevokeTarget(null)}
        title={t('apiKeys.revokeTitle')}
        description={t('apiKeys.revokeDescription', { name: revokeTarget?.name })}
        confirmLabel={t('apiKeys.revokeConfirm')}
        onConfirm={handleRevoke}
        isLoading={revokeMutation.isPending}
        variant="danger"
      />
    </div>
  );
}
```

Note: Add the missing `Trash2` import at the top of the file.

- [ ] **Step 3: Build frontend**

Run: `cd boilerplateFE && npm run build`
Expected: Build succeeds.

- [ ] **Step 4: Commit**

```bash
git add boilerplateFE/src/features/api-keys/pages/ApiKeysPage.tsx \
       boilerplateFE/src/features/api-keys/components/CreateApiKeyDialog.tsx
git commit -m "feat(api-keys): dual view page (platform admin tabs + tenant CRUD)"
```

---

## Task 12: Frontend — i18n Translations

**Files:**
- Modify: `boilerplateFE/src/i18n/locales/en/translation.json`
- Modify: `boilerplateFE/src/i18n/locales/ar/translation.json`
- Modify: `boilerplateFE/src/i18n/locales/ku/translation.json`

- [ ] **Step 1: Add new English translation keys**

Add these keys inside the `"apiKeys"` section:

```json
"platformKeys": "Platform Keys",
"tenantKeys": "Tenant Keys",
"tenant": "Tenant",
"createPlatformKey": "Create Platform Key",
"emptyPlatformTitle": "No Platform Keys",
"emptyPlatformDescription": "Create your first platform API key for cross-tenant access.",
"emptyTenantTitle": "No Tenant Keys",
"emptyTenantDescription": "No tenants have created API keys yet.",
"emergencyRevoke": "Emergency Revoke",
"emergencyRevokeTitle": "Emergency Revoke API Key",
"emergencyRevokeDescription": "This is an emergency action. The key will be immediately revoked.",
"emergencyRevokeWarning": "This will permanently revoke a key belonging to tenant \"{{tenant}}\". This action cannot be undone. Use only for security incidents.",
"emergencyRevokeConfirmLabel": "Type \"{{name}}\" to confirm:",
"emergencyRevokeReason": "Reason (optional)",
"emergencyRevokeReasonPlaceholder": "e.g., Compromised key, security incident...",
"emergencyRevokeConfirm": "Emergency Revoke",
"emergencyRevokedSuccess": "API key emergency revoked successfully."
```

- [ ] **Step 2: Add Arabic translations**

Add the same keys with Arabic values inside the `"apiKeys"` section.

- [ ] **Step 3: Add Kurdish translations**

Add the same keys with Kurdish values inside the `"apiKeys"` section.

- [ ] **Step 4: Build frontend to verify JSON validity**

Run: `cd boilerplateFE && npm run build`
Expected: Build succeeds.

- [ ] **Step 5: Commit**

```bash
git add boilerplateFE/src/i18n/locales/
git commit -m "feat(api-keys): i18n translations for dual-scope, emergency revoke (en/ar/ku)"
```

---

## Task 13: Final Build & Verification

- [ ] **Step 1: Full backend build**

Run: `cd boilerplateBE && dotnet build`
Expected: Build succeeded, 0 errors.

- [ ] **Step 2: Full frontend build**

Run: `cd boilerplateFE && npm run build`
Expected: Build succeeds.

- [ ] **Step 3: Run the post-feature testing workflow**

Follow the testing skill (`.claude/skills/post-feature-testing.md`):
1. Create test app: `scripts/rename.ps1 -Name "_testApiKeysV2" -OutputDir "."`
2. Fix seed email (remove underscore from domain)
3. Generate EF migration: `dotnet ef migrations add InitialCreate`
4. Configure ports (5100/3100), CORS, .env
5. Start backend + frontend
6. Playwright test: Login → Platform admin tab → Create platform key → Tenant keys tab → Regression on all pages

- [ ] **Step 4: Fix any findings**

If tests reveal issues, fix in the worktree source and regenerate test app.

- [ ] **Step 5: Leave test instance running for manual QA**

Report URLs:
- Frontend: http://localhost:3100
- Backend: http://localhost:5100/swagger
