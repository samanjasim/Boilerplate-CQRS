using FluentAssertions;
using Starter.Module.AI.Application.Services.Retrieval;
using Starter.Module.AI.Infrastructure.Retrieval;
using Xunit;

namespace Starter.Api.Tests.Ai.Retrieval;

public sealed class HybridScoreCalculatorTests
{
    private static readonly Guid A = Guid.Parse("00000000-0000-0000-0000-00000000000A");
    private static readonly Guid B = Guid.Parse("00000000-0000-0000-0000-00000000000B");
    private static readonly Guid C = Guid.Parse("00000000-0000-0000-0000-00000000000C");
    private static readonly Guid D = Guid.Parse("00000000-0000-0000-0000-00000000000D");

    private static IReadOnlyList<IReadOnlyList<VectorSearchHit>> Vec(params (Guid id, decimal score)[][] lists)
        => lists.Select(l => (IReadOnlyList<VectorSearchHit>)l.Select(t => new VectorSearchHit(t.id, t.score)).ToList()).ToList();

    private static IReadOnlyList<IReadOnlyList<KeywordSearchHit>> Kw(params (Guid id, decimal score)[][] lists)
        => lists.Select(l => (IReadOnlyList<KeywordSearchHit>)l.Select(t => new KeywordSearchHit(t.id, t.score)).ToList()).ToList();

    [Fact]
    public void Combine_SingleVector_SingleKeyword_DifferentIds_BothReturned()
    {
        var result = HybridScoreCalculator.Combine(
            Vec(new[] { (A, 0.9m) }),
            Kw(new[] { (B, 0.5m) }),
            vectorWeight: 1m, keywordWeight: 1m, rrfK: 60, minScore: 0m);

        result.Should().HaveCount(2);
        result.Select(h => h.ChunkId).Should().BeEquivalentTo(new[] { A, B });
    }

    [Fact]
    public void Combine_SameId_InBothLists_ScoresSum()
    {
        var result = HybridScoreCalculator.Combine(
            Vec(new[] { (A, 0.9m), (B, 0.5m) }),
            Kw(new[] { (A, 0.7m), (B, 0.4m) }),
            vectorWeight: 1m, keywordWeight: 1m, rrfK: 60, minScore: 0m);

        // A appears rank-0 in both lists; B rank-1 in both. A's RRF > B's RRF.
        result.Should().HaveCount(2);
        result[0].ChunkId.Should().Be(A);
        result[1].ChunkId.Should().Be(B);
        result[0].HybridScore.Should().BeGreaterThan(result[1].HybridScore);
    }

    [Fact]
    public void Combine_EmptyKeyword_ReturnsVectorListOrdered()
    {
        var result = HybridScoreCalculator.Combine(
            Vec(new[] { (A, 0.9m), (B, 0.5m) }),
            Kw(Array.Empty<(Guid, decimal)>()),
            vectorWeight: 1m, keywordWeight: 1m, rrfK: 60, minScore: 0m);

        result.Select(h => h.ChunkId).Should().ContainInOrder(A, B);
    }

    [Fact]
    public void Combine_EmptyVector_ReturnsKeywordListOrdered()
    {
        var result = HybridScoreCalculator.Combine(
            Vec(Array.Empty<(Guid, decimal)>()),
            Kw(new[] { (B, 0.5m), (A, 0.3m) }),
            vectorWeight: 1m, keywordWeight: 1m, rrfK: 60, minScore: 0m);

        result.Select(h => h.ChunkId).Should().ContainInOrder(B, A);
    }

    [Fact]
    public void Combine_AllEmpty_ReturnsEmpty()
    {
        var result = HybridScoreCalculator.Combine(
            Vec(Array.Empty<(Guid, decimal)>()),
            Kw(Array.Empty<(Guid, decimal)>()),
            vectorWeight: 1m, keywordWeight: 1m, rrfK: 60, minScore: 0m);

        result.Should().BeEmpty();
    }

    [Fact]
    public void Combine_MultipleVectorLists_RanksAccumulate()
    {
        // A is rank-0 in both variant lists; B only in list #1 at rank-1.
        // A should win because it gets two high contributions.
        var result = HybridScoreCalculator.Combine(
            Vec(
                new[] { (A, 0.9m), (B, 0.8m) },
                new[] { (A, 0.9m), (C, 0.7m) }),
            Kw(Array.Empty<(Guid, decimal)>()),
            vectorWeight: 1m, keywordWeight: 1m, rrfK: 60, minScore: 0m);

        result[0].ChunkId.Should().Be(A);
    }

    [Fact]
    public void Combine_MinScoreFilter_DropsLowScores()
    {
        // With only one hit, A's RRF = 1/61. Set a min above that.
        var result = HybridScoreCalculator.Combine(
            Vec(new[] { (A, 0.9m) }),
            Kw(Array.Empty<(Guid, decimal)>()),
            vectorWeight: 1m, keywordWeight: 1m, rrfK: 60, minScore: 1m);

        result.Should().BeEmpty();
    }

    [Fact]
    public void Combine_TieBreakByChunkId_IsDeterministic()
    {
        // A and B both appear at rank-0 in one of two symmetric lists → equal scores → tiebreak on id.
        var result = HybridScoreCalculator.Combine(
            Vec(
                new[] { (A, 0.9m) },
                new[] { (B, 0.9m) }),
            Kw(Array.Empty<(Guid, decimal)>()),
            vectorWeight: 1m, keywordWeight: 1m, rrfK: 60, minScore: 0m);

        result[0].ChunkId.Should().Be(A);  // A < B by Guid ordering
        result[1].ChunkId.Should().Be(B);
    }

    [Fact]
    public void Combine_SemanticScore_ReportsMaxRawAcrossLists()
    {
        var result = HybridScoreCalculator.Combine(
            Vec(
                new[] { (A, 0.7m) },
                new[] { (A, 0.95m) }),
            Kw(Array.Empty<(Guid, decimal)>()),
            vectorWeight: 1m, keywordWeight: 1m, rrfK: 60, minScore: 0m);

        result[0].SemanticScore.Should().Be(0.95m);
    }

    [Fact]
    public void Combine_KeywordWeightZero_VectorOnly()
    {
        // B scores higher on keyword; A scores on vector only. With keywordWeight=0, A must win.
        var result = HybridScoreCalculator.Combine(
            Vec(new[] { (A, 0.9m) }),
            Kw(new[] { (B, 0.9m) }),
            vectorWeight: 1m, keywordWeight: 0m, rrfK: 60, minScore: 0m);

        result[0].ChunkId.Should().Be(A);
    }
}
