using MassTransit;
using MassTransit.Middleware.Outbox;
using Microsoft.Extensions.Options;

namespace Starter.Infrastructure.Messaging;

/// <summary>
/// Thread-safe replacement for MassTransit's stock <c>BusOutboxNotification</c>.
///
/// MassTransit 8.3.6–8.5.2 ships a <c>BusOutboxNotification</c> singleton whose
/// <c>WaitForDelivery</c> writes a shared <c>_cancellationTokenSource</c> field,
/// reads that same field without a lock to get <c>.Token</c>, and disposes it in
/// a <c>finally</c>. When more than one <c>BusOutboxDeliveryService&lt;T&gt;</c>
/// is registered — typically one per EF outbox DbContext, e.g.
/// <c>ApplicationDbContext</c> + <c>WorkflowDbContext</c> — two threads call
/// <c>WaitForDelivery</c> concurrently, the second overwrites the field, and
/// whichever thread loses the race throws NRE at
/// <c>_cancellationTokenSource.Dispose()</c> because the winner already set it
/// to null. The delivery loop then never drains the outbox, and every
/// <c>IPublishEndpoint.Publish</c> inside <c>UseBusOutbox()</c> is stuck at
/// rest.
///
/// This implementation keeps the CTS reference local to each <c>WaitForDelivery</c>
/// call, so concurrent callers never dispose each other's state. <c>Delivered()</c>
/// still cancels the currently-tracked CTS (whichever it is), which is fine for
/// the MT contract — spurious wake-ups are harmless because each delivery
/// service re-checks its own outbox table.
///
/// Registered in DI by replacing the <see cref="IBusOutboxNotification"/>
/// registration MassTransit adds inside
/// <c>AddEntityFrameworkOutbox&lt;T&gt;()</c>.
/// </summary>
public sealed class SafeBusOutboxNotification : IBusOutboxNotification
{
    private readonly object _lock = new();
    private readonly IOptions<OutboxDeliveryServiceOptions> _options;
    private CancellationTokenSource? _current;

    public SafeBusOutboxNotification(IOptions<OutboxDeliveryServiceOptions> options)
    {
        _options = options;
    }

    public async Task WaitForDelivery(CancellationToken cancellationToken)
    {
        var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        lock (_lock)
        {
            // We only track ONE CTS for Delivered() to signal. If another
            // caller is already waiting, its CTS stays intact — we just
            // overwrite the "signal target" so the newest waiter wins the
            // next Delivered(). That caller's own WaitForDelivery still
            // resolves on its QueryDelay or outer cancellation.
            _current = cts;
        }

        try
        {
            await Task.Delay(_options.Value.QueryDelay, cts.Token)
                .ContinueWith(
                    t => t,
                    CancellationToken.None,
                    TaskContinuationOptions.ExecuteSynchronously,
                    TaskScheduler.Default)
                .ConfigureAwait(false);

            cancellationToken.ThrowIfCancellationRequested();
        }
        finally
        {
            lock (_lock)
            {
                // Only clear the shared reference if it's still ours.
                if (ReferenceEquals(_current, cts))
                    _current = null;
            }

            cts.Dispose();
        }
    }

    public void Delivered()
    {
        lock (_lock)
            _current?.Cancel();
    }
}
