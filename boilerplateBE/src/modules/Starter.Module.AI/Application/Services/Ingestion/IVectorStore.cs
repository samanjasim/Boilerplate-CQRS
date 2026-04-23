using Starter.Module.AI.Application.Services.Retrieval;

namespace Starter.Module.AI.Application.Services.Ingestion;

public interface IVectorStore
{
    Task EnsureCollectionAsync(Guid tenantId, int vectorSize, CancellationToken ct);
    Task UpsertAsync(Guid tenantId, IReadOnlyList<VectorPoint> points, CancellationToken ct);
    Task DeleteByDocumentAsync(Guid tenantId, Guid documentId, CancellationToken ct);
    Task DropCollectionAsync(Guid tenantId, CancellationToken ct);
    Task<IReadOnlyList<VectorSearchHit>> SearchAsync(
        Guid tenantId,
        float[] queryVector,
        IReadOnlyCollection<Guid>? documentFilter,
        AclPayloadFilter? aclFilter,
        int limit,
        CancellationToken ct);

    /// <summary>
    /// Retrieve stored embedding vectors for a batch of point-ids. Missing ids are
    /// silently omitted from the result dictionary (eventual consistency between
    /// Qdrant and the relational DB can leave orphan ids).
    /// Callers should keep batches small (≤ ~100 ids per call); this method does not
    /// chunk internally and a large batch may exceed the vector store's request size.
    /// </summary>
    Task<IReadOnlyDictionary<Guid, float[]>> GetVectorsByIdsAsync(
        Guid tenantId,
        IReadOnlyCollection<Guid> pointIds,
        CancellationToken ct);
}
