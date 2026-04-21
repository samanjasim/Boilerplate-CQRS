using Starter.Module.AI.Application.Services.Ingestion;
using Starter.Module.AI.Application.Services.Retrieval;

namespace Starter.Module.AI.Infrastructure.Retrieval.Resilience;

/// <summary>
/// <see cref="IVectorStore"/> decorator that routes <see cref="SearchAsync"/> and
/// <see cref="GetVectorsByIdsAsync"/> through the Qdrant circuit-breaker pipeline.
/// Mutating operations (Ensure/Upsert/Delete/Drop) bypass the breaker because they
/// are invoked from the ingestion path (indexing retries are MassTransit's concern,
/// not the live-chat latency budget).
/// </summary>
internal sealed class CircuitBreakingVectorStore : IVectorStore
{
    private readonly IVectorStore _inner;
    private readonly RagCircuitBreakerRegistry _registry;

    public CircuitBreakingVectorStore(IVectorStore inner, RagCircuitBreakerRegistry registry)
    {
        _inner = inner;
        _registry = registry;
    }

    public Task EnsureCollectionAsync(Guid tenantId, int vectorSize, CancellationToken ct)
        => _inner.EnsureCollectionAsync(tenantId, vectorSize, ct);

    public Task UpsertAsync(Guid tenantId, IReadOnlyList<VectorPoint> points, CancellationToken ct)
        => _inner.UpsertAsync(tenantId, points, ct);

    public Task DeleteByDocumentAsync(Guid tenantId, Guid documentId, CancellationToken ct)
        => _inner.DeleteByDocumentAsync(tenantId, documentId, ct);

    public Task DropCollectionAsync(Guid tenantId, CancellationToken ct)
        => _inner.DropCollectionAsync(tenantId, ct);

    public async Task<IReadOnlyList<VectorSearchHit>> SearchAsync(
        Guid tenantId,
        float[] queryVector,
        IReadOnlyCollection<Guid>? documentFilter,
        int limit,
        CancellationToken ct)
    {
        return await _registry.Qdrant.ExecuteAsync(
            async token => await _inner.SearchAsync(tenantId, queryVector, documentFilter, limit, token),
            ct);
    }

    public async Task<IReadOnlyDictionary<Guid, float[]>> GetVectorsByIdsAsync(
        Guid tenantId,
        IReadOnlyCollection<Guid> pointIds,
        CancellationToken ct)
    {
        return await _registry.Qdrant.ExecuteAsync(
            async token => await _inner.GetVectorsByIdsAsync(tenantId, pointIds, token),
            ct);
    }
}
