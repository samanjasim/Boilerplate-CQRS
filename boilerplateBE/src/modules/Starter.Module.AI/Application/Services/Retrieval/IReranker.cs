using Starter.Module.AI.Domain.Entities;

namespace Starter.Module.AI.Application.Services.Retrieval;

public interface IReranker
{
    /// <summary>
    /// Reorders candidates by relevance. Never throws — falls back to RRF order
    /// (returns RerankResult with StrategyUsed = FallbackRrf) on any failure.
    /// </summary>
    Task<RerankResult> RerankAsync(
        string query,
        IReadOnlyList<HybridHit> candidates,
        IReadOnlyList<AiDocumentChunk> candidateChunks,
        RerankContext context,
        CancellationToken ct);
}
