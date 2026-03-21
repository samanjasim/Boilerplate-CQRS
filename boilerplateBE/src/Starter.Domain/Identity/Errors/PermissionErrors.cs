using Starter.Shared.Results;

namespace Starter.Domain.Identity.Errors;

public static class PermissionErrors
{
    public static Error NotFound(Guid id) =>
        Error.NotFound("Permission.NotFound", $"Permission with ID '{id}' was not found.");

    public static Error NotFoundByName(string name) =>
        Error.NotFound("Permission.NotFound", $"Permission '{name}' was not found.");

    public static Error NameAlreadyExists(string name) =>
        Error.Conflict("Permission.NameAlreadyExists", $"A permission with name '{name}' already exists.");
}
