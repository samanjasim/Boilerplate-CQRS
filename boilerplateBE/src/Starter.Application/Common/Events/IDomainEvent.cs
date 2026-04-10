namespace Starter.Application.Common.Events;

/// <summary>
/// Marker interface for cross-module domain events.
///
/// Domain events are published via MassTransit's transactional outbox after a
/// successful command. Modules subscribe by implementing
/// <c>IConsumer&lt;TEvent&gt;</c>; MassTransit's assembly scanning discovers
/// them at startup.
///
/// Events are reusable contracts owned by core. Modules may define their own
/// events in their <c>Contracts/Events/</c> folder, but cross-module events
/// that core publishes belong here.
/// </summary>
public interface IDomainEvent
{
    DateTime OccurredAt { get; }
}
