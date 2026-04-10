using Starter.Domain.Common;

namespace Starter.Module.Billing.Domain.Events;

/// <summary>
/// Raised by <c>TenantSubscription</c> when a subscription is created, its
/// plan changes, or its features need resyncing. Consumed intra-module by
/// <c>SyncPlanFeaturesHandler</c> to update tenant feature flag overrides.
///
/// Module-internal: no module outside Billing references this event. Cross-
/// module side effects (e.g. webhook dispatch) happen via direct
/// <c>IWebhookPublisher.PublishAsync</c> calls in the Billing command
/// handlers, not via cross-module event subscription.
/// </summary>
public sealed record SubscriptionChangedEvent(
    Guid TenantId,
    Guid? OldPlanId,
    Guid NewPlanId) : DomainEventBase;
