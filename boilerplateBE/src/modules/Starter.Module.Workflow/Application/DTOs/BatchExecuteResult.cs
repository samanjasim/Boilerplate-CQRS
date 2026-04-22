namespace Starter.Module.Workflow.Application.DTOs;

public sealed record BatchExecuteResult(
    int Succeeded,
    int Failed,
    int Skipped,
    IReadOnlyList<BatchItemOutcome> Items);

public sealed record BatchItemOutcome(
    Guid TaskId,
    string Status, // "Succeeded" | "Failed" | "Skipped"
    string? Error);
