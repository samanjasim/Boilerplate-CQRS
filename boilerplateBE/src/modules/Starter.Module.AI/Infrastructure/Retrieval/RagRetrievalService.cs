using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Starter.Module.AI.Application.Services.Ingestion;
using Starter.Module.AI.Application.Services.Retrieval;
using Starter.Module.AI.Domain.Entities;
using Starter.Module.AI.Domain.Enums;
using Starter.Module.AI.Infrastructure.Ingestion;
using Starter.Module.AI.Infrastructure.Observability;
using Starter.Module.AI.Infrastructure.Persistence;
using Starter.Module.AI.Infrastructure.Retrieval.Reranking;
using Starter.Module.AI.Infrastructure.Settings;
using Starter.Module.AI.Infrastructure.Telemetry;

namespace Starter.Module.AI.Infrastructure.Retrieval;

internal sealed class RagRetrievalService : IRagRetrievalService
{
    private readonly AiDbContext _db;
    private readonly IVectorStore _vectorStore;
    private readonly IKeywordSearchService _keywordSearch;
    private readonly IEmbeddingService _embeddingService;
    private readonly IQueryRewriter _queryRewriter;
    private readonly IContextualQueryResolver _contextualResolver;
    private readonly IQuestionClassifier _classifier;
    private readonly IReranker _reranker;
    private readonly RerankStrategySelector _rerankSelector;
    private readonly INeighborExpander _neighborExpander;
    private readonly TokenCounter _tokenCounter;
    private readonly AiRagSettings _settings;
    private readonly ILogger<RagRetrievalService> _logger;

