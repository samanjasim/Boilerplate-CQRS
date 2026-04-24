using System.Diagnostics;
using MassTransit;
using MassTransit.EntityFrameworkCoreIntegration;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Starter.Infrastructure.Persistence;

namespace Starter.Infrastructure.Persistence.Interceptors;

/// <summary>
/// Writes integration events to the <c>ApplicationDbContext</c> MassTransit outbox table
/// atomically with the business data during <see cref="SaveChangesInterceptor.SavingChangesAsync"/>.
///
/// <para>
/// <b>Why not inject <c>IPublishEndpoint</c> directly?</b><br/>
/// When two <c>AddEntityFrameworkOutbox&lt;T&gt;</c> calls are registered (e.g. for
/// <c>ApplicationDbContext</c> and <c>WorkflowDbContext</c>), each call replaces the
/// <c>IScopedBusContextProvider&lt;IBus&gt;</c> DI registration — last writer wins. In
/// practice the Workflow module registers second, so <c>IPublishEndpoint</c> resolved
/// in a handler scope routes messages through <c>WorkflowDbContext</c>'s outbox.
/// Since <c>WorkflowDbContext.SaveChangesAsync</c> is never called in a core command
/// handler, those outbox rows are never persisted and the event is silently dropped.
/// </para>
///
/// <para>
/// <b>The fix:</b><br/>
/// Resolve <c>EntityFrameworkScopedBusContextProvider&lt;IBus, ApplicationDbContext&gt;</c>
/// lazily from <c>IServiceProvider</c> inside <see cref="SavingChangesAsync"/>.
/// MassTransit registers each concrete provider independently (via <c>TryAddScoped</c>)
/// so the Application provider survives the Workflow module's override of the abstract
/// <c>IScopedBusContextProvider&lt;IBus&gt;</c>. Lazy resolution also avoids the
/// circular-dependency risk that would arise if the provider were injected at
/// constructor time (it depends on <c>ApplicationDbContext</c>, which hasn't finished
/// constructing yet when interceptors are first resolved).
///
/// The interceptor's <c>SavingChangesAsync</c> fires before EF generates any SQL; outbox
/// rows added here are tracked on the same <c>ApplicationDbContext</c> instance and
/// committed in one atomic write.
/// </para>
/// </summary>
internal sealed class IntegrationEventOutboxInterceptor : SaveChangesInterceptor
{
    private readonly IntegrationEventCollector _collector;
    private readonly IServiceProvider _serviceProvider;

    public IntegrationEventOutboxInterceptor(
        IntegrationEventCollector collector,
        IServiceProvider serviceProvider)
    {
        _collector = collector;
        _serviceProvider = serviceProvider;
    }

    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        var pending = _collector.TakeAll();

        if (pending.Count > 0)
        {
            // Resolve lazily: at SavingChangesAsync time ApplicationDbContext is already
            // live in scope, so the provider can safely receive it without circular dep.
            var busContextProvider = _serviceProvider
                .GetRequiredService<EntityFrameworkScopedBusContextProvider<IBus, ApplicationDbContext>>();

            var publishEndpoint = busContextProvider.Context.PublishEndpoint;
            var conversationId = DeriveConversationIdFromActivity();

            foreach (var (evt, evtType) in pending)
            {
                if (conversationId.HasValue)
                {
                    await publishEndpoint.Publish(evt, evtType, ctx =>
                    {
                        // Every event scheduled during one HTTP request shares a
                        // ConversationId derived from the originating trace. This
                        // lets consumers, log aggregators, and MT's built-in traces
                        // group the causal chain even when OpenTelemetry is off.
                        // If a caller set CorrelationId upstream, preserve it.
                        ctx.ConversationId ??= conversationId;
                        ctx.CorrelationId ??= conversationId;
                    }, cancellationToken);
                }
                else
                {
                    // Background/non-HTTP context (e.g. hosted service) — let MT
                    // assign a fresh Guid. Still at-least-once-safe, just not
                    // correlatable to an HTTP request.
                    await publishEndpoint.Publish(evt, evtType, cancellationToken);
                }
            }
        }

        return await base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    /// <summary>
    /// Projects the current <see cref="Activity"/>'s 16-byte W3C TraceId into a
    /// deterministic <see cref="Guid"/> for use as a MassTransit ConversationId.
    /// Returns <c>null</c> when no ambient Activity exists (e.g. background work).
    ///
    /// The mapping is lossless from a trace-correlation perspective: two calls
    /// within the same HTTP request observe the same Activity and therefore
    /// produce the same ConversationId.
    /// </summary>
    internal static Guid? DeriveConversationIdFromActivity() =>
        Activity.Current is { } activity
            ? DeriveConversationIdFromTraceId(activity.TraceId)
            : null;

    /// <summary>
    /// Pure function variant for unit testing without an ambient <see cref="Activity"/>.
    /// </summary>
    internal static Guid DeriveConversationIdFromTraceId(ActivityTraceId traceId)
    {
        Span<byte> traceIdBytes = stackalloc byte[16];
        traceId.CopyTo(traceIdBytes);
        return new Guid(traceIdBytes);
    }
}
