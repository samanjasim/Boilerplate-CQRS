using Microsoft.EntityFrameworkCore;
using Starter.Application.Common.Access;
using Starter.Application.Common.Access.Contracts;
using Starter.Application.Common.Interfaces;
using Starter.Domain.Common.Access.Enums;
using Starter.Domain.Common.Access.Errors;
using Starter.Domain.Identity.Entities;
using Starter.Domain.Identity.Enums;
using Starter.Shared.Constants;
using Starter.Shared.Results;

namespace Starter.Infrastructure.Services.Access;

public sealed class ResourceOwnershipProbe(
    IApplicationDbContext db,
    ICurrentUserService currentUser,
    IEnumerable<IResourceOwnershipHandler> handlers) : IResourceOwnershipProbe
{
    private readonly Dictionary<string, IResourceOwnershipHandler> _handlers =
        handlers.ToDictionary(h => h.ResourceType);

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

        var role = await db.Roles.FirstOrDefaultAsync(r => r.Id == subjectId, ct);
        if (role is null) return Result.Failure(AccessErrors.SubjectNotFound);
        if (role.TenantId != tenantId) return Result.Failure(AccessErrors.CrossTenantGrantBlocked);
        return Result.Success();
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
