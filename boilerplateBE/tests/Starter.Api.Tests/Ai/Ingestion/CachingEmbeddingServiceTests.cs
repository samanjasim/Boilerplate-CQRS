using FluentAssertions;
using Microsoft.Extensions.Options;
using Starter.Application.Common.Interfaces;
using Starter.Module.AI.Application.Services.Ingestion;
using Starter.Module.AI.Domain.Enums;
using Starter.Module.AI.Infrastructure.Ingestion;
using Starter.Module.AI.Infrastructure.Settings;
using Xunit;

namespace Starter.Api.Tests.Ai.Ingestion;

public sealed class CachingEmbeddingServiceTests
{
    private sealed class InnerCountingEmbedder : IEmbeddingService
    {
        public int VectorSize => 4;
        public int CallCount { get; private set; }
        public Task<float[][]> EmbedAsync(
            IReadOnlyList<string> texts, CancellationToken ct,
            EmbedAttribution? a = null, AiRequestType r = AiRequestType.Embedding)
        {
            CallCount++;
            return Task.FromResult(texts.Select((t, i) => new float[] { 0.1f * i + t.GetHashCode() * 1e-9f, 0.2f, 0.3f, 0.4f }).ToArray());
        }
    }

    private sealed class MemoryCache : ICacheService
    {
        private readonly Dictionary<string, object?> _store = new();
        public Task<T?> GetAsync<T>(string key, CancellationToken ct = default)
            => Task.FromResult((T?)(_store.TryGetValue(key, out var v) ? v : default));
        public Task SetAsync<T>(string key, T value, TimeSpan? expiration = null, CancellationToken ct = default)
        { _store[key] = value; return Task.CompletedTask; }
        public Task RemoveAsync(string key, CancellationToken ct = default)
        { _store.Remove(key); return Task.CompletedTask; }
        public Task RemoveByPrefixAsync(string prefix, CancellationToken ct = default)
        {
            foreach (var k in _store.Keys.Where(k => k.StartsWith(prefix)).ToList()) _store.Remove(k);
            return Task.CompletedTask;
        }
        public async Task<T> GetOrSetAsync<T>(string key, Func<Task<T>> factory, TimeSpan? expiration = null, CancellationToken ct = default)
        {
            if (_store.TryGetValue(key, out var v) && v is T existing) return existing;
            var created = await factory();
            _store[key] = created;
            return created;
        }
        public Task<bool> ExistsAsync(string key, CancellationToken ct = default) => Task.FromResult(_store.ContainsKey(key));
    }

    private static CachingEmbeddingService Build(InnerCountingEmbedder inner, ICacheService cache) =>
        new(inner, cache, Options.Create(new AiRagSettings { EmbeddingCacheTtlSeconds = 60 }));

    [Fact]
    public async Task Hit_ReturnsCachedVector_WithoutCallingInner()
    {
        var inner = new InnerCountingEmbedder();
        var cache = new MemoryCache();
        var svc = Build(inner, cache);

        var first = await svc.EmbedAsync(["hello"], CancellationToken.None);
        var second = await svc.EmbedAsync(["hello"], CancellationToken.None);

        inner.CallCount.Should().Be(1);
        second[0].Should().BeEquivalentTo(first[0]);
    }

    [Fact]
    public async Task Miss_CallsInner_AndStoresResult()
    {
        var inner = new InnerCountingEmbedder();
        var cache = new MemoryCache();
        var svc = Build(inner, cache);

        var first = await svc.EmbedAsync(["a"], CancellationToken.None);
        var second = await svc.EmbedAsync(["b"], CancellationToken.None);

        inner.CallCount.Should().Be(2);
        first[0].Should().NotBeEquivalentTo(second[0]);
    }

    [Fact]
    public async Task MultiText_BypassesCache()
    {
        var inner = new InnerCountingEmbedder();
        var cache = new MemoryCache();
        var svc = Build(inner, cache);

        _ = await svc.EmbedAsync(["a", "b"], CancellationToken.None);
        _ = await svc.EmbedAsync(["a", "b"], CancellationToken.None);

        inner.CallCount.Should().Be(2);  // cache never engaged for multi-text
    }

    [Fact]
    public async Task SetsVectorSizeFromCacheHit()
    {
        var inner = new InnerCountingEmbedder();
        var cache = new MemoryCache();
        var svc = Build(inner, cache);

        _ = await svc.EmbedAsync(["warmup"], CancellationToken.None);

        var svc2 = Build(new InnerCountingEmbedder(), cache);
        _ = await svc2.EmbedAsync(["warmup"], CancellationToken.None);

        svc2.VectorSize.Should().Be(4);
    }
}
