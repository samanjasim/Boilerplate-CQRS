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
/// <b>Caller contract:</b> publish must be scheduled BEFORE the originating
/// <c>SaveChangesAsync</c> on <c>ApplicationDbContext</c>. The legacy
/// "Save then Publish" order silently dropped messages even with the old
/// <c>IPublishEndpoint</c> implementation when multiple EF outboxes were
/// registered. The current implementation surfaces the bug consistently:
/// callers must flip to <c>Publish → Save</c>. Notification / event
/// handlers fired during the outer <c>SavingChangesAsync</c> are correct
/// by construction — the outer save flushes the collector after they return.
/// </para>
///
/// <para>
/// <b>Why not <c>IPublishEndpoint</c> directly?</b> MT 8.x's
/// <c>UseBusOutbox()</c> uses <c>ReplaceScoped&lt;IScopedBusContextProvider&lt;IBus&gt;,
/// EntityFrameworkScopedBusContextProvider&lt;IBus, TDbContext&gt;&gt;()</c> —
/// the abstract slot is replaced on every <c>AddEntityFrameworkOutbox&lt;T&gt;</c>
/// call. With multiple outboxes (core + Workflow), the last call wins and
/// <c>IPublishEndpoint</c> would route messages through the wrong DbContext,
/// silently dropping them. The collector → interceptor path explicitly
/// targets <c>ApplicationDbContext</c>'s outbox.
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
