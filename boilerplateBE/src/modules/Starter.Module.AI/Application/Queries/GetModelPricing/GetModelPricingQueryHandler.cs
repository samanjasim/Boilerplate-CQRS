using MediatR;
using Microsoft.EntityFrameworkCore;
using Starter.Module.AI.Infrastructure.Persistence;
using Starter.Shared.Results;

namespace Starter.Module.AI.Application.Queries.GetModelPricing;

internal sealed class GetModelPricingQueryHandler(AiDbContext db)
    : IRequestHandler<GetModelPricingQuery, Result<IReadOnlyList<ModelPricingDto>>>
{
    public async Task<Result<IReadOnlyList<ModelPricingDto>>> Handle(GetModelPricingQuery request, CancellationToken ct)
    {
        var query = db.AiModelPricings.AsNoTracking().AsQueryable();
        if (request.ActiveOnly)
            query = query.Where(p => p.IsActive);

        var rows = await query
            .OrderBy(p => p.Provider)
            .ThenBy(p => p.Model)
            .ThenByDescending(p => p.EffectiveFrom)
            .Select(p => new ModelPricingDto(
                p.Id,
                p.Provider,
                p.Model,
                p.InputUsdPer1KTokens,
                p.OutputUsdPer1KTokens,
                p.IsActive,
                p.EffectiveFrom))
            .ToListAsync(ct);

        return Result.Success<IReadOnlyList<ModelPricingDto>>(rows);
    }
}
