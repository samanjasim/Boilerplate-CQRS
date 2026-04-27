using Starter.Shared.Results;

namespace Starter.Module.AI.Domain.Errors;

public static class PendingApprovalErrors
{
    public static Error NotFound(Guid id) =>
        Error.NotFound("PendingApproval.NotFound", $"Pending approval '{id}' not found.");

    public static readonly Error NotPending =
        Error.Conflict("PendingApproval.NotPending",
            "Pending approval is no longer in the Pending state (already approved, denied, or expired).");

    public static Error ToolUnavailable(string commandTypeName) =>
        Error.Failure("PendingApproval.ToolUnavailable",
            $"The MediatR command type '{commandTypeName}' could not be resolved at approval time.");

    public static readonly Error DenyReasonRequired =
        Error.Validation("PendingApproval.DenyReasonRequired",
            "A reason must be provided when denying a pending approval.");

    public static readonly Error AccessDenied =
        Error.Forbidden("Caller is not permitted to view or act on this pending approval.");
}
