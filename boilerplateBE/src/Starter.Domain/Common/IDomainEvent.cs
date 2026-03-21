using MediatR;

namespace Starter.Domain.Common;

public interface IDomainEvent : INotification
{
    DateTime OccurredOn { get; }
}

public abstract record DomainEventBase : IDomainEvent
{
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
    public Guid EventId { get; } = Guid.NewGuid();
}
