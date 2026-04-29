using System.Diagnostics;
using System.Text;
using System.Text.Json;
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

internal sealed class PointwiseReranker
{
    private readonly IAiProviderFactory _factory;
    private readonly ICacheService _cache;
    private readonly IAiModelDefaultResolver _modelDefaults;
    private readonly IAiProviderCredentialResolver _providerCredentials;
    private readonly AiRagSettings _settings;
    private readonly ILogger<PointwiseReranker> _logger;

    public PointwiseReranker(
        IAiProviderFactory factory,
        ICacheService cache,
        IAiModelDefaultResolver modelDefaults,
        IAiProviderCredentialResolver providerCredentials,
        IOptions<AiRagSettings> settings,
        ILogger<PointwiseReranker> logger)
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
            return new RerankResult(candidates, RerankStrategy.Pointwise, RerankStrategy.Pointwise, 0, 0, 0, 0, 0, 0, 0.0);

        var byPointId = candidateChunks.ToDictionary(c => c.QdrantPointId);
        var scores = new decimal[candidates.Count];
        var cacheHits = 0;
        var failures = 0;
        var tokensIn = 0;
        var tokensOut = 0;
        var providerState = await ResolveRagHelperProviderAsync(tenantId, _settings.RerankerModel, 0.0, 64, null, ct);
        if (providerState is null)
        {
            EmitReordered(candidates, candidates);
            return new RerankResult(
                Ordered: candidates,
                StrategyRequested: RerankStrategy.Pointwise,
                StrategyUsed: RerankStrategy.FallbackRrf,
                CandidatesIn: candidates.Count,
                CandidatesScored: 0,
                CacheHits: 0,
                LatencyMs: sw.ElapsedMilliseconds,
                TokensIn: 0,
                TokensOut: 0,
                UnusedRatio: 0.0);
        }

        var maxParallel = Math.Max(1, _settings.PointwiseMaxParallelism);
        using var sem = new SemaphoreSlim(maxParallel);
        var tasks = new List<Task>(candidates.Count);

        for (var i = 0; i < candidates.Count; i++)
        {
            var idx = i;
            tasks.Add(Task.Run(async () =>
            {
                await sem.WaitAsync(ct);
                try
                {
                    var hit = candidates[idx];
                    var key = BuildPairKey(tenantId, providerState.Value.ProviderType.ToString(), providerState.Value.Options.Model, query, hit.ChunkId);
                    var cached = await _cache.GetAsync<decimal?>(key, ct);
                    AiRagMetrics.CacheRequests.Add(
                        1,
                        new KeyValuePair<string, object?>("rag.cache", "rerank"),
                        new KeyValuePair<string, object?>("rag.hit", cached.HasValue));
                    if (cached.HasValue)
                    {
                        scores[idx] = cached.Value;
                        Interlocked.Increment(ref cacheHits);
                        return;
                    }

                    if (!byPointId.TryGetValue(hit.ChunkId, out var chunk))
                    {
                        scores[idx] = RrfFallbackScore(idx);
                        Interlocked.Increment(ref failures);
                        return;
                    }

                    try
                    {
                        var (messages, systemPrompt) = BuildPrompt(query, chunk);
                        var completion = await providerState.Value.Provider.ChatAsync(
                            messages,
                            providerState.Value.Options with { SystemPrompt = systemPrompt },
                            ct);
                        Interlocked.Add(ref tokensIn, completion.InputTokens);
                        Interlocked.Add(ref tokensOut, completion.OutputTokens);

                        if (completion.Content is null || !TryParseScore(completion.Content, out var score))
                        {
                            _logger.LogWarning("PointwiseReranker: malformed score for chunk {ChunkId}", hit.ChunkId);
                            scores[idx] = RrfFallbackScore(idx);
                            Interlocked.Increment(ref failures);
                            return;
                        }

                        scores[idx] = score;
                        if (_settings.RerankCacheTtlSeconds > 0)
                            await _cache.SetAsync<decimal?>(key, score,
                                TimeSpan.FromSeconds(_settings.RerankCacheTtlSeconds), ct);
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        _logger.LogWarning(ex, "PointwiseReranker: provider call failed for chunk {ChunkId}", hit.ChunkId);
                        scores[idx] = RrfFallbackScore(idx);
                        Interlocked.Increment(ref failures);
                    }
                }
                finally
                {
                    sem.Release();
                }
            }, ct));
        }

