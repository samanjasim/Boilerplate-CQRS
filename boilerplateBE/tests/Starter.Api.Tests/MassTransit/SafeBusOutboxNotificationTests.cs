using FluentAssertions;
using MassTransit;
using Microsoft.Extensions.Options;
using Starter.Infrastructure.Messaging;
using Xunit;

namespace Starter.Api.Tests.MassTransit;

/// <summary>
/// Regression for a race in MassTransit's stock <c>BusOutboxNotification</c>
/// (present in 8.3.6 through 8.5.2 at time of writing). When two
/// <c>BusOutboxDeliveryService&lt;T&gt;</c> instances share the notification
/// singleton — e.g. one for <c>ApplicationDbContext</c> and one for
/// <c>WorkflowDbContext</c> — their concurrent <c>WaitForDelivery</c> calls
/// stomp a shared <c>_cancellationTokenSource</c> field and the slower caller
/// throws NRE at <c>_cancellationTokenSource.Dispose()</c> because the faster
/// caller already nulled it.
///
/// <see cref="SafeBusOutboxNotification"/> keeps a local reference to the
/// per-call CTS so each caller only disposes its own.
/// </summary>
public sealed class SafeBusOutboxNotificationTests
{
    private static SafeBusOutboxNotification NewSut(TimeSpan? queryDelay = null)
    {
        var options = Options.Create(new OutboxDeliveryServiceOptions
        {
            QueryDelay = queryDelay ?? TimeSpan.FromMilliseconds(200),
        });
        return new SafeBusOutboxNotification(options);
    }

    [Fact]
    public async Task WaitForDelivery_Returns_When_QueryDelay_Elapses()
    {
        var sut = NewSut(queryDelay: TimeSpan.FromMilliseconds(50));

        var act = async () => await sut.WaitForDelivery(CancellationToken.None);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task WaitForDelivery_Returns_Early_When_Delivered_Is_Signalled()
    {
        var sut = NewSut(queryDelay: TimeSpan.FromSeconds(30));

        var waitTask = sut.WaitForDelivery(CancellationToken.None);
        await Task.Delay(50);
        sut.Delivered();

        // If Delivered() cancels the internal CTS, WaitForDelivery should
        // complete well before the 30-second QueryDelay.
        await waitTask.WaitAsync(TimeSpan.FromSeconds(2));
    }

    [Fact]
    public async Task WaitForDelivery_Does_Not_NRE_With_Concurrent_Callers()
    {
        // This is the regression. With stock BusOutboxNotification, running
        // many concurrent WaitForDelivery calls reliably reproduces the NRE
        // on Dispose because the slower caller reads a null field set by the
        // faster caller's finally.
        var sut = NewSut(queryDelay: TimeSpan.FromMilliseconds(50));

        var tasks = Enumerable.Range(0, 16)
            .Select(_ => Task.Run(() => sut.WaitForDelivery(CancellationToken.None)))
            .ToArray();

        var act = async () => await Task.WhenAll(tasks);
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task WaitForDelivery_Respects_Outer_Cancellation()
    {
        var sut = NewSut(queryDelay: TimeSpan.FromSeconds(30));
        using var cts = new CancellationTokenSource();

        var waitTask = sut.WaitForDelivery(cts.Token);
        await Task.Delay(50);
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => waitTask.WaitAsync(TimeSpan.FromSeconds(2)));
    }

    [Fact]
    public void Delivered_With_No_Waiter_Is_Noop()
    {
        var sut = NewSut();

        var act = () => sut.Delivered();

        act.Should().NotThrow();
    }

    [Fact]
    public async Task Multiple_Wait_Deliver_Cycles_Do_Not_Leak_Or_Throw()
    {
        var sut = NewSut(queryDelay: TimeSpan.FromSeconds(30));

        for (var i = 0; i < 5; i++)
        {
            var waitTask = sut.WaitForDelivery(CancellationToken.None);
            await Task.Delay(20);
            sut.Delivered();
            await waitTask.WaitAsync(TimeSpan.FromSeconds(2));
        }
    }
}
