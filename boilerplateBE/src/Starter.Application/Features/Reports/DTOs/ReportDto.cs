namespace Starter.Application.Features.Reports.DTOs;

public sealed record ReportDto(
    Guid Id,
    string ReportType,
    string Format,
    string Status,
    string? Filters,
    string? FileName,
    DateTime RequestedAt,
    DateTime? CompletedAt,
    DateTime? ExpiresAt,
    string? ErrorMessage,
    string? DownloadUrl = null);
