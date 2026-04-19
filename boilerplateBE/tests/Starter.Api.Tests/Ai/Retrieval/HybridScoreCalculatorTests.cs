using FluentAssertions;
using Starter.Module.AI.Application.Services.Retrieval;
using Starter.Module.AI.Infrastructure.Retrieval;
using Xunit;

namespace Starter.Api.Tests.Ai.Retrieval;

public sealed class HybridScoreCalculatorTests
{
    [Fact]
    public void Combine_Blends_With_Alpha()
    {
        var semantic = new List<VectorSearchHit>
        {
            new(Guid.Parse("00000000-0000-0000-0000-000000000001"), 0.9m),
            new(Guid.Parse("00000000-0000-0000-0000-000000000002"), 0.1m)
        };
        var keyword = new List<KeywordSearchHit>
        {
            new(Guid.Parse("00000000-0000-0000-0000-000000000001"), 0.5m),
            new(Guid.Parse("00000000-0000-0000-0000-000000000003"), 1.5m)
        };

        var merged = HybridScoreCalculator.Combine(semantic, keyword, alpha: 0.7m, minScore: 0m);

        merged.Select(m => m.ChunkId).Should().ContainInOrder(
            Guid.Parse("00000000-0000-0000-0000-000000000001"),
            Guid.Parse("00000000-0000-0000-0000-000000000003"),
            Guid.Parse("00000000-0000-0000-0000-000000000002"));
        merged[0].HybridScore.Should().BeApproximately(0.70m, 0.001m);
        merged[1].HybridScore.Should().BeApproximately(0.30m, 0.001m);
        merged[2].HybridScore.Should().BeApproximately(0.00m, 0.001m);
    }

    [Fact]
    public void Combine_Filters_By_MinScore()
    {
        var semantic = new List<VectorSearchHit> { new(Guid.NewGuid(), 0.2m) };
        var keyword = new List<KeywordSearchHit>();

        var merged = HybridScoreCalculator.Combine(semantic, keyword, alpha: 0.7m, minScore: 0.8m);

        merged.Should().BeEmpty();
    }

    [Fact]
    public void Combine_Handles_Empty_Inputs()
    {
        var merged = HybridScoreCalculator.Combine(
            new List<VectorSearchHit>(),
            new List<KeywordSearchHit>(),
            alpha: 0.7m,
            minScore: 0m);

        merged.Should().BeEmpty();
    }

    [Fact]
    public void Combine_Single_Hit_On_Each_Side_Normalises_To_One()
    {
        var semantic = new List<VectorSearchHit> { new(Guid.Parse("00000000-0000-0000-0000-000000000001"), 0.5m) };
        var keyword = new List<KeywordSearchHit> { new(Guid.Parse("00000000-0000-0000-0000-000000000001"), 0.5m) };

        var merged = HybridScoreCalculator.Combine(semantic, keyword, alpha: 0.7m, minScore: 0m);

        merged.Should().HaveCount(1);
        merged[0].HybridScore.Should().BeApproximately(1.0m, 0.001m);
    }
}
