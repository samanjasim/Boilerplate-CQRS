using Starter.Domain.Common;

namespace Starter.Domain.Billing.Events;

public sealed record SubscriptionChangedEvent(
    Guid TenantId,
    Guid? OldPlanId,
    Guid NewPlanId) : DomainEventBase;
