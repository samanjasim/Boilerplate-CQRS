using Starter.Abstractions.Capabilities;

namespace Starter.Infrastructure.Capabilities.NullObjects;

/// <summary>
/// Null implementation of <see cref="IBillingProvider"/> registered when the
/// Billing module is not installed. Every write operation throws
/// <see cref="CapabilityNotInstalledException"/>, which the global exception
/// middleware translates to HTTP 501 Not Implemented.
/// </summary>
public sealed class NullBillingProvider : IBillingProvider
{
    private const string ModuleName = "Starter.Module.Billing";

    public Task<CreateSubscriptionResult> CreateSubscriptionAsync(
        Guid tenantId,
        string planSlug,
        BillingInterval interval,
        CancellationToken ct = default) =>
        throw new CapabilityNotInstalledException(nameof(IBillingProvider), ModuleName);

    public Task<ChangeSubscriptionResult> ChangeSubscriptionAsync(
        string externalSubscriptionId,
        string newPlanSlug,
        BillingInterval interval,
        CancellationToken ct = default) =>
        throw new CapabilityNotInstalledException(nameof(IBillingProvider), ModuleName);

    public Task CancelSubscriptionAsync(string externalSubscriptionId, CancellationToken ct = default) =>
        throw new CapabilityNotInstalledException(nameof(IBillingProvider), ModuleName);
}
