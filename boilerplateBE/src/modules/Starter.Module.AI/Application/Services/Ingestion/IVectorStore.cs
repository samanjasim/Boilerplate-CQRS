namespace Starter.Module.AI.Application.Services.Ingestion;

public interface IVectorStore
{
    Task EnsureCollectionAsync(Guid tenantId, int vectorSize, CancellationToken ct);
    Task UpsertAsync(Guid tenantId, IReadOnlyList<VectorPoint> points, CancellationToken ct);
    Task DeleteByDocumentAsync(Guid tenantId, Guid documentId, CancellationToken ct);
    Task DropCollectionAsync(Guid tenantId, CancellationToken ct);
}
