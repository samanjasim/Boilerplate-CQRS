namespace Starter.Module.ImportExport.Application.DTOs;

public sealed record ImportPreviewDto(
    string[] Headers, string[][] PreviewRows, string[] ValidationErrors,
    int TotalRowCount, string[] UnrecognizedColumns);
