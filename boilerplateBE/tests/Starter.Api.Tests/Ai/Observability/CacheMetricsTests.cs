using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Starter.Api.Tests.Ai.Fakes;
using Starter.Application.Common.Interfaces;
using Starter.Module.AI.Application.Services.Ingestion;
using Starter.Module.AI.Application.Services.Retrieval;
using Starter.Module.AI.Domain.Entities;
using Starter.Module.AI.Domain.Enums;
using Starter.Module.AI.Infrastructure.Ingestion;
using Starter.Module.AI.Infrastructure.Observability;
using Starter.Module.AI.Infrastructure.Providers;
using Starter.Module.AI.Infrastructure.Retrieval.Classification;
using Starter.Module.AI.Infrastructure.Retrieval.QueryRewriting;
using Starter.Module.AI.Infrastructure.Settings;
using Xunit;

namespace Starter.Api.Tests.Ai.Observability;

[Collection(ObservabilityTestCollection.Name)]
public class CacheMetricsTests
{
    [Fact]
    public async Task Embedding_cache_miss_then_hit_emits_both_counters()
    {
        using var listener = new TestMeterListener(AiRagMetrics.MeterName);

        var cache = new InMemoryCacheStub();
        var inner = new StubEmbedder(new[] { 0.1f, 0.2f, 0.3f });
        var providerFactory = new StubProviderFactory("model-X");
        var settings = Options.Create(new AiRagSettings { EmbeddingCacheTtlSeconds = 60 });

        var sut = new CachingEmbeddingService(inner, cache, providerFactory, settings);

        await sut.EmbedAsync(new[] { "hello" }, CancellationToken.None); // miss
        await sut.EmbedAsync(new[] { "hello" }, CancellationToken.None); // hit

        var rows = listener.Snapshot()
            .Where(m => m.InstrumentName == "rag.cache.requests"
                        && (string?)m.Tags["rag.cache"] == "embed")
            .ToList();
        rows.Should().HaveCount(2);
        rows.Should().ContainSingle(m => (bool?)m.Tags["rag.hit"] == false);
        rows.Should().ContainSingle(m => (bool?)m.Tags["rag.hit"] == true);
    }

    [Fact]
    public async Task Query_rewriter_cache_miss_then_hit_emits_both_counters()
    {
        using var listener = new TestMeterListener(AiRagMetrics.MeterName);

        var provider = new FakeAiProvider();
        provider.EnqueueContent("[\"define pump\", \"pump definition\"]");
        var cache = new FakeCacheService();
        var factory = new FakeAiProviderFactory(provider);
        var settings = Options.Create(new AiRagSettings
        {
            EnableQueryExpansion = true,
            QueryRewriteCacheTtlSeconds = 60,
        });

        var sut = new QueryRewriter(
            factory,
            cache,
            settings,
            NullLogger<QueryRewriter>.Instance);

        // Miss: no cached entry, LLM returns two variants → cache populated.
        await sut.RewriteAsync("what is a pump?", language: "en", CancellationToken.None);
        // Hit: cached entry from previous call is returned.
        await sut.RewriteAsync("what is a pump?", language: "en", CancellationToken.None);

        provider.Calls.Should().Be(1);

        var rows = listener.Snapshot()
            .Where(m => m.InstrumentName == "rag.cache.requests"
                        && (string?)m.Tags["rag.cache"] == "rewrite")
            .ToList();
        rows.Should().HaveCount(2);
        rows.Should().ContainSingle(m => (bool?)m.Tags["rag.hit"] == false);
        rows.Should().ContainSingle(m => (bool?)m.Tags["rag.hit"] == true);
    }

