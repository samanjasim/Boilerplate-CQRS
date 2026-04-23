using Microsoft.Extensions.Options;
using Starter.Module.AI.Application.Services.Ingestion;
using Starter.Module.AI.Infrastructure.Settings;

namespace Starter.Module.AI.Infrastructure.Ingestion.Structured;

internal sealed class ChunkerRouter(
    StructuredMarkdownChunker structural,
    HierarchicalDocumentChunker fallback,
    HtmlToMarkdownConverter htmlConverter,
    IOptions<AiRagSettings> settings) : IDocumentChunker
{
    private readonly AiRagSettings _settings = settings.Value;

    public HierarchicalChunks Chunk(ExtractedDocument document, ChunkingOptions options)
    {
        if (!_settings.EnableStructuralChunking)
            return fallback.Chunk(document, options);

        var ct = Normalize(options.ContentType);

        if (ct == "text/html")
        {
            var converted = new ExtractedDocument(
                document.Pages
                    .Select(p => new ExtractedPage(p.PageNumber, htmlConverter.Convert(p.Text), p.SectionTitle))
                    .ToList(),
                document.UsedOcr);
            return structural.Chunk(converted, options);
        }

        if (ct == "text/markdown") return structural.Chunk(document, options);

        if (ct == "text/plain" && LooksLikeMarkdown(document)) return structural.Chunk(document, options);

        return fallback.Chunk(document, options);
    }

    private static string? Normalize(string? contentType)
    {
        if (string.IsNullOrWhiteSpace(contentType)) return null;
        var semi = contentType.IndexOf(';');
        return (semi >= 0 ? contentType[..semi] : contentType).Trim().ToLowerInvariant();
    }

    private static bool LooksLikeMarkdown(ExtractedDocument doc)
    {
        foreach (var page in doc.Pages)
        {
            if (string.IsNullOrWhiteSpace(page.Text)) continue;
            var nonEmpty = page.Text.Split('\n').Where(l => l.Length > 0).Take(4).ToArray();
            if (nonEmpty.Length == 0) continue;
            if (nonEmpty.Count(l => l.TrimStart().StartsWith('#')) >= 1) return true;
            break;
        }
        return false;
    }
}
