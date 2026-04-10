using MediatR;
using Microsoft.EntityFrameworkCore;
using Starter.Application.Common.Interfaces;
using Starter.Domain.Common;
using Starter.Domain.Common.Enums;
using Starter.Domain.Identity.Errors;
using Starter.Shared.Results;

namespace Starter.Application.Features.Reports.Queries.GetReportDownload;

internal sealed class GetReportDownloadQueryHandler(
    IApplicationDbContext context,
    ICurrentUserService currentUserService,
    IStorageService storageService) : IRequestHandler<GetReportDownloadQuery, Result<string>>
{
    public async Task<Result<string>> Handle(GetReportDownloadQuery request, CancellationToken cancellationToken)
    {
        var userId = currentUserService.UserId;
        if (userId is null)
            return Result.Failure<string>(UserErrors.Unauthorized());

        var report = await context.Set<ReportRequest>()
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == request.Id, cancellationToken);

        if (report is null)
            return Result.Failure<string>(Error.NotFound("Report.NotFound", $"Report with ID '{request.Id}' was not found."));

        if (currentUserService.TenantId is not null && report.TenantId != currentUserService.TenantId)
            return Result.Failure<string>(Error.Forbidden("You do not have access to this report."));

        if (report.Status != ReportStatus.Completed)
            return Result.Failure<string>(Error.Validation("Report.NotCompleted", "Report is not yet completed."));

        if (report.FileId is null)
            return Result.Failure<string>(Error.Validation("Report.NoFile", "Report has no associated file."));

        if (report.ExpiresAt.HasValue && report.ExpiresAt.Value < DateTime.UtcNow)
            return Result.Failure<string>(Error.Validation("Report.Expired", "Report has expired. Please request a new report."));

        var signedUrl = await storageService.GetSignedUrlAsync(
            report.GetStorageKey(),
            TimeSpan.FromMinutes(15),
            cancellationToken);

        return Result.Success(signedUrl);
    }
}
