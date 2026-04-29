namespace Starter.Application.Features.Reports.DTOs;

public sealed record ReportStatusCountsDto(
    int Pending,
    int Processing,
    int Completed,
    int Failed
);
