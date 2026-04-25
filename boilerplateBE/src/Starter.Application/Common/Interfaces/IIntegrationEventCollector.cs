namespace Starter.Application.Common.Interfaces;

/// <summary>
/// Collects integration events / messages scheduled by command handlers within a
/// request scope, for transactional outbox delivery.
///
/// Anything published via this collector is written to <c>ApplicationDbContext</c>'s
/// MassTransit outbox table during <c>SaveChangesAsync</c> — atomically with the
/// business data — so the message is delivered iff the business transaction committed.
///
/// Usage in a handler:
/// <code>
/// _eventCollector.Schedule(new TenantRegisteredEvent(tenant.Id, ...));
/// _eventCollector.Schedule(new ProcessDocumentMessage(doc.Id, ...));
/// await _context.SaveChangesAsync(ct); // both flushed atomically
/// </code>
///
/// Do NOT inject <c>IPublishEndpoint</c> directly in command handlers. The
/// MassTransit EF outbox scoped-context registration is overridden by the last
/// registered <c>AddEntityFrameworkOutbox&lt;T&gt;</c> call, which — with multiple
/// module outboxes — can route publishes to the wrong DbContext and silently drop
/// messages. This collector bypasses that ambiguity by explicitly targeting
/// <c>ApplicationDbContext</c>'s outbox in the interceptor.
///
/// The constraint is intentionally just <c>class</c>, not <c>IDomainEvent</c>:
/// cross-module command-style messages (e.g. <c>ProcessDocumentMessage</c>) need
/// the same outbox guarantees as fan-out events.
/// </summary>
public interface IIntegrationEventCollector
{
    /// <summary>Schedules <paramref name="message"/> for transactional outbox delivery.</summary>
    void Schedule<T>(T message) where T : class;
}