    [Fact]
    public async Task Classifier_cache_miss_then_hit_emits_both_counters()
    {
        using var listener = new TestMeterListener(AiRagMetrics.MeterName);

        var provider = new FakeAiProvider();
        // Seed the provider with a classify label the parser accepts (e.g., "Definition").
        provider.EnqueueContent("Definition");
        var cache = new FakeCacheService();
        var factory = new FakeAiProviderFactory(provider);
        var settings = Options.Create(new AiRagSettings
        {
            QuestionCacheTtlSeconds = 60,
            ApplyArabicNormalization = false,
        });

        var sut = new QuestionClassifier(
            factory,
            cache,
            settings,
            NullLogger<QuestionClassifier>.Instance);

        // Query contains no RegexQuestionClassifier trigger words (no greeting/define/list/show/why/how come/explain/compare),
        // so it falls through to the LLM+cache path.
        const string query = "centrifugal pumps move fluid through impeller rotation";

        await sut.ClassifyAsync(query, CancellationToken.None); // miss
        await sut.ClassifyAsync(query, CancellationToken.None); // hit

        provider.Calls.Should().Be(1);

        var rows = listener.Snapshot()
            .Where(m => m.InstrumentName == "rag.cache.requests"
                        && (string?)m.Tags["rag.cache"] == "classify")
            .ToList();
        rows.Should().HaveCount(2);
        rows.Should().ContainSingle(m => (bool?)m.Tags["rag.hit"] == false);
        rows.Should().ContainSingle(m => (bool?)m.Tags["rag.hit"] == true);
    }

    [Fact]
    public async Task Listwise_reranker_cache_miss_then_hit_emits_both_counters()
    {
        using var listener = new TestMeterListener(AiRagMetrics.MeterName);

        var cache = new Starter.Api.Tests.Ai.Fakes.FakeCacheService();
        var provider = new Starter.Api.Tests.Ai.Fakes.FakeAiProvider();
        provider.EnqueueContent("[1,0,2]");
        var factory = new Starter.Api.Tests.Ai.Fakes.FakeAiProviderFactory(provider);
        var settings = Options.Create(new AiRagSettings { RerankCacheTtlSeconds = 60 });

        var sut = new Starter.Module.AI.Infrastructure.Retrieval.Reranking.ListwiseReranker(
            factory, cache, settings,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<Starter.Module.AI.Infrastructure.Retrieval.Reranking.ListwiseReranker>.Instance);

        var (candidates, chunks) = BuildRerankInputs(count: 3);
        await sut.RerankAsync("q", candidates, chunks, CancellationToken.None); // miss (populates cache)
        await sut.RerankAsync("q", candidates, chunks, CancellationToken.None); // hit

        var rows = listener.Snapshot()
            .Where(m => m.InstrumentName == "rag.cache.requests"
                        && (string?)m.Tags["rag.cache"] == "rerank")
            .ToList();
        rows.Should().HaveCount(2);
        rows.Should().ContainSingle(m => (bool?)m.Tags["rag.hit"] == false);
        rows.Should().ContainSingle(m => (bool?)m.Tags["rag.hit"] == true);
    }

    [Fact]
    public async Task Pointwise_reranker_emits_one_cache_request_per_candidate()
    {
        using var listener = new TestMeterListener(AiRagMetrics.MeterName);

        var cache = new Starter.Api.Tests.Ai.Fakes.FakeCacheService();
        var provider = new Starter.Api.Tests.Ai.Fakes.FakeAiProvider();
        // Three candidates, three LLM calls on the first pass (all misses).
        provider.EnqueueContent("{\"score\":0.8}");
        provider.EnqueueContent("{\"score\":0.4}");
        provider.EnqueueContent("{\"score\":0.9}");
        var factory = new Starter.Api.Tests.Ai.Fakes.FakeAiProviderFactory(provider);
        var settings = Options.Create(new AiRagSettings
        {
            RerankCacheTtlSeconds = 60,
            PointwiseMaxParallelism = 1, // serialize so counts are deterministic
            PointwiseMaxFailureRatio = 1.0,
            MinPointwiseScore = 0.0m,
        });

        var sut = new Starter.Module.AI.Infrastructure.Retrieval.Reranking.PointwiseReranker(
            factory, cache, settings,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<Starter.Module.AI.Infrastructure.Retrieval.Reranking.PointwiseReranker>.Instance);

        var (candidates, chunks) = BuildRerankInputs(count: 3);
        await sut.RerankAsync("q", candidates, chunks, CancellationToken.None); // 3 misses
        await sut.RerankAsync("q", candidates, chunks, CancellationToken.None); // 3 hits

        var rows = listener.Snapshot()
            .Where(m => m.InstrumentName == "rag.cache.requests"
                        && (string?)m.Tags["rag.cache"] == "rerank")
            .ToList();
        rows.Count.Should().Be(6, "3 candidates × 2 calls");
        rows.Count(m => (bool?)m.Tags["rag.hit"] == true).Should().Be(3);
        rows.Count(m => (bool?)m.Tags["rag.hit"] == false).Should().Be(3);
    }

