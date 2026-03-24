using FluentValidation;
using Starter.Domain.Common.Enums;

namespace Starter.Application.Features.Reports.Commands.RequestReport;

public sealed class RequestReportCommandValidator : AbstractValidator<RequestReportCommand>
{
    public RequestReportCommandValidator()
    {
        RuleFor(x => x.ReportType)
            .NotEmpty().WithMessage("Report type is required.")
            .Must(name => ReportType.FromName(name) is not null)
            .WithMessage("Invalid report type. Valid values: AuditLogs, Users, Files.");

        RuleFor(x => x.Format)
            .NotEmpty().WithMessage("Report format is required.")
            .Must(name => name == "Csv" || name == "Pdf")
            .WithMessage("Invalid report format. Valid values: Csv, Pdf.");
    }
}
