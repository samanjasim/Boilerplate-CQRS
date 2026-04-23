namespace Starter.Module.AI.Application.Services.Ingestion;

public interface IDocumentTextExtractor
{
    IReadOnlyCollection<string> SupportedContentTypes { get; }

    Task<ExtractedDocument> ExtractAsync(Stream content, CancellationToken ct);
}
