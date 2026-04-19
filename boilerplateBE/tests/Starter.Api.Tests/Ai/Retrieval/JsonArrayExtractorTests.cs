using FluentAssertions;
using Starter.Module.AI.Infrastructure.Retrieval.Json;
using Xunit;

namespace Starter.Api.Tests.Ai.Retrieval;

public sealed class JsonArrayExtractorTests
{
    [Fact]
    public void BareArray_ReturnsElements()
    {
        JsonArrayExtractor.TryExtractStrings("[\"a\", \"b\"]", out var items).Should().BeTrue();
        items.Should().Equal("a", "b");
    }

    [Fact]
    public void MarkdownFenced_ReturnsElements()
    {
        var input = "Sure, here you go:\n```json\n[\"x\", \"y\", \"z\"]\n```\n";
        JsonArrayExtractor.TryExtractStrings(input, out var items).Should().BeTrue();
        items.Should().Equal("x", "y", "z");
    }

    [Fact]
    public void LeadingPreamble_ReturnsElements()
    {
        JsonArrayExtractor.TryExtractStrings("Here: [\"foo\"]", out var items).Should().BeTrue();
        items.Should().Equal("foo");
    }

    [Fact]
    public void NoArray_ReturnsFalse()
    {
        JsonArrayExtractor.TryExtractStrings("nope", out var items).Should().BeFalse();
        items.Should().BeEmpty();
    }

    [Fact]
    public void IntegerArray_UsesTryExtractInts()
    {
        JsonArrayExtractor.TryExtractInts("[2, 0, 1]", out var items).Should().BeTrue();
        items.Should().Equal(2, 0, 1);
    }

    [Fact]
    public void IntegerArrayWithFence_UsesTryExtractInts()
    {
        JsonArrayExtractor.TryExtractInts("```\n[1,2,3]\n```", out var items).Should().BeTrue();
        items.Should().Equal(1, 2, 3);
    }

    [Fact]
    public void ArabicStringsPreserved()
    {
        JsonArrayExtractor.TryExtractStrings("[\"ما هو\", \"التعريف\"]", out var items).Should().BeTrue();
        items.Should().Equal("ما هو", "التعريف");
    }
}
