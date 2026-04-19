using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Starter.Module.AI.Application.Services.Ingestion;
using Starter.Module.AI.Application.Services.Retrieval;
using Starter.Module.AI.Domain.Entities;
using Starter.Module.AI.Domain.Enums;
using Starter.Module.AI.Infrastructure.Ingestion;
using Starter.Module.AI.Infrastructure.Persistence;
using Starter.Module.AI.Infrastructure.Retrieval.Reranking;
using Starter.Module.AI.Infrastructure.Settings;

namespace Starter.Module.AI.Infrastructure.Retrieval;

internal sealed class RagRetrievalService : IRagRetrievalService
{
    private readonly AiDbContext _db;
    private readonly IVectorStore _vectorStore;
    private readonly IKeywordSearchService _keywordSearch;
    private readonly IEmbeddingService _embeddingService;
    private readonly IQueryRewriter _queryRewriter;
    private readonly IReranker _reranker;
    private readonly RerankStrategySelector _rerankSelector;
    private readonly TokenCounter _tokenCounter;
    private readonly AiRagSettings _settings;
    private readonly ILogger<RagRetrievalService> _logger;

    public RagRetrievalService(
        AiDbContext db,
        IVectorStore vectorStore,
        IKeywordSearchService keywordSearch,
        IEmbeddingService embeddingService,
        IQueryRewriter queryRewriter,
        IReranker reranker,
        RerankStrategySelector rerankSelector,
        TokenCounter tokenCounter,
        IOptions<AiRagSettings> settings,
        ILogger<RagRetrievalService> logger)
    {
        _db = db;
        _vectorStore = vectorStore;
        _keywordSearch = keywordSearch;
        _embeddingService = embeddingService;
        _queryRewriter = queryRewriter;
        _reranker = reranker;
        _rerankSelector = rerankSelector;
        _tokenCounter = tokenCounter;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<RetrievedContext> RetrieveForTurnAsync(
        AiAssistant assistant,
        string latestUserMessage,
        CancellationToken ct)
    {
        if (assistant.RagScope == AiRagScope.None)
            throw new InvalidOperationException(
                "Caller must ensure RagScope != None before invoking retrieval.");

        var tenantId = assistant.TenantId ?? Guid.Empty;
        IReadOnlyCollection<Guid>? docFilter = assistant.RagScope == AiRagScope.SelectedDocuments
            ? assistant.KnowledgeBaseDocIds.ToList()
            : null;

        return await RetrieveForQueryAsync(
            tenantId,
            latestUserMessage,
            docFilter,
            _settings.TopK,
            _settings.MinHybridScore,
            _settings.IncludeParentContext,
            ct);
    }

    public async Task<RetrievedContext> RetrieveForQueryAsync(
        Guid tenantId,
        string queryText,
        IReadOnlyCollection<Guid>? documentFilter,
        int topK,
        decimal? minScore,
        bool includeParents,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(queryText))
            return RetrievedContext.Empty;

        var degraded = new List<string>();

        // 1. Query rewrite (original + variants). Never throws.
        var variants = await WithTimeoutAsync(
            innerCt => _queryRewriter.RewriteAsync(queryText, language: null, innerCt),
            _settings.StageTimeoutQueryRewriteMs,
            "query-rewrite",
            degraded,
            ct);
        // On rewriter timeout/failure variants is null/empty — fall back to the original query so retrieval continues (degraded stage already recorded above).
        IReadOnlyList<string> effectiveVariants = variants is { Count: > 0 } ? variants : new[] { queryText };

        // 2. Embed all variants in one batched call.
        var vectors = await WithTimeoutAsync(
            innerCt => _embeddingService.EmbedAsync(
                effectiveVariants, innerCt, attribution: null, requestType: AiRequestType.QueryEmbedding),
            _settings.StageTimeoutEmbedMs,
            "embed-query",
            degraded,
            ct);

        if (vectors is null || vectors.Length == 0)
        {
            return new RetrievedContext([], [], 0, false, degraded);
        }

        // Defensive: batched embed contract is 1:1 with inputs, but if a provider
        // ever returns a mismatched count we truncate both loops to the shared
        // minimum so vector/keyword indices stay aligned.
        var variantCount = Math.Min(vectors.Length, effectiveVariants.Count);
        if (vectors.Length != effectiveVariants.Count)
        {
            _logger.LogWarning(
                "Embedding returned {VectorCount} vectors for {VariantCount} query variants; truncating to {VariantCount2}",
                vectors.Length, effectiveVariants.Count, variantCount);
        }

        var retrievalTopK = _settings.RetrievalTopK;
        var minHybrid = minScore ?? _settings.MinHybridScore;

        // 3. Vector search per variant.
        var vectorLists = new List<IReadOnlyList<VectorSearchHit>>(variantCount);
        for (var i = 0; i < variantCount; i++)
        {
            var v = vectors[i];
            var hits = await WithTimeoutAsync(
                innerCt => _vectorStore.SearchAsync(tenantId, v, documentFilter, retrievalTopK, innerCt),
                _settings.StageTimeoutVectorMs,
                $"vector-search[{i}]",
                degraded,
                ct);
            vectorLists.Add(hits ?? (IReadOnlyList<VectorSearchHit>)Array.Empty<VectorSearchHit>());
        }

        // 4. Keyword search per variant.
        var keywordLists = new List<IReadOnlyList<KeywordSearchHit>>(variantCount);
        for (var i = 0; i < variantCount; i++)
        {
            var q = effectiveVariants[i];
            var hits = await WithTimeoutAsync(
                innerCt => _keywordSearch.SearchAsync(tenantId, q, documentFilter, retrievalTopK, innerCt),
                _settings.StageTimeoutKeywordMs,
                $"keyword-search[{i}]",
                degraded,
                ct);
            keywordLists.Add(hits ?? (IReadOnlyList<KeywordSearchHit>)Array.Empty<KeywordSearchHit>());
        }

        // 5. RRF multi-list fuse.
        var mergedHits = HybridScoreCalculator.Combine(
            vectorLists,
            keywordLists,
            _settings.VectorWeight,
            _settings.KeywordWeight,
            _settings.RrfK,
            minHybrid);

        // 6. Rerank. Resolve the strategy up front so we can size the candidate pool
        // larger than topK — the reranker reorders within this pool, then we trim.
        // Task 17 will populate QuestionType once the classifier runs earlier in the
        // pipeline; for now we pass a null-question context so the selector's default
        // applies (Listwise when RerankStrategy=Auto).
        var plannedCtx = new RerankContext(QuestionType: null, StrategyOverride: null);
        var plannedStrategy = _rerankSelector.Resolve(plannedCtx);
        var poolMultiplier = plannedStrategy switch
        {
            RerankStrategy.Listwise => _settings.ListwisePoolMultiplier,
            RerankStrategy.Pointwise => _settings.PointwisePoolMultiplier,
            _ => 1
        };
        var poolSize = Math.Max(topK, topK * poolMultiplier);
        var pool = mergedHits.Take(poolSize).ToList();

        if (pool.Count == 0)
            return new RetrievedContext([], [], 0, false, degraded);

        // HybridHit.ChunkId carries the qdrant_point_id; chunk rows are looked up by
        // QdrantPointId (not Id) because the Qdrant point uses a distinct guid from
        // the ai_document_chunks primary key. We load chunk rows for the full pool so
        // the reranker can see content — reusing this map after rerank avoids a
        // second DB roundtrip.
        var poolPointIds = pool.Select(h => h.ChunkId).ToList();
        var poolChunkEntities = await _db.AiDocumentChunks
            .AsNoTracking()
            .Where(c => poolPointIds.Contains(c.QdrantPointId))
            .ToListAsync(ct);
        var chunkByPointId = poolChunkEntities.ToDictionary(c => c.QdrantPointId);

        // Reranker contract: for every hit in the input list there must be a matching
        // chunk in candidateChunks (it builds a dictionary keyed by QdrantPointId and
        // throws on missing). Eventual consistency between Qdrant and the DB can leave
        // orphan point ids — drop those from the pool before reranking.
        var alignedPool = new List<HybridHit>(pool.Count);
        var alignedChunks = new List<AiDocumentChunk>(pool.Count);
        foreach (var hit in pool)
        {
            if (chunkByPointId.TryGetValue(hit.ChunkId, out var chunk))
            {
                alignedPool.Add(hit);
                alignedChunks.Add(chunk);
            }
            else
            {
                _logger.LogWarning(
                    "Pool hit {PointId} has no matching chunk row; skipping", hit.ChunkId);
            }
        }

        if (alignedPool.Count == 0)
            return new RetrievedContext([], [], 0, false, degraded);

        // 7. Rerank the aligned pool. On timeout/failure fall back to RRF order.
        var rerankResult = await WithTimeoutAsync(
            innerCt => _reranker.RerankAsync(queryText, alignedPool, alignedChunks, plannedCtx, innerCt),
            _settings.StageTimeoutRerankMs,
            "rerank",
            degraded,
            ct);

        IReadOnlyList<HybridHit> rerankedHits = rerankResult?.Ordered ?? alignedPool;

        if (rerankResult is not null)
        {
            _logger.LogInformation(
                "RAG rerank: Requested={RerankStrategyRequested} Used={RerankStrategyUsed} Latency={RerankLatencyMs}ms TokensIn={RerankTokensIn} TokensOut={RerankTokensOut} CacheHits={RerankCacheHits} Unused={RerankUnusedRatio:P0}",
                rerankResult.StrategyRequested,
                rerankResult.StrategyUsed,
                rerankResult.LatencyMs,
                rerankResult.TokensIn,
                rerankResult.TokensOut,
                rerankResult.CacheHits,
                rerankResult.UnusedRatio);
        }

        var topKHits = rerankedHits.Take(topK).ToList();

        if (topKHits.Count == 0)
            return new RetrievedContext([], [], 0, false, degraded);

        // chunkByPointId already covers the full pool, so we can look up children
        // directly rather than issue another DB query.
        var children = topKHits
            .Select(h => chunkByPointId[h.ChunkId])
            .ToList();

        var scoreMap = topKHits.ToDictionary(h => h.ChunkId, h => h);

        List<AiDocumentChunk> parentEntities = [];
        if (includeParents)
        {
            var parentIds = children
                .Where(c => c.ParentChunkId.HasValue)
                .Select(c => c.ParentChunkId!.Value)
                .Distinct()
                .ToList();

            if (parentIds.Count > 0)
            {
                parentEntities = await _db.AiDocumentChunks
                    .AsNoTracking()
                    .Where(c => parentIds.Contains(c.Id))
                    .ToListAsync(ct);
            }
        }

        var docIds = children.Select(c => c.DocumentId)
            .Concat(parentEntities.Select(p => p.DocumentId))
            .Distinct()
            .ToList();

        var docNames = await _db.AiDocuments
            .AsNoTracking()
            .Where(d => docIds.Contains(d.Id))
            .ToDictionaryAsync(d => d.Id, d => d.Name, ct);

        var childChunks = children
            .Select(c => Map(c, scoreMap.GetValueOrDefault(c.QdrantPointId), docNames))
            .ToList();

        var parentChunks = parentEntities
            .Select(p => Map(p, null, docNames))
            .ToList();

        var (trimmedChildren, trimmedParents, totalTokens, truncated) =
            TrimToBudget(childChunks, parentChunks, _settings.MaxContextTokens);

        return new RetrievedContext(trimmedChildren, trimmedParents, totalTokens, truncated, degraded);
    }

