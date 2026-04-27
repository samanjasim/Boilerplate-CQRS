using MediatR;
using Starter.Application.Common.Interfaces;
using Starter.Module.AI.Application.Queries.GetModelPricing;
using Starter.Module.AI.Application.Services.Pricing;
using Starter.Module.AI.Domain.Entities;
using Starter.Module.AI.Infrastructure.Persistence;
using Starter.Shared.Results;

namespace Starter.Module.AI.Application.Commands.UpsertModelPricing;

internal sealed class UpsertModelPricingCommandHandler(
    AiDbContext db,
    ICurrentUserService currentUser,
    IModelPricingService pricingCache) : IRequestHandler<UpsertModelPricingCommand, Result<ModelPricingDto>>
{
    public async Task<Result<ModelPricingDto>> Handle(UpsertModelPricingCommand request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Model))
            return Result.Failure<ModelPricingDto>(Error.Validation("AiPricing.ModelRequired", "Model is required."));

        if (request.InputUsdPer1KTokens < 0 || request.OutputUsdPer1KTokens < 0)
            return Result.Failure<ModelPricingDto>(Error.Validation("AiPricing.NegativePrice", "Prices must be non-negative."));

        var entry = AiModelPricing.Create(
            request.Provider,
            request.Model,
            request.InputUsdPer1KTokens,
            request.OutputUsdPer1KTokens,
            request.EffectiveFrom ?? DateTimeOffset.UtcNow,
            currentUser.UserId);

        db.AiModelPricings.Add(entry);
        await db.SaveChangesAsync(ct);

        // Invalidate the pricing cache so the new rate is picked up immediately rather
        // than waiting for the 5-minute TTL.
        await pricingCache.InvalidateAsync(entry.Provider, entry.Model, ct);

        return Result.Success(new ModelPricingDto(
            entry.Id,
            entry.Provider,
            entry.Model,
            entry.InputUsdPer1KTokens,
            entry.OutputUsdPer1KTokens,
            entry.IsActive,
            entry.EffectiveFrom));
    }
}
