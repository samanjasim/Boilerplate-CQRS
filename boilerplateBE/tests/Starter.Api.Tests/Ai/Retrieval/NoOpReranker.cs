using Starter.Module.AI.Application.Services.Retrieval;
using Starter.Module.AI.Domain.Entities;
using Starter.Module.AI.Infrastructure.Retrieval;

namespace Starter.Api.Tests.Ai.Retrieval;

/// <summary>
/// Passthrough <see cref="IReranker"/> for tests that don't exercise reranking —
/// returns candidates unchanged with StrategyUsed = Off so the pipeline proceeds
/// with the RRF order.
/// </summary>
internal sealed class NoOpReranker : IReranker
{
    public Task<RerankResult> RerankAsync(
        string query,
        IReadOnlyList<HybridHit> candidates,
        IReadOnlyList<AiDocumentChunk> candidateChunks,
        RerankContext context,
        CancellationToken ct)
        => Task.FromResult(new RerankResult(
            Ordered: candidates,
            StrategyRequested: RerankStrategy.Off,
            StrategyUsed: RerankStrategy.Off,
            CandidatesIn: candidates.Count,
            CandidatesScored: 0,
            CacheHits: 0,
            LatencyMs: 0,
            TokensIn: 0,
            TokensOut: 0,
            UnusedRatio: 0.0));
}
