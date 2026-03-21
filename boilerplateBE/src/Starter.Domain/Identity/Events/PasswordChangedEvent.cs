using Starter.Domain.Common;

namespace Starter.Domain.Identity.Events;

public sealed record PasswordChangedEvent(Guid UserId) : DomainEventBase;
