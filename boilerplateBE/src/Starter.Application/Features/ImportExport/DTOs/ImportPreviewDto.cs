namespace Starter.Application.Features.ImportExport.DTOs;

public sealed record ImportPreviewDto(
    string[] Headers, string[][] PreviewRows, string[] ValidationErrors,
    int TotalRowCount, string[] UnrecognizedColumns);
