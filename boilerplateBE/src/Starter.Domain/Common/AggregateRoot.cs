namespace Starter.Domain.Common;

public abstract class AggregateRoot<TId> : BaseAuditableEntity<TId> where TId : notnull
{
    private readonly List<IDomainEvent> _domainEvents = [];

    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    protected AggregateRoot() : base() { }
    protected AggregateRoot(TId id) : base(id) { }

    protected void RaiseDomainEvent(IDomainEvent domainEvent)
    {
        _domainEvents.Add(domainEvent);
    }

    public void ClearDomainEvents()
    {
        _domainEvents.Clear();
    }

    public IReadOnlyCollection<IDomainEvent> GetDomainEventsAndClear()
    {
        var events = _domainEvents.ToList();
        _domainEvents.Clear();
        return events.AsReadOnly();
    }
}

public abstract class AggregateRoot : AggregateRoot<Guid>
{
    protected AggregateRoot() : base() { }
    protected AggregateRoot(Guid id) : base(id) { }
}
