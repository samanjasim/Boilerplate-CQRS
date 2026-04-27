using Starter.Abstractions.Ai;

namespace Starter.Module.AI.Application.Services.Pricing;

/// <summary>
/// Looks up per-token USD pricing for an (AiProviderType, model) tuple from the
/// AiModelPricing table and computes estimated cost for a request. Cached via
/// ICacheService. Fails closed when no pricing row exists for the requested model.
/// </summary>
public interface IModelPricingService
{
    Task<decimal> EstimateCostAsync(
        AiProviderType provider,
        string model,
        int inputTokens,
        int outputTokens,
        CancellationToken ct = default);

    Task<(decimal InputUsdPer1KTokens, decimal OutputUsdPer1KTokens)> GetPricingAsync(
        AiProviderType provider,
        string model,
        CancellationToken ct = default);

    /// <summary>
    /// Removes the cached pricing entry for `(provider, model)`. Called by the upsert
    /// and deactivate handlers so superadmin pricing changes propagate without a 5-minute
    /// TTL lag.
    /// </summary>
    Task InvalidateAsync(AiProviderType provider, string model, CancellationToken ct = default);
}
