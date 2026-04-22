using MediatR;
using Starter.Abstractions.Capabilities;
using Starter.Application.Common.Interfaces;
using Starter.Shared.Results;

namespace Starter.Module.Workflow.Application.Commands.ExecuteTask;

internal sealed class ExecuteTaskCommandHandler(
    IWorkflowService workflowService,
    ICurrentUserService currentUser) : IRequestHandler<ExecuteTaskCommand, Result<bool>>
{
    public async Task<Result<bool>> Handle(ExecuteTaskCommand request, CancellationToken cancellationToken)
    {
        var wfResult = await workflowService.ExecuteTaskAsync(
            request.TaskId,
            request.Action,
            request.Comment,
            currentUser.UserId!.Value,
            request.FormData,
            cancellationToken);

        if (wfResult.IsSuccess)
            return Result.Success(true);

        if (wfResult.FieldErrors is not null)
        {
            var validationErrors = new ValidationErrors();
            foreach (var (field, messages) in wfResult.FieldErrors)
                foreach (var msg in messages)
                    validationErrors.Add(field, msg);
            return Result.ValidationFailure<bool>(validationErrors);
        }

        return Result.Failure<bool>(
            Error.Failure(wfResult.ErrorCode!, wfResult.ErrorDescription!));
    }
}
