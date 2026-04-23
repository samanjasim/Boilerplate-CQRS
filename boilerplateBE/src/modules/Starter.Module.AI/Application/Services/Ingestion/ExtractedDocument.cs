namespace Starter.Module.AI.Application.Services.Ingestion;

public sealed record ExtractedDocument(
    IReadOnlyList<ExtractedPage> Pages,
    bool UsedOcr);
