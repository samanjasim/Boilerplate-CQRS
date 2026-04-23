using Starter.Module.AI.Application.Services.Ingestion;

namespace Starter.Module.AI.Infrastructure.Ingestion.Extractors;

public sealed class PlainTextExtractor : IDocumentTextExtractor
{
    public IReadOnlyCollection<string> SupportedContentTypes { get; } =
        new[] { "text/plain", "text/markdown" };

    public async Task<ExtractedDocument> ExtractAsync(Stream content, CancellationToken ct)
    {
        using var reader = new StreamReader(content, leaveOpen: true);
        var text = await reader.ReadToEndAsync(ct);
        return new ExtractedDocument(
            Pages: new[] { new ExtractedPage(1, text) },
            UsedOcr: false);
    }
}
