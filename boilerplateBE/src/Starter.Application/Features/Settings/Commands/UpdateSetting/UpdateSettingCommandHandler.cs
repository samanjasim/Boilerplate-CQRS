using Starter.Application.Common.Interfaces;
using Starter.Shared.Results;
using MediatR;

namespace Starter.Application.Features.Settings.Commands.UpdateSetting;

internal sealed class UpdateSettingCommandHandler(
    ISettingsService settingsService,
    ICurrentUserService currentUserService,
    ICacheService cacheService) : IRequestHandler<UpdateSettingCommand, Result>
{
    public async Task<Result> Handle(UpdateSettingCommand request, CancellationToken cancellationToken)
    {
        var tenantId = currentUserService.TenantId;

        await settingsService.SetValueAsync(request.Key, request.Value, tenantId, cancellationToken);

        // Invalidate the prefix-based cache for this tenant
        var cachePrefix = $"settings:{tenantId?.ToString() ?? "platform"}:";
        await cacheService.RemoveByPrefixAsync(cachePrefix, cancellationToken);

        return Result.Success();
    }
}
