using Starter.Domain.Common;

namespace Starter.Domain.Identity.Events;

public sealed record RoleCreatedEvent(Guid RoleId, Guid? TenantId, string Name) : DomainEventBase;
