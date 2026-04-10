using Starter.Domain.Common;

namespace Starter.Module.Billing.Domain.Events;

/// <summary>
/// Raised by <c>TenantSubscription.Cancel</c>. Module-internal; no consumers
/// today but kept for symmetry with <see cref="SubscriptionChangedEvent"/>
/// and as a future-proofing seam.
/// </summary>
public sealed record SubscriptionCanceledEvent(
    Guid TenantId,
    Guid SubscriptionId) : DomainEventBase;
