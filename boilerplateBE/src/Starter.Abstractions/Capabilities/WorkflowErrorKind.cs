namespace Starter.Abstractions.Capabilities;

/// <summary>
/// Classification for a <see cref="WorkflowTaskResult"/> outcome. The MediatR
/// handler adapts this to Starter.Shared.Results.ErrorType at the module
/// boundary via an explicit mapping — numeric parity is NOT assumed, so
/// either enum can be re-ordered safely.
/// Defined here (inside Starter.Abstractions) because Starter.Abstractions
/// must not reference Starter.Shared (enforced by AbstractionsPurityTests).
/// </summary>
public enum WorkflowErrorKind
{
    /// <summary>Used only on <see cref="WorkflowTaskResult.Success"/> — the result carries no error.</summary>
    None = 0,
    Failure = 1,
    Validation = 2,
    NotFound = 3,
    Conflict = 4,
    Unauthorized = 5,
    Forbidden = 6,
}
