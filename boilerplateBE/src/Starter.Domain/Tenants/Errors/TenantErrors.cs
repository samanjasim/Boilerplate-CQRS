using Starter.Shared.Results;

namespace Starter.Domain.Tenants.Errors;

public static class TenantErrors
{
    public static Error NotFound(Guid id) =>
        Error.NotFound("Tenant.NotFound", $"Tenant with ID '{id}' was not found.");

    public static Error NameAlreadyExists(string name) =>
        Error.Conflict("Tenant.NameAlreadyExists", $"A tenant with name '{name}' already exists.");

    public static Error SlugAlreadyExists(string slug) =>
        Error.Conflict("Tenant.SlugAlreadyExists", $"A tenant with slug '{slug}' already exists.");

    public static Error AlreadyActive() =>
        Error.Failure("Tenant.AlreadyActive", "Tenant is already active.");

    public static Error AlreadySuspended() =>
        Error.Failure("Tenant.AlreadySuspended", "Tenant is already suspended.");

    public static Error AlreadyInactive() =>
        Error.Failure("Tenant.AlreadyInactive", "Tenant is already inactive.");
}