    private static (List<HybridHit> candidates, List<AiDocumentChunk> chunks) BuildRerankInputs(int count)
    {
        var candidates = new List<HybridHit>(count);
        var chunks = new List<AiDocumentChunk>(count);
        for (var i = 0; i < count; i++)
        {
            var id = Guid.NewGuid();
            candidates.Add(new HybridHit(
                ChunkId: id,
                SemanticScore: 0.5m,
                KeywordScore: 0.5m,
                HybridScore: 0.5m));
            chunks.Add(TestChunkFactory.Build(
                pointId: id,
                content: $"excerpt-{i}",
                chunkIndex: i,
                pageNumber: 1));
        }
        return (candidates, chunks);
    }

    private sealed class InMemoryCacheStub : ICacheService
    {
        private readonly Dictionary<string, object?> _store = new();

        public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
            => Task.FromResult((T?)(_store.TryGetValue(key, out var v) ? v : default));

        public Task SetAsync<T>(string key, T value, TimeSpan? expiration = null, CancellationToken cancellationToken = default)
        {
            _store[key] = value;
            return Task.CompletedTask;
        }

        public Task RemoveAsync(string key, CancellationToken cancellationToken = default)
        {
            _store.Remove(key);
            return Task.CompletedTask;
        }

        public Task RemoveByPrefixAsync(string prefix, CancellationToken cancellationToken = default)
        {
            foreach (var k in _store.Keys.Where(k => k.StartsWith(prefix)).ToList())
                _store.Remove(k);
            return Task.CompletedTask;
        }

        public async Task<T> GetOrSetAsync<T>(string key, Func<Task<T>> factory, TimeSpan? expiration = null, CancellationToken cancellationToken = default)
        {
            if (_store.TryGetValue(key, out var v) && v is T existing) return existing;
            var created = await factory();
            _store[key] = created;
            return created;
        }

        public Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
            => Task.FromResult(_store.ContainsKey(key));
    }

    private sealed class StubEmbedder : IEmbeddingService
    {
        private readonly float[] _seed;
        public StubEmbedder(float[] seed) { _seed = seed; }

        public int VectorSize => _seed.Length;

        public Task<float[][]> EmbedAsync(
            IReadOnlyList<string> texts,
            CancellationToken ct,
            EmbedAttribution? attribution = null,
            AiRequestType requestType = AiRequestType.Embedding)
            => Task.FromResult(texts.Select(_ => _seed).ToArray());
    }

    private sealed class StubProviderFactory : IAiProviderFactory
    {
        private readonly string _modelId;
        public StubProviderFactory(string modelId) { _modelId = modelId; }

        public string GetEmbeddingModelId() => _modelId;

        public IAiProvider Create(AiProviderType providerType) => throw new NotImplementedException();
        public AiProviderType GetDefaultProviderType() => throw new NotImplementedException();
        public AiProviderType GetEmbeddingProviderType() => throw new NotImplementedException();
        public IAiProvider CreateDefault() => throw new NotImplementedException();
        public IAiProvider CreateForEmbeddings() => throw new NotImplementedException();
        public string GetDefaultChatModelId() => throw new NotImplementedException();
    }
}
