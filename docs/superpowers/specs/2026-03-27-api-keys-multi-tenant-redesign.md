# API Keys Multi-Tenant Redesign

## Summary

Redesign the API key system to support two tiers: **tenant-scoped keys** (created and managed by tenants for their own data) and **platform keys** (created by platform admins for cross-tenant or platform-level access). SuperAdmin can view tenant keys read-only and has emergency revoke capability.

## Current State

- `ApiKey` entity has nullable `TenantId` (already correct)
- Global query filter scopes keys by current user's tenant
- Auth handler already uses `IgnoreQueryFilters()` and sets `tenant_id` claim when present
- Single permission set: `ApiKeys.View/Create/Update/Delete`
- No distinction between tenant keys and platform keys in handlers or UI
- Create handler always sets `TenantId = currentUserService.TenantId`
- Auth handler uses `BCrypt.Net.BCrypt.Verify()` directly while CreateApiKeyCommandHandler uses `IPasswordService.HashPasswordAsync()` — these must use the same algorithm (both are BCrypt, verified in `PasswordService` implementation)

### Breaking Change Notice

Currently, if a platform admin (TenantId=null) creates an API key, it silently becomes a platform key (`TenantId=null`). After this redesign, platform admins must explicitly send `isPlatformKey: true`. This is an intentional breaking change — platform keys are high-privilege and should never be created accidentally. No migration path is needed since the feature is new and has no production consumers.

## Design

### Two Key Types

