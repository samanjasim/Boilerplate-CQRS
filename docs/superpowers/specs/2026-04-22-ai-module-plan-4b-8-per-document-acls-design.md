# Plan 4b-8 — Per-document ACLs & unified file/access primitive

**Status:** Design (brainstormed) — awaiting user review before plan writing.
**Owner:** Saman
**Date:** 2026-04-22
**Preceded by:** Plan 4b-7 (MMR diversification, shipped 2026-04-22)
**Succeeded by:** Plan 4b-9 (RAG evaluation harness) — last item in Plan 4 family.

---

## 1. Goal

Per-resource access control for AI documents and assistants, built on a **generic `ResourceGrant` primitive** shared across the solution. Unifies AI files under the existing `FileMetadata` hub so storage, ACLs, and audit are tracked in one place. Enforced at two layers: assistant-level gate (pre-RAG) and retrieval-level filter (inside RAG) with Qdrant payload push-down and Redis-backed caching.

Non-goals: anonymous link sharing with expiry, external-tenant sharing, per-operation bit flags, long-term reviewer workflows. Addressed later if needed.

## 2. Non-negotiable constraints

- **No migrations shipped** with the boilerplate — rename-apps regenerate their own (per standing directive).
- **Never commit Co-Authored-By or Claude mentions** (per standing directive).
- **Core vs. Module vs. Shared** layering — this spec adopts the core-feature pattern (see §4); documents the "way" in `CLAUDE.md`.
- **Fail-closed for ACL resolution** — timeout/error never falls through to "no filter" (which would leak documents). Empty context + degraded-stage surface.
- **No live data to migrate** — schema changes drop/replace without backfill SQL.

## 3. Decisions locked in brainstorm

| # | Decision | Rationale |
|---|---|---|
| D1 | Full visibility model `Private / TenantWide / Public` — no "Restricted" enum value (derived at UI level) | Avoids redundant state; grants compose with visibility as a floor |
| D2 | Grant levels `Viewer / Editor / Manager` (named tiers, not bit flags) | Familiar (Drive/SharePoint), UI-clean, covers real cases |
| D3 | Owner distinct from grants; stored on the resource (`UploadedBy` / `CreatedByUserId`) | Delete + visibility-change + ownership-transfer never grantable |
| D4 | Two-layer defence: assistant-level ACL first, then retrieval filter | Fast 403 on unauthorized assistants; deep-layer enforcement for doc ACLs |
| D5 | Assistants have `AccessMode = CallerPrincipal \| AssistantPrincipal`, default `CallerPrincipal`, `AssistantPrincipal` admin-gated | Default safe; opt-in "knowledge broker" pattern explicit |
| D6 | Files module is the hub — AI uploads route through `FileService.CreateManagedFileAsync`; `AiDocument.FileId` replaces raw `FileRef` | One storage dashboard, one ACL path, one audit trail |
| D7 | Shared ACL primitive placed in core layers (`Starter.Domain/Common/Access`, `Starter.Application/Common/Access`, `Starter.Infrastructure/Services/Access`), registered on `ApplicationDbContext` | Matches `FileMetadata`/`Notification`/`AuditLog` pattern; avoids cross-context DbContext issues |
| D8 | Qdrant payload enriched with `file_id / visibility / uploaded_by_user_id`; grants **not** stamped on payload | Payload-indexed filter for stable fields; small IN-list only for volatile explicit grants |
| D9 | `IResourceAccessService.ResolveAccessibleResourcesAsync` returns only admin-flag + explicit grants — not the whole accessible set | Tiny indexed scan; visibility/ownership handled at the search layer via payload |
| D10 | Redis cache keyed by per-user version counter (`aclv:user:{id}`), TTL 60s | Targeted invalidation on grant change; p95 sub-millisecond on warm cache |
| D11 | Delete cascades fan out: resource → grants, user → grants, role → grants, tenant → grants — each wired in the existing deletion handlers | No orphan grants; indexed bulk deletes |

## 4. Core vs. Module vs. Shared — documented

Adds a block to `CLAUDE.md` under **Architecture Overview**:

