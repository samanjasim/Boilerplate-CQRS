using FluentAssertions;
using Starter.Module.AI.Application.Services.Retrieval;
using Starter.Module.AI.Infrastructure.Retrieval.Diversification;
using Xunit;

namespace Starter.Api.Tests.Ai.Retrieval.Diversification;

public class MmrDiversifierTests
{
    [Fact]
    public void Lambda_one_is_pure_relevance_and_short_circuits()
    {
        var id1 = Guid.NewGuid(); var id2 = Guid.NewGuid(); var id3 = Guid.NewGuid();
        var hits = new[]
        {
            new HybridHit(id1, 0m, 0m, 0.9m),
            new HybridHit(id2, 0m, 0m, 0.8m),
            new HybridHit(id3, 0m, 0m, 0.7m),
        };
        var embeddings = new Dictionary<Guid, float[]>
        {
            [id1] = new[] { 1f, 0f }, [id2] = new[] { 1f, 0f }, [id3] = new[] { 1f, 0f }
        };

        var result = MmrDiversifier.Diversify(hits, embeddings, lambda: 1.0, topK: 2);

        result.Select(h => h.ChunkId).Should().ContainInOrder(id1, id2);
    }

    [Fact]
    public void Suppresses_near_duplicates_at_moderate_lambda()
    {
        var a = Guid.NewGuid(); var b = Guid.NewGuid(); var c = Guid.NewGuid(); var d = Guid.NewGuid();
        var hits = new[]
        {
            new HybridHit(a, 0m, 0m, 0.99m),
            new HybridHit(b, 0m, 0m, 0.97m),
            new HybridHit(c, 0m, 0m, 0.95m),
            new HybridHit(d, 0m, 0m, 0.50m),
        };
        var embeddings = new Dictionary<Guid, float[]>
        {
            [a] = new[] { 1f, 0f },
            [b] = new[] { 1f, 0f },
            [c] = new[] { 1f, 0f },
            [d] = new[] { 0f, 1f },
        };

        var result = MmrDiversifier.Diversify(hits, embeddings, lambda: 0.5, topK: 2);

        result.Select(h => h.ChunkId).Should().Contain(a).And.Contain(d);
    }

    [Fact]
    public void Empty_input_returns_empty()
    {
        var result = MmrDiversifier.Diversify(
            Array.Empty<HybridHit>(),
            new Dictionary<Guid, float[]>(),
            lambda: 0.5,
            topK: 5);
        result.Should().BeEmpty();
    }

    [Fact]
    public void TopK_greater_than_pool_returns_pool()
    {
        var id1 = Guid.NewGuid(); var id2 = Guid.NewGuid();
        var hits = new[]
        {
            new HybridHit(id1, 0m, 0m, 0.9m),
            new HybridHit(id2, 0m, 0m, 0.8m),
        };
        var embeddings = new Dictionary<Guid, float[]>
        {
            [id1] = new[] { 1f, 0f }, [id2] = new[] { 0f, 1f }
        };

        var result = MmrDiversifier.Diversify(hits, embeddings, lambda: 0.5, topK: 5);

        result.Should().HaveCount(2);
    }

    [Fact]
    public void Drops_hits_with_missing_embedding()
    {
        var id1 = Guid.NewGuid(); var id2 = Guid.NewGuid(); var id3 = Guid.NewGuid();
        var hits = new[]
        {
            new HybridHit(id1, 0m, 0m, 0.9m),
            new HybridHit(id2, 0m, 0m, 0.8m),
            new HybridHit(id3, 0m, 0m, 0.7m),
        };
        var embeddings = new Dictionary<Guid, float[]>
        {
            [id1] = new[] { 1f, 0f },
            [id3] = new[] { 0f, 1f },
        };

        var result = MmrDiversifier.Diversify(hits, embeddings, lambda: 0.5, topK: 5);

        result.Select(h => h.ChunkId).Should().BeEquivalentTo(new[] { id1, id3 });
    }

    [Fact]
    public void Lambda_zero_prefers_diverse_pick_over_second_most_relevant()
    {
        var a = Guid.NewGuid(); var b = Guid.NewGuid(); var c = Guid.NewGuid();
        var hits = new[]
        {
            new HybridHit(a, 0m, 0m, 0.9m),
            new HybridHit(b, 0m, 0m, 0.8m),
            new HybridHit(c, 0m, 0m, 0.3m),
        };
        var embeddings = new Dictionary<Guid, float[]>
        {
            [a] = new[] { 1f, 0f },
            [b] = new[] { 0.99f, 0.01f },
            [c] = new[] { 0f, 1f },
        };

        var result = MmrDiversifier.Diversify(hits, embeddings, lambda: 0.0, topK: 2);

        result.Select(h => h.ChunkId).Should().ContainInOrder(a, c);
    }

    [Fact]
    public void Clamps_lambda_to_valid_range()
    {
        var id1 = Guid.NewGuid();
        var hits = new[] { new HybridHit(id1, 0m, 0m, 0.9m) };
        var embeddings = new Dictionary<Guid, float[]> { [id1] = new[] { 1f, 0f } };

        var r1 = MmrDiversifier.Diversify(hits, embeddings, lambda: 5.0, topK: 1);
        var r2 = MmrDiversifier.Diversify(hits, embeddings, lambda: -1.0, topK: 1);

        r1.Should().ContainSingle();
        r2.Should().ContainSingle();
    }
}
