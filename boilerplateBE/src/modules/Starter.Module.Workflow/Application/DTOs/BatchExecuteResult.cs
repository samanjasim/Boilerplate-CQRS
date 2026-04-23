namespace Starter.Module.Workflow.Application.DTOs;

public sealed record BatchExecuteResult(
    int Succeeded,
    int Failed,
    int Skipped,
    IReadOnlyList<BatchItemOutcome> Items);

public sealed record BatchItemOutcome(
    Guid TaskId,
    string Status,                                      // "Succeeded" | "Failed" | "Skipped"
    string? Error,                                      // Human-readable message; null on Succeeded.
    string? ErrorCode = null,                           // e.g. "Workflow.TaskNotFound" — present on typed failures.
    IReadOnlyDictionary<string, string[]>? FieldErrors = null); // Field-level validation errors if applicable.
