namespace Starter.Application.Features.ImportExport.DTOs;

public sealed record ImportJobDto(
    Guid Id, string EntityType, string FileName, string ConflictMode,
    string Status, int TotalRows, int ProcessedRows,
    int CreatedCount, int UpdatedCount, int SkippedCount, int FailedCount,
    bool HasErrorReport, string? ErrorMessage,
    DateTime? StartedAt, DateTime? CompletedAt, DateTime CreatedAt);
