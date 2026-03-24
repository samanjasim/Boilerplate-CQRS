using FluentValidation;

namespace Starter.Application.Features.Reports.Commands.DeleteReport;

public sealed class DeleteReportCommandValidator : AbstractValidator<DeleteReportCommand>
{
    public DeleteReportCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
    }
}
