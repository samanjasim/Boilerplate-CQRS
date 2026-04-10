using Starter.Domain.Common.Enums;

namespace Starter.Domain.Common;

public sealed class ReportRequest : BaseAuditableEntity, ITenantEntity
{
    public Guid? TenantId { get; private set; }
    public Guid RequestedBy { get; private set; }
    public ReportType ReportType { get; private set; } = null!;
    public ReportFormat Format { get; private set; } = null!;
    public string? Filters { get; private set; }
    public ReportStatus Status { get; private set; } = null!;
    public Guid? FileId { get; private set; }
    public string? FileName { get; private set; }
    public string? ErrorMessage { get; private set; }
    public DateTime RequestedAt { get; private set; }
    public DateTime? CompletedAt { get; private set; }
    public DateTime? ExpiresAt { get; private set; }
    public string? FilterHash { get; private set; }

    private ReportRequest() { }

    private ReportRequest(Guid id) : base(id) { }

    public static ReportRequest Create(
        Guid requestedBy,
        Guid? tenantId,
        ReportType reportType,
        ReportFormat format,
        string? filters,
        string? filterHash)
    {
        return new ReportRequest(Guid.NewGuid())
        {
            RequestedBy = requestedBy,
            TenantId = tenantId,
            ReportType = reportType,
            Format = format,
            Filters = filters,
            Status = ReportStatus.Pending,
            RequestedAt = DateTime.UtcNow,
            FilterHash = filterHash
        };
    }

    public void MarkProcessing()
    {
        Status = ReportStatus.Processing;
    }

    public void MarkCompleted(Guid fileId, string fileName, DateTime? expiresAt)
    {
        Status = ReportStatus.Completed;
        FileId = fileId;
        FileName = fileName;
        CompletedAt = DateTime.UtcNow;
        ExpiresAt = expiresAt;
    }

    public void MarkFailed(string errorMessage)
    {
        Status = ReportStatus.Failed;
        ErrorMessage = errorMessage;
        CompletedAt = DateTime.UtcNow;
    }

    public string GetStorageKey()
    {
        var tenantKey = TenantId?.ToString() ?? "platform";
        var extension = Format == ReportFormat.Csv ? "csv" : "pdf";
        return $"reports/{tenantKey}/{ReportType.Name}/{Id}.{extension}";
    }

    public string GetFileName()
    {
        var extension = Format == ReportFormat.Csv ? "csv" : "pdf";
        return $"{ReportType.Name}_Report_{RequestedAt:yyyyMMdd_HHmmss}.{extension}";
    }
}
