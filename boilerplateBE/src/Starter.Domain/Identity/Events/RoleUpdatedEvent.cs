using Starter.Domain.Common;

namespace Starter.Domain.Identity.Events;

public sealed record RoleUpdatedEvent(Guid RoleId, Guid? TenantId, string Name) : DomainEventBase;
