using FluentAssertions;
using Microsoft.Extensions.Options;
using Starter.Application.Common.Interfaces;
using Starter.Module.AI.Application.Services.Ingestion;
using Starter.Module.AI.Domain.Enums;
using Starter.Module.AI.Infrastructure.Ingestion;
using Starter.Module.AI.Infrastructure.Observability;
using Starter.Module.AI.Infrastructure.Providers;
using Starter.Module.AI.Infrastructure.Settings;
using Xunit;

namespace Starter.Api.Tests.Ai.Observability;

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
