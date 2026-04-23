using FluentAssertions;
using Starter.Module.AI.Infrastructure.Eval.Metrics;
using Xunit;

namespace Starter.Api.Tests.Ai.Eval.Metrics;

public sealed class PrecisionAtKCalculatorTests
{
    [Fact]
    public void TwoOfFourRelevantInTopK_ReturnsRatio()
    {
        var a = Guid.NewGuid(); var b = Guid.NewGuid();
        var c = Guid.NewGuid(); var d = Guid.NewGuid();
        PrecisionAtKCalculator.Compute(
            retrieved: new[] { a, c, b, d },
            relevant: new HashSet<Guid> { a, b },
            k: 4).Should().BeApproximately(0.5, 1e-9);
    }

    [Fact]
    public void AllRetrievedRelevant_ReturnsOne()
    {
        var a = Guid.NewGuid(); var b = Guid.NewGuid();
        PrecisionAtKCalculator.Compute(
            retrieved: new[] { a, b },
            relevant: new HashSet<Guid> { a, b },
            k: 2).Should().Be(1.0);
    }

    [Fact]
    public void EmptyRetrievedOrK0_ReturnsZero()
    {
        var a = Guid.NewGuid();
        PrecisionAtKCalculator.Compute(
            retrieved: Array.Empty<Guid>(),
            relevant: new HashSet<Guid> { a },
            k: 5).Should().Be(0.0);
        PrecisionAtKCalculator.Compute(
            retrieved: new[] { a },
            relevant: new HashSet<Guid> { a },
            k: 0).Should().Be(0.0);
    }

    [Fact]
    public void KGreaterThanRetrievedCount_UsesK_InDenominator()
    {
        var a = Guid.NewGuid();
        PrecisionAtKCalculator.Compute(
            retrieved: new[] { a },
            relevant: new HashSet<Guid> { a },
            k: 5).Should().BeApproximately(0.2, 1e-9);
    }
}
