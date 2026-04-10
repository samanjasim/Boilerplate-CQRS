using MediatR;
using Microsoft.EntityFrameworkCore;
using Starter.Application.Common.Interfaces;
using Starter.Domain.Common;
using Starter.Domain.Identity.Errors;
using Starter.Shared.Results;

namespace Starter.Application.Features.Reports.Commands.DeleteReport;

internal sealed class DeleteReportCommandHandler(
    IApplicationDbContext context,
    ICurrentUserService currentUserService,
    IStorageService storageService) : IRequestHandler<DeleteReportCommand, Result>
{
    public async Task<Result> Handle(DeleteReportCommand request, CancellationToken cancellationToken)
    {
        var userId = currentUserService.UserId;
        if (userId is null)
            return Result.Failure(UserErrors.Unauthorized());

        var report = await context.Set<ReportRequest>()
            .FirstOrDefaultAsync(r => r.Id == request.Id, cancellationToken);

        if (report is null)
            return Result.Failure(Error.NotFound("Report.NotFound", $"Report with ID '{request.Id}' was not found."));

        // Platform admin can delete any report; tenant user can only delete their own
        if (currentUserService.TenantId is not null && report.TenantId != currentUserService.TenantId)
            return Result.Failure(Error.Forbidden("You do not have access to this report."));

        // Delete the storage file if it exists
        if (report.FileId.HasValue)
        {
            try
            {
                await storageService.DeleteAsync(report.GetStorageKey(), cancellationToken);
            }
            catch
            {
                // Best effort — file may already be deleted
            }
        }

        context.Set<ReportRequest>().Remove(report);
        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
