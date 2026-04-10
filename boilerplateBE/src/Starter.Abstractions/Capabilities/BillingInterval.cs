namespace Starter.Abstractions.Capabilities;

/// <summary>
/// Billing cycle for a subscription. Used by <see cref="IBillingProvider"/>
/// and by the Billing module's <c>TenantSubscription</c> entity.
///
/// Co-located with <see cref="IBillingProvider"/> so the capability contract
/// is fully self-contained — <c>Starter.Abstractions</c> doesn't have to
/// reference any module's domain project.
/// </summary>
public enum BillingInterval { Monthly = 0, Annual = 1 }
