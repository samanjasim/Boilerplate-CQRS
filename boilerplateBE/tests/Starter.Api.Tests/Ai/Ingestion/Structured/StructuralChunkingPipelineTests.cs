using System.Linq;
using FluentAssertions;
using Starter.Module.AI.Application.Services.Ingestion;
using Starter.Module.AI.Application.Services.Retrieval;
using Starter.Module.AI.Domain.Enums;
using Starter.Module.AI.Infrastructure.Ingestion;
using Starter.Module.AI.Infrastructure.Ingestion.Structured;
using Starter.Module.AI.Infrastructure.Retrieval;
using Xunit;

namespace Starter.Api.Tests.Ai.Ingestion.Structured;

public class StructuralChunkingPipelineTests
{
    [Fact]
    public void Mixed_language_document_is_chunked_and_rendered_correctly()
    {
        var tc = new TokenCounter();
        var router = new ChunkerRouter(
            new StructuredMarkdownChunker(tc, new MarkdownBlockTokenizer()),
            new HierarchicalDocumentChunker(tc),
            new HtmlToMarkdownConverter(),
            Microsoft.Extensions.Options.Options.Create(new Starter.Module.AI.Infrastructure.Settings.AiRagSettings()));

        var md =
            "# قسم 1\n\n" +
            "## قسم 1.1\n\n" +
            "هذا نص عربي.\n\n" +
            "```python\n" +
            "x = 1\n" +
            "```\n\n" +
            "$$\n" +
            "E = mc^2\n" +
            "$$\n";

        var extracted = new ExtractedDocument(new[] { new ExtractedPage(1, md) }, UsedOcr: false);
        var chunks = router.Chunk(extracted, new ChunkingOptions(1536, 512, 50) { ContentType = "text/markdown" });

        chunks.Children.Should().HaveCountGreaterOrEqualTo(3);
        chunks.Children.Should().Contain(c => c.ChunkType == ChunkType.Body && c.SectionTitle == "قسم 1 > قسم 1.1");
        chunks.Children.Should().Contain(c => c.ChunkType == ChunkType.Code);
        chunks.Children.Should().Contain(c => c.ChunkType == ChunkType.Math);

        var retrieved = chunks.Children.Select(c => new RetrievedChunk(
            ChunkId: Guid.NewGuid(),
            DocumentId: Guid.NewGuid(),
            DocumentName: "doc.md",
            Content: c.Content,
            SectionTitle: c.SectionTitle,
            PageNumber: c.PageNumber,
            ChunkLevel: "child",
            SemanticScore: 0,
            KeywordScore: 0,
            HybridScore: 1,
            ParentChunkId: null,
            ChunkIndex: c.Index,
            ChunkType: c.ChunkType)).ToList();

        var prompt = ContextPromptBuilder.Build("sys", new RetrievedContext(retrieved, [], 100, false, [], [], 0, "unknown"));

        prompt.Should().Contain("```\nx = 1\n```");
        prompt.Should().Contain("$$\nE = mc^2\n$$");
        prompt.Should().Contain("Section: \"قسم 1 > قسم 1.1\"");
    }
}
