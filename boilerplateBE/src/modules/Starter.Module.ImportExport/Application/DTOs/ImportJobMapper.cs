using Starter.Module.ImportExport.Domain.Entities;

namespace Starter.Module.ImportExport.Application.DTOs;

public static class ImportJobMapper
{
    public static ImportJobDto ToDto(this ImportJob entity) =>
        new(
            Id: entity.Id,
            EntityType: entity.EntityType,
            FileName: entity.FileName,
            ConflictMode: entity.ConflictMode.ToString(),
            Status: entity.Status.ToString(),
            TotalRows: entity.TotalRows,
            ProcessedRows: entity.ProcessedRows,
            CreatedCount: entity.CreatedCount,
            UpdatedCount: entity.UpdatedCount,
            SkippedCount: entity.SkippedCount,
            FailedCount: entity.FailedCount,
            HasErrorReport: entity.ResultsFileId.HasValue,
            ErrorMessage: entity.ErrorMessage,
            StartedAt: entity.StartedAt,
            CompletedAt: entity.CompletedAt,
            CreatedAt: entity.CreatedAt);
}
