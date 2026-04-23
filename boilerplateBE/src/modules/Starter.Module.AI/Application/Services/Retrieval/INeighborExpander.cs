namespace Starter.Module.AI.Application.Services.Retrieval;

public interface INeighborExpander
{
    Task<IReadOnlyList<RetrievedChunk>> ExpandAsync(
        Guid tenantId,
        IReadOnlyList<RetrievedChunk> anchors,
        int windowSize,
        CancellationToken ct);
}