        await Task.WhenAll(tasks);

        var failureRatio = (double)failures / candidates.Count;
        if (failureRatio > _settings.PointwiseMaxFailureRatio)
        {
            _logger.LogWarning(
                "PointwiseReranker: failure ratio {Ratio:F2} exceeded {Threshold:F2}; falling back to RRF order",
                failureRatio, _settings.PointwiseMaxFailureRatio);
            EmitReordered(candidates, candidates);
            return new RerankResult(
                Ordered: candidates,
                StrategyRequested: RerankStrategy.Pointwise,
                StrategyUsed: RerankStrategy.FallbackRrf,
                CandidatesIn: candidates.Count,
                CandidatesScored: candidates.Count - failures,
                CacheHits: cacheHits,
                LatencyMs: sw.ElapsedMilliseconds,
                TokensIn: tokensIn,
                TokensOut: tokensOut,
                UnusedRatio: 0.0);
        }

        var orderedIndices = Enumerable.Range(0, candidates.Count)
            .OrderByDescending(i => scores[i])
            .ThenBy(i => i)
            .Where(i => scores[i] >= _settings.MinPointwiseScore)
            .ToList();

        var ordered = orderedIndices.Select(i => candidates[i]).ToList();
        var unusedRatio = candidates.Count == 0
            ? 0.0
            : 1.0 - ((double)ordered.Count / candidates.Count);

        EmitReordered(candidates, ordered);
        return new RerankResult(
            Ordered: ordered,
            StrategyRequested: RerankStrategy.Pointwise,
            StrategyUsed: RerankStrategy.Pointwise,
            CandidatesIn: candidates.Count,
            CandidatesScored: candidates.Count - failures,
            CacheHits: cacheHits,
            LatencyMs: sw.ElapsedMilliseconds,
            TokensIn: tokensIn,
            TokensOut: tokensOut,
            UnusedRatio: unusedRatio);
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
        string query, AiDocumentChunk chunk)
    {
        var system =
            "You rate how well a document excerpt answers a user query on a scale from 0.0 to 1.0. " +
            "You may see queries and excerpts in Arabic or English. " +
            "Respond ONLY with strict JSON of shape {\"score\": <float 0.0-1.0>, \"reason\": \"<max 60 chars>\"}. " +
            "No commentary outside the JSON.";

        var excerpt = chunk.Content.Length > 500 ? chunk.Content[..500] : chunk.Content;
        var user = new StringBuilder()
            .Append("Query: ").AppendLine(query)
            .Append("Excerpt (page ").Append(chunk.PageNumber ?? 0).Append("): ").AppendLine(excerpt)
            .ToString();

        return (new List<AiChatMessage> { new("user", user) }, system);
    }

    private static bool TryParseScore(string content, out decimal score)
    {
        score = 0m;
        if (!JsonLooseExtractor.TryExtractObject(content, out var obj))
            return false;
        if (!obj.TryGetProperty("score", out var scoreEl))
            return false;

        decimal value;
        if (scoreEl.ValueKind == JsonValueKind.Number)
        {
            value = scoreEl.GetDecimal();
        }
        else if (scoreEl.ValueKind == JsonValueKind.String
            && decimal.TryParse(scoreEl.GetString(), System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var parsed))
        {
            value = parsed;
        }
        else
        {
            return false;
        }

        if (value < 0m) value = 0m;
        if (value > 1m) value = 1m;
        score = value;
        return true;
    }

    private decimal RrfFallbackScore(int rank) => 1m / (_settings.RrfK + rank + 1);

    private string BuildPairKey(Guid tenantId, string provider, string model, string query, Guid chunkId)
    {
        return RagCacheKeys.PointwiseRerank(tenantId, provider, model, query, chunkId);
    }
}