    public RagRetrievalService(
        AiDbContext db,
        IVectorStore vectorStore,
        IKeywordSearchService keywordSearch,
        IEmbeddingService embeddingService,
        IQueryRewriter queryRewriter,
        IContextualQueryResolver contextualResolver,
        IQuestionClassifier classifier,
        IReranker reranker,
        RerankStrategySelector rerankSelector,
        INeighborExpander neighborExpander,
        TokenCounter tokenCounter,
        IOptions<AiRagSettings> settings,
        ILogger<RagRetrievalService> logger)
    {
        _db = db;
        _vectorStore = vectorStore;
        _keywordSearch = keywordSearch;
        _embeddingService = embeddingService;
        _queryRewriter = queryRewriter;
        _contextualResolver = contextualResolver;
        _classifier = classifier;
        _reranker = reranker;
        _rerankSelector = rerankSelector;
        _neighborExpander = neighborExpander;
        _tokenCounter = tokenCounter;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<RetrievedContext> RetrieveForTurnAsync(
        AiAssistant assistant,
        string latestUserMessage,
        IReadOnlyList<RagHistoryMessage> history,
        CancellationToken ct)
    {
        if (assistant.RagScope == AiRagScope.None)
            throw new InvalidOperationException(
                "Caller must ensure RagScope != None before invoking retrieval.");

        var tenantId = assistant.TenantId ?? Guid.Empty;
        IReadOnlyCollection<Guid>? docFilter = assistant.RagScope == AiRagScope.SelectedDocuments
            ? assistant.KnowledgeBaseDocIds.ToList()
            : null;

        string effectiveQuery = latestUserMessage;
        var degradedForContext = new List<string>();

        if (_settings.EnableContextualRewrite && history.Count > 0)
        {
            var detectedLang = RagLanguageDetector.Detect(latestUserMessage);
            var resolved = await WithTimeoutAsync(
                innerCt => _contextualResolver.ResolveAsync(latestUserMessage, history, detectedLang, innerCt),
                _settings.StageTimeoutContextualizeMs,
                RagStages.Contextualize,
                degradedForContext,
                ct);

            if (!string.IsNullOrWhiteSpace(resolved))
                effectiveQuery = resolved;
        }

        if (degradedForContext.Count > 0)
        {
            return await RetrieveForQueryInternalAsync(
                tenantId,
                effectiveQuery,
                docFilter,
                _settings.TopK,
                _settings.MinHybridScore,
                _settings.IncludeParentContext,
                seedDegraded: degradedForContext,
                ct);
        }

        return await RetrieveForQueryAsync(
            tenantId,
            effectiveQuery,
            docFilter,
            _settings.TopK,
            _settings.MinHybridScore,
            _settings.IncludeParentContext,
            ct);
    }

    public Task<RetrievedContext> RetrieveForQueryAsync(
        Guid tenantId,
        string queryText,
        IReadOnlyCollection<Guid>? documentFilter,
        int topK,
        decimal? minScore,
        bool includeParents,
        CancellationToken ct)
        => RetrieveForQueryInternalAsync(tenantId, queryText, documentFilter, topK, minScore, includeParents, seedDegraded: null, ct);

    private async Task<RetrievedContext> RetrieveForQueryInternalAsync(
        Guid tenantId,
        string queryText,
        IReadOnlyCollection<Guid>? documentFilter,
        int topK,
        decimal? minScore,
        bool includeParents,
        IReadOnlyList<string>? seedDegraded,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(queryText))
            return RetrievedContext.Empty;

        var scopeTag = documentFilter is { Count: > 0 } ? "SelectedDocuments" : "AllTenantDocuments";
        AiRagMetrics.RetrievalRequests.Add(
            1, new KeyValuePair<string, object?>("rag.scope", scopeTag));
        var detectedLang = RagLanguageDetector.Detect(queryText);

        using var activity = RagActivitySource.Source.StartActivity("rag.retrieve", ActivityKind.Internal);
        activity?.SetTag(RagTracingTags.RetrieveTopK, topK);

        var degraded = seedDegraded is { Count: > 0 }
            ? new List<string>(seedDegraded)
            : new List<string>();

        // 0. Classify the question. Short-circuits greetings and passes QuestionType
        // downstream for the reranker strategy selector.
        QuestionType? questionType = null;
        using (var classifyCts = CancellationTokenSource.CreateLinkedTokenSource(ct))
        {
            classifyCts.CancelAfter(_settings.StageTimeoutClassifyMs);
            var classifySw = Stopwatch.StartNew();
            string classifyOutcome = RagStageOutcome.Success;
            try
            {
                questionType = await _classifier.ClassifyAsync(queryText, classifyCts.Token);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                classifyOutcome = RagStageOutcome.Timeout;
                throw;
            }
            catch (OperationCanceledException)
            {
                classifyOutcome = RagStageOutcome.Timeout;
                degraded.Add(RagStages.Classify);
                _logger.LogWarning(
                    "RAG stage '{Stage}' timed out after {TimeoutMs}ms",
                    RagStages.Classify,
                    _settings.StageTimeoutClassifyMs);
            }
            catch (Exception ex) when (IsTransientStageException(ex))
            {
                classifyOutcome = RagStageOutcome.Error;
                degraded.Add(RagStages.Classify);
                _logger.LogError(ex, "RAG stage '{Stage}' failed", RagStages.Classify);
            }
            finally
            {
                classifySw.Stop();
                AiRagMetrics.StageDuration.Record(
                    classifySw.Elapsed.TotalMilliseconds,
                    new KeyValuePair<string, object?>("rag.stage", RagStages.Classify));
                AiRagMetrics.StageOutcome.Add(
                    1,
                    new KeyValuePair<string, object?>("rag.stage", RagStages.Classify),
                    new KeyValuePair<string, object?>("rag.outcome", classifyOutcome));
            }
        }

        _logger.LogDebug(
            "RAG classify: QuestionType={QuestionType}",
            questionType);
        activity?.SetTag(RagTracingTags.ClassifyType, questionType?.ToString() ?? "null");

        // Short-circuit greetings — chat injection layer handles empty context gracefully.
        if (questionType == QuestionType.Greeting)
        {
            _logger.LogInformation("RAG short-circuit: greeting");
            return new RetrievedContext([], [], 0, false, degraded, [], 0, detectedLang);
        }

        // 1. Query rewrite (original + variants). Never throws.
        var variants = await WithTimeoutAsync(
            innerCt => _queryRewriter.RewriteAsync(queryText, language: null, innerCt),
            _settings.StageTimeoutQueryRewriteMs,
            RagStages.QueryRewrite,
            degraded,
            ct);
        // On rewriter timeout/failure variants is null/empty — fall back to the original query so retrieval continues (degraded stage already recorded above).
        IReadOnlyList<string> effectiveVariants = variants is { Count: > 0 } ? variants : new[] { queryText };
        activity?.SetTag(RagTracingTags.RewriteVariantsUsed, effectiveVariants.Count);

        // 2. Embed all variants in one batched call.
        var vectors = await WithTimeoutAsync(
            innerCt => _embeddingService.EmbedAsync(
                effectiveVariants, innerCt, attribution: null, requestType: AiRequestType.QueryEmbedding),
            _settings.StageTimeoutEmbedMs,
            RagStages.EmbedQuery,
            degraded,
            ct);

        if (vectors is null || vectors.Length == 0)
        {
            return new RetrievedContext([], [], 0, false, degraded, [], 0, detectedLang);
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
                RagStages.VectorSearch(i),
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
                RagStages.KeywordSearch(i),
                degraded,
                ct);
            keywordLists.Add(hits ?? (IReadOnlyList<KeywordSearchHit>)Array.Empty<KeywordSearchHit>());
            AiRagMetrics.KeywordHits.Record(
                (long)(hits?.Count ?? 0),
                new KeyValuePair<string, object?>("rag.lang", detectedLang));
        }

