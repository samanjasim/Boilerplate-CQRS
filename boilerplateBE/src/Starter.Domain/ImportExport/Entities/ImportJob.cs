using Starter.Domain.Common;
using Starter.Domain.ImportExport.Enums;

namespace Starter.Domain.ImportExport.Entities;

public sealed class ImportJob : AggregateRoot
{
    public Guid TenantId { get; private set; }
    public string EntityType { get; private set; } = default!;
    public string FileName { get; private set; } = default!;
    public Guid FileId { get; private set; }
    public ConflictMode ConflictMode { get; private set; }
    public ImportJobStatus Status { get; private set; }
    public int TotalRows { get; private set; }
    public int ProcessedRows { get; private set; }
    public int CreatedCount { get; private set; }
    public int UpdatedCount { get; private set; }
    public int SkippedCount { get; private set; }
    public int FailedCount { get; private set; }
    public Guid? ResultsFileId { get; private set; }
    public string? ErrorMessage { get; private set; }
    public Guid RequestedBy { get; private set; }
    public DateTime? StartedAt { get; private set; }
    public DateTime? CompletedAt { get; private set; }

    private ImportJob() { }

    private ImportJob(
        Guid id,
        Guid tenantId,
        string entityType,
        string fileName,
        Guid fileId,
        ConflictMode conflictMode,
        Guid requestedBy) : base(id)
    {
        TenantId = tenantId;
        EntityType = entityType;
        FileName = fileName;
        FileId = fileId;
        ConflictMode = conflictMode;
        RequestedBy = requestedBy;
        Status = ImportJobStatus.Pending;
        TotalRows = 0;
        ProcessedRows = 0;
        CreatedCount = 0;
        UpdatedCount = 0;
        SkippedCount = 0;
        FailedCount = 0;
    }

    public static ImportJob Create(
        Guid tenantId,
        string entityType,
        string fileName,
        Guid fileId,
        ConflictMode conflictMode,
        Guid requestedBy)
    {
        return new ImportJob(
            Guid.NewGuid(),
            tenantId,
            entityType,
            fileName,
            fileId,
            conflictMode,
            requestedBy);
    }

    public void SetTotalRows(int count)
    {
        TotalRows = count;
        ModifiedAt = DateTime.UtcNow;
    }

    public void MarkValidating()
    {
        Status = ImportJobStatus.Validating;
        StartedAt = DateTime.UtcNow;
        ModifiedAt = DateTime.UtcNow;
    }

    public void MarkProcessing()
    {
        Status = ImportJobStatus.Processing;
        ModifiedAt = DateTime.UtcNow;
    }

    public void UpdateProgress(int processedRows, int created, int updated, int skipped, int failed)
    {
        ProcessedRows = processedRows;
        CreatedCount = created;
        UpdatedCount = updated;
        SkippedCount = skipped;
        FailedCount = failed;
        ModifiedAt = DateTime.UtcNow;
    }

    public void MarkCompleted(Guid? resultsFileId = null)
    {
        Status = ImportJobStatus.Completed;
        CompletedAt = DateTime.UtcNow;
        ResultsFileId = resultsFileId;
        ModifiedAt = DateTime.UtcNow;
    }

    public void MarkPartialSuccess(Guid resultsFileId)
    {
        Status = ImportJobStatus.PartialSuccess;
        CompletedAt = DateTime.UtcNow;
        ResultsFileId = resultsFileId;
        ModifiedAt = DateTime.UtcNow;
    }

    public void MarkFailed(string errorMessage)
    {
        Status = ImportJobStatus.Failed;
        CompletedAt = DateTime.UtcNow;
        ErrorMessage = errorMessage;
        ModifiedAt = DateTime.UtcNow;
    }
}
