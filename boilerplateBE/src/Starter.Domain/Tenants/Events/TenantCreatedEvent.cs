using Starter.Domain.Common;

namespace Starter.Domain.Tenants.Events;

public sealed record TenantCreatedEvent(Guid TenantId) : DomainEventBase;
