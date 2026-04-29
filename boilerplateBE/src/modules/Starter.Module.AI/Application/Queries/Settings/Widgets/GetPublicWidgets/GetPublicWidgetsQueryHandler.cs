using MediatR;
using Microsoft.EntityFrameworkCore;
using Starter.Application.Common.Interfaces;
using Starter.Module.AI.Application.Commands.Settings.Widgets;
using Starter.Module.AI.Application.DTOs;
using Starter.Module.AI.Infrastructure.Persistence;
using Starter.Shared.Results;

namespace Starter.Module.AI.Application.Queries.Settings.Widgets.GetPublicWidgets;

internal sealed class GetPublicWidgetsQueryHandler(
    AiDbContext db,
    ICurrentUserService currentUser) : IRequestHandler<GetPublicWidgetsQuery, Result<IReadOnlyList<AiPublicWidgetDto>>>
{
    public async Task<Result<IReadOnlyList<AiPublicWidgetDto>>> Handle(GetPublicWidgetsQuery request, CancellationToken ct)
    {
        var tenantId = currentUser.TenantId ?? request.TenantId;
        if (tenantId is null || tenantId == Guid.Empty)
            return Result.Failure<IReadOnlyList<AiPublicWidgetDto>>(
                Error.Validation("AiSettings.TenantIdRequired", "A tenant id is required to read AI public widgets."));

        var widgets = await db.AiPublicWidgets
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(w => w.TenantId == tenantId.Value)
            .OrderBy(w => w.Name)
            .ThenBy(w => w.Id)
            .ToListAsync(ct);

        return Result.Success<IReadOnlyList<AiPublicWidgetDto>>(
            widgets.Select(AiPublicWidgetMappings.ToDto).ToList());
    }
}
