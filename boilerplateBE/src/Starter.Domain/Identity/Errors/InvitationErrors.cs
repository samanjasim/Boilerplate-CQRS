using Starter.Shared.Results;

namespace Starter.Domain.Identity.Errors;

public static class InvitationErrors
{
    public static Error NotFound(Guid id) =>
        Error.NotFound("Invitation.NotFound", $"Invitation with ID '{id}' was not found.");

    public static Error NotFoundByToken() =>
        Error.NotFound("Invitation.NotFound", "Invitation not found or has already been used.");

    public static Error AlreadyAccepted() =>
        Error.Failure("Invitation.AlreadyAccepted", "This invitation has already been accepted.");

    public static Error Expired() =>
        Error.Failure("Invitation.Expired", "This invitation has expired.");

    public static Error EmailAlreadyInvited(string email) =>
        Error.Conflict("Invitation.EmailAlreadyInvited", $"A pending invitation already exists for '{email}'.");

    public static Error InvalidToken() =>
        Error.Validation("Invitation.InvalidToken", "The invitation token is invalid or expired.");

    public static Error RoleNotFound(Guid roleId) =>
        Error.NotFound("Invitation.RoleNotFound", $"Role with ID '{roleId}' was not found.");

    public static Error TenantRequired() =>
        Error.Validation("Invitation.TenantRequired", "You must belong to a tenant to invite users.");
}
