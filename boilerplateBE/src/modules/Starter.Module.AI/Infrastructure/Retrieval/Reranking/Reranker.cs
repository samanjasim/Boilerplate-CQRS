using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Starter.Module.AI.Application.Services.Retrieval;
using Starter.Module.AI.Domain.Entities;
using Starter.Module.AI.Infrastructure.Settings;

namespace Starter.Module.AI.Infrastructure.Retrieval.Reranking;

/// <summary>
/// Composite <see cref="IReranker"/> that runs the <see cref="RerankStrategySelector"/>
/// then dispatches to <see cref="ListwiseReranker"/> or <see cref="PointwiseReranker"/>.
/// Implements the fallback chain: Pointwise → Listwise → FallbackRrf, Listwise → FallbackRrf,
/// Off → passthrough. Never throws for provider failures; <see cref="OperationCanceledException"/>
/// always bubbles up.
/// </summary>
internal sealed class Reranker : IReranker
{
    private readonly RerankStrategySelector _selector;
    private readonly ListwiseReranker _listwise;
    private readonly PointwiseReranker _pointwise;
    private readonly AiRagSettings _settings;
    private readonly ILogger<Reranker> _logger;

    public Reranker(
        RerankStrategySelector selector,
        ListwiseReranker listwise,
        PointwiseReranker pointwise,
        IOptions<AiRagSettings> settings,
        ILogger<Reranker> logger)
    {
        _selector = selector;
        _listwise = listwise;
        _pointwise = pointwise;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<RerankResult> RerankAsync(
        string query,
        IReadOnlyList<HybridHit> candidates,
        IReadOnlyList<AiDocumentChunk> candidateChunks,
        RerankContext context,
        CancellationToken ct)
    {
        var requested = _selector.Resolve(context);

        if (candidates.Count == 0 || requested == RerankStrategy.Off)
            return Passthrough(candidates, requested);

        var shouldTryListwise = requested == RerankStrategy.Listwise;

        if (requested == RerankStrategy.Pointwise)
        {
            try
            {
                var pointwiseResult = await _pointwise.RerankAsync(query, candidates, candidateChunks, ct);
                if (pointwiseResult.StrategyUsed != RerankStrategy.FallbackRrf)
                    return pointwiseResult;

                _logger.LogInformation(
                    "Reranker: Pointwise aborted to FallbackRrf; falling through to Listwise");
                shouldTryListwise = true;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Reranker: Pointwise threw unexpectedly; falling through to Listwise");
                shouldTryListwise = true;
            }
        }

        if (shouldTryListwise)
        {
            try
            {
                var listwiseResult = await _listwise.RerankAsync(query, candidates, candidateChunks, ct);
                return listwiseResult with { StrategyRequested = requested };
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Reranker: Listwise threw unexpectedly; falling back to RRF order");
            }
        }

        return Passthrough(candidates, requested, used: RerankStrategy.FallbackRrf);
    }

    private static RerankResult Passthrough(
        IReadOnlyList<HybridHit> candidates,
        RerankStrategy requested,
        RerankStrategy? used = null) =>
        new(
            Ordered: candidates,
            StrategyRequested: requested,
            StrategyUsed: used ?? requested,
            CandidatesIn: candidates.Count,
            CandidatesScored: 0,
            CacheHits: 0,
            LatencyMs: 0,
            TokensIn: 0,
            TokensOut: 0,
            UnusedRatio: 0.0);
}
