namespace Starter.Module.AI.Infrastructure.Ingestion.Ocr;

internal sealed class NullOcrService : IOcrService
{
    public Task<string> ExtractAsync(Stream imageStream, CancellationToken ct) =>
        throw new NotSupportedException("OCR is disabled (AI:Ocr:Enabled=false).");
}
