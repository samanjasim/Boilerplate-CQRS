using Microsoft.Extensions.Options;
using Starter.Module.AI.Infrastructure.Settings;
using Tesseract;

namespace Starter.Module.AI.Infrastructure.Ingestion.Ocr;

internal sealed class TesseractOcrService(IOptions<AiOcrSettings> options) : IOcrService
{
    private readonly string? _configuredTessdataPath = options.Value.TessdataPath;
    private readonly string _language = options.Value.Language;

    public Task<string> ExtractAsync(Stream imageStream, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var tessdataPath = _configuredTessdataPath ?? ResolveDefaultTessdataPath();

        using var ms = new MemoryStream();
        imageStream.CopyTo(ms);

        using var engine = new TesseractEngine(tessdataPath, _language, EngineMode.Default);
        using var img = Pix.LoadFromMemory(ms.ToArray());
        using var page = engine.Process(img);
        return Task.FromResult(page.GetText() ?? string.Empty);
    }

    private static string ResolveDefaultTessdataPath()
    {
        var candidates = new[]
        {
            "/usr/share/tesseract-ocr/4.00/tessdata",
            "/usr/share/tesseract-ocr/tessdata",
            "/opt/homebrew/share/tessdata",
            Path.Combine(AppContext.BaseDirectory, "tessdata")
        };

        return candidates.FirstOrDefault(Directory.Exists)
            ?? throw new InvalidOperationException(
                "Could not find tessdata directory. Set AI:Ocr:TessdataPath.");
    }
}