> **Core feature** — required by other features or cross-cutting (access control, auth, audit, notifications, files). Lives in `Starter.Domain/Starter.Application/Starter.Infrastructure`, uses `ApplicationDbContext`.
>
> **Module** (`src/modules/Starter.Module.*`) — optional vertical with its own bounded context, DbContext, migrations, and DI module. Modules may depend on core; core must not depend on a module.
>
> **Shared** (`Starter.Shared`) — constants, permissions, error codes, enums with no behavior. No EF entities, no services.

ACL is a **core feature** (required by Files). AI module consumes it via injected `IResourceAccessService` — same pattern as `ICurrentUserService`.

## 5. Components

### 5.1 Shared ACL primitive (core feature)

```
Starter.Domain/Common/Access/
  ├── Entities/ResourceGrant.cs          (aggregate root)
  ├── Enums/ResourceVisibility.cs        (Private | TenantWide | Public)
  ├── Enums/GrantSubjectType.cs          (User | Role)
  ├── Enums/AccessLevel.cs               (Viewer | Editor | Manager)
  └── IShareable.cs                      (marker: Id, Visibility)

Starter.Application/Common/Access/
  ├── IResourceAccessService.cs
  ├── Contracts/AccessResolution.cs      (record: IsAdminBypass, ExplicitGrantedResourceIds)
  ├── Contracts/ResourceTypes.cs         (constants: File, AiAssistant; registry with MaxVisibility)
  ├── DTOs/ResourceGrantDto.cs
  └── Errors/AccessErrors.cs

Starter.Application/Features/Access/
  ├── Commands/GrantResourceAccess/…
  ├── Commands/RevokeResourceAccess/…
  ├── Commands/SetResourceVisibility/…
  ├── Commands/TransferResourceOwnership/…
  └── Queries/ListResourceGrants/…

Starter.Infrastructure/
  ├── Services/Access/ResourceAccessService.cs
  ├── Services/Access/AclCacheKeys.cs
  └── Persistence/Configurations/ResourceGrantConfiguration.cs

ApplicationDbContext  →  DbSet<ResourceGrant> ResourceGrants
```

**`ResourceGrant` schema:**

```
Id (Guid, PK)
TenantId (Guid?, query-filtered)
ResourceType (string, e.g. "File", "AiAssistant")
ResourceId (Guid)
SubjectType (GrantSubjectType)
SubjectId (Guid)
Level (AccessLevel)
GrantedByUserId (Guid)
GrantedAt (DateTime)

Unique index:  (TenantId, ResourceType, ResourceId, SubjectType, SubjectId)
Lookup index:  (TenantId, ResourceType, ResourceId)          -- list grants for a resource
Lookup index:  (TenantId, SubjectType, SubjectId, ResourceType) -- subject's accessible resources
```

**`IResourceAccessService` contract:**

```csharp
Task<Guid> GrantAsync(string resourceType, Guid resourceId,
    GrantSubjectType subjectType, Guid subjectId,
    AccessLevel level, CancellationToken ct);
Task RevokeAsync(Guid grantId, CancellationToken ct);
Task RevokeAllForResourceAsync(string resourceType, Guid resourceId, CancellationToken ct);
Task<IReadOnlyList<ResourceGrantDto>> ListGrantsAsync(
    string resourceType, Guid resourceId, CancellationToken ct);
Task<bool> CanAccessAsync(
    ICurrentUser user, string resourceType, Guid resourceId,
    AccessLevel minLevel, CancellationToken ct);
Task<AccessResolution> ResolveAccessibleResourcesAsync(
    ICurrentUser user, string resourceType, CancellationToken ct);
```

`AccessResolution = (bool IsAdminBypass, IReadOnlyList<Guid> ExplicitGrantedResourceIds)`.

### 5.2 `FileMetadata` changes

- Drop `IsPublic`.
- Add `Visibility` (`ResourceVisibility`, not null, default `Private`).
- Implements `IShareable`.

Handler behavior changes:
- `GetFileUrl` — uses `CanAccessAsync` with `Viewer` minimum.
- `GetFiles` list — filter is `Visibility == TenantWide || UploadedBy == user.Id || id IN accessibleIds`.
- `UpdateFileMetadata` — requires `Editor+`.
- `DeleteFile` — owner or `Files.Manage`.
- New: `SetVisibility` (owner or admin; `Public` admin-only), `TransferOwnership` (owner or admin).

