using Starter.Application.Common.Interfaces;
using Starter.Application.Features.Settings.DTOs;
using Starter.Shared.Results;
using MediatR;

namespace Starter.Application.Features.Settings.Queries.GetSettings;

internal sealed class GetSettingsQueryHandler(
    ISettingsService settingsService,
    ICurrentUserService currentUserService) : IRequestHandler<GetSettingsQuery, Result<List<SettingGroupDto>>>
{
    public async Task<Result<List<SettingGroupDto>>> Handle(GetSettingsQuery request, CancellationToken cancellationToken)
    {
        var settings = await settingsService.GetAllAsync(currentUserService.TenantId, cancellationToken);

        var groups = settings
            .GroupBy(s => s.Category ?? "Other")
            .OrderBy(g => g.Key)
            .Select(g => new SettingGroupDto(g.Key, g.ToList()))
            .ToList();

        return Result.Success(groups);
    }
}
