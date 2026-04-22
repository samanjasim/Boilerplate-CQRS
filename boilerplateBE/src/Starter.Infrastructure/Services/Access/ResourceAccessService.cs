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
        string resourceType,
        Guid resourceId,
        GrantSubjectType subjectType,
        Guid subjectId,
        AccessLevel level,
        CancellationToken ct)
    {
        if (!ResourceTypes.IsKnown(resourceType))
            throw new DomainException(AccessErrors.ResourceNotFound.Description, AccessErrors.ResourceNotFound.Code);

        var tenantId = currentUser.TenantId;

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
                tenantId,
                resourceType,
                resourceId,
                subjectType,
                subjectId,
                level,
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
        string resourceType,
        Guid resourceId,
        CancellationToken ct)
    {
        var tenantId = currentUser.TenantId;
        var rows = await db.ResourceGrants
            .Where(g => g.TenantId == tenantId && g.ResourceType == resourceType && g.ResourceId == resourceId)
            .AsNoTracking()
            .ToListAsync(ct);

        var userIds = rows
            .Where(r => r.SubjectType == GrantSubjectType.User)
            .Select(r => r.SubjectId)
            .Distinct()
            .ToList();

        var roleIds = rows
            .Where(r => r.SubjectType == GrantSubjectType.Role)
            .Select(r => r.SubjectId)
            .Distinct()
            .ToList();

        var users = await db.Users
            .Where(u => userIds.Contains(u.Id))
            .AsNoTracking()
            .Select(u => new { u.Id, Name = (u.FullName.FirstName + " " + u.FullName.LastName).Trim() })
            .ToDictionaryAsync(u => u.Id, u => u.Name, ct);

        var roles = await db.Roles
            .Where(r => roleIds.Contains(r.Id))
            .AsNoTracking()
            .Select(r => new { r.Id, r.Name })
            .ToDictionaryAsync(r => r.Id, r => r.Name, ct);

        string? LookupName(GrantSubjectType st, Guid id) =>
            st == GrantSubjectType.User ? users.GetValueOrDefault(id) : roles.GetValueOrDefault(id);

        return rows.Select(g => new ResourceGrantDto(
            g.Id,
            g.ResourceType,
            g.ResourceId,
            g.SubjectType,
            g.SubjectId,
            LookupName(g.SubjectType, g.SubjectId),
            g.Level,
            g.GrantedByUserId,
            g.GrantedAt)).ToList();
    }

    public async Task<bool> CanAccessAsync(
        ICurrentUserService user,
        string resourceType,
        Guid resourceId,
        AccessLevel minLevel,
        CancellationToken ct)
    {
        if (resourceType == ResourceTypes.File && user.HasPermission(Permissions.Files.Manage))
            return true;

        if (resourceType == ResourceTypes.AiAssistant &&
            (user.IsInRole(Roles.Admin) || user.IsInRole(Roles.SuperAdmin)))
            return true;

        if (user.UserId is not Guid uid) return false;
        var tenantId = user.TenantId;

        var userGrant = await db.ResourceGrants.AsNoTracking().FirstOrDefaultAsync(
            g => g.TenantId == tenantId
                 && g.ResourceType == resourceType
                 && g.ResourceId == resourceId
                 && g.SubjectType == GrantSubjectType.User
                 && g.SubjectId == uid,
            ct);

        if (userGrant is not null && userGrant.Level >= minLevel) return true;

        var roleIds = await db.UserRoles
            .Where(ur => ur.UserId == uid)
            .Select(ur => ur.RoleId)
            .ToListAsync(ct);

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
        ICurrentUserService user,
        string resourceType,
        CancellationToken ct)
    {
        if (resourceType == ResourceTypes.File && user.HasPermission(Permissions.Files.Manage))
            return new AccessResolution(true, Array.Empty<Guid>());

        if (resourceType == ResourceTypes.AiAssistant &&
            (user.IsInRole(Roles.Admin) || user.IsInRole(Roles.SuperAdmin)))
            return new AccessResolution(true, Array.Empty<Guid>());

        if (user.UserId is not Guid uid || user.TenantId is not Guid tid)
            return new AccessResolution(false, Array.Empty<Guid>());

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

        var roleIds = await db.UserRoles
            .Where(ur => ur.UserId == uid)
            .Select(ur => ur.RoleId)
            .ToListAsync(ct);

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

        var userIds = await db.UserRoles
            .Where(ur => ur.RoleId == roleId)
            .Select(ur => ur.UserId)
            .ToListAsync(ct);

        foreach (var uid in userIds)
            await cache.RemoveAsync(AclCacheKeys.UserVersion(tid, uid), ct);
    }

    private Task InvalidateForSubjectAsync(GrantSubjectType st, Guid sid, CancellationToken ct) =>
        st == GrantSubjectType.User ? InvalidateUserAsync(sid, ct) : InvalidateRoleMembersAsync(sid, ct);
}
