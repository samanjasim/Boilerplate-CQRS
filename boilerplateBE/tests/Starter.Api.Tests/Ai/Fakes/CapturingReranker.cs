using Starter.Module.AI.Application.Services.Retrieval;
using Starter.Module.AI.Domain.Entities;

namespace Starter.Api.Tests.Ai.Fakes;

internal sealed class CapturingReranker : IReranker
{
    public RerankContext? CapturedContext { get; private set; }

    public Task<RerankResult> RerankAsync(
        string query,
        IReadOnlyList<HybridHit> candidates,
        IReadOnlyList<AiDocumentChunk> candidateChunks,
        RerankContext context,
        CancellationToken ct)
    {
        CapturedContext = context;
        return Task.FromResult(new RerankResult(
            Ordered: candidates,
            StrategyRequested: RerankStrategy.Off,
            StrategyUsed: RerankStrategy.Off,
            CandidatesIn: candidates.Count,
            CandidatesScored: 0,
            CacheHits: 0,
            LatencyMs: 0,
            TokensIn: 0,
            TokensOut: 0,
            UnusedRatio: 0));
    }
}
