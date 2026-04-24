using Starter.Shared.Results;

namespace Starter.Module.AI.Domain.Errors;

public static class PersonaErrors
{
    public static Error NotFound =>
        Error.NotFound("Persona.NotFound", "Persona not found.");

    public static Error NotAssignedToUser =>
        new("Persona.NotAssignedToUser", "You do not have this persona assigned.", ErrorType.Forbidden);

    public static Error RequiresAuthentication =>
        new("Persona.RequiresAuthentication", "This persona is not available for anonymous access.", ErrorType.Unauthorized);

    public static Error NoDefaultForUser =>
        Error.Validation("Persona.NoDefaultForUser",
            "No default persona is configured for your account. Contact your administrator.");

    public static Error AnonymousNotAvailable =>
        Error.Validation("Persona.AnonymousNotAvailable",
            "Anonymous persona is not configured or not active for this tenant.");

    public static Error CannotDeleteSystemReserved =>
        Error.Conflict("Persona.CannotDeleteSystemReserved",
            "System-reserved personas cannot be deleted.");

    public static Error HasActiveAssignments =>
        Error.Conflict("Persona.HasActiveAssignments",
            "Cannot delete a persona with active user assignments. Reassign users first.");

    public static Error SlugReserved(string slug) =>
        Error.Validation("Persona.SlugReserved", $"The slug '{slug}' is reserved.");

    public static Error SlugAlreadyExists(string slug) =>
        Error.Conflict("Persona.SlugAlreadyExists", $"A persona with slug '{slug}' already exists.");

    public static Error AnonymousAudienceImmutable =>
        Error.Validation("Persona.AnonymousAudienceImmutable",
            "Audience type of the anonymous persona cannot be changed.");

    public static Error CannotRemoveLastAssignment =>
        Error.Validation("Persona.CannotRemoveLastAssignment",
            "Cannot unassign the user's only persona. Assign another first.");

    public static Error AlreadyAssigned =>
        Error.Conflict("Persona.AlreadyAssigned", "User already has this persona assigned.");

    public static Error UserNotInTenant =>
        Error.Validation("Persona.UserNotInTenant",
            "Target user does not belong to this tenant.");

    public static Error AnonymousAlreadyExists =>
        Error.Conflict("Persona.AnonymousAlreadyExists",
            "An anonymous persona already exists for this tenant.");

    public static Error AudienceAnonymousReserved =>
        Error.Validation("Persona.AudienceAnonymousReserved",
            "Anonymous audience is reserved for the system-managed anonymous persona.");

    public static Error InvalidSlug =>
        Error.Validation("Persona.InvalidSlug",
            "Slug must be lowercase kebab-case: letters, digits, and hyphens only.");

    public static Error NotActive =>
        Error.Validation("Persona.NotActive", "Persona is inactive.");
}
