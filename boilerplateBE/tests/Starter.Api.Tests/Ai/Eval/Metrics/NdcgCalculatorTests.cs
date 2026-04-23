using FluentAssertions;
using Starter.Module.AI.Infrastructure.Eval.Metrics;
using Xunit;

namespace Starter.Api.Tests.Ai.Eval.Metrics;

public sealed class NdcgCalculatorTests
{
    [Fact]
    public void IdealRanking_Ndcg_Is_One()
    {
        var a = Guid.NewGuid(); var b = Guid.NewGuid(); var c = Guid.NewGuid();
        NdcgCalculator.Compute(
            retrieved: new[] { a, b, c },
            relevant: new HashSet<Guid> { a, b, c },
            k: 3).Should().BeApproximately(1.0, 1e-9);
    }

    [Fact]
    public void NoRelevantInTopK_Ndcg_Is_Zero()
    {
        var a = Guid.NewGuid(); var b = Guid.NewGuid();
        NdcgCalculator.Compute(
            retrieved: new[] { b },
            relevant: new HashSet<Guid> { a },
            k: 1).Should().Be(0.0);
    }

    [Fact]
    public void RelevantAtSecondPosition_KnownNdcg()
    {
        var a = Guid.NewGuid(); var b = Guid.NewGuid(); var c = Guid.NewGuid();
        NdcgCalculator.Compute(
            retrieved: new[] { b, a, c },
            relevant: new HashSet<Guid> { a },
            k: 3).Should().BeApproximately(1.0 / Math.Log2(3), 1e-9);
    }

    [Fact]
    public void EmptyRelevant_Ndcg_Is_Zero()
    {
        var a = Guid.NewGuid();
        NdcgCalculator.Compute(
            retrieved: new[] { a },
            relevant: new HashSet<Guid>(),
            k: 1).Should().Be(0.0);
    }
}
