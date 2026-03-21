using Starter.Shared.Results;

namespace Starter.Domain.Identity.Errors;

public static class UserErrors
{
    public static Error NotFound(Guid id) =>
        Error.NotFound("User.NotFound", $"User with ID '{id}' was not found.");

    public static Error EmailAlreadyExists(string email) =>
        Error.Conflict("User.EmailAlreadyExists", $"A user with email '{email}' already exists.");

    public static Error UsernameAlreadyExists(string username) =>
        Error.Conflict("User.UsernameAlreadyExists", $"A user with username '{username}' already exists.");

    public static Error InvalidCredentials() =>
        Error.Unauthorized("Invalid email or password.");

    public static Error AccountLocked() =>
        Error.Failure("User.AccountLocked", "Account is locked due to too many failed login attempts.");

    public static Error AccountNotActive() =>
        Error.Failure("User.AccountNotActive", "Account is not active.");

    public static Error InvalidRefreshToken() =>
        Error.Unauthorized("Invalid or expired refresh token.");

    public static Error RoleAlreadyAssigned(string roleName) =>
        Error.Conflict("User.RoleAlreadyAssigned", $"Role '{roleName}' is already assigned to this user.");

    public static Error RoleNotAssigned(string roleName) =>
        Error.NotFound("User.RoleNotAssigned", $"Role '{roleName}' is not assigned to this user.");

    public static Error InvalidCurrentPassword() =>
        Error.Validation("User.InvalidCurrentPassword", "The current password is incorrect.");
}
