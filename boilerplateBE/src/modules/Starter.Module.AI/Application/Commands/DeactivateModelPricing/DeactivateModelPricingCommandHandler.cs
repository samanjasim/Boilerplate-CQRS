using MediatR;
using Microsoft.EntityFrameworkCore;
using Starter.Module.AI.Application.Services.Pricing;
using Starter.Module.AI.Infrastructure.Persistence;
using Starter.Shared.Results;

namespace Starter.Module.AI.Application.Commands.DeactivateModelPricing;

internal sealed class DeactivateModelPricingCommandHandler(
    AiDbContext db,
    IModelPricingService pricingCache) : IRequestHandler<DeactivateModelPricingCommand, Result>
{
    public async Task<Result> Handle(DeactivateModelPricingCommand request, CancellationToken ct)
    {
        var entry = await db.AiModelPricings.FirstOrDefaultAsync(p => p.Id == request.Id, ct);
        if (entry is null)
            return Result.Failure(Error.NotFound("AiPricing.NotFound", $"Pricing entry '{request.Id}' not found."));

        entry.Deactivate();
        await db.SaveChangesAsync(ct);

        // Drop the cached entry so the next lookup falls through to find the next-most-recent
        // active row (or fail-closed if none exists).
        await pricingCache.InvalidateAsync(entry.Provider, entry.Model, ct);

        return Result.Success();
    }
}
