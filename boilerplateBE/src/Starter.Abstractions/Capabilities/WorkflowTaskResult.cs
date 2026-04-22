namespace Starter.Abstractions.Capabilities;

/// <summary>
/// Lightweight result type for <see cref="IWorkflowService.ExecuteTaskAsync"/>.
/// Defined here (inside Starter.Abstractions) because Starter.Abstractions
/// must not reference Starter.Shared (which itself references Starter.Abstractions).
/// </summary>
public sealed class WorkflowTaskResult
{
    private WorkflowTaskResult(bool isSuccess, string? errorCode, string? errorDescription,
        IReadOnlyDictionary<string, string[]>? fieldErrors = null)
    {
        IsSuccess = isSuccess;
        ErrorCode = errorCode;
        ErrorDescription = errorDescription;
        FieldErrors = fieldErrors;
    }

    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;

    /// <summary>Error code when <see cref="IsFailure"/> is true (e.g. "Workflow.TaskNotFound").</summary>
    public string? ErrorCode { get; }

    /// <summary>Human-readable error message when <see cref="IsFailure"/> is true.</summary>
    public string? ErrorDescription { get; }

    /// <summary>
    /// Field-level validation errors keyed by field name.
    /// Populated when <see cref="ErrorCode"/> is "Workflow.ValidationError".
    /// </summary>
    public IReadOnlyDictionary<string, string[]>? FieldErrors { get; }

    public static WorkflowTaskResult Success() => new(true, null, null);

    public static WorkflowTaskResult Failure(string errorCode, string errorDescription) =>
        new(false, errorCode, errorDescription);

    public static WorkflowTaskResult ValidationFailure(IReadOnlyDictionary<string, string[]> fieldErrors) =>
        new(false, "Workflow.ValidationError", "One or more form validation errors occurred.", fieldErrors);
}