        // 5. RRF multi-list fuse.
        var mergedHits = HybridScoreCalculator.Combine(
            vectorLists,
            keywordLists,
            _settings.VectorWeight,
            _settings.KeywordWeight,
            _settings.RrfK,
            minHybrid);
        AiRagMetrics.FusionCandidates.Record(mergedHits.Count);
        int fusedCandidatesCount = mergedHits.Count;

        // 6. Rerank. Resolve the strategy up front so we can size the candidate pool
        // larger than topK — the reranker reorders within this pool, then we trim.
        var plannedCtx = new RerankContext(QuestionType: questionType, StrategyOverride: null);
        var plannedStrategy = _rerankSelector.Resolve(plannedCtx);
        var poolMultiplier = plannedStrategy switch
        {
            RerankStrategy.Listwise => Math.Max(1, _settings.ListwisePoolMultiplier),
            RerankStrategy.Pointwise => Math.Max(1, _settings.PointwisePoolMultiplier),
            _ => 1
        };
        var poolSize = Math.Max(topK, topK * poolMultiplier);
        var pool = mergedHits.Take(poolSize).ToList();
        activity?.SetTag(RagTracingTags.RetrievePoolSize, pool.Count);
        activity?.SetTag(RagTracingTags.RetrieveVariantsUsed, variantCount);

        if (pool.Count == 0)
            return new RetrievedContext([], [], 0, false, degraded, [], 0, detectedLang);

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
            return new RetrievedContext([], [], 0, false, degraded, [], 0, detectedLang);

        // 7. Rerank the aligned pool. On timeout/failure fall back to RRF order.
        var rerankResult = await WithTimeoutAsync(
            innerCt => _reranker.RerankAsync(queryText, alignedPool, alignedChunks, plannedCtx, innerCt),
            _settings.StageTimeoutRerankMs,
            RagStages.Rerank,
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

            activity?.SetTag(RagTracingTags.RerankStrategyRequested, rerankResult.StrategyRequested.ToString());
            activity?.SetTag(RagTracingTags.RerankStrategyUsed, rerankResult.StrategyUsed.ToString());
            activity?.SetTag(RagTracingTags.RerankFellBack, rerankResult.StrategyUsed == RerankStrategy.FallbackRrf);
            activity?.SetTag(RagTracingTags.RerankCacheHits, rerankResult.CacheHits);
            activity?.SetTag(RagTracingTags.RerankLatencyMs, rerankResult.LatencyMs);
            activity?.SetTag(RagTracingTags.RerankUnusedRatio, rerankResult.UnusedRatio);
        }

        var topKHits = rerankedHits.Take(topK).ToList();

        if (topKHits.Count == 0)
            return new RetrievedContext([], [], 0, false, degraded, [], 0, detectedLang);

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

        var (trimmedChildren, trimmedParents, usedTokens, truncated) =
            TrimToBudget(childChunks, parentChunks, _settings.MaxContextTokens);

        IReadOnlyList<RetrievedChunk> siblings = Array.Empty<RetrievedChunk>();
        if (_settings.NeighborWindowSize > 0 && trimmedChildren.Count > 0)
        {
            var expanded = await WithTimeoutAsync(
                innerCt => _neighborExpander.ExpandAsync(tenantId, trimmedChildren, _settings.NeighborWindowSize, innerCt),
                _settings.StageTimeoutNeighborMs,
                RagStages.NeighborExpand,
                degraded,
                ct);
            if (expanded is { Count: > 0 })
            {
                (siblings, usedTokens, truncated) = TrimSiblingsToRemainingBudget(
                    expanded, _settings.MaxContextTokens, usedTokens, truncated);
            }
        }
        activity?.SetTag(RagTracingTags.NeighborSiblingsReturned, siblings.Count);
        activity?.SetTag(RagTracingTags.RetrieveTruncated, truncated);
        activity?.SetTag(RagTracingTags.RetrieveDegradedStages, string.Join(",", degraded));

