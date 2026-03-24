using Starter.Domain.Common;

namespace Starter.Application.Features.Reports.DTOs;

public static class ReportMapper
{
    public static ReportDto ToDto(this ReportRequest report, string? downloadUrl = null)
    {
        return new ReportDto(
            report.Id,
            report.ReportType.Name,
            report.Format.Name,
            report.Status.Name,
            report.Filters,
            report.FileName,
            report.RequestedAt,
            report.CompletedAt,
            report.ExpiresAt,
            report.ErrorMessage,
            downloadUrl);
    }
}
