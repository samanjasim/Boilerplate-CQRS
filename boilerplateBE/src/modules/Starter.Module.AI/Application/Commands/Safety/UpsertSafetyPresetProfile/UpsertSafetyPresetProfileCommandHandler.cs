using MediatR;
using Microsoft.EntityFrameworkCore;
using Starter.Application.Common.Interfaces;
using Starter.Module.AI.Constants;
using Starter.Module.AI.Domain.Entities;
using Starter.Module.AI.Infrastructure.Persistence;
using Starter.Shared.Results;

namespace Starter.Module.AI.Application.Commands.Safety.UpsertSafetyPresetProfile;

internal sealed class UpsertSafetyPresetProfileCommandHandler(
    AiDbContext db,
    ICurrentUserService currentUser) : IRequestHandler<UpsertSafetyPresetProfileCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(UpsertSafetyPresetProfileCommand cmd, CancellationToken ct)
    {
        // SuperAdmin only for platform-default rows. Tenant admin scoped to own tenant.
        if (cmd.TenantId is null)
        {
            if (!currentUser.HasPermission(AiPermissions.SafetyProfilesManage) || currentUser.TenantId is not null)
                return Result.Failure<Guid>(Error.Forbidden("Only platform admins can edit platform-default safety profiles."));
        }
        else
        {
            if (currentUser.TenantId is not Guid mine || mine != cmd.TenantId)
            {
                if (!IsPlatformAdmin(currentUser))
                    return Result.Failure<Guid>(Error.Forbidden("Cannot manage another tenant's safety profile."));
            }
        }

        var existing = await db.AiSafetyPresetProfiles
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(p =>
                p.TenantId == cmd.TenantId && p.Preset == cmd.Preset &&
                p.Provider == cmd.Provider && p.IsActive, ct);

        if (existing is null)
        {
            var entity = AiSafetyPresetProfile.Create(
                cmd.TenantId, cmd.Preset, cmd.Provider,
                cmd.CategoryThresholdsJson, cmd.BlockedCategoriesJson,
                cmd.FailureMode, cmd.RedactPii);
            db.AiSafetyPresetProfiles.Add(entity);
            await db.SaveChangesAsync(ct);
            return Result.Success(entity.Id);
        }

        existing.Update(cmd.CategoryThresholdsJson, cmd.BlockedCategoriesJson, cmd.FailureMode, cmd.RedactPii);
        await db.SaveChangesAsync(ct);
        return Result.Success(existing.Id);
    }

    private static bool IsPlatformAdmin(ICurrentUserService cu) => cu.TenantId is null;
}
