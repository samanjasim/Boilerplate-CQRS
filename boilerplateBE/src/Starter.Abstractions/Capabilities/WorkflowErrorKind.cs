namespace Starter.Abstractions.Capabilities;

/// <summary>
/// Classification for a <see cref="WorkflowTaskResult"/> failure. Mirrors the values
/// of Starter.Shared.Results.ErrorType so the handler boundary can cast directly.
/// Defined here (inside Starter.Abstractions) because Starter.Abstractions must not
/// reference Starter.Shared (enforced by AbstractionsPurityTests).
/// </summary>
public enum WorkflowErrorKind
{
    Failure = 0,
    Validation = 1,
    NotFound = 2,
    Conflict = 3,
    Unauthorized = 4,
    Forbidden = 5,
}
