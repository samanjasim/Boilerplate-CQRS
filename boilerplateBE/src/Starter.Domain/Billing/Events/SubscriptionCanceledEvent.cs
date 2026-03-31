using Starter.Domain.Common;

namespace Starter.Domain.Billing.Events;

public sealed record SubscriptionCanceledEvent(
    Guid TenantId,
    Guid SubscriptionId) : DomainEventBase;
