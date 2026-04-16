using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using FluentAssertions;
using Starter.Module.AI.Infrastructure.Ingestion.Extractors;
using Xunit;

namespace Starter.Api.Tests.Ai.Ingestion;

public sealed class DocxTextExtractorTests
{
    [Fact]
    public async Task Extracts_Paragraphs_As_SinglePage()
    {
        using var ms = new MemoryStream();
        CreateDocx(ms, "Heading1", "Hello world.");
        ms.Position = 0;

        var extractor = new DocxTextExtractor();
        var result = await extractor.ExtractAsync(ms, CancellationToken.None);

        result.Pages.Should().HaveCount(1);
        result.Pages[0].Text.Should().Contain("Hello world.");
        result.Pages[0].SectionTitle.Should().Be("Heading1");
        result.UsedOcr.Should().BeFalse();
    }

    private static void CreateDocx(Stream stream, string heading, string body)
    {
        using var doc = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document);
        var main = doc.AddMainDocumentPart();
        main.Document = new Document(new Body(
            new Paragraph(
                new ParagraphProperties(new ParagraphStyleId { Val = "Heading1" }),
                new Run(new Text(heading))),
            new Paragraph(new Run(new Text(body)))));
        main.Document.Save();
    }
}
