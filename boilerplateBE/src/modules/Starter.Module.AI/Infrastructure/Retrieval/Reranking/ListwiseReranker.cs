using System.Diagnostics;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Starter.Application.Common.Interfaces;
using Starter.Module.AI.Application.Services.Retrieval;
using Starter.Module.AI.Domain.Entities;
using Starter.Module.AI.Infrastructure.Providers;
using Starter.Module.AI.Infrastructure.Retrieval.Json;
using Starter.Module.AI.Infrastructure.Settings;

namespace Starter.Module.AI.Infrastructure.Retrieval.Reranking;

internal sealed class ListwiseReranker
{
    private readonly IAiProviderFactory _factory;
    private readonly ICacheService _cache;
    private readonly AiRagSettings _settings;
    private readonly ILogger<ListwiseReranker> _logger;

    public ListwiseReranker(
        IAiProviderFactory factory,
        ICacheService cache,
        IOptions<AiRagSettings> settings,
        ILogger<ListwiseReranker> logger)
    {
        _factory = factory;
        _cache = cache;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<RerankResult> RerankAsync(
        string query,
        IReadOnlyList<HybridHit> candidates,
        IReadOnlyList<AiDocumentChunk> candidateChunks,
        CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        if (candidates.Count == 0)
            return new RerankResult(candidates, RerankStrategy.Listwise, RerankStrategy.Listwise, 0, 0, 0, 0, 0, 0, 0.0);

        var key = BuildCacheKey(query, candidates);
        var cached = await _cache.GetAsync<List<int>>(key, ct);
        IReadOnlyList<int>? indices = cached;
        int tokensIn = 0, tokensOut = 0;
        int cacheHits = cached is null ? 0 : 1;

        if (indices is null)
        {
            try
            {
                var provider = _factory.CreateDefault();
                var (messages, opts) = BuildPrompt(query, candidates, candidateChunks);
                var completion = await provider.ChatAsync(messages, opts, ct);
                tokensIn = completion.InputTokens;
                tokensOut = completion.OutputTokens;
                if (completion.Content is null || !JsonArrayExtractor.TryExtractInts(completion.Content, out var parsed))
                {
                    _logger.LogWarning("ListwiseReranker: output did not contain a JSON int array");
                    return Fallback(candidates, sw);
                }
                indices = parsed;
                if (_settings.RerankCacheTtlSeconds > 0)
                    await _cache.SetAsync(key, parsed.ToList(),
                        TimeSpan.FromSeconds(_settings.RerankCacheTtlSeconds), ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "ListwiseReranker: provider call failed; falling back to RRF order");
                return Fallback(candidates, sw);
            }
        }

        var ordered = ApplyIndices(candidates, indices);
        return new RerankResult(
            Ordered: ordered,
            StrategyRequested: RerankStrategy.Listwise,
            StrategyUsed: RerankStrategy.Listwise,
            CandidatesIn: candidates.Count,
            CandidatesScored: candidates.Count,
            CacheHits: cacheHits,
            LatencyMs: sw.ElapsedMilliseconds,
            TokensIn: tokensIn,
            TokensOut: tokensOut,
            UnusedRatio: 0.0);
    }

    private (List<AiChatMessage> messages, AiChatOptions opts) BuildPrompt(
        string query, IReadOnlyList<HybridHit> candidates, IReadOnlyList<AiDocumentChunk> chunks)
    {
        var system =
            "You rank document excerpts by relevance to a query. " +
            "You may see queries and excerpts in Arabic or English. " +
            "Respond with a JSON array of integer indices, most relevant first. " +
            "Include every input index exactly once. No commentary.";

        var sb = new StringBuilder();
        sb.AppendLine($"Query: {query}");
        sb.AppendLine();
        sb.AppendLine("Excerpts:");
        var byPointId = chunks.ToDictionary(c => c.QdrantPointId);
        for (var i = 0; i < candidates.Count; i++)
        {
            var c = byPointId[candidates[i].ChunkId];
            var excerpt = c.Content.Length > 500 ? c.Content[..500] : c.Content;
            sb.AppendLine($"[{i}] (page {c.PageNumber ?? 0}) {excerpt}");
        }

        var model = _settings.RerankerModel ?? _factory.GetDefaultChatModelId();
        var opts = new AiChatOptions(
            Model: model,
            Temperature: 0.0,
            MaxTokens: 128,
            SystemPrompt: system);

        return (new List<AiChatMessage> { new("user", sb.ToString()) }, opts);
    }

    private static IReadOnlyList<HybridHit> ApplyIndices(
        IReadOnlyList<HybridHit> candidates, IReadOnlyList<int> indices)
    {
        var ordered = new List<HybridHit>(candidates.Count);
        var seen = new HashSet<int>();
        foreach (var idx in indices)
        {
            if (idx < 0 || idx >= candidates.Count) continue;
            if (!seen.Add(idx)) continue;
            ordered.Add(candidates[idx]);
        }
        for (var i = 0; i < candidates.Count; i++)
        {
            if (!seen.Contains(i)) ordered.Add(candidates[i]);
        }
        return ordered;
    }

    private static RerankResult Fallback(IReadOnlyList<HybridHit> candidates, Stopwatch sw) =>
        new(candidates, RerankStrategy.Listwise, RerankStrategy.FallbackRrf, candidates.Count, 0, 0, sw.ElapsedMilliseconds, 0, 0, 0.0);

    private string BuildCacheKey(string query, IReadOnlyList<HybridHit> candidates)
    {
        var provider = _factory.GetDefaultProviderType().ToString();
        var model = _settings.RerankerModel ?? _factory.GetDefaultChatModelId();
        return RagCacheKeys.ListwiseRerank(provider, model, query, candidates);
    }
}
