using Starter.Module.AI.Application.Services.Retrieval;

namespace Starter.Module.AI.Infrastructure.Retrieval.Resilience;

/// <summary>
/// <see cref="IKeywordSearchService"/> decorator that routes
/// <see cref="SearchAsync"/> through the Postgres-FTS circuit-breaker pipeline.
/// </summary>
internal sealed class CircuitBreakingKeywordSearch : IKeywordSearchService
{
    private readonly IKeywordSearchService _inner;
    private readonly RagCircuitBreakerRegistry _registry;

    public CircuitBreakingKeywordSearch(IKeywordSearchService inner, RagCircuitBreakerRegistry registry)
    {
        _inner = inner;
        _registry = registry;
    }

    public async Task<IReadOnlyList<KeywordSearchHit>> SearchAsync(
        Guid tenantId,
        string queryText,
        IReadOnlyCollection<Guid>? documentFilter,
        int limit,
        CancellationToken ct)
    {
        return await _registry.PostgresFts.ExecuteAsync(
            async token => await _inner.SearchAsync(tenantId, queryText, documentFilter, limit, token),
            ct);
    }
}
