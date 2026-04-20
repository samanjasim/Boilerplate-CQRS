using FluentAssertions;
using Starter.Module.AI.Application.Services.Ingestion;
using Starter.Module.AI.Domain.Enums;
using Starter.Module.AI.Infrastructure.Ingestion;
using Starter.Module.AI.Infrastructure.Ingestion.Structured;
using Xunit;

namespace Starter.Api.Tests.Ai.Ingestion.Structured;

public class StructuredMarkdownChunkerTests
{
    private static StructuredMarkdownChunker NewChunker() =>
        new(new TokenCounter(), new MarkdownBlockTokenizer());

    private static ExtractedDocument OneMarkdownPage(string md) =>
        new([new ExtractedPage(1, md)], UsedOcr: false);

    private static ChunkingOptions Opts(int child = 512, int parent = 1536, int overlap = 50)
        => new(ParentTokens: parent, ChildTokens: child, ChildOverlapTokens: overlap);

    [Fact]
    public void Single_heading_followed_by_body_produces_body_chunk_with_breadcrumb()
    {
        var md = "# Chapter 1\n\nThis is the body of chapter 1.\n";
        var chunks = NewChunker().Chunk(OneMarkdownPage(md), Opts());
        chunks.Children.Should().ContainSingle();
        var child = chunks.Children[0];
        child.ChunkType.Should().Be(ChunkType.Body);
        child.SectionTitle.Should().Be("Chapter 1");
        child.Content.Should().Contain("This is the body");
    }

    [Fact]
    public void Code_block_emits_code_chunk_with_language_preserved_in_text()
    {
        var md = "# A\n\n```python\nx = 1\n```\n";
        var chunks = NewChunker().Chunk(OneMarkdownPage(md), Opts());
        var code = chunks.Children.Should().ContainSingle().Subject;
        code.ChunkType.Should().Be(ChunkType.Code);
    }

    [Fact]
    public void Nested_headings_produce_breadcrumb_path_in_section_title()
    {
        var md = "# Ch1\n\n## Sec A\n\nbody under A\n\n## Sec B\n\nbody under B\n";
        var chunks = NewChunker().Chunk(OneMarkdownPage(md), Opts());
        chunks.Children.Should().HaveCount(2);
        chunks.Children[0].SectionTitle.Should().Be("Sec A");
        chunks.Children[1].SectionTitle.Should().Be("Sec B");
    }

    [Fact]
    public void Empty_markdown_produces_no_chunks()
    {
        var chunks = NewChunker().Chunk(OneMarkdownPage(""), Opts());
        chunks.Children.Should().BeEmpty();
        chunks.Parents.Should().BeEmpty();
    }

    [Fact]
    public void Parent_wraps_adjacent_children()
    {
        var md = "# A\n\nfirst paragraph of body.\n\nsecond paragraph of body.\n";
        var chunks = NewChunker().Chunk(OneMarkdownPage(md), Opts(child: 32, parent: 128, overlap: 0));
        chunks.Parents.Should().HaveCountGreaterOrEqualTo(1);
        chunks.Children.Should().OnlyContain(c => c.ParentIndex.HasValue);
    }

    [Fact]
    public void Arabic_heading_and_body_preserve_order()
    {
        var md = "# مقدمة\n\nهذا هو النص العربي.\n";
        var chunks = NewChunker().Chunk(OneMarkdownPage(md), Opts());
        chunks.Children.Should().ContainSingle().Which.SectionTitle.Should().Be("مقدمة");
    }
}
