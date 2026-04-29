using MediatR;
using Microsoft.EntityFrameworkCore;
using Starter.Abstractions.Ai;
using Starter.Application.Common.Interfaces;
using Starter.Module.AI.Domain.Enums;
using Starter.Module.AI.Domain.Errors;
using Starter.Module.AI.Infrastructure.Persistence;
using Starter.Shared.Results;

namespace Starter.Module.AI.Application.Commands.Safety.DeactivateSafetyPresetProfile;

internal sealed class DeactivateSafetyPresetProfileCommandHandler(
    AiDbContext db,
    ICurrentUserService currentUser) : IRequestHandler<DeactivateSafetyPresetProfileCommand, Result>
{
    public async Task<Result> Handle(DeactivateSafetyPresetProfileCommand cmd, CancellationToken ct)
    {
        var entity = await db.AiSafetyPresetProfiles
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(p => p.Id == cmd.ProfileId, ct);
        if (entity is null)
            return Result.Failure(AiModerationErrors.PresetProfileNotFound(default, default));

        if (entity.TenantId is null && currentUser.TenantId is not null)
            return Result.Failure(Error.Forbidden("Only platform admins can deactivate platform-default profiles."));
        if (entity.TenantId is { } et && currentUser.TenantId is { } current && et != current)
            return Result.Failure(Error.Forbidden("Cannot deactivate another tenant's profile."));

        entity.Deactivate();
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }
}
