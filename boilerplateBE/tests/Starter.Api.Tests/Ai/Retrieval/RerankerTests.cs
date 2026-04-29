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

public sealed class RerankerTests
{
    private static (Reranker reranker, FakeAiProvider provider, FakeCacheService cache) BuildReranker(
        AiRagSettings settings,
        FakeAiProvider? scriptedChat = null)
    {
        var provider = scriptedChat ?? new FakeAiProvider();
        var factory = new FakeAiProviderFactory(provider);
        var cache = new FakeCacheService();
        var opts = Options.Create(settings);

        var listwise = new ListwiseReranker(
            factory,
            cache,
            new FakeAiModelDefaultResolver(),
            new FakeAiProviderCredentialResolver(),
            opts,
            NullLogger<ListwiseReranker>.Instance);
        var pointwise = new PointwiseReranker(
            factory,
            cache,
            new FakeAiModelDefaultResolver(),
            new FakeAiProviderCredentialResolver(),
            opts,
            NullLogger<PointwiseReranker>.Instance);
        var selector = new RerankStrategySelector(settings);

        var reranker = new Reranker(
            selector,
            listwise,
            pointwise,
            opts,
            NullLogger<Reranker>.Instance);
        return (reranker, provider, cache);
    }

    private static (List<HybridHit> hits, List<AiDocumentChunk> chunks) FakeBatch(int n)
    {
        var hits = new List<HybridHit>(n);
        var chunks = new List<AiDocumentChunk>(n);
        for (var i = 0; i < n; i++)
        {
            var pointId = Guid.NewGuid();
            var rrf = 1.0m - i * 0.01m;
            hits.Add(new HybridHit(pointId, SemanticScore: rrf, KeywordScore: rrf, HybridScore: rrf));
            chunks.Add(TestChunkFactory.Build(pointId: pointId, content: $"excerpt-{i}", chunkIndex: i, pageNumber: 1));
        }
        return (hits, chunks);
    }

    [Fact]
    public async Task Off_strategy_passes_through_unchanged()
    {
        var settings = new AiRagSettings { RerankStrategy = RerankStrategy.Off };
        var (reranker, provider, _) = BuildReranker(settings);
        var (hits, chunks) = FakeBatch(3);
        var ctx = new RerankContext(QuestionType: null, StrategyOverride: null, TenantId: Guid.Empty);

        var result = await reranker.RerankAsync("q", hits, chunks, ctx, CancellationToken.None);

        result.StrategyRequested.Should().Be(RerankStrategy.Off);
        result.StrategyUsed.Should().Be(RerankStrategy.Off);
        result.Ordered.Should().Equal(hits);
        result.CandidatesIn.Should().Be(3);
        provider.Calls.Should().Be(0);
    }

    [Fact]
    public async Task Listwise_failure_falls_back_to_rrf()
    {
        var settings = new AiRagSettings { RerankStrategy = RerankStrategy.Listwise };
        var provider = new FakeAiProvider();
        provider.EnqueueAllFail("listwise provider down");
        var (reranker, _, _) = BuildReranker(settings, provider);
        var (hits, chunks) = FakeBatch(3);
        var ctx = new RerankContext(QuestionType: null, StrategyOverride: null, TenantId: Guid.Empty);

        var result = await reranker.RerankAsync("q", hits, chunks, ctx, CancellationToken.None);

        result.StrategyRequested.Should().Be(RerankStrategy.Listwise);
        result.StrategyUsed.Should().Be(RerankStrategy.FallbackRrf);
        result.Ordered.Should().Equal(hits);
    }

    [Fact]
    public async Task Pointwise_failure_falls_back_to_listwise_then_rrf()
    {
        var settings = new AiRagSettings
        {
            RerankStrategy = RerankStrategy.Pointwise,
            PointwiseMaxFailureRatio = 0.0,
        };
        var provider = new FakeAiProvider();
        provider.EnqueueAllFail("all chat calls fail");
        var (reranker, _, _) = BuildReranker(settings, provider);
        var (hits, chunks) = FakeBatch(3);
        var ctx = new RerankContext(QuestionType: null, StrategyOverride: null, TenantId: Guid.Empty);

        var result = await reranker.RerankAsync("q", hits, chunks, ctx, CancellationToken.None);

        result.StrategyRequested.Should().Be(RerankStrategy.Pointwise);
        result.StrategyUsed.Should().Be(RerankStrategy.FallbackRrf);
        result.Ordered.Should().Equal(hits);
    }

    [Fact]
    public async Task Context_override_wins()
    {
        var settings = new AiRagSettings { RerankStrategy = RerankStrategy.Listwise };
        var (reranker, provider, _) = BuildReranker(settings);
        var (hits, chunks) = FakeBatch(3);
        var ctx = new RerankContext(QuestionType: null, StrategyOverride: RerankStrategy.Off, TenantId: Guid.Empty);

        var result = await reranker.RerankAsync("q", hits, chunks, ctx, CancellationToken.None);

        result.StrategyRequested.Should().Be(RerankStrategy.Off);
        result.StrategyUsed.Should().Be(RerankStrategy.Off);
        result.Ordered.Should().Equal(hits);
        provider.Calls.Should().Be(0);
    }
}