New endpoints:
- `GET    /api/v1/files/{id}/grants`
- `POST   /api/v1/files/{id}/grants`
- `DELETE /api/v1/files/{id}/grants/{grantId}`
- `PUT    /api/v1/files/{id}/visibility`
- `POST   /api/v1/files/{id}/transfer-ownership`
- `GET    /api/v1/files/storage-summary` (aggregate query; tenant-scoped, admin sees all)

Permissions added: `Files.ShareOwn` (default: all authenticated tenant users). `Files.Manage` bypass retained.

### 5.3 Files as central hub

- `AiDocument.FileRef` → removed. `AiDocument.FileId` (Guid, FK → `FileMetadata.Id`) added.
- `IFileService` gains:
  - `Task<FileMetadata> CreateManagedFileAsync(ManagedFileUpload upload, CancellationToken ct)`
  - `Task DeleteManagedFileAsync(Guid fileId, CancellationToken ct)`
  - `Task<FileDownloadResult?> ResolveDownloadAsync(Guid fileId, CancellationToken ct)`
- `UploadDocumentCommandHandler` flow rewritten: validate → `fileService.CreateManagedFileAsync({ Visibility=Private, Category=AiDocument, EntityType=AiDocument })` → create `AiDocument` with the returned `FileMetadata.Id` → update `FileMetadata.EntityId` to `AiDocument.Id` → publish event.
- Storage key format centralized in `FileService`: `{tenantId}/{category}/{yyyy}/{fileId}/{safe-name}`.
- AI `DocumentsController` no longer calls MinIO directly.

### 5.4 `AiAssistant` changes

- Add `Visibility` (`ResourceVisibility`, default `Private`).
- Add `AccessMode` (`AssistantAccessMode`, default `CallerPrincipal`).
- Entity methods: `SetVisibility`, `SetAccessMode` — each validates and raises a domain event.
- Implements `IShareable`.

Controller gate changes on `AiAssistantsController`:

| Endpoint | Policy | Additional ACL |
|---|---|---|
| `GET /ai/assistants` | `Ai.Chat` | list filtered via `ResolveAccessibleResourcesAsync` + `Visibility=TenantWide` + `CreatedBy=me` |
| `GET /ai/assistants/{id}` | `Ai.Chat` | `CanAccessAsync` |
| `POST /ai/assistants` | `Ai.ManageAssistants` | unchanged |
| `PUT /ai/assistants/{id}` | `Ai.ManageAssistants` | unchanged (admin/owner) |
| `DELETE /ai/assistants/{id}` | `Ai.ManageAssistants` | unchanged (admin/owner) |
| `GET    /ai/assistants/{id}/grants` | `Ai.Chat` | owner/admin |
| `POST   /ai/assistants/{id}/grants` | `Ai.Chat` | owner/admin |
| `DELETE /ai/assistants/{id}/grants/{grantId}` | `Ai.Chat` | owner/admin |
| `PUT /ai/assistants/{id}/visibility` | `Ai.ManageAssistants` | owner/admin |
| `PUT /ai/assistants/{id}/access-mode` | `Ai.ManageAssistants` | **admin only** (stricter) |
| `POST /ai/assistants/{id}/transfer-ownership` | `Ai.ManageAssistants` | owner/admin |

Chat endpoint pre-check:

```csharp
if (!await _access.CanAccessAsync(user, ResourceTypes.AiAssistant, assistantId, AccessLevel.Viewer, ct))
    return Forbid();
```

Runs before RAG pipeline. Fast 403 on unauthorized.

Seed update: `SeedSampleAssistant: true` sets `Visibility=TenantWide`, `AccessMode=CallerPrincipal`.

### 5.5 Retrieval filter — second-line gate (`RagRetrievalService`)

**Pipeline position:** new stage `acl-resolve` between query planning and parallel vector/keyword search.

**Resolution flow:**

