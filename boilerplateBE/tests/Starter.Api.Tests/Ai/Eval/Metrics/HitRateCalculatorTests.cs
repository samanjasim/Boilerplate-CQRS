using FluentAssertions;
using Starter.Module.AI.Infrastructure.Eval.Metrics;
using Xunit;

namespace Starter.Api.Tests.Ai.Eval.Metrics;

public sealed class HitRateCalculatorTests
{
    [Fact]
    public void HitInTopK_ReturnsOne()
    {
        var a = Guid.NewGuid(); var b = Guid.NewGuid();
        HitRateCalculator.Compute(
            retrieved: new[] { a, b },
            relevant: new HashSet<Guid> { b },
            k: 2).Should().Be(1.0);
    }

    [Fact]
    public void NoHitInTopK_ReturnsZero()
    {
        var a = Guid.NewGuid(); var b = Guid.NewGuid(); var c = Guid.NewGuid();
        HitRateCalculator.Compute(
            retrieved: new[] { a, b },
            relevant: new HashSet<Guid> { c },
            k: 2).Should().Be(0.0);
    }

    [Fact]
    public void MeanAcrossQuestions_ReturnsFraction()
    {
        HitRateCalculator.Mean(new[] { 1.0, 0.0, 1.0, 1.0 })
            .Should().BeApproximately(0.75, 1e-9);
    }

    [Fact]
    public void MeanOfEmpty_IsZero()
    {
        HitRateCalculator.Mean(Array.Empty<double>()).Should().Be(0.0);
    }
}
