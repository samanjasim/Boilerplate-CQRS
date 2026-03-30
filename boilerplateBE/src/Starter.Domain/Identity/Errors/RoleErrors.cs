using Starter.Shared.Results;

namespace Starter.Domain.Identity.Errors;

public static class RoleErrors
{
    public static Error NotFound(Guid id) =>
        Error.NotFound("Role.NotFound", $"Role with ID '{id}' was not found.");

    public static Error NotFoundByName(string name) =>
        Error.NotFound("Role.NotFound", $"Role '{name}' was not found.");

    public static Error NameAlreadyExists(string name) =>
        Error.Conflict("Role.NameAlreadyExists", $"A role with name '{name}' already exists.");

    public static Error SystemRoleCannotBeModified() =>
        Error.Failure("Role.SystemRoleCannotBeModified", "System roles cannot be modified.");

    public static Error SystemRoleCannotBeDeleted() =>
        Error.Failure("Role.SystemRoleCannotBeDeleted", "System roles cannot be deleted.");

    public static Error RoleInUse(string name) =>
        Error.Conflict("Role.InUse", $"Role '{name}' is currently assigned to users and cannot be deleted.");

    public static Error PermissionCeiling() =>
        Error.Forbidden("The requested permissions exceed your own permission set. You can only assign permissions you hold.");

    public static Error CustomRolesDisabled() =>
        Error.Failure("Role.CustomRolesDisabled", "Custom tenant roles are not enabled. Contact your platform administrator.");
}
