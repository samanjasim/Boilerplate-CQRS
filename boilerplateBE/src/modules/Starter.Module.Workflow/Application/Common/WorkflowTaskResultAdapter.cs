using Starter.Abstractions.Capabilities;
using Starter.Shared.Results;

namespace Starter.Module.Workflow.Application.Common;

/// <summary>
/// Maps <see cref="WorkflowTaskResult"/> (the capability-boundary shape) to
/// <see cref="Result{T}"/> / <see cref="Error"/> used by MediatR handlers.
/// Isolates the enum translation so re-ordering either side never silently
/// mis-translates — an unmapped <see cref="WorkflowErrorKind"/> triggers a
/// compile-ish failure via the switch exhaustiveness path (throws at runtime,
/// caught by a unit test).
/// </summary>
internal static class WorkflowTaskResultAdapter
{
    public static ErrorType ToErrorType(WorkflowErrorKind kind) => kind switch
    {
        WorkflowErrorKind.Failure => ErrorType.Failure,
        WorkflowErrorKind.Validation => ErrorType.Validation,
        WorkflowErrorKind.NotFound => ErrorType.NotFound,
        WorkflowErrorKind.Conflict => ErrorType.Conflict,
        WorkflowErrorKind.Unauthorized => ErrorType.Unauthorized,
        WorkflowErrorKind.Forbidden => ErrorType.Forbidden,
        WorkflowErrorKind.None => throw new InvalidOperationException(
            "WorkflowErrorKind.None is only valid on Success results and must not be adapted to an ErrorType."),
        _ => throw new InvalidOperationException(
            $"Unhandled WorkflowErrorKind: {kind}. Add the mapping in {nameof(WorkflowTaskResultAdapter)}."),
    };

    public static Result<bool> ToResult(WorkflowTaskResult wfResult)
    {
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

        return Result.Failure<bool>(new Error(
            wfResult.ErrorCode!,
            wfResult.ErrorDescription!,
            ToErrorType(wfResult.Kind)));
    }
}
