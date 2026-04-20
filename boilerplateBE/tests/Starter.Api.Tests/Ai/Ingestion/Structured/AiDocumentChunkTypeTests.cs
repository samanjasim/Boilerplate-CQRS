using FluentAssertions;
using Starter.Module.AI.Domain.Entities;
using Starter.Module.AI.Domain.Enums;
using Xunit;

namespace Starter.Api.Tests.Ai.Ingestion.Structured;

public class AiDocumentChunkTypeTests
{
    [Fact]
    public void Create_defaults_chunk_type_to_body()
    {
        var chunk = AiDocumentChunk.Create(
            documentId: Guid.NewGuid(),
            chunkLevel: "child",
            content: "hello",
            chunkIndex: 0,
            tokenCount: 1,
            qdrantPointId: Guid.NewGuid());
        chunk.ChunkType.Should().Be(ChunkType.Body);
    }

    [Fact]
    public void Create_preserves_chunk_type_when_supplied()
    {
        var chunk = AiDocumentChunk.Create(
            documentId: Guid.NewGuid(),
            chunkLevel: "child",
            content: "```\nx\n```",
            chunkIndex: 0,
            tokenCount: 3,
            qdrantPointId: Guid.NewGuid(),
            chunkType: ChunkType.Code);
        chunk.ChunkType.Should().Be(ChunkType.Code);
    }
}
