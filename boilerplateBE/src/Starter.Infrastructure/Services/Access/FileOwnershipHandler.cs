using Microsoft.EntityFrameworkCore;
using Starter.Application.Common.Access;
using Starter.Application.Common.Access.Contracts;
using Starter.Application.Common.Interfaces;
using Starter.Domain.Common;
using Starter.Domain.Common.Access.Enums;
using Starter.Domain.Common.Access.Errors;
using Starter.Domain.Identity.Entities;
using Starter.Domain.Identity.Enums;
using Starter.Shared.Results;

namespace Starter.Infrastructure.Services.Access;

public sealed class FileOwnershipHandler(
    IApplicationDbContext db,
    IResourceAccessService access) : IResourceOwnershipHandler
{
    public string ResourceType => ResourceTypes.File;

    public async Task<Result<Guid>> GetOwnerAsync(Guid resourceId, CancellationToken ct)
    {
        var file = await db.Set<FileMetadata>().FirstOrDefaultAsync(f => f.Id == resourceId, ct);
        return file is null
            ? Result.Failure<Guid>(AccessErrors.ResourceNotFound)
            : Result.Success(file.UploadedBy);
    }

    public async Task<Result<string>> GetDisplayNameAsync(Guid resourceId, CancellationToken ct)
    {
        var file = await db.Set<FileMetadata>().FirstOrDefaultAsync(f => f.Id == resourceId, ct);
        return file is null
            ? Result.Failure<string>(AccessErrors.ResourceNotFound)
            : Result.Success(file.FileName);
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

        await access.GrantAsync(
            ResourceTypes.File,
            resourceId,
            GrantSubjectType.User,
            oldOwner,
            AccessLevel.Manager,
            ct);

        return Result.Success();
    }
}
