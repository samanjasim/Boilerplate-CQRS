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

public sealed class PointwiseRerankerTests
{
    private static PointwiseReranker Build(
        FakeAiProvider provider,
        FakeCacheService cache,
        AiRagSettings? settings = null)
    {
        var factory = new FakeAiProviderFactory(provider);
        return new PointwiseReranker(
            factory,
            cache,
            new FakeAiModelDefaultResolver(),
            new FakeAiProviderCredentialResolver(),
            Options.Create(settings ?? new AiRagSettings()),
            NullLogger<PointwiseReranker>.Instance);
    }

    private static (List<HybridHit> hits, List<AiDocumentChunk> chunks) Build(int n)
    {
        var hits = new List<HybridHit>(n);
        var chunks = new List<AiDocumentChunk>(n);
        for (var i = 0; i < n; i++)
        {
            var id = Guid.NewGuid();
            hits.Add(new HybridHit(id, SemanticScore: 0.5m, KeywordScore: 0.5m, HybridScore: 0.5m));
            chunks.Add(TestChunkFactory.Build(pointId: id, content: $"excerpt-{i}", chunkIndex: i, pageNumber: 1));
        }
        return (hits, chunks);
    }

    [Fact]
    public async Task Scores_ByDescending_Drops_BelowCutoff()
    {
        var provider = new FakeAiProvider();
        // Content-keyed responses — parallel scoring means queue order is not deterministic.
        provider.WhenUserContains("excerpt-0", "{\"score\": 0.9, \"reason\": \"match\"}");
        provider.WhenUserContains("excerpt-1", "{\"score\": 0.2, \"reason\": \"weak\"}");
        provider.WhenUserContains("excerpt-2", "{\"score\": 0.7, \"reason\": \"ok\"}");
        var cache = new FakeCacheService();
        var settings = new AiRagSettings { MinPointwiseScore = 0.3m };
        var svc = Build(provider, cache, settings);
        var (hits, chunks) = Build(3);

        var result = await svc.RerankAsync(Guid.Empty, "q", hits, chunks, CancellationToken.None);

        result.StrategyUsed.Should().Be(RerankStrategy.Pointwise);
        result.Ordered.Should().HaveCount(2);
        result.Ordered[0].ChunkId.Should().Be(hits[0].ChunkId);
        result.Ordered[1].ChunkId.Should().Be(hits[2].ChunkId);
    }

    [Fact]
    public async Task PairCache_ReusesAcrossCalls()
    {
        var provider = new FakeAiProvider();
        provider.WhenUserContains("excerpt-0", "{\"score\": 0.8, \"reason\": \"a\"}");
        provider.WhenUserContains("excerpt-1", "{\"score\": 0.6, \"reason\": \"b\"}");
        var cache = new FakeCacheService();
        var svc = Build(provider, cache);
        var (hits, chunks) = Build(2);

        _ = await svc.RerankAsync(Guid.Empty, "same query", hits, chunks, CancellationToken.None);
        _ = await svc.RerankAsync(Guid.Empty, "same query", hits, chunks, CancellationToken.None);

        provider.Calls.Should().Be(2);
    }

    [Fact]
    public async Task TooManyFailures_FallsBackToRrf()
    {
        var provider = new FakeAiProvider();
        // 4 candidates; excerpt-0 succeeds, excerpts 1-3 throw → ratio 0.75 > 0.25
        provider.WhenUserContains("excerpt-0", "{\"score\": 0.9, \"reason\": \"ok\"}");
        provider.WhenUserContainsThrow("excerpt-1", new InvalidOperationException("boom"));
        provider.WhenUserContainsThrow("excerpt-2", new InvalidOperationException("boom"));
        provider.WhenUserContainsThrow("excerpt-3", new InvalidOperationException("boom"));
        var cache = new FakeCacheService();
        var settings = new AiRagSettings { PointwiseMaxFailureRatio = 0.25 };
        var svc = Build(provider, cache, settings);
        var (hits, chunks) = Build(4);

        var result = await svc.RerankAsync(Guid.Empty, "q", hits, chunks, CancellationToken.None);

        result.StrategyUsed.Should().Be(RerankStrategy.FallbackRrf);
        result.Ordered.Should().Equal(hits);
        result.CandidatesIn.Should().Be(4);
        result.CandidatesScored.Should().Be(1);
        result.UnusedRatio.Should().Be(0.0);
    }

    [Fact]
    public async Task EmptyCandidates_ReturnsEmpty()
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
        result.StrategyUsed.Should().Be(RerankStrategy.Pointwise);
        provider.Calls.Should().Be(0);
    }

    [Fact]
    public async Task MalformedScore_UsesRrfFallbackScore_ForThatPair()
    {
        var provider = new FakeAiProvider();
        provider.WhenUserContains("excerpt-0", "{\"score\": 0.9, \"reason\": \"ok\"}");
        provider.WhenUserContains("excerpt-1", "not json at all");
        provider.WhenUserContains("excerpt-2", "{\"score\": 0.5, \"reason\": \"ok\"}");
        var cache = new FakeCacheService();
        // Failure ratio tolerated: 1/3 > 0.25 default, so raise the threshold to keep Pointwise.
        var settings = new AiRagSettings
        {
            PointwiseMaxFailureRatio = 0.5,
            MinPointwiseScore = 0.0m,
        };
        var svc = Build(provider, cache, settings);
        var (hits, chunks) = Build(3);

        var result = await svc.RerankAsync(Guid.Empty, "q", hits, chunks, CancellationToken.None);

        result.StrategyUsed.Should().Be(RerankStrategy.Pointwise);
        result.Ordered[0].ChunkId.Should().Be(hits[0].ChunkId);
    }
}
