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

    public static Error PermissionEscalation() =>
        Error.Forbidden("The target role has permissions that exceed your own. You cannot assign a role with more privileges than you have.");

    public static Error SuperAdminOnly() =>
        Error.Forbidden("Only a SuperAdmin can assign the SuperAdmin role.");

    public static Error TenantNotFound(Guid tenantId) =>
        Error.NotFound("Invitation.TenantNotFound", $"Tenant with ID '{tenantId}' was not found.");
}
