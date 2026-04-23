using Starter.Module.AI.Application.Services.Ingestion;

namespace Starter.Module.AI.Infrastructure.Ingestion;

internal sealed class DocumentTextExtractorRegistry : IDocumentTextExtractorRegistry
{
    private readonly Dictionary<string, IDocumentTextExtractor> _byType;

    public DocumentTextExtractorRegistry(IEnumerable<IDocumentTextExtractor> extractors)
    {
        _byType = new Dictionary<string, IDocumentTextExtractor>(StringComparer.OrdinalIgnoreCase);
        foreach (var ex in extractors)
            foreach (var ct in ex.SupportedContentTypes)
                _byType[ct] = ex;
    }

    public IDocumentTextExtractor? Resolve(string contentType)
    {
        if (string.IsNullOrWhiteSpace(contentType)) return null;
        var semi = contentType.IndexOf(';');
        var key = (semi >= 0 ? contentType[..semi] : contentType).Trim();
        return _byType.TryGetValue(key, out var ex) ? ex : null;
    }
}
