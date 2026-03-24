using Starter.Application.Common.Interfaces;
using Starter.Shared.Results;
using MediatR;

namespace Starter.Application.Features.Settings.Commands.UpdateSettings;

internal sealed class UpdateSettingsCommandHandler(
    ISettingsService settingsService,
    ICurrentUserService currentUserService,
    ICacheService cacheService) : IRequestHandler<UpdateSettingsCommand, Result>
{
    public async Task<Result> Handle(UpdateSettingsCommand request, CancellationToken cancellationToken)
    {
        var tenantId = currentUserService.TenantId;

        foreach (var setting in request.Settings)
        {
            await settingsService.SetValueAsync(setting.Key, setting.Value, tenantId, cancellationToken);
        }

        // Invalidate the prefix-based cache for this tenant
        var cachePrefix = $"settings:{tenantId?.ToString() ?? "platform"}:";
        await cacheService.RemoveByPrefixAsync(cachePrefix, cancellationToken);

        return Result.Success();
    }
}