    private RetrievedChunk Map(
        AiDocumentChunk chunk,
        HybridHit? hit,
        Dictionary<Guid, string> docNames)
    {
        var docName = docNames.TryGetValue(chunk.DocumentId, out var n) ? n : "(unknown)";
        return new RetrievedChunk(
            ChunkId: chunk.Id,
            DocumentId: chunk.DocumentId,
            DocumentName: docName,
            Content: chunk.Content,
            SectionTitle: chunk.SectionTitle,
            PageNumber: chunk.PageNumber,
            ChunkLevel: chunk.ChunkLevel,
            SemanticScore: hit?.SemanticScore ?? 0m,
            KeywordScore: hit?.KeywordScore ?? 0m,
            HybridScore: hit?.HybridScore ?? 0m,
            ParentChunkId: chunk.ParentChunkId);
    }

    private (IReadOnlyList<RetrievedChunk> Children, IReadOnlyList<RetrievedChunk> Parents, int TotalTokens, bool Truncated)
        TrimToBudget(
            List<RetrievedChunk> children,
            List<RetrievedChunk> parents,
            int budget)
    {
        var kept = new List<RetrievedChunk>();
        var usedTokens = 0;
        var truncated = false;

        foreach (var c in children)
        {
            var tokens = _tokenCounter.Count(c.Content);
            if (usedTokens + tokens > budget)
            {
                truncated = true;
                break;
            }
            kept.Add(c);
            usedTokens += tokens;
        }

        var keptParentIds = kept
            .Where(c => c.ParentChunkId.HasValue)
            .Select(c => c.ParentChunkId!.Value)
            .ToHashSet();

        var keptParents = new List<RetrievedChunk>();
        foreach (var p in parents)
        {
            if (!keptParentIds.Contains(p.ChunkId)) continue;
            var tokens = _tokenCounter.Count(p.Content);
            if (usedTokens + tokens > budget)
            {
                truncated = true;
                continue;
            }
            keptParents.Add(p);
            usedTokens += tokens;
        }

        return (kept, keptParents, usedTokens, truncated);
    }

