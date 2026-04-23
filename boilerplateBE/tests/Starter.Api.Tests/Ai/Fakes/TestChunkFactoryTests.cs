using FluentAssertions;
using Xunit;

namespace Starter.Api.Tests.Ai.Fakes;

public sealed class TestChunkFactoryTests
{
    [Fact]
    public void Build_CreatesChunkWithProvidedValues()
    {
        var pointId = Guid.NewGuid();
        var documentId = Guid.NewGuid();

        var chunk = TestChunkFactory.Build(
            pointId: pointId,
            documentId: documentId,
            chunkIndex: 7,
            content: "hello",
            pageNumber: 3,
            sectionTitle: "Intro",
            tokenCount: 42);

        chunk.QdrantPointId.Should().Be(pointId);
        chunk.DocumentId.Should().Be(documentId);
        chunk.ChunkIndex.Should().Be(7);
        chunk.Content.Should().Be("hello");
        chunk.ChunkLevel.Should().Be("child");
        chunk.PageNumber.Should().Be(3);
        chunk.SectionTitle.Should().Be("Intro");
        chunk.TokenCount.Should().Be(42);
        chunk.Id.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public void Build_UsesDefaultsWhenNotProvided()
    {
        var chunk = TestChunkFactory.Build(chunkIndex: 2);

        chunk.Content.Should().Be("content-2");
        chunk.ChunkLevel.Should().Be("child");
        chunk.QdrantPointId.Should().NotBe(Guid.Empty);
        chunk.DocumentId.Should().NotBe(Guid.Empty);
        chunk.PageNumber.Should().BeNull();
        chunk.SectionTitle.Should().BeNull();
        chunk.ParentChunkId.Should().BeNull();
    }
}
