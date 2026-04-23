using FluentAssertions;
using Starter.Module.AI.Application.Services.Retrieval;
using Xunit;

namespace Starter.Api.Tests.Ai.Retrieval;

public class RetrievedContextShapeTests
{
    [Fact]
    public void RetrievedChunk_carries_chunk_index()
    {
        var chunk = new RetrievedChunk(
            ChunkId: Guid.NewGuid(),
            DocumentId: Guid.NewGuid(),
            DocumentName: "doc.pdf",
            Content: "body",
            SectionTitle: null,
            PageNumber: 2,
            ChunkLevel: "child",
            SemanticScore: 0m,
            KeywordScore: 0m,
            HybridScore: 0.8m,
            ParentChunkId: null,
            ChunkIndex: 5);

        chunk.ChunkIndex.Should().Be(5);
    }

    [Fact]
    public void RetrievedContext_has_siblings_collection()
    {
        var ctx = new RetrievedContext(
            Children: [],
            Parents: [],
            TotalTokens: 0,
            TruncatedByBudget: false,
            DegradedStages: [],
            Siblings: [],
            FusedCandidates: 0,
            DetectedLanguage: "unknown");

        ctx.Siblings.Should().NotBeNull().And.BeEmpty();
    }

    [Fact]
    public void RetrievedContext_Empty_has_empty_siblings()
    {
        RetrievedContext.Empty.Siblings.Should().NotBeNull().And.BeEmpty();
    }
}
