using Starter.Application.Common.Events;
using Starter.Application.Common.Interfaces;

namespace Starter.Infrastructure.Persistence.Interceptors;

/// <summary>
/// Scoped accumulator for integration events scheduled during a command handler.
/// Both the handler (via <see cref="IIntegrationEventCollector"/>) and the
/// <see cref="IntegrationEventOutboxInterceptor"/> share the same instance within
/// the request scope.
/// </summary>
internal sealed class IntegrationEventCollector : IIntegrationEventCollector
{
    private readonly List<(object Event, Type EventType)> _pending = [];

    public void Schedule<T>(T @event) where T : class, IDomainEvent
        => _pending.Add((@event, typeof(T)));

    internal IReadOnlyList<(object Event, Type EventType)> TakeAll()
    {
        var snapshot = _pending.ToList();
        _pending.Clear();
        return snapshot;
    }
}
