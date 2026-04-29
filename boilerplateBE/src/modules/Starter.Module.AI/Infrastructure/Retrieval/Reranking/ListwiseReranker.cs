using System.Diagnostics;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Starter.Abstractions.Ai;
using Starter.Application.Common.Interfaces;
using Starter.Module.AI.Application.Services.Retrieval;
using Starter.Module.AI.Application.Services.Settings;
using Starter.Module.AI.Domain.Entities;
using Starter.Module.AI.Domain.Enums;
using Starter.Module.AI.Infrastructure.Observability;
using Starter.Module.AI.Infrastructure.Providers;
using Starter.Module.AI.Infrastructure.Retrieval.Json;
using Starter.Module.AI.Infrastructure.Settings;

namespace Starter.Module.AI.Infrastructure.Retrieval.Reranking;

internal sealed class ListwiseReranker
{
    private readonly IAiProviderFactory _factory;
    private readonly ICacheService _cache;
    private readonly IAiModelDefaultResolver _modelDefaults;
    private readonly IAiProviderCredentialResolver _providerCredentials;
    private readonly AiRagSettings _settings;
    private readonly ILogger<ListwiseReranker> _logger;

    public ListwiseReranker(
        IAiProviderFactory factory,
        ICacheService cache,
        IAiModelDefaultResolver modelDefaults,
        IAiProviderCredentialResolver providerCredentials,
        IOptions<AiRagSettings> settings,
        ILogger<ListwiseReranker> logger)
    {
        _factory = factory;
        _cache = cache;
        _modelDefaults = modelDefaults;
        _providerCredentials = providerCredentials;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<RerankResult> RerankAsync(
        Guid tenantId,
        string query,
        IReadOnlyList<HybridHit> candidates,
        IReadOnlyList<AiDocumentChunk> candidateChunks,
        CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        if (candidates.Count == 0)
            return new RerankResult(candidates, RerankStrategy.Listwise, RerankStrategy.Listwise, 0, 0, 0, 0, 0, 0, 0.0);

        var providerState = await ResolveRagHelperProviderAsync(tenantId, _settings.RerankerModel, 0.0, 128, null, ct);
        if (providerState is null)
        {
            EmitReordered(candidates, candidates);
            return Fallback(candidates, sw);
        }

        var key = BuildCacheKey(tenantId, providerState.Value.ProviderType.ToString(), providerState.Value.Options.Model, query, candidates);
        var cached = await _cache.GetAsync<List<int>>(key, ct);
        AiRagMetrics.CacheRequests.Add(
            1,
            new KeyValuePair<string, object?>("rag.cache", "rerank"),
            new KeyValuePair<string, object?>("rag.hit", cached is not null));
        IReadOnlyList<int>? indices = cached;
        int tokensIn = 0, tokensOut = 0;
        int cacheHits = cached is null ? 0 : 1;

        if (indices is null)
        {
            try
            {
                var (messages, systemPrompt) = BuildPrompt(query, candidates, candidateChunks);
                var completion = await providerState.Value.Provider.ChatAsync(
                    messages,
                    providerState.Value.Options with { SystemPrompt = systemPrompt },
                    ct);
                tokensIn = completion.InputTokens;
                tokensOut = completion.OutputTokens;
                if (completion.Content is null || !JsonArrayExtractor.TryExtractInts(completion.Content, out var parsed))
                {
                    _logger.LogWarning("ListwiseReranker: output did not contain a JSON int array");
                    EmitReordered(candidates, candidates);
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
                EmitReordered(candidates, candidates);
                return Fallback(candidates, sw);
            }
        }

        var ordered = ApplyIndices(candidates, indices);
        EmitReordered(candidates, ordered);
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

    private async Task<(AiProviderType ProviderType, IAiProvider Provider, AiChatOptions Options)?> ResolveRagHelperProviderAsync(
        Guid tenantId,
        string? overrideModel,
        double temperature,
        int maxTokens,
        string? systemPrompt,
        CancellationToken ct)
    {
        var modelResult = await _modelDefaults.ResolveAsync(
            tenantId,
            AiAgentClass.RagHelper,
            explicitProvider: null,
            explicitModel: overrideModel,
            explicitTemperature: temperature,
            explicitMaxTokens: maxTokens,
            ct);
        if (modelResult.IsFailure)
        {
            _logger.LogWarning("RAG helper model resolution failed: {Error}", modelResult.Error.Description);
            return null;
        }

        var credentialResult = await _providerCredentials.ResolveAsync(tenantId, modelResult.Value.Provider, ct);
        if (credentialResult.IsFailure)
        {
            _logger.LogWarning("RAG helper provider credential resolution failed: {Error}", credentialResult.Error.Description);
            return null;
        }

        var credential = credentialResult.Value;
        var provider = _factory.Create(modelResult.Value.Provider);
        var options = new AiChatOptions(
            Model: modelResult.Value.Model,
            Temperature: modelResult.Value.Temperature,
            MaxTokens: modelResult.Value.MaxTokens,
            SystemPrompt: systemPrompt,
            ApiKey: credential.Secret,
            ProviderCredentialSource: credential.Source,
            ProviderCredentialId: credential.ProviderCredentialId);

        return (modelResult.Value.Provider, provider, options);
    }

    private static void EmitReordered(
        IReadOnlyList<HybridHit> input,
        IReadOnlyList<HybridHit> output)
    {
        var changed = !input.Select(c => c.ChunkId).SequenceEqual(output.Select(c => c.ChunkId));
        AiRagMetrics.RerankReordered.Add(
            1, new KeyValuePair<string, object?>("rag.changed", changed));
    }

    private (List<AiChatMessage> messages, string systemPrompt) BuildPrompt(
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

        return (new List<AiChatMessage> { new("user", sb.ToString()) }, system);
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

    private string BuildCacheKey(Guid tenantId, string provider, string model, string query, IReadOnlyList<HybridHit> candidates)
    {
        return RagCacheKeys.ListwiseRerank(tenantId, provider, model, query, candidates);
    }
}
