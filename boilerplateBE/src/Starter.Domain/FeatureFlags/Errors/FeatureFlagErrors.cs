using Starter.Shared.Results;

namespace Starter.Domain.FeatureFlags.Errors;

public static class FeatureFlagErrors
{
    public static readonly Error NotFound = Error.NotFound(
        "FeatureFlag.NotFound",
        "The specified feature flag was not found.");

    public static readonly Error KeyAlreadyExists = Error.Conflict(
        "FeatureFlag.KeyAlreadyExists",
        "A feature flag with this key already exists.");

    public static readonly Error CannotDeleteSystemFlag = Error.Validation(
        "FeatureFlag.CannotDeleteSystemFlag",
        "System feature flags cannot be deleted.");

    public static readonly Error OverrideNotFound = Error.NotFound(
        "FeatureFlag.OverrideNotFound",
        "No tenant override found for this feature flag.");

    public static readonly Error InvalidValueForType = Error.Validation(
        "FeatureFlag.InvalidValueForType",
        "The provided value is not valid for the flag's value type.");

    public static readonly Error CannotOptOut = Error.Validation(
        "FeatureFlag.CannotOptOut",
        "Can only opt out of non-system boolean feature flags.");

    public static Error FeatureDisabled(string feature) => Error.Validation(
        "FeatureFlag.FeatureDisabled",
        $"The feature '{feature}' is not enabled for your tenant.");

    public static Error QuotaExceeded(string resource, int limit) => Error.Validation(
        "FeatureFlag.QuotaExceeded",
        $"Quota exceeded: maximum {limit} {resource} allowed for your tenant.");
}
