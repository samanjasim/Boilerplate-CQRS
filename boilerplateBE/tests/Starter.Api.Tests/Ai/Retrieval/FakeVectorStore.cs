using Starter.Module.AI.Application.Services.Ingestion;
using Starter.Module.AI.Application.Services.Retrieval;

namespace Starter.Api.Tests.Ai.Retrieval;

internal sealed class FakeVectorStore : IVectorStore
{
    public List<VectorSearchHit> HitsToReturn { get; set; } = new();
    public Guid LastTenantId { get; private set; }
    public IReadOnlyCollection<Guid>? LastDocFilter { get; private set; }

    public Task EnsureCollectionAsync(Guid tenantId, int vectorSize, CancellationToken ct)
        => Task.CompletedTask;

    public Task UpsertAsync(Guid tenantId, IReadOnlyList<VectorPoint> points, CancellationToken ct)
        => Task.CompletedTask;

    public Task DeleteByDocumentAsync(Guid tenantId, Guid documentId, CancellationToken ct)
        => Task.CompletedTask;

    public Task DropCollectionAsync(Guid tenantId, CancellationToken ct)
        => Task.CompletedTask;

    public AclPayloadFilter? LastAclFilter { get; private set; }

    public Task<IReadOnlyList<VectorSearchHit>> SearchAsync(
        Guid tenantId,
        float[] queryVector,
        IReadOnlyCollection<Guid>? documentFilter,
        AclPayloadFilter? aclFilter,
        int limit,
        CancellationToken ct)
    {
        LastTenantId = tenantId;
        LastDocFilter = documentFilter;
        LastAclFilter = aclFilter;
        IReadOnlyList<VectorSearchHit> result = HitsToReturn.Take(limit).ToList();
        return Task.FromResult(result);
    }

    public Dictionary<Guid, float[]> VectorsById { get; } = new();
    public int GetVectorsByIdsCallCount { get; private set; }

    public Task<IReadOnlyDictionary<Guid, float[]>> GetVectorsByIdsAsync(
        Guid tenantId,
        IReadOnlyCollection<Guid> pointIds,
        CancellationToken ct)
    {
        GetVectorsByIdsCallCount++;
        IReadOnlyDictionary<Guid, float[]> result = pointIds
            .Where(VectorsById.ContainsKey)
            .ToDictionary(id => id, id => VectorsById[id]);
        return Task.FromResult(result);
    }
}
