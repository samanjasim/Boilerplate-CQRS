using Starter.Application.Common.Events;

namespace Starter.Application.Common.Interfaces;

/// <summary>
/// Collects integration events scheduled by command handlers within a request scope.
///
/// Events are written atomically to the MassTransit outbox table as part of the same
/// <c>SaveChangesAsync</c> call that persists the business data, guaranteeing that
/// an event is published if and only if the business transaction committed.
///
/// Usage in a handler:
/// <code>
/// _eventCollector.Schedule(new TenantRegisteredEvent(tenant.Id, ...));
/// await _context.SaveChangesAsync(ct); // events flushed automatically
/// </code>
///
/// Do NOT inject <c>IPublishEndpoint</c> directly in command handlers for cross-module
/// integration events. The MassTransit EF outbox scoped-context registration is
/// overridden by the last registered <c>AddEntityFrameworkOutbox&lt;T&gt;</c> call,
/// which — with multiple module outboxes — can route publishes to the wrong DbContext
/// and silently drop messages. This collector bypasses that ambiguity by explicitly
/// targeting <c>ApplicationDbContext</c>'s outbox in the interceptor.
/// </summary>
public interface IIntegrationEventCollector
{
    /// <summary>Schedules <paramref name="event"/> for transactional outbox delivery.</summary>
    void Schedule<T>(T @event) where T : class, IDomainEvent;
}