```
Layer 1 (Qdrant payload, stamped at upsert):
  tenant_id, document_id, file_id, visibility, uploaded_by_user_id
  → most cases matched via indexed payload fields

Layer 2 (DB query, only for explicit grants):
  SELECT resource_id FROM ResourceGrants
   WHERE resource_type='File' AND tenant_id=:tid
     AND ((subject_type='User' AND subject_id=:uid)
          OR (subject_type='Role' AND subject_id = ANY(:roleIds)))
  → typical 1-3 ms, indexed
  → admin-bypass short-circuit when caller has Files.Manage

Layer 3 (Redis cache):
  Key: acl:user:{tenantId}:{userId}:v{userVersion}
  TTL 60s; version bump on grant/role-membership change
  → warm hit ~0.3 ms
```

**Effective Qdrant filter:**

```
tenant_id = :tid AND (
  visibility = TenantWide
  OR uploaded_by_user_id = :userId
  OR file_id IN :explicitGrantedFileIds
)
[AND document_id IN :KnowledgeBaseDocIds  // when RagScope=SelectedDocuments]
```

Admin bypass or `AccessMode=AssistantPrincipal` → ACL clause dropped; tenant-scoped filter only.

**Postgres keyword search filter** — equivalent SQL. Denormalize `file_id`, `visibility`, `uploaded_by_user_id` onto `AiDocumentChunk` at ingest; update on visibility-change events. No JOINs in the hot path.

**Filter composition table:**

| AccessMode | RagScope | Effective filter |
|---|---|---|
| `CallerPrincipal` | `AllTenantDocuments` | `visibility=TenantWide OR uploaded_by=:u OR file_id IN :grants` |
| `CallerPrincipal` | `SelectedDocuments` | above AND `document_id IN :KnowledgeBaseDocIds` |
| `AssistantPrincipal` | `AllTenantDocuments` | tenant-scoped only |
| `AssistantPrincipal` | `SelectedDocuments` | `document_id IN :KnowledgeBaseDocIds` |
| admin bypass | any | tenant-scoped only |

**Fail-closed behavior:**
- Stage wrapped in existing `WithTimeoutAsync` → emits `StageDuration`/`StageOutcome`.
- Timeout/exception: if Redis has a non-expired cached value → serve stale; else **empty context** + `DegradedStages` contains `acl-resolve`.
- Never falls through to "no filter".

**Settings added to `AiRagSettings`:**

```json
"StageTimeoutAclResolveMs": 1500,
"AclCacheTtlSeconds": 60
```

**Invalidation:**
- Grant/revoke to a User → bump `aclv:user:{uid}`.
- Grant/revoke to a Role → async handler looks up current role members, bumps each user's version.
- User role-change → bump that user's version.
- Visibility change → **no** cache invalidation (cache holds explicit grants only).
- Payload update for visibility change → async handler enumerates chunks via Qdrant `scroll`, batches `set_payload` in 500s.

### 5.6 Ownership transfer

Endpoint pattern: `POST /{resource}/{id}/transfer-ownership`.

Rules:
1. Caller is current owner or admin.
2. New owner is active + same tenant.
3. Old owner auto-demoted to `Manager` grant (UPSERT).
4. Single DB transaction.
5. Audit `ResourceOwnershipTransferred`.

### 5.7 Cascades

| Delete target | Cascade |
|---|---|
| `FileMetadata` | `RevokeAllForResourceAsync("File", fileId)` + `MinIO.DeleteAsync(storageKey)` |
| `AiDocument` | chunks + Qdrant points + `DeleteManagedFileAsync` (which cascades grants via FileMetadata) |
| `AiAssistant` | `RevokeAllForResourceAsync("AiAssistant", assistantId)` + session cleanup |
| `User` (hard delete) | `DELETE FROM ResourceGrants WHERE SubjectType='User' AND SubjectId=:uid` |
| `Role` | `DELETE FROM ResourceGrants WHERE SubjectType='Role' AND SubjectId=:rid` |
| `Tenant` | `DELETE FROM ResourceGrants WHERE TenantId=:tid` |

### 5.8 Audit

All events use existing `IAuditLogService`:

- `ResourceGrantCreated` / `Updated` / `Revoked`
- `ResourceVisibilityChanged`
- `ResourceVisibilityMadePublic` (distinct — compliance query target)
- `ResourceOwnershipTransferred`
- `AssistantAccessModeChanged`

## 6. Error codes

All returned via existing `Result<T>` / `HandleResult` pipeline.

