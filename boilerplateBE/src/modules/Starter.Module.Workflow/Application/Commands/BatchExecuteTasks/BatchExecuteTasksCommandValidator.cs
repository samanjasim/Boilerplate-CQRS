using FluentValidation;

namespace Starter.Module.Workflow.Application.Commands.BatchExecuteTasks;

public sealed class BatchExecuteTasksCommandValidator : AbstractValidator<BatchExecuteTasksCommand>
{
    public BatchExecuteTasksCommandValidator()
    {
        RuleFor(x => x.TaskIds)
            .NotEmpty().WithMessage("At least one task id is required.")
            .Must(ids => ids.Count <= 50).WithMessage("Bulk action supports at most 50 tasks per request.");

        RuleFor(x => x.Action)
            .NotEmpty().WithMessage("Action is required.")
            .MaximumLength(100);

        RuleFor(x => x.Comment)
            .MaximumLength(2000);
    }
}
