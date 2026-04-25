using Starter.Application.Common.Interfaces;

namespace Starter.Infrastructure.Services;

/// <summary>
/// <see cref="IMessagePublisher"/> implementation that schedules the message on
/// the request-scoped <see cref="IIntegrationEventCollector"/>. The
/// <c>IntegrationEventOutboxInterceptor</c> drains the collector during
/// <c>ApplicationDbContext.SaveChangesAsync</c>, so the message lands in the
/// outbox table atomically with the originating handler's business data.
///
/// <para>
/// <b>Why not <c>IPublishEndpoint</c> directly?</b><br/>
/// Same reason as the broader collector pattern: with multiple
/// <c>AddEntityFrameworkOutbox&lt;T&gt;()</c> registrations,
/// <c>IPublishEndpoint</c> resolves to whichever DbContext registered last,
/// which silently drops messages from handlers that don't save through that
/// context. Routing through the collector forces the message into
/// <c>ApplicationDbContext</c>'s outbox unambiguously.
/// </para>
///
/// <para>
/// <b>Caller contract:</b> the originating command handler (or a MediatR
/// notification fired during its <c>SaveChangesAsync</c>) must drive
/// <c>ApplicationDbContext.SaveChangesAsync</c> at some point in the same DI
/// scope — that's when the interceptor runs and the message is committed.
/// In practice every caller already does, because publishing without an
/// associated save makes no sense.
/// </para>
/// </summary>
public sealed class MassTransitMessagePublisher(IIntegrationEventCollector eventCollector) : IMessagePublisher
{
    public Task PublishAsync<T>(T message, CancellationToken cancellationToken = default) where T : class
    {
        eventCollector.Schedule(message);
        return Task.CompletedTask;
    }
}
