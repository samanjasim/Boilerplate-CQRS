using Starter.Shared.Results;

namespace Starter.Domain.Common.Access.Errors;

public static class AccessErrors
{
    public static readonly Error ResourceNotFound = Error.NotFound(
        "Access.ResourceNotFound",
        "Resource not found or inaccessible.");

    public static readonly Error GrantNotFound = Error.NotFound(
        "Access.GrantNotFound",
        "Grant not found.");

    public static readonly Error SubjectNotFound = Error.Validation(
        "Access.SubjectNotFound",
        "Grant target not found.");

    public static readonly Error SubjectInactive = Error.Validation(
        "Access.SubjectInactive",
        "Grant target is inactive.");

    public static readonly Error CrossTenantGrantBlocked = new(
        "Access.CrossTenantGrantBlocked",
        "Cannot grant access across tenants.",
        ErrorType.Forbidden);

    public static readonly Error SelfGrantBlocked = Error.Conflict(
        "Access.SelfGrantBlocked",
        "Owners already have full access.");

    public static readonly Error InsufficientLevelToGrant = new(
        "Access.InsufficientLevelToGrant",
        "You cannot grant a higher level than you have.",
        ErrorType.Forbidden);

    public static readonly Error VisibilityNotAllowedForResourceType = Error.Validation(
        "Access.VisibilityNotAllowedForResourceType",
        "This visibility is not allowed for this resource type.");

    public static readonly Error OnlyOwnerCanPerform = new(
        "Access.OnlyOwnerCanPerform",
        "Only the owner can perform this action.",
        ErrorType.Forbidden);

    public static readonly Error OwnershipTargetNotInTenant = Error.Validation(
        "Access.OwnershipTargetNotInTenant",
        "New owner must be in the same tenant.");

    public static readonly Error OwnershipTargetInactive = Error.Validation(
        "Access.OwnershipTargetInactive",
        "New owner must be active.");
}
