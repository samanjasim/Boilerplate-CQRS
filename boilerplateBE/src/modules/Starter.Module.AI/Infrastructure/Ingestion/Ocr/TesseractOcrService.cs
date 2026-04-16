using Microsoft.Extensions.Options;
using Starter.Module.AI.Infrastructure.Settings;
using Tesseract;

namespace Starter.Module.AI.Infrastructure.Ingestion.Ocr;

internal sealed class TesseractOcrService : IOcrService
{
    private readonly string _tessdataPath;
    private readonly string _language;

    public TesseractOcrService(IOptions<AiOcrSettings> options)
    {
        _tessdataPath = options.Value.TessdataPath ?? ResolveDefaultTessdataPath();
        _language = options.Value.Language;
    }

    public Task<string> ExtractAsync(Stream imageStream, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        using var ms = new MemoryStream();
        imageStream.CopyTo(ms);

        using var engine = new TesseractEngine(_tessdataPath, _language, EngineMode.Default);
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