        return new RetrievedContext(trimmedChildren, trimmedParents, usedTokens, truncated, degraded, siblings, fusedCandidatesCount, detectedLang);
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
            ParentChunkId: chunk.ParentChunkId,
            ChunkIndex: chunk.ChunkIndex,
            ChunkType: chunk.ChunkType);
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

    // Siblings are the first casualties of context-window pressure — they're
    // contextual nicety, not citation targets. Drop any that would push past
    // the budget, preserving anchor-order.
    private (IReadOnlyList<RetrievedChunk> Siblings, int UsedTokens, bool Truncated) TrimSiblingsToRemainingBudget(
        IReadOnlyList<RetrievedChunk> siblings,
        int budget,
        int usedTokens,
        bool truncated)
    {
        var kept = new List<RetrievedChunk>(siblings.Count);
        foreach (var s in siblings)
        {
            var tokens = _tokenCounter.Count(s.Content);
            if (usedTokens + tokens > budget)
            {
                truncated = true;
                continue;
            }
            kept.Add(s);
            usedTokens += tokens;
        }
        return (kept, usedTokens, truncated);
    }

    /// <summary>
    /// Runs an I/O stage under a linked CancellationTokenSource that fires after
    /// <paramref name="timeoutMs"/>. If the stage throws OperationCanceledException
    /// due to the timeout (caller's token not cancelled) or any other exception, the
    /// stage name is appended to <paramref name="degraded"/> and null is returned so
    /// the pipeline can continue with empty hits for this stage.
    /// </summary>
    private Task<T?> WithTimeoutAsync<T>(
        Func<CancellationToken, Task<T>> op,
        int timeoutMs,
        string stageName,
        List<string> degraded,
        CancellationToken ct) where T : class
        => WithTimeoutAsyncCore(op, timeoutMs, stageName, degraded, _logger, IsTransientStageException, ct);

    /// <summary>
    /// Test-facing entry point. Uses an "all-transient" filter so tests can assert
    /// error-outcome emission without importing the internal IsTransientStageException.
    /// </summary>
    internal static Task<T?> RunWithTimeoutAsyncForTests<T>(
        Func<CancellationToken, Task<T>> op,
        int timeoutMs,
        string stageName,
        List<string> degraded,
        CancellationToken ct = default) where T : class
        => WithTimeoutAsyncCore(op, timeoutMs, stageName, degraded, logger: null, isTransient: _ => true, ct);

    private static async Task<T?> WithTimeoutAsyncCore<T>(
        Func<CancellationToken, Task<T>> op,
        int timeoutMs,
        string stageName,
        List<string> degraded,
        ILogger? logger,
        Func<Exception, bool> isTransient,
        CancellationToken ct) where T : class
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(timeoutMs);
        var sw = Stopwatch.StartNew();
        string outcome = RagStageOutcome.Success;
        try
        {
            return await op(cts.Token);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Caller cancelled — propagate so the whole chat turn can abort cleanly.
            outcome = RagStageOutcome.Timeout;
            throw;
        }
        catch (OperationCanceledException)
        {
            // Stage exceeded the per-stage budget — degrade and continue.
            outcome = RagStageOutcome.Timeout;
            degraded.Add(stageName);
            logger?.LogWarning("RAG stage '{Stage}' timed out after {TimeoutMs}ms", stageName, timeoutMs);
            return null;
        }
        catch (Exception ex) when (isTransient(ex))
        {
            outcome = RagStageOutcome.Error;
            degraded.Add(stageName);
            logger?.LogError(ex, "RAG stage '{Stage}' failed", stageName);
            return null;
        }
        finally
        {
            sw.Stop();
            AiRagMetrics.StageDuration.Record(
                sw.Elapsed.TotalMilliseconds,
                new KeyValuePair<string, object?>("rag.stage", stageName));
            AiRagMetrics.StageOutcome.Add(
                1,
                new KeyValuePair<string, object?>("rag.stage", stageName),
                new KeyValuePair<string, object?>("rag.outcome", outcome));
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
