using MediatR;
using Microsoft.EntityFrameworkCore;
using Starter.Application.Common.Interfaces;
using Starter.Domain.FeatureFlags.Entities;
using Starter.Domain.FeatureFlags.Enums;
using Starter.Module.Billing.Application.DTOs;
using Starter.Shared.Results;

namespace Starter.Module.Billing.Application.Queries.GetPlanOptions;

internal sealed class GetPlanOptionsQueryHandler(
    IApplicationDbContext context) : IRequestHandler<GetPlanOptionsQuery, Result<List<PlanOptionDto>>>
{
    public async Task<Result<List<PlanOptionDto>>> Handle(GetPlanOptionsQuery request, CancellationToken cancellationToken)
    {
        var options = await context.Set<FeatureFlag>()
            .AsNoTracking()
            .Where(f => f.Category != FlagCategory.Custom)
            .OrderBy(f => f.Category)
            .ThenBy(f => f.Name)
            .Select(f => new PlanOptionDto(
                f.Key,
                f.Name,
                f.Description,
                f.ValueType.ToString(),
                f.DefaultValue,
                f.Category.ToString()))
            .ToListAsync(cancellationToken);

        return Result.Success(options);
    }
}
