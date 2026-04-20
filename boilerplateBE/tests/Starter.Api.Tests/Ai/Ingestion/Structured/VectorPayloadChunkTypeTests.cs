using FluentAssertions;
using Starter.Module.AI.Application.Services.Ingestion;
using Starter.Module.AI.Domain.Enums;
using Xunit;

namespace Starter.Api.Tests.Ai.Ingestion.Structured;

public class VectorPayloadChunkTypeTests
{
    [Fact]
    public void Chunk_type_defaults_to_body()
    {
        var p = new VectorPayload(Guid.NewGuid(), "doc", "child", 0, null, null, null, Guid.NewGuid());
        p.ChunkType.Should().Be(ChunkType.Body);
    }

    [Fact]
    public void Chunk_type_preserved_when_specified()
    {
        var p = new VectorPayload(Guid.NewGuid(), "doc", "child", 0, null, null, null, Guid.NewGuid(), ChunkType.Code);
        p.ChunkType.Should().Be(ChunkType.Code);
    }
}
