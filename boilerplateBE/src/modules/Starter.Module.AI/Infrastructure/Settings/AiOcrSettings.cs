namespace Starter.Module.AI.Infrastructure.Settings;

public sealed class AiOcrSettings
{
    public const string SectionName = "AI:Ocr";

    public bool Enabled { get; init; } = true;
    public string Provider { get; init; } = "Tesseract";
    public string? TessdataPath { get; init; } // null → use default OS install
    public string Language { get; init; } = "eng";
}
