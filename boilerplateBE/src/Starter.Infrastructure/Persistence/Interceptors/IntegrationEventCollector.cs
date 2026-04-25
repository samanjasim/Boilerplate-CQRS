using Starter.Application.Common.Interfaces;

namespace Starter.Infrastructure.Persistence.Interceptors;

/// <summary>
/// Scoped accumulator for integration events / messages scheduled during a
/// command handler. Both the handler (via <see cref="IIntegrationEventCollector"/>)
/// and the <see cref="IntegrationEventOutboxInterceptor"/> share the same instance
/// within the request scope.
/// </summary>
internal sealed class IntegrationEventCollector : IIntegrationEventCollector
{
    private readonly List<(object Message, Type MessageType)> _pending = [];

    public void Schedule<T>(T message) where T : class
        => _pending.Add((message, typeof(T)));

    internal IReadOnlyList<(object Message, Type MessageType)> TakeAll()
    {
        var snapshot = _pending.ToList();
        _pending.Clear();
        return snapshot;
    }
}
