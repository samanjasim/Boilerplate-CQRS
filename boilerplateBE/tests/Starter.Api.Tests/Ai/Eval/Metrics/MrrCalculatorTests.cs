using FluentAssertions;
using Starter.Module.AI.Infrastructure.Eval.Metrics;
using Xunit;

namespace Starter.Api.Tests.Ai.Eval.Metrics;

public sealed class MrrCalculatorTests
{
    [Fact]
    public void FirstResultRelevant_ReciprocalIsOne()
    {
        var a = Guid.NewGuid(); var b = Guid.NewGuid();
        MrrCalculator.ReciprocalRank(
            retrieved: new[] { a, b },
            relevant: new HashSet<Guid> { a })
            .Should().Be(1.0);
    }

    [Fact]
    public void ThirdResultFirstRelevant_ReciprocalIsOneThird()
    {
        var a = Guid.NewGuid(); var b = Guid.NewGuid(); var c = Guid.NewGuid();
        MrrCalculator.ReciprocalRank(
            retrieved: new[] { b, c, a },
            relevant: new HashSet<Guid> { a })
            .Should().BeApproximately(1.0 / 3.0, 1e-9);
    }

    [Fact]
    public void NoRelevantRetrieved_ReciprocalIsZero()
    {
        var a = Guid.NewGuid(); var b = Guid.NewGuid();
        MrrCalculator.ReciprocalRank(
            retrieved: new[] { b },
            relevant: new HashSet<Guid> { a })
            .Should().Be(0.0);
    }

    [Fact]
    public void MrrIsMeanOfReciprocalRanks()
    {
        MrrCalculator.Mean(new[] { 1.0, 0.5, 0.0 })
            .Should().BeApproximately(0.5, 1e-9);
    }

    [Fact]
    public void MrrOfEmpty_IsZero()
    {
        MrrCalculator.Mean(Array.Empty<double>()).Should().Be(0.0);
    }
}
