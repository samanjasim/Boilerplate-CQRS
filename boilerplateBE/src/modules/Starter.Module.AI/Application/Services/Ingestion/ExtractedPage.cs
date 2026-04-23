namespace Starter.Module.AI.Application.Services.Ingestion;

public sealed record ExtractedPage(
    int PageNumber,
    string Text,
    string? SectionTitle = null);