    /// <summary>
    /// Runs an I/O stage under a linked CancellationTokenSource that fires after
    /// <paramref name="timeoutMs"/>. If the stage throws OperationCanceledException
    /// due to the timeout (caller's token not cancelled) or any other exception, the
    /// stage name is appended to <paramref name="degraded"/> and null is returned so
    /// the pipeline can continue with empty hits for this stage.
    /// </summary>
    private async Task<T?> WithTimeoutAsync<T>(
        Func<CancellationToken, Task<T>> op,
        int timeoutMs,
        string stageName,
        List<string> degraded,
        CancellationToken ct) where T : class
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(timeoutMs);
        try
        {
            return await op(cts.Token);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Caller cancelled — propagate so the whole chat turn can abort cleanly.
            throw;
        }
        catch (OperationCanceledException)
        {
            // Stage exceeded the per-stage budget — degrade and continue.
            degraded.Add(stageName);
            _logger.LogWarning("RAG stage '{Stage}' timed out after {TimeoutMs}ms", stageName, timeoutMs);
            return null;
        }
        catch (Exception ex) when (IsTransientStageException(ex))
        {
            degraded.Add(stageName);
            _logger.LogError(ex, "RAG stage '{Stage}' failed", stageName);
            return null;
        }
    }

    // Exceptions we treat as transient dependency failures (I/O or RPC). Programmer
    // bugs (ArgumentException, ObjectDisposedException, InvalidOperationException,
    // NullReferenceException, etc.) fall through and fail the turn loudly so they
    // are caught in development instead of being silently hidden as "degraded".
    private static bool IsTransientStageException(Exception ex) =>
        ex is System.Net.Http.HttpRequestException
           or TimeoutException
           or System.Data.Common.DbException
           or Grpc.Core.RpcException
           or TaskCanceledException;
}
