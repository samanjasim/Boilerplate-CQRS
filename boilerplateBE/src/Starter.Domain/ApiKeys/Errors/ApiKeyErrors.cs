using Starter.Shared.Results;

namespace Starter.Domain.ApiKeys.Errors;

public static class ApiKeyErrors
{
    public static readonly Error NotFound = Error.NotFound(
        "ApiKey.NotFound",
        "The specified API key was not found.");

    public static readonly Error AlreadyRevoked = Error.Conflict(
        "ApiKey.AlreadyRevoked",
        "API key is already revoked.");

    public static readonly Error TenantCannotCreatePlatformKey = new(
        "ApiKey.TenantCannotCreatePlatformKey",
        "Tenant users cannot create platform keys.",
        ErrorType.Validation);

    public static readonly Error PlatformAdminMustBeExplicit = new(
        "ApiKey.PlatformAdminMustBeExplicit",
        "Platform admins must explicitly create platform keys. Set isPlatformKey to true.",
        ErrorType.Validation);

    public static readonly Error CannotModifyTenantKey = new(
        "ApiKey.CannotModifyTenantKey",
        "Cannot modify tenant API keys. Tenant keys are managed by their owning tenant.",
        ErrorType.Forbidden);

    public static readonly Error UseTenantEmergencyRevoke = new(
        "ApiKey.UseTenantEmergencyRevoke",
        "Use the emergency-revoke endpoint to revoke tenant keys.",
        ErrorType.Forbidden);

    public static Error ScopeEscalation(IEnumerable<string> missing) =>
        new(
            "ApiKey.ScopeEscalation",
            $"Cannot grant scopes the caller does not hold: {string.Join(", ", missing)}.",
            ErrorType.Forbidden);
}
