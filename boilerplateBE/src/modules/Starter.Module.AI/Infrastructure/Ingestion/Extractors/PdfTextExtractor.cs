using Microsoft.Extensions.Options;
using Starter.Module.AI.Application.Services.Ingestion;
using Starter.Module.AI.Infrastructure.Settings;
using UglyToad.PdfPig;

namespace Starter.Module.AI.Infrastructure.Ingestion.Extractors;

internal sealed class PdfTextExtractor(
    IOcrService ocr,
    IOptions<AiRagSettings> ragOptions,
    IOptions<AiOcrSettings> ocrOptions) : IDocumentTextExtractor
{
    public IReadOnlyCollection<string> SupportedContentTypes { get; } =
        new[] { "application/pdf" };

    public async Task<ExtractedDocument> ExtractAsync(Stream content, CancellationToken ct)
    {
        var minChars = ragOptions.Value.OcrFallbackMinCharsPerPage;
        var ocrEnabled = ocrOptions.Value.Enabled;

        using var document = PdfDocument.Open(content);

        var pages = new List<ExtractedPage>(document.NumberOfPages);
        var usedOcr = false;
        var failedPages = 0;

        for (var i = 1; i <= document.NumberOfPages; i++)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var page = document.GetPage(i);
                var text = page.Text?.Trim() ?? "";

                if (text.Length < minChars && ocrEnabled)
                {
                    var firstImage = page.GetImages().FirstOrDefault();
                    if (firstImage != null && firstImage.TryGetPng(out var png))
                    {
                        using var imgStream = new MemoryStream(png);
                        text = await ocr.ExtractAsync(imgStream, ct);
                        usedOcr = true;
                    }
                }

                pages.Add(new ExtractedPage(i, text));
            }
            catch (OperationCanceledException) { throw; }
            catch
            {
                failedPages++;
                pages.Add(new ExtractedPage(i, ""));
            }
        }

        var failureRatio = document.NumberOfPages == 0
            ? 0d
            : (double)failedPages / document.NumberOfPages;
        if (failureRatio > ragOptions.Value.PageFailureThreshold)
            throw new InvalidOperationException(
                $"PDF extraction failed for {failedPages}/{document.NumberOfPages} pages " +
                $"(threshold {ragOptions.Value.PageFailureThreshold:P0}).");

        return new ExtractedDocument(pages, usedOcr);
    }
}
