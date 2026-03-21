using Starter.Domain.Common;

namespace Starter.Domain.Identity.Events;

public sealed record UserUpdatedEvent(Guid UserId) : DomainEventBase;
