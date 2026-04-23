using Starter.Domain.Common;

namespace Starter.Domain.Identity.Events;

public sealed record UserCreatedEvent(
    Guid UserId,
    string Email,
    string FullName,
    Guid? TenantId = null) : DomainEventBase;
