using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Starter.Abstractions.Ai;
using Starter.Application.Common.Interfaces;
using Starter.Module.AI.Application.Services.Pricing;
using Starter.Module.AI.Infrastructure.Persistence;

namespace Starter.Module.AI.Infrastructure.Services.Pricing;

internal sealed class ModelPricingService(
    AiDbContext db,
    ICacheService cache,
    ILogger<ModelPricingService> logger) : IModelPricingService
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);

    public async Task<decimal> EstimateCostAsync(
        AiProviderType provider,
        string model,
        int inputTokens,
        int outputTokens,
        CancellationToken ct = default)
    {
        var (input, output) = await GetPricingAsync(provider, model, ct);
        return (input * inputTokens + output * outputTokens) / 1000m;
    }

    public async Task<(decimal InputUsdPer1KTokens, decimal OutputUsdPer1KTokens)> GetPricingAsync(
        AiProviderType provider,
        string model,
        CancellationToken ct = default)
    {
        var key = $"ai:pricing:{provider}:{model}";
        var pricing = await cache.GetOrSetAsync(key, async () =>
        {
            var now = DateTimeOffset.UtcNow;
            var row = await db.AiModelPricings
                .AsNoTracking()
                .Where(p => p.Provider == provider && p.Model == model && p.IsActive && p.EffectiveFrom <= now)
                .OrderByDescending(p => p.EffectiveFrom)
                .Select(p => new PricingEntry(p.InputUsdPer1KTokens, p.OutputUsdPer1KTokens))
                .FirstOrDefaultAsync(ct);
            if (row is null)
            {
                logger.LogWarning("No active pricing found for {Provider}/{Model}.", provider, model);
                throw new InvalidOperationException($"No active pricing configured for {provider}/{model}.");
            }
            return row;
        }, CacheTtl, ct);

        return (pricing.Input, pricing.Output);
    }

    private sealed record PricingEntry(decimal Input, decimal Output);
}
