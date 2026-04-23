using Microsoft.EntityFrameworkCore;
using Starter.Application.Common.Access;
using Starter.Application.Common.Access.Contracts;
using Starter.Application.Common.Interfaces;
using Starter.Domain.Common.Access.Enums;
using Starter.Domain.Common.Access.Errors;
using Starter.Domain.Identity.Entities;
using Starter.Domain.Identity.Enums;
using Starter.Module.AI.Infrastructure.Persistence;
using Starter.Shared.Results;

namespace Starter.Module.AI.Infrastructure.Access;

public sealed class AiAssistantOwnershipHandler(
    AiDbContext db,
    IApplicationDbContext appDb,
    IResourceAccessService access) : IResourceOwnershipHandler
{
    public string ResourceType => ResourceTypes.AiAssistant;

    public async Task<Result<Guid>> GetOwnerAsync(Guid resourceId, CancellationToken ct)
    {
        var assistant = await db.AiAssistants
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(a => a.Id == resourceId, ct);
        return assistant is null
            ? Result.Failure<Guid>(AccessErrors.ResourceNotFound)
            : Result.Success(assistant.CreatedByUserId);
    }

    public async Task<Result<string>> GetDisplayNameAsync(Guid resourceId, CancellationToken ct)
    {
        var assistant = await db.AiAssistants
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(a => a.Id == resourceId, ct);
        return assistant is null
            ? Result.Failure<string>(AccessErrors.ResourceNotFound)
            : Result.Success(assistant.Name);
    }

    public async Task<Result> SetVisibilityAsync(Guid resourceId, ResourceVisibility visibility, CancellationToken ct)
    {
        var assistant = await db.AiAssistants
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(a => a.Id == resourceId, ct);
        if (assistant is null) return Result.Failure(AccessErrors.ResourceNotFound);
        if ((int)visibility > (int)ResourceTypes.MaxVisibility(ResourceTypes.AiAssistant))
            return Result.Failure(AccessErrors.VisibilityNotAllowedForResourceType);

        assistant.SetVisibility(visibility);
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result> TransferOwnershipAsync(Guid resourceId, Guid newOwnerId, CancellationToken ct)
    {
        var assistant = await db.AiAssistants
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(a => a.Id == resourceId, ct);
        if (assistant is null) return Result.Failure(AccessErrors.ResourceNotFound);

        var newOwner = await appDb.Users.FirstOrDefaultAsync(u => u.Id == newOwnerId, ct);
        if (newOwner is null) return Result.Failure(AccessErrors.OwnershipTargetNotInTenant);
        if (newOwner.TenantId != assistant.TenantId) return Result.Failure(AccessErrors.OwnershipTargetNotInTenant);
        if (newOwner.Status != UserStatus.Active) return Result.Failure(AccessErrors.OwnershipTargetInactive);

        var oldOwner = assistant.CreatedByUserId;
        assistant.TransferOwnership(newOwnerId);
        await db.SaveChangesAsync(ct);

        await access.GrantAsync(
            ResourceTypes.AiAssistant,
            resourceId,
            GrantSubjectType.User,
            oldOwner,
            AccessLevel.Manager,
            ct);

        return Result.Success();
    }
}
