using FluentAssertions;
using Starter.Module.AI.Infrastructure.Eval.Metrics;
using Xunit;

namespace Starter.Api.Tests.Ai.Eval.Metrics;

public sealed class RecallAtKCalculatorTests
{
    [Fact]
    public void PartialMatch_ReturnsProportionInTopK()
    {
        var a = Guid.NewGuid(); var b = Guid.NewGuid();
        var c = Guid.NewGuid(); var d = Guid.NewGuid();
        var result = RecallAtKCalculator.Compute(
            retrieved: new[] { a, c, b, d },
            relevant: new HashSet<Guid> { a, b },
            k: 2);
        result.Should().BeApproximately(0.5, 1e-9);
    }

    [Fact]
    public void NoMatchInTopK_ReturnsZero()
    {
        var a = Guid.NewGuid(); var b = Guid.NewGuid(); var c = Guid.NewGuid();
        var result = RecallAtKCalculator.Compute(
            retrieved: new[] { b, c },
            relevant: new HashSet<Guid> { a },
            k: 2);
        result.Should().Be(0.0);
    }

    [Fact]
    public void EmptyRelevant_ReturnsZero()
    {
        var a = Guid.NewGuid();
        var result = RecallAtKCalculator.Compute(
            retrieved: new[] { a },
            relevant: new HashSet<Guid>(),
            k: 1);
        result.Should().Be(0.0);
    }

    [Fact]
    public void AllRelevantInTopK_ReturnsOne()
    {
        var a = Guid.NewGuid(); var b = Guid.NewGuid(); var c = Guid.NewGuid();
        var result = RecallAtKCalculator.Compute(
            retrieved: new[] { a, b, c, Guid.NewGuid() },
            relevant: new HashSet<Guid> { a, b, c },
            k: 3);
        result.Should().Be(1.0);
    }

    [Fact]
    public void KLargerThanRetrieved_ScansWholeList()
    {
        var a = Guid.NewGuid(); var b = Guid.NewGuid();
        var result = RecallAtKCalculator.Compute(
            retrieved: new[] { a, b },
            relevant: new HashSet<Guid> { a, b },
            k: 100);
        result.Should().Be(1.0);
    }
}