| Code | HTTP | When |
|---|---|---|
| `Access.ResourceNotFound` | 404 | Resource doesn't exist or caller can't see it |
| `Access.GrantNotFound` | 404 | Revoke of non-existent grant |
| `Access.SubjectNotFound` | 400 | Grant target doesn't exist |
| `Access.SubjectInactive` | 400 | Grant target is suspended/deactivated |
| `Access.CrossTenantGrantBlocked` | 403 | Grant target is in a different tenant |
| `Access.SelfGrantBlocked` | 409 | Owner attempting to grant to themselves |
| `Access.InsufficientLevelToGrant` | 403 | Editor trying to grant Manager, etc. |
| `Access.VisibilityNotAllowedForResourceType` | 400 | `Public` on `AiAssistant`, etc. |
| `Access.OnlyOwnerCanPerform` | 403 | Non-owner trying ownership-only operation |
| `Access.OwnershipTargetNotInTenant` | 400 | Transfer target is cross-tenant |
| `Access.OwnershipTargetInactive` | 400 | Transfer target is suspended |

## 7. Testing strategy

### 7.1 Unit (`Starter.Application.Tests`, `Starter.Module.AI.Tests`)

- `ResourceVisibility` enum / serialization.
- `EffectiveAccessCalculator` — table-driven: (visibility, isOwner, userGrant, roleGrant) → expected level.
- `AiAssistant.SetVisibility / SetAccessMode` — raises event, rejects invalid.
- `FileMetadata.SetVisibility` — rejects `Public` above resource-type ceiling.
- `ResourceAccessService` idempotency (upsert), all safety-rule rejections.
- Cascade delete behavior (EF in-memory).
- Admin-bypass short-circuit.

### 7.2 Integration (`Starter.Api.Tests/Ai/Retrieval/AclIntegrationTests.cs` — new file, pattern: `MmrIntegrationTests`)

New fake `FakeResourceAccessService` — in-memory grants, toggleable admin bypass, call counters.

- `CallerPrincipal + AllTenantDocuments` — no grants: only `TenantWide` retrieved.
- `CallerPrincipal + AllTenantDocuments` — with grants: granted private + tenant-wide retrieved; others' private excluded.
- `CallerPrincipal + SelectedDocuments` — filter = accessible ∩ `KnowledgeBaseDocIds`.
- `AssistantPrincipal + AllTenantDocuments` — caller ACL ignored.
- `AssistantPrincipal + SelectedDocuments` — `KnowledgeBaseDocIds` used directly.
- Admin bypass — null filter.
- Empty accessible set — pipeline short-circuits (`Children=[]`) before search called (verified via mock counters).
- Degradation — resolver throws → `DegradedStages` contains `acl-resolve`, context empty.
- Cache hit — second call within TTL uses cached value (resolver call count = 1).
- Version-bump invalidation — publish grant-change event → next call hits resolver.

### 7.3 API / security regression (`Starter.Api.Tests`)

Each of these must pass — security contract:

- Tenant A user chatting with Tenant B assistant → 404.
- User without assistant grant → 403 before any vector/keyword call (verified via fake call counts).
- `Private` file download without grant → 403.
- `PUT /files/{id}/visibility Public` by non-admin owner → 403.
- `PUT /ai/assistants/{id}/access-mode AssistantPrincipal` by owner non-admin → 403.
- Grant/revoke endpoints write expected audit rows.
- `Public` on `AiAssistant` → 400 `Access.VisibilityNotAllowedForResourceType`.

### 7.4 Performance guards

- `acl-resolve` p95 ≤ 5 ms on warm cache.
- `acl-resolve` p95 ≤ 25 ms on cold resolver (EF in-memory).
- Visibility-change payload update: 1000-chunk doc ≤ 2 Qdrant round trips (batched `set_payload`).

## 8. Implementation packaging

Single Plan 4b-8 with phased internal tasks — no broken intermediate states. Suggested task ordering (confirmed in plan-writing step):

