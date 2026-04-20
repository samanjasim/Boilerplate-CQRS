using FluentAssertions;
using Starter.Module.AI.Application.Services.Ingestion;
using Starter.Module.AI.Infrastructure.Ingestion;
using Starter.Module.AI.Infrastructure.Ingestion.Structured;
using Xunit;

namespace Starter.Api.Tests.Ai.Ingestion.Structured;

public class ChunkerRouterTests
{
    private static ChunkerRouter NewRouter()
    {
        var tc = new TokenCounter();
        return new ChunkerRouter(
            structural: new StructuredMarkdownChunker(tc, new MarkdownBlockTokenizer()),
            fallback: new HierarchicalDocumentChunker(tc),
            htmlConverter: new HtmlToMarkdownConverter());
    }

    private static ExtractedDocument Doc(string text) => new([new ExtractedPage(1, text)], false);
    private static ChunkingOptions Opts(string contentType) =>
        new(ParentTokens: 1536, ChildTokens: 512, ChildOverlapTokens: 50) { ContentType = contentType };

    [Fact]
    public void Markdown_content_type_uses_structural()
    {
        var chunks = NewRouter().Chunk(Doc("# H\n\nbody\n"), Opts("text/markdown"));
        chunks.Children.Single().SectionTitle.Should().Be("H");
    }

    [Fact]
    public void Plain_text_without_heading_hints_uses_fallback()
    {
        var chunks = NewRouter().Chunk(Doc("some plain text here."), Opts("text/plain"));
        chunks.Children.Single().SectionTitle.Should().BeNull();
    }

    [Fact]
    public void Plain_text_with_heading_heuristic_uses_structural()
    {
        var md = "# Title\n\n## Sub\n\nbody\n";
        var chunks = NewRouter().Chunk(Doc(md), Opts("text/plain"));
        chunks.Children.Single().SectionTitle.Should().Be("Title > Sub");
    }

    [Fact]
    public void Html_is_converted_then_chunked_structurally()
    {
        var html = "<h1>Doc</h1><p>body text.</p>";
        var chunks = NewRouter().Chunk(Doc(html), Opts("text/html"));
        chunks.Children.Single().SectionTitle.Should().Be("Doc");
    }

    [Fact]
    public void Pdf_content_type_uses_fallback()
    {
        var chunks = NewRouter().Chunk(Doc("raw PDF text without markdown."), Opts("application/pdf"));
        chunks.Children.Should().NotBeEmpty();
        chunks.Children.Should().OnlyContain(c => c.SectionTitle == null);
    }
}
