namespace Starter.Abstractions.Capabilities;

/// <summary>
/// Lightweight result type for <see cref="IWorkflowService.ExecuteTaskAsync"/>.
/// Defined here (inside Starter.Abstractions) because Starter.Abstractions
/// must not reference Starter.Shared (which itself references Starter.Abstractions).
/// The MediatR handler adapts this to Starter.Shared.Results.Result&lt;bool&gt; at the
/// module boundary, using <see cref="Kind"/> to pick the right ErrorType.
/// </summary>
public sealed class WorkflowTaskResult
{
    public const string ValidationErrorCode = "Workflow.ValidationError";

    private WorkflowTaskResult(
        bool isSuccess,
        WorkflowErrorKind kind,
        string? errorCode,
        string? errorDescription,
        IReadOnlyDictionary<string, string[]>? fieldErrors = null)
    {
        IsSuccess = isSuccess;
        Kind = kind;
        ErrorCode = errorCode;
        ErrorDescription = errorDescription;
        FieldErrors = fieldErrors;
    }

    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;

    /// <summary>Classification for the failure; drives the HTTP status at the boundary.</summary>
    public WorkflowErrorKind Kind { get; }

    /// <summary>Error code when <see cref="IsFailure"/> is true (e.g. "Workflow.TaskNotFound").</summary>
    public string? ErrorCode { get; }

    /// <summary>Human-readable error message when <see cref="IsFailure"/> is true.</summary>
    public string? ErrorDescription { get; }

    /// <summary>
    /// Field-level validation errors keyed by field name.
    /// Populated when <see cref="Kind"/> is <see cref="WorkflowErrorKind.Validation"/> and
    /// the failure came from form-data validation.
    /// </summary>
    public IReadOnlyDictionary<string, string[]>? FieldErrors { get; }

    public static WorkflowTaskResult Success() =>
        new(true, WorkflowErrorKind.Failure, null, null);

    public static WorkflowTaskResult Failure(string code, string description, WorkflowErrorKind kind) =>
        new(false, kind, code, description);

    public static WorkflowTaskResult ValidationFailure(IReadOnlyDictionary<string, string[]> fieldErrors) =>
        new(false, WorkflowErrorKind.Validation,
            ValidationErrorCode,
            "One or more form validation errors occurred.",
            fieldErrors);
}