1. Shared ACL primitive (domain + application + infrastructure + DI).
2. `ResourceGrant` schema + EF config + `ApplicationDbContext` wiring.
3. `ResourceAccessService` implementation (all methods, tests).
4. `FileMetadata.Visibility` + drop `IsPublic` + updated handlers + grant endpoints.
5. `FileService.CreateManagedFileAsync` / `DeleteManagedFileAsync` / `ResolveDownloadAsync`.
6. AI upload pipeline — route through `FileService`; introduce `AiDocument.FileId`; drop `FileRef`.
7. Qdrant payload enrichment (`file_id`, `visibility`, `uploaded_by_user_id`) at upsert.
8. Denormalize ACL fields on `AiDocumentChunk`; keyword-search filter update.
9. Visibility-change payload-update handler (async, batched).
10. `AiAssistant` visibility + access mode + endpoints + seed update + chat-endpoint gate.
11. `acl-resolve` stage in `RagRetrievalService` with cache + fail-closed.
12. Cascade handlers for User / Role / Tenant / Resource deletions.
13. Ownership-transfer endpoints.
14. Audit events (all listed).
15. Storage-summary endpoint.
16. Unit tests for all layers (§7.1).
17. RAG integration tests (§7.2).
18. API / security regression tests (§7.3).
19. Performance-guard tests (§7.4).
20. `CLAUDE.md` update with Core/Module/Shared layering block.

## 9. Risks & mitigations

| Risk | Mitigation |
|---|---|
| ACL resolution becomes a single-point-of-failure on every chat turn | Redis cache + targeted invalidation keeps warm p95 at ~0.3ms; fail-closed keeps security intact |
| Payload-update batch blocks UI on visibility change | Async handler via MediatR domain event; UI returns immediately; 60s staleness documented |
| Grant on Role affects many users, thunderous cache invalidation | Bump per-user versions in a batched async job; Redis handles the key-churn |
| `AssistantPrincipal` mode silently leaks documents to unauthorized chatters | Admin-only flag + UI warning + distinct audit event + still capped by `RagScope` |
| Public file accidentally exposed | `Public` setting requires admin (`Files.Manage`); separate `FileMadePublic` audit row for compliance |
| Storage-key scheme change breaks existing references | No live data; all fresh storage keys under new scheme |
| Cross-context queries (ApplicationDbContext ↔ AiDbContext) introduce latency | Qdrant payload push-down + denormalized chunk columns remove JOIN in hot path |

## 10. Out of scope (deferred / YAGNI)

- Anonymous signed-link sharing with expiry.
- External-tenant sharing.
- Per-operation bit flags.
- Link-based sharing UI.
- Grant-expiry (`ExpiresAt`).
- Admin force-purge endpoint (`POST /admin/access/purge-cache/{userId}`) — usable future enhancement.
- Dedup reference counting on `FileMetadata` (only matters if content-hash dedup is introduced).
- Audit orphan-grant diagnostic job (optional reliability monitoring).

## 11. Acceptance criteria

- Plan 4b-8 implementation plan exists and is executable.
- All §7 test categories pass.
- `CLAUDE.md` contains the Core/Module/Shared block.
- A user with no grants + no tenant-wide docs sees `Children=[]` on chat (integration test).
- A user with grants on specific files sees only those + tenant-wide in retrieval.
- Assistant-level 403 fires before RAG runs (verified via fake call counts).
- `acl-resolve` stage emits metrics and surfaces in `DegradedStages` on failure (fail-closed proven).
- AI upload creates a `FileMetadata` row with `EntityType="AiDocument"` and `Visibility=Private`.
- Storage summary endpoint reports per-category + per-entity-type bytes.
- Ownership transfer demotes old owner to `Manager` grant.

## 12. Post-implementation verification (post-feature-testing skill)

Standard rename-app flow:

1. `scripts/rename.ps1 -Name "_testAcl"` → `_testAcl/` on ports 5100/3100.
2. Database reset; fresh seed with a default `TenantWide` assistant.
3. Playwright scenarios:
   - Upload a doc as User A — other users can't see it.
   - Grant User B `Viewer` — B sees it.
   - Change visibility to `TenantWide` — everyone sees it.
   - Revoke — B loses access within 60s or on version bump.
   - Chat with a `Private` assistant — 403 for non-grantees.
   - Admin sets `AccessMode=AssistantPrincipal` on a curated-knowledge assistant — non-admin chatters see curated docs regardless of doc ACL.
   - Transfer ownership — original owner demoted to Manager, new owner has full rights.
4. Regression tests on Users/Roles/Files untouched surfaces.
