using FluentAssertions;
using Starter.Module.AI.Infrastructure.Ingestion;
using Xunit;

namespace Starter.Api.Tests.Ai;

public sealed class TokenCounterTests
{
    private readonly TokenCounter _counter = new();

    [Fact]
    public void Count_Returns_Positive_For_NonEmpty_Text()
    {
        _counter.Count("Hello, world!").Should().BeGreaterThan(0);
    }

    [Fact]
    public void Count_Returns_Zero_For_Empty_Text()
    {
        _counter.Count("").Should().Be(0);
    }

    [Fact]
    public void Split_Respects_MaxTokens_Budget()
    {
        var text = string.Join(" ", Enumerable.Range(0, 2000).Select(i => $"word{i}"));
        var pieces = _counter.Split(text, maxTokens: 100).ToList();

        pieces.Should().OnlyContain(p => _counter.Count(p) <= 100);
        string.Concat(pieces).Length.Should().BeGreaterThan(text.Length / 2);
    }
}
