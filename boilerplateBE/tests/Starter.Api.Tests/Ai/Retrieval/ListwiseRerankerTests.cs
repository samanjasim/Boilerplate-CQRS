using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Starter.Api.Tests.Ai.Fakes;
using Starter.Module.AI.Application.Services.Retrieval;
using Starter.Module.AI.Domain.Entities;
using Starter.Module.AI.Infrastructure.Retrieval;
using Starter.Module.AI.Infrastructure.Retrieval.Reranking;
using Starter.Module.AI.Infrastructure.Settings;
using Xunit;

namespace Starter.Api.Tests.Ai.Retrieval;

public sealed class ListwiseRerankerTests
{
    private static ListwiseReranker Build(
        FakeAiProvider provider,
        FakeCacheService cache,
        AiRagSettings? settings = null)
    {
        var factory = new FakeAiProviderFactory(provider);
        return new ListwiseReranker(
            factory,
            cache,
            new FakeAiModelDefaultResolver(),
            new FakeAiProviderCredentialResolver(),
            Options.Create(settings ?? new AiRagSettings()),
            NullLogger<ListwiseReranker>.Instance);
    }

    private static (List<HybridHit> hits, List<AiDocumentChunk> chunks, Guid id0, Guid id1, Guid id2) Build3Candidates()
    {
        var id0 = Guid.NewGuid();
        var id1 = Guid.NewGuid();
        var id2 = Guid.NewGuid();
        var hits = new List<HybridHit>
        {
            new(id0, SemanticScore: 0.9m, KeywordScore: 0.1m, HybridScore: 0.5m),
            new(id1, SemanticScore: 0.8m, KeywordScore: 0.2m, HybridScore: 0.4m),
            new(id2, SemanticScore: 0.7m, KeywordScore: 0.3m, HybridScore: 0.3m),
        };
        var chunks = new List<AiDocumentChunk>
        {
            TestChunkFactory.Build(pointId: id0, content: "first excerpt about photosynthesis", pageNumber: 1),
            TestChunkFactory.Build(pointId: id1, content: "second excerpt about respiration", pageNumber: 1),
            TestChunkFactory.Build(pointId: id2, content: "third excerpt about chlorophyll", pageNumber: 1),
        };
        return (hits, chunks, id0, id1, id2);
    }

    [Fact]
    public async Task HappyPath_ReordersByLlmOutput()
    {
        var provider = new FakeAiProvider();
        provider.EnqueueContent("[2, 0, 1]");
        var cache = new FakeCacheService();
        var svc = Build(provider, cache);
        var (hits, chunks, id0, id1, id2) = Build3Candidates();

        var result = await svc.RerankAsync(Guid.Empty, "what is photosynthesis?", hits, chunks, CancellationToken.None);

        result.StrategyUsed.Should().Be(RerankStrategy.Listwise);
        result.Ordered.Should().HaveCount(3);
        result.Ordered[0].ChunkId.Should().Be(id2);
        result.Ordered[1].ChunkId.Should().Be(id0);
        result.Ordered[2].ChunkId.Should().Be(id1);
    }

    [Fact]
    public async Task MissingIndices_AreAppendedInRrfOrder()
    {
        var provider = new FakeAiProvider();
        provider.EnqueueContent("[2]");
        var cache = new FakeCacheService();
        var svc = Build(provider, cache);
        var (hits, chunks, id0, id1, id2) = Build3Candidates();

        var result = await svc.RerankAsync(Guid.Empty, "q", hits, chunks, CancellationToken.None);

        result.Ordered.Should().HaveCount(3);
        result.Ordered[0].ChunkId.Should().Be(id2);
        result.Ordered[1].ChunkId.Should().Be(id0);
        result.Ordered[2].ChunkId.Should().Be(id1);
    }

    [Fact]
    public async Task MalformedJson_FallsBackToRrf()
    {
        var provider = new FakeAiProvider();
        provider.EnqueueContent("sorry, I cannot produce indices");
        var cache = new FakeCacheService();
        var svc = Build(provider, cache);
        var (hits, chunks, _, _, _) = Build3Candidates();

        var result = await svc.RerankAsync(Guid.Empty, "q", hits, chunks, CancellationToken.None);

        result.StrategyUsed.Should().Be(RerankStrategy.FallbackRrf);
        result.Ordered.Should().Equal(hits);
    }

    [Fact]
    public async Task ProviderThrows_FallsBackToRrf()
    {
        var provider = new FakeAiProvider();
        provider.EnqueueThrow(new InvalidOperationException("provider down"));
        var cache = new FakeCacheService();
        var svc = Build(provider, cache);
        var (hits, chunks, _, _, _) = Build3Candidates();

        var result = await svc.RerankAsync(Guid.Empty, "q", hits, chunks, CancellationToken.None);

        result.StrategyUsed.Should().Be(RerankStrategy.FallbackRrf);
        result.Ordered.Should().Equal(hits);
    }

    [Fact]
    public async Task CacheHit_AvoidsProviderCall()
    {
        var provider = new FakeAiProvider();
        provider.EnqueueContent("[1, 0, 2]");
        var cache = new FakeCacheService();
        var svc = Build(provider, cache);
        var (hits, chunks, _, _, _) = Build3Candidates();

        _ = await svc.RerankAsync(Guid.Empty, "same query", hits, chunks, CancellationToken.None);
        _ = await svc.RerankAsync(Guid.Empty, "same query", hits, chunks, CancellationToken.None);

        provider.Calls.Should().Be(1);
    }

    [Fact]
    public async Task EmptyCandidates_ReturnsEmptyImmediately()
    {
        var provider = new FakeAiProvider();
        var cache = new FakeCacheService();
        var svc = Build(provider, cache);

        var result = await svc.RerankAsync(
            Guid.Empty,
            "q",
            Array.Empty<HybridHit>(),
            Array.Empty<AiDocumentChunk>(),
            CancellationToken.None);

        result.Ordered.Should().BeEmpty();
        provider.Calls.Should().Be(0);
    }
}
