using FluentAssertions;
using Microsoft.Extensions.Options;
using Starter.Module.AI.Infrastructure.Ingestion.Ocr;
using Starter.Module.AI.Infrastructure.Settings;
using Xunit;

namespace Starter.Api.Tests.Ai.Ingestion;

public sealed class TesseractOcrServiceTests
{
    [Fact]
    public void Constructor_Does_Not_Probe_Tessdata()
    {
        // Regression: previously the ctor resolved the tessdata path eagerly
        // and threw during DI resolution on machines without Tesseract data,
        // which broke the consumer for every document type (not just PDFs).
        var bogusPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var opts = Options.Create(new AiOcrSettings { TessdataPath = bogusPath });

        var act = () => new TesseractOcrService(opts);

        act.Should().NotThrow();
    }

    [Fact]
    public async Task ExtractAsync_Throws_When_Tessdata_Missing()
    {
        var bogusPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var opts = Options.Create(new AiOcrSettings { TessdataPath = bogusPath });
        var service = new TesseractOcrService(opts);

        using var stream = new MemoryStream(new byte[] { 0, 1, 2 });
        var act = async () => await service.ExtractAsync(stream, CancellationToken.None);

        await act.Should().ThrowAsync<Exception>();
    }
}