| Property | Tenant Key | Platform Key |
|----------|-----------|--------------|
| `TenantId` | Non-null (creator's tenant) | `null` |
| Created by | Tenant user with `ApiKeys.Create` | Platform admin with `ApiKeys.CreatePlatform` |
| Managed by | That tenant only | Platform admins only |
| Data access | Scoped to that tenant | Cross-tenant (default) or per-request via `X-Tenant-Id` header |
| Visible to SuperAdmin | Read-only + emergency revoke | Full CRUD |
| Visible to tenant | Full CRUD (own keys) | Not visible |

### Authentication Flow

When a request arrives with `X-Api-Key` header:

1. Validate the key (prefix lookup via `IgnoreQueryFilters()` + `BCrypt.Net.BCrypt.Verify()`).
2. Read the key's `TenantId`.
   - **Tenant key** (`TenantId` is set): Set `tenant_id` claim to that value. Ignore any `X-Tenant-Id` header — the key is locked to its tenant.
   - **Platform key** (`TenantId` is null): Check for `X-Tenant-Id` request header.
     - Header present: Validate the tenant ID exists in the `Tenants` table **and is Active** (not Suspended or Deactivated). If valid, set `tenant_id` claim. If invalid or inactive, return `AuthenticateResult.Fail("Invalid or inactive tenant.")`.
     - Header absent: Set no `tenant_id` claim. Downstream queries see all tenants (the existing global query filter already allows this when `TenantId == null`).
3. Add scope claims as `permission` claims (unchanged).
4. Add `is_platform_key` claim with value `"true"` or `"false"` for downstream authorization checks.
5. Update `LastUsedAt` (fire-and-forget, unchanged).

### Permissions

Extend `Permissions.ApiKeys` with platform-level permissions:

```
ApiKeys.View              — View own tenant's API keys
ApiKeys.Create            — Create tenant-scoped API key
ApiKeys.Update            — Update own tenant's API keys
ApiKeys.Delete            — Revoke own tenant's API keys
ApiKeys.ViewPlatform      — View platform API keys + read-only view of all tenant keys
ApiKeys.CreatePlatform    — Create platform-scoped API key
ApiKeys.UpdatePlatform    — Update platform API keys
ApiKeys.DeletePlatform    — Revoke platform API keys
ApiKeys.EmergencyRevoke   — Revoke any tenant's API key (emergency action)
```

**Role assignments:**
- `SuperAdmin` role: Gets all `ApiKeys.*` permissions (both tenant-level and platform-level).
- `Admin` role: Gets `ApiKeys.View`, `ApiKeys.Create`, `ApiKeys.Update`, `ApiKeys.Delete` (tenant-level only).
- `User` role: No API key permissions by default.

### Controller Authorization — Composite Policies

Each endpoint needs a composite `[Authorize]` policy that accepts ANY of the relevant permissions, with fine-grained ownership checks in the handler. This solves the problem where a platform admin with only `ApiKeys.CreatePlatform` (but not `ApiKeys.Create`) would be rejected at the gate.

| Endpoint | Controller Policy (any of) | Handler checks |
|----------|---------------------------|----------------|
| `GET /ApiKeys` | `ApiKeys.View` OR `ApiKeys.ViewPlatform` | Scope filtering based on caller context |
| `GET /ApiKeys/{id}` | `ApiKeys.View` OR `ApiKeys.ViewPlatform` | Ownership check: tenant user can only see own keys |
| `POST /ApiKeys` | `ApiKeys.Create` OR `ApiKeys.CreatePlatform` | Tenant user: sets TenantId. Platform admin: requires `isPlatformKey=true` |
| `PATCH /ApiKeys/{id}` | `ApiKeys.Update` OR `ApiKeys.UpdatePlatform` | Ownership: tenant key → tenant only. Platform key → platform admin only |
| `DELETE /ApiKeys/{id}` | `ApiKeys.Delete` OR `ApiKeys.DeletePlatform` | Ownership: tenant key → tenant user. Platform key → platform admin |
| `DELETE /ApiKeys/{id}/emergency-revoke` | `ApiKeys.EmergencyRevoke` | Platform admin only. Any key regardless of tenant |

**Implementation:** Register a custom `IAuthorizationPolicyProvider` or use `[Authorize]` with a composite requirement. The simplest approach: create a helper attribute `[AuthorizeAny("ApiKeys.View", "ApiKeys.ViewPlatform")]` that succeeds if the user has any of the listed permissions. The handler then performs the fine-grained check and returns `403` or `404` as appropriate.

### HTTP Response Codes for Authorization Failures

- Tenant user requests a platform key by ID → **404 Not Found** (hide existence, prevent enumeration).
- Tenant user requests another tenant's key by ID → **404 Not Found**.
- Platform admin tries to update a tenant key → **403 Forbidden** (they can see it, so 404 would be confusing).
- Missing permission entirely → **403 Forbidden** (handled by controller-level policy).

### Query Filter Strategy

The existing query filter already handles the dual-scope correctly:

```csharp
modelBuilder.Entity<ApiKey>().HasQueryFilter(a =>
    TenantId == null || a.TenantId == TenantId);
```

- Platform admin (`TenantId == null`): Sees all keys (both platform and tenant).
- Tenant user (`TenantId == guid`): Sees only keys where `a.TenantId == their tenant`.

No changes needed to the filter itself. The handlers control what subset to return.

### API Endpoints

The controller remains at `/api/v1/ApiKeys`. The behavior changes based on the caller's context.

**`GET /ApiKeys`** — List API keys
- Query parameter: `?keyType=tenant|platform|all` (renamed from `scope` to avoid confusion with the key's `Scopes` field)
- Tenant user: Returns only their tenant's keys. Ignores `keyType` parameter.
- Platform admin:
  - `keyType=platform` (default): Returns only platform keys (`TenantId == null`).
  - `keyType=tenant`: Returns all tenant keys (read-only view). Adds `tenantId` and `tenantName` to the DTO.
  - `keyType=all`: Returns both.
- Optional filter: `?tenantId={guid}` — Platform admin can filter tenant keys by specific tenant.

**`GET /ApiKeys/{id}`** — Get single API key
- Tenant user: Returns key only if `TenantId == their tenant`. Otherwise 404.
- Platform admin: Returns any key. Populates `TenantName` for tenant keys.

**`POST /ApiKeys`** — Create API key
- Tenant user: Creates key with `TenantId = currentUser.TenantId`. The `isPlatformKey` field is ignored/rejected.
- Platform admin: Must send `"isPlatformKey": true` in the body. Creates key with `TenantId = null`. If `isPlatformKey` is false or missing, return 400 with message "Platform admins must explicitly create platform keys."

**`PATCH /ApiKeys/{id}`** — Update API key
- Tenant user: Can only update keys where `TenantId == their tenant`. Otherwise 404.
- Platform admin: Can only update platform keys (`TenantId == null`). For tenant keys, return 403.

**`DELETE /ApiKeys/{id}`** — Revoke API key
- Tenant user: Can only revoke keys where `TenantId == their tenant`. Otherwise 404.
- Platform admin: Can revoke platform keys (requires `ApiKeys.DeletePlatform`). For tenant keys via this endpoint, return 403 with message directing to emergency-revoke.

**`DELETE /ApiKeys/{id}/emergency-revoke`** — Emergency revoke (separate endpoint)
- Only platform admins with `ApiKeys.EmergencyRevoke` permission.
- Accepts optional `reason` field in request body.
- Can revoke any key regardless of tenant.
- Creates a distinct audit log entry noting this was an emergency action with the reason.

### Entity Changes

`ApiKey.cs` — No structural changes needed. `TenantId` is already `Guid?`. Add a computed property:

```csharp
public bool IsPlatformKey => TenantId == null;
```

### DTOs

Extend `ApiKeyDto` to include tenant info for platform admin views:

```csharp
public sealed record ApiKeyDto(
    Guid Id,
    string Name,
    string KeyPrefix,
    List<string> Scopes,
    DateTime? ExpiresAt,
    DateTime? LastUsedAt,
    bool IsRevoked,
    bool IsExpired,
    bool IsPlatformKey,       // NEW
    Guid? TenantId,           // NEW
    string? TenantName,       // NEW (populated only for platform admin views)
    DateTime CreatedAt,
    Guid? CreatedBy);
```

`CreateApiKeyCommand` — Add optional field:

```csharp
public sealed record CreateApiKeyCommand(
    string Name,
    List<string> Scopes,
    DateTime? ExpiresAt,
    bool IsPlatformKey = false   // NEW: only respected for platform admins
);
```

`RevokeApiKeyCommand` — unchanged (normal revoke, no reason needed).

`EmergencyRevokeApiKeyCommand` — new:

```csharp
public sealed record EmergencyRevokeApiKeyCommand(
    Guid Id,
    string? Reason = null);
```

### Command Handler Changes

**CreateApiKeyCommandHandler:**
- If caller is a tenant user (`currentUserService.TenantId != null`): Always set `TenantId = currentUserService.TenantId`. Ignore `IsPlatformKey` — if it's `true`, reject with error "Tenant users cannot create platform keys."
- If caller is a platform admin (`currentUserService.TenantId == null`) and `IsPlatformKey == true`: Set `TenantId = null`. Require `ApiKeys.CreatePlatform` permission.
- If caller is a platform admin and `IsPlatformKey == false`: Return error 400 "Platform admins must explicitly create platform keys. Set isPlatformKey to true."

**RevokeApiKeyCommandHandler:**
- Load key with `IgnoreQueryFilters()` to see all keys.
- If key is a tenant key (`TenantId != null`):
  - If caller is that tenant's user with `ApiKeys.Delete`: Allow.
  - If caller is platform admin: Return 403 "Use the emergency-revoke endpoint for tenant keys."
  - Otherwise: Return 404.
- If key is a platform key (`TenantId == null`):
  - If caller is platform admin with `ApiKeys.DeletePlatform`: Allow.
  - Otherwise: Return 404 (tenant users cannot see platform keys).

**EmergencyRevokeApiKeyCommandHandler (NEW):**
- Requires `ApiKeys.EmergencyRevoke` permission (checked at controller gate).
- Load key with `IgnoreQueryFilters()`.
- If key not found: Return 404.
- If already revoked: Return error "Key is already revoked."
- Revoke the key.
- Create audit log entry with action `ApiKey.EmergencyRevoked`, including key name, tenant name, and reason.

**UpdateApiKeyCommandHandler:**
- Load key with `IgnoreQueryFilters()`.
- If key is a tenant key:
  - If caller is that tenant's user with `ApiKeys.Update`: Allow.
  - If caller is platform admin: Return 403 "Cannot modify tenant API keys."
  - Otherwise: Return 404.
- If key is a platform key:
  - If caller is platform admin with `ApiKeys.UpdatePlatform`: Allow.
  - Otherwise: Return 404.

**GetApiKeysQueryHandler:**
- Accept `keyType` parameter (string: `"tenant"`, `"platform"`, `"all"`).
- Tenant user: Always filter by `TenantId == currentUser.TenantId`. Ignore `keyType`.
- Platform admin: Filter based on `keyType` parameter.
  - `"platform"` (default): `Where(k => k.TenantId == null)`
  - `"tenant"`: `Where(k => k.TenantId != null)`. Left-join `Tenants` to populate `TenantName`.
  - `"all"`: No filter on TenantId. Join `Tenants` for keys where `TenantId != null`.
- Optional `tenantId` filter: If provided and caller is platform admin, add `Where(k => k.TenantId == tenantId)`.

**GetApiKeyByIdQueryHandler:**
- Load key via query (respects global filter for tenant users).
- Platform admin: Use `IgnoreQueryFilters()` to see all keys. Join `Tenants` to populate `TenantName` if key is a tenant key.
- Map to `ApiKeyDto` including the new `IsPlatformKey`, `TenantId`, `TenantName` fields.

### Scope Validation per Key Type

The `Scopes` field on a key determines what permissions the key grants when used for authentication. Different key types should have different allowed scopes:

- **Tenant keys**: Can only have tenant-level scopes (e.g., `Users.View`, `Files.Upload`, `Roles.View`). Cannot have platform-level scopes like `Tenants.View`, `ApiKeys.ViewPlatform`, etc.
- **Platform keys**: Can have any scope, including cross-tenant scopes. However, scopes like `Files.Upload` on a platform key without an `X-Tenant-Id` header would fail at the storage level (no tenant context for bucket). The handler should validate that platform key scopes make sense for cross-tenant use but this is a soft warning, not a hard block.

For now, both key types share the same scope list but the frontend shows a contextual hint. The `AVAILABLE_SCOPES` list in the frontend should be fetched from the `GET /Permissions` API endpoint rather than hardcoded.

### Frontend Changes

**Permissions constants** — Add new permissions:

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

**API layer** — Update query params to use `keyType` instead of `scope`. Add `isPlatformKey` to create. Add `emergencyRevoke` function. Fetch available scopes from `/Permissions` endpoint instead of hardcoding.

**ApiKeysPage** — Two different layouts based on user context:

**Tenant user view** (user has `ApiKeys.View` but NOT `ApiKeys.ViewPlatform`):
- Same as current: title, create button, table of their keys, pagination.
- No tabs, no tenant column.

**Platform admin view** (user has `ApiKeys.ViewPlatform`):
- Two tabs: "Platform Keys" (default) and "Tenant Keys".
- **Platform Keys tab** (`PlatformKeysTab.tsx`):
  - "Create Platform Key" button (if `ApiKeys.CreatePlatform`).
  - Table: name, prefix, scopes, status, last used, expires, created. Full CRUD actions.
  - Pagination with `Pagination` component.
- **Tenant Keys tab** (`TenantKeysTab.tsx`):
  - Read-only table with additional "Tenant" column.
  - Filter dropdown by tenant. **Note:** This requires the user to also have `Tenants.View` permission. If missing, show all tenant keys without the filter dropdown (the API returns `tenantName` in the DTO regardless).
  - Only action: "Emergency Revoke" button per row (if `ApiKeys.EmergencyRevoke`). Styled as destructive with a confirmation dialog.
  - No create/edit buttons.

**CreateApiKeyDialog** — No changes needed for tenant users. Platform admin dialog is identical but the `isPlatformKey: true` flag is sent automatically. Available scopes are fetched from the API.

**EmergencyRevokeDialog** — New component. Shows a clear warning: "This will revoke a tenant's API key. Use only for security incidents." Requires typing the key name to confirm. Includes an optional "Reason" text field.

**Query key structure** — Update `queryKeys.apiKeys` to include `keyType` in the cache key:

```typescript
apiKeys: {
  all: ['apiKeys'] as const,
  lists: () => [...queryKeys.apiKeys.all, 'list'] as const,
  list: (filters?: object) => [...queryKeys.apiKeys.lists(), filters ?? {}] as const,
  // keyType is part of filters, so switching tabs hits different cache entries
  details: () => [...queryKeys.apiKeys.all, 'detail'] as const,
  detail: (id: string) => [...queryKeys.apiKeys.details(), id] as const,
},
```

**Translations** — Add keys for: "Platform Keys", "Tenant Keys", "Emergency Revoke", "This will revoke a tenant's API key. Use only for security incidents.", "Type the key name to confirm", "Reason (optional)", tenant column header. All three languages (en, ar, ku).

### Audit Trail

All API key operations generate audit log entries via the existing `AuditableEntityInterceptor` for entity changes, plus explicit audit log creation for emergency actions:

| Action | Actor | Mechanism | Details |
|--------|-------|-----------|---------|
| `ApiKey.Created` | User | Interceptor (entity Created) | Key name, scopes, isPlatformKey, tenantId |
| `ApiKey.Updated` | User | Interceptor (entity Modified) | Changed fields |
| `ApiKey.Revoked` | User | Interceptor (entity Modified, IsRevoked=true) | Key name |
| `ApiKey.EmergencyRevoked` | Platform Admin | Explicit audit log in handler | Key name, tenant name, reason, flagged as emergency |

The `EmergencyRevokeApiKeyCommandHandler` creates the audit log entry directly (not just via the interceptor) to capture the emergency flag and reason.

### Migration

No schema migration needed — `TenantId` is already `Guid?` and `IsPlatformKey` is a C# computed property.

The only migration-related change: the `DataSeeder` will seed the new permissions into the `Permissions` table and assign them to the `SuperAdmin` and `Admin` roles on next startup.

### Security Considerations

- Tenant keys are locked to their tenant at creation. The `TenantId` cannot be changed after creation.
- Platform keys bypass tenant scoping. They should be treated as high-privilege and audited carefully.
- The `X-Tenant-Id` header on platform key requests is validated against the `Tenants` table. The tenant must exist **and be Active** (not Suspended or Deactivated). This prevents platform keys from accessing data in disabled tenants.
- Emergency revoke is a destructive action — the confirmation dialog requires typing the key name to confirm. A dedicated rate limit of 5 requests/minute should be applied to the emergency-revoke endpoint.
- Tenant users requesting platform keys or other tenants' keys by ID receive 404 (not 403) to prevent key enumeration.
- Rate limiting applies to all API key endpoints. The emergency-revoke endpoint gets a tighter limit (5/min vs 10/sec general).

## Files to Change

**Backend (modify):**
1. `Starter.Shared/Constants/Permissions.cs` — Add platform + emergency permissions with descriptions
2. `Starter.Shared/Constants/Roles.cs` — Assign new permissions to SuperAdmin (all) and Admin (tenant-level only)
3. `Starter.Api/Controllers/ApiKeysController.cs` — Composite authorize policies, `keyType` param, emergency-revoke endpoint
4. `Starter.Application/Features/ApiKeys/DTOs/ApiKeyDto.cs` — Add `IsPlatformKey`, `TenantId`, `TenantName`
5. `Starter.Application/Features/ApiKeys/DTOs/ApiKeyMapper.cs` — Map new fields
6. `Starter.Application/Features/ApiKeys/Commands/CreateApiKey/CreateApiKeyCommand.cs` — Add `IsPlatformKey`
7. `Starter.Application/Features/ApiKeys/Commands/CreateApiKey/CreateApiKeyCommandHandler.cs` — Dual-scope logic with permission checks
8. `Starter.Application/Features/ApiKeys/Commands/RevokeApiKey/RevokeApiKeyCommandHandler.cs` — Ownership checks, reject tenant keys for platform admin (direct to emergency-revoke)
9. `Starter.Application/Features/ApiKeys/Commands/UpdateApiKey/UpdateApiKeyCommandHandler.cs` — Ownership checks per key type
10. `Starter.Application/Features/ApiKeys/Queries/GetApiKeys/GetApiKeysQuery.cs` — Add `KeyType` and `TenantId` filter params
11. `Starter.Application/Features/ApiKeys/Queries/GetApiKeys/GetApiKeysQueryHandler.cs` — KeyType filtering + tenant join for TenantName
12. `Starter.Application/Features/ApiKeys/Queries/GetApiKeyById/GetApiKeyByIdQueryHandler.cs` — Platform admin: IgnoreQueryFilters + TenantName join
13. `Starter.Infrastructure.Identity/Authentication/ApiKeyAuthenticationHandler.cs` — X-Tenant-Id header handling, tenant validation (exists + active), `is_platform_key` claim
14. `Starter.Domain/ApiKeys/Entities/ApiKey.cs` — Add `IsPlatformKey` computed property

**Backend (new):**
15. `Starter.Application/Features/ApiKeys/Commands/EmergencyRevokeApiKey/EmergencyRevokeApiKeyCommand.cs`
16. `Starter.Application/Features/ApiKeys/Commands/EmergencyRevokeApiKey/EmergencyRevokeApiKeyCommandHandler.cs`

**Frontend (modify):**
17. `constants/permissions.ts` — Add platform + emergency permissions
18. `config/api.config.ts` — Add emergency revoke endpoint
19. `features/api-keys/api/api-keys.api.ts` — Add keyType param, isPlatformKey, emergency revoke, fetch scopes from API
20. `features/api-keys/api/api-keys.queries.ts` — New hooks for platform queries, emergency revoke, scopes
21. `features/api-keys/pages/ApiKeysPage.tsx` — Dual view (tabs for platform admin, simple for tenant)
22. `features/api-keys/components/CreateApiKeyDialog.tsx` — isPlatformKey flag, dynamic scopes from API
23. `i18n/locales/{en,ar,ku}/translation.json` — New translation keys
24. `lib/query/keys.ts` — keyType included in cache key via filters object

**Frontend (new):**
25. `features/api-keys/components/TenantKeysTab.tsx` — Read-only table with tenant column + emergency revoke
26. `features/api-keys/components/PlatformKeysTab.tsx` — Full CRUD table for platform keys
27. `features/api-keys/components/EmergencyRevokeDialog.tsx` — Confirmation dialog with key name typing + reason field
