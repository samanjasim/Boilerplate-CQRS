namespace Starter.Abstractions.Capabilities;

/// <summary>
/// Provider for subscription lifecycle operations. Implemented by the Billing
/// module (or a real gateway adapter like Stripe). When the Billing module is
/// not installed, a Null Object registered in core throws
/// <see cref="CapabilityNotInstalledException"/> on writes and 501 surfaces
/// to the API caller.
///
/// <see cref="BillingInterval"/> is co-located in this namespace so the
/// contract is fully self-contained — <c>Starter.Abstractions</c> does not
/// reference any other project.
/// </summary>
public interface IBillingProvider : ICapability
{
    Task<CreateSubscriptionResult> CreateSubscriptionAsync(Guid tenantId, string planSlug, BillingInterval interval, CancellationToken ct = default);
    Task<ChangeSubscriptionResult> ChangeSubscriptionAsync(string externalSubscriptionId, string newPlanSlug, BillingInterval interval, CancellationToken ct = default);
    Task CancelSubscriptionAsync(string externalSubscriptionId, CancellationToken ct = default);
}

public sealed record CreateSubscriptionResult(string ExternalSubscriptionId, string ExternalCustomerId, DateTime PeriodStart, DateTime PeriodEnd);
public sealed record ChangeSubscriptionResult(DateTime NewPeriodStart, DateTime NewPeriodEnd, decimal ProratedAmount);
