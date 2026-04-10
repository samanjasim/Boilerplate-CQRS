using Starter.Shared.Results;

namespace Starter.Module.Billing.Domain.Errors;

public static class BillingErrors
{
    public static readonly Error PlanNotFound = Error.NotFound(
        "Billing.PlanNotFound",
        "The specified subscription plan was not found.");

    public static readonly Error PlanNotActive = Error.Validation(
        "Billing.PlanNotActive",
        "The specified subscription plan is not active.");

    public static readonly Error SubscriptionNotFound = Error.NotFound(
        "Billing.SubscriptionNotFound",
        "The specified subscription was not found.");

    public static readonly Error AlreadyOnPlan = Error.Validation(
        "Billing.AlreadyOnPlan",
        "The tenant is already subscribed to this plan.");

    public static readonly Error SlugAlreadyExists = Error.Conflict(
        "Billing.SlugAlreadyExists",
        "A subscription plan with this slug already exists.");

    public static readonly Error CannotDeactivateWithSubscribers = Error.Validation(
        "Billing.CannotDeactivateWithSubscribers",
        "Cannot deactivate a plan that has active subscribers.");

    public static readonly Error FreePlanRequired = Error.Validation(
        "Billing.FreePlanRequired",
        "A free plan is required for this operation.");

    public static readonly Error CannotCancelFreePlan = Error.Validation(
        "Billing.CannotCancelFreePlan",
        "A free plan subscription cannot be canceled.");

    public static Error InvalidFeatureKeys(List<string> keys) =>
        Error.Validation("Billing.InvalidFeatureKeys",
            $"Feature flag keys do not exist: {string.Join(", ", keys)}");
}
