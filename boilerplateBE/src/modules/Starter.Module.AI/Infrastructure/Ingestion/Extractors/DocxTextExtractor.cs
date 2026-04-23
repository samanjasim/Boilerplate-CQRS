using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Starter.Module.AI.Application.Services.Ingestion;

namespace Starter.Module.AI.Infrastructure.Ingestion.Extractors;

internal sealed class DocxTextExtractor : IDocumentTextExtractor
{
    public IReadOnlyCollection<string> SupportedContentTypes { get; } =
        new[] { "application/vnd.openxmlformats-officedocument.wordprocessingml.document" };

    public Task<ExtractedDocument> ExtractAsync(Stream content, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        using var doc = WordprocessingDocument.Open(content, false);
        var body = doc.MainDocumentPart?.Document.Body;
        if (body is null)
            return Task.FromResult(new ExtractedDocument(Array.Empty<ExtractedPage>(), false));

        var paragraphs = body.Elements<Paragraph>().ToList();
        var buffer = new System.Text.StringBuilder();
        string? currentHeading = null;

        foreach (var p in paragraphs)
        {
            var style = p.ParagraphProperties?.ParagraphStyleId?.Val?.Value;
            if (style != null && style.StartsWith("Heading", StringComparison.OrdinalIgnoreCase))
                currentHeading ??= p.InnerText;

            if (!string.IsNullOrWhiteSpace(p.InnerText))
                buffer.AppendLine(p.InnerText);
        }

        return Task.FromResult(new ExtractedDocument(
            Pages: new[] { new ExtractedPage(1, buffer.ToString().Trim(), currentHeading) },
            UsedOcr: false));
    }
}
