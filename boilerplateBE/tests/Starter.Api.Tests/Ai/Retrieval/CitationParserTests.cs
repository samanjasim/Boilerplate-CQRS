using FluentAssertions;
using Starter.Module.AI.Application.DTOs;
using Starter.Module.AI.Application.Services.Retrieval;
using Starter.Module.AI.Infrastructure.Retrieval;
using Xunit;

namespace Starter.Api.Tests.Ai.Retrieval;

public sealed class CitationParserTests
{
    private static readonly IReadOnlyList<RetrievedChunk> Chunks = new List<RetrievedChunk>
    {
        MakeChunk(1), MakeChunk(2), MakeChunk(3)
    };

    [Fact]
    public void Parses_Single_Marker()
    {
        var result = CitationParser.Parse("Water is wet [1].", Chunks);
        result.Should().HaveCount(1);
        result[0].Marker.Should().Be(1);
        result[0].ChunkId.Should().Be(Chunks[0].ChunkId);
    }

    [Fact]
    public void Parses_Multi_Index_Markers()
    {
        var result = CitationParser.Parse("Both sources agree [1, 3].", Chunks);
        result.Should().HaveCount(2);
        result.Select(c => c.Marker).Should().BeEquivalentTo(new[] { 1, 3 });
    }

    [Fact]
    public void Tolerates_Whitespace_And_Dedupes()
    {
        var result = CitationParser.Parse("See [1] and [  1 ] and [1, 2].", Chunks);
        result.Select(c => c.Marker).Should().BeEquivalentTo(new[] { 1, 2 });
    }

    [Fact]
    public void Drops_OutOfRange_Markers_With_Warning()
    {
        var result = CitationParser.Parse("Check [5] or [1].", Chunks);
        result.Should().HaveCount(1);
        result[0].Marker.Should().Be(1);
    }

    [Fact]
    public void No_Markers_Returns_Fallback_Full_Set()
    {
        var result = CitationParser.Parse("plain answer with no citations", Chunks);
        result.Should().HaveCount(3);
        result.Select(c => c.Marker).Should().BeEquivalentTo(new[] { 1, 2, 3 });
    }

    [Fact]
    public void No_Markers_No_Chunks_Returns_Empty()
    {
        var result = CitationParser.Parse("text", new List<RetrievedChunk>());
        result.Should().BeEmpty();
    }

    private static RetrievedChunk MakeChunk(int i) => new(
        ChunkId: Guid.Parse($"00000000-0000-0000-0000-00000000000{i}"),
        DocumentId: Guid.NewGuid(),
        DocumentName: $"Doc{i}",
        Content: $"content {i}",
        SectionTitle: null,
        PageNumber: null,
        ChunkLevel: "child",
        SemanticScore: 0.9m,
        KeywordScore: 0.5m,
        HybridScore: 0.8m,
        ParentChunkId: null,
        ChunkIndex: i);
}
