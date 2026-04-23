using FluentAssertions;
using Starter.Module.AI.Application.Services.Ingestion;
using Starter.Module.AI.Infrastructure.Ingestion;
using Xunit;

namespace Starter.Api.Tests.Ai.Ingestion;

public sealed class HierarchicalDocumentChunkerTests
{
    private readonly TokenCounter _counter = new();
    private readonly HierarchicalDocumentChunker _chunker;

    public HierarchicalDocumentChunkerTests()
    {
        _chunker = new HierarchicalDocumentChunker(_counter);
    }

    [Fact]
    public void Produces_At_Least_One_Parent_And_One_Child_For_Short_Text()
    {
        var doc = new ExtractedDocument(
            new[] { new ExtractedPage(1, "Hello world. This is a small document.") },
            UsedOcr: false);

        var result = _chunker.Chunk(doc, new ChunkingOptions(1536, 512, 50));

        result.Parents.Should().HaveCountGreaterOrEqualTo(1);
        result.Children.Should().HaveCountGreaterOrEqualTo(1);
        result.Children[0].ParentIndex.Should().NotBeNull();
    }

    [Fact]
    public void Respects_Parent_Token_Budget()
    {
        var bigText = string.Join(" ", Enumerable.Range(0, 5000).Select(i => $"word{i}"));
        var doc = new ExtractedDocument(
            new[] { new ExtractedPage(1, bigText) },
            UsedOcr: false);

        var result = _chunker.Chunk(doc, new ChunkingOptions(ParentTokens: 200, ChildTokens: 50, ChildOverlapTokens: 10));

        result.Parents.Should().OnlyContain(p => p.TokenCount <= 200);
    }

    [Fact]
    public void Children_Overlap_By_Configured_Token_Budget()
    {
        var bigText = string.Join(" ", Enumerable.Range(0, 500).Select(i => $"word{i}"));
        var doc = new ExtractedDocument(
            new[] { new ExtractedPage(1, bigText) },
            UsedOcr: false);

        var result = _chunker.Chunk(doc, new ChunkingOptions(ParentTokens: 1536, ChildTokens: 50, ChildOverlapTokens: 10));

        result.Children.Should().HaveCountGreaterThan(1);
        result.Children.Should().OnlyContain(c => c.TokenCount <= 60);
    }

    [Fact]
    public void Carries_Page_Number_And_Section_Title()
    {
        var doc = new ExtractedDocument(
            new[]
            {
                new ExtractedPage(1, "First page text.", "Intro"),
                new ExtractedPage(2, "Second page text.", "Details"),
            },
            UsedOcr: false);

        var result = _chunker.Chunk(doc, new ChunkingOptions(1536, 512, 50));

        result.Children.Select(c => c.PageNumber).Should().Contain(new int?[] { 1, 2 });
        result.Children.Select(c => c.SectionTitle).Should().Contain(new[] { "Intro", "Details" });
    }
}
