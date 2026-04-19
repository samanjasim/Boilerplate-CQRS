using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Starter.Module.AI.Application.Services.Ingestion;
using Starter.Module.AI.Application.Services.Retrieval;
using Starter.Module.AI.Domain.Entities;
using Starter.Module.AI.Domain.Enums;
using Starter.Module.AI.Infrastructure.Ingestion;
using Starter.Module.AI.Infrastructure.Persistence;
using Starter.Module.AI.Infrastructure.Settings;

namespace Starter.Module.AI.Infrastructure.Retrieval;

internal sealed class RagRetrievalService : IRagRetrievalService
{
    private readonly AiDbContext _db;
    private readonly IVectorStore _vectorStore;
    private readonly IKeywordSearchService _keywordSearch;
    private readonly IEmbeddingService _embeddingService;
    private readonly TokenCounter _tokenCounter;
    private readonly AiRagSettings _settings;

    public RagRetrievalService(
        AiDbContext db,
        IVectorStore vectorStore,
        IKeywordSearchService keywordSearch,
        IEmbeddingService embeddingService,
        TokenCounter tokenCounter,
        IOptions<AiRagSettings> settings)
    {
        _db = db;
        _vectorStore = vectorStore;
        _keywordSearch = keywordSearch;
        _embeddingService = embeddingService;
        _tokenCounter = tokenCounter;
        _settings = settings.Value;
    }

    public async Task<RetrievedContext> RetrieveForTurnAsync(
        AiAssistant assistant,
        string latestUserMessage,
        CancellationToken ct)
    {
        if (assistant.RagScope == AiRagScope.None)
            throw new InvalidOperationException(
                "Caller must ensure RagScope != None before invoking retrieval.");

        var tenantId = assistant.TenantId!.Value;
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

        var vectors = await _embeddingService.EmbedAsync([queryText], ct);
        var queryVector = vectors[0];

        var retrievalTopK = _settings.RetrievalTopK;
        var alpha = (decimal)_settings.HybridSearchWeight;
        var minHybrid = minScore ?? _settings.MinHybridScore;

        var vectorHits = await _vectorStore.SearchAsync(tenantId, queryVector, documentFilter, retrievalTopK, ct);
        var keywordHits = await _keywordSearch.SearchAsync(tenantId, queryText, documentFilter, retrievalTopK, ct);

        var mergedHits = HybridScoreCalculator.Combine(vectorHits, keywordHits, alpha, minHybrid);
        var topKHits = mergedHits.Take(topK).ToList();

        if (topKHits.Count == 0)
            return RetrievedContext.Empty;

        var childIds = topKHits.Select(h => h.ChunkId).ToList();
        var children = await _db.AiDocumentChunks
            .AsNoTracking()
            .Where(c => childIds.Contains(c.Id))
            .ToListAsync(ct);

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
            .Select(c => Map(c, scoreMap.GetValueOrDefault(c.Id), docNames))
            .ToList();

        var parentChunks = parentEntities
            .Select(p => Map(p, null, docNames))
            .ToList();

        var (trimmedChildren, trimmedParents, totalTokens, truncated) =
            TrimToBudget(childChunks, parentChunks, _settings.MaxContextTokens);

        return new RetrievedContext(trimmedChildren, trimmedParents, totalTokens, truncated);
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
}
